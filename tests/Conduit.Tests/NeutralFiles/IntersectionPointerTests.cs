using Conduit.Core.NeutralFiles;
using Xunit;

namespace Conduit.Tests.NeutralFiles;

/// <summary>
/// Regression coverage for <see cref="Element.IntersectionPointer"/> (the real <c>#$ SIF&amp;TEES</c>
/// pointer, vendor doc's "Pointer to Intersection Auxiliary field", <c>AuxiliaryPointers[10]</c>) —
/// per direct instruction (2026-09-01, "determine a tee by its tee/sif pointer, not by the actual
/// geometry"), checked against a real user-supplied sample (<c>fixtures/real-samples/NEWTEST.cii</c>)
/// that specifically demonstrates why node degree alone is unreliable: 3 of its 4 real
/// tee/intersection nodes (160, 1007, 1120) have ordinary degree-2 geometry — no branch pipe
/// modeled in this file at all — and would have been missed entirely by a degree-based guess. Only
/// node 895 happens to also have real branch geometry (an element from 895 to 1270).
/// </summary>
public class IntersectionPointerTests
{
    private static string RealSamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "real-samples", name);

    [Theory]
    [InlineData(130, 160)]
    [InlineData(1000, 1007)]
    [InlineData(1105, 1120)]
    [InlineData(880, 895)]
    public void RealSample_ElementEndingAtAnIntersectionNode_HasANonZeroIntersectionPointer(int fromNode, int toNode)
    {
        var file = NeutralFileReader.Read(RealSamplePath("NEWTEST.cii"));

        var element = Assert.Single(file.Elements, e => e.FromNode == fromNode && e.ToNode == toNode);

        Assert.NotEqual(0, element.IntersectionPointer);
    }

    /// <summary>
    /// The whole point of this fixture: three of the four real intersections have no branch pipe
    /// modeled here at all (an ordinary two-element chain through the node), so a node-degree-based
    /// guess would have missed them. Node 895 is the one exception — it also happens to have real
    /// branch geometry (element 895-&gt;1270) — confirming degree and the SIF pointer aren't
    /// interchangeable signals.
    /// </summary>
    [Theory]
    [InlineData(160)]
    [InlineData(1007)]
    [InlineData(1120)]
    public void RealSample_MostIntersectionNodes_HaveOnlyOrdinaryTwoElementDegree_NotABranch(int node)
    {
        var file = NeutralFileReader.Read(RealSamplePath("NEWTEST.cii"));

        var degree = file.Elements.Count(e => e.FromNode == node) + file.Elements.Count(e => e.ToNode == node);

        Assert.Equal(2, degree);
    }

    [Fact]
    public void RealSample_Node895_HasBothAnIntersectionPointerAndRealBranchGeometry()
    {
        var file = NeutralFileReader.Read(RealSamplePath("NEWTEST.cii"));

        var degree = file.Elements.Count(e => e.FromNode == 895) + file.Elements.Count(e => e.ToNode == 895);
        Assert.True(degree > 2, "node 895 should have real branch geometry (a third element, 895->1270)");
        Assert.Contains(file.Elements, e => e.FromNode == 895 && e.ToNode == 1270);
    }

    [Fact]
    public void RealSample_ElementsWithNoIntersection_HaveAZeroIntersectionPointer()
    {
        var file = NeutralFileReader.Read(RealSamplePath("NEWTEST.cii"));

        // A completely ordinary element far from any of the four known intersections.
        var element = Assert.Single(file.Elements, e => e.FromNode == 100 && e.ToNode == 130);

        Assert.Equal(0, element.IntersectionPointer);
    }
}
