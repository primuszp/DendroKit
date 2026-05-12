using DendroKit.Core.Tree;

namespace DendroKit.Core.Mesh;

/// <summary>
/// Builds a TreeMesh from an ITree by traversal.
/// </summary>
public sealed class MeshGenerator
{
    public bool UseQuads { get; }

    public MeshGenerator(bool useQuads = false) { UseQuads = useQuads; }

    public TreeMesh CreateStemMesh(ITree tree)
    {
        int levels = tree is TreeImpl ti ? ti.Params.Levels : 4;
        var mesh   = new TreeMesh(levels);
        tree.TraverseTree(new MeshCreator(mesh, -1, UseQuads));
        return mesh;
    }

    public TreeMesh CreateStemMeshByLevel(ITree tree)
    {
        int levels = tree is TreeImpl ti ? ti.Params.Levels : 4;
        var mesh   = new TreeMesh(levels);
        for (int l = 0; l < levels; l++)
            tree.TraverseTree(new MeshCreator(mesh, l, UseQuads));
        return mesh;
    }
}

/// <summary>Visitor that builds mesh parts for all stems.</summary>
internal sealed class MeshCreator : DefaultTreeTraversal
{
    private readonly TreeMesh _mesh;
    private readonly int      _targetLevel;
    private readonly bool     _useQuads;

    public MeshCreator(TreeMesh mesh, int targetLevel, bool useQuads)
    {
        _mesh        = mesh;
        _targetLevel = targetLevel;
        _useQuads    = useQuads;
    }

    public override bool EnterStem(IStem stem)
    {
        if (_targetLevel >= 0 && stem.Level != _targetLevel) return true;

        var part  = new MeshPart(stem, useNormals: true, _useQuads);
        bool first = true;

        foreach (var sec in stem.Sections())
        {
            var pts     = sec.GetSectionPoints();
            var section = new MeshSection { IsFirst = first };
            first = false;

            foreach (var pt in pts)
                section.AddVertex(new Vertex(pt));

            part.AddSection(section);
        }

        part.SetNormals();
        _mesh.AddPart(part);
        return true;
    }
}
