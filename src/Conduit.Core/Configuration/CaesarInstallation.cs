namespace Conduit.Core.Configuration;

/// <summary>One discovered CAESAR II install version, and the paths derived from it.</summary>
/// <param name="Version">The version folder's own name, parsed (e.g. <c>15.01</c>).</param>
/// <param name="RootDirectory">The version's own directory, e.g. <c>...\CAESAR II\15.01</c>.</param>
/// <param name="SystemDirectory">
/// <c>RootDirectory</c>'s <c>System</c> subfolder — what <c>caesar.cfg</c>'s
/// <c>SYSTEM_DIRECTORY_NAME</c> resolves relative to, and where the material/component databases
/// live for this version.
/// </param>
public sealed record CaesarInstallation(Version Version, string RootDirectory, string SystemDirectory);
