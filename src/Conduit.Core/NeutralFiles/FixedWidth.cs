using System.Globalization;

namespace Conduit.Core.NeutralFiles;

/// <summary>
/// Helpers for the CAESAR II neutral file's fixed-width columnar record format
/// (FORTRAN <c>(2X, 6G13.6)</c> / <c>(2X, 6I13)</c>): each data line is two leading
/// spaces followed by up to six 13-character fields, packed with no separator — a
/// negative number's sign occupies the column a separating space would otherwise use,
/// so fields must be sliced by fixed column width, never split on whitespace.
/// </summary>
internal static class FixedWidth
{
    public const int FieldWidth = 13;
    public const int ValuesPerLine = 6;
    private const int LinePrefixWidth = 2;

    /// <summary>
    /// Reads <paramref name="count"/> packed real values starting at <paramref name="lineIndex"/>
    /// within <paramref name="lines"/>, advancing <paramref name="lineIndex"/> past the lines consumed.
    /// </summary>
    public static List<double> ParseReals(IReadOnlyList<string> lines, ref int lineIndex, int count)
    {
        var values = new List<double>(count);
        while (values.Count < count)
        {
            var line = RequireLine(lines, lineIndex, "real value block");
            var take = Math.Min(ValuesPerLine, count - values.Count);
            for (var i = 0; i < take; i++)
            {
                var field = SliceField(line, i);
                values.Add(double.Parse(field, NumberStyles.Float, CultureInfo.InvariantCulture));
            }
            lineIndex++;
        }
        return values;
    }

    /// <summary>
    /// Reads <paramref name="count"/> packed integer values starting at <paramref name="lineIndex"/>
    /// within <paramref name="lines"/>, advancing <paramref name="lineIndex"/> past the lines consumed.
    /// </summary>
    public static List<long> ParseInts(IReadOnlyList<string> lines, ref int lineIndex, int count)
    {
        var values = new List<long>(count);
        while (values.Count < count)
        {
            var line = RequireLine(lines, lineIndex, "integer value block");
            var take = Math.Min(ValuesPerLine, count - values.Count);
            for (var i = 0; i < take; i++)
            {
                var field = SliceField(line, i);
                values.Add(long.Parse(field, NumberStyles.Integer, CultureInfo.InvariantCulture));
            }
            lineIndex++;
        }
        return values;
    }

    private static string RequireLine(IReadOnlyList<string> lines, int lineIndex, string context)
    {
        if (lineIndex >= lines.Count)
        {
            throw new NeutralFileParseException($"Expected another line while reading {context}, but reached the end of the section.");
        }
        return lines[lineIndex];
    }

    /// <summary>Slices the (0-based) <paramref name="fieldIndex"/>-th 13-char field on a data line.</summary>
    private static string SliceField(string line, int fieldIndex)
    {
        var start = LinePrefixWidth + (fieldIndex * FieldWidth);
        if (start >= line.Length)
        {
            return "0";
        }
        var length = Math.Min(FieldWidth, line.Length - start);
        return line.Substring(start, length).Trim();
    }

    public static string FormatInt(long value) =>
        value.ToString(CultureInfo.InvariantCulture).PadLeft(FieldWidth);

    /// <summary>Formats a value as FORTRAN <c>G13.6</c> scientific notation, e.g. <c>1.000000E+02</c>.</summary>
    public static string FormatReal(double value) =>
        value.ToString("0.000000E+00", CultureInfo.InvariantCulture).PadLeft(FieldWidth);

    public static IEnumerable<string> FormatIntLines(IReadOnlyList<long> values) =>
        Chunk(values, ValuesPerLine).Select(chunk => "  " + string.Concat(chunk.Select(FormatInt)));

    public static IEnumerable<string> FormatRealLines(IReadOnlyList<double> values) =>
        Chunk(values, ValuesPerLine).Select(chunk => "  " + string.Concat(chunk.Select(FormatReal)));

    private static IEnumerable<IReadOnlyList<T>> Chunk<T>(IReadOnlyList<T> values, int size)
    {
        for (var i = 0; i < values.Count; i += size)
        {
            yield return values.Skip(i).Take(size).ToList();
        }
    }

    /// <summary>
    /// Formats a length-prefixed string field, FORTRAN <c>(7X, I5, 1X, A&lt;n&gt;)</c>: seven
    /// spaces, the string's length right-justified in 5 chars, one space, then the string itself.
    /// </summary>
    public static string FormatLengthPrefixedString(string value) =>
        "       " + value.Length.ToString(CultureInfo.InvariantCulture).PadLeft(5) + " " + value;

    /// <summary>Parses a line written by <see cref="FormatLengthPrefixedString"/>.</summary>
    public static string ParseLengthPrefixedString(string line)
    {
        if (line.Length < 12)
        {
            return string.Empty;
        }
        var lengthField = line.Substring(7, 5).Trim();
        if (!int.TryParse(lengthField, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length) || length <= 0)
        {
            return string.Empty;
        }
        var start = 13;
        if (start >= line.Length)
        {
            return string.Empty;
        }
        var available = line.Length - start;
        return line.Substring(start, Math.Min(length, available));
    }
}
