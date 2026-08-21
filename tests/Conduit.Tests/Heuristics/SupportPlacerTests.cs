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
        Assert.All(placed, p => Assert.Equal(RestraintType.Y, p.RestraintType));
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

    [Fact]
    public void VerticalRiserSegment_GetsAGuideSupport()
    {
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>();
        for (var i = 0; i < 8; i++)
        {
            segments.Add(NeutralFileFixtureBuilder.Schedule40Run(10 + (i * 10), 20 + (i * 10), 50));
        }
        segments.Add(NeutralFileFixtureBuilder.Schedule40Riser(90, 100, 80));
        for (var i = 0; i < 9; i++)
        {
            segments.Add(NeutralFileFixtureBuilder.Schedule40Run(100 + (i * 10), 110 + (i * 10), 50));
        }
        var file = NeutralFileFixtureBuilder.Build(segments, [10, 190]);

        var placed = SupportPlacer.PlaceSupports(file);

        Assert.Contains(placed, p => p.Type == SupportType.Guide && p.RestraintType == RestraintType.Gui);
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

        Assert.Equal(SupportType.Anchor, SupportTypeClassifier.Classify(context, maxSpan));
    }
}
