using DendroKit.Core.Geom;
using DendroKit.Core.Params;

namespace DendroKit.Core.Tree;

internal sealed class StemImpl : IStem
{
    const double MinStemLen    = 0.00001;
    const double MinStemRadius = MinStemLen / 10.0;

    internal readonly TreeImpl   Tree;
    internal readonly TreeParams Par;
    internal readonly LevelParams LPar;

    internal StemImpl?  Parent;
    internal StemImpl?  ClonedFrom;

    public Transformation Transform { get; private set; }

    public int    Level  { get; }
    public double Length { get; private set; }
    public bool   IsClone => ClonedFrom != null;

    internal double SegmentLength;
    internal int    SegmentCount;
    internal double _baseRadius;
    public   double BaseRadius  => _baseRadius;
    public   double PeakRadius  => Segments.Count > 0 ? Segments[^1].UpperRadius : 0;

    internal double LengthChildMax;
    internal double SubstemsPerSegment;
    internal double SubstemRotAngle;
    internal double LeavesPerSegment;
    internal double SplitCorrection;
    internal int    StemIndex;
    internal List<int> CloneIndex = new();

    internal readonly List<SegmentImpl> Segments;
    internal          List<StemImpl>?   Clones;
    internal          List<StemImpl>?   Substems;
    internal          List<LeafImpl>?   Leaves;

    private bool   _pruneTest;
    private double _maxX, _maxY, _maxZ;
    private double _minX, _minY, _minZ;
    private readonly double _offset;

    public string TreePosition
    {
        get
        {
            var stem = this;
            int lev  = Level;
            string pos = "";
            while (lev >= 0)
            {
                string cloneStr = stem.CloneIndex.Count > 0
                    ? string.Concat(stem.CloneIndex.Select(c => "c" + c)) : "";
                pos = stem.StemIndex + cloneStr + "." + pos;
                if (lev > 0 && stem.Parent != null) stem = stem.Parent;
                lev--;
            }
            return pos.TrimEnd('.');
        }
    }

    public int CloneSectionOffset
    {
        get
        {
            if (ClonedFrom == null || Segments.Count == 0) return 0;
            return ClonedFrom.GetSectionCountBelow(Segments[0].Index);
        }
    }

    private int GetSectionCountBelow(int segIndex)
    {
        int count = 1; // base section of first segment
        foreach (var seg in Segments)
        {
            if (seg.Index < segIndex) count += seg.Subsegments.Count;
            else return count - 1;
        }
        return count - 1;
    }

    public StemImpl(TreeImpl tree, StemImpl? growsOutOf, int level,
                    Transformation trf, double offset)
    {
        Tree      = tree;
        Level     = level;
        Transform = trf;
        _offset   = offset;
        Par       = tree.Params;
        LPar      = Par.GetLevelParams(level);

        if (growsOutOf != null)
        {
            if (growsOutOf.Level < level) Parent = growsOutOf;
            else { ClonedFrom = growsOutOf; Parent = growsOutOf.Parent; }
        }

        Segments = new List<SegmentImpl>(LPar.NCurveRes);
        if (LPar.NSegSplits > 0 || Par.BaseSplits0 > 0)
            Clones = new List<StemImpl>();
        if (level < Par.Levels - 1)
            Substems = new List<StemImpl>(Par.GetLevelParams(Math.Min(LPar.Level + 1, 3)).NBranches);
        if (level == Par.Levels - 1 && Par.Leaves != 0)
            Leaves = new List<LeafImpl>(Math.Abs(Par.Leaves));

        _maxX = _maxY = _maxZ = double.MinValue;
        _minX = _minY = _minZ = double.MaxValue;
    }

    public bool Make()
    {
        SegmentCount  = LPar.NCurveRes;
        Length        = StemLength();
        SegmentLength = Length / LPar.NCurveRes;
        _baseRadius   = StemBaseRadius();

        if (Level == 0)
        {
            double bw = Math.Max(_baseRadius, StemRadius(0));
            MinMaxTest(new Vector3d(bw, bw, 0));
        }

        if (Level > 0 && Par.PruneRatio > 0) Pruning();

        if (Length > MinStemLen && _baseRadius > MinStemRadius)
        {
            PrepareSubstemParams();
            MakeSegments(0, SegmentCount);
            return true;
        }
        return false;
    }

    private void Pruning()
    {
        LPar.SaveState();
        double splitcorr = SplitCorrection;
        double origLen   = Length;
        _pruneTest = true;
        int segm = MakeSegments(0, SegmentCount);

        while (segm >= 0 && Length > 0.001 * Par.ScaleTree)
        {
            LPar.RestoreState();
            SplitCorrection = splitcorr;
            Clones?.Clear(); Segments.Clear();
            double minLen = Length / 2;
            double maxLen = Length - origLen / 15;
            Length = Math.Min(Math.Max(SegmentLength * segm, minLen), maxLen);
            SegmentLength = Length / LPar.NCurveRes;
            _baseRadius = StemBaseRadius();
            if (Length > MinStemLen) segm = MakeSegments(0, SegmentCount);
        }

        Length = origLen - (origLen - Length) * Par.PruneRatio;
        LPar.RestoreState(); SplitCorrection = splitcorr;
        LPar.ClearSavedState();
        Clones?.Clear(); Segments.Clear();
        _pruneTest = false;
    }

    private int MakeSegments(int startSeg, int endSeg)
    {
        if (Level == 1) Tree.UpdateGenProgress();
        Transformation trf = Transform;

        for (int s = startSeg; s < endSeg; s++)
        {
            trf = NewDirection(trf, s);
            double rad1 = StemRadius(s * SegmentLength);
            double rad2 = StemRadius((s + 1) * SegmentLength);

            var segment = new SegmentImpl(this, s, trf, rad1, rad2);
            segment.Make();
            Segments.Add(segment);

            if (!_pruneTest && Level < Par.Levels - 1) MakeSubstems(segment);
            if (!_pruneTest && Level == Par.Levels - 1 && Par.Leaves != 0) MakeLeaves(segment);

            trf = trf.Translate(trf.GetZ().Mul(SegmentLength));
            if (_pruneTest && !IsInsideEnvelope(trf.GetT())) return s;

            if (s < endSeg - 1)
            {
                int segm = MakeClones(ref trf, s);
                if (segm >= 0) return segm;
            }
        }
        return -1;
    }

    private Transformation NewDirection(Transformation trf, int nsegm)
    {
        if (nsegm == 0) return trf;

        double delta;
        if (LPar.NCurveBack == 0)
            delta = LPar.NCurve / LPar.NCurveRes;
        else
            delta = nsegm < (LPar.NCurveRes + 1) / 2
                ? LPar.NCurve * 2 / LPar.NCurveRes
                : LPar.NCurveBack * 2 / LPar.NCurveRes;
        delta += SplitCorrection;
        trf = trf.RotX(delta);

        if (LPar.NCurveV > 0)
        {
            double dv  = LPar.Var(LPar.NCurveV) / LPar.NCurveRes;
            double rho = 180 + LPar.Var(180);
            trf = trf.RotAxisZ(dv, rho);
        }
        if (Par.AttractionUp != 0 && Level >= 1)
        {
            double decl    = Math.Acos(Math.Max(-1, Math.Min(1, trf.GetZ().Z)));
            double curveUp = Par.AttractionUp * Math.Abs(decl * Math.Sin(decl)) / LPar.NCurveRes;
            var z = trf.GetZ();
            trf = trf.RotAxis(-curveUp * 180 / Math.PI, new Vector3d(-z.Y, z.X, 0));
        }
        return trf;
    }

    internal double StemRadius(double h)
    {
        double z     = Math.Min(h / Length, 1.0);
        double taper = LPar.NTaper;
        double unitTaper = taper <= 1 ? taper : taper <= 2 ? 2 - taper : 0;
        double radius = _baseRadius * (1 - unitTaper * z);

        if (taper > 1)
        {
            double z2 = (1 - z) * Length;
            double depth = (taper < 2 || z2 < radius) ? 1 : taper - 2;
            double z3 = taper < 2 ? z2 : Math.Abs(z2 - 2 * radius * (int)(z2 / 2 / radius + 0.5));
            if (taper > 2 || z3 < radius)
                radius = (1 - depth) * radius + depth * Math.Sqrt(radius * radius - (z3 - radius) * (z3 - radius));
        }
        if (Level == 0)
        {
            if (Par.Flare != 0)
            {
                double y = Math.Max(0, 1 - 8 * z);
                radius *= 1 + Par.Flare * (Math.Pow(100, y) - 1) / 100.0;
            }
            radius *= Par.Scale0;
        }
        return radius;
    }

    private double StemBaseRadius()
    {
        if (Level == 0) return Length * Par.Ratio;
        double maxRadius = Parent!.StemRadius(_offset);
        double radius    = Parent._baseRadius * Math.Pow(Length / Parent.Length, Par.RatioPower);
        return Math.Min(radius, maxRadius);
    }

    private double StemLength()
    {
        if (Level == 0) return (LPar.NLength + LPar.Var(LPar.NLengthV)) * Par.ScaleTree;
        if (Level == 1)
        {
            double parLen = Parent!.Length;
            double baseLen = Par.BaseSize * Par.ScaleTree;
            double ratio  = (parLen - _offset) / (parLen - baseLen);
            return parLen * Parent.LengthChildMax * Par.GetShapeRatio(ratio);
        }
        return Parent!.LengthChildMax * (Parent.Length - 0.6 * _offset);
    }

    private void PrepareSubstemParams()
    {
        var lpar1      = Par.GetLevelParams(Level + 1);
        LengthChildMax = lpar1.NLength + lpar1.Var(lpar1.NLengthV);
        double stemsMax = lpar1.NBranches;
        double substemCnt;

        if (Level == 0)
        { substemCnt = stemsMax; SubstemsPerSegment = substemCnt / (double)SegmentCount / (1 - Par.BaseSize); }
        else if (Par.Preview)
        { substemCnt = stemsMax; SubstemsPerSegment = substemCnt / (double)SegmentCount; }
        else if (Level == 1)
        { substemCnt = (int)(stemsMax * (0.2 + 0.8 * Length / Parent!.Length / Parent.LengthChildMax));
          SubstemsPerSegment = substemCnt / (double)SegmentCount; }
        else
        { substemCnt = (int)(stemsMax * (1.0 - 0.5 * _offset / Parent!.Length));
          SubstemsPerSegment = substemCnt / (double)SegmentCount; }

        SubstemRotAngle = 0;
        if (Level == Par.Levels - 1)
            LeavesPerSegment = LeavesPerBranch() / SegmentCount;
    }

    private double LeavesPerBranch()
    {
        if (Par.Leaves == 0 || Level == 0) return 0;
        return Math.Abs(Par.Leaves)
            * Par.GetShapeRatio(_offset / Parent!.Length, Par.LeafDistrib)
            * Par.LeafQuality;
    }

    private void MakeSubstems(SegmentImpl segment)
    {
        var lpar1 = Par.GetLevelParams(Level + 1);
        double substPerSegm, offs;

        if (Level > 0)
        { substPerSegm = SubstemsPerSegment;
          offs = segment.Index == 0 ? Parent!.StemRadius(_offset) / SegmentLength : 0; }
        else if (segment.Index * SegmentLength > Par.BaseSize * Length)
        { substPerSegm = SubstemsPerSegment; offs = 0; }
        else if ((segment.Index + 1) * SegmentLength <= Par.BaseSize * Length)
            return;
        else
        { offs = (Par.BaseSize * Length - segment.Index * SegmentLength) / SegmentLength;
          substPerSegm = SubstemsPerSegment * (1 - offs); }

        int substemEff = (int)(substPerSegm + LPar.SubstemErrorValue + 0.5);
        LPar.SubstemErrorValue -= (substemEff - substPerSegm);
        if (substemEff <= 0) return;

        double dist  = (1.0 - offs) / substemEff * lpar1.NBranchDist;
        double distv = dist * 0.25;

        for (int s = 0; s < substemEff; s++)
        {
            double where  = offs + dist / 2 + s * dist + lpar1.Var(distv);
            double offset = (segment.Index + where) * SegmentLength;
            Transformation trf = SubstemDirection(segment.Transform, offset);
            trf = segment.SubstemPosition(trf, where);
            var substem = new StemImpl(Tree, this, Level + 1, trf, offset) { StemIndex = Substems!.Count };
            if (substem.Make()) Substems.Add(substem);
        }
    }

    private Transformation SubstemDirection(Transformation segTrf, double offset)
    {
        var lpar1 = Par.GetLevelParams(Level + 1);
        double rotAngle;
        if (lpar1.NRotate >= 0)
        { SubstemRotAngle = (SubstemRotAngle + lpar1.NRotate + lpar1.Var(lpar1.NRotateV) + 360) % 360;
          rotAngle = SubstemRotAngle; }
        else
        { if (Math.Abs(SubstemRotAngle) != 1) SubstemRotAngle = 1;
          SubstemRotAngle = -SubstemRotAngle;
          rotAngle = SubstemRotAngle * (180 + lpar1.NRotate + lpar1.Var(lpar1.NRotateV)); }

        double downAngle;
        if (lpar1.NDownAngleV >= 0)
            downAngle = lpar1.NDownAngle + lpar1.Var(lpar1.NDownAngleV);
        else
        { double len = Level == 0 ? Length * (1 - Par.BaseSize) : Length;
          downAngle = lpar1.NDownAngle + lpar1.NDownAngleV * (1 - 2 * Par.GetShapeRatio((Length - offset) / len, 0)); }
        return segTrf.RotXZ(downAngle, rotAngle);
    }

    private void MakeLeaves(SegmentImpl segment)
    {
        if (Par.Leaves > 0)
        {
            double leavesEff = (int)(LeavesPerSegment + Par.LeavesErrorValue + 0.5);
            Par.LeavesErrorValue -= (leavesEff - LeavesPerSegment);
            if (leavesEff <= 0) return;
            double offs = segment.Index == 0 ? Parent!.StemRadius(_offset) / SegmentLength : 0;
            double dist = (1.0 - offs) / leavesEff;
            for (int s = 0; s < (int)leavesEff; s++)
            {
                double where = offs + dist / 2 + s * dist + LPar.Var(dist / 2);
                double loffs = (segment.Index + where) * SegmentLength;
                Transformation trf = SubstemDirection(segment.Transform, loffs);
                trf = trf.Translate(segment.Transform.GetZ().Mul(where * SegmentLength));
                var leaf = new LeafImpl(trf); leaf.Make(Par); Leaves!.Add(leaf);
            }
        }
        else if (Par.Leaves < 0 && segment.Index == SegmentCount - 1)
        {
            var lpar1 = Par.GetLevelParams(Level + 1);
            int cnt   = (int)(LeavesPerBranch() + 0.5);
            Transformation trf = segment.Transform.Translate(segment.Transform.GetZ().Mul(SegmentLength));
            double distAngle = lpar1.NRotate / cnt;
            double varAngle  = lpar1.NRotateV / cnt;
            double downAngle = lpar1.NDownAngle;
            double varDown   = lpar1.NDownAngleV;
            double offsetAngle = cnt % 2 == 1 ? distAngle : distAngle / 2;
            if (cnt % 2 == 1) { var lf = new LeafImpl(trf); lf.Make(Par); Leaves!.Add(lf); }
            for (int s = 0; s < cnt / 2; s++)
                for (int rot = 1; rot >= -1; rot -= 2)
                {
                    Transformation t1 = trf.RotY(rot * (offsetAngle + s * distAngle + lpar1.Var(varAngle)));
                    t1 = t1.RotX(downAngle + lpar1.Var(varDown));
                    var lf = new LeafImpl(t1); lf.Make(Par); Leaves!.Add(lf);
                }
        }
    }

    private int MakeClones(ref Transformation trf, int nseg)
    {
        int segSplitsEff;
        if (Level == 0 && nseg == 0 && Par.BaseSplits0 > 0)
            segSplitsEff = Par.BaseSplits0;
        else
        { double segs = LPar.NSegSplits;
          segSplitsEff = (int)(segs + LPar.SplitErrorValue + 0.5);
          LPar.SplitErrorValue -= (segSplitsEff - segs); }

        if (segSplitsEff < 1) return -1;
        double sAngle = 360.0 / (segSplitsEff + 1);

        for (int i = 0; i < segSplitsEff; i++)
        {
            var clone = CloneStem(trf, nseg + 1);
            clone.Transform = clone.Split(trf, sAngle * (1 + i), nseg, segSplitsEff);
            int segm = clone.MakeSegments(nseg + 1, clone.SegmentCount);
            if (segm >= 0) return segm;
            Clones!.Add(clone);
        }
        trf = Split(trf, 0, nseg, segSplitsEff);
        return -1;
    }

    private StemImpl CloneStem(Transformation trf, int startSegm)
    {
        var c = new StemImpl(Tree, this, Level, trf, _offset)
        { SegmentLength = SegmentLength, SegmentCount = SegmentCount, Length = Length,
          _pruneTest = _pruneTest, StemIndex = StemIndex, SplitCorrection = SplitCorrection };
        c._baseRadius = _baseRadius;
        c.CloneIndex.AddRange(CloneIndex);
        c.CloneIndex.Add(Clones!.Count);
        if (!_pruneTest)
        { c.LengthChildMax = LengthChildMax; c.SubstemsPerSegment = SubstemsPerSegment;
          c.SubstemRotAngle = SubstemRotAngle + 180; c.LeavesPerSegment = LeavesPerSegment; }
        return c;
    }

    private Transformation Split(Transformation trf, double sAngle, int nseg, int nsplits)
    {
        int    remaining   = SegmentCount - nseg - 1;
        double declination = Math.Acos(Math.Max(-1, Math.Min(1, trf.GetZ().Z))) * 180 / Math.PI;
        double splitAngle  = Math.Max(0, LPar.NSplitAngle + LPar.Var(LPar.NSplitAngleV) - declination);
        trf = trf.RotX(splitAngle);
        if (remaining > 0) SplitCorrection -= splitAngle / remaining;

        if (sAngle > 0)
        {
            double sd;
            if (Par.BaseSplits0 > 0 && Level == 0 && nseg == 0)
                sd = sAngle + LPar.Var(LPar.NSplitAngleV);
            else
            { sd = 20 + 0.75 * (30 + Math.Abs(declination - 90)) * Math.Pow((LPar.Var(1) + 1) / 2.0, 2);
              if (LPar.Var(1) >= 0) sd = -sd; }
            trf = trf.RotAxis(sd, Vector3d.ZAxis);
        }
        if (!_pruneTest) SubstemsPerSegment /= (double)(nsplits + 1);
        return trf;
    }

    private bool IsInsideEnvelope(Vector3d v)
    {
        double r     = Math.Sqrt(v.X * v.X + v.Y * v.Y);
        double ratio = (Par.ScaleTree - v.Z) / (Par.ScaleTree * (1 - Par.BaseSize));
        return (r / Par.ScaleTree) < (Par.PruneWidth * Par.GetShapeRatio(ratio, TreeParams.Envelope));
    }

    internal void MinMaxTest(Vector3d pt)
    {
        pt.SetMaxCoord(ref _maxX, ref _maxY, ref _maxZ);
        pt.SetMinCoord(ref _minX, ref _minY, ref _minZ);
        if (ClonedFrom != null) ClonedFrom.MinMaxTest(pt);
        else if (Parent  != null) Parent.MinMaxTest(pt);
        else Tree.MinMaxTest(pt);
    }

    public bool TraverseTree(ITreeTraversal traversal)
    {
        if (traversal.EnterStem(this))
        {
            if (Leaves != null) foreach (var l in Leaves) if (!l.TraverseTree(traversal)) break;
            if (Substems != null) foreach (var s in Substems) if (!s.TraverseTree(traversal)) break;
            if (Clones != null) foreach (var c in Clones) if (!c.TraverseTree(traversal)) break;
        }
        return traversal.LeaveStem(this);
    }

    public IEnumerable<IStemSection> Sections()
    {
        foreach (var seg in Segments)
        {
            yield return seg;
            foreach (var ss in seg.Subsegments)
                yield return new SubsegmentSection(ss, seg);
        }
    }
}

internal sealed class SubsegmentSection : IStemSection
{
    private readonly SubsegmentImpl _ss;
    private readonly SegmentImpl    _seg;
    public SubsegmentSection(SubsegmentImpl ss, SegmentImpl seg) { _ss = ss; _seg = seg; }
    public int            Index             => _seg.Index;
    public double         Length            => _seg.Length;
    public Transformation Transform         => _seg.Transform;
    public double         LowerRadius       => _ss.Radius;
    public double         UpperRadius       => _ss.Next?.Radius ?? _ss.Radius;
    public Vector3d       LowerPosition     => _ss.Position;
    public Vector3d       UpperPosition     => _ss.Next?.Position ?? _ss.Position;
    public bool           IsLastStemSegment => _seg.IsLastStemSegment;
    public Vector3d[]     GetSectionPoints() => _seg.GetSectionPointsAt(_ss.Radius, _ss.Position);
}