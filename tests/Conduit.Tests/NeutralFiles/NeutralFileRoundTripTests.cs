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

        file.AddRestraint(Restraint.CreateSingleDof(60, RestraintType.Y));

        Assert.Equal(3, file.Control.NumRestraints);

        var lines = NeutralFileWriter.ToLines(file);
        var reparsed = NeutralFileReader.Parse(lines);

        Assert.Equal(3, reparsed.Restraints.Count);
        Assert.Contains(reparsed.Restraints, r => r.Node == 60 && r.Dofs[0].Type == RestraintType.Y);
    }

    [Fact]
    public void OtherSections_StayByteIdentical_WhenOnlyRestraintsChange()
    {
        var file = NeutralFileReader.Read(FixturePath("straight-run.cii"));
        var elementsBlockBefore = file.Blocks.First(b => b.Name == "ELEMENTS").RawLines.ToList();

        file.AddRestraint(Restraint.CreateSingleDof(60, RestraintType.Y));
        NeutralFileWriter.ToLines(file); // syncs edited blocks

        var elementsBlockAfter = file.Blocks.First(b => b.Name == "ELEMENTS").RawLines;
        Assert.Equal(elementsBlockBefore, elementsBlockAfter);
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
