using DendroKit.Core.Mesh;
using DendroKit.Core.Params;
using DendroKit.Core.Tree;
using DendroKit.WpfApp;
using Xunit;

namespace DendroKit.Tests;

public sealed class RegressionTests
{
    [Fact]
    public void TreeRandom_GetSetState_MatchesArbaroContract()
    {
        var random = new TreeRandom(12345);
        _ = random.NextDouble();

        long savedState = random.GetState();
        double nextAfterGetState = random.NextDouble();

        var manual = new TreeRandom(12345);
        _ = manual.NextDouble();
        long expectedState = manual.NextLong();
        manual.SetSeed(expectedState);
        double expectedNext = manual.NextDouble();

        Assert.Equal(expectedState, savedState);
        Assert.Equal(expectedNext, nextAfterGetState);
    }

    [Theory]
    [InlineData("LeafBend", "2")]
    [InlineData("Shape", "99")]
    [InlineData("PruneRatio", "-0.1")]
    public void TreeParams_RejectsOutOfRangeValues(string name, string value)
    {
        var p = new TreeParams();
        Assert.Throws<ParamException>(() => p.SetParam(name, value));
    }

    [Fact]
    public void TreeParams_EnableDisable_FollowsArbaroRules()
    {
        var p = new TreeParams();

        Assert.False(p.GetParam("LeafShape").Enabled);
        Assert.False(p.GetParam("LeafScale").Enabled);
        Assert.False(p.GetParam("3DownAngle").Enabled);
        Assert.True(p.GetParam("2DownAngle").Enabled);

        p.SetParam("Leaves", "12");

        Assert.True(p.GetParam("LeafShape").Enabled);
        Assert.True(p.GetParam("LeafScale").Enabled);
        Assert.True(p.GetParam("3DownAngle").Enabled);

        p.SetParam("Levels", "2");

        Assert.False(p.GetParam("2Length").Enabled);
        Assert.True(p.GetParam("2DownAngle").Enabled);
        Assert.False(p.GetParam("3DownAngle").Enabled);
    }

    [Fact]
    public void ParamViewModel_RevertsRejectedNumericText()
    {
        var p = new TreeParams();
        var leafBend = (FloatParam)p.GetParam("LeafBend");
        var vm = new ParamViewModel(leafBend);

        vm.TextValue = "2";

        Assert.Equal(0.3, leafBend.DoubleValue(), 12);
        Assert.Equal("0.3", vm.TextValue);
    }

    [Fact]
    public void BundledTrees_GenerateStemAndMesh()
    {
        string treeDir = Path.Combine(GetSourceRoot(), "DendroKit.View", "Trees");
        foreach (string xmlPath in Directory.EnumerateFiles(treeDir, "*.xml"))
        {
            using var stream = File.OpenRead(xmlPath);
            var p = new TreeParams();
            p.ReadFromXml(stream);

            var tree = new TreeImpl(14, p);
            tree.Make();
            var mesh = new MeshGenerator().CreateStemMesh(tree);

            Assert.True(tree.StemCount > 0, xmlPath);
            Assert.True(mesh.VertexCount() > 0, xmlPath);
            Assert.False(double.IsNaN(tree.Height), xmlPath);
            Assert.False(double.IsNaN(tree.Width), xmlPath);
        }
    }

    private static string GetSourceRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "DendroKit.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Could not locate source root.");
    }
}
