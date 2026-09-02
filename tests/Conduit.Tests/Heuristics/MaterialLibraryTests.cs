using Conduit.Core.Heuristics;
using Xunit;

namespace Conduit.Tests.Heuristics;

/// <summary>
/// <see cref="MaterialLibrary"/> is a placeholder per direct instruction (2026-09-01) — the
/// resolve-by-material-ID mechanism is real, the data behind it is still just one material.
/// </summary>
public class MaterialLibraryTests
{
    [Fact]
    public void Resolve_KnownMaterialId_ReturnsItsRealProperties()
    {
        var material = MaterialLibrary.Resolve(MaterialLibrary.A106GradeBMaterialId);

        Assert.Equal("ASTM A106 Grade B", material.Name);
        Assert.Equal(118.0, material.AllowableStressMpa);
        Assert.Equal(203_400.0, material.ElasticModulusMpa);
        Assert.Equal(7833.4399, material.DensityKgPerM3);
    }

    /// <summary>
    /// Thermal expansion coefficient and Poisson's ratio were never extracted from the source
    /// printout (not needed until now) — must stay null rather than a guessed number, since this
    /// is safety-relevant engineering data. See QUESTIONS.md.
    /// </summary>
    [Fact]
    public void Resolve_KnownMaterial_HasNoGuessedThermalOrPoissonData()
    {
        var material = MaterialLibrary.Resolve(MaterialLibrary.A106GradeBMaterialId);

        Assert.Null(material.ThermalExpansionCoefficientPerDegreeCelsius);
        Assert.Null(material.PoissonsRatio);
    }

    [Fact]
    public void Resolve_UnknownMaterialId_FallsBackToA106GradeB()
    {
        var material = MaterialLibrary.Resolve(999_999);

        Assert.Equal(MaterialLibrary.A106GradeBMaterialId, material.MaterialId);
        Assert.Equal("ASTM A106 Grade B", material.Name);
    }
}
