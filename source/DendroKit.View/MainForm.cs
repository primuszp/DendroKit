using DendroKit.Core.Params;
using DendroKit.Core.Tree;
using DendroKit.Core.Mesh;
using DendroKit.Core.Export;
using OpenTK.GLControl;

namespace DendroKit.View;

public sealed class MainForm : Form
{
    private readonly SplitContainer    _split;
    private readonly ParamPanel        _paramPanel;
    private readonly TreeGLControl     _glControl;
    private readonly ToolStrip         _toolbar;
    private readonly StatusStrip       _status;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly ToolStripProgressBar _progressBar;

    private TreeParams? _params;
    private TreeImpl?   _tree;
    private TreeMesh?   _mesh;
    private string?     _currentFile;

    public MainForm()
    {
        Text            = "DendroKit";
        Size            = new Size(1280, 800);
        MinimumSize     = new Size(800, 600);
        StartPosition   = FormStartPosition.CenterScreen;

        _toolbar = BuildToolbar();
        Controls.Add(_toolbar);

        _status      = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("Kész") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _progressBar = new ToolStripProgressBar { Visible = false, Width = 200 };
        _status.Items.Add(_statusLabel);
        _status.Items.Add(_progressBar);
        Controls.Add(_status);

        _split = new SplitContainer
        {
            Dock        = DockStyle.Fill,
            Orientation = Orientation.Vertical,
        };
        Load += (_, _) =>
        {
            _split.Panel1MinSize    = 200;
            _split.Panel2MinSize    = 400;
            _split.SplitterDistance = 280;
        };

        _paramPanel = new ParamPanel();
        _split.Panel1.Controls.Add(_paramPanel);

        _glControl = new TreeGLControl();
        _split.Panel2.Controls.Add(_glControl);

        Controls.Add(_split);
        _toolbar.BringToFront();

        _params = new TreeParams();
        _paramPanel.LoadParams(_params);
    }

    private ToolStrip BuildToolbar()
    {
        var ts = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };

        var btnOpen = new ToolStripButton("Megnyitás") { ToolTipText = "XML paraméterfile megnyitása" };
        btnOpen.Click += OnOpen;

        var btnSave = new ToolStripButton("Mentés") { ToolTipText = "Mentés XML-be" };
        btnSave.Click += OnSave;

        var btnSaveAs = new ToolStripButton("Mentés másként") { ToolTipText = "Mentés más névvel" };
        btnSaveAs.Click += OnSaveAs;

        ts.Items.Add(btnOpen);
        ts.Items.Add(btnSave);
        ts.Items.Add(btnSaveAs);
        ts.Items.Add(new ToolStripSeparator());

        var btnGen = new ToolStripButton("Generálás") { ToolTipText = "Fa generálása" };
        btnGen.Click += OnGenerate;
        ts.Items.Add(btnGen);

        ts.Items.Add(new ToolStripSeparator());

        var btnExport = new ToolStripButton("OBJ export") { ToolTipText = "Exportálás Wavefront OBJ formátumba" };
        btnExport.Click += OnExport;
        ts.Items.Add(btnExport);

        ts.Items.Add(new ToolStripSeparator());

        var lblSeed = new ToolStripLabel("Seed:");
        ts.Items.Add(lblSeed);
        var txtSeed = new ToolStripTextBox { Text = "14", Width = 60, Name = "txtSeed" };
        ts.Items.Add(txtSeed);

        return ts;
    }

    private void OnOpen(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "XML paraméterfile megnyitása",
            Filter = "Arbaro XML|*.xml|Minden fájl|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var p = new TreeParams();
            using var fs = File.OpenRead(dlg.FileName);
            p.ReadFromXml(fs);
            _params      = p;
            _currentFile = dlg.FileName;
            Text         = $"DendroKit – {Path.GetFileName(dlg.FileName)}";
            _paramPanel.LoadParams(_params);
            _tree = null;
            _mesh = null;
            _glControl.SetMesh(null);
            SetStatus("Betöltve: " + dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (_currentFile == null) { OnSaveAs(sender, e); return; }
        SaveTo(_currentFile);
    }

    private void OnSaveAs(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Title            = "Mentés XML-be",
            Filter           = "Arbaro XML|*.xml|Minden fájl|*.*",
            DefaultExt       = "xml",
            FileName         = _currentFile ?? "tree.xml",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        SaveTo(dlg.FileName);
        _currentFile = dlg.FileName;
        Text         = $"DendroKit – {Path.GetFileName(dlg.FileName)}";
    }

    private void SaveTo(string path)
    {
        if (_params == null) return;
        _paramPanel.ApplyChanges();
        try
        {
            using var sw = new StreamWriter(path, false, System.Text.Encoding.UTF8);
            _params.WriteToXml(sw);
            SetStatus("Elmentve: " + path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnGenerate(object? sender, EventArgs e)
    {
        if (_params == null) return;
        _paramPanel.ApplyChanges();

        int seed = 14;
        if (_toolbar.Items["txtSeed"] is ToolStripTextBox txtSeed &&
            int.TryParse(txtSeed.Text, out int s)) seed = s;

        SetStatus("Generálás...");
        _progressBar.Visible = true;
        _progressBar.Value   = 0;

        var progress = new Progress<double>(v =>
        {
            _progressBar.Value = Math.Min(100, (int)(v * 100));
        });

        try
        {
            var tree = new TreeImpl(seed, _params);
            await Task.Run(() => tree.Make(progress));

            _tree = tree;
            _mesh = new MeshGenerator().CreateStemMesh(tree);
            _glControl.SetTree(tree, _mesh);
            SetStatus($"Kész  –  {tree.StemCount} ág  {tree.LeafCount} levél  {_mesh.VertexCount()} vertex");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Generálási hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Hiba");
        }
        finally
        {
            _progressBar.Visible = false;
        }
    }

    private void OnExport(object? sender, EventArgs e)
    {
        if (_tree == null || _mesh == null)
        {
            MessageBox.Show(this, "Előbb generálj egy fát!", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new SaveFileDialog
        {
            Title      = "OBJ export",
            Filter     = "Wavefront OBJ|*.obj|Minden fájl|*.*",
            DefaultExt = "obj",
            FileName   = "tree.obj",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var sw = new StreamWriter(dlg.FileName, false, System.Text.Encoding.UTF8);
            new ObjExporter().Write(_tree, _mesh, sw);
            SetStatus("Exportálva: " + dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetStatus(string msg) => _statusLabel.Text = msg;
}
