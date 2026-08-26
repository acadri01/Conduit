using Conduit.Core.NeutralFiles;
using Conduit.Tests.TestHelpers;
using Xunit;

namespace Conduit.Tests.NeutralFiles;

/// <summary>
/// Regression coverage for a confirmed class of bug: a section whose line count doesn't match
/// its own <c>#$ CONTROL</c> count field desyncs `iecho.exe`'s fixed-record reader, producing a
/// parse error several sections later (not at the section that's actually wrong) — see
/// QUESTIONS.md's "Fixed: WIND section unconditionally populated" entry.  Confirmed via
/// `fixtures/real-samples/TESTv15.cii`/`TESTv15_slugged.cii`, which have <em>no</em>
/// <c>#$ WIND</c> data line (matching `NumWindLoads = 0`) — contradicting an earlier, incorrect
/// assumption that <c>#$ WIND</c> always carries a default row regardless of the count.
/// </summary>
public class SectionCountConsistencyTests
{
    private static string RealSamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "real-samples", name);

    [Theory]
    [InlineData("TESTv15.cii", 0)]
    [InlineData("TESTv15_slugged.cii", 0)]
    [InlineData("44002.cii", 1)]
    public void RealSample_WindSectionLineCount_MatchesControlsNumWindLoads(string fixtureName, int expectedWindLines)
    {
        var file = NeutralFileReader.Read(RealSamplePath(fixtureName));
        var windLines = file.Blocks.First(b => b.Name == "WIND").RawLines;

        Assert.Equal(expectedWindLines, file.Control.NumWindLoads);
        Assert.Equal(expectedWindLines, windLines.Count);
    }

    [Fact]
    public void BuiltFixture_WindSectionIsEmpty_MatchingNumWindLoadsOfZero()
    {
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            NeutralFileFixtureBuilder.Schedule40Run(10, 20, 1000),
        };
        var file = NeutralFileFixtureBuilder.Build(segments, anchorNodes: [10, 20]);
        var windLines = file.Blocks.First(b => b.Name == "WIND").RawLines;

        Assert.Equal(0, file.Control.NumWindLoads);
        Assert.Empty(windLines);
    }
}
