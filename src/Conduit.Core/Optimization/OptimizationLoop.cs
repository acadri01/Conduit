using Conduit.Core.Heuristics;
using Conduit.Core.NeutralFiles;
using Conduit.Core.Stress;

namespace Conduit.Core.Optimization;

/// <summary>
/// Runs the initial support placement, then iterates against an <see cref="IStressSolver"/>:
/// on a failing span, it tries to add an intermediate rest support if the failing span has an
/// existing node to place one at; failing that, it splits the span into evenly-spaced chunks
/// (<see cref="ElementSplitter"/>) with a new support at each interior node, per direct
/// instruction — Conduit previously reported this case (a single overlong element with no
/// existing node in range) as an unresolvable failure rather than fixing it. No spring logic in
/// the MVP (per direct instruction; not implemented, not stubbed). Bounded by
/// <see cref="MaxIterations"/> so a genuinely irreducible failure (e.g. a pipe too small for even
/// a 1 m chunk) is reported rather than looped on forever.
/// </summary>
public static class OptimizationLoop
{
    public const int MaxIterations = 5;

    public static OptimizationResult Run(NeutralFile file, IStressSolver solver)
    {
        var notes = new List<string>();

        var placements = SupportPlacer.PlaceSupports(file);
        // SupportPlacer can emit more than one PlacedSupport at the same node (e.g. a rest and
        // its co-located guide) — these belong in one #$ RESTRANT record with several DOF slots,
        // not several separate records, matching how real files pack multi-DOF supports (see
        // Restraint.CreateMultiDof's doc comment).
        foreach (var group in placements.GroupBy(p => p.Node))
        {
            var types = group.Select(p => p.RestraintType).Distinct().ToList();
            file.AddRestraint(Restraint.CreateMultiDof(group.Key, types, file.Units.RigidRestraintStiffness));
        }
        notes.Add($"Placed {placements.Count} initial support(s):");
        foreach (var support in placements)
        {
            notes.Add($"node {support.Node} ({support.Type}, {support.RestraintType}): {support.Reason}");
        }

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
            file.AddRestraint(Restraint.CreateSingleDof(node, restraintType, file.Units.RigidRestraintStiffness));
            return $"Span {finding.FromNode}->{finding.ToNode} ({finding.ActualSpan:F2} mm > {finding.AllowableSpan:F2} mm) — " +
                   $"added an intermediate rest support at node {node}.";
        }

        if (segment.Count == 1)
        {
            var splitNote = TrySplit(file, segment[0], finding);
            if (splitNote is not null)
            {
                return splitNote;
            }
        }

        return $"Span {finding.FromNode}->{finding.ToNode} ({finding.ActualSpan:F2} mm > {finding.AllowableSpan:F2} mm) has no room " +
               "for an intermediate support — left as a reported failure.";
    }

    /// <summary>
    /// Splits a single-element span with no existing intermediate node into evenly-spaced chunks,
    /// adding a support at each new interior node. Returns null (falls through to the "no room"
    /// report) when the max allowable span rounds down to under
    /// <see cref="ElementSplitter.ChunkRoundingIncrementMillimetres"/> — a pipe too small for even
    /// a 1 m chunk, which splitting can't meaningfully fix.
    /// </summary>
    private static string? TrySplit(NeutralFile file, Element element, StressFinding finding)
    {
        var toMillimetres = file.Units.LengthToMillimetres;
        var elementLengthMillimetres = element.Length * toMillimetres;
        var maxAllowableSpanMillimetres = SpanLimitCalculator.ComputeMaxSpan(file, element);

        var outsideDiameterMillimetres = element.OutsideDiameter * toMillimetres;
        var nextNode = file.Elements.SelectMany(e => new[] { e.FromNode, e.ToNode }).DefaultIfEmpty(0).Max() + 10;

        // If this element already carries a restraint (its FromNode or ToNode is a run's
        // anchor), the split must preserve that pointer on whichever new chunk still ends at
        // that same node — see ElementSplitter.Split's doc comment.
        var restraintPointer = element.AuxiliaryPointers[Element.RestraintPointerIndex];
        var restraintBelongsToFromNode = restraintPointer != 0
            && file.Restraints[restraintPointer - 1].Node == element.FromNode;

        var plan = ElementSplitter.Split(
            element, elementLengthMillimetres, maxAllowableSpanMillimetres, outsideDiameterMillimetres,
            () =>
            {
                var allocated = nextNode;
                nextNode += 10;
                return allocated;
            },
            restraintBelongsToFromNode);

        if (plan.NewInteriorNodes.Count == 0)
        {
            return null;
        }

        file.ReplaceElement(element, plan.Elements);

        var izup = file.Control.Izup;
        var supportType = SupportPlacer.IsVertical(element, izup) ? SupportType.Guide : SupportType.Rest;
        var restraintType = RestraintTypeMapper.Map(supportType, izup);
        foreach (var interiorNode in plan.NewInteriorNodes)
        {
            file.AddRestraint(Restraint.CreateSingleDof(interiorNode, restraintType, file.Units.RigidRestraintStiffness));
        }

        return $"Span {finding.FromNode}->{finding.ToNode} ({finding.ActualSpan:F2} mm > {finding.AllowableSpan:F2} mm) — " +
               $"no existing node was close enough, so split it into {plan.Elements.Count} elements with a {supportType} " +
               $"support at each new interior node: {string.Join(", ", plan.NewInteriorNodes)}.";
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
