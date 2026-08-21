namespace Conduit.Core.Configuration;

/// <summary>
/// Locates CAESAR II installations under the real, user-confirmed data directory layout:
/// <c>C:\ProgramData\Intergraph CAS\CAESAR II\&lt;version&gt;\System</c> — one version subfolder
/// per installed CAESAR II release (e.g. <c>15.01</c>), each containing a <c>System</c> folder
/// with that version's material/component databases. This is what <c>caesar.cfg</c>'s
/// <c>SYSTEM_DIRECTORY_NAME</c> (typically just <c>SYSTEM</c>) resolves relative to.
///
/// <para>Conduit's supported version floor is 15.00 — older installations are deliberately not
/// discovered, per the user's build-scope direction ("begin the build from 15.00 and up").</para>
///
/// <para><b>Deliberately excludes <c>iecho.exe</c>.</b> The converter binary lives in a different
/// branch of the CAESAR II install (the application/program directory, not this <c>ProgramData</c>
/// data directory), per the user's reference wrapper — see "Native file adapter (iecho)" in
/// SPEC.md. Don't assume it's under any path this locator returns; it needs its own, separate
/// discovery logic when `IechoConverter` is implemented.</para>
///
/// <para>Pure <see cref="System.IO"/> directory listing against an injectable root, so — unlike
/// COM automation or actually invoking <c>iecho.exe</c> — this is fully testable without Windows
/// or a licensed CAESAR II install; only the <em>default</em> root is Windows-specific.</para>
/// </summary>
public static class CaesarInstallationLocator
{
    /// <summary>The real default CAESAR II data directory on Windows, confirmed by the user.</summary>
    public const string DefaultInstallRoot = @"C:\ProgramData\Intergraph CAS\CAESAR II";

    /// <summary>Conduit's supported version floor — installations older than this are not returned.</summary>
    public static readonly Version MinimumSupportedVersion = new(15, 0);

    /// <summary>
    /// Finds every installed version under <paramref name="installRoot"/> (default: <see cref="DefaultInstallRoot"/>)
    /// at or above <see cref="MinimumSupportedVersion"/>, newest first. Returns an empty list — never
    /// throws — if the root doesn't exist, matching how <c>caesar.cfg</c> lookup is treated elsewhere:
    /// optional/best-effort, not a hard requirement to run.
    /// </summary>
    public static List<CaesarInstallation> FindInstallations(string? installRoot = null)
    {
        var root = installRoot ?? DefaultInstallRoot;
        if (!Directory.Exists(root))
        {
            return [];
        }

        var installations = new List<CaesarInstallation>();
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            var folderName = Path.GetFileName(directory);
            if (!Version.TryParse(folderName, out var version) || version < MinimumSupportedVersion)
            {
                continue;
            }

            installations.Add(new CaesarInstallation(version, directory, Path.Combine(directory, "System")));
        }

        return installations.OrderByDescending(installation => installation.Version).ToList();
    }

    /// <summary>The newest supported installation under <paramref name="installRoot"/>, or null if none was found.</summary>
    public static CaesarInstallation? FindLatest(string? installRoot = null) =>
        FindInstallations(installRoot).FirstOrDefault();
}
