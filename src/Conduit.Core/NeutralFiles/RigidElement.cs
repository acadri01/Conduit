namespace Conduit.Core.NeutralFiles;

/// <summary>
/// One record from <c>#$ RIGID</c> — a rigid element's weight and type, per
/// <c>NeutralFile-v15.pdf</c>'s two-member <c>RIG</c> array. Read-only: Conduit never writes this
/// section. Resolved via an element's <see cref="Element.RigidPointer"/>, the same 1-based-pointer
/// convention as <see cref="Element.IntersectionPointer"/>/the bend pointer.
/// </summary>
public sealed class RigidElement
{
    public double Weight { get; init; }
    public double Type { get; init; }

    /// <summary>Parses <paramref name="count"/> rigid records — one line, two values, per record.</summary>
    public static List<RigidElement> ParseMany(IReadOnlyList<string> lines, int count)
    {
        var rigids = new List<RigidElement>(count);
        var lineIndex = 0;
        for (var i = 0; i < count; i++)
        {
            var values = FixedWidth.ParseReals(lines, ref lineIndex, 2);
            rigids.Add(new RigidElement { Weight = values[0], Type = values[1] });
        }
        return rigids;
    }
}
