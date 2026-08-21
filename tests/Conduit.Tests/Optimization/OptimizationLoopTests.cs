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
            .Select(i => NeutralFileFixtureBuilder.Schedule40Run(10 + (i * 10), 20 + (i * 10), 50))
            .ToList();
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 190]);

        var result = OptimizationLoop.Run(file, new MockStressSolver());

        Assert.True(result.Passed);
        Assert.Empty(result.FinalStressResult.Findings.Where(f => !f.Passed));
        Assert.NotEmpty(result.InitialPlacements);
    }

    [Fact]
    public void SingleOverlongElement_CannotBeAdjusted_StopsAtIterationCap()
    {
        // One element, far too long for its own max allowable span, directly between two
        // anchors with no intermediate node to add a support at, and no non-anchor support to
        // escalate either — the loop should recognise it can't do anything further and stop at
        // the bounded iteration count rather than loop forever or silently "pass".
        var maxSpan = Schedule40MaxSpan();
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            NeutralFileFixtureBuilder.Schedule40Run(10, 20, maxSpan * 10),
        };
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 20]);

        var result = OptimizationLoop.Run(file, new MockStressSolver());

        Assert.False(result.Passed);
        Assert.Equal(OptimizationLoop.MaxIterations, result.Iterations);
        Assert.Contains(result.FinalStressResult.Findings, f => !f.Passed);
        Assert.Contains(result.Notes, n => n.Contains("no adjustable support to escalate", StringComparison.Ordinal));
    }

    [Fact]
    public void OverlongSegmentPastAnAddedRestSupport_EscalatesThatSupportToSpring()
    {
        // A short first leg (fits under the max span) followed by a long second leg with no
        // further intermediate node — SupportPlacer's initial pass places a rest support at the
        // node between them, but that alone can't satisfy the second leg's span, so the loop
        // should escalate that rest support (not the anchors) to a spring candidate.
        var maxSpan = Schedule40MaxSpan();
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            NeutralFileFixtureBuilder.Schedule40Run(10, 15, maxSpan * 0.5),
            NeutralFileFixtureBuilder.Schedule40Run(15, 20, maxSpan * 10),
        };
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 20]);

        var result = OptimizationLoop.Run(file, new MockStressSolver());

        Assert.False(result.Passed); // the mock only checks span length, which a type change doesn't reduce
        Assert.Equal(OptimizationLoop.MaxIterations, result.Iterations);
        Assert.Contains(result.Notes, n => n.Contains("spring candidate", StringComparison.Ordinal));
        Assert.Contains(file.Restraints, r => r.Node == 15 && r.Dofs[0].Type == RestraintType.Xspr);
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
