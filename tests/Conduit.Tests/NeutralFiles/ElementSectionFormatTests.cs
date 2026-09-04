using Conduit.Core.NeutralFiles;
using Conduit.Tests.TestHelpers;
using Xunit;

namespace Conduit.Tests.NeutralFiles;

/// <summary>
/// Regression coverage for the <c>#$ ELEMENTS</c> record layout Conduit's synthetic-fixture
/// builder writes — guards against the byte-format bugs that made <c>iecho.exe</c> reject
/// Conduit-generated files (see QUESTIONS.md's "ELEMENTS color/visibility line" and "mm as
/// default" entries; docs/neutral-file/WALKTHROUGH.md documents the confirmed-correct layout
/// these tests check against).
/// </summary>
public class ElementSectionFormatTests
{
    private static string RealSamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "real-samples", name);

    /// <summary>
    /// <c>NeutralFile-v15.pdf</c> labels the line-color/line-visibility field <c>(2X, 6G13.6)</c>
    /// — real-value format — but all 3 real samples write it as plain integers ("-1 -1", no
    /// decimal/E-notation) instead. Writing it as a real (the bug this guards against) is a
    /// confirmed cause of iecho.exe's "Error processing ELEMENT section, line # NN".
    /// </summary>
    [Fact]
    public void BuiltElement_WritesColorVisibilityLine_AsPlainIntegers_MatchingRealSamples()
    {
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            NeutralFileFixtureBuilder.Schedule40Run(10, 20, 1000),
        };
        var file = NeutralFileFixtureBuilder.Build(segments, anchorNodes: [10, 20]);
        var elementsLines = file.Blocks.First(b => b.Name == "ELEMENTS").RawLines;

        // 9 real-value lines, then name, then line-number, then this line.
        var colorVisibilityLine = elementsLines[11];

        Assert.Equal("             -1           -1", colorVisibilityLine);
    }

    [Theory]
    [InlineData("TESTv15.cii")]
    [InlineData("TESTv15_slugged.cii")]
    [InlineData("44002.cii")]
    public void RealSample_ColorVisibilityLines_AreAllPlainIntegersNegativeOne(string fixtureName)
    {
        var file = NeutralFileReader.Read(RealSamplePath(fixtureName));
        var elementsLines = file.Blocks.First(b => b.Name == "ELEMENTS").RawLines;

        // Every element record is 15 lines (9 real + name + line-number + color/visibility + 3
        // pointer lines); the color/visibility line is the 12th (0-based index 11) of each.
        for (var i = 11; i < elementsLines.Count; i += 15)
        {
            Assert.Equal("             -1           -1", elementsLines[i]);
        }
    }

    [Theory]
    [InlineData("TESTv15.cii")]
    [InlineData("TESTv15_slugged.cii")]
    [InlineData("44002.cii")]
    public void RealSample_UnitsSection_ParsesAsMetric(string fixtureName)
    {
        var file = NeutralFileReader.Read(RealSamplePath(fixtureName));

        Assert.True(file.Units.IsMetric);
        Assert.Equal(1.0, file.Units.LengthToMillimetres, precision: 6);
    }

    [Fact]
    public void UnitsSection_Parse_TreatsMissingBlock_AsMetricDefault()
    {
        var units = UnitsSection.Parse(null);

        Assert.True(units.IsMetric);
        Assert.Equal(1.0, units.LengthToMillimetres);
    }

    /// <summary>
    /// An English-native file's CNVLEN is 1.0 (its own length unit already *is* one inch) —
    /// confirmed from <c>NeutralFile-v15.pdf</c>'s definition of CNVLEN as "native length units
    /// per inch"; no real English-unit sample was available to confirm byte-for-byte, so this
    /// constructs the block directly from that documented definition.
    /// </summary>
    [Fact]
    public void UnitsSection_Parse_DetectsEnglishFile_FromCnvlenOfOne()
    {
        var block = new NeutralFileBlock
        {
            Name = "UNITS",
            HeaderLine = "#$ UNITS",
            RawLines = ["   1.000000E+00 4.448000E+00 4.536000E-01 1.129800E-01 1.129800E-01 6.895000E-03"],
        };

        var units = UnitsSection.Parse(block);

        Assert.False(units.IsMetric);
        Assert.Equal(25.4, units.LengthToMillimetres, precision: 6);
    }
}
