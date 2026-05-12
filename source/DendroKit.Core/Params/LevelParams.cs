namespace DendroKit.Core.Params;

public class LevelParams
{
    public int Level { get; }

    public double NTaper;
    public int    NCurveRes;
    public double NCurve;
    public double NCurveV;
    public double NCurveBack;
    public double NLength;
    public double NLengthV;

    public double NSegSplits;
    public double NSplitAngle;
    public double NSplitAngleV;

    public int    NBranches;
    public double NBranchDist;
    public double NDownAngle;
    public double NDownAngleV;
    public double NRotate;
    public double NRotateV;

    public int    MeshPoints;
    public double SplitErrorValue;
    public double SubstemErrorValue;

    public TreeRandom Random { get; private set; } = null!;

    private readonly Dictionary<string, AbstractParam> _paramDb;

    // pruning state
    private long   _randState;
    private double _splitErrSaved = double.NaN;

    public LevelParams(int level, Dictionary<string, AbstractParam> paramDb)
    {
        Level   = level;
        _paramDb = paramDb;
    }

    public long InitRandom(long seed)
    {
        Random = new TreeRandom(seed);
        return Random.NextLong();
    }

    public double Var(double variation) =>
        Random.Uniform(-variation, variation);

    public void SaveState()
    {
        _randState    = Random.GetState();
        _splitErrSaved = SplitErrorValue;
    }

    public void RestoreState()
    {
        if (double.IsNaN(_splitErrSaved))
            throw new InvalidOperationException("No state saved, cannot restore.");
        Random.SetState(_randState);
        SplitErrorValue = _splitErrSaved;
        // intentionally NOT clearing _splitErrSaved:
        // Pruning() calls RestoreState multiple times in its retry loop
        // (same pattern as Java Arbaro lpar.restoreState())
    }

    public void ClearSavedState() => _splitErrSaved = double.NaN;

    internal void FromDb(bool leafLevelOnly)
    {
        if (!leafLevelOnly)
        {
            NTaper        = GetDbl("nTaper");
            NCurveRes     = GetInt("nCurveRes");
            NCurve        = GetDbl("nCurve");
            NCurveV       = GetDbl("nCurveV");
            NCurveBack    = GetDbl("nCurveBack");
            NLength       = GetDbl("nLength");
            NLengthV      = GetDbl("nLengthV");
            NSegSplits    = GetDbl("nSegSplits");
            NSplitAngle   = GetDbl("nSplitAngle");
            NSplitAngleV  = GetDbl("nSplitAngleV");
            NBranches     = GetInt("nBranches");
        }
        NBranchDist  = GetDbl("nBranchDist");
        NDownAngle   = GetDbl("nDownAngle");
        NDownAngleV  = GetDbl("nDownAngleV");
        NRotate      = GetDbl("nRotate");
        NRotateV     = GetDbl("nRotateV");
    }

    private string FullName(string name) => Level + name[1..];

    private int GetInt(string name)
    {
        string fn = FullName(name);
        if (_paramDb.TryGetValue(fn, out var p) && p is IntParam ip)
            return ip.IntValue();
        throw new ParamException($"Bug: param {fn} not found!");
    }

    private double GetDbl(string name)
    {
        string fn = FullName(name);
        if (_paramDb.TryGetValue(fn, out var p) && p is FloatParam fp)
            return fp.DoubleValue();
        throw new ParamException($"Bug: param {fn} not found!");
    }

    internal void ToXml(System.IO.TextWriter w, bool leafLevelOnly)
    {
        w.WriteLine($"    <!-- level {Level} -->");
        XmlParam(w, "nDownAngle",  NDownAngle);
        XmlParam(w, "nDownAngleV", NDownAngleV);
        XmlParam(w, "nRotate",     NRotate);
        XmlParam(w, "nRotateV",    NRotateV);
        if (!leafLevelOnly)
        {
            XmlParamI(w, "nBranches",     NBranches);
            XmlParam(w,  "nBranchDist",   NBranchDist);
            XmlParam(w,  "nLength",       NLength);
            XmlParam(w,  "nLengthV",      NLengthV);
            XmlParam(w,  "nTaper",        NTaper);
            XmlParam(w,  "nSegSplits",    NSegSplits);
            XmlParam(w,  "nSplitAngle",   NSplitAngle);
            XmlParam(w,  "nSplitAngleV",  NSplitAngleV);
            XmlParamI(w, "nCurveRes",     NCurveRes);
            XmlParam(w,  "nCurve",        NCurve);
            XmlParam(w,  "nCurveBack",    NCurveBack);
            XmlParam(w,  "nCurveV",       NCurveV);
        }
    }

    private void XmlParam(System.IO.TextWriter w, string n, double v) =>
        w.WriteLine($"    <param name='{Level}{n[1..]}' value='{v.ToString(System.Globalization.CultureInfo.InvariantCulture)}'/>");

    private void XmlParamI(System.IO.TextWriter w, string n, int v) =>
        w.WriteLine($"    <param name='{Level}{n[1..]}' value='{v}'/>");
}
