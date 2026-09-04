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
        // Node 15 (at 0.5x maxSpan) is *not* reused — it's too far short of the ideal, full-span
        // position to be worth it (per direct instruction, 2026-09-04: "the program prefers
        // existing element breaks... set itself regardless of what already exists", with a 100 mm
        // reuse tolerance). The overlong second leg is split at the computed ideal position
        // instead, landing a fresh node (30) there — Y, not PlusY, since a rest is placed with a
        // hold-down bundled in by default (2026-09-03).
        Assert.DoesNotContain(file.Restraints, r => r.Node == 15);
        Assert.Contains(file.Restraints, r => r.Node == 30 && r.Dofs[0].Type == RestraintType.Y);
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
            new(10, 20, DeltaX: 50000, DeltaY: 0, DeltaZ: 0, OutsideDiameter: 168.3, WallThickness: 7.11, PipeDensity: 50_000_000),
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

    private static NeutralFileFixtureBuilder.PipeSegmentSpec Seg(int from, int to, double dx, double dy, double dz) =>
        new(from, to, dx, dy, dz, OutsideDiameter: 168.3, WallThickness: 7.11, PipeDensity: SpanLimitCalculator.DefaultSteelDensityKgPerM3);

    /// <summary>
    /// Mirrors <c>fixtures/fig6-8-example.cii</c> — the user's own axis-aligned geometry for
    /// "Pipe Stress Engineering" Fig. 6.8 (a real worked example, per direct instruction:
    /// "From the first anchor, it rises two meters, then extends 12m in z, then goes 9.2 meters
    /// in x to the final anchor at the tower"), with no supporting arrangement besides the two
    /// anchors going in. Not a check against the book's own support count/spacing (this MVP's
    /// span model is simpler than a real thermal-expansion analysis) — just that the pipeline
    /// runs cleanly end to end on this topology and never places a support on either real bend
    /// (20, the riser's own top; 30, the Z-to-X transition).
    /// </summary>
    [Fact]
    public void Fig68Example_PassesWithoutSupportingEitherBendCorner()
    {
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            Seg(10, 20, 0, 2000, 0),
            Seg(20, 30, 0, 0, 12000),
            Seg(30, 40, 9200, 0, 0),
        };
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 40], izup: 0, bendNodes: [20, 30]);

        var result = OptimizationLoop.Run(file, new MockStressSolver());

        Assert.True(result.Passed);
        Assert.DoesNotContain(file.Restraints, r => r.Node is 20 or 30);
    }

    /// <summary>
    /// Regression test for a real report against <c>fixtures/loop-2d.cii</c> and
    /// <c>fixtures/loop-50m-3d.cii</c>: once each 24 m leg overflows, the split that resolves it —
    /// landing past the jog, with part of its axis budget already spent by the jog's own short
    /// legs — used to chunk the still-overlong leg from its own start rather than from the zone's
    /// true last reset point, producing a new node whose cumulative span (jog + first chunk) still
    /// overflowed and had nowhere left to go, which was visually confirmed in CAESAR II as a
    /// support sitting directly on a bend corner. Originally fixed in
    /// <see cref="OptimizationLoop"/>'s reactive <c>Adjust</c>/<c>TrySplit</c> fallback
    /// (budget-aware <c>TrySplitAtFirstOverflow</c>, bend/tee-aware <c>TryPickMidpointNode</c>);
    /// per direct instruction (2026-09-01), <see cref="SupportPlacer"/>'s own initial pass now
    /// resolves this same scenario directly (see <c>SupportPlacerTests</c>'s
    /// <c>SplitsAnOverlongLegItselfInsteadOfLeavingItForTheReactiveFallback</c>), so this test
    /// mostly exercises <see cref="OptimizationLoop"/>'s end-to-end wiring now rather than its
    /// reactive fallback specifically — kept as full-pipeline coverage for the same real bug, and
    /// as a check that the reactive fallback (still in place as a safety net) hasn't silently
    /// stopped being reachable/correct if this scenario is ever routed through it again.
    /// </summary>
    [Fact]
    public void PlanarJogWithOverlongLegs_NeverRestrainsABendNode()
    {
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            Seg(10, 20, 24000, 0, 0),
            Seg(20, 30, 0, 0, 2000),
            Seg(30, 40, 2000, 0, 0),
            Seg(40, 50, 0, 0, -2000),
            Seg(50, 60, 24000, 0, 0),
        };
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 60], izup: 0, bendNodes: [20, 30, 40, 50]);
        var bendNodes = new HashSet<int> { 20, 30, 40, 50 };

        var result = OptimizationLoop.Run(file, new MockStressSolver());

        Assert.True(result.Passed);
        Assert.DoesNotContain(file.Restraints, r => bendNodes.Contains(r.Node));

        // With splitting folded into SupportPlacer's own initial pass (per direct instruction,
        // 2026-09-01), this should resolve in a single iteration — the initial placement already
        // satisfies MockStressSolver, with no Adjust round needed at all.
        Assert.Equal(1, result.Iterations);

        // The second leg's split shouldn't cluster new supports just past the jog to stay
        // conservative — with the two-tier chunking fix (a short first chunk sized to the
        // already-spent budget, then full-length chunks after, since the new support resets it),
        // every restraint should land exactly on a clean, evenly-spaced 10,000 mm grid in X.
        var positions = file.ComputeNodePositions();
        var restraintXPositions = file.Restraints.Select(r => positions[r.Node].X).OrderBy(x => x).ToList();
        Assert.All(restraintXPositions, x => Assert.Equal(0.0, x % 10000.0, precision: 6));
    }
}
