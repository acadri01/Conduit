using Conduit.Core.Heuristics;
using Conduit.Core.NeutralFiles;
using Xunit;

namespace Conduit.Tests.Heuristics;

public class ElementSplitterTests
{
    private const double OutsideDiameterMillimetres = 168.3;

    private static Element MakeElement(int fromNode, int toNode, double deltaX, int bendPointer = 0, int restraintPointer = 0)
    {
        var real = new double[53];
        real[0] = fromNode;
        real[1] = toNode;
        real[2] = deltaX;
        real[5] = OutsideDiameterMillimetres;
        real[6] = 7.11;
        var pointers = new int[15];
        pointers[0] = bendPointer;
        pointers[Element.RestraintPointerIndex] = restraintPointer;
        return new Element { RealValues = real, AuxiliaryPointers = pointers };
    }

    /// <summary>The exact worked example from direct instruction: 25550 mm against a 6446.76 mm max allowable span.</summary>
    [Fact]
    public void WorkedExample_25550mmSpan_Against6446_76mmMaxSpan_SplitsIntoFour6000sAndA1550Remainder()
    {
        var element = MakeElement(10, 20, 25550);
        var nextNode = 100;

        var plan = ElementSplitter.Split(element, 25550, 6446.76, OutsideDiameterMillimetres, () => nextNode++);

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

        var plan = ElementSplitter.Split(element, 24000, 6446.76, OutsideDiameterMillimetres, () => 999);

        Assert.Equal(4, plan.Elements.Count);
        Assert.All(plan.Elements, e => Assert.Equal(6000, e.DeltaX));
        Assert.Equal(3, plan.NewInteriorNodes.Count);
    }

    [Fact]
    public void ElementAlreadyWithinMaxSpan_IsNotSplit()
    {
        var element = MakeElement(10, 20, 5000);

        var plan = ElementSplitter.Split(element, 5000, 6446.76, OutsideDiameterMillimetres, () => throw new InvalidOperationException("shouldn't need a new node"));

        Assert.Single(plan.Elements);
        Assert.Same(element, plan.Elements[0]);
        Assert.Empty(plan.NewInteriorNodes);
    }

    [Fact]
    public void MaxSpanRoundsDownToZero_IsNotSplit()
    {
        // A max allowable span under 1 m rounds down to a 0 mm chunk size — nothing meaningful to split into.
        var element = MakeElement(10, 20, 50000);

        var plan = ElementSplitter.Split(element, 50000, 900, OutsideDiameterMillimetres, () => throw new InvalidOperationException("shouldn't need a new node"));

        Assert.Single(plan.Elements);
        Assert.Empty(plan.NewInteriorNodes);
    }

    [Fact]
    public void BendPointer_OnlySurvivesOnTheFinalChunk_NotEveryInteriorOne()
    {
        // The original element's ToNode is a bend corner (AuxiliaryPointers[0] != 0) — only the
        // chunk that still ends at that corner may keep the bend pointer; every interior chunk
        // must not falsely claim to be the same bend.
        var element = MakeElement(10, 20, 24000, bendPointer: 3);

        var plan = ElementSplitter.Split(element, 24000, 6446.76, OutsideDiameterMillimetres, () => 999);

        Assert.Equal(4, plan.Elements.Count);
        Assert.All(plan.Elements.Take(3), e => Assert.Equal(0, e.AuxiliaryPointers[0]));
        Assert.Equal(3, plan.Elements[^1].AuxiliaryPointers[0]);
    }

    /// <summary>
    /// Per direct instruction: "an element break should never cause an element with a bend to be
    /// shorter than [the bend's minimum straight length]." A 500 mm remainder next to a bend
    /// (minimum: 1.5x168.3mm radius + 500mm shoe buffer = 752.45 mm) is too short, so it's merged
    /// into the previous chunk instead of left standing alone.
    /// </summary>
    [Fact]
    public void TooShortRemainderNearABend_IsMergedIntoThePreviousChunk_NotLeftStandingAlone()
    {
        var element = MakeElement(10, 20, 24500, bendPointer: 3); // 4x6000 + a 500 mm remainder

        var plan = ElementSplitter.Split(element, 24500, 6446.76, OutsideDiameterMillimetres, () => 900);

        Assert.Equal(4, plan.Elements.Count); // not 5 - the short remainder was absorbed
        Assert.Equal([6000, 6000, 6000, 6500], plan.Elements.Select(e => e.DeltaX));
        Assert.Equal(3, plan.NewInteriorNodes.Count);
        Assert.Equal(3, plan.Elements[^1].AuxiliaryPointers[0]); // the bend pointer still lands on the true final chunk
    }

    [Fact]
    public void NoBendAtTheToNode_RemainderIsNeverMerged_EvenIfShort()
    {
        // Same short remainder as above, but the ToNode isn't a bend — nothing to protect, so the
        // ordinary even chunking applies.
        var element = MakeElement(10, 20, 24500);

        var plan = ElementSplitter.Split(element, 24500, 6446.76, OutsideDiameterMillimetres, () => 900);

        Assert.Equal(5, plan.Elements.Count);
        Assert.Equal([6000, 6000, 6000, 6000, 500], plan.Elements.Select(e => Math.Round(e.DeltaX, 6)));
    }

    /// <summary>
    /// A restraint at the original element's ToNode (e.g. a run's end anchor, or any restraint
    /// Conduit itself placed there before this span turned out to need splitting) must stay on
    /// the one chunk that still ends at that node — the last — not every chunk, and not lost.
    /// </summary>
    [Fact]
    public void RestraintPointer_AtToNode_OnlySurvivesOnTheFinalChunk()
    {
        var element = MakeElement(10, 20, 24000, restraintPointer: 7);

        var plan = ElementSplitter.Split(element, 24000, 6446.76, OutsideDiameterMillimetres, () => 999, restraintBelongsToFromNode: false);

        Assert.Equal(4, plan.Elements.Count);
        Assert.All(plan.Elements.Take(3), e => Assert.Equal(0, e.AuxiliaryPointers[Element.RestraintPointerIndex]));
        Assert.Equal(7, plan.Elements[^1].AuxiliaryPointers[Element.RestraintPointerIndex]);
    }

    /// <summary>
    /// A restraint at the original element's FromNode (e.g. a run's very first node, whose anchor
    /// is nobody's ToNode) must stay on the first chunk, not the last.
    /// </summary>
    [Fact]
    public void RestraintPointer_AtFromNode_OnlySurvivesOnTheFirstChunk()
    {
        var element = MakeElement(10, 20, 24000, restraintPointer: 7);

        var plan = ElementSplitter.Split(element, 24000, 6446.76, OutsideDiameterMillimetres, () => 999, restraintBelongsToFromNode: true);

        Assert.Equal(4, plan.Elements.Count);
        Assert.Equal(7, plan.Elements[0].AuxiliaryPointers[Element.RestraintPointerIndex]);
        Assert.All(plan.Elements.Skip(1), e => Assert.Equal(0, e.AuxiliaryPointers[Element.RestraintPointerIndex]));
    }

    [Fact]
    public void SplitElements_PreserveTheOriginalsPipeProperties()
    {
        var element = MakeElement(10, 20, 24000);

        var plan = ElementSplitter.Split(element, 24000, 6446.76, OutsideDiameterMillimetres, () => 999);

        Assert.All(plan.Elements, e =>
        {
            Assert.Equal(element.OutsideDiameter, e.OutsideDiameter);
            Assert.Equal(element.WallThickness, e.WallThickness);
        });
    }
}
