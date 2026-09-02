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

        var midpointNode = TryPickMidpointNode(file, segment);
        if (midpointNode is { } node)
        {
            AddSupport(file, node, SupportType.Rest, file.Control.Izup);
            return $"Span {finding.FromNode}->{finding.ToNode} ({finding.ActualSpan:F2} mm > {finding.AllowableSpan:F2} mm) — " +
                   $"added an intermediate rest support at node {node}.";
        }

        // No existing node in the whole zone is a safe place for a support (every interior node
        // is a bend/tee, or too close to one) — split whichever element is the *first* (in file
        // order, from the zone's own start) to push finding.Axis's accumulated span past the
        // allowable. Walking in order, not just picking the longest element, matters once a zone
        // spans several elements: an earlier element may already have used up part of the budget
        // (e.g. a short pre-bend remainder plus a short cross-axis jog leg), so the element that
        // actually needs splitting isn't necessarily the longest one, and the split has to respect
        // however much of the allowable span is already spent before it, not the full amount.
        var splitNote = TrySplitAtFirstOverflow(file, segment, finding);
        if (splitNote is not null)
        {
            return splitNote;
        }

        return $"Span {finding.FromNode}->{finding.ToNode} ({finding.ActualSpan:F2} mm > {finding.AllowableSpan:F2} mm) has no room " +
               "for an intermediate support — left as a reported failure.";
    }

    private static string? TrySplitAtFirstOverflow(NeutralFile file, List<Element> segment, StressFinding finding)
    {
        var izup = file.Control.Izup;
        var toMillimetres = file.Units.LengthToMillimetres;
        var cumulativeOnAxis = 0.0;

        foreach (var element in segment)
        {
            var axis = PipeAxisClassifier.Determine(element, izup);
            if (axis != finding.Axis)
            {
                continue; // doesn't contribute to the axis this finding is about
            }

            var before = cumulativeOnAxis;
            cumulativeOnAxis += element.Length * toMillimetres;
            if (cumulativeOnAxis <= finding.AllowableSpan)
            {
                continue; // still within budget after this element — not the offender
            }

            var remainingBudget = finding.AllowableSpan - before;
            if (remainingBudget <= 0)
            {
                continue; // budget was already gone before this element even started
            }

            var splitNote = TrySplit(file, element, finding, remainingBudget);
            if (splitNote is not null)
            {
                return splitNote;
            }
        }

        return null;
    }

    /// <summary>
    /// Adds a restraint for <paramref name="type"/> at <paramref name="node"/>, packing in a
    /// co-located guide too when it's a plain rest — matching <see cref="SupportPlacer"/>'s own
    /// "guide at every eligible rest" rule, so a support added reactively here isn't missing the
    /// guide an equivalent one from the initial pass would have gotten.
    /// </summary>
    private static void AddSupport(NeutralFile file, int node, SupportType type, int izup)
    {
        var types = new List<RestraintType> { RestraintTypeMapper.Map(type, izup) };
        if (type == SupportType.Rest)
        {
            types.Add(RestraintType.Gui);
        }
        file.AddRestraint(Restraint.CreateMultiDof(node, types, file.Units.RigidRestraintStiffness));
    }

    /// <summary>
    /// Splits an overlong element (with no existing intermediate node) into evenly-spaced chunks,
    /// adding a support at each new interior node. Returns null (falls through to the "no room"
    /// report) when the max allowable span rounds down to under
    /// <see cref="ElementSplitter.ChunkRoundingIncrementMillimetres"/> — a pipe too small for even
    /// a 1 m chunk, which splitting can't meaningfully fix.
    /// </summary>
    /// <param name="remainingBudgetMillimetres">
    /// How much of the finding's allowable span is still unspent by the time this element starts
    /// (see <see cref="TrySplitAtFirstOverflow"/>). Used as a cap on the *first* chunk only —
    /// every chunk after that uses the pipe's full max span, since the support at the end of the
    /// first chunk resets the budget. Two-tier rather than uniformly shrinking every chunk, so a
    /// zone that already had some of its budget spent doesn't end up with more new supports than
    /// it actually needs.
    /// </param>
    private static string? TrySplit(NeutralFile file, Element element, StressFinding finding, double remainingBudgetMillimetres)
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
            restraintBelongsToFromNode,
            firstChunkBudgetMillimetres: remainingBudgetMillimetres);

        if (plan.NewInteriorNodes.Count == 0)
        {
            return null;
        }

        file.ReplaceElement(element, plan.Elements);

        var izup = file.Control.Izup;
        var supportType = SupportPlacer.IsVertical(element, izup) ? SupportType.Guide : SupportType.Rest;
        foreach (var interiorNode in plan.NewInteriorNodes)
        {
            AddSupport(file, interiorNode, supportType, izup);
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

    /// <summary>
    /// Picks the node closest to the segment's midpoint, excluding the segment's own bounding
    /// nodes and — per direct instruction ("Any element with a bend pointer shouldn't have a
    /// restraint"), after a real report of exactly this happening — any bend or tee/intersection
    /// node, plus the same bend-clearance buffer <see cref="ElementSplitter"/> and
    /// <see cref="SupportPlacer"/> already use. Tee detection uses the real
    /// <c>#$ SIF&amp;TEES</c> pointer (<see cref="Element.IntersectionPointer"/>), not node degree —
    /// per direct instruction (2026-09-01), matching <see cref="SupportPlacer"/>'s own switch (see
    /// its class doc comment for why node degree alone isn't reliable). This mirrors
    /// <see cref="SupportPlacer"/>'s own exclusion rule so a support added reactively here can't
    /// land somewhere the initial pass would have refused to.
    /// </summary>
    private static int? TryPickMidpointNode(NeutralFile file, List<Element> segment)
    {
        if (segment.Count < 2)
        {
            return null; // a single element has no intermediate node to place a support at
        }

        var toMillimetres = file.Units.LengthToMillimetres;

        var alongPath = 0.0;
        var positions = new List<(int Node, Element Element, double AlongPath)>();
        foreach (var element in segment)
        {
            alongPath += element.Length * toMillimetres;
            positions.Add((element.ToNode, element, alongPath));
        }

        var exclusionZones = positions
            .Where(p => p.Element.AuxiliaryPointers[0] != 0 || p.Element.IntersectionPointer != 0)
            .Select(p => p.AlongPath)
            .ToList();

        bool IsExcluded((int Node, Element Element, double AlongPath) p)
        {
            if (p.Element.AuxiliaryPointers[0] != 0 || p.Element.IntersectionPointer != 0)
            {
                return true;
            }
            var outsideDiameterMillimetres = p.Element.OutsideDiameter * toMillimetres;
            var clearance = ElementSplitter.ComputeMinimumChunkLengthNearBendMillimetres(outsideDiameterMillimetres);
            return exclusionZones.Any(z => Math.Abs(p.AlongPath - z) < clearance);
        }

        var half = alongPath / 2.0;
        var lastNode = segment[^1].ToNode;
        int? bestNode = null;
        var bestDiff = double.MaxValue;

        foreach (var p in positions)
        {
            if (p.Node == lastNode || IsExcluded(p))
            {
                continue; // the far bounding node, or a bend/tee (and its clearance zone) — not a valid placement
            }
            var diff = Math.Abs(p.AlongPath - half);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestNode = p.Node;
            }
        }

        return bestNode;
    }
}
