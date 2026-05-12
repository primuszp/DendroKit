namespace DendroKit.Core.Params;

public class FloatParam : AbstractParam
{
    public double Min     { get; }
    public double Max     { get; }
    private double _default;
    private double? _value;

    public FloatParam(string name, double min, double max, double deflt,
                      string group, int level, int order, string sd, string ld)
        : base(name, group, level, order, sd, ld)
    { Min = min; Max = max; _default = deflt; }

    public double DoubleValue() => _value ?? _default;

    public override void SetValue(string val)
    {
        if (!double.TryParse(val, System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture, out double d))
            throw new ParamException($"Parameter {Name}: '{val}' is not a valid number.");
        if (d < Min || d > Max)
            throw new ParamException(
                d < Min
                    ? $"Value of {Name} should be greater than or equal to {Min}."
                    : $"Value of {Name} should be less than or equal to {Max}.");
        _value = d;
        FireChanged();
    }

    public override string GetValue()        => DoubleValue().ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    public override string GetDefaultValue() => _default.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    public override void   Clear()           { _value = null; FireChanged(); }
    public override bool   IsEmpty()         => _value == null;
}

public class IntParam : AbstractParam
{
    public int Min { get; }
    public int Max { get; }
    private int _default;
    private int? _value;

    public IntParam(string name, int min, int max, int deflt,
                    string group, int level, int order, string sd, string ld)
        : base(name, group, level, order, sd, ld)
    { Min = min; Max = max; _default = deflt; }

    public int IntValue() => _value ?? _default;

    public override void SetValue(string val)
    {
        if (!int.TryParse(val, out int i))
            throw new ParamException($"Parameter {Name}: '{val}' is not a valid integer.");
        if (i < Min || i > Max)
            throw new ParamException(
                i < Min
                    ? $"Value of {Name} should be greater than or equal to {Min}."
                    : $"Value of {Name} should be less than or equal to {Max}.");
        _value = i;
        FireChanged();
    }

    public override string GetValue()        => IntValue().ToString();
    public override string GetDefaultValue() => _default.ToString();
    public override void   Clear()           { _value = null; FireChanged(); }
    public override bool   IsEmpty()         => _value == null;
}

public class StringParam : AbstractParam
{
    private string _default;
    private string? _value;

    public StringParam(string name, string deflt,
                       string group, int level, int order, string sd, string ld)
        : base(name, group, level, order, sd, ld)
    { _default = deflt; }

    public string StringValue() => _value ?? _default;

    public override void SetValue(string val) { _value = val; FireChanged(); }
    public override string GetValue()        => StringValue();
    public override string GetDefaultValue() => _default;
    public override void   Clear()           { _value = null; FireChanged(); }
    public override bool   IsEmpty()         => _value == null;
}

public class ShapeParam : IntParam
{
    public ShapeParam(string name, int min, int max, int deflt,
                      string group, int level, int order, string sd, string ld)
        : base(name, min, max, deflt, group, level, order, sd, ld) { }
}

public class LeafShapeParam : StringParam
{
    public LeafShapeParam(string name, string deflt,
                          string group, int level, int order, string sd, string ld)
        : base(name, deflt, group, level, order, sd, ld) { }
}

public class ParamException : Exception
{
    public ParamException(string message) : base(message) { }
}
