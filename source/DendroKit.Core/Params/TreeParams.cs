using System.Xml.Linq;

namespace DendroKit.Core.Params;

/// <summary>
/// All tree generation parameters. Port of Java Params class.
/// </summary>
public class TreeParams
{
    // Tree shape constants
    public const int Conical           = 0;
    public const int Spherical         = 1;
    public const int Hemispherical     = 2;
    public const int Cylindrical       = 3;
    public const int TaperedCylindrical= 4;
    public const int Flame             = 5;
    public const int InverseConical    = 6;
    public const int TendFlame         = 7;
    public const int Envelope          = 8;

    public readonly LevelParams[] LevelParamsArr = new LevelParams[4];
    public readonly Dictionary<string, AbstractParam> ParamDb = new(StringComparer.Ordinal);

    public TreeRandom? Random;

    public bool Preview;
    public bool IgnoreVParams;
    public int  StopLevel = -1;

    // Derived / prepared values
    public double ScaleTree;
    public double MeshQuality;
    public int    SmoothMeshLevel;

    // General params (populated by Prepare)
    public string Species       = "default";
    public double LeafQuality;
    public double Smooth;
    public int    Levels;
    public double Ratio;
    public double RatioPower;
    public int    Shape;
    public double BaseSize;
    public double Flare;
    public int    Lobes;
    public double LobeDepth;
    public int    Leaves;
    public string LeafShape      = "0";
    public double LeafScale;
    public double LeafScaleX;
    public double LeafStemLen;
    public double LeafBend;
    public int    LeafDistrib;
    public double Scale;
    public double ScaleV;
    public double Scale0;
    public double ScaleV0;
    public double AttractionUp;
    public double PruneRatio;
    public double PrunePowerLow;
    public double PrunePowerHigh;
    public double PruneWidth;
    public double PruneWidthPeak;
    public int    BaseSplits0;

    public double LeavesErrorValue;

    public event EventHandler? Changed;

    public TreeParams()
    {
        for (int l = 0; l < 4; l++)
            LevelParamsArr[l] = new LevelParams(l, ParamDb);
        RegisterParams();
        EnableDisable();
    }

    public TreeParams(TreeParams other) : this()
    {
        IgnoreVParams = other.IgnoreVParams;
        StopLevel     = other.StopLevel;
        Species       = other.Species;
        Smooth        = other.Smooth;

        foreach (var kv in other.ParamDb)
        {
            if (ParamDb.TryGetValue(kv.Key, out var myP) && !kv.Value.IsEmpty())
                myP.SetValue(kv.Value.GetValue());
        }

        EnableDisable();
    }

    public LevelParams GetLevelParams(int stemLevel) =>
        LevelParamsArr[Math.Min(stemLevel, 3)];

    public void SetParam(string name, string value)
    {
        if (!ParamDb.TryGetValue(name, out var p))
            throw new ParamException($"Unknown parameter {name}!");
        p.SetValue(value);
        EnableDisable();
    }

    public AbstractParam GetParam(string name)
    {
        if (ParamDb.TryGetValue(name, out var p)) return p;
        throw new ParamException($"Parameter {name} not found!");
    }

    public void Prepare(int seed)
    {
        FromDb();

        if (IgnoreVParams)
        {
            ScaleV = 0;
            for (int i = 1; i < 4; i++)
            {
                var lp = LevelParamsArr[i];
                lp.NCurveV = 0; lp.NLengthV = 0;
                lp.NSplitAngleV = 0; lp.NRotateV = 0;
                if (lp.NDownAngle > 0) lp.NDownAngle = 0;
            }
        }

        for (int l = 0; l < Math.Min(Levels, 4); l++)
        {
            var lp = LevelParamsArr[l];
            if (lp.NSegSplits > 0 && lp.NSplitAngle == 0)
                throw new ParamException("nSplitAngle may not be 0.");
        }

        long rnd = LevelParamsArr[0].InitRandom(seed);
        for (int i = 1; i < 4; i++)
            rnd = LevelParamsArr[i].InitRandom(rnd);

        Random = new TreeRandom(seed);

        SmoothMeshLevel = Smooth <= 0.2 ? -1 : (int)(Levels * Smooth);
        MeshQuality = Smooth;

        LevelParamsArr[0].MeshPoints = 4;
        LevelParamsArr[1].MeshPoints = 3;
        LevelParamsArr[2].MeshPoints = 2;
        LevelParamsArr[3].MeshPoints = 1;

        if (Lobes > 0)
        {
            LevelParamsArr[0].MeshPoints =
                (int)(Lobes * Math.Pow(2, (int)(1 + 2.5 * MeshQuality)));
            LevelParamsArr[0].MeshPoints =
                Math.Max(LevelParamsArr[0].MeshPoints, (int)(4 * (1 + 2 * MeshQuality)));
        }
        for (int i = 1; i < 4; i++)
        {
            LevelParamsArr[i].MeshPoints =
                Math.Max(3, (int)(LevelParamsArr[i].MeshPoints * (1 + 1.5 * MeshQuality)));
        }

        if (StopLevel >= 0 && StopLevel <= Levels)
        {
            Levels = StopLevel;
            Leaves = 0;
        }

        ScaleTree = Scale + LevelParamsArr[0].Var(ScaleV);
    }

    public double GetShapeRatio(double ratio) => GetShapeRatio(ratio, Shape);

    public double GetShapeRatio(double ratio, int shape) => shape switch
    {
        Conical            => ratio,
        Spherical          => 0.2 + 0.8 * Math.Sin(Math.PI * ratio),
        Hemispherical      => 0.2 + 0.8 * Math.Sin(0.5 * Math.PI * ratio),
        Cylindrical        => 1.0,
        TaperedCylindrical => 0.5 + 0.5 * ratio,
        Flame              => ratio <= 0.7 ? ratio / 0.7 : (1 - ratio) / 0.3,
        InverseConical     => 1 - 0.8 * ratio,
        TendFlame          => ratio <= 0.7 ? 0.5 + 0.5 * ratio / 0.7 : 0.5 + 0.5 * (1 - ratio) / 0.3,
        Envelope           => ratio < 0 || ratio > 1 ? 0
                              : ratio < (1 - PruneWidthPeak)
                                  ? Math.Pow(ratio / (1 - PruneWidthPeak), PrunePowerHigh)
                                  : Math.Pow((1 - ratio) / (1 - PruneWidthPeak), PrunePowerLow),
        _                  => 0
    };

    private void FromDb()
    {
        LeafQuality    = GetDbl("LeafQuality");
        Smooth         = GetDbl("Smooth");
        Levels         = GetInt("Levels");
        Ratio          = GetDbl("Ratio");
        RatioPower     = GetDbl("RatioPower");
        Shape          = GetInt("Shape");
        BaseSize       = GetDbl("BaseSize");
        Flare          = GetDbl("Flare");
        Lobes          = GetInt("Lobes");
        LobeDepth      = GetDbl("LobeDepth");
        Leaves         = GetInt("Leaves");
        LeafShape      = GetStr("LeafShape");
        LeafScale      = GetDbl("LeafScale");
        LeafScaleX     = GetDbl("LeafScaleX");
        LeafStemLen    = GetDbl("LeafStemLen");
        LeafDistrib    = GetInt("LeafDistrib");
        LeafBend       = GetDbl("LeafBend");
        Scale          = GetDbl("Scale");
        ScaleV         = GetDbl("ScaleV");
        Scale0         = GetDbl("0Scale");
        ScaleV0        = GetDbl("0ScaleV");
        AttractionUp   = GetDbl("AttractionUp");
        PruneRatio     = GetDbl("PruneRatio");
        PrunePowerLow  = GetDbl("PrunePowerLow");
        PrunePowerHigh = GetDbl("PrunePowerHigh");
        PruneWidth     = GetDbl("PruneWidth");
        PruneWidthPeak = GetDbl("PruneWidthPeak");
        BaseSplits0    = GetInt("0BaseSplits");
        Species        = GetStr("Species");

        for (int i = 0; i <= Math.Min(Levels, 3); i++)
            LevelParamsArr[i].FromDb(i == Levels);
    }

    private int    GetInt(string n) => ((IntParam)ParamDb[n]).IntValue();
    private double GetDbl(string n) => ((FloatParam)ParamDb[n]).DoubleValue();
    private string GetStr(string n) => ((StringParam)ParamDb[n]).StringValue();

    // -----------------------------------------------------------------------
    // XML I/O
    // -----------------------------------------------------------------------

    public void ReadFromXml(Stream stream)
    {
        var doc = XDocument.Load(stream);
        foreach (var el in doc.Descendants())
        {
            if (el.Name == "species")
                SetParam("Species", el.Attribute("name")?.Value ?? "default");
            else if (el.Name == "param")
            {
                string? name  = el.Attribute("name")?.Value;
                string? value = el.Attribute("value")?.Value;
                if (name != null && value != null)
                {
                    try { SetParam(name, value); }
                    catch (ParamException ex)
                    { Console.Error.WriteLine(ex.Message); }
                }
            }
        }

        EnableDisable();
    }

    public void ReadFromCfg(Stream stream)
    {
        using var reader = new System.IO.StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line) || line[0] == '#') continue;
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            string name  = line[..eq].Trim();
            string value = line[(eq+1)..].Trim();
            if (name == "species") name = "Species";
            try { SetParam(name, value); }
            catch (ParamException ex) { Console.Error.WriteLine(ex.Message); }
        }

        EnableDisable();
    }

    public void WriteToXml(TextWriter w)
    {
        FromDb();
        w.WriteLine("<?xml version='1.0' ?>");
        w.WriteLine("<arbaro>");
        w.WriteLine($"  <species name='{Species}'>");
        W(w, "Shape",         Shape);
        W(w, "Levels",        Levels);
        W(w, "Scale",         Scale);
        W(w, "ScaleV",        ScaleV);
        W(w, "BaseSize",      BaseSize);
        W(w, "Ratio",         Ratio);
        W(w, "RatioPower",    RatioPower);
        W(w, "Flare",         Flare);
        W(w, "Lobes",         Lobes);
        W(w, "LobeDepth",     LobeDepth);
        W(w, "Smooth",        Smooth);
        W(w, "Leaves",        Leaves);
        W(w, "LeafShape",     LeafShape);
        W(w, "LeafScale",     LeafScale);
        W(w, "LeafScaleX",    LeafScaleX);
        W(w, "LeafQuality",   LeafQuality);
        W(w, "LeafStemLen",   LeafStemLen);
        W(w, "LeafDistrib",   LeafDistrib);
        W(w, "LeafBend",      LeafBend);
        W(w, "AttractionUp",  AttractionUp);
        W(w, "PruneRatio",    PruneRatio);
        W(w, "PrunePowerLow", PrunePowerLow);
        W(w, "PrunePowerHigh",PrunePowerHigh);
        W(w, "PruneWidth",    PruneWidth);
        W(w, "PruneWidthPeak",PruneWidthPeak);
        W(w, "0Scale",        Scale0);
        W(w, "0ScaleV",       ScaleV0);
        W(w, "0BaseSplits",   BaseSplits0);
        for (int i = 0; i <= Math.Min(Levels, 3); i++)
            LevelParamsArr[i].ToXml(w, i == Levels);
        w.WriteLine("  </species>");
        w.WriteLine("</arbaro>");
        w.Flush();
    }

    private static void W(TextWriter wr, string name, object val) =>
        wr.WriteLine($"    <param name='{name}' value='{val}'/>");

    public void ClearParams()
    {
        foreach (var p in ParamDb.Values) p.Clear();
        EnableDisable();
    }

    public void EnableDisable()
    {
        bool enable;

        enable = ((IntParam)GetParam("Levels")).IntValue() > 1;
        GetParam("RatioPower").SetEnabled(enable);
        GetParam("Leaves").SetEnabled(enable);

        enable = ((IntParam)GetParam("Leaves")).IntValue() != 0
            && ((IntParam)GetParam("Levels")).IntValue() > 1;
        GetParam("LeafShape").SetEnabled(enable);
        GetParam("LeafScale").SetEnabled(enable);
        GetParam("LeafScaleX").SetEnabled(enable);
        GetParam("LeafBend").SetEnabled(enable);
        GetParam("LeafDistrib").SetEnabled(enable);
        GetParam("LeafQuality").SetEnabled(enable);
        GetParam("LeafStemLen").SetEnabled(enable);

        enable = ((IntParam)GetParam("Shape")).IntValue() == Envelope
            || ((FloatParam)GetParam("PruneRatio")).DoubleValue() > 0;
        GetParam("PrunePowerHigh").SetEnabled(enable);
        GetParam("PrunePowerLow").SetEnabled(enable);
        GetParam("PruneWidth").SetEnabled(enable);
        GetParam("PruneWidthPeak").SetEnabled(enable);

        enable = ((IntParam)GetParam("Lobes")).IntValue() > 0;
        GetParam("LobeDepth").SetEnabled(enable);

        enable = ((IntParam)GetParam("Levels")).IntValue() > 2;
        GetParam("AttractionUp").SetEnabled(enable);

        int levels = ((IntParam)GetParam("Levels")).IntValue();
        int leaves = ((IntParam)GetParam("Leaves")).IntValue();

        for (int i = 0; i < 4; i++)
        {
            enable = i < levels;

            GetParam($"{i}Length").SetEnabled(enable);
            GetParam($"{i}LengthV").SetEnabled(enable);
            GetParam($"{i}Taper").SetEnabled(enable);

            GetParam($"{i}Curve").SetEnabled(enable);
            GetParam($"{i}CurveV").SetEnabled(enable);
            GetParam($"{i}CurveRes").SetEnabled(enable);
            GetParam($"{i}CurveBack").SetEnabled(enable);

            GetParam($"{i}SegSplits").SetEnabled(enable);
            GetParam($"{i}SplitAngle").SetEnabled(enable);
            GetParam($"{i}SplitAngleV").SetEnabled(enable);

            GetParam($"{i}BranchDist").SetEnabled(enable);
            GetParam($"{i}Branches").SetEnabled(enable);

            bool leafLevel = leaves != 0 && i == levels;
            GetParam($"{i}DownAngle").SetEnabled(enable || leafLevel);
            GetParam($"{i}DownAngleV").SetEnabled(enable || leafLevel);
            GetParam($"{i}Rotate").SetEnabled(enable || leafLevel);
            GetParam($"{i}RotateV").SetEnabled(enable || leafLevel);
        }

        for (int i = 0; i < levels && i < 4; i++)
        {
            enable = ((FloatParam)GetParam($"{i}SegSplits")).DoubleValue() > 0
                || (i == 0 && ((IntParam)GetParam("0BaseSplits")).IntValue() > 0);
            GetParam($"{i}SplitAngle").SetEnabled(enable);
            GetParam($"{i}SplitAngleV").SetEnabled(enable);

            enable = ((IntParam)GetParam($"{i}CurveRes")).IntValue() > 1;
            GetParam($"{i}Curve").SetEnabled(enable);
            GetParam($"{i}CurveV").SetEnabled(enable);
            GetParam($"{i}CurveBack").SetEnabled(enable);
        }
    }

    // -----------------------------------------------------------------------
    // Parameter registration (mirrors Java Params.registerParams)
    // -----------------------------------------------------------------------

    private int _order;

    private void RegisterParams()
    {
        _order = 1;

        Str("Species", "default", "SHAPE", "the tree's species", "");
        Shape_("Shape", 0, 8, 0, "SHAPE", "general tree shape id", "");
        Int("Levels", 0, 9, 3, "SHAPE", "levels of recursion", "");
        Dbl("Scale", 1e-6, double.PositiveInfinity, 10, "SHAPE", "average tree size in meters", "");
        Dbl("ScaleV", 0, double.PositiveInfinity, 0, "SHAPE", "variation of tree size", "");
        Dbl("BaseSize", 0, 1, 0.25, "SHAPE", "fractional branchless area", "");
        Int("0BaseSplits", 0, int.MaxValue, 0, "SHAPE", "stem splits at base", "");
        Dbl("Ratio", 1e-6, double.PositiveInfinity, 0.05, "TRUNK", "trunk radius/length ratio", "");
        Dbl("RatioPower", double.NegativeInfinity, double.PositiveInfinity, 1.0, "SHAPE", "radius reduction", "");
        Dbl("Flare", -1, double.PositiveInfinity, 0.5, "TRUNK", "expansion at base", "");
        Int("Lobes", 0, int.MaxValue, 0, "TRUNK", "sinusoidal cross-section variation", "");
        Dbl("LobeDepth", 0, double.PositiveInfinity, 0, "TRUNK", "amplitude of cross-section variation", "");
        Int("Leaves", int.MinValue, int.MaxValue, 0, "LEAVES", "number of leaves per stem", "");
        Lsh("LeafShape", "0", "LEAVES", "leaf shape id", "");
        Dbl("LeafScale", 1e-6, double.PositiveInfinity, 0.2, "LEAVES", "leaf length", "");
        Dbl("LeafScaleX", 1e-6, double.PositiveInfinity, 0.5, "LEAVES", "fractional leaf width", "");
        Dbl("LeafBend", 0, 1, 0.3, "LEAVES", "leaf orientation toward light", "");
        Dbl("LeafStemLen", double.NegativeInfinity, double.PositiveInfinity, 0.5, "LEAVES", "fractional leaf stem length", "");
        Int("LeafDistrib", 0, 8, 4, "LEAVES", "leaf distribution", "");
        Dbl("LeafQuality", 1e-6, 1.0, 1.0, "QUALITY", "leaf quality/count reduction", "");
        Dbl("Smooth", 0, 1, 0.5, "QUALITY", "smooth value", "");
        Dbl("AttractionUp", double.NegativeInfinity, double.PositiveInfinity, 0, "SHAPE", "upward/downward tendency", "");
        Dbl("PruneRatio", 0, 1, 0, "PRUNING", "fractional effect of pruning", "");
        Dbl("PruneWidth", 0, 1, 0.5, "PRUNING", "width of envelope peak", "");
        Dbl("PruneWidthPeak", 0, 1, 0.5, "PRUNING", "position of envelope peak", "");
        Dbl("PrunePowerLow", 0, double.PositiveInfinity, 0.5, "PRUNING", "curvature below peak", "");
        Dbl("PrunePowerHigh", 0, double.PositiveInfinity, 0.5, "PRUNING", "curvature above peak", "");
        Dbl("0Scale", 1e-6, double.PositiveInfinity, 1.0, "TRUNK", "extra trunk scaling", "");
        Dbl("0ScaleV", 0, double.PositiveInfinity, 0, "TRUNK", "variation for extra trunk scaling", "");

        Dbl4("nLength",     0+1e-7, double.PositiveInfinity, 1,0.5,0.5,0.5,     "LENTAPER","fractional length","");
        Dbl4("nLengthV",    0, double.PositiveInfinity,      0,0,0,0,            "LENTAPER","length variation","");
        Dbl4("nTaper",      0, 2.99999999,                   1,1,1,1,            "LENTAPER","cross-section scaling","");
        Dbl4("nSegSplits",  0, double.PositiveInfinity,      0,0,0,0,            "SPLITTING","splits per segment","");
        Dbl4("nSplitAngle", 0, 180,                          0,0,0,0,            "SPLITTING","splitting angle","");
        Dbl4("nSplitAngleV",0, 180,                          0,0,0,0,            "SPLITTING","splitting angle variation","");
        Int4("nCurveRes",   1, int.MaxValue,                 3,3,1,1,            "CURVATURE","curvature resolution","");
        Dbl4("nCurve",      double.NegativeInfinity,double.PositiveInfinity, 0,0,0,0, "CURVATURE","curving angle","");
        Dbl4("nCurveV",     -90, double.PositiveInfinity,    0,0,0,0,            "CURVATURE","curving angle variation","");
        Dbl4("nCurveBack",  double.NegativeInfinity,double.PositiveInfinity, 0,0,0,0, "CURVATURE","curving angle upper half","");
        Dbl4("nDownAngle",  -179.9999999,179.999999,         0,30,30,30,         "BRANCHING","angle from parent","");
        Dbl4("nDownAngleV", -179.9999999,179.9999999,        0,0,0,0,            "BRANCHING","down angle variation","");
        Dbl4("nRotate",     -360,360,                        0,120,120,120,      "BRANCHING","spiraling angle","");
        Dbl4("nRotateV",    -360,360,                        0,0,0,0,            "BRANCHING","spiraling angle variation","");
        Int4("nBranches",   0, int.MaxValue,                 1,10,5,5,           "BRANCHING","number of branches","");
        Dbl4("nBranchDist", 0, 1,                            0,1,1,1,            "BRANCHING","branch distribution","");
    }

    private void Int(string name, int min, int max, int def, string grp, string sd, string ld)
    {
        ParamDb[name] = new IntParam(name, min, max, def, grp, AbstractParam.General, _order++, sd, ld);
    }
    private void Dbl(string name, double min, double max, double def, string grp, string sd, string ld)
    {
        ParamDb[name] = new FloatParam(name, min, max, def, grp, AbstractParam.General, _order++, sd, ld);
    }
    private void Str(string name, string def, string grp, string sd, string ld)
    {
        ParamDb[name] = new StringParam(name, def, grp, AbstractParam.General, _order++, sd, ld);
    }
    private void Shape_(string name, int min, int max, int def, string grp, string sd, string ld)
    {
        ParamDb[name] = new ShapeParam(name, min, max, def, grp, AbstractParam.General, _order++, sd, ld);
    }
    private void Lsh(string name, string def, string grp, string sd, string ld)
    {
        ParamDb[name] = new LeafShapeParam(name, def, grp, AbstractParam.General, _order++, sd, ld);
    }
    private void Int4(string name, int min, int max, int d0, int d1, int d2, int d3, string grp, string sd, string ld)
    {
        int[] defs = { d0, d1, d2, d3 };
        _order++;
        for (int i = 0; i < 4; i++)
        {
            string fn = i + name[1..];
            ParamDb[fn] = new IntParam(fn, min, max, defs[i], grp, i, _order, sd, ld);
        }
    }
    private void Dbl4(string name, double min, double max, double d0, double d1, double d2, double d3, string grp, string sd, string ld)
    {
        double[] defs = { d0, d1, d2, d3 };
        _order++;
        for (int i = 0; i < 4; i++)
        {
            string fn = i + name[1..];
            ParamDb[fn] = new FloatParam(fn, min, max, defs[i], grp, i, _order, sd, ld);
        }
    }
}
