namespace Conduit.Core.Heuristics;

/// <summary>Context about a candidate support location, gathered by <c>SupportPlacer</c> as it walks a run.</summary>
/// <param name="IsVerticalSegment">Whether the pipe segment at this location runs along the model's vertical axis.</param>
/// <param name="DistanceToNearestRunEndpoint">
/// Distance to the nearer of the run's two bounding fixed points (anchors/equipment) — v1's
/// proxy for "near equipment", since the <c>#$ EQUIPMNT</c> nozzle-check section isn't modeled.
/// </param>
public readonly record struct SupportCandidateContext(
    bool IsVerticalSegment,
    double DistanceToNearestRunEndpoint);

/// <summary>A classification decision paired with the plain-language reason it was made — surfaced in Conduit's output so every placement is explainable, not just stated.</summary>
public readonly record struct SupportClassification(SupportType Type, string Reason);

/// <summary>
/// Classifies a candidate support location as rest/guide/anchor at initial placement time.
///
/// <para><b>Simplifying rules (v1, not a substitute for engineering judgment):</b></para>
/// <list type="bullet">
/// <item>Vertical segments get a <see cref="SupportType.Guide"/> — a rest support can't restrain
/// a vertical run against gravity along its own axis.</item>
/// <item>Locations within <see cref="NozzleProximityFraction"/> of the max allowable span from
/// either end of the run get a <see cref="SupportType.Anchor"/> — a stand-in for "near an
/// equipment nozzle connection", since nozzle/equipment data isn't modeled in v1.</item>
/// <item>Everything else is a plain <see cref="SupportType.Rest"/>.</item>
/// </list>
/// </summary>
public static class SupportTypeClassifier
{
    /// <summary>Fraction of the max allowable span, from a run endpoint, treated as "near equipment".</summary>
    public const double NozzleProximityFraction = 0.15;

    public static SupportClassification Classify(SupportCandidateContext context, double maxAllowableSpan)
    {
        if (context.IsVerticalSegment)
        {
            return new SupportClassification(SupportType.Guide,
                "segment runs along the model's vertical axis — a rest can't restrain a vertical run against gravity along its own axis, so a guide is used instead");
        }

        if (maxAllowableSpan > 0 && context.DistanceToNearestRunEndpoint <= maxAllowableSpan * NozzleProximityFraction)
        {
            return new SupportClassification(SupportType.Anchor,
                $"within {NozzleProximityFraction:P0} of the max allowable span ({context.DistanceToNearestRunEndpoint:F2} mm of {maxAllowableSpan:F2} mm) " +
                "from a run endpoint — treated as near an equipment nozzle connection, so an anchor is used");
        }

        return new SupportClassification(SupportType.Rest, "a plain vertical rest is sufficient — not on a vertical segment and not near a run endpoint/equipment connection");
    }
}
