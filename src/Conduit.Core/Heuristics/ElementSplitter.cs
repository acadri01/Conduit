using Conduit.Core.NeutralFiles;

namespace Conduit.Core.Heuristics;

/// <summary>
/// Splits a single overlong element into evenly-spaced chunks with new interior nodes, so a rest
/// support can be placed even when the run has no existing node to use — per direct instruction:
/// Conduit was previously only able to place a support at an existing node, so a long stretch
/// with none in range (e.g. a single 24 m element) was reported as an unresolvable failure rather
/// than fixed.
/// </summary>
public static class ElementSplitter
{
    /// <summary>
    /// The granularity (mm) the max allowable span is rounded *down* to before chunking a span,
    /// per direct instruction — e.g. a 6446.76 mm max allowable span becomes a 6000 mm chunk
    /// size, so a 25550 mm element becomes four 6000 mm elements plus one 1550 mm remainder
    /// element (four new interior nodes/restraints), not an uneven or over-max split.
    /// </summary>
    public const double ChunkRoundingIncrementMillimetres = 1000.0;

    /// <summary>The elements <paramref name="element"/> was split into, and the new interior node at each internal boundary (in order, one fewer than <see cref="Elements"/>' count).</summary>
    public readonly record struct SplitPlan(List<Element> Elements, List<int> NewInteriorNodes);

    /// <summary>
    /// Splits <paramref name="element"/> — whose length is <paramref name="elementLengthMillimetres"/>
    /// mm — into chunks no longer than <paramref name="maxAllowableSpanMillimetres"/> mm, rounded
    /// down to <see cref="ChunkRoundingIncrementMillimetres"/>. Returns a single-element,
    /// no-new-nodes plan (a no-op) when the element already fits, or the max span rounds down to
    /// zero (a pipe too small for even a 1 m chunk — left for the caller to report as a failure,
    /// not divided into a meaningless number of chunks).
    /// </summary>
    public static SplitPlan Split(
        Element element,
        double elementLengthMillimetres,
        double maxAllowableSpanMillimetres,
        Func<int> allocateNode)
    {
        var chunkMillimetres = Math.Floor(maxAllowableSpanMillimetres / ChunkRoundingIncrementMillimetres) * ChunkRoundingIncrementMillimetres;
        if (chunkMillimetres <= 0 || elementLengthMillimetres <= chunkMillimetres)
        {
            return new SplitPlan([element], []);
        }

        var fullChunkCount = (int)Math.Floor(elementLengthMillimetres / chunkMillimetres);
        var remainderMillimetres = elementLengthMillimetres - (fullChunkCount * chunkMillimetres);

        var chunkLengths = Enumerable.Repeat(chunkMillimetres, fullChunkCount).ToList();
        if (remainderMillimetres > 0)
        {
            chunkLengths.Add(remainderMillimetres);
        }

        var newElements = new List<Element>(chunkLengths.Count);
        var newInteriorNodes = new List<int>(chunkLengths.Count - 1);
        var fromNode = element.FromNode;

        for (var i = 0; i < chunkLengths.Count; i++)
        {
            var isLast = i == chunkLengths.Count - 1;
            var toNode = isLast ? element.ToNode : allocateNode();
            var fraction = chunkLengths[i] / elementLengthMillimetres;

            var real = element.RealValues.ToArray();
            real[0] = fromNode;
            real[1] = toNode;
            real[2] = element.DeltaX * fraction;
            real[3] = element.DeltaY * fraction;
            real[4] = element.DeltaZ * fraction;

            var pointers = element.AuxiliaryPointers.ToArray();
            if (!isLast)
            {
                // The bend pointer (index 0) is tied to the original element's own ToNode — the
                // one true corner — so only the final chunk (the one that still ends there) may
                // keep it; every interior chunk must not claim to be that same bend.
                pointers[0] = 0;
            }

            newElements.Add(new Element { RealValues = real, AuxiliaryPointers = pointers });

            if (!isLast)
            {
                newInteriorNodes.Add(toNode);
            }
            fromNode = toNode;
        }

        return new SplitPlan(newElements, newInteriorNodes);
    }
}
