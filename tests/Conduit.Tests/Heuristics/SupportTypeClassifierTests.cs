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

        Assert.Equal(SupportType.Guide, SupportTypeClassifier.Classify(context, MaxSpan).Type);
    }

    [Fact]
    public void NearRunEndpoint_IsClassifiedAsAnchor()
    {
        var nearEndpoint = MaxSpan * SupportTypeClassifier.NozzleProximityFraction * 0.5;
        var context = new SupportCandidateContext(IsVerticalSegment: false, DistanceToNearestRunEndpoint: nearEndpoint);

        Assert.Equal(SupportType.Anchor, SupportTypeClassifier.Classify(context, MaxSpan).Type);
    }

    [Fact]
    public void FarFromEndpointAndNotVertical_IsClassifiedAsRest()
    {
        var context = new SupportCandidateContext(IsVerticalSegment: false, DistanceToNearestRunEndpoint: MaxSpan);

        Assert.Equal(SupportType.Rest, SupportTypeClassifier.Classify(context, MaxSpan).Type);
    }

    [Fact]
    public void VerticalTakesPriorityOverNearEndpoint()
    {
        var context = new SupportCandidateContext(IsVerticalSegment: true, DistanceToNearestRunEndpoint: 0);

        Assert.Equal(SupportType.Guide, SupportTypeClassifier.Classify(context, MaxSpan).Type);
    }

    [Fact]
    public void Classify_ReturnsANonEmptyReasonForEveryBranch()
    {
        var vertical = new SupportCandidateContext(IsVerticalSegment: true, DistanceToNearestRunEndpoint: 50);
        var nearEndpoint = new SupportCandidateContext(IsVerticalSegment: false, DistanceToNearestRunEndpoint: MaxSpan * SupportTypeClassifier.NozzleProximityFraction * 0.5);
        var farFromEndpoint = new SupportCandidateContext(IsVerticalSegment: false, DistanceToNearestRunEndpoint: MaxSpan);

        Assert.False(string.IsNullOrWhiteSpace(SupportTypeClassifier.Classify(vertical, MaxSpan).Reason));
        Assert.False(string.IsNullOrWhiteSpace(SupportTypeClassifier.Classify(nearEndpoint, MaxSpan).Reason));
        Assert.False(string.IsNullOrWhiteSpace(SupportTypeClassifier.Classify(farFromEndpoint, MaxSpan).Reason));
    }
}
