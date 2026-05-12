using DendroKit.Core.Geom;
using DendroKit.Core.Params;

namespace DendroKit.Core.Tree;

internal sealed class LeafImpl : ILeaf
{
    public Transformation Transform { get; private set; }

    public LeafImpl(Transformation trf)
    {
        Transform = trf;
    }

    public void Make(TreeParams par)
    {
        double leafBend = par.LeafBend;
        if (leafBend == 0) return;

        var z    = Transform.GetZ();
        var tPos = Transform.GetT();

        double dist = Math.Sqrt(tPos.X * tPos.X + tPos.Y * tPos.Y);
        if (dist > 1e-7)
        {
            double declination = Math.Acos(Math.Max(-1, Math.Min(1, z.Z)));
            double bendAngle   = leafBend * declination * 180 / Math.PI;
            Transform = Transform.RotAxis(bendAngle, new Vector3d(-z.Y, z.X, 0));
        }

        var z2 = Transform.GetZ();
        double up = leafBend * Math.Acos(Math.Max(-1, Math.Min(1, z2.Z))) * 180 / Math.PI;
        Transform = Transform.RotAxis(-up, new Vector3d(-z2.Y, z2.X, 0));
    }

    public bool TraverseTree(ITreeTraversal traversal) =>
        traversal.VisitLeaf(this);
}