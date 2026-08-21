using Conduit.Core.NeutralFiles;

namespace Conduit.Core.Heuristics;

/// <summary>
/// Computes a maximum allowable unsupported span for a pipe element from simple beam theory,
/// as a stand-in for a real B31.3 span/sag calculation.
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
/// <see cref="DefaultAllowableBendingStress"/> only when the file has no allowable-stress record
/// for that element (e.g. a fixture that doesn't populate <c>#$ ALLOWBLS</c>). Note this still
/// isn't a code-compliant span calculation even when a real allowable is available — the beam
/// formula above is still a simplification, just fed a materially better input than a guess.</item>
/// <item>Distributed weight <c>w</c> includes pipe metal, insulation, and a fully-liquid-filled
/// bore, computed from the element's own density fields (falling back to
/// <see cref="DefaultSteelDensity"/> when a density field is zero/unset). All neutral-file
/// dimensions and densities are assumed to be in a single consistent unit system (v1 doesn't
/// parse <c>#$ UNITS</c>) — the result's length unit matches the input's diameter/thickness unit.</item>
/// </list>
/// </summary>
public static class SpanLimitCalculator
{
    /// <summary>Assumed allowable bending stress (psi, if the file is in English units) for the span formula. Not a code value.</summary>
    public const double DefaultAllowableBendingStress = 1500.0;

    /// <summary>Fallback density (lb/in³, if the file is in English units) used when an element's own pipe density is zero/unset.</summary>
    public const double DefaultSteelDensity = 0.2836;

    public static double ComputeMaxSpan(Element element) =>
        ComputeMaxSpan(element, DefaultAllowableBendingStress);

    /// <summary>
    /// Computes max span using <paramref name="file"/>'s own <c>#$ ALLOWBLS</c> cold allowable
    /// stress for <paramref name="element"/> when one is linked, falling back to
    /// <see cref="DefaultAllowableBendingStress"/> otherwise.
    /// </summary>
    public static double ComputeMaxSpan(NeutralFile file, Element element)
    {
        var allowable = file.TryGetAllowableStress(element)?.ColdAllowableStress;
        var allowableBendingStress = allowable is > 0 ? allowable.Value : DefaultAllowableBendingStress;
        return ComputeMaxSpan(element, allowableBendingStress);
    }

    public static double ComputeMaxSpan(Element element, double allowableBendingStress)
    {
        var sectionModulus = ComputeSectionModulus(element);
        var weightPerLength = ComputeWeightPerLength(element);

        if (weightPerLength <= 0 || sectionModulus <= 0)
        {
            return 0;
        }

        return Math.Sqrt(8.0 * allowableBendingStress * sectionModulus / weightPerLength);
    }

    /// <summary>Section modulus of the pipe's hollow-cylinder cross-section, <c>Z = π(OD⁴-ID⁴)/(32·OD)</c>.</summary>
    private static double ComputeSectionModulus(Element element)
    {
        var outsideDiameter = element.OutsideDiameter;
        var insideDiameter = outsideDiameter - (2 * element.WallThickness);
        if (outsideDiameter <= 0 || insideDiameter < 0)
        {
            return 0;
        }
        return Math.PI * (Math.Pow(outsideDiameter, 4) - Math.Pow(insideDiameter, 4)) / (32.0 * outsideDiameter);
    }

    private static double ComputeWeightPerLength(Element element)
    {
        var outsideDiameter = element.OutsideDiameter;
        var insulationThickness = element.RealValues[7];
        var pipeDensity = element.RealValues[29] is > 0 ? element.RealValues[29] : DefaultSteelDensity;
        var insulationDensity = element.RealValues[30];
        var fluidDensity = element.RealValues[31];

        var insideDiameter = Math.Max(outsideDiameter - (2 * element.WallThickness), 0);
        var insulatedOutsideDiameter = outsideDiameter + (2 * insulationThickness);

        var metalArea = CircleArea(outsideDiameter) - CircleArea(insideDiameter);
        var insulationArea = Math.Max(CircleArea(insulatedOutsideDiameter) - CircleArea(outsideDiameter), 0);
        var fluidArea = CircleArea(insideDiameter);

        return (metalArea * pipeDensity) + (insulationArea * insulationDensity) + (fluidArea * fluidDensity);
    }

    private static double CircleArea(double diameter) => Math.PI / 4.0 * diameter * diameter;
}
