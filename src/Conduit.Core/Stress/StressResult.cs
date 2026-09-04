using Conduit.Core.Heuristics;

namespace Conduit.Core.Stress;

/// <summary>
/// One span/utilisation finding from an <see cref="IStressSolver"/> check between two supported
/// nodes. This is v1's simplified stand-in for a real per-load-case Code Compliance Report row
/// (Code Stress / Allowable Stress / Ratio % per element) — see <c>IStressSolver</c> for why.
/// </summary>
/// <param name="FromNode">The node bounding the start of the checked span.</param>
/// <param name="ToNode">The node bounding the end of the checked span.</param>
/// <param name="Axis">Which of the model's per-axis accumulators (see <see cref="SupportPlacer"/>) this finding is for.</param>
/// <param name="ActualSpan">The actual unsupported length between the two nodes on <paramref name="Axis"/>, in millimetres.</param>
/// <param name="AllowableSpan">The max allowable span for <paramref name="Axis"/> (already includes the 2x vertical multiplier where applicable), in millimetres.</param>
/// <param name="Message">A human-readable explanation, surfaced in the CLI summary.</param>
public sealed record StressFinding(int FromNode, int ToNode, PipeAxis Axis, double ActualSpan, double AllowableSpan, string Message)
{
    /// <summary>Utilisation ratio — actual span over allowable span. Above 1.0 means the span check failed.</summary>
    public double Ratio => AllowableSpan > 0 ? ActualSpan / AllowableSpan : double.PositiveInfinity;

    public bool Passed => Ratio <= 1.0;
}

/// <summary>The outcome of an <see cref="IStressSolver"/> check over a whole neutral file.</summary>
public sealed record StressResult(bool Passed, IReadOnlyList<StressFinding> Findings)
{
    public static StressResult Pass() => new(true, Array.Empty<StressFinding>());

    public static StressResult FromFindings(IReadOnlyList<StressFinding> findings) =>
        new(findings.All(f => f.Passed), findings);
}
