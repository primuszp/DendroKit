using DendroKit.Core.Geom;

namespace DendroKit.Core.Mesh;

public sealed class Vertex
{
    public Vector3d  Point  { get; set; }
    public Vector3d? Normal { get; set; }
    public UVVector? UV     { get; set; }

    public Vertex(Vector3d point) { Point = point; }
}

public readonly struct UVVector(double u, double v)
{
    public double U { get; } = u;
    public double V { get; } = v;
}

public readonly struct Face
{
    public readonly int A, B, C, D;
    public readonly bool IsQuad;

    public Face(int a, int b, int c) { A = a; B = b; C = c; D = 0; IsQuad = false; }
    public Face(int a, int b, int c, int d) { A = a; B = b; C = c; D = d; IsQuad = true; }
}

public sealed class VFace
{
    public Vertex A { get; }
    public Vertex B { get; }
    public Vertex C { get; }

    public VFace(Vertex a, Vertex b, Vertex c) { A = a; B = b; C = c; }
}
