namespace Conduit.Core.Heuristics;

/// <summary>
/// Material-specific constants Conduit's heuristics need (allowable stress, elastic modulus,
/// density; thermal expansion coefficient and Poisson's ratio pending real data — see below),
/// keyed by CAESAR II's own numeric material ID (<c>#$ MISCEL_1</c>'s <c>RRMAT</c> array, exposed
/// as <see cref="NeutralFiles.NeutralFile.MaterialIds"/>).
///
/// <para><b>Placeholder, per direct instruction (2026-09-01):</b> "we need... material-specific
/// constants. There is no point in creating an MVP that is only able to handle a single type of
/// material... Set a placeholder for this currently if required." This is that placeholder — the
/// <i>resolution mechanism</i> (look up by the file's own material ID, not always the same hardcoded
/// fallback regardless of what the file says) is real and wired in; the <i>data</i> is still just
/// one material (#107, ASTM A106 Grade B) until more materials' real values are available. Every
/// other material ID currently resolves to this same single entry — no worse than before this
/// existed (that was the *only* behavior previously), but now the architecture is ready to grow.
/// </para>
///
/// <para><b>Material #107 — ASTM A106 Grade B.</b> Read directly from the user's own CAESAR II
/// material database (<c>UMAT1.umd</c>) printout, chosen (over CAESAR's generic material #1, "LOW
/// CARBON") because it's a real, complete material with every field <see cref="SpanLimitCalculator"/>
/// needs populated — see QUESTIONS.md's "Implemented: real A106 Grade B material..." entry for the
/// full derivation. <see cref="ThermalExpansionCoefficientPerDegreeCelsius"/> and
/// <see cref="PoissonsRatio"/> were never extracted from that printout (not needed until now) —
/// left <c>null</c> rather than guessed, since this is safety-relevant engineering data. Per
/// direct instruction, thermal expansion in particular is needed to compute guide/hold-down
/// spacing via expansion stress — see SPEC.md's "Known open decisions" for the current status.
/// </para>
/// </summary>
public sealed record MaterialProperties(
    int MaterialId,
    string Name,
    double AllowableStressMpa,
    double ElasticModulusMpa,
    double DensityKgPerM3,
    double? ThermalExpansionCoefficientPerDegreeCelsius,
    double? PoissonsRatio);

public static class MaterialLibrary
{
    /// <summary>CAESAR II's numeric ID (per the user's own UMAT1.umd printout) for ASTM A106 Grade B, Conduit's only known-complete material so far.</summary>
    public const int A106GradeBMaterialId = 107;

    private static readonly MaterialProperties A106GradeB = new(
        MaterialId: A106GradeBMaterialId,
        Name: "ASTM A106 Grade B",
        AllowableStressMpa: 118.0,
        ElasticModulusMpa: 203_400.0,
        DensityKgPerM3: 7833.4399,
        ThermalExpansionCoefficientPerDegreeCelsius: null, // pending UMAT1 data — see QUESTIONS.md
        PoissonsRatio: null); // pending UMAT1 data — see QUESTIONS.md

    private static readonly Dictionary<int, MaterialProperties> ById = new()
    {
        [A106GradeBMaterialId] = A106GradeB,
    };

    /// <summary>
    /// Resolves <paramref name="materialId"/> to its known properties, falling back to
    /// <see cref="A106GradeBMaterialId"/>'s (the only material Conduit currently has real,
    /// complete data for) when the ID isn't in the library yet — matching the single-material
    /// behavior every caller already had before this library existed, for any material this
    /// doesn't yet recognize.
    /// </summary>
    public static MaterialProperties Resolve(int materialId) =>
        ById.TryGetValue(materialId, out var properties) ? properties : A106GradeB;
}
