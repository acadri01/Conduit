using Conduit.Core.Heuristics;
using Xunit;

namespace Conduit.Tests.Heuristics;

/// <summary>
/// <see cref="MaterialLibrary"/> is a placeholder per direct instruction (2026-09-01) — the
/// resolve-by-material-ID mechanism is real; the data behind it now covers two real materials.
/// Values here were corrected 2026-09-02 after the user's real UMAT1.pdf printout revealed the
/// previous round had the wrong material ID (107, actually ASTM A135 Grade A) for A106 Grade B
/// (actually material 106) — see <see cref="MaterialLibrary"/>'s class doc comment for the full
/// derivation.
/// </summary>
public class MaterialLibraryTests
{
    [Fact]
    public void Resolve_A106GradeB_ReturnsItsRealProperties()
    {
        var material = MaterialLibrary.Resolve(MaterialLibrary.A106GradeBMaterialId);

        Assert.Equal(106, material.MaterialId);
        Assert.Equal("ASTM A106 Grade B", material.Name);
        Assert.Equal(138.0, material.AllowableStressMpa);
        Assert.Equal(203_400.0, material.ElasticModulusMpa);
        Assert.Equal(7833.4399, material.DensityKgPerM3);
        Assert.Equal(1.0925e-5, material.ThermalExpansionCoefficientPerDegreeCelsius);
        Assert.Equal(0.30, material.PoissonsRatio);
    }

    [Fact]
    public void Resolve_A135GradeA_ReturnsItsRealProperties()
    {
        var material = MaterialLibrary.Resolve(MaterialLibrary.A135GradeAMaterialId);

        Assert.Equal(107, material.MaterialId);
        Assert.Equal("ASTM A135 Grade A", material.Name);
        Assert.Equal(110.0, material.AllowableStressMpa);
        Assert.Equal(203_400.0, material.ElasticModulusMpa);
        Assert.Equal(7833.4399, material.DensityKgPerM3);
    }

    [Fact]
    public void Resolve_UnknownMaterialId_FallsBackToA106GradeB()
    {
        var material = MaterialLibrary.Resolve(999_999);

        Assert.Equal(MaterialLibrary.A106GradeBMaterialId, material.MaterialId);
        Assert.Equal("ASTM A106 Grade B", material.Name);
    }
}
