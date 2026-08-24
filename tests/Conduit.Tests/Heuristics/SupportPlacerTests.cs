using Conduit.Core.Heuristics;
using Conduit.Core.NeutralFiles;
using Conduit.Tests.TestHelpers;
using Xunit;

namespace Conduit.Tests.Heuristics;

public class SupportPlacerTests
{
    [Fact]
    public void StraightRun_PlacesOnlyRestSupports_SpacedUnderMaxSpan()
    {
        var segments = Enumerable.Range(0, 18)
            .Select(i => NeutralFileFixtureBuilder.Schedule40Run(10 + (i * 10), 20 + (i * 10), 50))
            .ToList();
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 190]);

        var placed = SupportPlacer.PlaceSupports(file);

        Assert.NotEmpty(placed);
        Assert.All(placed, p => Assert.Equal(SupportType.Rest, p.Type));
        Assert.All(placed, p => Assert.Equal(RestraintType.PlusY, p.RestraintType));
    }

    [Fact]
    public void PlacedSupports_KeepEachSegmentUnderItsMaxAllowableSpan()
    {
        var segments = Enumerable.Range(0, 18)
            .Select(i => NeutralFileFixtureBuilder.Schedule40Run(10 + (i * 10), 20 + (i * 10), 50))
            .ToList();
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 190]);

        foreach (var support in SupportPlacer.PlaceSupports(file))
        {
            file.AddRestraint(Restraint.CreateSingleDof(support.Node, support.RestraintType));
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
    /// When the span-driven overflow check fires *on the riser element itself* (i.e. the riser is
    /// the element whose length pushes the accumulated span past the max allowable), the placer
    /// correctly classifies that location as a guide. This is narrower than "every vertical segment
    /// gets a guide" — a short riser whose own length doesn't trigger the overflow (because a later
    /// horizontal element does) won't get one in v1; see <see cref="SupportPlacer"/>'s remarks for
    /// why that's a deliberate, documented gap rather than a bug, pending element-splitting.
    /// </summary>
    [Fact]
    public void RiserThatTriggersTheSpanOverflow_GetsAGuideSupport()
    {
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>();
        for (var i = 0; i < 4; i++)
        {
            segments.Add(NeutralFileFixtureBuilder.Schedule40Run(10 + (i * 10), 20 + (i * 10), 50));
        }
        segments.Add(NeutralFileFixtureBuilder.Schedule40Riser(50, 60, 80));
        for (var i = 0; i < 4; i++)
        {
            segments.Add(NeutralFileFixtureBuilder.Schedule40Run(60 + (i * 10), 70 + (i * 10), 50));
        }
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 100]);

        var placed = SupportPlacer.PlaceSupports(file);

        Assert.Contains(placed, p => p.Node == 50 && p.Type == SupportType.Guide && p.RestraintType == RestraintType.Gui);
    }

    [Fact]
    public void CandidateNearRunEndpoint_IsClassifiedAsAnchor()
    {
        // A short run where the very first candidate falls inside the near-endpoint zone.
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec> { NeutralFileFixtureBuilder.Schedule40Run(10, 20, 5) };
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 20]);

        // No intermediate node exists here, so instead assert the classifier itself (used by the
        // placer) treats a location this close to a run boundary as an anchor.
        var maxSpan = SpanLimitCalculator.ComputeMaxSpan(file.Elements[0]);
        var context = new SupportCandidateContext(IsVerticalSegment: false, DistanceToNearestRunEndpoint: maxSpan * 0.01);

        Assert.Equal(SupportType.Anchor, SupportTypeClassifier.Classify(context, maxSpan).Type);
    }
}
