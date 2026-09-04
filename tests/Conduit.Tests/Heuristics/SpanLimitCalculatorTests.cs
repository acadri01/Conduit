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

    /// <summary>
    /// CAESAR uses -1.01 as its own "field not populated" sentinel throughout its data (per direct
    /// instruction — confirmed present in the UMAT1 printout's COLD MODULUS field for two
    /// materials; not itself found in the four real <c>.cii</c> samples' own insulation/fluid
    /// density fields, but guarded against here regardless, since trusting it as a literal
    /// negative density would silently *subtract* weight and overestimate the max span — the
    /// unsafe direction). Insulation/fluid density with the sentinel must compute identically to
    /// the same element with those fields genuinely zero (no insulation, empty bore), not a
    /// larger span from a phantom negative contribution.
    /// </summary>
    [Fact]
    public void ComputeMaxSpan_TreatsNegativeInsulationOrFluidDensityAsZero_NotANegativeContribution()
    {
        double[] MakeRealValues(double insulationDensity, double fluidDensity)
        {
            var real = new double[53];
            real[5] = SixInchOutsideDiameterMm;
            real[6] = SixInchWallThicknessMm;
            real[29] = SpanLimitCalculator.DefaultSteelDensityKgPerM3;
            real[30] = insulationDensity;
            real[31] = fluidDensity;
            return real;
        }

        var withSentinel = new Element { RealValues = MakeRealValues(-1.01, -1.01), AuxiliaryPointers = new int[15] };
        var withGenuineZero = new Element { RealValues = MakeRealValues(0, 0), AuxiliaryPointers = new int[15] };

        Assert.Equal(SpanLimitCalculator.ComputeMaxSpan(withGenuineZero), SpanLimitCalculator.ComputeMaxSpan(withSentinel));
    }
}
