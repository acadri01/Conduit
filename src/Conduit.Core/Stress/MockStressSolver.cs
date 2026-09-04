using Conduit.Core.Heuristics;
using Conduit.Core.NeutralFiles;

namespace Conduit.Core.Stress;

/// <summary>
/// v1's only functional <see cref="IStressSolver"/>: walks the element chain, resetting each of
/// the model's two horizontal-axis and one vertical span accumulators at every currently-
/// restrained node (any used restraint DOF, not just anchors — a deliberate simplification; a
/// real check would care about restraint direction/axis, not just presence), and flags any axis
/// whose accumulated span since the last reset exceeds its allowable — the same per-axis model
/// <see cref="SupportPlacer"/> uses to decide where new supports go, so the two agree with each
/// other rather than fighting (see <see cref="SupportPlacer"/>'s class doc comment for the model
/// itself: separate horizontal-axis tracking, universal reset, and the 2x vertical multiplier).
///
/// This is a deterministic span/utilisation proxy, not a code-compliance stress check — see
/// SPEC.md's "Real load cases vs. v1's simplification" for what a real check would need
/// (load cases, stress types, combination methods) instead of this single pass/fail.
/// </summary>
public sealed class MockStressSolver : IStressSolver
{
    public StressResult Evaluate(NeutralFile file)
    {
        var izup = file.Control.Izup;
        var toMillimetres = file.Units.LengthToMillimetres;
        var restrainedNodes = file.Restraints
            .SelectMany(r => r.Dofs)
            .Where(d => d.IsUsed)
            .Select(d => d.Node)
            .ToHashSet();

        var findings = new List<StressFinding>();
        double cumA = 0, cumB = 0, cumVertical = 0;
        double baseA = 0, baseB = 0, baseVertical = 0;
        var resetNode = file.Elements.Count > 0 ? file.Elements[0].FromNode : 0;
        var elementsSinceReset = new List<Element>();

        void CheckAndReset(int atNode)
        {
            if (elementsSinceReset.Count == 0)
            {
                return;
            }

            var tightestSpan = elementsSinceReset.Min(e => SpanLimitCalculator.ComputeMaxSpan(file, e));
            AddFindingIfUsed(findings, resetNode, atNode, PipeAxis.HorizontalA, cumA - baseA, tightestSpan);
            AddFindingIfUsed(findings, resetNode, atNode, PipeAxis.HorizontalB, cumB - baseB, tightestSpan);
            AddFindingIfUsed(findings, resetNode, atNode, PipeAxis.Vertical, cumVertical - baseVertical, tightestSpan * SupportPlacer.VerticalSpanMultiplier);

            baseA = cumA;
            baseB = cumB;
            baseVertical = cumVertical;
            resetNode = atNode;
            elementsSinceReset.Clear();
        }

        foreach (var element in file.Elements)
        {
            var length = element.Length * toMillimetres;
            switch (PipeAxisClassifier.Determine(element, izup))
            {
                case PipeAxis.Vertical: cumVertical += length; break;
                case PipeAxis.HorizontalA: cumA += length; break;
                default: cumB += length; break;
            }
            elementsSinceReset.Add(element);

            if (restrainedNodes.Contains(element.ToNode))
            {
                CheckAndReset(element.ToNode);
            }
        }
        CheckAndReset(file.Elements.Count > 0 ? file.Elements[^1].ToNode : resetNode); // trailing, unsupported overhang past the last support, if any

        return StressResult.FromFindings(findings);
    }

    private static void AddFindingIfUsed(List<StressFinding> findings, int fromNode, int toNode, PipeAxis axis, double actualSpan, double allowableSpan)
    {
        if (actualSpan <= 0)
        {
            return; // this axis wasn't exercised in this stretch at all — nothing to report
        }

        var axisLabel = axis switch
        {
            PipeAxis.Vertical => "vertical",
            PipeAxis.HorizontalA => "horizontal-A",
            _ => "horizontal-B",
        };
        var passed = allowableSpan <= 0 || actualSpan <= allowableSpan;
        var message = passed
            ? $"Span {fromNode}->{toNode} ({axisLabel}-axis, {actualSpan:F2} mm) is within the allowable span ({allowableSpan:F2} mm)."
            : $"Span {fromNode}->{toNode} ({axisLabel}-axis, {actualSpan:F2} mm) exceeds the allowable span ({allowableSpan:F2} mm).";

        findings.Add(new StressFinding(fromNode, toNode, axis, actualSpan, allowableSpan, message));
    }
}
