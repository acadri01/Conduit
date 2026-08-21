using Conduit.Core.Heuristics;
using Conduit.Core.NeutralFiles;

namespace Conduit.Core.Stress;

/// <summary>
/// v1's only functional <see cref="IStressSolver"/>: walks the element chain, breaking it into
/// segments at every currently-restrained node (any used restraint DOF, not just anchors — a
/// deliberate simplification; a real check would care about restraint direction/axis, not just
/// presence), and flags any segment whose actual length exceeds <see cref="SpanLimitCalculator"/>'s
/// max allowable span for the tightest element in that segment.
///
/// This is a deterministic span/utilisation proxy, not a code-compliance stress check — see
/// SPEC.md's "Real load cases vs. v1's simplification" for what a real check would need
/// (load cases, stress types, combination methods) instead of this single pass/fail.
/// </summary>
public sealed class MockStressSolver : IStressSolver
{
    public StressResult Evaluate(NeutralFile file)
    {
        var supportedNodes = file.Restraints
            .SelectMany(r => r.Dofs)
            .Where(d => d.IsUsed)
            .Select(d => d.Node)
            .ToHashSet();

        var findings = new List<StressFinding>();
        var segment = new List<Element>();

        void FlushSegment()
        {
            if (segment.Count == 0)
            {
                return;
            }

            var actualSpan = segment.Sum(e => e.Length);
            var allowableSpan = segment.Min(SpanLimitCalculator.ComputeMaxSpan);
            var fromNode = segment[0].FromNode;
            var toNode = segment[^1].ToNode;

            var passed = allowableSpan <= 0 || actualSpan <= allowableSpan;
            var message = passed
                ? $"Span {fromNode}->{toNode} ({actualSpan:F2}) is within the allowable span ({allowableSpan:F2})."
                : $"Span {fromNode}->{toNode} ({actualSpan:F2}) exceeds the allowable span ({allowableSpan:F2}).";

            findings.Add(new StressFinding(fromNode, toNode, actualSpan, allowableSpan, message));
            segment.Clear();
        }

        foreach (var element in file.Elements)
        {
            segment.Add(element);
            if (supportedNodes.Contains(element.ToNode))
            {
                FlushSegment();
            }
        }
        FlushSegment(); // trailing, unsupported overhang past the last support, if any

        return StressResult.FromFindings(findings);
    }
}
