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
/// <item><b>Discontinuity clearance (flat 250 mm, per direct instruction 2026-09-03).</b> A support
/// is never placed directly on, or within <see cref="DiscontinuityClearanceMillimetres"/> of, a
/// bend corner (<c>#$ ELEMENTS</c>' own bend pointer), a tee/intersection node (the real
/// <c>#$ SIF&amp;TEES</c> pointer, <see cref="Element.IntersectionPointer"/> — not node degree, per
/// direct instruction 2026-09-01: a real user-supplied sample showed 3 of 4 actual intersections
/// have no branch geometry at all, which a degree-based guess would have missed; node degree is
/// still used, separately, by <see cref="SplitIntoRuns"/> to recognize a genuine topological branch
/// run), a node touching a weighted rigid element (<see cref="Element.RigidPointer"/>, both its
/// <c>FromNode</c> and <c>ToNode</c> — after a real report of a support landing at the *starting*
/// node of a flange, which a `ToNode`-only check would have missed), or a reducer
/// (<see cref="Element.ReducerPointer"/>). This replaced an earlier bend/tee-only, OD-dependent
/// clearance (<c>ElementSplitter.ComputeMinimumChunkLengthNearBendMillimetres</c>) with one flat
/// distance covering every discontinuity type uniformly, per direct instruction: "keep a 250 mm
/// margin on each side of a support for any discontinuities in the piping, such as tees, rigid
/// elements, bends, reducers, and anything else I have not thought of." When an overflow is
/// detected at an excluded node, the placer backs off to the nearest eligible node already passed
/// since the last reset, if any; if none exists in the zone (e.g. a single overlong element ending
/// right at a bend, with no interior node at all), it splits the offending element itself (see the
/// "splits during the initial pass" item below) rather than leaving it unresolved.</item>
/// <item><b>Guide at every (eligible) rest.</b> Per direct instruction ("I think we can use a
/// guide at every rest, unless it comes very close to a directional change... No need to define
/// this right now"): every plain horizontal rest also gets a co-located guide. Since eligible
/// placement nodes are already guaranteed clear of the bend/tee exclusion zone above, "not very
/// close to a directional change" falls out of that same clearance check for free — no separate
/// threshold was introduced. A vertical segment's own guide (already a guide, not a rest, since a
/// rest can't restrain gravity along its own axis) doesn't get a second, redundant one.</item>
/// <item>Tee/branch <i>span exclusion</i>: a branch arm starting at a tee node (rather than an
/// anchor) is recognized as its own run (<see cref="SplitIntoRuns"/>) with its own independent
/// span accumulators, starting fresh at the tee rather than inheriting whatever the header run had
/// already accumulated by that point — since the branch's own unsupported span genuinely does
/// start there. The header run's own walk is unaffected: it still accumulates straight through the
/// tee (excluded only as a placement location, not as a reset point), matching how a bend is
/// handled. A branch that itself ends at something other than an anchor (e.g. a second tee) isn't
/// recognized yet — narrower than the fully general case, per "one thing at a time."</item>
/// <item><b>Splits during the initial pass, not reactively (per direct instruction, 2026-09-01:
/// "I would not like the placement to be done during a walk. It would be better if the initial
/// pass identified the same placements as we currently have").</b> When an overflow is detected,
/// <see cref="SupportPlacer"/> computes the *ideal* position on the overflowing axis — exactly the
/// max allowable span past the last reset — and only backs off to the last eligible node already
/// passed on that axis when that node's own achieved span is already within
/// <see cref="SpanReuseToleranceMillimetres"/> of the ideal; otherwise (including when nothing
/// eligible exists in the zone at all — the current node is itself excluded, and nothing eligible
/// came before it since the last reset) it splits the offending element itself
/// (<see cref="ElementSplitter"/>, same two-tier chunking and restraint-pointer-preservation math
/// <see cref="Optimization.OptimizationLoop"/>'s reactive fallback already used), placing a support
/// at each new interior node inline. Per direct instruction (2026-09-04): "the program prefers
/// existing element breaks. I would prefer that it set itself regardless of what already exists...
/// We can set a 100 mm tolerance for the splitting" — this replaced an earlier version that always
/// preferred any existing eligible node, however far short of the ideal position, and only split as
/// a last resort. <see cref="Optimization.OptimizationLoop"/>'s own reactive `Adjust`/`TrySplit`
/// path stays in place as a safety net for whatever this pass still misses, but should trigger
/// rarely to never for cases this pass's own model already covers.</item>
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

    /// <summary>
    /// A support must sit at least this far from any real piping discontinuity — a bend, tee,
    /// weighted rigid element (e.g. a flange), or reducer — per direct instruction (2026-09-03):
    /// "keep a 250 mm margin on each side of a support for any discontinuities in the piping, such
    /// as tees, rigid elements, bends, reducers, and anything else I have not thought of." A flat
    /// value, not the OD-dependent tangent-length-plus-buffer <see cref="ElementSplitter"/> uses
    /// for its own, different concern (a physically viable minimum chunk size when splitting near
    /// a bend) — those two clearances answer different questions and were kept independent rather
    /// than merged.
    /// </summary>
    public const double DiscontinuityClearanceMillimetres = 250.0;

    /// <summary>
    /// How close an existing eligible node's own achieved span must already be to the ideal,
    /// full-max-allowable-span position before it's reused as-is, rather than splitting the
    /// overflowing element to land a new node exactly at that ideal position — per direct
    /// instruction (2026-09-04): "It also seems like the program prefers existing element breaks.
    /// I would prefer that it set itself regardless of what already exists... We can set a 100 mm
    /// tolerance for the splitting." Below this tolerance, reusing the existing node wastes a
    /// negligible fraction of the allowable span (not worth a needless extra node); above it, the
    /// existing node is treated as "too far short" and a fresh split is preferred instead. The
    /// existing bend-radius/OD-based minimum chunk size near a bend
    /// (<see cref="ElementSplitter.ComputeMinimumChunkLengthNearBendMillimetres"/>, called from
    /// <see cref="ElementSplitter.Split"/> itself) still applies on top of this — confirmed
    /// unaffected by this constant, per the same instruction ("the minimum bend lengths must also
    /// apply"): it answers a different question (how short a chunk `ElementSplitter` is willing to
    /// create), not whether to split in the first place.
    /// </summary>
    public const double SpanReuseToleranceMillimetres = 100.0;

    public static List<PlacedSupport> PlaceSupports(NeutralFile file)
    {
        var fixedNodes = GetFixedNodes(file);
        var alreadySupported = GetSupportedNodes(file);
        var placed = new List<PlacedSupport>();
        var nozzleNodePositions = GetNozzleNodePositions(file);
        var nodeDegree = ComputeNodeDegree(file.Elements);

        foreach (var run in SplitIntoRuns(file.Elements, fixedNodes, nodeDegree))
        {
            PlaceSupportsForRun(file, run, alreadySupported, nozzleNodePositions, placed);
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

    /// <summary>
    /// Splits the element chain into contiguous runs between two anchor nodes — plus, per direct
    /// instruction (2026-09-01), a branch arm that starts at a tee/branch node (node degree > 2)
    /// rather than an anchor is now also recognized as its own run, so it gets its own independent
    /// span accumulator instead of being silently dropped. Only the <i>starting</i> condition is
    /// relaxed this way — the reset trigger below still fires on an anchor `ToNode` only, so a run
    /// that simply passes <i>through</i> a tee (the header continuing past its own branch point)
    /// still accumulates uninterrupted across it, exactly as before: a tee alone doesn't relieve
    /// gravity sag any more than a bend does, so it must not act as a false reset point for the
    /// run it's actually part of. A branch that itself ends at something other than an anchor (a
    /// second tee, say) is still not recognized — deliberately narrower than the general case, per
    /// "one thing at a time," matching the one concrete scenario this fixes.
    /// </summary>
    private static List<List<Element>> SplitIntoRuns(IReadOnlyList<Element> elements, HashSet<int> fixedNodes, Dictionary<int, int> nodeDegree)
    {
        var runs = new List<List<Element>>();
        var current = new List<Element>();

        bool CanStartARun(int node) => fixedNodes.Contains(node) || nodeDegree.GetValueOrDefault(node) > 2;

        foreach (var element in elements)
        {
            current.Add(element);
            if (fixedNodes.Contains(element.ToNode))
            {
                if (CanStartARun(current[0].FromNode))
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
        bool IsTee,
        bool IsRigidWithWeight,
        bool IsReducer)
    {
        /// <summary>Any real piping discontinuity a support must keep <see cref="DiscontinuityClearanceMillimetres"/> clear of.</summary>
        public bool IsDiscontinuity => IsBend || IsTee || IsRigidWithWeight || IsReducer;
    }

    private static void PlaceSupportsForRun(
        NeutralFile file,
        List<Element> run,
        HashSet<int> alreadySupported,
        List<(double X, double Y, double Z)> nozzleNodePositions,
        List<PlacedSupport> placed)
    {
        var izup = file.Control.Izup;
        var toMillimetres = file.Units.LengthToMillimetres;
        var runStartNode = run[0].FromNode;
        var runEndNode = run[^1].ToNode;
        var runLength = run.Sum(e => e.Length) * toMillimetres;
        var positions = nozzleNodePositions.Count > 0 ? file.ComputeNodePositions() : null;

        var nodes = BuildRunNodes(file, run, izup, toMillimetres);
        var exclusionZones = nodes.Where(n => n.IsDiscontinuity).Select(n => n.AlongPath).ToList();

        bool IsEligible(RunNode n) =>
            n.Node != runStartNode && n.Node != runEndNode
            && !n.IsDiscontinuity
            && !alreadySupported.Contains(n.Node)
            && exclusionZones.All(z => Math.Abs(n.AlongPath - z) >= DiscontinuityClearanceMillimetres);

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

            // Per direct instruction (2026-09-04): "the program prefers existing element breaks.
            // I would prefer that it set itself regardless of what already exists... We can set a
            // 100 mm tolerance for the splitting." Compute the ideal position on the overflowing
            // axis (exactly the max allowable span past the last reset — always inside the current,
            // overflowing element, since the *previous* node didn't yet exceed the threshold), and
            // only fall back to backing off to the last eligible node passed *on the overflowing
            // axis* when that node's own achieved span already comes within
            // <see cref="SpanReuseToleranceMillimetres"/> of that ideal — otherwise split the
            // overflowing element itself to land a new node there instead of settling for an
            // existing break that wastes a meaningful fraction of the allowable span just because
            // it happens to already exist.
            var axis = overflowAxis.Value;
            var maxSpanHere = SpanLimitCalculator.ComputeMaxSpan(file, node.ElementEndingHere);
            var thresholdHere = axis == PipeAxis.Vertical ? maxSpanHere * VerticalSpanMultiplier : maxSpanHere;
            var baseForAxis = axis switch
            {
                PipeAxis.Vertical => baseVertical,
                PipeAxis.HorizontalA => baseA,
                _ => baseB,
            };

            var previousEligible = axis switch
            {
                PipeAxis.Vertical => previousEligibleVertical,
                PipeAxis.HorizontalA => previousEligibleA,
                _ => previousEligibleB,
            };
            var target = previousEligible ?? (eligibleHere ? node : (RunNode?)null);

            var targetAxisValue = target is { } tgt ? axis switch
            {
                PipeAxis.Vertical => tgt.CumulativeVertical,
                PipeAxis.HorizontalA => tgt.CumulativeA,
                _ => tgt.CumulativeB,
            } : (double?)null;
            var targetWastesBudget = targetAxisValue is not { } achieved
                || thresholdHere - (achieved - baseForAxis) > SpanReuseToleranceMillimetres;

            if (targetWastesBudget)
            {
                var beforeCumulative = axis switch
                {
                    PipeAxis.Vertical => i == 0 ? 0.0 : nodes[i - 1].CumulativeVertical,
                    PipeAxis.HorizontalA => i == 0 ? 0.0 : nodes[i - 1].CumulativeA,
                    _ => i == 0 ? 0.0 : nodes[i - 1].CumulativeB,
                };
                var remainingBudget = thresholdHere - (beforeCumulative - baseForAxis);
                if (remainingBudget > 0 && TrySplitElement(node, axis, thresholdHere, remainingBudget))
                {
                    continue;
                }
            }

            if (target is not { } t)
            {
                // Genuinely stuck: no eligible node anywhere in this zone (the current node is
                // itself excluded — a bend/tee or its clearance — and nothing eligible came before
                // it since the last reset), and splitting wasn't possible either (e.g. the max span
                // rounds down to too small a chunk). Left as an unresolved failure for
                // OptimizationLoop's reactive fallback, same as before.
                continue;
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

    private static List<RunNode> BuildRunNodes(NeutralFile file, List<Element> run, int izup, double toMillimetres)
    {
        // Precomputed up front (not per-element below) because a weighted rigid excludes *both*
        // of its own endpoints, not just the one RunNode is otherwise keyed by (an element's
        // ToNode) — per direct instruction (2026-09-03), after a real report of a support landing
        // at the *starting* node of a flange (that flange element's FromNode, which nothing
        // "ends" at from this run's own walk, so it would otherwise never get flagged).
        var weightedRigidNodes = new HashSet<int>();
        foreach (var element in run)
        {
            if (file.TryGetRigidElement(element) is { Weight: not 0 })
            {
                weightedRigidNodes.Add(element.FromNode);
                weightedRigidNodes.Add(element.ToNode);
            }
        }

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
            // Per direct instruction (2026-09-01), a tee/intersection is determined by the real
            // #$ SIF&TEES pointer, not inferred from node degree — a real user-supplied sample
            // showed 3 of 4 actual intersections have no branch geometry at all (degree 2, a
            // fitting needing SIF treatment without a modeled branch pipe), which node degree
            // alone would have missed entirely.
            var isTee = element.IntersectionPointer != 0;
            // A massless rigid (weight 0 — confirmed to occur in real files, e.g. a zero-length
            // tie) isn't a real equipment-weight discontinuity in the sense meant, so it doesn't
            // exclude a node — only a rigid with real weight does (see weightedRigidNodes above).
            var isRigidWithWeight = weightedRigidNodes.Contains(element.ToNode);
            var isReducer = element.ReducerPointer != 0;
            nodes.Add(new RunNode(element.ToNode, element, axis, cumA, cumB, cumVertical, alongPath, isBend, isTee, isRigidWithWeight, isReducer));
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
