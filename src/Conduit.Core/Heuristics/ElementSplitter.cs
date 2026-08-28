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

    /// <summary>
    /// "Long radius" (per ASME B16.9 — the default CAESAR II bend-radius preset the user asked
    /// for) is 1.5x the pipe's diameter. Approximated here from the element's actual outside
    /// diameter rather than a true nominal-pipe-size lookup (Conduit has no NPS table), which is
    /// off by only a percent or two for standard schedules — see QUESTIONS.md if that approximation
    /// ever needs tightening.
    /// </summary>
    public const double LongRadiusToOutsideDiameterFactor = 1.5;

    /// <summary>
    /// Minimum clearance (mm) required between a bend's weld/tangent point and any restraint
    /// placed near it, to leave room for a pipe shoe, per direct instruction.
    /// </summary>
    public const double ShoeClearanceBufferMillimetres = 500.0;

    /// <summary>
    /// The straight length (mm) a 90° bend of the given outside diameter (mm) needs beyond its
    /// own corner node before another feature (like a split-off restraint) can go there: the
    /// bend's own tangent length (radius x tan(45°) = radius, for a 90° bend at
    /// <see cref="LongRadiusToOutsideDiameterFactor"/>'s default radius) plus the shoe clearance
    /// buffer above. Every bend Conduit places or preserves is currently a 90° corner turn (see
    /// <c>NeutralFileFixtureBuilder.BuildBendLines</c>), so this doesn't (yet) need a general bend
    /// angle.
    /// </summary>
    public static double ComputeMinimumChunkLengthNearBendMillimetres(double outsideDiameterMillimetres)
    {
        var tangentLength = LongRadiusToOutsideDiameterFactor * outsideDiameterMillimetres; // radius * tan(45°) = radius
        return tangentLength + ShoeClearanceBufferMillimetres;
    }

    /// <summary>The elements <paramref name="element"/> was split into, and the new interior node at each internal boundary (in order, one fewer than <see cref="Elements"/>' count).</summary>
    public readonly record struct SplitPlan(List<Element> Elements, List<int> NewInteriorNodes);

    /// <summary>
    /// Splits <paramref name="element"/> — whose length is <paramref name="elementLengthMillimetres"/>
    /// mm — into chunks no longer than <paramref name="maxAllowableSpanMillimetres"/> mm, rounded
    /// down to <see cref="ChunkRoundingIncrementMillimetres"/>. Returns a single-element,
    /// no-new-nodes plan (a no-op) when the element already fits, or the max span rounds down to
    /// zero (a pipe too small for even a 1 m chunk — left for the caller to report as a failure,
    /// not divided into a meaningless number of chunks).
    ///
    /// <para>When <paramref name="element"/>'s own <c>ToNode</c> is a bend corner
    /// (<c>AuxiliaryPointers[0] != 0</c>), the final chunk (the one that still ends there) is
    /// never left shorter than <see cref="ComputeMinimumChunkLengthNearBendMillimetres"/> — a
    /// too-short remainder is merged into the previous chunk instead, per direct instruction ("an
    /// element break should never cause an element with a bend to be shorter than this
    /// [minimum] length"). <b>Known gap</b>: this only covers a bend at the element's own
    /// <c>ToNode</c> — a bend at its <c>FromNode</c> (the *preceding* element's own corner) isn't
    /// visible from a single element and isn't handled yet; see QUESTIONS.md.</para>
    ///
    /// <para>When <paramref name="element"/> already carries a restraint pointer
    /// (<c>AuxiliaryPointers[3] != 0</c> — e.g. it's the element ending at a run's anchor, or
    /// starting at one, per <see cref="NeutralFiles.NeutralFile.AddRestraint"/>'s doc comment),
    /// that pointer is preserved on whichever chunk still touches the node it actually belongs
    /// to — the first chunk if it's a <c>FromNode</c>-side restraint, the last if it's a
    /// <c>ToNode</c>-side one (<paramref name="restraintBelongsToFromNode"/> tells the split which)
    /// — and cleared on every other chunk. Naively copying it to every chunk (as bend's pointer
    /// briefly was) would leave several elements claiming the same restraint, and a later restraint
    /// added at one of the new interior nodes would silently overwrite it on whichever chunk
    /// happened to end up owning that node — losing the original restraint's association entirely.
    /// </para>
    /// </summary>
    /// <param name="firstChunkBudgetMillimetres">
    /// When this element doesn't start at a true reset point — some of <paramref name="maxAllowableSpanMillimetres"/>'s
    /// budget was already spent by earlier elements since the last actual support (see
    /// <see cref="Optimization.OptimizationLoop"/>'s <c>TrySplitAtFirstOverflow</c>) — the first
    /// chunk is capped at whatever budget remains (rounded down, same as every other chunk),
    /// rather than the element's full max span; every chunk *after* that first one still uses the
    /// full span, since the new support at the end of the first chunk resets the budget. Null (the
    /// default) means this element starts fresh, with the full span available throughout — the
    /// original, single-tier behavior.
    /// </param>
    public static SplitPlan Split(
        Element element,
        double elementLengthMillimetres,
        double maxAllowableSpanMillimetres,
        double outsideDiameterMillimetres,
        Func<int> allocateNode,
        bool restraintBelongsToFromNode = false,
        double? firstChunkBudgetMillimetres = null)
    {
        var chunkMillimetres = Math.Floor(maxAllowableSpanMillimetres / ChunkRoundingIncrementMillimetres) * ChunkRoundingIncrementMillimetres;
        if (chunkMillimetres <= 0 || elementLengthMillimetres <= chunkMillimetres)
        {
            return new SplitPlan([element], []);
        }

        List<double> chunkLengths;
        if (firstChunkBudgetMillimetres is { } budget)
        {
            var firstChunkMillimetres = Math.Floor(budget / ChunkRoundingIncrementMillimetres) * ChunkRoundingIncrementMillimetres;
            if (firstChunkMillimetres <= 0)
            {
                return new SplitPlan([element], []); // no room for even a first chunk within the remaining budget
            }

            var restLengthMillimetres = elementLengthMillimetres - firstChunkMillimetres;
            chunkLengths = [firstChunkMillimetres];
            if (restLengthMillimetres > 0)
            {
                var restFullChunkCount = (int)Math.Floor(restLengthMillimetres / chunkMillimetres);
                var restRemainderMillimetres = restLengthMillimetres - (restFullChunkCount * chunkMillimetres);
                chunkLengths.AddRange(Enumerable.Repeat(chunkMillimetres, restFullChunkCount));
                if (restRemainderMillimetres > 0)
                {
                    chunkLengths.Add(restRemainderMillimetres);
                }
            }
        }
        else
        {
            var fullChunkCount = (int)Math.Floor(elementLengthMillimetres / chunkMillimetres);
            var remainderMillimetres = elementLengthMillimetres - (fullChunkCount * chunkMillimetres);

            chunkLengths = Enumerable.Repeat(chunkMillimetres, fullChunkCount).ToList();
            if (remainderMillimetres > 0)
            {
                chunkLengths.Add(remainderMillimetres);
            }
        }

        if (element.AuxiliaryPointers[0] != 0 && chunkLengths.Count > 1)
        {
            var minimumNearBend = ComputeMinimumChunkLengthNearBendMillimetres(outsideDiameterMillimetres);
            if (chunkLengths[^1] < minimumNearBend)
            {
                chunkLengths[^2] += chunkLengths[^1];
                chunkLengths.RemoveAt(chunkLengths.Count - 1);
            }
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

            // The restraint pointer (index 3) belongs to one specific node of the original
            // element — the first chunk if that's its FromNode, the last if its ToNode — never
            // both, and never an interior chunk that touches neither original endpoint.
            var isFirst = i == 0;
            var keepsRestraint = restraintBelongsToFromNode ? isFirst : isLast;
            if (!keepsRestraint)
            {
                pointers[Element.RestraintPointerIndex] = 0;
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
