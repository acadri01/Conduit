using Conduit.Core.NeutralFiles;

namespace Conduit.Core.Heuristics;

/// <summary>A support location and type proposed by <see cref="SupportPlacer"/>, with the reason it was placed there.</summary>
public sealed record PlacedSupport(int Node, SupportType Type, RestraintType RestraintType, string Reason);

/// <summary>
/// Walks each pipe run between fixed points (anchors, and — when <c>#$ EQUIPMNT</c> is populated
/// — real nozzle/equipment node locations), placing candidate supports at or under the max
/// allowable span, and classifying each with <see cref="SupportTypeClassifier"/>.
///
/// <para><b>The model, per direct instruction (2026-08-27/28):</b></para>
/// <list type="bullet">
/// <item><b>Per-axis accumulation.</b> The two horizontal axes' unsupported distance are tracked
/// <i>separately</i> (see <see cref="PipeAxis"/>), not summed into one combined running total —
/// a jog's cross-leg still "sees" whatever the other leg already accumulated, since a change in
/// horizontal direction alone doesn't relieve gravity sag. A run's own vertical segments get their
/// own accumulator too, checked against <see cref="VerticalSpanMultiplier"/>x the horizontal max
/// span rather than 1x, per direct instruction ("2x the horizontal span requirement" for
/// standalone risers and loop verticals alike).</item>
/// <item><b>Universal reset.</b> Placing (or encountering an existing) support of any kind resets
/// <i>all three</i> accumulators (both horizontal axes and vertical) — a rest resists gravity sag
/// regardless of which direction the pipe runs, so it's a valid reset point for every axis being
/// tracked, not just its own segment's local axis. Extending this same universal-reset treatment
/// to the vertical accumulator too (not just between the two horizontal axes, which is as far as
/// direct instruction went) is this file's own reversible, logged extension — the alternative
/// (guides not resetting horizontal accumulators, or vice versa) seemed like an arbitrary
/// asymmetry with no stated reason to prefer it.</item>
/// <item><b>Bend/tee corner exclusion.</b> A support is never placed directly on a bend corner
/// (<c>#$ ELEMENTS</c>' own bend pointer) or a tee/branch node (any node where a third element —
/// beyond the run's own incoming/outgoing pair — also connects, detected by node degree across
/// the whole file, not just this run), nor within <see cref="ElementSplitter.ComputeMinimumChunkLengthNearBendMillimetres"/>
/// of one. When an overflow is detected at an excluded node, the placer backs off to the nearest
/// eligible node already passed since the last reset, if any; if none exists in the zone (e.g. a
/// single overlong element ending right at a bend, with no interior node at all), no support is
/// placed there — left as an unresolved failure for <see cref="Optimization.OptimizationLoop"/>'s
/// reactive <see cref="ElementSplitter"/> fallback to resolve by introducing a new node, exactly
/// as it already does for the "single overlong element, no existing node" case.</item>
/// <item><b>Guide at every (eligible) rest.</b> Per direct instruction ("I think we can use a
/// guide at every rest, unless it comes very close to a directional change... No need to define
/// this right now"): every plain horizontal rest also gets a co-located guide. Since eligible
/// placement nodes are already guaranteed clear of the bend/tee exclusion zone above, "not very
/// close to a directional change" falls out of that same clearance check for free — no separate
/// threshold was introduced. A vertical segment's own guide (already a guide, not a rest, since a
/// rest can't restrain gravity along its own axis) doesn't get a second, redundant one.</item>
/// <item>Tee/branch <i>span exclusion</i> (the branch arm getting its own, separate span
/// accumulation rather than folding into the header's) is <b>not yet implemented</b> — this round
/// only keeps a tee node itself clear of placements, per "let's take one thing at a time." A run's
/// own topology is still a simple element-order chain (see <see cref="SplitIntoRuns"/>); a branch
/// element diverging from a run is walked as its own separate run wherever it appears in file
/// order, unaffected by (and not affecting) the header run's accumulators.</item>
/// <item><b>Splits during the initial pass, not reactively (per direct instruction, 2026-09-01:
/// "I would not like the placement to be done during a walk. It would be better if the initial
/// pass identified the same placements as we currently have").</b> When an overflow is detected
/// and there's no eligible node anywhere in the zone to back off to (the current node is itself
/// excluded — a bend/tee or its clearance — and nothing eligible came before it since the last
/// reset), <see cref="SupportPlacer"/> now splits the offending element itself
/// (<see cref="ElementSplitter"/>, same two-tier chunking and restraint-pointer-preservation math
/// <see cref="Optimization.OptimizationLoop"/>'s reactive fallback already used), placing a support
/// at each new interior node inline. <see cref="Optimization.OptimizationLoop"/>'s own reactive
/// `Adjust`/`TrySplit` path stays in place as a safety net for whatever this pass still misses, but
/// should trigger rarely to never for cases this pass's own model already covers.</item>
/// <item>A "run" is a contiguous stretch of <see cref="NeutralFile.Elements"/> (in file order)
/// between two nodes that already carry an anchor (<see cref="RestraintType.Anc"/>) restraint.
/// Elements before the first anchor or after the last aren't part of any run and are skipped.</item>
/// </list>
/// </summary>
public static class SupportPlacer
{
    /// <summary>Fraction of an element's length its vertical-axis delta must reach to count as a vertical segment.</summary>
    public const double VerticalDominanceFraction = PipeAxisClassifier.DominanceFraction;

    /// <summary>Multiplier applied to the horizontal max allowable span when checking a vertical run's own accumulated length, per direct instruction.</summary>
    public const double VerticalSpanMultiplier = 2.0;

    public static List<PlacedSupport> PlaceSupports(NeutralFile file)
    {
        var fixedNodes = GetFixedNodes(file);
        var alreadySupported = GetSupportedNodes(file);
        var placed = new List<PlacedSupport>();
        var nozzleNodePositions = GetNozzleNodePositions(file);
        var nodeDegree = ComputeNodeDegree(file.Elements);

        foreach (var run in SplitIntoRuns(file.Elements, fixedNodes))
        {
            PlaceSupportsForRun(file, run, alreadySupported, nozzleNodePositions, nodeDegree, placed);
        }

        return placed;
    }

    private static HashSet<int> GetFixedNodes(NeutralFile file) =>
        file.Restraints
            .SelectMany(r => r.Dofs)
            .Where(d => d.IsUsed && d.Type == RestraintType.Anc)
            .Select(d => d.Node)
            .ToHashSet();

    private static HashSet<int> GetSupportedNodes(NeutralFile file) =>
        file.Restraints
            .SelectMany(r => r.Dofs)
            .Where(d => d.IsUsed)
            .Select(d => d.Node)
            .ToHashSet();

    /// <summary>Positions of nodes carrying a real <c>#$ EQUIPMNT</c> nozzle/load limit, if any are defined.</summary>
    private static List<(double X, double Y, double Z)> GetNozzleNodePositions(NeutralFile file)
    {
        if (file.NozzleLimits.Count == 0)
        {
            return [];
        }
        var positions = file.ComputeNodePositions();
        var nozzleNodes = file.NozzleLimits.Select(n => n.Node).ToHashSet();
        return positions.Where(p => nozzleNodes.Contains(p.Key)).Select(p => p.Value).ToList();
    }

    /// <summary>Every node's connection count across the whole file — an ordinary interior run node has degree 2 (one element in, one out); a tee/branch node has 3+.</summary>
    private static Dictionary<int, int> ComputeNodeDegree(IReadOnlyList<Element> elements)
    {
        var degree = new Dictionary<int, int>();
        void Bump(int node) => degree[node] = degree.GetValueOrDefault(node) + 1;
        foreach (var element in elements)
        {
            Bump(element.FromNode);
            Bump(element.ToNode);
        }
        return degree;
    }

    /// <summary>Splits the element chain into contiguous runs between two anchor nodes.</summary>
    private static List<List<Element>> SplitIntoRuns(IReadOnlyList<Element> elements, HashSet<int> fixedNodes)
    {
        var runs = new List<List<Element>>();
        var current = new List<Element>();

        foreach (var element in elements)
        {
            current.Add(element);
            if (fixedNodes.Contains(element.ToNode))
            {
                if (fixedNodes.Contains(current[0].FromNode))
                {
                    runs.Add(current);
                }
                current = new List<Element>();
            }
        }

        return runs;
    }

    /// <summary>One run node's precomputed geometry/topology, in run order.</summary>
    private readonly record struct RunNode(
        int Node,
        Element ElementEndingHere,
        PipeAxis Axis,
        double CumulativeA,
        double CumulativeB,
        double CumulativeVertical,
        double AlongPath,
        bool IsBend,
        bool IsTee);

    private static void PlaceSupportsForRun(
        NeutralFile file,
        List<Element> run,
        HashSet<int> alreadySupported,
        List<(double X, double Y, double Z)> nozzleNodePositions,
        Dictionary<int, int> nodeDegree,
        List<PlacedSupport> placed)
    {
        var izup = file.Control.Izup;
        var toMillimetres = file.Units.LengthToMillimetres;
        var runStartNode = run[0].FromNode;
        var runEndNode = run[^1].ToNode;
        var runLength = run.Sum(e => e.Length) * toMillimetres;
        var positions = nozzleNodePositions.Count > 0 ? file.ComputeNodePositions() : null;
        var outsideDiameterMillimetres = run[0].OutsideDiameter * toMillimetres;
        var clearance = ElementSplitter.ComputeMinimumChunkLengthNearBendMillimetres(outsideDiameterMillimetres);

        var nodes = BuildRunNodes(run, izup, toMillimetres, nodeDegree);
        var exclusionZones = nodes.Where(n => n.IsBend || n.IsTee).Select(n => n.AlongPath).ToList();

        bool IsEligible(RunNode n) =>
            n.Node != runStartNode && n.Node != runEndNode
            && !n.IsBend && !n.IsTee
            && !alreadySupported.Contains(n.Node)
            && exclusionZones.All(z => Math.Abs(n.AlongPath - z) >= clearance);

        var baseA = 0.0;
        var baseB = 0.0;
        var baseVertical = 0.0;

        // Tracked per axis, not shared — backing off to relieve a vertical overflow must land on
        // a node that's actually on the vertical run itself (an earlier point up the riser), not
        // on an unrelated horizontal node that happens to be the most recently-seen eligible one
        // (which would do nothing to shorten the riser's own unsupported length).
        RunNode? lastEligibleA = null;
        RunNode? lastEligibleB = null;
        RunNode? lastEligibleVertical = null;

        void PlaceAt(RunNode target)
        {
            var isVertical = target.Axis == PipeAxis.Vertical;
            var maxSpan = SpanLimitCalculator.ComputeMaxSpan(file, target.ElementEndingHere);
            var distanceToRunEndpoint = Math.Min(target.AlongPath, runLength - target.AlongPath);
            var distanceToEquipment = DistanceToNearestNozzle(positions, target.Node, nozzleNodePositions, toMillimetres);
            var distanceToNearestEndpoint = Math.Min(distanceToRunEndpoint, distanceToEquipment);

            var context = new SupportCandidateContext(IsVerticalSegment: isVertical, DistanceToNearestRunEndpoint: distanceToNearestEndpoint);
            var classification = SupportTypeClassifier.Classify(context, maxSpan);
            var restraintType = RestraintTypeMapper.Map(classification.Type, izup);

            var axisLabel = target.Axis switch
            {
                PipeAxis.Vertical => "vertical",
                PipeAxis.HorizontalA => "horizontal-A",
                _ => "horizontal-B",
            };
            var span = target.Axis switch
            {
                PipeAxis.Vertical => target.CumulativeVertical - baseVertical,
                PipeAxis.HorizontalA => target.CumulativeA - baseA,
                _ => target.CumulativeB - baseB,
            };
            var threshold = isVertical ? maxSpan * VerticalSpanMultiplier : maxSpan;
            var reason = $"{axisLabel}-axis span {span:F2} mm would exceed the max allowable span of {threshold:F2} mm at node " +
                         $"{target.Node} — {classification.Reason}";
            placed.Add(new PlacedSupport(target.Node, classification.Type, restraintType, reason));

            if (classification.Type == SupportType.Rest)
            {
                placed.Add(new PlacedSupport(target.Node, SupportType.Guide, RestraintType.Gui,
                    $"co-located with the rest support at node {target.Node} — a guide is added at every rest that isn't close to a bend or tee, per direct instruction"));
            }

            alreadySupported.Add(target.Node);
            baseA = target.CumulativeA;
            baseB = target.CumulativeB;
            baseVertical = target.CumulativeVertical;
            lastEligibleA = null;
            lastEligibleB = null;
            lastEligibleVertical = null;
        }

        // The first axis (priority: vertical, A, B) whose accumulated span since the last reset
        // exceeds its threshold at this node, or null if none do.
        PipeAxis? DetectOverflowAxis(RunNode n)
        {
            var maxSpan = SpanLimitCalculator.ComputeMaxSpan(file, n.ElementEndingHere);
            if (maxSpan <= 0)
            {
                return null;
            }
            if (n.CumulativeVertical - baseVertical > maxSpan * VerticalSpanMultiplier)
            {
                return PipeAxis.Vertical;
            }
            if (n.CumulativeA - baseA > maxSpan)
            {
                return PipeAxis.HorizontalA;
            }
            if (n.CumulativeB - baseB > maxSpan)
            {
                return PipeAxis.HorizontalB;
            }
            return null;
        }

        // Splits the element ending at <paramref name="node"/> (the axis <paramref name="axis"/>
        // overflowed) into evenly-spaced chunks, per <see cref="ElementSplitter"/>, placing a
        // support (with a companion guide, matching every other rest) at each new interior node.
        // Returns false (nothing changed) when the max span rounds down to under
        // <see cref="ElementSplitter.ChunkRoundingIncrementMillimetres"/> — a pipe too small for
        // even a 1 m chunk, left for the caller to leave unresolved exactly as before.
        bool TrySplitElement(RunNode node, PipeAxis axis, double thresholdMillimetres, double remainingBudgetMillimetres)
        {
            var element = node.ElementEndingHere;
            var elementLengthMillimetres = element.Length * toMillimetres;
            var elementOutsideDiameterMillimetres = element.OutsideDiameter * toMillimetres;
            var nextNode = file.Elements.SelectMany(e => new[] { e.FromNode, e.ToNode }).DefaultIfEmpty(0).Max() + 10;

            // If this element already carries a restraint (its FromNode or ToNode is a run's own
            // anchor), the split must preserve that pointer on whichever new chunk still ends at
            // that same node — see ElementSplitter.Split's doc comment.
            var restraintPointer = element.AuxiliaryPointers[Element.RestraintPointerIndex];
            var restraintBelongsToFromNode = restraintPointer != 0
                && file.Restraints[restraintPointer - 1].Node == element.FromNode;

            var plan = ElementSplitter.Split(
                element, elementLengthMillimetres, thresholdMillimetres, elementOutsideDiameterMillimetres,
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
                return false;
            }

            file.ReplaceElement(element, plan.Elements);

            var chunkSupportType = axis == PipeAxis.Vertical ? SupportType.Guide : SupportType.Rest;
            var chunkRestraintType = RestraintTypeMapper.Map(chunkSupportType, izup);
            var axisLabel = axis switch
            {
                PipeAxis.Vertical => "vertical",
                PipeAxis.HorizontalA => "horizontal-A",
                _ => "horizontal-B",
            };
            foreach (var interiorNode in plan.NewInteriorNodes)
            {
                placed.Add(new PlacedSupport(interiorNode, chunkSupportType, chunkRestraintType,
                    $"{axisLabel}-axis span would exceed the max allowable span of {thresholdMillimetres:F2} mm with no " +
                    $"existing node close enough — split into {plan.Elements.Count} elements with a {chunkSupportType} " +
                    "support at each new interior node"));
                if (chunkSupportType == SupportType.Rest)
                {
                    placed.Add(new PlacedSupport(interiorNode, SupportType.Guide, RestraintType.Gui,
                        $"co-located with the rest support at node {interiorNode} — a guide is added at every rest that isn't close to a bend or tee, per direct instruction"));
                }
                alreadySupported.Add(interiorNode);
            }

            // Reset every accumulator to reflect the last new interior node as the new baseline —
            // the same "universal reset" every ordinary placement gets. Only the overflowing axis
            // actually changes across this element (each element belongs to exactly one axis), so
            // the other two axes' cumulative values are unchanged by the split either way.
            var lastChunkLengthMillimetres = plan.Elements[^1].Length * toMillimetres;
            baseVertical = axis == PipeAxis.Vertical ? node.CumulativeVertical - lastChunkLengthMillimetres : node.CumulativeVertical;
            baseA = axis == PipeAxis.HorizontalA ? node.CumulativeA - lastChunkLengthMillimetres : node.CumulativeA;
            baseB = axis == PipeAxis.HorizontalB ? node.CumulativeB - lastChunkLengthMillimetres : node.CumulativeB;
            lastEligibleA = null;
            lastEligibleB = null;
            lastEligibleVertical = null;

            return true;
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node.Node != runEndNode && alreadySupported.Contains(node.Node))
            {
                // An existing restraint (from the input file, or one this run already placed)
                // resets accumulation exactly like a new placement would.
                baseA = node.CumulativeA;
                baseB = node.CumulativeB;
                baseVertical = node.CumulativeVertical;
                lastEligibleA = null;
                lastEligibleB = null;
                lastEligibleVertical = null;
                continue;
            }

            // The candidates passed *before* this one — captured ahead of updating them below,
            // since backing off must never land on the very node that's doing the overflowing.
            var previousEligibleA = lastEligibleA;
            var previousEligibleB = lastEligibleB;
            var previousEligibleVertical = lastEligibleVertical;
            var eligibleHere = node.Node != runEndNode && IsEligible(node);

            var overflowAxis = DetectOverflowAxis(node);
            if (overflowAxis is null)
            {
                if (eligibleHere)
                {
                    switch (node.Axis)
                    {
                        case PipeAxis.Vertical: lastEligibleVertical = node; break;
                        case PipeAxis.HorizontalA: lastEligibleA = node; break;
                        default: lastEligibleB = node; break;
                    }
                }
                continue;
            }

            // Prefer backing off to the last eligible node passed *on the overflowing axis*
            // since the last reset — placing there keeps this stretch's span at or under the max
            // allowable, whereas placing at the current (already-overflowing) node would not.
            // Only fall back to the current node when there's no earlier same-axis candidate at
            // all (e.g. this is the first element since the last reset, or the whole zone up to
            // here has been excluded — including a single overlong element with no interior node
            // of its own, like a lone riser).
            var previousEligible = overflowAxis switch
            {
                PipeAxis.Vertical => previousEligibleVertical,
                PipeAxis.HorizontalA => previousEligibleA,
                _ => previousEligibleB,
            };
            var target = previousEligible ?? (eligibleHere ? node : (RunNode?)null);
            if (target is not { } t)
            {
                // Genuinely stuck: no eligible node anywhere in this zone (the current node is
                // itself excluded — a bend/tee or its clearance — and nothing eligible came before
                // it since the last reset). Split the offending element itself rather than leaving
                // it for OptimizationLoop's reactive fallback to discover later.
                var axis = overflowAxis.Value;
                var maxSpanHere = SpanLimitCalculator.ComputeMaxSpan(file, node.ElementEndingHere);
                var thresholdHere = axis == PipeAxis.Vertical ? maxSpanHere * VerticalSpanMultiplier : maxSpanHere;
                var beforeCumulative = axis switch
                {
                    PipeAxis.Vertical => i == 0 ? 0.0 : nodes[i - 1].CumulativeVertical,
                    PipeAxis.HorizontalA => i == 0 ? 0.0 : nodes[i - 1].CumulativeA,
                    _ => i == 0 ? 0.0 : nodes[i - 1].CumulativeB,
                };
                var baseForAxis = axis switch
                {
                    PipeAxis.Vertical => baseVertical,
                    PipeAxis.HorizontalA => baseA,
                    _ => baseB,
                };
                var remainingBudget = thresholdHere - (beforeCumulative - baseForAxis);
                if (remainingBudget > 0)
                {
                    TrySplitElement(node, axis, thresholdHere, remainingBudget);
                }
                continue; // if splitting wasn't possible either, left as an unresolved failure — same as before
            }

            PlaceAt(t);

            // Backing off to an earlier node may not have been enough to clear the overflow (e.g.
            // two bends close together) — re-check the current node against the new baseline.
            if (t.Node != node.Node && eligibleHere && DetectOverflowAxis(node) is not null)
            {
                PlaceAt(node); // still overflowing even from the new baseline — place here too
            }
            else if (t.Node != node.Node && eligibleHere)
            {
                switch (node.Axis)
                {
                    case PipeAxis.Vertical: lastEligibleVertical = node; break;
                    case PipeAxis.HorizontalA: lastEligibleA = node; break;
                    default: lastEligibleB = node; break;
                }
            }
        }
    }

    private static List<RunNode> BuildRunNodes(List<Element> run, int izup, double toMillimetres, Dictionary<int, int> nodeDegree)
    {
        var nodes = new List<RunNode>(run.Count);
        double cumA = 0, cumB = 0, cumVertical = 0, alongPath = 0;

        foreach (var element in run)
        {
            var length = element.Length * toMillimetres;
            var axis = PipeAxisClassifier.Determine(element, izup);
            switch (axis)
            {
                case PipeAxis.Vertical: cumVertical += length; break;
                case PipeAxis.HorizontalA: cumA += length; break;
                default: cumB += length; break;
            }
            alongPath += length;

            var isBend = element.AuxiliaryPointers[0] != 0;
            var isTee = nodeDegree.GetValueOrDefault(element.ToNode) > 2;
            nodes.Add(new RunNode(element.ToNode, element, axis, cumA, cumB, cumVertical, alongPath, isBend, isTee));
        }

        return nodes;
    }

    private static double DistanceToNearestNozzle(
        Dictionary<int, (double X, double Y, double Z)>? positions,
        int node,
        List<(double X, double Y, double Z)> nozzleNodePositions,
        double toMillimetres)
    {
        if (positions is null || nozzleNodePositions.Count == 0 || !positions.TryGetValue(node, out var here))
        {
            return double.PositiveInfinity;
        }
        return nozzleNodePositions.Min(p => Distance(here, p)) * toMillimetres;
    }

    private static double Distance((double X, double Y, double Z) a, (double X, double Y, double Z) b) =>
        Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2) + Math.Pow(b.Z - a.Z, 2));

    /// <summary>Internal (not private) so <see cref="Optimization.OptimizationLoop"/> can reuse the same vertical-segment test when classifying a newly-split element.</summary>
    internal static bool IsVertical(Element element, int izup) => PipeAxisClassifier.Determine(element, izup) == PipeAxis.Vertical;
}
