using System.Linq;
using Conduit.Core.Heuristics;
using Conduit.Core.NeutralFiles;
using Xunit;

namespace Conduit.Tests.Heuristics;

/// <summary>
/// Regression coverage for <see cref="SupportPlacer"/>'s rigid-with-weight exclusion, per direct
/// instruction (2026-09-03): "Any node with a connecting rigid with weight should not have a
/// support placed on it" — after a real run placed a support at the starting node of a flange.
/// Checked against the real, restraint-free <c>fixtures/real-samples/44002.cii</c>, which has a
/// literal element named "Flange" (10-&gt;20, rigid weight 400) among 5 other weighted rigids
/// (valves/similar) and 5 more with zero weight (structural, not real equipment mass).
/// </summary>
public class RigidWeightExclusionTests
{
    private static string RealSamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "real-samples", name);

    /// <summary>Every node touching one of 44002.cii's 6 real weighted-rigid elements (both endpoints of each), confirmed via direct inspection of the file's own #$ RIGID/#$ ELEMENTS data.</summary>
    private static readonly int[] WeightedRigidNodes = [10, 20, 40, 50, 60, 70, 200, 210, 220];

    [Fact]
    public void RealSample_HasWeightedAndMasslessRigids_ConfirmingTheDistinctionIsReal()
    {
        var file = NeutralFileReader.Read(RealSamplePath("44002.cii"));

        Assert.Equal(11, file.RigidElements.Count);
        Assert.Contains(file.RigidElements, r => r.Weight != 0);
        Assert.Contains(file.RigidElements, r => r.Weight == 0);
    }

    [Fact]
    public void RealSample_FlangeElement_IsIdentifiedAsAWeightedRigid()
    {
        var file = NeutralFileReader.Read(RealSamplePath("44002.cii"));

        var flange = Assert.Single(file.Elements, e => e.FromNode == 10 && e.ToNode == 20);
        Assert.Contains("Flange", flange.Name);
        var rigid = file.TryGetRigidElement(flange);
        Assert.NotNull(rigid);
        Assert.NotEqual(0, rigid!.Weight);
    }

    [Fact]
    public void PlaceSupports_OnRealSample_NeverPlacesOnOrNearAWeightedRigidNode()
    {
        var file = NeutralFileReader.Read(RealSamplePath("44002.cii"));
        var positions = file.ComputeNodePositions();
        var toMillimetres = file.Units.LengthToMillimetres;

        var placed = SupportPlacer.PlaceSupports(file);

        Assert.NotEmpty(placed); // sanity: this run does have candidate spans to resolve
        foreach (var support in placed)
        {
            Assert.DoesNotContain(support.Node, WeightedRigidNodes);

            if (!positions.TryGetValue(support.Node, out var supportPos))
            {
                continue;
            }
            foreach (var rigidNode in WeightedRigidNodes)
            {
                if (!positions.TryGetValue(rigidNode, out var rigidPos))
                {
                    continue;
                }
                var dx = (supportPos.X - rigidPos.X) * toMillimetres;
                var dy = (supportPos.Y - rigidPos.Y) * toMillimetres;
                var dz = (supportPos.Z - rigidPos.Z) * toMillimetres;
                var distanceMillimetres = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
                Assert.True(distanceMillimetres >= SupportPlacer.DiscontinuityClearanceMillimetres,
                    $"support at node {support.Node} is only {distanceMillimetres:F1} mm from weighted-rigid node {rigidNode}");
            }
        }
    }
}
