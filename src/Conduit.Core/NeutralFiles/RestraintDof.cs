namespace Conduit.Core.NeutralFiles;

/// <summary>
/// One degree-of-freedom slot within a <c>#$ RESTRANT</c> record. Every restraint always
/// reserves exactly six of these (one per DOF); unused slots are zero-filled (<see cref="Node"/>
/// and <see cref="RawTypeCode"/> both 0) rather than omitted — confirmed against real CAESAR II
/// output, not just the vendor doc's array-dimension notes.
/// </summary>
public sealed class RestraintDof
{
    public int Node { get; set; }

    /// <summary>The raw 1–62 restraint-type code (0 = unused slot). Use <see cref="Type"/> for the typed value.</summary>
    public int RawTypeCode { get; set; }

    public double Stiffness { get; set; }
    public double Gap { get; set; }
    public double Friction { get; set; }
    public int ConnectingNode { get; set; }
    public double DirectionCosineX { get; set; }
    public double DirectionCosineY { get; set; }
    public double DirectionCosineZ { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Guid { get; set; } = string.Empty;

    public bool IsUsed => Node != 0 && RawTypeCode != 0;

    public RestraintType? Type => RawTypeCode is >= 1 and <= 62 ? (RestraintType)RawTypeCode : null;

    internal static RestraintDof Parse(IReadOnlyList<string> lines, ref int lineIndex)
    {
        var values = FixedWidth.ParseReals(lines, ref lineIndex, 9);
        var tagLine = RequireLine(lines, lineIndex++);
        var guidLine = RequireLine(lines, lineIndex++);

        return new RestraintDof
        {
            Node = (int)values[0],
            RawTypeCode = (int)values[1],
            Stiffness = values[2],
            Gap = values[3],
            Friction = values[4],
            ConnectingNode = (int)values[5],
            DirectionCosineX = values[6],
            DirectionCosineY = values[7],
            DirectionCosineZ = values[8],
            Tag = FixedWidth.ParseLengthPrefixedString(tagLine),
            Guid = FixedWidth.ParseLengthPrefixedString(guidLine),
        };
    }

    internal IEnumerable<string> ToRawLines()
    {
        var values = new List<double>
        {
            Node, RawTypeCode, Stiffness, Gap, Friction, ConnectingNode,
            DirectionCosineX, DirectionCosineY, DirectionCosineZ,
        };
        foreach (var line in FixedWidth.FormatRealLines(values))
        {
            yield return line;
        }
        yield return FixedWidth.FormatLengthPrefixedString(Tag);
        yield return FixedWidth.FormatLengthPrefixedString(Guid);
    }

    private static string RequireLine(IReadOnlyList<string> lines, int lineIndex)
    {
        if (lineIndex >= lines.Count)
        {
            throw new NeutralFileParseException("Expected a restraint tag/GUID line, but reached the end of the section.");
        }
        return lines[lineIndex];
    }
}
