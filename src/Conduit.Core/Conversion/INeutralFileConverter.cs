namespace Conduit.Core.Conversion;

/// <summary>
/// Converts between CAESAR II's native input format (<c>.C2</c>/<c>._A</c> — what a piping
/// engineer actually has on disk) and the <c>.cii</c> neutral file Conduit parses. See
/// <see cref="IechoConverter"/> and SPEC.md's "Native file adapter (iecho)" section: the goal is
/// that users never have to run <c>iecho.exe</c> by hand.
/// </summary>
public interface INeutralFileConverter
{
    /// <summary>Converts a native <c>.C2</c>/<c>._A</c> file to a <c>.cii</c> neutral file, returning the neutral file's path.</summary>
    string ToNeutralFile(string nativePath);

    /// <summary>Converts a <c>.cii</c> neutral file back to CAESAR II's native format, returning the native file's path.</summary>
    string ToNativeFile(string neutralPath);
}
