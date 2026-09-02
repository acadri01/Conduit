using Conduit.Core.Heuristics;
using Xunit;

namespace Conduit.Tests.Heuristics;

/// <summary>
/// <see cref="MaterialLibrary"/> now covers all 399 real materials from the user's UMAT1 printout
/// (per direct instruction, 2026-09-02: "I would like to have all the materials in the
/// database"). These tests spot-check a handful of entries against the raw printout rather than
/// asserting all 399 — see <see cref="MaterialLibrary"/>'s class doc comment for the full
/// extraction/verification methodology.
/// </summary>
public class MaterialLibraryTests
{
    [Fact]
    public void Resolve_A106GradeB_ReturnsItsRealProperties()
    {
        var material = MaterialLibrary.Resolve(MaterialLibrary.A106GradeBMaterialId);

        Assert.Equal(106, material.MaterialId);
        Assert.Equal("A106 B", material.Name);
        Assert.Equal(138.0, material.AllowableStressMpa);
        Assert.Equal(203_400.0, material.ElasticModulusMpa);
        Assert.Equal(7833.4399, material.DensityKgPerM3);
        Assert.Equal(1.0925e-5, material.ThermalExpansionCoefficientPerDegreeCelsius);
        Assert.Equal(0.292, material.PoissonsRatio);
    }

    [Fact]
    public void Resolve_A135GradeA_ReturnsItsRealProperties()
    {
        var material = MaterialLibrary.Resolve(MaterialLibrary.A135GradeAMaterialId);

        Assert.Equal(107, material.MaterialId);
        Assert.Equal("A135 A", material.Name);
        Assert.Equal(110.0, material.AllowableStressMpa);
        Assert.Equal(203_400.0, material.ElasticModulusMpa);
        Assert.Equal(7833.4399, material.DensityKgPerM3);
    }

    /// <summary>
    /// Most of the 399 materials have no code-authoritative allowable stress cross-referenced yet
    /// (see the class doc comment) — this should be a real, honest <c>null</c>, not a guess.
    /// </summary>
    [Fact]
    public void Resolve_MostMaterials_HaveNoGuessedAllowableStress()
    {
        var lowCarbon = MaterialLibrary.Resolve(1);

        Assert.Null(lowCarbon.AllowableStressMpa);
        Assert.NotNull(lowCarbon.ElasticModulusMpa);
        Assert.True(lowCarbon.DensityKgPerM3 > 0);
    }

    /// <summary>
    /// Aluminum (#14) is a physically distinctive spot-check: its properties (density ~2,800
    /// kg/m³, modulus ~71,000 MPa) are nothing like the carbon-steel entries the extraction was
    /// built and tuned against, so a correct match here is real evidence the extraction
    /// generalizes rather than having been curve-fit to the two hand-verified materials.
    /// </summary>
    [Fact]
    public void Resolve_Aluminum_HasRealDistinctPhysicalProperties()
    {
        var aluminum = MaterialLibrary.Resolve(14);

        Assert.Equal("ALUMINUM", aluminum.Name);
        Assert.Equal(71_020.0, aluminum.ElasticModulusMpa);
        Assert.Equal(2803.9841, aluminum.DensityKgPerM3);
    }

    /// <summary>
    /// Materials #9 (WROUGHT IRON) and #12 (K-MONEL) list an obviously-invalid negative elastic
    /// modulus (-1.01 MPa) in the source printout itself — a real data-quality issue, not an
    /// extraction bug. <see cref="MaterialProperties.ElasticModulusMpa"/> must be <c>null</c>
    /// rather than that impossible value.
    /// </summary>
    [Theory]
    [InlineData(9)]
    [InlineData(12)]
    public void Resolve_MaterialsWithInvalidSourceModulus_HaveNullElasticModulusRatherThanTheSentinel(int materialId)
    {
        var material = MaterialLibrary.Resolve(materialId);

        Assert.Null(material.ElasticModulusMpa);
        Assert.True(material.DensityKgPerM3 > 0);
    }

    [Fact]
    public void Resolve_UnknownMaterialId_FallsBackToA106GradeB()
    {
        var material = MaterialLibrary.Resolve(999_999);

        Assert.Equal(MaterialLibrary.A106GradeBMaterialId, material.MaterialId);
        Assert.Equal("A106 B", material.Name);
    }
}
