using Conduit.Core.Heuristics;
using Conduit.Core.NeutralFiles;
using Conduit.Core.Optimization;
using Conduit.Core.Stress;
using Conduit.Tests.TestHelpers;
using Xunit;

namespace Conduit.Tests.Optimization;

public class OptimizationLoopTests
{
    /// <summary>The max allowable span the formula computes for the fixture builder's standard 6" Sch 40 run.</summary>
    private static double Schedule40MaxSpan() =>
        SpanLimitCalculator.ComputeMaxSpan(NeutralFileFixtureBuilder.Schedule40Run(1, 2, 1).ToElement());

    [Fact]
    public void StraightRun_PassesAfterInitialPlacement()
    {
        var segments = Enumerable.Range(0, 18)
            .Select(i => NeutralFileFixtureBuilder.Schedule40Run(10 + (i * 10), 20 + (i * 10), 1270))
            .ToList();
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 190]);

        var result = OptimizationLoop.Run(file, new MockStressSolver());

        Assert.True(result.Passed);
        Assert.Empty(result.FinalStressResult.Findings.Where(f => !f.Passed));
        Assert.NotEmpty(result.InitialPlacements);
    }

    [Fact]
    public void SingleOverlongElement_GetsSplitIntoEvenChunks_AndPasses()
    {
        // One element, far too long for its own max allowable span, directly between two
        // anchors with no intermediate node to add a support at — per direct instruction, the
        // loop now splits it into evenly-spaced chunks (rounded down to the nearest metre) with
        // a new rest support at each interior node, rather than reporting an unresolvable
        // failure. No spring logic in the MVP either way.
        var maxSpan = Schedule40MaxSpan();
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            NeutralFileFixtureBuilder.Schedule40Run(10, 20, maxSpan * 10),
        };
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 20]);

        var result = OptimizationLoop.Run(file, new MockStressSolver());

        Assert.True(result.Passed);
        Assert.True(file.Elements.Count > 1, "the single overlong element should have been split into several shorter ones");
        Assert.True(file.Restraints.Count > 2, "new interior-node restraints should have been added beyond the 2 anchors");
    }

    [Fact]
    public void OverlongSegmentPastAnAddedRestSupport_AlsoGetsSplitAndPasses()
    {
        // A short first leg (fits under the max span) followed by a long second leg with no
        // further intermediate node — SupportPlacer's initial pass places a rest support at the
        // node between them, and the loop now splits the still-overlong second leg to resolve
        // it too, rather than reporting a failure past that point.
        var maxSpan = Schedule40MaxSpan();
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            NeutralFileFixtureBuilder.Schedule40Run(10, 15, maxSpan * 0.5),
            NeutralFileFixtureBuilder.Schedule40Run(15, 20, maxSpan * 10),
        };
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 20]);

        var result = OptimizationLoop.Run(file, new MockStressSolver());

        Assert.True(result.Passed);
        Assert.Contains(file.Restraints, r => r.Node == 15 && r.Dofs[0].Type == RestraintType.PlusY);
        Assert.True(file.Restraints.Count > 3, "the overlong second leg should have gained its own interior restraints");
    }

    [Fact]
    public void UnsplittableElement_IsStillReportedRatherThanLoopedForever()
    {
        // An unrealistically dense (but nonzero) pipe has a max allowable span under 1 m, which
        // rounds down to a 0 mm chunk size — ElementSplitter can't chunk that, so this stays a
        // genuinely irreducible failure, reported rather than looped on. No spring escalation in
        // the MVP.
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            new(10, 20, DeltaX: 50000, DeltaY: 0, DeltaZ: 0, OutsideDiameter: 168.3, WallThickness: 7.11, PipeDensity: 5_000_000),
        };
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 20]);

        var result = OptimizationLoop.Run(file, new MockStressSolver());

        Assert.False(result.Passed);
        Assert.Equal(OptimizationLoop.MaxIterations, result.Iterations);
        Assert.Contains(result.Notes, n => n.Contains("left as a reported failure", StringComparison.Ordinal));
    }

    [Fact]
    public void RunWithOneIntermediateNode_EndsUpSupportedAndPassing()
    {
        // Anchors far enough apart that the run needs exactly one intermediate support — whether
        // it's SupportPlacer's initial pass or the loop's "add support" adjustment that uses the
        // node at 15 is an implementation detail; what matters is the system ends up supported
        // and passing, not reporting an unresolved failure with an available node going unused.
        var maxSpan = Schedule40MaxSpan();
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            NeutralFileFixtureBuilder.Schedule40Run(10, 15, maxSpan * 0.6),
            NeutralFileFixtureBuilder.Schedule40Run(15, 20, maxSpan * 0.6),
        };
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 20]);

        var result = OptimizationLoop.Run(file, new MockStressSolver());

        Assert.True(result.Passed);
        Assert.Contains(file.Restraints, r => r.Node == 15);
    }
}
