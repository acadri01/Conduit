using Conduit.Core.NeutralFiles;

namespace Conduit.Core.Heuristics;

/// <summary>A support location and type proposed by <see cref="SupportPlacer"/>, with the reason it was placed there.</summary>
public sealed record PlacedSupport(int Node, SupportType Type, RestraintType RestraintType, string Reason);

/// <summary>
/// Walks each pipe run between fixed points (anchors, and — when <c>#$ EQUIPMNT</c> is populated
/// — real nozzle/equipment node locations), placing candidate supports at or under the max
/// allowable span computed for each element, and classifying each with
/// <see cref="SupportTypeClassifier"/>.
///
/// <para><b>Simplifying assumptions (v1):</b></para>
/// <list type="bullet">
/// <item>Supports can only be placed at existing nodes — Conduit never splits an element to
/// introduce a new node mid-span, so the achieved spacing is the largest node-to-node distance
/// at or under the max allowable span, not necessarily the max allowable span itself. This also
/// means a vertical segment only gets classified as a guide when it happens to be the element
/// that triggers a span-driven placement — a short riser fully contained within an otherwise-fine
/// span may not get its own guide in v1. Placing supports mid-element (splitting the element,
/// introducing a new node) is the real fix and is deliberately deferred, not papered over with a
/// heuristic that forces a placement at every vertical segment's start regardless of span — that
/// was tried and doesn't hold up in general (flagged in review).</item>
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
        var nozzleNodePositions = GetNozzleNodePositions(file);

        foreach (var run in SplitIntoRuns(file.Elements, fixedNodes))
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

        var distanceFromRunStart = 0.0;
        var accumulatedSinceLastSupport = 0.0;
        var lastSupportNode = runStartNode;
        var positions = nozzleNodePositions.Count > 0 ? file.ComputeNodePositions() : null;

        foreach (var element in run)
        {
            var isVertical = IsVertical(element, izup);
            var maxSpan = SpanLimitCalculator.ComputeMaxSpan(file, element); // millimetres
            var elementLength = element.Length * toMillimetres;
            var prospective = accumulatedSinceLastSupport + elementLength;

            var reachedEndOfRun = element.ToNode == runEndNode;
            var wouldExceedSpan = maxSpan > 0 && prospective > maxSpan;

            if (wouldExceedSpan && element.FromNode != lastSupportNode && !alreadySupported.Contains(element.FromNode))
            {
                var distanceToEnd = runLength - distanceFromRunStart;
                var distanceToRunEndpoint = Math.Min(distanceFromRunStart, distanceToEnd);
                var distanceToEquipment = DistanceToNearestNozzle(positions, element.FromNode, nozzleNodePositions, toMillimetres);
                var distanceToNearestEndpoint = Math.Min(distanceToRunEndpoint, distanceToEquipment);

                var context = new SupportCandidateContext(
                    IsVerticalSegment: isVertical,
                    DistanceToNearestRunEndpoint: distanceToNearestEndpoint);

                var classification = SupportTypeClassifier.Classify(context, maxSpan);
                var restraintType = RestraintTypeMapper.Map(classification.Type, izup);
                var reason = $"span {prospective:F2} mm would exceed the max allowable span of {maxSpan:F2} mm at node " +
                             $"{element.FromNode} — {classification.Reason}";
                placed.Add(new PlacedSupport(element.FromNode, classification.Type, restraintType, reason));

                alreadySupported.Add(element.FromNode);
                lastSupportNode = element.FromNode;
                accumulatedSinceLastSupport = elementLength;
            }
            else
            {
                accumulatedSinceLastSupport = prospective;
            }

            distanceFromRunStart += elementLength;

            if (reachedEndOfRun)
            {
                break;
            }
        }
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
    internal static bool IsVertical(Element element, int izup)
    {
        if (element.Length <= 0)
        {
            return false;
        }
        var verticalDelta = izup == 0 ? element.DeltaY : element.DeltaZ;
        return Math.Abs(verticalDelta) / element.Length >= VerticalDominanceFraction;
    }
}
