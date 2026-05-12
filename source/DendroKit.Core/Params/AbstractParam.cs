namespace DendroKit.Core.Params;

public abstract class AbstractParam
{
    public const int General = -999;

    public string Name     { get; }
    public string Group    { get; }
    public int    Level    { get; }
    public int    Order    { get; }
    public string ShortDesc { get; }
    public string LongDesc  { get; }
    public bool   Enabled  { get; private set; } = true;

    public event EventHandler? Changed;

    protected AbstractParam(string name, string group, int level, int order,
                             string shortDesc, string longDesc)
    {
        Name      = name;
        Group     = group;
        Level     = level;
        Order     = order;
        ShortDesc = shortDesc;
        LongDesc  = longDesc;
    }

    public abstract void   SetValue(string val);
    public abstract string GetValue();
    public abstract string GetDefaultValue();
    public abstract void   Clear();
    public abstract bool   IsEmpty();

    public void SetEnabled(bool en)
    {
        Enabled = en;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public string GetNiceName()
    {
        var n = Name;
        int i = n.Length > 0 && n[0] >= '0' && n[0] <= '9' ? 1 : 0;
        var result = string.Empty;
        for (; i < n.Length; i++)
        {
            char c = n[i];
            if (c >= 'A' && c <= 'Z') result += " " + c;
            else result += c;
        }
        if (result.EndsWith("V"))    result = result[..^1] + "Variation";
        if (result.EndsWith("Res"))  result = result[..^3] + "Resolution";
        if (result.EndsWith("Dist")) result = result[..^4] + "Distribution";
        return result;
    }

    public override string ToString() =>
        !IsEmpty() ? GetValue() : GetDefaultValue();

    protected void FireChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
