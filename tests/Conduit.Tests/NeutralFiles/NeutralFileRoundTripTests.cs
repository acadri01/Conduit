using Conduit.Core.NeutralFiles;
using Xunit;

namespace Conduit.Tests.NeutralFiles;

public class NeutralFileRoundTripTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Theory]
    [InlineData("straight-run.cii")]
    [InlineData("run-with-riser.cii")]
    public void UnmodifiedFile_RoundTripsByteIdentical(string fixtureName)
    {
        var path = FixturePath(fixtureName);
        var originalLines = File.ReadAllLines(path);

        var file = NeutralFileReader.Read(path);
        var writtenLines = NeutralFileWriter.ToLines(file);

        Assert.Equal(originalLines, writtenLines);
    }

    [Fact]
    public void StraightRun_ParsesExpectedElementsAndAnchors()
    {
        var file = NeutralFileReader.Read(FixturePath("straight-run.cii"));

        Assert.Equal(18, file.Elements.Count);
        Assert.Equal(2, file.Restraints.Count);
        Assert.Contains(file.Restraints, r => r.Node == 10 && r.Dofs[0].Type == RestraintType.Anc);
        Assert.Contains(file.Restraints, r => r.Node == 190 && r.Dofs[0].Type == RestraintType.Anc);
    }

    [Fact]
    public void AddingARestraint_UpdatesControlCountAndAppearsOnWrite()
    {
        var file = NeutralFileReader.Read(FixturePath("straight-run.cii"));

        file.AddRestraint(Restraint.CreateSingleDof(60, RestraintType.Y, UnitsSection.Metric.RigidRestraintStiffness));

        Assert.Equal(3, file.Control.NumRestraints);

        var lines = NeutralFileWriter.ToLines(file);
        var reparsed = NeutralFileReader.Parse(lines);

        Assert.Equal(3, reparsed.Restraints.Count);
        Assert.Contains(reparsed.Restraints, r => r.Node == 60 && r.Dofs[0].Type == RestraintType.Y);
    }

    /// <summary>
    /// Adding a restraint must wire up its owning element's restraint pointer (see
    /// <see cref="NeutralFile.AddRestraint"/>'s doc comment) — so the ELEMENTS section is
    /// expected to change, but only the one element ending at the restrained node, and only its
    /// pointer array (not its geometry or any other element).
    /// </summary>
    [Fact]
    public void AddingARestraint_OnlyChangesItsOwningElementsPointer()
    {
        var file = NeutralFileReader.Read(FixturePath("straight-run.cii"));
        var owningElementIndex = file.Elements.FindIndex(e => e.ToNode == 60);
        Assert.True(owningElementIndex >= 0, "fixture must have an element ending at node 60 for this test to mean anything");

        var miscel1Before = file.Blocks.First(b => b.Name == "MISCEL_1").RawLines.ToList();
        var otherElementsBefore = file.Elements.Where((_, i) => i != owningElementIndex).ToList();

        file.AddRestraint(Restraint.CreateSingleDof(60, RestraintType.Y, UnitsSection.Metric.RigidRestraintStiffness));
        NeutralFileWriter.ToLines(file); // syncs edited blocks

        Assert.Equal(3, file.Elements[owningElementIndex].AuxiliaryPointers[Element.RestraintPointerIndex]);
        Assert.Equal(
            otherElementsBefore.Select(e => e.ToRawLines()),
            file.Elements.Where((_, i) => i != owningElementIndex).Select(e => e.ToRawLines()));
        Assert.Equal(miscel1Before, file.Blocks.First(b => b.Name == "MISCEL_1").RawLines);
    }

    [Fact]
    public void MalformedFile_ThrowsParseExceptionAndDoesNotThrowFromMissingHandling()
    {
        var ex = Assert.Throws<NeutralFileParseException>(() => NeutralFileReader.Read(FixturePath("malformed.cii")));
        Assert.Contains("CONTROL", ex.Message);
    }

    /// <summary>
    /// Real CAESAR II-exported <c>.cii</c> files use CRLF line endings (confirmed against real
    /// samples) — <c>iecho.exe</c> and CAESAR II itself reject LF-only output. This asserts the
    /// actual bytes written to disk, not <see cref="NeutralFileWriter.ToLines"/>'s in-memory
    /// string list (which strips line endings entirely and so can't catch this).
    /// </summary>
    [Fact]
    public void Write_UsesCrlfLineEndings()
    {
        var file = NeutralFileReader.Read(FixturePath("straight-run.cii"));
        var outputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            NeutralFileWriter.Write(file, outputPath);
            var raw = File.ReadAllText(outputPath);

            Assert.Contains("\r\n", raw);
            Assert.DoesNotContain("\r\n\r\n", raw); // no doubled CR from re-writing an already-CRLF-read file
            Assert.DoesNotMatch(@"(?<!\r)\n", raw); // every \n must be preceded by \r — no bare LF anywhere
        }
        finally
        {
            File.Delete(outputPath);
        }
    }
}
