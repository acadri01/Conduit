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

    /// <summary>
    /// The "rigid" stiffness CAESAR II itself writes into <c>#$ RESTRANT</c> for every non-spring
    /// restraint (any type other than <see cref="RestraintType.Xspr"/>/<see cref="RestraintType.Yspr"/>/
    /// <see cref="RestraintType.Zspr"/>), in this file's native force/length unit — a restraint with
    /// stiffness 0 is a spring with no resistance, not a rigid support, so this can't be left at 0.
    /// Confirmed byte-exact against <c>fixtures/real-samples/44002.cii</c>'s real restraints (all
    /// 1.751200E+11 N/mm): CAESAR's constant is 1E12 lbf/in, converted to native units via
    /// <c>#$ UNITS</c>' 14th conversion constant, CNVTSF ("translational stiffness conversion" —
    /// native units per 1 lbf/in) — 1E12 * 0.17512 = 1.7512E11, exactly the sample's value.
    /// </summary>
    public required double RigidRestraintStiffness { get; init; }

    /// <summary>Conduit's default when a file has no usable <c>#$ UNITS</c> data (per direct instruction: mm/metric).</summary>
    public static UnitsSection Metric { get; } =
        new() { LengthToMillimetres = 1.0, IsMetric = true, RigidRestraintStiffness = DefaultMetricRigidStiffness };

    private const double MillimetresPerInch = 25.4;

    /// <summary>CAESAR II's fixed "rigid" restraint stiffness constant, in lbf/in — see <see cref="RigidRestraintStiffness"/>.</summary>
    private const double RigidStiffnessLbfPerInch = 1.0e12;

    /// <summary>1-based position of CNVTSF (translational stiffness conversion) within <c>#$ UNITS</c>' conversion-constant array.</summary>
    private const int TranslationalStiffnessFieldPosition = 14;

    /// <summary>
    /// Fallback CNVTSF when a file's own <c>#$ UNITS</c> data doesn't reach that far (older/partial
    /// fixtures) — confirmed byte-exact against the real sample: 1.751200E-01 native (N/mm) per
    /// lbf/in, i.e. CNVFOR/CNVLEN = 4.448 N/lbf / 25.4 mm/in.
    /// </summary>
    private const double DefaultMetricCnvtsf = 0.17512;

    private const double DefaultMetricRigidStiffness = RigidStiffnessLbfPerInch * DefaultMetricCnvtsf;

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

            var isMetric = cnvlen > MetricCnvlenThreshold;
            var cnvtsf = TryParseCnvtsf(block) ?? (isMetric ? DefaultMetricCnvtsf : 1.0);

            return new UnitsSection
            {
                LengthToMillimetres = MillimetresPerInch / cnvlen,
                IsMetric = isMetric,
                RigidRestraintStiffness = RigidStiffnessLbfPerInch * cnvtsf,
            };
        }
        catch (Exception ex) when (ex is NeutralFileParseException or FormatException or OverflowException)
        {
            return Metric;
        }
    }

    /// <summary>
    /// Parses CNVTSF on its own (position <see cref="TranslationalStiffnessFieldPosition"/>) — kept
    /// separate from the main CNVLEN parse so a file with only a short/partial <c>#$ UNITS</c> block
    /// (fewer than 14 values — older fixtures) still gets a correct length conversion instead of
    /// falling all the way back to <see cref="Metric"/>; it just uses a per-system default CNVTSF
    /// instead of that file's own.
    /// </summary>
    private static double? TryParseCnvtsf(NeutralFileBlock block)
    {
        try
        {
            var lineIndex = 0;
            var values = FixedWidth.ParseReals(block.RawLines, ref lineIndex, TranslationalStiffnessFieldPosition);
            var cnvtsf = values[TranslationalStiffnessFieldPosition - 1];
            return cnvtsf > 0 ? cnvtsf : null;
        }
        catch (Exception ex) when (ex is NeutralFileParseException or FormatException or OverflowException)
        {
            return null;
        }
    }
}
