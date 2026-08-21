namespace Conduit.Core.NeutralFiles;

/// <summary>
/// One <c>#$ NAME</c>-delimited chunk of a neutral file — either a top-level section
/// (<c>VERSION</c>, <c>CONTROL</c>, <c>ELEMENTS</c>, <c>AUX_DATA</c>, <c>MISCEL_1</c>,
/// <c>UNITS</c>, <c>COORDS</c>) or one of <c>AUX_DATA</c>'s subsections (<c>NODENAME</c>,
/// <c>BEND</c>, <c>RESTRANT</c>, …). The file has no closing tags, so a block simply runs
/// from its header line up to (not including) the next <c>#$ </c> header line, or EOF.
/// </summary>
public sealed class NeutralFileBlock
{
    /// <summary>The section name, trimmed (e.g. <c>"RESTRANT"</c>), without the leading <c>#$ </c>.</summary>
    public required string Name { get; init; }

    /// <summary>The original header line text, preserved verbatim for byte-identical round-trip.</summary>
    public required string HeaderLine { get; init; }

    /// <summary>
    /// The lines between this header and the next, mutable so blocks Conduit actively edits
    /// (<c>CONTROL</c>, <c>RESTRANT</c>) can be regenerated. Every other block's lines are
    /// written back verbatim.
    /// </summary>
    public required List<string> RawLines { get; init; }
}
