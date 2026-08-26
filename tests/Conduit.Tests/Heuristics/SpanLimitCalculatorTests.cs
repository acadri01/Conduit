using Conduit.Core.Heuristics;
using Conduit.Core.NeutralFiles;
using Xunit;

namespace Conduit.Tests.Heuristics;

public class SpanLimitCalculatorTests
{
    private static Element MakeElement(double outsideDiameter, double wallThickness, double pipeDensity = SpanLimitCalculator.DefaultSteelDensityKgPerM3)
    {
        var real = new double[53];
        real[5] = outsideDiameter;
        real[6] = wallThickness;
        real[29] = pipeDensity;
        return new Element { RealValues = real, AuxiliaryPointers = new int[15] };
    }

    // 6" Sch 40 pipe, in millimetres — Conduit's default unit system (ComputeMaxSpan(Element) with
    // no NeutralFile always assumes UnitsSection.Metric).
    private const double SixInchOutsideDiameterMm = 168.3;
    private const double SixInchWallThicknessMm = 7.11;

    [Fact]
    public void ComputeMaxSpan_ReturnsPositiveValue_ForATypicalPipe()
    {
        var element = MakeElement(outsideDiameter: SixInchOutsideDiameterMm, wallThickness: SixInchWallThicknessMm);

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
        var lighter = MakeElement(outsideDiameter: SixInchOutsideDiameterMm, wallThickness: SixInchWallThicknessMm, pipeDensity: 4000);
        var heavier = MakeElement(outsideDiameter: SixInchOutsideDiameterMm, wallThickness: SixInchWallThicknessMm, pipeDensity: 8000);

        var lighterSpan = SpanLimitCalculator.ComputeMaxSpan(lighter);
        var heavierSpan = SpanLimitCalculator.ComputeMaxSpan(heavier);

        Assert.True(lighterSpan > heavierSpan, $"Expected the lighter pipe's span ({lighterSpan}) to exceed the heavier one's ({heavierSpan}).");
    }

    [Fact]
    public void ComputeMaxSpan_FallsBackToDefaultSteelDensity_WhenPipeDensityIsUnset()
    {
        var withDensity = MakeElement(outsideDiameter: SixInchOutsideDiameterMm, wallThickness: SixInchWallThicknessMm, pipeDensity: SpanLimitCalculator.DefaultSteelDensityKgPerM3);
        var withoutDensity = MakeElement(outsideDiameter: SixInchOutsideDiameterMm, wallThickness: SixInchWallThicknessMm, pipeDensity: 0);

        Assert.Equal(SpanLimitCalculator.ComputeMaxSpan(withDensity), SpanLimitCalculator.ComputeMaxSpan(withoutDensity));
    }
}
