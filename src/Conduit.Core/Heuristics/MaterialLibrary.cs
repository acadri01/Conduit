namespace Conduit.Core.Heuristics;

/// <summary>
/// Material-specific constants Conduit's heuristics need (allowable stress, elastic modulus,
/// density, thermal expansion coefficient, Poisson's ratio), keyed by CAESAR II's own numeric
/// material ID (<c>#$ MISCEL_1</c>'s <c>RRMAT</c> array, exposed as
/// <see cref="NeutralFiles.NeutralFile.MaterialIds"/>).
///
/// <para><b>Placeholder, per direct instruction (2026-09-01):</b> "we need... material-specific
/// constants. There is no point in creating an MVP that is only able to handle a single type of
/// material... Set a placeholder for this currently if required." This is that placeholder — the
/// <i>resolution mechanism</i> (look up by the file's own material ID, not always the same hardcoded
/// fallback regardless of what the file says) is real and wired in; the <i>data</i> now covers two
/// materials (both read from the user's real UMAT1.umd printout) and can grow from here.
/// </para>
///
/// <para><b>Correction (2026-09-02): the previous round's material ID was off by one.</b> The user
/// uploaded their real UMAT1.pdf printout this round (1,708 pages, CAESAR II Material Data Base
/// v4.20). Grepping it for material #107 shows <c>NUMBER: 107  NAME: A135 A</c> (ASTM A135, an
/// electric-resistance-welded pipe spec) — not A106 Grade B as every earlier round's docs and code
/// claimed. A106 Grade B is actually material <b>#106</b> (<c>NUMBER: 106  NAME: A106 B</c> /
/// <c>A106 Grade B</c>, confirmed at 7 separate locations across the printout's repeated
/// per-piping-code sections). <see cref="A106GradeBMaterialId"/> is corrected to 106 accordingly;
/// material #107 (A135 Grade A) is now also populated as a real second entry, both from the
/// printout's <c>APPLICABLE PIPING CODE: 0</c> section (the generic, code-independent physical-data
/// listing every material has, before CAESAR repeats the same material once per supported piping
/// code with only its allowable-stress/Poisson entries changed for that code's rules).
/// </para>
///
/// <para><b>Where each field comes from, and why it's split across two sources:</b></para>
/// <list type="bullet">
/// <item><see cref="DensityKgPerM3"/>, <see cref="ElasticModulusMpa"/> (the printout's "COLD
/// MODULUS") and <see cref="ThermalExpansionCoefficientPerDegreeCelsius"/> (the printout's ambient
/// "EXP COEFF", at 21°C) are genuine physical material properties — confirmed identical across
/// every one of the printout's repeated per-code sections for the same material (e.g. A106 Grade
/// B's density and cold modulus are the same in the code-0, code-1, code-3... sections). Sourced
/// from the code-0 (generic) section, since it's the code-independent listing.</item>
/// <item><see cref="PoissonsRatio"/> is likewise a physical constant. The printout's code-0 section
/// shows 0.2920 for both materials; the code-1 section shows 0.3000 instead (why a "material
/// property" shifts slightly by code section isn't documented in the printout and wasn't chased
/// further) — 0.30 is used here since it's carbon steel's conventional engineering value and is
/// what the printout itself uses once a specific piping code is selected.</item>
/// <item><see cref="AllowableStressMpa"/> is deliberately <b>not</b> read from the printout. An
/// "allowable stress" is inherently a design-code limit, not a material property, and the
/// printout's numeric <c>APPLICABLE PIPING CODE</c> IDs (0, 1, 3, 4, 5, 8, 10...) have no legend
/// anywhere in the 1,708-page document tying them to a named code/edition — cross-checking code-1's
/// A106 Grade B allowable (118 MPa flat to 343°C) against <c>reference/B31.3-2024.pdf</c>'s own
/// Table A-1 (Line 33, the line the same table's material-listing page unambiguously assigns to
/// ASTM A106 Grade B) shows a materially different curve (138 MPa flat only to 200°C, declining
/// above), meaning code-1 is some other, unidentified code/edition — not B31.3-2024. Since
/// <see cref="Configuration.CaesarConfig.DefaultAssumedCode"/> is B31.3-2024 (the code Conduit
/// targets by default), <see cref="AllowableStressMpa"/> is read directly from that table instead —
/// authoritative, unambiguous, and already in <c>reference/</c>. This corrects the previous round's
/// value (118 MPa) which had silently inherited whichever unidentified code-1 curve the earlier
/// extraction happened to copy.</item>
/// <item>All values here are the <i>ambient/cold</i> (≤40°C / ≤100°F) end of what are genuinely
/// temperature-dependent curves in both source documents. <see cref="SpanLimitCalculator"/> already
/// prefers the file's own real per-element data (<c>#$ ALLOWBLS</c>'s computed allowable stress,
/// the element's own cold modulus) when present — these are the pure library values only for the
/// case a file has none. Full temperature-curve lookup (rather than a single ambient scalar) is
/// deferred — see QUESTIONS.md.</item>
/// </list>
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
    /// <summary>CAESAR II's numeric ID (per the user's own UMAT1.umd printout, <c>NUMBER: 106  NAME: A106 B</c>) for ASTM A106 Grade B. Corrected from 107 (2026-09-02) — see the class doc comment.</summary>
    public const int A106GradeBMaterialId = 106;

    /// <summary>CAESAR II's numeric ID (per the user's own UMAT1.umd printout, <c>NUMBER: 107  NAME: A135 A</c>) for ASTM A135 Grade A (electric-resistance-welded steel pipe).</summary>
    public const int A135GradeAMaterialId = 107;

    private static readonly MaterialProperties A106GradeB = new(
        MaterialId: A106GradeBMaterialId,
        Name: "ASTM A106 Grade B",
        AllowableStressMpa: 138.0, // B31.3-2024 Table A-1, Line 33, ≤40°C — see class doc comment
        ElasticModulusMpa: 203_400.0, // UMAT1.umd, material #106, COLD MODULUS
        DensityKgPerM3: 7833.4399, // UMAT1.umd, material #106, DENSITY
        ThermalExpansionCoefficientPerDegreeCelsius: 1.0925e-5, // UMAT1.umd, material #106, EXP COEFF at 21°C
        PoissonsRatio: 0.30); // UMAT1.umd, material #106 — see class doc comment

    private static readonly MaterialProperties A135GradeA = new(
        MaterialId: A135GradeAMaterialId,
        Name: "ASTM A135 Grade A",
        AllowableStressMpa: 110.0, // B31.3-2024 Table A-1, Line 12 (confirmed ASTM A135 Grade A via its own material-listing page, min tensile 330 MPa/yield 205 MPa matching), ≤40°C
        ElasticModulusMpa: 203_400.0, // UMAT1.umd, material #107, COLD MODULUS
        DensityKgPerM3: 7833.4399, // UMAT1.umd, material #107, DENSITY
        ThermalExpansionCoefficientPerDegreeCelsius: 1.0925e-5, // UMAT1.umd, material #107, EXP COEFF at 21°C
        PoissonsRatio: 0.30); // UMAT1.umd, material #107 (code-1 section) — see class doc comment

    private static readonly Dictionary<int, MaterialProperties> ById = new()
    {
        [A106GradeBMaterialId] = A106GradeB,
        [A135GradeAMaterialId] = A135GradeA,
    };

    /// <summary>
    /// Resolves <paramref name="materialId"/> to its known properties, falling back to
    /// <see cref="A106GradeBMaterialId"/>'s (the material Conduit has the most-verified data for,
    /// and the one every fixture/test predates this library assuming) when the ID isn't in the
    /// library yet.
    /// </summary>
    public static MaterialProperties Resolve(int materialId) =>
        ById.TryGetValue(materialId, out var properties) ? properties : A106GradeB;
}
