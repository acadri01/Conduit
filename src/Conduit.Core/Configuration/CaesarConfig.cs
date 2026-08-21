namespace Conduit.Core.Configuration;

/// <summary>
/// The global CAESAR II settings from a model directory's <c>caesar.cfg</c> file — install-wide
/// defaults (axis convention, default piping code, material/structural database locations) that
/// apply to every job in that directory, as distinct from the per-job settings baked into each
/// job's own neutral file.
///
/// <para>Exposes only the handful of fields v1 cares about; <see cref="Values"/> holds everything
/// the parser recognized, keyed exactly as written in the file, for anything else callers need.</para>
/// </summary>
public sealed class CaesarConfig
{
    public required IReadOnlyDictionary<string, string> Values { get; init; }

    /// <summary>Whether Z (not Y) is the model's vertical axis, from <c>Z_AXIS_UP</c>. Null if the file didn't set it.</summary>
    public bool? ZAxisUp => Values.TryGetValue("Z_AXIS_UP", out var value) ? value.Equals("YES", StringComparison.OrdinalIgnoreCase) : null;

    /// <summary>The default piping code and edition (e.g. <c>B31.3_2020</c>) new jobs in this directory are analyzed against, from <c>DEFAULT_CODE</c>.</summary>
    public string? DefaultCode => Values.GetValueOrDefault("DEFAULT_CODE");

    /// <summary>Name of the CAESAR II system directory (material/component databases, unit files, …) relative to the install, from <c>SYSTEM_DIRECTORY_NAME</c>.</summary>
    public string? SystemDirectoryName => Values.GetValueOrDefault("SYSTEM_DIRECTORY_NAME");

    /// <summary>User-defined material database file name (<c>.UMD</c>), from <c>User_Material_File_Name</c>, if one is configured.</summary>
    public string? UserMaterialFileName => Values.GetValueOrDefault("User_Material_File_Name");

    /// <summary>Structural steel database file name, from <c>STRCT_DBASE</c>.</summary>
    public string? StructuralDatabaseFileName => Values.GetValueOrDefault("STRCT_DBASE");
}
