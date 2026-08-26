namespace Conduit.Core.NeutralFiles;

/// <summary>
/// The subset of <c>#$ UNITS</c>'s conversion-constant block Conduit needs: how to convert this
/// file's own length values into millimetres, the unit Conduit always computes and reports spans
/// in (per direct instruction — see QUESTIONS.md's "mm as default" entry).
///
/// <para>Only two unit systems are distinguished — metric (mm/N/MPa/kg) and English
/// (in/lbf/psi/lb) — matching every real sample and fixture seen so far. A file whose <c>#$
/// UNITS</c> data doesn't clearly match either (or is missing/unparseable) falls back to
/// <see cref="Metric"/>, Conduit's default, rather than guessing further.</para>
/// </summary>
public sealed class UnitsSection
{
    /// <summary>Multiply a native-unit length/diameter/thickness value by this to get millimetres.</summary>
    public required double LengthToMillimetres { get; init; }

    /// <summary>True when this file's native unit system is metric (mm/N/MPa/kg), false when English (in/lbf/psi/lb).</summary>
    public required bool IsMetric { get; init; }

    /// <summary>Conduit's default when a file has no usable <c>#$ UNITS</c> data (per direct instruction: mm/metric).</summary>
    public static UnitsSection Metric { get; } = new() { LengthToMillimetres = 1.0, IsMetric = true };

    private const double MillimetresPerInch = 25.4;

    /// <summary>
    /// A file's native length unit is English (inches) when its own length is already 1 inch —
    /// i.e. CNVLEN itself is ~1.0. It's metric (mm) when CNVLEN is ~25.4 (confirmed
    /// byte-identical across 3 real samples: <c>fixtures/real-samples/*.cii</c>). 5.0 sits
    /// cleanly between those two known values.
    /// </summary>
    private const double MetricCnvlenThreshold = 5.0;

    /// <summary>
    /// Parses <c>#$ UNITS</c>'s first conversion constant, CNVLEN (<c>NeutralFile-v15.pdf</c>'s
    /// FORTRAN <c>(2X, 6G13.6)</c>, first value of the first line) — "how many native length
    /// units equal one inch". Falls back to <see cref="Metric"/> when the block is missing/empty
    /// (older fixtures, or a file predating this parsing) or unparseable.
    /// </summary>
    public static UnitsSection Parse(NeutralFileBlock? block)
    {
        if (block is null || block.RawLines.Count == 0)
        {
            return Metric;
        }

        try
        {
            var lineIndex = 0;
            var cnvlen = FixedWidth.ParseReals(block.RawLines, ref lineIndex, 1)[0];
            if (cnvlen <= 0)
            {
                return Metric;
            }

            return new UnitsSection
            {
                LengthToMillimetres = MillimetresPerInch / cnvlen,
                IsMetric = cnvlen > MetricCnvlenThreshold,
            };
        }
        catch (Exception ex) when (ex is NeutralFileParseException or FormatException or OverflowException)
        {
            return Metric;
        }
    }
}
