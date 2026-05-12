using DendroKit.Core.Params;
using DendroKit.Core.Geom;

namespace DendroKit.Core.Tree;

public sealed class TreeImpl : ITree
{
    internal readonly TreeParams Params;
    private  readonly int        _seed;

    public long   StemCount { get; private set; }
    public long   LeafCount { get; private set; }
    public string Species   => Params.Species;
    public double Scale     => Params.Scale;
    public string LeafShape => Params.LeafShape;
    public double LeafWidth  => Params.LeafScale * Params.LeafScaleX / Math.Sqrt(Params.LeafQuality);
    public double LeafLength => Params.LeafScale / Math.Sqrt(Params.LeafQuality);
    public double LeafStemLength => Params.LeafStemLen;

    private readonly List<StemImpl> _trunks = new();
    private double _trunkRotAngle;

    private double _maxX, _maxY, _maxZ;
    private double _minX, _minY, _minZ;

    public double Height => _maxZ;
    public double Width  => Math.Sqrt(Math.Max(
        _minX * _minX + _minY * _minY,
        _maxX * _maxX + _maxY * _maxY));

    internal long GenProgress;

    public event Action<string, long>? ProgressBeginPhase;
    public event Action<long>?         ProgressUpdate;
    public event Action?               ProgressEnd;

    public TreeImpl(int seed, TreeParams @params)
    {
        Params = @params;
        _seed  = seed;
        ResetBounds();
    }

    private void ResetBounds()
    {
        _maxX = _maxY = _maxZ = double.MinValue;
        _minX = _minY = _minZ = double.MaxValue;
    }

    public void Make(IProgress<double>? progress = null)
    {
        _trunks.Clear();
        ResetBounds();
        GenProgress = 0;

        Params.LeavesErrorValue = 0;
        Params.Prepare(_seed);

        long maxGenProgress =
            ((IntParam)Params.GetParam("0Branches")).IntValue()
            * ((IntParam)Params.GetParam("0CurveRes")).IntValue()
            * (((IntParam)Params.GetParam("1Branches")).IntValue() + 1);

        ProgressBeginPhase?.Invoke("Creating tree structure", maxGenProgress);

        var Transformation   = new Transformation();
        var lpar  = Params.GetLevelParams(0);
        for (int i = 0; i < lpar.NBranches; i++)
        {
            var branchTrf = TrunkDirection(Transformation, lpar);
            double angle  = lpar.Var(360);
            double dist   = lpar.Var(lpar.NBranchDist);
            branchTrf = branchTrf.Translate(
                new Vector3d(dist * Math.Sin(angle), dist * Math.Cos(angle), 0));

            var trunk = new StemImpl(this, null, 0, branchTrf, 0) { StemIndex = 0 };
            _trunks.Add(trunk);
            trunk.Make();
        }

        // count
        if (Params.Leaves == 0)
            LeafCount = 0;
        else
        {
            var lc = new LeafCounter();
            TraverseTree(lc);
            LeafCount = lc.Count;
        }
        var sc = new StemCounter();
        TraverseTree(sc);
        StemCount = sc.Count;

        ProgressEnd?.Invoke();
    }

    private Transformation TrunkDirection(Transformation Transformation, LevelParams lpar)
    {
        double rotAngle;
        if (lpar.NRotate >= 0)
        {
            _trunkRotAngle = (_trunkRotAngle + lpar.NRotate + lpar.Var(lpar.NRotateV) + 360) % 360;
            rotAngle = _trunkRotAngle;
        }
        else
        {
            if (Math.Abs(_trunkRotAngle) != 1) _trunkRotAngle = 1;
            _trunkRotAngle = -_trunkRotAngle;
            rotAngle = _trunkRotAngle * (180 + lpar.NRotate + lpar.Var(lpar.NRotateV));
        }
        double downAngle = lpar.NDownAngle + lpar.Var(lpar.NDownAngleV);
        return Transformation.RotXZ(downAngle, rotAngle);
    }

    public void TraverseTree(ITreeTraversal traversal)
    {
        if (traversal.EnterTree(this))
            foreach (var trunk in _trunks)
                if (!trunk.TraverseTree(traversal)) break;
        traversal.LeaveTree(this);
    }

    internal void MinMaxTest(Vector3d pt)
    {
        pt.SetMaxCoord(ref _maxX, ref _maxY, ref _maxZ);
        pt.SetMinCoord(ref _minX, ref _minY, ref _minZ);
    }

    internal void UpdateGenProgress()
    {
        long sum = 0;
        foreach (var trunk in _trunks)
            sum += trunk.Segments.Count * ((trunk.Substems?.Count ?? 0) + 1);
        GenProgress = sum;
        ProgressUpdate?.Invoke(sum);
    }

    public string GetVertexInfo(int level) =>
        $"vertices/section: {Params.GetLevelParams(level).MeshPoints}, " +
        $"smooth: {(Params.SmoothMeshLevel >= level ? "yes" : "no")}";
}
