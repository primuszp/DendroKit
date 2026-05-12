using DendroKit.Core.Geom;
using DendroKit.Core.Params;

namespace DendroKit.Core.Tree;

internal sealed class SegmentImpl : IStemSection
{
    public int            Index          { get; }
    public Transformation Transform      { get; }
    public double         LowerRadius    { get; }
    public double         UpperRadius    { get; }
    public double         Length         { get; }

    public Vector3d LowerPosition => Transform.GetT();
    public Vector3d UpperPosition => Transform.GetT().Add(Transform.GetZ().Mul(Length));

    public bool IsLastStemSegment => Index == _stem.SegmentCount - 1;

    private readonly StemImpl    _stem;
    private readonly TreeParams  _par;
    private readonly LevelParams _lpar;

    internal readonly List<SubsegmentImpl> Subsegments = new(10);

    public SegmentImpl(StemImpl stem, int index, Transformation trf, double rad1, double rad2)
    {
        _stem  = stem;
        _par   = stem.Par;
        _lpar  = stem.LPar;
        Index  = index;
        Transform   = trf;
        LowerRadius = rad1;
        UpperRadius = rad2;
        Length = stem.SegmentLength;
    }

    public void Make()
    {
        if (_lpar.NCurveV < 0)
            MakeHelix(10);
        else if (_lpar.NTaper > 1 && _lpar.NTaper <= 2 && IsLastStemSegment)
            MakeSphericalEnd(10);
        else if (_lpar.NTaper > 2)
            MakeSubsegments(20);
        else if (_lpar.Level == 0 && _par.Flare != 0 && Index == 0)
            MakeFlare(10);
        else
            MakeSubsegments(1);

        MinMaxTest();
    }

    private void AddSubsegment(SubsegmentImpl ss)
    {
        if (Subsegments.Count > 0)
        {
            var last = Subsegments[^1];
            last.Next = ss;
            ss.Prev   = last;
        }
        Subsegments.Add(ss);
    }

    private void MakeSubsegments(int cnt)
    {
        var dir = UpperPosition.Sub(LowerPosition);
        for (int i = 1; i <= cnt; i++)
        {
            double pos = i * Length / cnt;
            double rad = _stem.StemRadius(Index * Length + pos);
            AddSubsegment(new SubsegmentImpl(LowerPosition.Add(dir.Mul(pos / Length)), rad, pos, this));
        }
    }

    private void MakeSphericalEnd(int cnt)
    {
        var dir = UpperPosition.Sub(LowerPosition);
        for (int i = 1; i < cnt; i++)
        {
            double pos = Length - Length / Math.Pow(2, i);
            double rad = _stem.StemRadius(Index * Length + pos);
            AddSubsegment(new SubsegmentImpl(LowerPosition.Add(dir.Mul(pos / Length)), rad, pos, this));
        }
        AddSubsegment(new SubsegmentImpl(UpperPosition, UpperRadius, Length, this));
    }

    private void MakeFlare(int cnt)
    {
        var dir = UpperPosition.Sub(LowerPosition);
        for (int i = cnt - 1; i >= 0; i--)
        {
            double pos = Length / Math.Pow(2, i);
            double rad = _stem.StemRadius(Index * Length + pos);
            AddSubsegment(new SubsegmentImpl(LowerPosition.Add(dir.Mul(pos / Length)), rad, pos, this));
        }
    }

    private void MakeHelix(int cnt)
    {
        double angle = Math.Abs(_lpar.NCurveV) / 180 * Math.PI;
        double rad   = Math.Sqrt(1.0 / (Math.Cos(angle) * Math.Cos(angle)) - 1) * Length / Math.PI / 2.0;
        for (int i = 1; i <= cnt; i++)
        {
            var pos = new Vector3d(
                rad * Math.Cos(2 * Math.PI * i / cnt) - rad,
                rad * Math.Sin(2 * Math.PI * i / cnt),
                i * Length / cnt);
            double srad = _stem.StemRadius(Index * Length + i * Length / cnt);
            AddSubsegment(new SubsegmentImpl(Transform.Apply(pos), srad, i * Length / cnt, this));
        }
    }

    private void MinMaxTest()
    {
        _stem.MinMaxTest(UpperPosition);
        _stem.MinMaxTest(LowerPosition);
    }

    public Transformation SubstemPosition(Transformation trf, double where)
    {
        if (_lpar.NCurveV >= 0)
            return trf.Translate(Transform.GetZ().Mul(where * Length));

        int idx = (int)(where * (Subsegments.Count - 1));
        var p1  = Subsegments[idx].Position;
        var p2  = Subsegments[Math.Min(idx + 1, Subsegments.Count - 1)].Position;
        var pos = p1.Add(p2.Sub(p1).Mul(where - (double)idx / (Subsegments.Count - 1)));
        return trf.Translate(pos.Sub(LowerPosition));
    }

    public Vector3d[] GetSectionPoints() =>
        GetSectionPointsAt(LowerRadius, Transform.GetT());

    internal Vector3d[] GetSectionPointsAt(double radius, Vector3d center)
    {
        int ptCnt = _lpar.MeshPoints;

        if (radius < 1e-6)
            return [center];

        var points = new Vector3d[ptCnt];
        for (int i = 0; i < ptCnt; i++)
        {
            double ang = i * 360.0 / ptCnt;
            if (_lpar.Level == 0 && _par.Lobes != 0)
                ang -= 10.0 / _par.Lobes;

            var pt = new Vector3d(Math.Cos(ang * Math.PI / 180), Math.Sin(ang * Math.PI / 180), 0);

            if (_lpar.Level == 0 && (_par.Lobes != 0 || _par.ScaleV0 != 0))
            {
                double r1 = radius * (1 + _par.Random!.Uniform(-_par.ScaleV0, _par.ScaleV0) / Subsegments.Count);
                pt = pt.Mul(r1 * (1.0 + _par.LobeDepth * Math.Cos(_par.Lobes * ang * Math.PI / 180.0)));
            }
            else
            {
                pt = pt.Mul(radius);
            }
            // apply only the rotation part of the segment transform, then offset to center
            points[i] = center.Add(Transform.ApplyRotation(pt));
        }
        return points;
    }
}