using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using DendroKit.Core.Export;
using DendroKit.Core.Mesh;
using DendroKit.Core.Params;
using DendroKit.Core.Tree;

using WinOpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WinSaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WpfMessageBox    = System.Windows.MessageBox;

namespace DendroKit.WpfApp;

public partial class MainWindow : Window
{
    // ── core state ──────────────────────────────────────────────────────
    private TreeParams            _params      = new();
    private TreeImpl?             _tree;
    private TreeMesh?             _mesh;
    private string?               _currentFile;
    private bool                  _needsCameraReset = true;

    // ── UI helpers ───────────────────────────────────────────────────────
    private readonly TreeGLControl        _gl         = new();
    private readonly DispatcherTimer      _regenTimer;
    private          List<ParamViewModel> _allVms     = new();
    private          List<string>         _groupNames = new();
    private readonly List<int>            _visibleLevels = new();
    private          bool                 _isGenerating;
    private          bool                 _pendingGenerate;
    private          bool                 _isSliderDragActive;

    // groups that have per-level params (0..3)
    private static readonly HashSet<string> LevelGroups =
        new(StringComparer.OrdinalIgnoreCase)
        { "LENTAPER", "SPLITTING", "CURVATURE", "BRANCHING" };

    private static readonly string[] LevelLabels =
    {
        "0 – törzs",
        "1 – ág",
        "2 – hajtás",
        "3 – részlet",
    };

    private int _selectedLevel;

    public MainWindow()
    {
        InitializeComponent();
        GlHost.Child = _gl;

        _regenTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _regenTimer.Tick += async (_, _) => { _regenTimer.Stop(); await GenerateAsync(); };

        LoadParams(_params);

        // keyboard shortcuts
        CommandBindings.Add(new CommandBinding(
            new RoutedCommand("Open",   typeof(MainWindow),
                new InputGestureCollection { new KeyGesture(Key.O, ModifierKeys.Control) }),
            (_, _) => OnOpen(this, new RoutedEventArgs())));
        CommandBindings.Add(new CommandBinding(
            new RoutedCommand("Save",   typeof(MainWindow),
                new InputGestureCollection { new KeyGesture(Key.S, ModifierKeys.Control) }),
            (_, _) => OnSave(this, new RoutedEventArgs())));
        CommandBindings.Add(new CommandBinding(
            new RoutedCommand("Generate", typeof(MainWindow),
                new InputGestureCollection { new KeyGesture(Key.F5) }),
            (_, _) => OnGenerate(this, new RoutedEventArgs())));
    }

    // ── param loading ────────────────────────────────────────────────────

    private void LoadParams(TreeParams p)
    {
        foreach (var vm in _allVms) vm.ValueChanged -= OnParamChanged;

        p.EnableDisable();

        _allVms = p.ParamDb.Values
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name)
            .Select(x => new ParamViewModel(x))
            .ToList();

        foreach (var vm in _allVms) vm.ValueChanged += OnParamChanged;

        RefreshGroupList(preferredGroup: null);
    }

    private void RefreshAllVms()
    {
        foreach (var vm in _allVms) vm.ReadFromParam();
    }

    private void RefreshGroupList(string? preferredGroup)
    {
        string? targetGroup = preferredGroup ?? CmbGroup.SelectedItem as string;

        _groupNames = _allVms
            .GroupBy(v => v.Group)
            .OrderBy(g => g.Min(v => v.Order))
            .Select(g => g.Key)
            .ToList();

        CmbGroup.ItemsSource = null;
        CmbGroup.ItemsSource = _groupNames;

        if (_groupNames.Count == 0)
        {
            ParamList.ItemsSource = null;
            LevelStrip.Visibility = Visibility.Collapsed;
            return;
        }

        string selected = targetGroup != null && _groupNames.Contains(targetGroup, StringComparer.Ordinal)
            ? targetGroup
            : _groupNames[0];

        CmbGroup.SelectedItem = selected;
        FilterParams(selected);
    }

    // ── group / level filter ─────────────────────────────────────────────

    private void OnGroupChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbGroup.SelectedItem is not string group) return;

        bool isLevel = LevelGroups.Contains(group);
        if (isLevel)
        {
            RefreshLevelSelector(group);
        }
        else
        {
            LevelStrip.Visibility = Visibility.Collapsed;
            _visibleLevels.Clear();
        }

        FilterParams(group);
    }

    private void OnLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstLevel.SelectedIndex < 0) return;
        if (LstLevel.SelectedIndex >= _visibleLevels.Count) return;
        _selectedLevel = _visibleLevels[LstLevel.SelectedIndex];
        if (CmbGroup.SelectedItem is string group && LevelGroups.Contains(group))
            FilterParams(group);
    }

    private void FilterParams(string group)
    {
        IEnumerable<ParamViewModel> visible = _allVms
            .Where(v => v.Group == group)
            .OrderBy(v => v.Order);

        if (LevelGroups.Contains(group))
            visible = visible.Where(v => v.Param.Level == _selectedLevel);

        ParamList.ItemsSource = visible.ToList();
    }

    private void RefreshLevelSelector(string group)
    {
        _visibleLevels.Clear();
        _visibleLevels.AddRange(_allVms
            .Where(v => v.Param.Enabled && v.Group == group)
            .Select(v => v.Param.Level)
            .Where(level => level >= 0 && level < LevelLabels.Length)
            .Distinct()
            .OrderBy(level => level));

        if (_visibleLevels.Count == 0)
        {
            LevelStrip.Visibility = Visibility.Collapsed;
            LstLevel.ItemsSource = null;
            return;
        }

        LevelStrip.Visibility = Visibility.Visible;
        if (!_visibleLevels.Contains(_selectedLevel))
            _selectedLevel = _visibleLevels[0];

        LstLevel.ItemsSource = _visibleLevels.Select(level => LevelLabels[level]).ToList();
        LstLevel.SelectedIndex = _visibleLevels.IndexOf(_selectedLevel);
    }

    // ── auto-regeneration ────────────────────────────────────────────────

    private void OnParamChanged(object? sender, EventArgs e)
    {
        if (sender is ParamViewModel vm && AffectsParamAvailability(vm.Param.Name))
        {
            string? selectedGroup = CmbGroup.SelectedItem as string;
            _params.EnableDisable();
            RefreshAllVms();
            RefreshGroupList(selectedGroup);
        }

        if (ChkAutoGen.IsChecked == true)
        {
            if (_isSliderDragActive) return;
            _regenTimer.Stop();
            _regenTimer.Start();
        }
    }

    private void OnSliderDragStarted(object sender, DragStartedEventArgs e)
    {
        _isSliderDragActive = true;
        _regenTimer.Stop();
    }

    private async void OnSliderDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isSliderDragActive = false;
        if (sender is Slider slider && slider.DataContext is ParamViewModel vm)
            vm.CommitPreviewValue();

        if (ChkAutoGen.IsChecked == true)
        {
            _regenTimer.Stop();
            await GenerateAsync();
        }
    }

    private static bool AffectsParamAvailability(string name) =>
        name is "Levels" or "Leaves" or "Shape" or "PruneRatio" or "Lobes" or "0BaseSplits"
        || name.EndsWith("SegSplits", StringComparison.Ordinal)
        || name.EndsWith("CurveRes", StringComparison.Ordinal);

    // ── toolbar commands ─────────────────────────────────────────────────

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        var dlg = new WinOpenFileDialog
        {
            Title  = "XML paraméterfájl megnyitása",
            Filter = "Arbaro XML|*.xml|Minden fájl|*.*",
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var p = new TreeParams();
            using var fs = File.OpenRead(dlg.FileName);
            p.ReadFromXml(fs);
            _params      = p;
            _currentFile = dlg.FileName;
            Title        = $"DendroKit – {Path.GetFileName(dlg.FileName)}";
            _tree = null; _mesh = null;
            _needsCameraReset = true;
            _gl.SetMesh(null);
            LoadParams(_params);
            SetStatus("Betöltve: " + dlg.FileName);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(ex.Message, "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_currentFile == null) { OnSaveAs(sender, e); return; }
        SaveTo(_currentFile);
    }

    private void OnSaveAs(object sender, RoutedEventArgs e)
    {
        var dlg = new WinSaveFileDialog
        {
            Title      = "Mentés XML-be",
            Filter     = "Arbaro XML|*.xml|Minden fájl|*.*",
            DefaultExt = "xml",
            FileName   = _currentFile ?? "tree.xml",
        };
        if (dlg.ShowDialog(this) != true) return;
        SaveTo(dlg.FileName);
        _currentFile = dlg.FileName;
        Title        = $"DendroKit – {Path.GetFileName(dlg.FileName)}";
    }

    private void SaveTo(string path)
    {
        try
        {
            using var sw = new StreamWriter(path, append: false, System.Text.Encoding.UTF8);
            _params.WriteToXml(sw);
            SetStatus("Elmentve: " + path);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(ex.Message, "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnGenerate(object sender, RoutedEventArgs e)
    {
        _regenTimer.Stop();
        await GenerateAsync();
    }

    private async Task GenerateAsync()
    {
        if (_isGenerating)
        {
            _pendingGenerate = true;
            return;
        }

        _isGenerating = true;
        try
        {
            while (true)
            {
                _pendingGenerate = false;
                if (!int.TryParse(TxtSeed.Text, out int seed)) seed = 14;
                SetStatus("Generálás…");
                PrgBar.Visibility = Visibility.Visible;
                PrgBar.Value      = 0;

                var paramsCopy = new TreeParams(_params);
                var tree       = new TreeImpl(seed, paramsCopy);

                long maxProg = 1;
                tree.ProgressBeginPhase += (_, max) => maxProg = max;
                tree.ProgressUpdate += v => Dispatcher.BeginInvoke(() =>
                {
                    if (maxProg > 0)
                        PrgBar.Value = Math.Min(100, (int)(v * 100.0 / maxProg));
                });

                TreeMesh? mesh = null;
                await Task.Run(() =>
                {
                    tree.Make();
                    mesh = new MeshGenerator().CreateStemMesh(tree);
                });

                if (_pendingGenerate)
                    continue;

                _tree = tree;
                _mesh = mesh;
                _gl.SetTree(tree, mesh, resetCamera: _needsCameraReset);
                _needsCameraReset = false;
                SetStatus($"Kész  –  {tree.StemCount} ág  {tree.LeafCount} levél  {mesh?.VertexCount()} vertex");
                break;
            }
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(ex.Message, "Generálási hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("Hiba");
        }
        finally
        {
            _isGenerating = false;
            PrgBar.Visibility = Visibility.Collapsed;

            if (_pendingGenerate)
                await GenerateAsync();
        }
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        if (_tree == null || _mesh == null)
        {
            WpfMessageBox.Show("Előbb generálj egy fát!", "Export",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new WinSaveFileDialog
        {
            Title      = "OBJ export",
            Filter     = "Wavefront OBJ|*.obj|Minden fájl|*.*",
            DefaultExt = "obj",
            FileName   = "tree.obj",
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            using var sw = new StreamWriter(dlg.FileName, append: false, System.Text.Encoding.UTF8);
            new ObjExporter().Write(_tree, _mesh, sw);
            SetStatus("Exportálva: " + dlg.FileName);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(ex.Message, "Export hiba", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnWindowClosing(object sender, CancelEventArgs e)
    {
        _regenTimer.Stop();
    }

    private void SetStatus(string msg) => TxtStatus.Text = msg;
}
