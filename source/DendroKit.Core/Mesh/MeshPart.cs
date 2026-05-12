using DendroKit.Core.Geom;
using DendroKit.Core.Tree;

namespace DendroKit.Core.Mesh;

public sealed class MeshPart
{
    private readonly List<MeshSection> _sections = new();

    public IStem Stem      { get; }
    public bool  UseNormals{ get; }
    public bool  UseQuads  { get; }
    public int   Level     => Stem.Level;

    public MeshPart(IStem stem, bool useNormals, bool useQuads)
    {
        Stem       = stem;
        UseNormals = useNormals;
        UseQuads   = useQuads;
    }

    public void AddSection(MeshSection section)
    {
        if (_sections.Count > 0)
        {
            var last = _sections[^1];
            last.Next        = section;
            section.Previous = last;
        }
        _sections.Add(section);
    }

    public IReadOnlyList<MeshSection> Sections => _sections;

    public int VertexCount()
    {
        int cnt = 0;
        for (int i = 1; i < _sections.Count; i++)
            cnt += _sections[i].Count;
        return cnt;
    }

    public int FaceCount()
    {
        int cnt = 0;
        for (int i = 1; i < _sections.Count - 1; i++)
        {
            int ci  = _sections[i].Count;
            int ci1 = _sections[i+1].Count;
            if (ci != ci1)         cnt += Math.Max(ci, ci1);
            else if (ci > 1)       cnt += 2 * ci;
        }
        return cnt;
    }

    /// <summary>Returns flat Face indices starting at startIndex.</summary>
    public IEnumerable<Face> AllFaces(int startIndex)
    {
        int idx = startIndex;
        for (int i = 0; i < _sections.Count - 1; i++)
        {
            var cur  = _sections[i];
            var next = _sections[i + 1];

            if (cur.IsFirst)
            {
                for (int j = 1; j < next.Count - 1; j++)
                    yield return new Face(idx, idx + j, idx + j + 1);
                idx += next.Count;
                continue;
            }

            int nidx = idx + cur.Count;

            if (cur.Count == 1)
            {
                for (int j = 0; j < next.Count; j++)
                    yield return new Face(idx, nidx + j, nidx + (j + 1) % next.Count);
            }
            else if (next.Count == 1)
            {
                for (int j = 0; j < cur.Count; j++)
                    yield return new Face(idx + j, nidx, idx + (j + 1) % cur.Count);
            }
            else
            {
                for (int j = 0; j < cur.Count; j++)
                {
                    if (UseQuads)
                        yield return new Face(idx + j, nidx + j,
                            nidx + (j + 1) % next.Count, idx + (j + 1) % cur.Count);
                    else
                    {
                        yield return new Face(idx + j, nidx + j, idx + (j + 1) % cur.Count);
                        yield return new Face(idx + (j + 1) % cur.Count, nidx + j, nidx + (j + 1) % next.Count);
                    }
                }
            }

            idx += cur.Count;
        }
    }

    /// <summary>Returns VFace (vertex + normal) pairs.</summary>
    public IEnumerable<VFace> AllVFaces()
    {
        for (int i = 0; i < _sections.Count - 1; i++)
        {
            var cur  = _sections[i];
            var next = _sections[i + 1];
            if (cur.Count == 1 && next.Count == 1) continue;

            if (cur.IsFirst)
            {
                for (int j = 1; j < next.Count - 1; j++)
                    yield return new VFace(next.PointAt(0), next.PointAt(j), next.PointAt(j + 1));
                continue;
            }

            if (cur.Count == 1)
            {
                for (int j = 0; j < next.Count; j++)
                    yield return new VFace(cur.PointAt(0), next.PointAt(j), next.PointAt((j + 1) % next.Count));
            }
            else if (next.Count == 1)
            {
                for (int j = 0; j < cur.Count; j++)
                    yield return new VFace(cur.PointAt(j), next.PointAt(0), cur.PointAt((j + 1) % cur.Count));
            }
            else
            {
                for (int j = 0; j < cur.Count; j++)
                {
                    yield return new VFace(cur.PointAt(j), next.PointAt(j), cur.PointAt((j + 1) % cur.Count));
                    yield return new VFace(cur.PointAt((j + 1) % cur.Count), next.PointAt(j), next.PointAt((j + 1) % next.Count));
                }
            }
        }
    }

    public void SetNormals()
    {
        if (_sections.Count <= 1) return;
        _sections[1].SetNormalsUp();
        for (int i = 2; i < _sections.Count - 1; i++)
            _sections[i].SetNormalsUpDown();
    }
}
