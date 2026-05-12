using DendroKit.Core.Mesh;
using DendroKit.Core.Tree;

namespace DendroKit.Core.Export;

/// <summary>
/// Wavefront OBJ exporter for stem mesh.
/// </summary>
public sealed class ObjExporter
{
    public void Write(ITree tree, TreeMesh mesh, TextWriter writer)
    {
        writer.WriteLine("# DendroKit OBJ export");
        writer.WriteLine($"# Species: {tree.Species}");
        writer.WriteLine($"# Stems: {tree.StemCount}  Leaves: {tree.LeafCount}");
        writer.WriteLine();

        int vertexOffset = 1; // OBJ is 1-indexed

        foreach (var part in mesh.Parts)
        {
            writer.WriteLine($"g stem_{part.Stem.TreePosition}");

            var startOffset = vertexOffset;

            // write vertices
            foreach (var section in part.Sections)
            {
                foreach (var v in section.AllVertices())
                {
                    var p = v.Point;
                    writer.WriteLine($"v {F(p.X)} {F(p.Z)} {F(-p.Y)}"); // Z-up to Y-up
                    vertexOffset++;
                }
            }

            // write normals
            bool hasNormals = part.Sections.Any(s => s.AllVertices().Any(v => v.Normal != null));
            if (hasNormals)
            {
                foreach (var section in part.Sections)
                {
                    foreach (var v in section.AllVertices())
                    {
                        if (v.Normal is { } n)
                            writer.WriteLine($"vn {F(n.X)} {F(n.Z)} {F(-n.Y)}");
                        else
                            writer.WriteLine("vn 0.000000 1.000000 0.000000");
                    }
                }
            }

            // write faces
            foreach (var face in part.AllFaces(startOffset))
            {
                if (face.IsQuad)
                    writer.WriteLine($"f {face.A} {face.B} {face.C} {face.D}");
                else
                    writer.WriteLine($"f {face.A} {face.B} {face.C}");
            }
        }

        writer.Flush();
    }

    private static string F(double v) =>
        v.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
}
