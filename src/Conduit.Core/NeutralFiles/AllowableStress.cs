namespace Conduit.Core.NeutralFiles;

/// <summary>
/// One record from <c>#$ ALLOWBLS</c> — allowable-stress data CAESAR II itself computed for an
/// element's material/code/temperature when the file was generated. 168 real values per record;
/// v1 only names the handful of fields it actually uses (cold allowable stress, piping code id)
/// and keeps the rest available via <see cref="Values"/> for future use — see the vendor doc for
/// the full field list (fatigue curve pairs, code-specific fields that vary by piping code, etc.).
/// Read-only: Conduit never writes this section.
/// </summary>
public sealed class AllowableStress
{
    /// <summary>All 168 real values, in vendor-doc order (0-based).</summary>
    public required IReadOnlyList<double> Values { get; init; }

    /// <summary>Cold (ambient) allowable stress for the element's material/code.</summary>
    public double ColdAllowableStress => Values[0];

    /// <summary>An internal CAESAR II piping-code identifier (meaning is code-table-specific, not decoded by v1).</summary>
    public double PipingCodeId => Values[11];

    /// <summary>Hot allowable stress for thermal case 1-9 (non-contiguous in the underlying array — see the vendor doc).</summary>
    public double HotAllowableStress(int thermalCase) => thermalCase switch
    {
        1 => Values[1],
        2 => Values[2],
        3 => Values[3],
        >= 4 and <= 9 => Values[8 + thermalCase], // cases 4-9 -> indices 12-17
        _ => throw new ArgumentOutOfRangeException(nameof(thermalCase), thermalCase, "Thermal case must be 1-9."),
    };

    public static List<AllowableStress> ParseMany(IReadOnlyList<string> lines, int count)
    {
        var records = new List<AllowableStress>(count);
        var lineIndex = 0;
        for (var i = 0; i < count; i++)
        {
            var values = FixedWidth.ParseReals(lines, ref lineIndex, 168);
            records.Add(new AllowableStress { Values = values });
        }
        return records;
    }
}
