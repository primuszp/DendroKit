using DendroKit.Core.Tree;

namespace DendroKit.Core.Mesh;

/// <summary>
/// Collection of all mesh parts for a tree, organized by branch level.
/// </summary>
public sealed class TreeMesh
{
    private readonly List<MeshPart> _parts = new();
    private readonly int[]          _firstPartByLevel;

    public TreeMesh(int levels)
    {
        _firstPartByLevel = new int[Math.Max(levels, 1)];
        Array.Fill(_firstPartByLevel, -1);
    }

    public void AddPart(MeshPart part)
    {
        int level = part.Level;
        if (level < _firstPartByLevel.Length && _firstPartByLevel[level] < 0)
            _firstPartByLevel[level] = _parts.Count;
        _parts.Add(part);
    }

    public IReadOnlyList<MeshPart> Parts => _parts;

    public MeshPart? FirstPartAtLevel(int level) =>
        level < _firstPartByLevel.Length && _firstPartByLevel[level] >= 0
            ? _parts[_firstPartByLevel[level]]
            : null;

    public int VertexCount()
    {
        int cnt = 0;
        foreach (var p in _parts) cnt += p.VertexCount();
        return cnt;
    }
}
