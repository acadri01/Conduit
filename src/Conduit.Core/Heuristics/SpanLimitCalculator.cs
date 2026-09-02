using Conduit.Core.NeutralFiles;

namespace Conduit.Core.Heuristics;

/// <summary>
/// Computes a maximum allowable unsupported span for a pipe element from the same two criteria
/// used by classic pipe-support-spacing references — per direct instruction to use the textbook's
/// own formulae rather than an ad-hoc derivation (see "Pipe Stress Engineering," Ch. 6, Section
/// 6.2, Eqs. 6.1/6.2 — full citation in <c>reference/README.md</c>). Always returns the span in
/// millimetres — Conduit's default unit system (per direct instruction) — converting the
/// element's own data first when its file is in a different unit system.
///
/// <para><b>The two criteria, per the textbook (Section 6.2):</b></para>
/// <list type="bullet">
/// <item><b>Bending-stress criterion, Eq. 6.1</b>: <c>L1 = sqrt(10·Z·S / w)</c> — a "semi-fixed
/// beam" model (accounts for the pipe continuing past each support, unlike a naive isolated
/// simply-supported span, hence the constant 10 rather than the simply-supported beam's 8) where
/// <c>S</c> is the allowable bending stress, <c>Z</c> the section modulus, <c>w</c> the weight per
/// unit length.</item>
/// <item><b>Sag criterion, Eq. 6.2</b>: <c>L2 = (128·E·I·Δ / w)^(1/4)</c> where <c>E</c> is the
/// elastic modulus, <c>I</c> the moment of inertia (<c>= Z·OD/2</c> for a hollow circular
/// section), and <c>Δ</c> the design sag limit — the book cites B31.1's 2.5 mm (0.1 in) for power
/// plants and Kellogg's 12.5-25 mm (0.5-1.0 in) range for process plants. Conduit defaults to
/// B31.3 (process piping), so <see cref="DesignSagLimitMillimetres"/> uses the lower, more
/// conservative end of Kellogg's range (12.5 mm) — a decide-and-proceed pick, not itself sourced
/// from the text, logged in QUESTIONS.md as an assumption open to correction.</item>
/// <item>The allowable span is <c>min(L1, L2)</c>, per the book: "The allowable span, Ls, is
/// therefore taken as the smaller of L1 and L2." If the sag criterion can't be evaluated (no
/// usable elastic modulus), only the bending-stress criterion is used.</item>
/// </list>
///
/// <para><b>Real per-element data vs. fallback constants:</b></para>
/// <list type="bullet">
/// <item>The allowable stress <c>S</c> is the element's own <c>#$ ALLOWBLS</c> cold allowable
/// stress when the file provides one (real, per-material/code/temperature data CAESAR II computed
/// when generating the file — see <see cref="ComputeMaxSpan(NeutralFile, Element)"/>), falling
/// back to <see cref="DefaultAllowableBendingStressMpa"/>/<see cref="DefaultAllowableBendingStressPsi"/>
/// only when the file has no allowable-stress record for that element. This fallback is not an
/// arbitrary placeholder — it's ASTM A106 Grade B's real cold/ambient allowable stress (138 MPa),
/// read from <c>reference/B31.3-2024.pdf</c>'s own Table A-1 (Line 33), the code Conduit targets by
/// default. See <see cref="MaterialLibrary"/>'s class doc comment for why allowable stress is
/// sourced from the B31.3 code table rather than the user's UMAT1.umd printout directly (that
/// printout's numeric "applicable piping code" IDs have no legend tying them to a named
/// code/edition) and for the 2026-09-02 correction of material #107 (previously believed to be
/// A106 Grade B; it's actually A135 Grade A — A106 Grade B is material #106).
/// </item>
/// <item>Distributed weight <c>w</c> includes pipe metal, insulation, and a fully-liquid-filled
/// bore, computed from the element's own density fields (falling back to
/// <see cref="DefaultSteelDensityKgPerM3"/>/<see cref="DefaultSteelDensityLbPerIn3"/> — also
/// A106 Grade B's real density — when a density field is zero/unset). A metric file's density
/// fields are mass density (kg/m³) and are converted to weight density via
/// <see cref="GravitationalAccelerationMetresPerSecond2"/>; an English file's are already weight
/// density (lbf/in³) and need no such conversion — see QUESTIONS.md's "mm as default" entry for
/// how this was confirmed against <c>#$ UNITS</c>'s CNVPDN constant.</item>
/// <item>The elastic modulus <c>E</c> is the element's own cold modulus (<c>RealValues[27]</c>)
/// when populated, falling back to <see cref="DefaultElasticModulusMpa"/>/
/// <see cref="DefaultElasticModulusPsi"/> — again A106 Grade B's real cold modulus (203,400 MPa),
/// not an arbitrary constant.</item>
/// <item>Every fallback (allowable stress, elastic modulus, density) is resolved through
/// <see cref="MaterialLibrary"/>, keyed by the element's own material ID (<c>#$ MISCEL_1</c>'s
/// <c>RRMAT</c> array) — not always the same hardcoded A106 Grade B values regardless of what the
/// file specifies. <see cref="MaterialLibrary"/> now covers all 399 real materials in the user's
/// UMAT1 printout (per direct instruction, 2026-09-02: "I would like to have all the materials in
/// the database"), though only two (#106, #107) have a real, verified allowable stress — see
/// <see cref="MaterialLibrary"/>'s class doc comment. A material whose own allowable stress or
/// elastic modulus isn't yet known falls back further, to material #106's real values, inside
/// <see cref="ComputeMaxSpan(NeutralFile, Element)"/>. This constants block
/// (<see cref="DefaultAllowableBendingStressMpa"/> etc.) mirrors <see cref="MaterialLibrary"/>'s
/// A106 Grade B entry and exists for backward-compatible direct access;
/// <see cref="MaterialLibrary"/> is the source of truth going forward.</item>
/// </list>
/// </summary>
public static class SpanLimitCalculator
{
    /// <summary>
    /// A106 Grade B's real cold/ambient allowable stress (MPa), read from
    /// <c>reference/B31.3-2024.pdf</c>'s Table A-1, Line 33 — used as the fallback allowable
    /// stress for a metric file with no <c>#$ ALLOWBLS</c> record for an element. The metric
    /// equivalent of <see cref="DefaultAllowableBendingStressPsi"/>. See
    /// <see cref="MaterialLibrary"/>'s class doc comment for why this is a code-table value
    /// rather than the UMAT1.umd printout's own (ambiguous) allowable-stress data.
    /// </summary>
    public const double DefaultAllowableBendingStressMpa = 138.0;

    /// <summary>A106 Grade B's real cold/ambient allowable stress (psi), for an English file's span formula, when it has no <c>#$ ALLOWBLS</c> record.</summary>
    public const double DefaultAllowableBendingStressPsi = DefaultAllowableBendingStressMpa / MpaPerPsi;

    /// <summary>
    /// A106 Grade B's real density (kg/m³), read directly from the user's own CAESAR II material
    /// database — used as the fallback pipe density for a metric file when an element's own pipe
    /// density is zero/unset. The metric equivalent of <see cref="DefaultSteelDensityLbPerIn3"/>.
    /// </summary>
    public const double DefaultSteelDensityKgPerM3 = 7833.4399;

    /// <summary>Fallback pipe density (lb/in³) for an English file, used when an element's own pipe density is zero/unset.</summary>
    public const double DefaultSteelDensityLbPerIn3 = DefaultSteelDensityKgPerM3 / KgPerM3PerLbPerIn3;

    /// <summary>
    /// A106 Grade B's real cold elastic modulus (MPa), read directly from the user's own CAESAR II
    /// material database — used as the fallback elastic modulus (for the sag criterion, Eq. 6.2)
    /// when an element's own cold modulus (<c>RealValues[27]</c>) is zero/unset.
    /// </summary>
    public const double DefaultElasticModulusMpa = 203_400.0;

    /// <summary>Fallback elastic modulus (psi) for an English file, used when an element's own cold modulus is zero/unset.</summary>
    public const double DefaultElasticModulusPsi = DefaultElasticModulusMpa / MpaPerPsi;

    /// <summary>
    /// Design sag limit (mm) for Eq. 6.2's sag criterion — Kellogg's suggested range for process
    /// plants is 12.5-25 mm (0.5-1.0 in); Conduit defaults to B31.3 (process piping), so this uses
    /// the lower, more conservative end. Not itself sourced from the text (the text gives a range,
    /// not a single value) — a decide-and-proceed pick, logged in QUESTIONS.md.
    /// </summary>
    public const double DesignSagLimitMillimetres = 12.5;

    /// <summary>The bending-stress criterion's "semi-fixed beam" constant, Eq. 6.1 — accounts for the pipe continuing past each support, unlike a naive isolated simply-supported span (whose constant would be 8).</summary>
    private const double SemiFixedBeamSpanConstant = 10.0;

    private const double MillimetresPerInch = 25.4;
    private const double MpaPerPsi = 0.00689476;
    private const double NewtonsPerPoundForce = 4.448222;
    private const double KgPerM3PerLbPerIn3 = 27680.0; // confirmed against #$ UNITS's CNVPDN constant in 3 real samples
    private const double GravitationalAccelerationMetresPerSecond2 = 9.80665;

    /// <summary>Computes max span (mm) assuming <see cref="UnitsSection.Metric"/> — for callers with an <see cref="Element"/> but no <see cref="NeutralFile"/> (e.g. tests).</summary>
    public static double ComputeMaxSpan(Element element) =>
        ComputeMaxSpanMillimetres(element, DefaultAllowableBendingStressMpa, DefaultElasticModulusMpa, UnitsSection.Metric, MaterialLibrary.Resolve(MaterialLibrary.A106GradeBMaterialId));

    /// <summary>
    /// Computes max span (always in millimetres) using <paramref name="file"/>'s own
    /// <c>#$ ALLOWBLS</c> cold allowable stress and the element's own cold elastic modulus for
    /// <paramref name="element"/> when populated, falling back to <paramref name="element"/>'s own
    /// resolved material (<see cref="MaterialLibrary.Resolve"/>, via <c>#$ MISCEL_1</c>'s
    /// <c>RRMAT</c> material ID) otherwise — not always the same hardcoded material regardless of
    /// what the file actually specifies. <see cref="MaterialLibrary"/> now covers all 399 real
    /// materials in the user's UMAT1 printout, though only two (#106, #107) have a real, verified
    /// <see cref="MaterialProperties.AllowableStressMpa"/> (an inherently code-specific value, not
    /// safely extractable for the other 397 — see <see cref="MaterialLibrary"/>'s class doc
    /// comment); any material with a <c>null</c> allowable stress or elastic modulus (materials
    /// #9/#12 — a genuine data-quality issue in the source printout, also documented there) falls
    /// back further, to material #106's own real values, rather than leaving the span uncomputable.
    /// </summary>
    public static double ComputeMaxSpan(NeutralFile file, Element element)
    {
        var units = file.Units;
        var elementIndex = file.Elements.IndexOf(element);
        var materialId = elementIndex >= 0 && elementIndex < file.MaterialIds.Count ? file.MaterialIds[elementIndex] : MaterialLibrary.A106GradeBMaterialId;
        var material = MaterialLibrary.Resolve(materialId);
        var fallbackMaterial = MaterialLibrary.Resolve(MaterialLibrary.A106GradeBMaterialId);

        var allowable = file.TryGetAllowableStress(element)?.ColdAllowableStress;
        var materialAllowableStressMpa = material.AllowableStressMpa ?? fallbackMaterial.AllowableStressMpa!.Value;
        var defaultAllowableBendingStress = units.IsMetric ? materialAllowableStressMpa : materialAllowableStressMpa / MpaPerPsi;
        var allowableBendingStress = allowable is > 0 ? allowable.Value : defaultAllowableBendingStress;
        var materialElasticModulusMpa = material.ElasticModulusMpa ?? fallbackMaterial.ElasticModulusMpa!.Value;
        var defaultElasticModulus = units.IsMetric ? materialElasticModulusMpa : materialElasticModulusMpa / MpaPerPsi;
        var elasticModulus = element.RealValues[27] is > 0 ? element.RealValues[27] : defaultElasticModulus;
        return ComputeMaxSpanMillimetres(element, allowableBendingStress, elasticModulus, units, material);
    }

    /// <param name="allowableBendingStress">In the same unit system as <paramref name="units"/> (MPa if metric, psi if English).</param>
    /// <param name="elasticModulus">In the same unit system as <paramref name="units"/> (MPa if metric, psi if English).</param>
    /// <param name="material">The element's resolved material — only its density fallback is used here; allowable stress/elastic modulus are already resolved by the caller.</param>
    private static double ComputeMaxSpanMillimetres(Element element, double allowableBendingStress, double elasticModulus, UnitsSection units, MaterialProperties material)
    {
        var sectionModulusMm3 = ComputeSectionModulusMillimetres(element, units);
        var weightPerLengthNewtonsPerMm = ComputeWeightPerLengthNewtonsPerMillimetre(element, units, material);

        if (weightPerLengthNewtonsPerMm <= 0 || sectionModulusMm3 <= 0)
        {
            return 0;
        }

        var allowableStressMpa = units.IsMetric ? allowableBendingStress : allowableBendingStress * MpaPerPsi;
        var bendingStressSpan = Math.Sqrt(SemiFixedBeamSpanConstant * allowableStressMpa * sectionModulusMm3 / weightPerLengthNewtonsPerMm);

        var elasticModulusMpa = units.IsMetric ? elasticModulus : elasticModulus * MpaPerPsi;
        if (elasticModulusMpa <= 0)
        {
            return bendingStressSpan; // Eq. 6.2 unusable with no elastic modulus — bending-stress criterion only.
        }

        var outsideDiameterMm = element.OutsideDiameter * units.LengthToMillimetres;
        var momentOfInertiaMm4 = sectionModulusMm3 * outsideDiameterMm / 2.0; // I = Z·OD/2 for a hollow circular section
        var sagSpan = Math.Pow(128.0 * elasticModulusMpa * momentOfInertiaMm4 * DesignSagLimitMillimetres / weightPerLengthNewtonsPerMm, 0.25);

        return Math.Min(bendingStressSpan, sagSpan); // Eq. 6.2's text: "the smaller of L1 and L2"
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

    private static double ComputeWeightPerLengthNewtonsPerMillimetre(Element element, UnitsSection units, MaterialProperties material)
    {
        var toMm = units.LengthToMillimetres;
        var outsideDiameter = element.OutsideDiameter * toMm;
        var wallThickness = element.WallThickness * toMm;
        var insulationThickness = element.RealValues[7] * toMm;

        var defaultPipeDensity = units.IsMetric ? material.DensityKgPerM3 : material.DensityKgPerM3 / KgPerM3PerLbPerIn3;
        var pipeDensity = element.RealValues[29] is > 0 ? element.RealValues[29] : defaultPipeDensity;
        // Clamped to zero rather than trusted as-is: CAESAR uses -1.01 as its own "field not
        // populated" sentinel throughout its data (confirmed by direct user instruction, and seen
        // in the UMAT1 printout's COLD MODULUS field for materials #9/#12 — see MaterialLibrary's
        // class doc comment). Unlike pipe density/elastic modulus above, zero is itself a
        // legitimate real value here (no insulation; an empty/gas-filled bore), so the fallback
        // isn't "substitute a material default" but "treat the sentinel as no contribution."
        var insulationDensity = Math.Max(element.RealValues[30], 0);
        var fluidDensity = Math.Max(element.RealValues[31], 0);

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
