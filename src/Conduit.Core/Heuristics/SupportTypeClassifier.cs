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
/// <see cref="SupportType.SpringCandidate"/> is deliberately not assigned here — it's an
/// escalation the iterate-and-adjust loop applies to an already-placed support when the span
/// heuristic alone can't satisfy <c>IStressSolver</c> (see <c>OptimizationLoop</c>), not an
/// initial classification. A rule like "flag as spring whenever the span approaches the max
/// allowable span" would fire on almost every rest support by construction, since placement
/// spaces supports at/under that same limit — that would make the rule meaningless, not useful.
/// </summary>
public static class SupportTypeClassifier
{
    /// <summary>Fraction of the max allowable span, from a run endpoint, treated as "near equipment".</summary>
    public const double NozzleProximityFraction = 0.15;

    public static SupportType Classify(SupportCandidateContext context, double maxAllowableSpan)
    {
        if (context.IsVerticalSegment)
        {
            return SupportType.Guide;
        }

        if (maxAllowableSpan > 0 && context.DistanceToNearestRunEndpoint <= maxAllowableSpan * NozzleProximityFraction)
        {
            return SupportType.Anchor;
        }

        return SupportType.Rest;
    }
}
