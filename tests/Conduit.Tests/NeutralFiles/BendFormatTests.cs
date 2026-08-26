using Conduit.Tests.TestHelpers;
using Xunit;

namespace Conduit.Tests.NeutralFiles;

/// <summary>
/// Coverage for <see cref="NeutralFileFixtureBuilder"/>'s <c>#$ BEND</c> support — added per
/// direct instruction after the "50 m 3D loop" test file's original zigzag geometry (no return
/// path, no bends) was corrected to a proper expansion loop. See
/// <c>NeutralFileFixtureBuilder.BuildBendLines</c>'s doc comment for the confirmed real-sample
/// conventions this follows.
/// </summary>
public class BendFormatTests
{
    private static readonly List<NeutralFileFixtureBuilder.PipeSegmentSpec> LoopSegments =
    [
        NeutralFileFixtureBuilder.Schedule40Run(10, 20, 1000),
        NeutralFileFixtureBuilder.Schedule40Riser(20, 30, 500),
        NeutralFileFixtureBuilder.Schedule40Run(30, 40, 1000),
    ];

    [Fact]
    public void EachElement_BendPointer_MatchesItsToNodesOneBasedPositionInBendNodes()
    {
        var file = NeutralFileFixtureBuilder.Build(LoopSegments, anchorNodes: [10, 40], bendNodes: [20, 30]);

        Assert.Equal(1, file.Elements[0].AuxiliaryPointers[0]); // 10->20: ToNode 20 is bendNodes[0]
        Assert.Equal(2, file.Elements[1].AuxiliaryPointers[0]); // 20->30: ToNode 30 is bendNodes[1]
        Assert.Equal(0, file.Elements[2].AuxiliaryPointers[0]); // 30->40: ToNode 40 isn't a bend
    }

    [Fact]
    public void ControlSection_NumBends_MatchesBendNodeCount()
    {
        var file = NeutralFileFixtureBuilder.Build(LoopSegments, anchorNodes: [10, 40], bendNodes: [20, 30]);

        Assert.Equal(2, file.Control.NumBends);
        Assert.Equal(2, file.Blocks.First(b => b.Name == "BEND").RawLines.Count / 3);
    }

    [Fact]
    public void BendRecord_UsesConfirmedRealSampleDefaults_AndCornerMinusOneMinusTwoTangentNodes()
    {
        var file = NeutralFileFixtureBuilder.Build(LoopSegments, anchorNodes: [10, 40], bendNodes: [20, 30]);
        var bendLines = file.Blocks.First(b => b.Name == "BEND").RawLines;

        // First bend is for corner node 20 -> tangent nodes 19, 18 (corner - 1, corner - 2).
        Assert.Equal(
        [
            "   3.810000E+02 0.000000E+00-2.020200E+00 1.900000E+01 0.000000E+00 1.800000E+01",
            "   0.000000E+00 0.000000E+00 0.000000E+00 4.191000E+00 0.000000E+00 0.000000E+00",
            "   0.000000E+00 0.000000E+00",
        ],
        bendLines.Take(3));
    }

    [Fact]
    public void NoBendNodes_ProducesEmptyBendSection_AndAllZeroPointers()
    {
        var file = NeutralFileFixtureBuilder.Build(LoopSegments, anchorNodes: [10, 40]);

        Assert.Equal(0, file.Control.NumBends);
        Assert.Empty(file.Blocks.First(b => b.Name == "BEND").RawLines);
        Assert.All(file.Elements, e => Assert.Equal(0, e.AuxiliaryPointers[0]));
    }
}
