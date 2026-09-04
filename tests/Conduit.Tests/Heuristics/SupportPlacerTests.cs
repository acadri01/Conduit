using Conduit.Core.Heuristics;
using Conduit.Core.NeutralFiles;
using Conduit.Tests.TestHelpers;
using Xunit;

namespace Conduit.Tests.Heuristics;

public class SupportPlacerTests
{
    /// <summary>
    /// 30x1000 mm (30 m total) keeps both placements comfortably clear of the run's own endpoints
    /// (each lands ~10 m in from an anchor, well outside <see cref="SupportTypeClassifier"/>'s 15%
    /// near-endpoint zone), so this exercises the plain rest+guide case specifically rather than
    /// also tripping the near-equipment anchor heuristic — see
    /// <see cref="CandidateNearRunEndpoint_IsClassifiedAsAnchor"/> for that one.
    /// </summary>
    [Fact]
    public void StraightRun_PlacesRestAndCoLocatedGuideSupports_SpacedUnderMaxSpan()
    {
        var segments = Enumerable.Range(0, 30)
            .Select(i => NeutralFileFixtureBuilder.Schedule40Run(10 + (i * 10), 20 + (i * 10), 1000))
            .ToList();
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 310]);

        var placed = SupportPlacer.PlaceSupports(file);

        Assert.NotEmpty(placed);
        Assert.All(placed, p => Assert.True(p.Type is SupportType.Rest or SupportType.Guide));
        // Per direct instruction, every plain rest also gets a co-located guide — so every node
        // that got a placement at all should have gotten exactly one of each.
        foreach (var group in placed.GroupBy(p => p.Node))
        {
            Assert.Equal([SupportType.Rest, SupportType.Guide], group.Select(p => p.Type).OrderBy(t => t));
        }
    }

    [Fact]
    public void PlacedSupports_KeepEachSegmentUnderItsMaxAllowableSpan()
    {
        var segments = Enumerable.Range(0, 18)
            .Select(i => NeutralFileFixtureBuilder.Schedule40Run(10 + (i * 10), 20 + (i * 10), 1270))
            .ToList();
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 190]);

        // A node can carry more than one PlacedSupport (e.g. a rest and its co-located guide) —
        // group into one multi-DOF restraint per node, same as OptimizationLoop.Run does.
        foreach (var group in SupportPlacer.PlaceSupports(file).GroupBy(p => p.Node))
        {
            var types = group.Select(p => p.RestraintType).Distinct().ToList();
            file.AddRestraint(Restraint.CreateMultiDof(group.Key, types, file.Units.RigidRestraintStiffness));
        }

        var positions = file.ComputeNodePositions();
        var supportedNodes = file.Restraints.Select(r => r.Node).OrderBy(n => n).ToList();

        for (var i = 0; i < supportedNodes.Count - 1; i++)
        {
            var a = positions[supportedNodes[i]];
            var b = positions[supportedNodes[i + 1]];
            var span = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2) + Math.Pow(b.Z - a.Z, 2));

            var elementsBetween = file.Elements.Where(e => e.FromNode >= supportedNodes[i] && e.ToNode <= supportedNodes[i + 1]);
            var maxSpan = elementsBetween.Min(SpanLimitCalculator.ComputeMaxSpan);

            Assert.True(span <= maxSpan + 0.0001, $"Span {supportedNodes[i]}->{supportedNodes[i + 1]} ({span}) exceeded max allowable span ({maxSpan}).");
        }
    }

    /// <summary>
    /// A vertical run's own accumulated length is checked against <see cref="SupportPlacer.VerticalSpanMultiplier"/>x
    /// the horizontal max allowable span, not 1x, per direct instruction ("2x the horizontal span
    /// requirement" for standalone risers). A 25 m riser comfortably exceeds that 2x threshold
    /// (~21.7 m for the fixture's standard 6" Sch 40 pipe) entirely on its own, with short
    /// horizontal legs before/after that stay well under the (1x) horizontal threshold — isolating
    /// the vertical-specific rule from the horizontal one.
    /// </summary>
    [Fact]
    public void RiserThatExceedsTheVerticalSpanThreshold_GetsAGuideSupport()
    {
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>();
        for (var i = 0; i < 4; i++)
        {
            segments.Add(NeutralFileFixtureBuilder.Schedule40Run(10 + (i * 10), 20 + (i * 10), 2000));
        }
        segments.Add(NeutralFileFixtureBuilder.Schedule40Riser(50, 60, 25_000));
        for (var i = 0; i < 4; i++)
        {
            segments.Add(NeutralFileFixtureBuilder.Schedule40Run(60 + (i * 10), 70 + (i * 10), 50));
        }
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 100]);

        var placed = SupportPlacer.PlaceSupports(file);

        Assert.Contains(placed, p => p.Node == 60 && p.Type == SupportType.Guide && p.RestraintType == RestraintType.Gui);
    }

    [Fact]
    public void CandidateNearRunEndpoint_IsClassifiedAsAnchor()
    {
        // A short run where the very first candidate falls inside the near-endpoint zone.
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec> { NeutralFileFixtureBuilder.Schedule40Run(10, 20, 127) };
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 20]);

        // No intermediate node exists here, so instead assert the classifier itself (used by the
        // placer) treats a location this close to a run boundary as an anchor.
        var maxSpan = SpanLimitCalculator.ComputeMaxSpan(file.Elements[0]);
        var context = new SupportCandidateContext(IsVerticalSegment: false, DistanceToNearestRunEndpoint: maxSpan * 0.01);

        Assert.Equal(SupportType.Anchor, SupportTypeClassifier.Classify(context, maxSpan).Type);
    }

    private static NeutralFileFixtureBuilder.PipeSegmentSpec Seg(int from, int to, double dx, double dy, double dz) =>
        new(from, to, dx, dy, dz, OutsideDiameter: 168.3, WallThickness: 7.11, PipeDensity: SpanLimitCalculator.DefaultSteelDensityKgPerM3);

    /// <summary>
    /// Never place a support directly on a bend corner — the bug report that started this whole
    /// redesign. A 24 m leg into a 90° bend, on its own, has no interior node to fall back to —
    /// per direct instruction (2026-09-01), <see cref="SupportPlacer"/>'s own initial pass now
    /// splits this leg itself (<see cref="ElementSplitter"/>) rather than leaving it for
    /// <see cref="Optimization.OptimizationLoop"/>'s reactive fallback to discover after a failed
    /// evaluate — see <see cref="SplitsAnOverlongLegItselfInsteadOfLeavingItForTheReactiveFallback"/>
    /// for that. What this test asserts is simply that nothing ever lands *on* node 20 itself,
    /// regardless of which mechanism resolves the rest of the leg.
    /// </summary>
    [Fact]
    public void NeverPlacesASupportDirectlyOnABendCorner()
    {
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            Seg(10, 20, 24000, 0, 0),
            Seg(20, 30, 0, 0, 2000),
        };
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 30], izup: 0, bendNodes: [20]);

        var placed = SupportPlacer.PlaceSupports(file);

        Assert.DoesNotContain(placed, p => p.Node == 20);
    }

    /// <summary>
    /// Per direct instruction (2026-09-01): "I would not like the placement to be done during a
    /// walk. It would be better if the initial pass identified the same placements as we currently
    /// have." A 24 m leg ending at a bend has no interior node to back off to, so
    /// <see cref="SupportPlacer"/>'s own pass now splits it (same math
    /// <see cref="Optimization.OptimizationLoop"/>'s reactive fallback used to apply one evaluate
    /// cycle later) and places a rest+guide at each new interior node, in this same call —
    /// verified directly here rather than only through <see cref="Optimization.OptimizationLoopTests"/>'s
    /// end-to-end coverage.
    /// </summary>
    [Fact]
    public void SplitsAnOverlongLegItselfInsteadOfLeavingItForTheReactiveFallback()
    {
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            Seg(10, 20, 24000, 0, 0),
            Seg(20, 30, 0, 0, 2000),
        };
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 30], izup: 0, bendNodes: [20]);

        var placed = SupportPlacer.PlaceSupports(file);

        Assert.NotEmpty(placed);
        Assert.All(placed, p => Assert.True(p.Type is SupportType.Rest or SupportType.Guide));
        Assert.DoesNotContain(placed, p => p.Node is 10 or 20 or 30); // never on an anchor or the bend
        Assert.True(file.Elements.Count > 2, "the 24 m leg should have been split into more than one element");
    }

    /// <summary>
    /// A purely-planar ("2D") jog — mirrors <c>fixtures/loop-2d.cii</c>: a long X leg, a small
    /// Z-axis offset, then a long X leg back to the far anchor, all in one horizontal plane (no
    /// vertical element at all). Per direct instruction, the two horizontal axes' span
    /// accumulation is tracked separately with a universal reset at any support — a rest on
    /// either long leg (split by <see cref="SupportPlacer"/>'s own initial pass, since a single
    /// 24 m element has no interior node of its own) resets the *other* horizontal axis too, so
    /// the short 2000 mm Z-axis offset in the middle never needs — and never gets — a support of
    /// its own.
    /// </summary>
    /// <summary>
    /// Per direct instruction (2026-09-01): a branch arm starting at a tee node (rather than an
    /// anchor) should get its own, independent span accumulator — not be silently dropped, and not
    /// inherit whatever the header run had already accumulated by the time it reaches the tee.
    /// Header: anchor 10 -&gt; tee 20 (8 m, comfortably under the ~10.8 m max span alone) -&gt; anchor
    /// 30 (2 m). Branch: tee 20 -&gt; anchor 100 (12 m, over the max span on its own, with no
    /// interior node) — long enough that it needs a support (in fact a split, since it has no
    /// existing intermediate node), which only happens if the branch is recognized as a run and
    /// walked at all.
    /// </summary>
    [Fact]
    public void BranchArmStartingAtATeeNode_GetsItsOwnIndependentSpanAccumulator()
    {
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            Seg(10, 20, 8000, 0, 0),
            Seg(20, 30, 0, 0, 2000),
            Seg(20, 100, 12000, 0, 0), // the branch — diverges from the tee at node 20
        };
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 30, 100], izup: 0);

        var placed = SupportPlacer.PlaceSupports(file);

        // The branch's own 12 m leg needed splitting to get a support at all — proves it was
        // recognized as its own run and walked, not silently dropped.
        Assert.Contains(placed, p => p.Node is > 100 && p.Type == SupportType.Rest);
        // Never on the tee itself, and never on either anchor.
        Assert.DoesNotContain(placed, p => p.Node is 10 or 20 or 30 or 100);
    }

    /// <summary>
    /// Per direct instruction (2026-09-01, after a real user-supplied sample showed node degree
    /// alone misses most real intersections): a tee/intersection is excluded from placement by its
    /// real <c>#$ SIF&amp;TEES</c> pointer (<see cref="Element.IntersectionPointer"/>), not by node
    /// degree or a synthetic <c>bendNodes</c> marker. This test sets that pointer directly (no bend
    /// pointer, no branch geometry — an otherwise perfectly ordinary two-element chain) and confirms
    /// <see cref="SupportPlacer"/> still refuses to place anything there.
    /// </summary>
    [Fact]
    public void NeverPlacesASupportDirectlyOnANodeWithAnIntersectionPointer_EvenWithoutBranchGeometryOrABendMarker()
    {
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            Seg(10, 20, 24000, 0, 0),
            Seg(20, 30, 2000, 0, 0),
        };
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 30], izup: 0);

        var original = file.Elements.Single(e => e.FromNode == 10 && e.ToNode == 20);
        var pointers = original.AuxiliaryPointers.ToArray();
        pointers[10] = 1; // the intersection pointer — arbitrary non-zero value, no #$ SIF&TEES record needed for this test
        var withIntersection = new Element { RealValues = original.RealValues, Name = original.Name, LineNumber = original.LineNumber, AuxiliaryPointers = pointers };
        file.Elements[file.Elements.IndexOf(original)] = withIntersection;

        var placed = SupportPlacer.PlaceSupports(file);

        Assert.DoesNotContain(placed, p => p.Node == 20);
    }

    /// <summary>
    /// Per direct instruction (2026-09-03), after a real run placed a support at the starting node
    /// of a flange: a node with a connecting rigid element that has real weight must never get a
    /// support, on <i>either</i> of the rigid's own endpoints — not just the one
    /// <see cref="SupportPlacer"/>'s own per-element walk happens to key a position by. This test
    /// puts the weighted rigid on the 20-&gt;30 element (so node 20, its FromNode — the "starting
    /// node" scenario from the real report — is excluded even though no element "ends" there with
    /// the rigid pointer itself) and confirms both node 20 and node 30 are refused.
    /// </summary>
    [Fact]
    public void NeverPlacesASupportOnEitherEndOfAConnectingRigidElementWithRealWeight()
    {
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            Seg(10, 20, 24000, 0, 0),
            Seg(20, 30, 2000, 0, 0),
            Seg(30, 40, 24000, 0, 0),
        };
        var built = NeutralFileFixtureBuilder.Build(segments, [10, 40], izup: 0);

        var original = built.Elements.Single(e => e.FromNode == 20 && e.ToNode == 30);
        var pointers = original.AuxiliaryPointers.ToArray();
        pointers[1] = 1; // the rigid pointer — 1-based, into the RigidElements list below
        var withRigid = new Element { RealValues = original.RealValues, Name = original.Name, LineNumber = original.LineNumber, AuxiliaryPointers = pointers };
        built.Elements[built.Elements.IndexOf(original)] = withRigid;

        var file = new NeutralFile
        {
            Blocks = built.Blocks,
            Control = built.Control,
            Elements = built.Elements,
            NodeNames = built.NodeNames,
            Restraints = built.Restraints,
            MaterialIds = built.MaterialIds,
            AllowableStresses = built.AllowableStresses,
            NozzleLimits = built.NozzleLimits,
            RigidElements = [new RigidElement { Weight = 400, Type = 2 }], // a real weight, like the flange in fixtures/real-samples/44002.cii
            Units = built.Units,
        };

        var placed = SupportPlacer.PlaceSupports(file);

        Assert.DoesNotContain(placed, p => p.Node is 20 or 30);
    }

    [Fact]
    public void PlanarJog_GetsNoSupportsInsideTheJogItself()
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

        var placed = SupportPlacer.PlaceSupports(file);

        Assert.DoesNotContain(placed, p => p.Node is 20 or 30 or 40 or 50);
    }
}
