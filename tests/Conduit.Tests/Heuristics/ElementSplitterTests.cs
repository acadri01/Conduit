using Conduit.Core.Heuristics;
using Conduit.Core.NeutralFiles;
using Xunit;

namespace Conduit.Tests.Heuristics;

public class ElementSplitterTests
{
    private static Element MakeElement(int fromNode, int toNode, double deltaX)
    {
        var real = new double[53];
        real[0] = fromNode;
        real[1] = toNode;
        real[2] = deltaX;
        real[5] = 168.3;
        real[6] = 7.11;
        return new Element { RealValues = real, AuxiliaryPointers = new int[15] };
    }

    /// <summary>The exact worked example from direct instruction: 25550 mm against a 6446.76 mm max allowable span.</summary>
    [Fact]
    public void WorkedExample_25550mmSpan_Against6446_76mmMaxSpan_SplitsIntoFour6000sAndA1550Remainder()
    {
        var element = MakeElement(10, 20, 25550);
        var nextNode = 100;

        var plan = ElementSplitter.Split(element, 25550, 6446.76, () => nextNode++);

        Assert.Equal(5, plan.Elements.Count);
        Assert.Equal([6000, 6000, 6000, 6000, 1550], plan.Elements.Select(e => e.DeltaX));
        Assert.Equal(4, plan.NewInteriorNodes.Count);
        Assert.Equal([100, 101, 102, 103], plan.NewInteriorNodes);

        // Chained FromNode/ToNode: 10 -> 100 -> 101 -> 102 -> 103 -> 20.
        Assert.Equal([10, 100, 101, 102, 103], plan.Elements.Select(e => e.FromNode));
        Assert.Equal([100, 101, 102, 103, 20], plan.Elements.Select(e => e.ToNode));
    }

    [Fact]
    public void ExactMultiple_SplitsWithNoRemainderElement()
    {
        var element = MakeElement(10, 20, 24000);

        var plan = ElementSplitter.Split(element, 24000, 6446.76, () => 999);

        Assert.Equal(4, plan.Elements.Count);
        Assert.All(plan.Elements, e => Assert.Equal(6000, e.DeltaX));
        Assert.Equal(3, plan.NewInteriorNodes.Count);
    }

    [Fact]
    public void ElementAlreadyWithinMaxSpan_IsNotSplit()
    {
        var element = MakeElement(10, 20, 5000);

        var plan = ElementSplitter.Split(element, 5000, 6446.76, () => throw new InvalidOperationException("shouldn't need a new node"));

        Assert.Single(plan.Elements);
        Assert.Same(element, plan.Elements[0]);
        Assert.Empty(plan.NewInteriorNodes);
    }

    [Fact]
    public void MaxSpanRoundsDownToZero_IsNotSplit()
    {
        // A max allowable span under 1 m rounds down to a 0 mm chunk size — nothing meaningful to split into.
        var element = MakeElement(10, 20, 50000);

        var plan = ElementSplitter.Split(element, 50000, 900, () => throw new InvalidOperationException("shouldn't need a new node"));

        Assert.Single(plan.Elements);
        Assert.Empty(plan.NewInteriorNodes);
    }

    [Fact]
    public void BendPointer_OnlySurvivesOnTheFinalChunk_NotEveryInteriorOne()
    {
        // The original element's ToNode is a bend corner (AuxiliaryPointers[0] != 0) — only the
        // chunk that still ends at that corner may keep the bend pointer; every interior chunk
        // must not falsely claim to be the same bend.
        var real = new double[53];
        real[0] = 10;
        real[1] = 20;
        real[2] = 24000;
        real[5] = 168.3;
        real[6] = 7.11;
        var pointers = new int[15];
        pointers[0] = 3; // bend record #3
        var element = new Element { RealValues = real, AuxiliaryPointers = pointers };

        var plan = ElementSplitter.Split(element, 24000, 6446.76, () => 999);

        Assert.Equal(4, plan.Elements.Count);
        Assert.All(plan.Elements.Take(3), e => Assert.Equal(0, e.AuxiliaryPointers[0]));
        Assert.Equal(3, plan.Elements[^1].AuxiliaryPointers[0]);
    }

    [Fact]
    public void SplitElements_PreserveTheOriginalsPipeProperties()
    {
        var element = MakeElement(10, 20, 24000);

        var plan = ElementSplitter.Split(element, 24000, 6446.76, () => 999);

        Assert.All(plan.Elements, e =>
        {
            Assert.Equal(element.OutsideDiameter, e.OutsideDiameter);
            Assert.Equal(element.WallThickness, e.WallThickness);
        });
    }
}
