namespace Conduit.Core.NeutralFiles;

/// <summary>
/// One record from <c>#$ ELEMENTS</c> — a pipe (or other) element between two nodes. Elements are
/// normally read-only passthrough (unmodified elements' raw lines are left exactly as read), but
/// <see cref="NeutralFile.ReplaceElement"/> can splice new elements in — e.g. splitting an
/// overlong span into evenly-spaced chunks with a new node at each interior boundary, per direct
/// instruction — surgically, without touching any other element's raw lines.
/// </summary>
public sealed class Element
{
    /// <summary>Every element record is a fixed 15-line block, regardless of content: 53 reals (9 lines), name, line number, color/visibility, 15 pointer ints (3 lines).</summary>
    public const int LinesPerElement = 15;

    /// <summary>All 53 real values from the element's basic-data block, in vendor-doc order (0-based).</summary>
    public required IReadOnlyList<double> RealValues { get; init; }

    /// <summary>
    /// The 15-item IEL auxiliary-data pointer array (1-based pointers into each auxiliary
    /// section's records; 0 = no data of that type for this element), in vendor-doc order:
    /// bend, rigid, expansion joint, restraint, displacement, force/moment, uniform load, wind
    /// load, element offset, allowable stress, intersection, node name, reducer, flange,
    /// nozzle/equipment check.
    /// </summary>
    public required IReadOnlyList<int> AuxiliaryPointers { get; init; }

    /// <summary>0-based index of the restraint pointer within <see cref="AuxiliaryPointers"/> (vendor doc's pointer #4).</summary>
    public const int RestraintPointerIndex = 3;

    public int FromNode => (int)RealValues[0];
    public int ToNode => (int)RealValues[1];
    public double DeltaX => RealValues[2];
    public double DeltaY => RealValues[3];
    public double DeltaZ => RealValues[4];

    /// <summary>Actual outside diameter, in the file's length units.</summary>
    public double OutsideDiameter => RealValues[5];

    /// <summary>Actual wall thickness, in the file's length units.</summary>
    public double WallThickness => RealValues[6];

    /// <summary>Straight-line length of this element (from the delta coordinates).</summary>
    public double Length => Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY) + (DeltaZ * DeltaZ));

    public string Name { get; init; } = string.Empty;
    public string LineNumber { get; init; } = string.Empty;

    /// <summary>1-based pointer into <c>#$ ALLOWBLS</c> (0 = none).</summary>
    public int AllowableStressPointer => AuxiliaryPointers[9];

    /// <summary>
    /// 1-based pointer into <c>#$ SIF&amp;TEES</c> (0 = none) — the vendor doc's "Pointer to
    /// Intersection Auxiliary field". Set on the element whose <see cref="ToNode"/> is the
    /// tee/intersection node, the same convention as the bend pointer. Confirmed against a real
    /// user-supplied sample: a node can carry this pointer (needing SIF/tee treatment) without
    /// having branch geometry (node degree 2, no third element) — CAESAR marks some fittings as
    /// intersections independent of whether a branch pipe is modeled in the same file — so this is
    /// the correct signal for "is this a tee" (per direct instruction), not node degree.
    /// </summary>
    public int IntersectionPointer => AuxiliaryPointers[10];

    /// <summary>1-based pointer into <c>#$ EQUIPMNT</c> (0 = none).</summary>
    public int EquipmentCheckPointer => AuxiliaryPointers[14];

    /// <summary>
    /// Parses <paramref name="count"/> elements starting at <paramref name="lineIndex"/>, each a
    /// fixed 15-line record: 53 reals (9 lines), name, line number, 2-value color/visibility line,
    /// then 15 pointer ints (3 lines).
    /// </summary>
    public static List<Element> ParseMany(IReadOnlyList<string> lines, int startLineIndex, int count)
    {
        var elements = new List<Element>(count);
        var lineIndex = startLineIndex;
        for (var i = 0; i < count; i++)
        {
            var real = FixedWidth.ParseReals(lines, ref lineIndex, 53);
            var name = FixedWidth.ParseLengthPrefixedString(RequireLine(lines, lineIndex++));
            var lineNumber = FixedWidth.ParseLengthPrefixedString(RequireLine(lines, lineIndex++));
            _ = FixedWidth.ParseReals(lines, ref lineIndex, 2); // color, visibility — not needed by v1 heuristics
            var pointers = FixedWidth.ParseInts(lines, ref lineIndex, 15).Select(v => (int)v).ToList();

            elements.Add(new Element { RealValues = real, Name = name, LineNumber = lineNumber, AuxiliaryPointers = pointers });
        }
        return elements;
    }

    /// <summary>
    /// The inverse of <see cref="ParseMany"/>'s per-element format — used both by
    /// <c>NeutralFileFixtureBuilder</c> (test fixtures) and <see cref="NeutralFile.ReplaceElement"/>
    /// (production element-splitting), so the two can never format-drift apart the way the
    /// color/visibility line once did.
    /// </summary>
    public IEnumerable<string> ToRawLines()
    {
        var lines = new List<string>();
        lines.AddRange(FixedWidth.FormatRealLines(RealValues));
        lines.Add(FixedWidth.FormatLengthPrefixedString(Name));
        lines.Add(FixedWidth.FormatLengthPrefixedString(LineNumber));
        // Line color/line visibility: NeutralFile-v15.pdf labels this (2X, 6G13.6) — real-value
        // format — but all 3 real samples (fixtures/real-samples/*.cii) write it as plain integers
        // ("-1 -1", no decimal/E-notation) instead. See QUESTIONS.md's "ELEMENTS color/visibility
        // line" entry.
        lines.AddRange(FixedWidth.FormatIntLines([-1, -1]));
        lines.AddRange(FixedWidth.FormatIntLines(AuxiliaryPointers.Select(p => (long)p).ToList()));
        return lines;
    }

    private static string RequireLine(IReadOnlyList<string> lines, int lineIndex)
    {
        if (lineIndex >= lines.Count)
        {
            throw new NeutralFileParseException("Expected an element name/line-number line, but reached the end of the section.");
        }
        return lines[lineIndex];
    }
}
