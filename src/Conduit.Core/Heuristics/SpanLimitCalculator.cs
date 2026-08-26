using Conduit.Core.NeutralFiles;

namespace Conduit.Core.Heuristics;

/// <summary>
/// Computes a maximum allowable unsupported span for a pipe element from simple beam theory,
/// as a stand-in for a real B31.3 span/sag calculation. Always returns the span in millimetres —
/// Conduit's default unit system (per direct instruction) — converting the element's own data
/// first when its file is in a different unit system.
///
/// <para><b>Simplifying assumptions (v1, not code-compliant):</b></para>
/// <list type="bullet">
/// <item>Each span is modeled as a uniformly-loaded, simply-supported beam: max bending moment
/// <c>M = w·L²/8</c>, max bending stress <c>σ = M/Z</c>. Solving for span length at an assumed
/// allowable bending stress gives <c>L = sqrt(8·σ_allow·Z / w)</c>. Real span tables are usually
/// governed by a sag/deflection limit rather than bending stress once diameter grows past a few
/// inches — this is a deliberately simpler, more conservative-by-construction proxy, not a
/// substitute for either calculation.</item>
/// <item>The allowable stress used is the element's own <c>#$ ALLOWBLS</c> cold allowable stress
/// when the file provides one (real, per-material/code/temperature data CAESAR II computed when
/// generating the file — see <see cref="ComputeMaxSpan(NeutralFile, Element)"/>), falling back to
/// a default (<see cref="DefaultAllowableBendingStressMpa"/> or
/// <see cref="DefaultAllowableBendingStressPsi"/>, matching the file's own unit system) only when
/// the file has no allowable-stress record for that element. Note this still isn't a
/// code-compliant span calculation even when a real allowable is available — the beam formula
/// above is still a simplification, just fed a materially better input than a guess.</item>
/// <item>Distributed weight <c>w</c> includes pipe metal, insulation, and a fully-liquid-filled
/// bore, computed from the element's own density fields (falling back to
/// <see cref="DefaultSteelDensityKgPerM3"/>/<see cref="DefaultSteelDensityLbPerIn3"/> when a
/// density field is zero/unset). A metric file's density fields are mass density (kg/m³) and are
/// converted to weight density via <see cref="GravitationalAccelerationMetresPerSecond2"/>; an
/// English file's are already weight density (lbf/in³) and need no such conversion — see
/// QUESTIONS.md's "mm as default" entry for how this was confirmed against
/// <c>#$ UNITS</c>'s CNVPDN constant.</item>
/// </list>
/// </summary>
public static class SpanLimitCalculator
{
    /// <summary>Assumed allowable bending stress (MPa) for a metric file's span formula, when it has no <c>#$ ALLOWBLS</c> record. Not a code value — the metric equivalent of <see cref="DefaultAllowableBendingStressPsi"/>.</summary>
    public const double DefaultAllowableBendingStressMpa = DefaultAllowableBendingStressPsi * MpaPerPsi;

    /// <summary>Assumed allowable bending stress (psi) for an English file's span formula, when it has no <c>#$ ALLOWBLS</c> record. Not a code value.</summary>
    public const double DefaultAllowableBendingStressPsi = 1500.0;

    /// <summary>Fallback pipe density (kg/m³) for a metric file, used when an element's own pipe density is zero/unset. Ordinary carbon-steel density — the metric equivalent of <see cref="DefaultSteelDensityLbPerIn3"/>.</summary>
    public const double DefaultSteelDensityKgPerM3 = DefaultSteelDensityLbPerIn3 * KgPerM3PerLbPerIn3;

    /// <summary>Fallback pipe density (lb/in³) for an English file, used when an element's own pipe density is zero/unset.</summary>
    public const double DefaultSteelDensityLbPerIn3 = 0.2836;

    private const double MillimetresPerInch = 25.4;
    private const double MpaPerPsi = 0.00689476;
    private const double NewtonsPerPoundForce = 4.448222;
    private const double KgPerM3PerLbPerIn3 = 27680.0; // confirmed against #$ UNITS's CNVPDN constant in 3 real samples
    private const double GravitationalAccelerationMetresPerSecond2 = 9.80665;

    /// <summary>Computes max span (mm) assuming <see cref="UnitsSection.Metric"/> — for callers with an <see cref="Element"/> but no <see cref="NeutralFile"/> (e.g. tests).</summary>
    public static double ComputeMaxSpan(Element element) =>
        ComputeMaxSpanMillimetres(element, DefaultAllowableBendingStressMpa, UnitsSection.Metric);

    /// <summary>
    /// Computes max span (always in millimetres) using <paramref name="file"/>'s own
    /// <c>#$ ALLOWBLS</c> cold allowable stress for <paramref name="element"/> when one is
    /// linked, falling back to a default matching the file's own unit system otherwise.
    /// </summary>
    public static double ComputeMaxSpan(NeutralFile file, Element element)
    {
        var units = file.Units;
        var allowable = file.TryGetAllowableStress(element)?.ColdAllowableStress;
        var defaultAllowableBendingStress = units.IsMetric ? DefaultAllowableBendingStressMpa : DefaultAllowableBendingStressPsi;
        var allowableBendingStress = allowable is > 0 ? allowable.Value : defaultAllowableBendingStress;
        return ComputeMaxSpanMillimetres(element, allowableBendingStress, units);
    }

    /// <param name="allowableBendingStress">In the same unit system as <paramref name="units"/> (MPa if metric, psi if English).</param>
    private static double ComputeMaxSpanMillimetres(Element element, double allowableBendingStress, UnitsSection units)
    {
        var sectionModulusMm3 = ComputeSectionModulusMillimetres(element, units);
        var weightPerLengthNewtonsPerMm = ComputeWeightPerLengthNewtonsPerMillimetre(element, units);

        if (weightPerLengthNewtonsPerMm <= 0 || sectionModulusMm3 <= 0)
        {
            return 0;
        }

        var allowableStressMpa = units.IsMetric ? allowableBendingStress : allowableBendingStress * MpaPerPsi;
        return Math.Sqrt(8.0 * allowableStressMpa * sectionModulusMm3 / weightPerLengthNewtonsPerMm);
    }

    /// <summary>Section modulus of the pipe's hollow-cylinder cross-section, <c>Z = π(OD⁴-ID⁴)/(32·OD)</c>, in mm³.</summary>
    private static double ComputeSectionModulusMillimetres(Element element, UnitsSection units)
    {
        var outsideDiameter = element.OutsideDiameter * units.LengthToMillimetres;
        var insideDiameter = outsideDiameter - (2 * element.WallThickness * units.LengthToMillimetres);
        if (outsideDiameter <= 0 || insideDiameter < 0)
        {
            return 0;
        }
        return Math.PI * (Math.Pow(outsideDiameter, 4) - Math.Pow(insideDiameter, 4)) / (32.0 * outsideDiameter);
    }

    private static double ComputeWeightPerLengthNewtonsPerMillimetre(Element element, UnitsSection units)
    {
        var toMm = units.LengthToMillimetres;
        var outsideDiameter = element.OutsideDiameter * toMm;
        var wallThickness = element.WallThickness * toMm;
        var insulationThickness = element.RealValues[7] * toMm;

        var defaultPipeDensity = units.IsMetric ? DefaultSteelDensityKgPerM3 : DefaultSteelDensityLbPerIn3;
        var pipeDensity = element.RealValues[29] is > 0 ? element.RealValues[29] : defaultPipeDensity;
        var insulationDensity = element.RealValues[30];
        var fluidDensity = element.RealValues[31];

        var insideDiameter = Math.Max(outsideDiameter - (2 * wallThickness), 0);
        var insulatedOutsideDiameter = outsideDiameter + (2 * insulationThickness);

        var metalAreaMm2 = CircleArea(outsideDiameter) - CircleArea(insideDiameter);
        var insulationAreaMm2 = Math.Max(CircleArea(insulatedOutsideDiameter) - CircleArea(outsideDiameter), 0);
        var fluidAreaMm2 = CircleArea(insideDiameter);

        return (metalAreaMm2 * WeightDensityNewtonsPerMm3(pipeDensity, units))
             + (insulationAreaMm2 * WeightDensityNewtonsPerMm3(insulationDensity, units))
             + (fluidAreaMm2 * WeightDensityNewtonsPerMm3(fluidDensity, units));
    }

    /// <summary>Converts a native-unit density (kg/m³ mass density if metric, lbf/in³ weight density if English) into N/mm³ weight density.</summary>
    private static double WeightDensityNewtonsPerMm3(double density, UnitsSection units) => units.IsMetric
        ? density * GravitationalAccelerationMetresPerSecond2 / 1_000_000_000.0
        : density * NewtonsPerPoundForce / Math.Pow(MillimetresPerInch, 3);

    private static double CircleArea(double diameter) => Math.PI / 4.0 * diameter * diameter;
}
