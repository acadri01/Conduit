namespace Conduit.Core.NeutralFiles;

/// <summary>
/// One line from the optional <c>#$ NODENAME</c> section — FORTRAN <c>(2X, A10, 16X, A10)</c>:
/// a FROM node name, then (after 16 filler columns) a TO node name. This section is read-only
/// in v1 (not used by any heuristic, not modified) and, per the vendor doc, may be entirely
/// absent — no header at all — in files that don't use node names; that was confirmed against
/// real CAESAR II output during spec research, not just documentation.
/// </summary>
public sealed class NodeName
{
    public required string FromName { get; init; }
    public required string ToName { get; init; }

    public static List<NodeName> ParseMany(IReadOnlyList<string> lines)
    {
        var result = new List<NodeName>(lines.Count);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            var from = Slice(line, 2, 10);
            var to = Slice(line, 28, 10);
            result.Add(new NodeName { FromName = from, ToName = to });
        }
        return result;
    }

    private static string Slice(string line, int start, int length)
    {
        if (start >= line.Length)
        {
            return string.Empty;
        }
        return line.Substring(start, Math.Min(length, line.Length - start)).Trim();
    }
}
