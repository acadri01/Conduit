namespace Conduit.Core.Heuristics;

/// <summary>
/// Material-specific constants Conduit's heuristics need (allowable stress, elastic modulus,
/// density, thermal expansion coefficient, Poisson's ratio), keyed by CAESAR II's own numeric
/// material ID (<c>#$ MISCEL_1</c>'s <c>RRMAT</c> array, exposed as
/// <see cref="NeutralFiles.NeutralFile.MaterialIds"/>).
///
/// <para><b>Per direct instruction (2026-09-02): "I would like to have all the materials in the
/// database."</b> This now covers all <b>399</b> materials in the user's real UMAT1.umd printout
/// (<c>reference/pipe-stress-engineering/UMAT1-material-database.pdf</c> — already committed,
/// cleared for commitment by the user, 2026-08-28; confirmed byte-identical to their fresh
/// 2026-09-02 upload, so this was never a missing-source problem, only an extraction one), not
/// just the two materials (#106, #107) hand-verified in the previous round.
/// </para>
///
/// <para><b>How this was built.</b> Extracted programmatically (not retyped by hand — 399 entries
/// is far past the point where manual transcription is reliable) from the printout's
/// <c>APPLICABLE PIPING CODE: 0</c> section, which every material has exactly once and which is
/// the code-independent physical-property listing (confirmed identical to every other piping-code
/// section for the same material, for every field that repeats — density, modulus). Extraction
/// verified multiple ways before being trusted:
/// <list type="bullet">
/// <item>Cross-checked against the two already hand-verified entries (#106 A106 Grade B, #107
/// A135 Grade A) — density/modulus/thermal-expansion values matched exactly.</item>
/// <item>Sanity-range-checked every one of the 399 extracted density/modulus/Poisson's-ratio/
/// thermal-expansion values (e.g. density 500-25,000 kg/m³, Poisson's ratio 0-0.6) — this caught
/// two real bugs during development (see below), both fixed before this data was trusted.</item>
/// <item>Spot-checked physically-implausible-looking entries individually against the raw
/// printout text (e.g. material #14 "ALUMINUM": 71,020 MPa modulus, 2,804 kg/m³ density — both
/// match real aluminum's known properties, confirming the extraction pipeline generalizes
/// correctly beyond the two carbon-steel entries it was built and tuned against).</item>
/// </list>
/// </para>
///
/// <para><b>Two materials hit CAESAR's own "field not populated" sentinel</b> (confirmed by direct
/// instruction, 2026-09-02: "the -1.01 in Caesar is the Caesar null value. This is always the case
/// for the neutral file" — i.e. this isn't specific to UMAT1 or to elastic modulus; -1.01 is
/// CAESAR's general convention for an unset numeric field, wherever it appears). Materials
/// <b>#9 (WROUGHT IRON)</b> and <b>#12 (K-MONEL)</b> both list <c>COLD MODULUS MPa: -0.1010E+01</c>
/// (-1.01 MPa, verified against the raw printout text) instead of a real cold modulus.
/// <see cref="MaterialProperties.ElasticModulusMpa"/> is <c>null</c> for these two rather than that
/// sentinel value. Checked whether the same literal sentinel appears anywhere in the four real
/// <c>.cii</c> neutral-file samples' own <c>#$ ELEMENTS</c> real-value fields (pipe density,
/// elastic modulus, Poisson's ratio, insulation/fluid density) — it doesn't; those files use
/// <c>0.0</c> for "unset" instead, which <see cref="SpanLimitCalculator"/>'s existing per-element
/// fallback checks already handle correctly. <see cref="SpanLimitCalculator"/>'s insulation/fluid
/// density handling was still hardened to clamp any negative value (the sentinel or otherwise) to
/// zero rather than trust it, since the general convention could appear in a file this codebase
/// hasn't seen yet, and trusting a negative density there would silently understate the computed
/// weight per length — the unsafe direction for a support-spacing calculation.</para>
///
/// <para><b>Allowable stress (2026-09-03): sourced from UMAT1's own B31.3 code section, not the
/// generic code-0 physical listing.</b> Allowable stress is inherently a design-code limit, not a
/// material physical property, so unlike density/modulus/expansion it varies by which piping code
/// is selected — and the printout tabulates every material once per an internal numeric
/// <c>APPLICABLE PIPING CODE</c> ID (0, 1, 3, 4, 5, 8, 10, 33, 50, 63...) with no legend anywhere
/// in its 1,708 pages naming those codes. Rather than parse the B31.3-2024 PDF's own ~110-page
/// Table A-1 (attempted first, then abandoned — its geometry-based listing↔stress-page join gave a
/// wrong answer for one of the two verification anchors; see QUESTIONS.md), the code section itself
/// was <i>identified</i>: for each numeric code, the ambient allowable of the two independently
/// hand-verified anchors (#106 A106 Grade B = 138 MPa, #107 A135 Grade A = 110 MPa, both confirmed
/// against <c>reference/B31.3-2024.pdf</c> Table A-1) was read; exactly three code sections match
/// both anchors (3, 50, 63 — plausibly consecutive B31.3 editions), they never disagree where they
/// overlap, and code <b>3</b> is the widest (codes 50/63 are strict subsets of it). Code 3 was then
/// cross-checked against the B31.3-2024 PDF at seven <i>further</i> materials by name (A53 A/B,
/// A106 A, A135 A/B, A333 1/6) — all seven matched — for nine independent confirmations total. So
/// allowable stress now comes from a single, internally-consistent, nine-point-verified CAESAR code
/// section rather than a fragile PDF-table parse. This covers <b>200</b> of the 399 materials — the
/// carbon/low-alloy/stainless ASTM materials B31.3 actually lists. The remaining ~199 (EN/DIN/JIS
/// specs like <c>1.4301</c>/<c>STPG370</c>, plus a handful of ASTM duplicate entries CAESAR tabulates
/// only under other codes, e.g. #153 A312 304 whose canonical twin #155 A312 TP304 <i>is</i> covered)
/// are genuinely absent from B31.3's tables, so their <see cref="MaterialProperties.AllowableStressMpa"/>
/// stays <c>null</c>; <see cref="SpanLimitCalculator"/> falls back to material #106's real value for
/// those — see its class doc comment.
/// </para>
///
/// <para><b>Correction (2026-09-02, superseding the previous round's choice): Poisson's ratio for
/// #106/#107 now uses the same code-0 (generic) source as every other material</b> (0.292,
/// not the 0.30 the immediately preceding round used from an unidentified per-code section) — for
/// consistency across the whole library now that it covers all materials, not just these two.
/// Both are real values from the same document; the difference is immaterial for beam-theory
/// support-spacing purposes.</para>
///
/// <para><b>Material names are verbatim from the printout</b> (e.g. <c>"A106 B"</c>, not "ASTM
/// A106 Grade B") — expanding all 399 to fully-qualified ASTM/EN/DIN names would need per-material
/// judgment calls not safely automatable at this scale, so it wasn't attempted. A human-readable
/// name lookup/expansion is a possible future enhancement, not part of this round.</para>
/// </summary>
public sealed record MaterialProperties(
    int MaterialId,
    string Name,
    double? AllowableStressMpa,
    double? ElasticModulusMpa,
    double DensityKgPerM3,
    double? ThermalExpansionCoefficientPerDegreeCelsius,
    double? PoissonsRatio);

public static class MaterialLibrary
{
    /// <summary>CAESAR II's numeric ID (per the user's own UMAT1.umd printout, <c>NUMBER: 106  NAME: A106 B</c>) for ASTM A106 Grade B — a fully hand-verified material (one of the two anchors used to identify the B31.3 code section), and <see cref="Resolve"/>'s fallback material for an unrecognized ID or a null allowable/modulus field.</summary>
    public const int A106GradeBMaterialId = 106;

    /// <summary>CAESAR II's numeric ID (per the user's own UMAT1.umd printout, <c>NUMBER: 107  NAME: A135 A</c>) for ASTM A135 Grade A (electric-resistance-welded steel pipe).</summary>
    public const int A135GradeAMaterialId = 107;

    private static readonly IReadOnlyDictionary<int, MaterialProperties> ById = BuildLibrary();

    /// <summary>
    /// Resolves <paramref name="materialId"/> to its known properties, falling back to
    /// <see cref="A106GradeBMaterialId"/>'s (a fully hand-verified material) when the ID isn't in
    /// the library at all. A recognized material with its
    /// own entry but a <c>null</c> field (allowable stress for the ~199 non-B31.3 materials; elastic
    /// modulus for materials #9/#12 — see the class doc comment) is returned as-is with that field
    /// <c>null</c> — callers needing a usable fallback for a specific <c>null</c> field do that
    /// resolution themselves (see <see cref="SpanLimitCalculator"/>), rather than this method
    /// silently substituting a different material's data into an otherwise-real record.
    /// </summary>
    public static MaterialProperties Resolve(int materialId) =>
        ById.TryGetValue(materialId, out var properties) ? properties : ById[A106GradeBMaterialId];

    /// <summary>
    /// Every material actually in the library (the real UMAT1 entries, not the resolve-fallback).
    /// Exposed so a caller — or a regression test — can inspect the whole set; used to assert no
    /// entry ever holds CAESAR's <c>-1.01</c> "field not populated" sentinel or another negative
    /// value as if it were real data (the sentinel is represented as <c>null</c> instead — see the
    /// class doc comment).
    /// </summary>
    public static IReadOnlyCollection<MaterialProperties> AllMaterials => (IReadOnlyCollection<MaterialProperties>)ById.Values;

    /// <summary>
    /// Builds the full 399-material library. Generated from the user's real UMAT1.umd printout
    /// (extraction methodology in the class doc comment) — every <c>Add</c> call below carries one
    /// material's physical properties from its code-0 (generic, code-independent) record and its
    /// allowable stress from CAESAR's B31.3 code section (code 3), in CAESAR's own material-number
    /// order. Allowable stress is a real B31.3 value for the 200 materials B31.3 lists and
    /// <c>null</c> for the ~199 it doesn't (EN/DIN/JIS specs and CAESAR ASTM duplicates); elastic
    /// modulus is <c>null</c> for materials #9/#12 (an invalid sentinel value in the source printout
    /// itself, not a missing extraction).
    /// </summary>
    private static Dictionary<int, MaterialProperties> BuildLibrary()
    {
        var byId = new Dictionary<int, MaterialProperties>();

        void Add(
            int id,
            string name,
            double? allowableStressMpa,
            double? elasticModulusMpa,
            double densityKgPerM3,
            double? thermalExpansionCoefficientPerDegreeCelsius,
            double? poissonsRatio) =>
            byId[id] = new MaterialProperties(id, name, allowableStressMpa, elasticModulusMpa, densityKgPerM3, thermalExpansionCoefficientPerDegreeCelsius, poissonsRatio);

        Add(1, "LOW CARBON", null, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(2, "HIGH CARBON", null, 202000.0, 7752.8911, 1.0925e-05, 0.289);
        Add(3, "CARBON MOLY", null, 201300.0, 8009.208, 1.0925e-05, 0.289);
        Add(4, "LOW CHROME MOLY", null, 204800.0, 8009.208, 1.0925e-05, 0.289);
        Add(5, "MED CHROME MOLY", null, 213100.0, 8009.208, 1.0313e-05, 0.289);
        Add(6, "AUSTENITIC STNL", null, 195100.0, 8007.8237, 1.6397e-05, 0.292);
        Add(7, "STRGHT CHROMIUM", null, 201300.0, 7753.1675, 9.4312e-06, 0.305);
        Add(8, "310 STAINLESS", null, 195100.0, 8024.4321, 1.3463e-05, 0.305);
        Add(9, "WROUGHT IRON", null, null, 7769.7759, 1.2545e-05, 0.3);
        Add(10, "GREY CAST IRON", null, 92390.0, 7080.5444, 9.7012e-06, 0.211);
        Add(11, "MONEL 67Ni 30Cu", null, 179300.0, 8821.6152, 1.3463e-05, 0.315);
        Add(12, "K-MONEL", null, null, 8472.8486, 1.2815e-05, 0.315);
        Add(13, "COPPER-NICKEL", null, 151700.0, 9369.6797, 1.4687e-05, 0.33);
        Add(14, "ALUMINUM", null, 71020.0, 2803.9841, 2.2048e-05, 0.33);
        Add(15, "COPPER (99.8%)", null, 110300.0, 8937.8721, 1.6775e-05, 0.355);
        Add(16, "COMMERCIAL BRAS", null, 117200.0, 8472.8486, 1.6811e-05, 0.331);
        Add(17, "LEAD TIN BRONZE", null, 96530.0, 8827.1514, 1.7225e-05, 0.33);
        Add(101, "A53 A", 110.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(102, "A53 B", 138.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(103, "A105", 161.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(104, "A106 A", 110.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(105, "A106 C", 161.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(106, "A106 B", 138.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(107, "A135 A", 110.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(108, "A135 B", 138.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(109, "A181 60", 138.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(110, "A181 70", 161.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(111, "A182 F310", 138.0, 195100.0, 8027.1997, 1.5821e-05, 0.292);
        Add(112, "A182 F1", 161.0, 201300.0, 7833.4399, 1.0925e-05, 0.292);
        Add(113, "A182 F11", null, 204800.0, 7833.4399, 1.0925e-05, 0.292);
        Add(114, "A182 F11 CL1", 138.0, 204800.0, 7833.4399, 1.0925e-05, 0.292);
        Add(115, "A182 F11 CL2", 161.0, 204800.0, 7833.4399, 1.0925e-05, 0.292);
        Add(116, "A182 F12", null, 204800.0, 7833.4399, 1.0925e-05, 0.292);
        Add(117, "A182 F12 CL1", 138.0, 204800.0, 7833.4399, 1.0925e-05, 0.292);
        Add(118, "A182 F12 CL2", 161.0, 204800.0, 7833.4399, 1.0925e-05, 0.292);
        Add(119, "A182 F2", 161.0, 204800.0, 7833.4399, 1.0925e-05, 0.292);
        Add(120, "A182 F21", 172.0, 211000.0, 7833.4399, 1.0925e-05, 0.292);
        Add(121, "A182 F22", null, 211000.0, 7833.4399, 1.0925e-05, 0.292);
        Add(122, "A182 F22 CL1", 138.0, 211000.0, 7833.4399, 1.0925e-05, 0.292);
        Add(123, "A182 F22 CL3", 172.0, 211000.0, 7833.4399, 1.0925e-05, 0.292);
        Add(124, "A182 F304", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(125, "A182 F304H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(126, "A182 F304L", 115.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(127, "A182 F304N", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(128, "A-182 F310", null, 195100.0, 8027.1997, 1.5659e-05, 0.292);
        Add(129, "A182 F316", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(130, "A182 F316H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(131, "A182 F316L", 115.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(132, "A182 F316N", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(133, "A182 F321", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(134, "A182 F321H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(135, "A182 F347", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(136, "A182 F347H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(137, "A182 F348", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(138, "A182 F348H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(139, "A182 F5", 161.0, 213100.0, 7695.04, 1.0313e-05, 0.292);
        Add(140, "A182 F5A", 207.0, 213100.0, 7695.04, 1.0313e-05, 0.292);
        Add(142, "A182 F9", 195.0, 213100.0, 7639.6797, 1.0313e-05, 0.292);
        Add(143, "A182 F91", null, 213100.0, 7639.6797, 1.0313e-05, 0.292);
        Add(144, "A268 TP405", 138.0, 201300.0, 7750.3999, 9.4312e-06, 0.292);
        Add(145, "A268 TP410", 138.0, 201300.0, 7750.3999, 9.4312e-06, 0.292);
        Add(146, "A268 TP429", null, 201300.0, 7833.4399, 9.4312e-06, 0.292);
        Add(147, "A268 TP430", 138.0, 201300.0, 7750.3999, 9.4312e-06, 0.292);
        Add(148, "A268 TP446", 161.0, 201300.0, 7473.6001, 9.4312e-06, 0.292);
        Add(149, "A268 TP446-1", null, 201300.0, 7473.6001, 9.4312e-06, 0.292);
        Add(150, "A285 A", 103.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(151, "A285 B", 115.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(152, "A285 C", 126.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(153, "A312 304", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(154, "A312 304L", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(155, "A312 TP304", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(156, "A312 TP304H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(157, "A312 TP304L", 115.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(158, "A312 TP304N", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(159, "A312 TP309", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(160, "A312 TP309S", null, 195100.0, 8027.1997, 1.5659e-05, 0.292);
        Add(161, "A312 TP310", 138.0, 195100.0, 8027.1997, 1.5821e-05, 0.292);
        Add(162, "A312 TP310S", null, 195100.0, 8027.1997, 1.5659e-05, 0.292);
        Add(163, "A312 TP316", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(164, "A312 TP316H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(165, "A312 TP316L", 115.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(166, "A312 TP316N", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(167, "A312 TP317", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(168, "A312 TP321", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(169, "A312 TP321H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(170, "A312 TP347", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(171, "A312 TP347H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(172, "A312 TP348", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(173, "A312 TP348H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(174, "A333 1", 126.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(175, "A333 3", 150.0, 191700.0, 7750.3999, 1.1249e-05, 0.291);
        Add(176, "A333 4", 138.0, 191700.0, 7750.3999, 1.1249e-05, 0.291);
        Add(177, "A333 6", 138.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(178, "A333 7", 150.0, 191700.0, 7750.3999, 1.1249e-05, 0.291);
        Add(179, "A333 9", 145.0, 191700.0, 7750.3999, 1.1249e-05, 0.291);
        Add(180, "A335 P1", 126.0, 201300.0, 7833.4399, 1.0925e-05, 0.292);
        Add(181, "A335 P11", 138.0, 204800.0, 7833.4399, 1.0925e-05, 0.292);
        Add(182, "A335 P12", 138.0, 204800.0, 7833.4399, 1.0925e-05, 0.292);
        Add(183, "A335 P2", 126.0, 204800.0, 7833.4399, 1.0925e-05, 0.292);
        Add(184, "A335 P21", 138.0, 211000.0, 7833.4399, 1.0925e-05, 0.292);
        Add(185, "A335 P22", 138.0, 211000.0, 7833.4399, 1.0925e-05, 0.292);
        Add(186, "A335 P5", 138.0, 213100.0, 7695.04, 1.0313e-05, 0.292);
        Add(187, "A335 P5B", 138.0, 213100.0, 7695.04, 1.0313e-05, 0.292);
        Add(188, "A335 P5C", 138.0, 213100.0, 7695.04, 1.0313e-05, 0.292);
        Add(190, "A335 P9", 138.0, 213100.0, 7639.6797, 1.0313e-05, 0.292);
        Add(191, "A335 P91", 195.0, 213100.0, 7850.02, 1.0313e-05, 0.292);
        Add(192, "A358 304", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(193, "A358 304 CL1", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(194, "A358 304 CL2", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(195, "A358 304 CL3", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(196, "A358 304L", 115.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(197, "A358 304L CL1", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(198, "A358 304L CL2", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(199, "A358 304L CL3", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(200, "A-358 304N", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(201, "A358 304N CL1", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(202, "A358 304N CL2", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(203, "A358 304N CL3", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(204, "A358 309 CL1", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(205, "A358 309 CL2", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(206, "A358 309 CL3", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(207, "A358 309S", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(208, "A-358 310S", null, 195100.0, 8027.1997, 1.5659e-05, 0.292);
        Add(209, "A358 310 CL1", null, 195100.0, 8027.1997, 1.3463e-05, 0.292);
        Add(210, "A358 310 CL2", null, 195100.0, 8027.1997, 1.3463e-05, 0.292);
        Add(211, "A358 310 CL3", null, 195100.0, 8027.1997, 1.3463e-05, 0.292);
        Add(212, "A358 310S", 138.0, 195100.0, 8027.1997, 1.5821e-05, 0.292);
        Add(213, "A358 316", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(214, "A358 316 CL1", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(215, "A358 316 CL2", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(216, "A358 316 CL3", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(217, "A358 316L", 115.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(218, "A358 316L CL1", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(219, "A358 316L CL2", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(220, "A358 316L CL3", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(221, "A358 316N", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(222, "A358 316N CL1", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(223, "A358 316N CL2", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(224, "A358 316N CL3", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(225, "A358 321", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(226, "A358 321 CL1", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(227, "A358 321 CL2", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(228, "A358 321 CL3", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(229, "A358 347", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(230, "A358 347 CL1", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(231, "A358 347 CL2", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(232, "A358 347 CL3", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(233, "A358 348", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(234, "A358 348 CL1", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(235, "A358 348 CL2", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(236, "A358 348 CL3", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(237, "A369 FP1", 126.0, 201300.0, 7833.4399, 1.0925e-05, 0.292);
        Add(238, "A369 FP11", 138.0, 204800.0, 7833.4399, 1.0925e-05, 0.292);
        Add(239, "A369 FP12", 138.0, 204800.0, 7833.4399, 1.0925e-05, 0.292);
        Add(240, "A369 FP2", 126.0, 204800.0, 7833.4399, 1.0925e-05, 0.292);
        Add(241, "A369 FP21", 138.0, 211000.0, 7833.4399, 1.0925e-05, 0.292);
        Add(242, "A369 FP22", 138.0, 211000.0, 7833.4399, 1.0925e-05, 0.292);
        Add(243, "A369 FP3B", 138.0, 204800.0, 7833.4399, 1.0925e-05, 0.292);
        Add(244, "A369 FP5", 138.0, 213100.0, 7695.04, 1.0313e-05, 0.292);
        Add(246, "A369 FP9", 138.0, 213100.0, 7639.6797, 1.0313e-05, 0.292);
        Add(247, "A376 304", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(248, "A376 TP304", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(249, "A376 TP304H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(250, "A376 TP304N", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(251, "A376 TP316", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(252, "A376 TP316H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(253, "A376 TP316N", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(254, "A376 TP321", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(255, "A376 TP321H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(256, "A376 TP347", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(257, "A376 TP347H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(258, "A376 TP348", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(259, "A-403 304", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(260, "A-403 304H", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(261, "A-403 304L", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(262, "A403 304N", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(263, "A-403 309", null, 195100.0, 8027.1997, 1.5659e-05, 0.292);
        Add(264, "A-403 310", null, 195100.0, 8027.1997, 1.5659e-05, 0.292);
        Add(265, "A-403 316", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(266, "A-403 316H", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(267, "A-403 316L", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(268, "A-403 316N", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(269, "A-403 321", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(270, "A403 321H", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(271, "A-403 347", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(272, "A-403 347H", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(273, "A-403 348", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(274, "A-403 348H", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(275, "A403 WP304", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(276, "A403 WP304H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(277, "A403 WP304L", 115.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(278, "A403 WP304N", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(279, "A403 WP309", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(280, "A403 WP310", 138.0, 195100.0, 8027.1997, 1.5821e-05, 0.292);
        Add(281, "A403 WP316", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(282, "A403 WP316H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(283, "A403 WP316L", 115.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(284, "A403 WP316N", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(285, "A403 WP317", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(286, "A403 WP321", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(287, "A403 WP321H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(288, "A403 WP347", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(289, "A403 WP347H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(290, "A403 WP348", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(291, "A403 WP348H", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(292, "A-430 FP347", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(293, "A-430 FP347H", null, 195100.0, 8027.1997, 1.5299e-05, 0.292);
        Add(294, "A430 FP304", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(295, "A430 FP304H", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(296, "A430 FP316", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(297, "A430 FP316H", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(298, "A430 FP316N", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(299, "A430 FP321", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(300, "A430 FP321H", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(301, "A430 FP347", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(302, "A430 FP347H", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(303, "API-5L A", 110.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(304, "API-5L A25", null, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(305, "API-5L B", 138.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(306, "API-5L X65", 177.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(307, "API-5L X70", 188.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(308, "API-5L X80", 207.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(309, "API-5LU U100", null, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(310, "API-5LU U80", null, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(311, "B165 Annealed", 115.0, 179300.0, 8470.0801, 1.3463e-05, 0.315);
        Add(312, "B241 6061 T6", 88.0, 68950.0, 2712.6399, 2.2048e-05, 0.33);
        Add(313, "B241 6063 T6", 69.0, 68950.0, 2712.6399, 2.2048e-05, 0.33);
        Add(314, "B337 1", 81.0, 106900.0, 4844.0, 8.4593e-06, 0.3);
        Add(315, "B337 2", 115.0, 106900.0, 4844.0, 8.4593e-06, 0.3);
        Add(316, "B337 3", 150.0, 106900.0, 4844.0, 8.4593e-06, 0.3);
        Add(317, "B337 7", 115.0, 106900.0, 4844.0, 8.4593e-06, 0.3);
        Add(318, "B42 Annealed", 41.0, 117200.0, 8912.96, 1.6775e-05, 0.355);
        Add(319, "B42 Drawn", 83.0, 117200.0, 8912.96, 1.6775e-05, 0.355);
        Add(320, "B43", 55.0, 117200.0, 8912.96, 1.6811e-05, 0.355);
        Add(321, "A234 WPC", 161.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(322, "API-5L X60", 172.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(323, "API-5L X46", 145.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(324, "A213 TP304", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(325, "A213 TP304H", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(326, "A213 TP304L", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(327, "A213 TP304N", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(328, "A213 TP309H", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(329, "A213 TP310H", null, 195100.0, 8027.1997, 1.3463e-05, 0.292);
        Add(330, "API-5L X42", 138.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(331, "API-5L X52", 152.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(332, "API-5L X56", 163.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(333, "A789 S32304", 200.0, 195100.0, 8027.1997, 1.2599e-05, 0.292);
        Add(334, "A790 S32304", 200.0, 195100.0, 8027.1997, 1.2599e-05, 0.292);
        Add(335, "A789 S32900", 207.0, 195100.0, 8027.1997, 1.0079e-05, 0.292);
        Add(336, "A790 S32900", 207.0, 195100.0, 8027.1997, 1.0079e-05, 0.292);
        Add(337, "A789 S31803", 207.0, 195100.0, 8027.1997, 1.2599e-05, 0.292);
        Add(338, "A790 S31803", 207.0, 195100.0, 8027.1997, 1.2599e-05, 0.292);
        Add(339, "A789 S32760", 250.0, 195100.0, 8027.1997, 1.2599e-05, 0.292);
        Add(340, "A790 S32760", 250.0, 195100.0, 8027.1997, 1.2599e-05, 0.292);
        Add(341, "A789 S32750", 267.0, 195100.0, 8027.1997, 1.2599e-05, 0.292);
        Add(342, "A790 S32750", 267.0, 195100.0, 8027.1997, 1.2599e-05, 0.292);
        Add(343, "A269 TP304", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(344, "A409 TP304", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(345, "A452 TP304H", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(346, "A452 TP347H", null, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(347, "A451 CPF8M", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(348, "A451 CPE20N", 184.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(349, "A409 TP348", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(350, "A409 TP347", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(351, "A269 TP304L", 115.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(352, "A269 TP316L", 115.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(353, "A451 CPF8", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(354, "A671 C55", 126.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(355, "A671 CC60", 138.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(356, "A671 CB60", 138.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(357, "A672 B60", 138.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(358, "A672 C60", 138.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(359, "A524 GR1", 138.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(360, "A334 6", 138.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(361, "A381Y35", 138.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(362, "A381Y48", 143.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(363, "A381Y50", 147.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(364, "A671 CC65", 150.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(365, "A671 CB65", 150.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(366, "A672 B65", 150.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(367, "A672 C65", 150.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(368, "A671 CC70", 161.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(369, "A671 CB70", 161.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(370, "A672 B70", 161.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(371, "A672 C70", 161.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(372, "A671 CD70", 161.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(373, "A691 1CR", 126.0, 204800.0, 7833.4399, 1.0925e-05, 0.292);
        Add(374, "A691 5CR", 138.0, 204800.0, 7833.4399, 1.0313e-05, 0.292);
        Add(375, "A691 9CR", 138.0, 204800.0, 7833.4399, 1.0313e-05, 0.292);
        Add(376, "A691 3CR", 138.0, 204800.0, 7833.4399, 1.0925e-05, 0.292);
        Add(377, "A426 CP5", 207.0, 213100.0, 7639.6797, 1.0313e-05, 0.292);
        Add(378, "A426 CP9", 207.0, 213100.0, 7639.6797, 1.0313e-05, 0.292);
        Add(379, "A691 1.25CR", 138.0, 204800.0, 7833.4399, 1.0925e-05, 0.292);
        Add(380, "A516 55", 126.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(381, "A516 60", 138.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(382, "A516 65", 150.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(383, "A516 70", 161.0, 203400.0, 7833.4399, 1.0925e-05, 0.292);
        Add(384, "A240 304", 138.0, 195100.0, 8007.8237, 1.5299e-05, 0.3);
        Add(385, "A240 304L", 115.0, 195100.0, 8007.8237, 1.5299e-05, 0.3);
        Add(386, "A240 304N", null, 195100.0, 8007.8237, 1.5299e-05, 0.3);
        Add(387, "A240 310H", null, 195100.0, 8007.8237, 1.4759e-05, 0.3);
        Add(388, "A240 310S", 138.0, 195100.0, 8007.8237, 1.4759e-05, 0.3);
        Add(389, "A240 316", 138.0, 195100.0, 8007.8237, 1.5299e-05, 0.3);
        Add(390, "A240 316L", 115.0, 195100.0, 8007.8237, 1.5299e-05, 0.3);
        Add(391, "A240 316N", null, 195100.0, 8007.8237, 1.5299e-05, 0.3);
        Add(392, "B444 N06625", 276.0, 206800.0, 8442.4004, 1.2833e-05, 0.28);
        Add(393, "B423 N08825", 161.0, 196500.0, 8082.5601, 1.3895e-05, 0.3);
        Add(394, "B729 N08020", 161.0, 196500.0, 8082.5601, 1.3463e-05, 0.3);
        Add(395, "B163 N08800", null, 196500.0, 8082.5601, 1.4003e-05, 0.3);
        Add(396, "B161 N02201", 46.0, 206800.0, 8082.5601, 1.3463e-05, 0.3);
        Add(397, "B165 N04400", 129.0, 179300.0, 8082.5601, 1.3463e-05, 0.3);
        Add(398, "B861 1", null, 106900.0, 4844.0, 8.4593e-06, 0.3);
        Add(399, "B861 2", null, 106900.0, 4844.0, 8.4593e-06, 0.3);
        Add(400, "B861 3", null, 106900.0, 4844.0, 8.4593e-06, 0.3);
        Add(401, "B861 7", null, 106900.0, 4844.0, 8.4593e-06, 0.3);
        Add(402, "A312 TP317L", 138.0, 195100.0, 8027.1997, 1.6397e-05, 0.292);
        Add(403, "B619 N10276", 188.0, 205500.0, 8885.2803, 1.0799e-05, 0.29);
        Add(404, "A213 T11", null, 204800.0, 7833.4399, 1.1519e-05, 0.3);
        Add(405, "A213 T12", null, 204800.0, 7833.4399, 1.1519e-05, 0.3);
        Add(406, "1.0345S-16-100", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(407, "1.0345S-40-100", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(408, "1.0425S-16-100", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(409, "1.0425S-40-100", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(410, "1.7715S-16-100", null, 214400.0, 7760.0049, 1.0324e-05, 0.3);
        Add(411, "1.7715S-40-100", null, 214400.0, 7760.0049, 1.0324e-05, 0.3);
        Add(412, "1.5415S-16-100", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(413, "1.5415S-40-100", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(414, "1.7380S-16-100", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(415, "1.7380S-40-100", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(416, "1.4922S-16-100", null, 215100.0, 7760.0049, 1.0324e-05, 0.3);
        Add(417, "1.4922S-40-100", null, 214400.0, 7760.0049, 1.0324e-05, 0.3);
        Add(418, "1.4903S-16-100", null, 214400.0, 7760.0049, 1.0324e-05, 0.3);
        Add(419, "1.4903S-40-100", null, 214400.0, 7760.0049, 1.0324e-05, 0.3);
        Add(420, "1.4541CS", null, 199900.0, 7929.9878, 1.5287e-05, 0.3);
        Add(421, "1.4541HS", null, 199900.0, 7929.9878, 1.5287e-05, 0.3);
        Add(422, "1.4571CS", null, 199900.0, 7929.9878, 1.5287e-05, 0.3);
        Add(423, "1.4571HS", null, 199900.0, 7929.9878, 1.5287e-05, 0.3);
        Add(424, "1.4301S", null, 199900.0, 7929.9878, 1.5287e-05, 0.3);
        Add(425, "1.4462S", null, 199900.0, 7929.9878, 1.5287e-05, 0.3);
        Add(426, "1.4539S", null, 200000.0, 7929.9878, 1.5287e-05, 0.3);
        Add(427, "1.4439S", null, 200000.0, 7929.9878, 1.5287e-05, 0.3);
        Add(428, "1.4306S", null, 199900.0, 7929.9878, 1.5287e-05, 0.3);
        Add(429, "1.4307S", null, 199900.0, 7929.9878, 1.5287e-05, 0.3);
        Add(430, "1.4435S", null, 200000.0, 7929.9878, 1.5287e-05, 0.3);
        Add(431, "1.4301W", null, 200000.0, 7929.9878, 1.5287e-05, 0.3);
        Add(432, "1.4541W", null, 200000.0, 7929.9878, 1.5287e-05, 0.3);
        Add(433, "1.4571W", null, 200000.0, 7929.9878, 1.5287e-05, 0.3);
        Add(434, "1.4439W", null, 200000.0, 7929.9878, 1.5287e-05, 0.3);
        Add(435, "1.4539W", null, 200000.0, 7929.9878, 1.5287e-05, 0.3);
        Add(436, "1.4462W", null, 199900.0, 7929.9878, 1.5287e-05, 0.3);
        Add(437, "1.0345W-16", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(438, "1.0345W-40", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(439, "1.0425W-16", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(440, "1.0425W-40", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(441, "1.5415W-16", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(442, "1.5415W-40", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(443, "1.4307W", null, 200000.0, 7929.9878, 1.5287e-05, 0.3);
        Add(444, "1.4306W", null, 200000.0, 7929.9878, 1.5287e-05, 0.3);
        Add(445, "1.4435W", null, 200000.0, 7929.9878, 1.5287e-05, 0.3);
        Add(446, "1.7335S-16-100", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(447, "1.7335S-40-100", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(448, "STPG370-S", null, 203400.0, 7861.1201, 1.1057e-05, 0.3);
        Add(449, "STPT370-S", null, 203400.0, 7861.1201, 1.1057e-05, 0.3);
        Add(450, "STS370-S", null, 203400.0, 7861.1201, 1.1057e-05, 0.3);
        Add(451, "STPL380-S", null, 203400.0, 7861.1201, 1.1057e-05, 0.3);
        Add(468, "1.0345S-16-200", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(469, "1.0345S-40-200", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(470, "1.0425S-16-200", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(471, "1.0425S-40-200", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(472, "1.7715S-16-200", null, 214400.0, 7760.0049, 1.0324e-05, 0.3);
        Add(473, "1.7715S-40-200", null, 214400.0, 7760.0049, 1.0324e-05, 0.3);
        Add(474, "1.5415S-16-200", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(475, "1.5415S-40-200", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(476, "1.7380S-16-200", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(477, "1.7380S-40-200", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(478, "1.4922S-16-200", null, 215100.0, 7760.0049, 1.0324e-05, 0.3);
        Add(479, "1.4922S-40-200", null, 214400.0, 7760.0049, 1.0324e-05, 0.3);
        Add(480, "1.4903S-16-200", null, 214400.0, 7760.0049, 1.0324e-05, 0.3);
        Add(481, "1.4903S-40-200", null, 214400.0, 7760.0049, 1.0324e-05, 0.3);
        Add(482, "1.7335S-16-200", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(483, "1.7335S-40-200", null, 211700.0, 7850.02, 1.1299e-05, 0.3);
        Add(484, "1.4404S-60", null, 199900.0, 7929.9878, 1.5287e-05, 0.3);
        Add(485, "1.4311W-60", null, 199900.0, 7929.9878, 1.5287e-05, 0.3);
        Add(486, "1.4401S-60", null, 199900.0, 7929.9878, 1.5287e-05, 0.3);
        Add(487, "1.4401W-60", null, 199900.0, 7929.9878, 1.5287e-05, 0.3);
        Add(488, "1.4311S-60", null, 199900.0, 7929.9878, 1.5287e-05, 0.3);
        Add(489, "1.4404W-60", null, 199900.0, 7929.9878, 1.5287e-05, 0.3);
        Add(490, "1.4429S-60", null, 199900.0, 7929.9878, 1.5287e-05, 0.3);
        Add(491, "1.4429W-60", null, 199900.0, 7929.9878, 1.5287e-05, 0.3);
        Add(492, "1.7383S-16-100", null, 211700.0, 7849.9927, 1.1299e-05, 0.3);
        Add(493, "1.7383S-40-100", null, 211700.0, 7849.9927, 1.1299e-05, 0.3);
        Add(494, "1.4901S-16-100", null, 214500.0, 7760.0049, 1.0324e-05, 0.3);
        Add(495, "1.4901S-40-100", null, 214500.0, 7760.0049, 1.0324e-05, 0.3);
        Add(496, "1.7338S-16-100", null, 211700.0, 7849.9927, 1.1299e-05, 0.3);
        Add(497, "1.7338S-40-100", null, 211700.0, 7849.9927, 1.1299e-05, 0.3);
        Add(498, "1.4901S-16-200", null, 214500.0, 7760.0049, 1.0324e-05, 0.3);
        Add(499, "1.4901S-40-200", null, 214500.0, 7760.0049, 1.0324e-05, 0.3);
        Add(500, "1.7338S-16-200", null, 211700.0, 7849.9927, 1.1299e-05, 0.3);
        Add(501, "1.7338S-40-200", null, 211700.0, 7849.9927, 1.1299e-05, 0.3);

        return byId;
    }
}
