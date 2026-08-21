namespace Conduit.Core.Conversion;

/// <summary>
/// Unimplemented skeleton for an <see cref="INeutralFileConverter"/> backed by CAESAR II's own
/// <c>iecho.exe</c>. Not wired up or tested in this repo — <c>iecho.exe</c> ships with a licensed
/// CAESAR II install and is Windows-only, unavailable in this project's headless Linux build/test
/// environment. Intended to be completed and validated later on a Windows machine.
///
/// <para><b>Planned implementation</b> (see SPEC.md's "Native file adapter (iecho)" for the full
/// reasoning):</para>
/// <list type="bullet">
/// <item><c>iecho.exe</c>'s location isn't fixed — search common install paths (both the
/// "Intergraph CAS" and "Hexagon" branded install directories, across CAESAR II versions),
/// plus an environment-variable/config override, the same pattern as any external-tool
/// discovery.</item>
/// <item><see cref="ToNativeFile"/> (<c>.cii</c> → <c>.C2</c>) is expected to be a plain,
/// silent subprocess invocation of <c>iecho.exe</c> with the neutral file path as an argument.</item>
/// <item><see cref="ToNeutralFile"/> (<c>.C2</c> → <c>.cii</c>) has an open question: whether
/// <c>iecho.exe</c> truly supports a silent export, or needs to be launched interactively with
/// the caller polling for the output file to appear (as a reference implementation shared for
/// context — not copied here — did it). Verify directly against <c>iecho.exe</c> on Windows
/// before assuming either way.</item>
/// </list>
/// </summary>
public sealed class IechoConverter : INeutralFileConverter
{
    public string ToNeutralFile(string nativePath) =>
        throw new NotImplementedException(
            "IechoConverter requires a licensed CAESAR II install (iecho.exe) on Windows, " +
            "neither available in this build environment. See this class's XML docs and SPEC.md's " +
            "\"Native file adapter (iecho)\" section for the planned implementation.");

    public string ToNativeFile(string neutralPath) =>
        throw new NotImplementedException(
            "IechoConverter requires a licensed CAESAR II install (iecho.exe) on Windows, " +
            "neither available in this build environment. See this class's XML docs and SPEC.md's " +
            "\"Native file adapter (iecho)\" section for the planned implementation.");
}
