using System.ComponentModel;
using System.Runtime.CompilerServices;
using DendroKit.Core.Params;

namespace DendroKit.WpfApp;

public sealed class ParamViewModel : INotifyPropertyChanged
{
    private static readonly string[] DefaultLeafShapes =
    [
        "0", "disc1", "disc2", "disc3", "disc4", "disc5", "disc6",
        "disc7", "disc8", "disc9", "disc10", "sphere", "palm"
    ];

    public AbstractParam Param   { get; }
    public string  Name          => Param.Name;
    public string  NiceName      => Param.GetNiceName();
    public string  Group         => Param.Group;
    public int     Order         => Param.Order;
    public bool    IsNumeric     => Param is IntParam or FloatParam or ShapeParam;
    public bool    IsLeafShape   => Param is LeafShapeParam;
    public bool    IsEnabled     => Param.Enabled;
    public double  SliderMin     => _sliderMin;
    public double  SliderMax     => _sliderMax;
    public double  TickFreq      => _tickFreq;
    public IReadOnlyList<string> LeafShapeChoices => _leafShapeChoices;
    public int     LeafShapeMaxIndex => Math.Max(0, _leafShapeChoices.Count - 1);

    private double _num;
    private int    _leafShapeIndex;
    private string _txt = "";
    private double _sliderMin;
    private double _sliderMax;
    private double _tickFreq;
    private readonly List<string> _leafShapeChoices = [];

    /// Live slider value. This does not write back to the model until committed.
    public double PreviewNumericValue
    {
        get => _num;
        set
        {
            double v = (Param is IntParam or ShapeParam) ? Math.Round(value) : value;
            v = Math.Max(SliderMin, Math.Min(SliderMax, v));
            if (Math.Abs(_num - v) < 1e-13) return;
            _num = v;
            _txt = FormatPreviewText(v);
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextValue));
        }
    }

    /// Text value (for string params and text-entry override).
    public string TextValue
    {
        get => _txt;
        set
        {
            if (_txt == value) return;
            _txt = value;
            if (IsNumeric)
            {
                if (double.TryParse(value, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double d))
                {
                    double v = (Param is IntParam or ShapeParam) ? Math.Round(d) : d;
                    if (TryWriteNumeric(v))
                    {
                        OnPropertyChanged(nameof(PreviewNumericValue));
                        ValueChanged?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        RevertFromParam();
                    }
                }
                else
                {
                    RevertFromParam();
                }
            }
            else
            {
                try
                {
                    Param.SetValue(value);
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
                catch { RevertFromParam(); }
            }
            OnPropertyChanged();
        }
    }

    public int PreviewLeafShapeIndex
    {
        get => _leafShapeIndex;
        set
        {
            int v = Math.Clamp(value, 0, LeafShapeMaxIndex);
            if (_leafShapeIndex == v) return;
            _leafShapeIndex = v;
            _txt = _leafShapeChoices[v];
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextValue));
        }
    }

    public event EventHandler?              ValueChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public ParamViewModel(AbstractParam param)
    {
        Param = param;
        InitializeLeafShapeChoices();
        ReadFromParam();
    }

    public void ReadFromParam()
    {
        if (Param is IntParam ip)
        {
            _num = ip.IntValue();
            _txt = ip.IntValue().ToString();
        }
        else if (Param is FloatParam fp)
        {
            _num = fp.DoubleValue();
            _txt = fp.DoubleValue().ToString("G6", System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            _txt = Param.GetValue();
            EnsureLeafShapeChoice(_txt);
            SyncLeafShapeIndexFromText();
        }

        UpdateRange();
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(PreviewLeafShapeIndex));
        OnPropertyChanged(nameof(LeafShapeMaxIndex));
    }

    public void CommitPreviewValue()
    {
        if (IsLeafShape)
        {
            try
            {
                string selected = _leafShapeChoices[Math.Clamp(_leafShapeIndex, 0, LeafShapeMaxIndex)];
                Param.SetValue(selected);
                _txt = Param.GetValue();
                SyncLeafShapeIndexFromText();
                OnPropertyChanged(nameof(PreviewLeafShapeIndex));
                OnPropertyChanged(nameof(TextValue));
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                RevertFromParam();
            }
            return;
        }

        if (TryWriteNumeric(_num))
        {
            OnPropertyChanged(nameof(PreviewNumericValue));
            OnPropertyChanged(nameof(TextValue));
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            RevertFromParam();
        }
    }

    private bool TryWriteNumeric(double value)
    {
        try
        {
            string s = (Param is IntParam or ShapeParam)
                ? ((long)value).ToString()
                : value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            Param.SetValue(s);
            _num = value;
            _txt = Param.GetValue();
            UpdateRange();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static (double min, double max) ComputeRange(AbstractParam param)
    {
        if (param is IntParam ip)
        {
            double lo = Math.Max(-9999, ip.Min);
            double hi = ip.Max == int.MaxValue
                ? Math.Max(20, ip.IntValue() * 4 + 8)
                : Math.Min(9999, ip.Max);
            return (lo, hi);
        }
        if (param is FloatParam fp)
        {
            double lo = double.IsNegativeInfinity(fp.Min)
                ? Math.Min(-10.0, fp.DoubleValue() * 2 - 0.1)
                : fp.Min;
            double hi = double.IsPositiveInfinity(fp.Max)
                ? Math.Max(20.0, fp.DoubleValue() * 6 + 1.0)
                : fp.Max;
            return (lo, hi);
        }
        return (0, 1);
    }

    private void UpdateRange()
    {
        var (min, max) = ComputeRange(Param);
        double tick = Math.Max(1e-6, (max - min) / 20.0);

        if (Math.Abs(_sliderMin - min) > 1e-13)
        {
            _sliderMin = min;
            OnPropertyChanged(nameof(SliderMin));
        }

        if (Math.Abs(_sliderMax - max) > 1e-13)
        {
            _sliderMax = max;
            OnPropertyChanged(nameof(SliderMax));
        }

        if (Math.Abs(_tickFreq - tick) > 1e-13)
        {
            _tickFreq = tick;
            OnPropertyChanged(nameof(TickFreq));
        }
    }

    private void RevertFromParam()
    {
        ReadFromParam();
        OnPropertyChanged(nameof(PreviewNumericValue));
        OnPropertyChanged(nameof(PreviewLeafShapeIndex));
        OnPropertyChanged(nameof(TextValue));
        OnPropertyChanged(nameof(LeafShapeMaxIndex));
    }

    private string FormatPreviewText(double value) =>
        Param is IntParam or ShapeParam
            ? ((long)Math.Round(value)).ToString()
            : value.ToString("G6", System.Globalization.CultureInfo.InvariantCulture);

    private void InitializeLeafShapeChoices()
    {
        if (!IsLeafShape) return;
        _leafShapeChoices.AddRange(DefaultLeafShapes);
        EnsureLeafShapeChoice(Param.GetValue());
    }

    private void EnsureLeafShapeChoice(string value)
    {
        if (!IsLeafShape || string.IsNullOrWhiteSpace(value)) return;
        if (_leafShapeChoices.Contains(value, StringComparer.OrdinalIgnoreCase)) return;
        _leafShapeChoices.Add(value);
        OnPropertyChanged(nameof(LeafShapeChoices));
        OnPropertyChanged(nameof(LeafShapeMaxIndex));
    }

    private void SyncLeafShapeIndexFromText()
    {
        if (!IsLeafShape) return;
        int idx = _leafShapeChoices.FindIndex(x => string.Equals(x, _txt, StringComparison.OrdinalIgnoreCase));
        _leafShapeIndex = idx >= 0 ? idx : 0;
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
