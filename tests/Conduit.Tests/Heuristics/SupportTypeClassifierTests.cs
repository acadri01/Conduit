using Conduit.Core.Heuristics;
using Xunit;

namespace Conduit.Tests.Heuristics;

public class SupportTypeClassifierTests
{
    private const double MaxSpan = 100.0;

    [Fact]
    public void VerticalSegment_IsClassifiedAsGuide()
    {
        var context = new SupportCandidateContext(IsVerticalSegment: true, DistanceToNearestRunEndpoint: 50);

        Assert.Equal(SupportType.Guide, SupportTypeClassifier.Classify(context, MaxSpan));
    }

    [Fact]
    public void NearRunEndpoint_IsClassifiedAsAnchor()
    {
        var nearEndpoint = MaxSpan * SupportTypeClassifier.NozzleProximityFraction * 0.5;
        var context = new SupportCandidateContext(IsVerticalSegment: false, DistanceToNearestRunEndpoint: nearEndpoint);

        Assert.Equal(SupportType.Anchor, SupportTypeClassifier.Classify(context, MaxSpan));
    }

    [Fact]
    public void FarFromEndpointAndNotVertical_IsClassifiedAsRest()
    {
        var context = new SupportCandidateContext(IsVerticalSegment: false, DistanceToNearestRunEndpoint: MaxSpan);

        Assert.Equal(SupportType.Rest, SupportTypeClassifier.Classify(context, MaxSpan));
    }

    [Fact]
    public void VerticalTakesPriorityOverNearEndpoint()
    {
        var context = new SupportCandidateContext(IsVerticalSegment: true, DistanceToNearestRunEndpoint: 0);

        Assert.Equal(SupportType.Guide, SupportTypeClassifier.Classify(context, MaxSpan));
    }
}
