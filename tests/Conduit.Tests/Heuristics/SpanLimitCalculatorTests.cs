using Conduit.Core.Heuristics;
using Conduit.Core.NeutralFiles;
using Xunit;

namespace Conduit.Tests.Heuristics;

public class SpanLimitCalculatorTests
{
    private static Element MakeElement(double outsideDiameter, double wallThickness, double pipeDensity = 0.2836)
    {
        var real = new double[53];
        real[5] = outsideDiameter;
        real[6] = wallThickness;
        real[29] = pipeDensity;
        return new Element { RealValues = real, AuxiliaryPointers = new int[15] };
    }

    [Fact]
    public void ComputeMaxSpan_ReturnsPositiveValue_ForATypicalPipe()
    {
        var element = MakeElement(outsideDiameter: 6.625, wallThickness: 0.280);

        var span = SpanLimitCalculator.ComputeMaxSpan(element);

        Assert.True(span > 0);
    }

    [Fact]
    public void ComputeMaxSpan_ReturnsZero_ForZeroDiameter()
    {
        var element = MakeElement(outsideDiameter: 0, wallThickness: 0);

        Assert.Equal(0, SpanLimitCalculator.ComputeMaxSpan(element));
    }

    [Fact]
    public void ComputeMaxSpan_Decreases_AsDensityIncreases_AtFixedGeometry()
    {
        // Heavier contents/material (at the same section modulus) should sag sooner — span
        // scales as 1/sqrt(w), an unambiguous relationship, unlike diameter vs. span (which
        // isn't monotonic in either direction in general, since both section modulus and
        // weight grow with diameter).
        var lighter = MakeElement(outsideDiameter: 6.625, wallThickness: 0.280, pipeDensity: 0.28);
        var heavier = MakeElement(outsideDiameter: 6.625, wallThickness: 0.280, pipeDensity: 0.56);

        var lighterSpan = SpanLimitCalculator.ComputeMaxSpan(lighter);
        var heavierSpan = SpanLimitCalculator.ComputeMaxSpan(heavier);

        Assert.True(lighterSpan > heavierSpan, $"Expected the lighter pipe's span ({lighterSpan}) to exceed the heavier one's ({heavierSpan}).");
    }

    [Fact]
    public void ComputeMaxSpan_FallsBackToDefaultSteelDensity_WhenPipeDensityIsUnset()
    {
        var withDensity = MakeElement(outsideDiameter: 6.625, wallThickness: 0.280, pipeDensity: SpanLimitCalculator.DefaultSteelDensity);
        var withoutDensity = MakeElement(outsideDiameter: 6.625, wallThickness: 0.280, pipeDensity: 0);

        Assert.Equal(SpanLimitCalculator.ComputeMaxSpan(withDensity), SpanLimitCalculator.ComputeMaxSpan(withoutDensity));
    }
}
