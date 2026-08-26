namespace Conduit.Core.NeutralFiles;

/// <summary>
/// A parsed CAESAR II neutral file (<c>.cii</c>). <see cref="Blocks"/> is the source of truth for
/// writing — every block is written back verbatim except <c>CONTROL</c> and <c>RESTRANT</c>,
/// which are regenerated from <see cref="Control"/> and <see cref="Restraints"/> so a file
/// round-tripped without any support changes is byte-identical, and one with only new supports
/// preserves everything else CAESAR II needs to re-import it.
/// </summary>
public sealed class NeutralFile
{
    public required List<NeutralFileBlock> Blocks { get; init; }
    public required ControlSection Control { get; init; }
    public required List<Element> Elements { get; init; }
    public required IReadOnlyList<NodeName> NodeNames { get; init; }
    public required List<Restraint> Restraints { get; init; }

    /// <summary>Per-element material ID (1-699), same order/count as <see cref="Elements"/>. From <c>#$ MISCEL_1</c>'s RRMAT array.</summary>
    public required List<int> MaterialIds { get; init; }

    /// <summary>Allowable-stress records from <c>#$ ALLOWBLS</c> — look up an element's via <see cref="TryGetAllowableStress"/>.</summary>
    public required IReadOnlyList<AllowableStress> AllowableStresses { get; init; }

    /// <summary>Nozzle/equipment load limits from <c>#$ EQUIPMNT</c>.</summary>
    public required IReadOnlyList<NozzleLimit> NozzleLimits { get; init; }

    /// <summary>This file's length-unit conversion, from <c>#$ UNITS</c> (defaults to <see cref="UnitsSection.Metric"/>).</summary>
    public required UnitsSection Units { get; init; }

    /// <summary>
    /// Appends a new support and keeps <see cref="Control"/>'s restraint count in sync so the
    /// regenerated <c>#$ CONTROL</c> section matches the regenerated <c>#$ RESTRANT</c> section.
    /// </summary>
    public void AddRestraint(Restraint restraint)
    {
        Restraints.Add(restraint);
        Control.NumRestraints = Restraints.Count;
    }

    /// <summary>
    /// Splices <paramref name="replacements"/> into <see cref="Elements"/> in place of
    /// <paramref name="original"/> — e.g. splitting one overlong element into several shorter
    /// ones with new interior nodes — surgically updating only the affected raw lines (both
    /// <c>#$ ELEMENTS</c> and <c>#$ MISCEL_1</c>'s RRMAT array, which is positional/one-entry-
    /// per-element and would otherwise desync from the new element count) so every other element
    /// and section is untouched. <paramref name="replacements"/> all get <paramref name="original"/>'s
    /// own material ID.
    /// </summary>
    public void ReplaceElement(Element original, IReadOnlyList<Element> replacements)
    {
        var index = Elements.IndexOf(original);
        if (index < 0)
        {
            throw new InvalidOperationException("Element not found in this file's Elements.");
        }

        var oldElementCount = Elements.Count;
        var materialId = index < MaterialIds.Count ? MaterialIds[index] : 1;

        Elements.RemoveAt(index);
        Elements.InsertRange(index, replacements);
        Control.NumElements = Elements.Count;

        var elementsBlock = Blocks.First(b => string.Equals(b.Name, "ELEMENTS", StringComparison.OrdinalIgnoreCase));
        var lineIndex = index * Element.LinesPerElement;
        elementsBlock.RawLines.RemoveRange(lineIndex, Element.LinesPerElement);
        elementsBlock.RawLines.InsertRange(lineIndex, replacements.SelectMany(e => e.ToRawLines()));

        MaterialIds.RemoveAt(index);
        MaterialIds.InsertRange(index, Enumerable.Repeat(materialId, replacements.Count));

        var miscel1Block = Blocks.FirstOrDefault(b => string.Equals(b.Name, "MISCEL_1", StringComparison.OrdinalIgnoreCase));
        if (miscel1Block is not null)
        {
            var oldRrmatLineCount = (int)Math.Ceiling(oldElementCount / 6.0);
            var trailingLines = miscel1Block.RawLines.Skip(oldRrmatLineCount).ToList();
            miscel1Block.RawLines.Clear();
            miscel1Block.RawLines.AddRange(FixedWidth.FormatRealLines(MaterialIds.Select(m => (double)m).ToList()));
            miscel1Block.RawLines.AddRange(trailingLines);
        }
    }

    /// <summary>Resolves an element's <c>#$ ALLOWBLS</c> record via its 1-based pointer, or null if it has none.</summary>
    public AllowableStress? TryGetAllowableStress(Element element)
    {
        var pointer = element.AllowableStressPointer;
        return pointer > 0 && pointer <= AllowableStresses.Count ? AllowableStresses[pointer - 1] : null;
    }

    /// <summary>
    /// Computes each node's position by walking <see cref="Elements"/> in order and accumulating
    /// delta coordinates. Conduit doesn't parse the optional <c>#$ COORDS</c> section, so each
    /// disconnected chain's first FROM node is treated as the origin — fine for the span/length
    /// calculations v1's heuristics need, which only depend on relative geometry along a run.
    /// </summary>
    public Dictionary<int, (double X, double Y, double Z)> ComputeNodePositions()
    {
        var positions = new Dictionary<int, (double X, double Y, double Z)>();
        foreach (var element in Elements)
        {
            if (!positions.ContainsKey(element.FromNode))
            {
                positions[element.FromNode] = (0, 0, 0);
            }
            var from = positions[element.FromNode];
            positions[element.ToNode] = (from.X + element.DeltaX, from.Y + element.DeltaY, from.Z + element.DeltaZ);
        }
        return positions;
    }
}
