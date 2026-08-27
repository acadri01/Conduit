using Conduit.Core.NeutralFiles;
using Conduit.Tests.TestHelpers;
using Xunit;

namespace Conduit.Tests.NeutralFiles;

/// <summary>
/// Coverage for the restraint fixes made per direct report ("the program does not actually set
/// any restraints"): <see cref="NeutralFile.AddRestraint"/>'s element-pointer wiring (the actual
/// root cause — see its doc comment) and <see cref="Restraint.CreateSingleDof"/>'s
/// stiffness/direction-cosine correctness, both confirmed byte-exact against
/// <c>fixtures/real-samples/44002.cii</c>'s real restraints.
/// </summary>
public class RestraintFormatTests
{
    [Fact]
    public void CreateSingleDof_SetsRigidStiffness_NotZero()
    {
        var restraint = Restraint.CreateSingleDof(60, RestraintType.Y, rigidStiffness: 1.7512e11);

        Assert.Equal(1.7512e11, restraint.Dofs[0].Stiffness);
    }

    [Theory]
    [InlineData(RestraintType.Y, 0, 1, 0)]
    [InlineData(RestraintType.PlusY, 0, 1, 0)]
    [InlineData(RestraintType.MinusY, 0, 1, 0)]
    [InlineData(RestraintType.X, 1, 0, 0)]
    [InlineData(RestraintType.PlusX, 1, 0, 0)]
    [InlineData(RestraintType.Z, 0, 0, 1)]
    [InlineData(RestraintType.PlusZ, 0, 0, 1)]
    public void CreateSingleDof_SetsDirectionCosine_ForAxisImpliedTypes(RestraintType type, double x, double y, double z)
    {
        var restraint = Restraint.CreateSingleDof(60, type, rigidStiffness: 1.0);

        Assert.Equal(x, restraint.Dofs[0].DirectionCosineX);
        Assert.Equal(y, restraint.Dofs[0].DirectionCosineY);
        Assert.Equal(z, restraint.Dofs[0].DirectionCosineZ);
    }

    /// <summary>
    /// Confirmed against the one real ANC restraint with a direction available
    /// (<c>fixtures/real-samples/44002.cii</c>, node 10): an anchor's direction cosine is
    /// (0,0,0), unlike the axis-implied types. GUI is left the same way — see
    /// <see cref="Restraint.CreateSingleDof"/>'s doc comment for why that one is still an open
    /// question rather than a confirmed default.
    /// </summary>
    [Theory]
    [InlineData(RestraintType.Anc)]
    [InlineData(RestraintType.Gui)]
    public void CreateSingleDof_LeavesDirectionCosineZero_ForNonAxisTypes(RestraintType type)
    {
        var restraint = Restraint.CreateSingleDof(60, type, rigidStiffness: 1.0);

        Assert.Equal((0, 0, 0), (restraint.Dofs[0].DirectionCosineX, restraint.Dofs[0].DirectionCosineY, restraint.Dofs[0].DirectionCosineZ));
    }

    [Fact]
    public void AddRestraint_SetsOwningElementsPointer_ToNodeMatchPreferred()
    {
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            NeutralFileFixtureBuilder.Schedule40Run(10, 20, 1000),
            NeutralFileFixtureBuilder.Schedule40Run(20, 30, 1000),
        };
        var file = NeutralFileFixtureBuilder.Build(segments, anchorNodes: []);

        file.AddRestraint(Restraint.CreateSingleDof(20, RestraintType.PlusY, file.Units.RigidRestraintStiffness));

        var owner = file.Elements.Single(e => e.ToNode == 20);
        Assert.Equal(1, owner.AuxiliaryPointers[Element.RestraintPointerIndex]);
        Assert.Equal(0, file.Elements.Single(e => e.FromNode == 20).AuxiliaryPointers[Element.RestraintPointerIndex]);
    }

    /// <summary>
    /// A run's very first node is nobody's <c>ToNode</c> — <see cref="NeutralFile.AddRestraint"/>
    /// falls back to the one element starting there.
    /// </summary>
    [Fact]
    public void AddRestraint_FallsBackToFromNodeMatch_ForARunsFirstNode()
    {
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            NeutralFileFixtureBuilder.Schedule40Run(10, 20, 1000),
        };
        var file = NeutralFileFixtureBuilder.Build(segments, anchorNodes: []);

        file.AddRestraint(Restraint.CreateSingleDof(10, RestraintType.Anc, file.Units.RigidRestraintStiffness));

        Assert.Equal(1, file.Elements.Single(e => e.FromNode == 10).AuxiliaryPointers[Element.RestraintPointerIndex]);
    }

    /// <summary>
    /// Two restraints that both need the *same* connecting element (an anchor immediately
    /// followed, with nothing in between, by a support needing the same element's other end) must
    /// end up on two distinct elements, not one clobbering the other's pointer — see
    /// <see cref="NeutralFile.AddRestraint"/>'s doc comment. This is exactly what surfaced the bug
    /// on <c>fixtures/loop-50m-3d.cii</c>: an anchor at the run's start immediately followed by a
    /// guide at the very next node.
    /// </summary>
    [Fact]
    public void AddRestraint_FallsBackToADifferentElement_WhenTheNaturalOwnerIsAlreadyClaimed()
    {
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            NeutralFileFixtureBuilder.Schedule40Run(10, 20, 1000),
            NeutralFileFixtureBuilder.Schedule40Run(20, 30, 1000),
        };
        // The fixture builder itself wires node 10's anchor onto the 10->20 element (its only
        // possible owner, via the FromNode fallback, since node 10 is nobody's ToNode).
        var file = NeutralFileFixtureBuilder.Build(segments, anchorNodes: [10]);
        var element1020 = file.Elements.Single(e => e.FromNode == 10 && e.ToNode == 20);
        Assert.Equal(1, element1020.AuxiliaryPointers[Element.RestraintPointerIndex]); // sanity check on the premise

        file.AddRestraint(Restraint.CreateSingleDof(20, RestraintType.Gui, file.Units.RigidRestraintStiffness));

        // 10->20 still owns the anchor — untouched — and 20->30 picks up the guide instead of
        // either restraint's pointer getting silently overwritten.
        Assert.Equal(1, file.Elements.Single(e => e.FromNode == 10 && e.ToNode == 20).AuxiliaryPointers[Element.RestraintPointerIndex]);
        Assert.Equal(2, file.Elements.Single(e => e.FromNode == 20 && e.ToNode == 30).AuxiliaryPointers[Element.RestraintPointerIndex]);
    }
}
