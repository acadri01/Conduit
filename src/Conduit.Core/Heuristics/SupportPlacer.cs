using Conduit.Core.NeutralFiles;

namespace Conduit.Core.Heuristics;

/// <summary>A support location and type proposed by <see cref="SupportPlacer"/>.</summary>
public sealed record PlacedSupport(int Node, SupportType Type, RestraintType RestraintType);

/// <summary>
/// Walks each pipe run between fixed points (anchors — v1 doesn't model the <c>#$ EQUIPMNT</c>
/// nozzle-check section, so equipment connections aren't a separate signal from anchors here),
/// placing candidate supports at or under the max allowable span computed for each element, and
/// classifying each with <see cref="SupportTypeClassifier"/>.
///
/// <para><b>Simplifying assumptions (v1):</b></para>
/// <list type="bullet">
/// <item>Supports can only be placed at existing nodes — Conduit never splits an element to
/// introduce a new node mid-span, so the achieved spacing is the largest node-to-node distance
/// at or under the max allowable span, not necessarily the max allowable span itself.</item>
/// <item>A "run" is a contiguous stretch of <see cref="NeutralFile.Elements"/> (in file order)
/// between two nodes that already carry an anchor (<see cref="RestraintType.Anc"/>) restraint.
/// Elements before the first anchor or after the last aren't part of any run and are skipped.</item>
/// <item>A segment is "vertical" when its delta along the model's vertical axis (from
/// <c>Control.Izup</c>) is at least <see cref="VerticalDominanceFraction"/> of its length.</item>
/// </list>
/// </summary>
public static class SupportPlacer
{
    /// <summary>Fraction of an element's length its vertical-axis delta must reach to count as a vertical segment.</summary>
    public const double VerticalDominanceFraction = 0.9;

    public static List<PlacedSupport> PlaceSupports(NeutralFile file)
    {
        var fixedNodes = GetFixedNodes(file);
        var alreadySupported = GetSupportedNodes(file);
        var placed = new List<PlacedSupport>();

        foreach (var run in SplitIntoRuns(file.Elements, fixedNodes))
        {
            PlaceSupportsForRun(run, fixedNodes, file.Control.Izup, alreadySupported, placed);
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

    private static void PlaceSupportsForRun(
        List<Element> run,
        HashSet<int> fixedNodes,
        int izup,
        HashSet<int> alreadySupported,
        List<PlacedSupport> placed)
    {
        var runStartNode = run[0].FromNode;
        var runEndNode = run[^1].ToNode;
        var runLength = run.Sum(e => e.Length);

        var distanceFromRunStart = 0.0;
        var accumulatedSinceLastSupport = 0.0;
        var lastSupportNode = runStartNode;

        foreach (var element in run)
        {
            var isVertical = IsVertical(element, izup);

            // A vertical run always needs its own guide at the point it starts, regardless of
            // accumulated span — a riser can need lateral restraint well before it would trip
            // the ordinary gravity-span check, and the span check alone might never trigger
            // exactly at the riser if a later horizontal element is what pushes it over instead.
            if (isVertical && element.FromNode != lastSupportNode && !alreadySupported.Contains(element.FromNode))
            {
                placed.Add(new PlacedSupport(element.FromNode, SupportType.Guide, RestraintType.Gui));
                alreadySupported.Add(element.FromNode);
                lastSupportNode = element.FromNode;
                accumulatedSinceLastSupport = 0;
            }

            var maxSpan = SpanLimitCalculator.ComputeMaxSpan(element);
            var prospective = accumulatedSinceLastSupport + element.Length;

            var reachedEndOfRun = element.ToNode == runEndNode;
            var wouldExceedSpan = maxSpan > 0 && prospective > maxSpan;

            if (wouldExceedSpan && element.FromNode != lastSupportNode && !alreadySupported.Contains(element.FromNode))
            {
                var distanceToEnd = runLength - distanceFromRunStart;
                var distanceToNearestEndpoint = Math.Min(distanceFromRunStart, distanceToEnd);
                var context = new SupportCandidateContext(
                    IsVerticalSegment: isVertical,
                    DistanceToNearestRunEndpoint: distanceToNearestEndpoint);

                var type = SupportTypeClassifier.Classify(context, maxSpan);
                var restraintType = RestraintTypeMapper.Map(type, izup);
                placed.Add(new PlacedSupport(element.FromNode, type, restraintType));

                alreadySupported.Add(element.FromNode);
                lastSupportNode = element.FromNode;
                accumulatedSinceLastSupport = element.Length;
            }
            else
            {
                accumulatedSinceLastSupport = prospective;
            }

            distanceFromRunStart += element.Length;

            if (reachedEndOfRun)
            {
                break;
            }
        }

        _ = fixedNodes; // run boundaries are already fixed by construction; kept for signature clarity
    }

    private static bool IsVertical(Element element, int izup)
    {
        if (element.Length <= 0)
        {
            return false;
        }
        var verticalDelta = izup == 0 ? element.DeltaY : element.DeltaZ;
        return Math.Abs(verticalDelta) / element.Length >= VerticalDominanceFraction;
    }
}
