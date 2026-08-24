using Conduit.Core.Heuristics;
using Conduit.Core.NeutralFiles;
using Conduit.Core.Stress;

namespace Conduit.Core.Optimization;

/// <summary>
/// Runs the initial support placement, then iterates against an <see cref="IStressSolver"/>:
/// on a failing span, it tries to add an intermediate rest support if the failing span has a
/// node to place one at, and otherwise reports the failure — no spring logic in the MVP (per
/// direct instruction; not implemented, not stubbed). Bounded by <see cref="MaxIterations"/> so
/// an irreducible failure (e.g. a single element longer than its own max allowable span) is
/// reported rather than looped on forever.
/// </summary>
public static class OptimizationLoop
{
    public const int MaxIterations = 5;

    public static OptimizationResult Run(NeutralFile file, IStressSolver solver)
    {
        var notes = new List<string>();

        var placements = SupportPlacer.PlaceSupports(file);
        foreach (var support in placements)
        {
            file.AddRestraint(Restraint.CreateSingleDof(support.Node, support.RestraintType));
        }
        notes.Add($"Placed {placements.Count} initial support(s): " +
                  string.Join(", ", placements.Select(p => $"node {p.Node} ({p.Type})")));

        var result = solver.Evaluate(file);
        var iteration = 1;

        while (!result.Passed && iteration < MaxIterations)
        {
            foreach (var finding in result.Findings.Where(f => !f.Passed))
            {
                notes.Add(Adjust(file, finding));
            }

            iteration++;
            result = solver.Evaluate(file);
        }

        if (!result.Passed)
        {
            notes.Add($"Stopped after {iteration} iteration(s) with unresolved failures — see the final stress result.");
        }

        return new OptimizationResult(result.Passed, iteration, result, placements, notes);
    }

    private static string Adjust(NeutralFile file, StressFinding finding)
    {
        var segment = GetSegmentElements(file.Elements, finding.FromNode, finding.ToNode);

        var midpointNode = TryPickMidpointNode(segment);
        if (midpointNode is { } node)
        {
            var restraintType = RestraintTypeMapper.Map(SupportType.Rest, file.Control.Izup);
            file.AddRestraint(Restraint.CreateSingleDof(node, restraintType));
            return $"Span {finding.FromNode}->{finding.ToNode} ({finding.ActualSpan:F2} > {finding.AllowableSpan:F2}) — " +
                   $"added an intermediate rest support at node {node}.";
        }

        return $"Span {finding.FromNode}->{finding.ToNode} ({finding.ActualSpan:F2} > {finding.AllowableSpan:F2}) has no room " +
               "for an intermediate support — left as a reported failure.";
    }

    private static List<Element> GetSegmentElements(IReadOnlyList<Element> elements, int fromNode, int toNode)
    {
        var segment = new List<Element>();
        var capturing = false;
        foreach (var element in elements)
        {
            if (!capturing && element.FromNode == fromNode)
            {
                capturing = true;
            }
            if (capturing)
            {
                segment.Add(element);
            }
            if (capturing && element.ToNode == toNode)
            {
                break;
            }
        }
        return segment;
    }

    /// <summary>Picks the node closest to the segment's midpoint, excluding the segment's own bounding nodes.</summary>
    private static int? TryPickMidpointNode(List<Element> segment)
    {
        if (segment.Count < 2)
        {
            return null; // a single element has no intermediate node to place a support at
        }

        var half = segment.Sum(e => e.Length) / 2.0;
        var cumulative = 0.0;
        int? bestNode = null;
        var bestDiff = double.MaxValue;
        var lastNode = segment[^1].ToNode;

        foreach (var element in segment)
        {
            cumulative += element.Length;
            if (element.ToNode == lastNode)
            {
                continue; // the far bounding node — already supported, not a valid placement
            }
            var diff = Math.Abs(cumulative - half);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestNode = element.ToNode;
            }
        }

        return bestNode;
    }
}
