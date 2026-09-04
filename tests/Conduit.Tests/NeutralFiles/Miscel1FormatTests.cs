using Conduit.Tests.TestHelpers;
using Xunit;

namespace Conduit.Tests.NeutralFiles;

/// <summary>
/// Regression coverage for <c>#$ MISCEL_1</c>'s trailing hanger-table-defaults/execution-options
/// block — present unconditionally (not gated by any <c>#$ CONTROL</c> count), so omitting it is
/// a confirmed cause of a downstream `iecho.exe` parse error at whatever section comes next (here,
/// <c>#$ UNITS</c>) rather than at <c>#$ MISCEL_1</c> itself. See QUESTIONS.md's "Fixed: MISCEL_1
/// section missing its unconditional trailing block" entry and
/// <c>NeutralFileFixtureBuilder.BuildMiscel1Lines</c>'s doc comment.
/// </summary>
public class Miscel1FormatTests
{
    [Fact]
    public void BuiltFixture_Miscel1_HasFourLineTrailingBlock_AfterRrmat()
    {
        var segments = new List<NeutralFileFixtureBuilder.PipeSegmentSpec>
        {
            NeutralFileFixtureBuilder.Schedule40Run(10, 20, 1000),
            NeutralFileFixtureBuilder.Schedule40Run(20, 30, 1000),
        };
        var file = NeutralFileFixtureBuilder.Build(segments, anchorNodes: [10, 30]);
        var miscel1Lines = file.Blocks.First(b => b.Name == "MISCEL_1").RawLines;

        // 2 elements -> 1 RRMAT line (ceil(2/6) = 1), then the 4-line trailing block.
        Assert.Equal(5, miscel1Lines.Count);
        Assert.Equal(
        [
            "              1            0            2            2 0.000000E+00            0",
            "              0            0 4.001740E+00 2.159830E+01            0            0",
            "              0            0            0            0 2.500000E-01            3",
            "              3            1",
        ],
        miscel1Lines.Skip(1));
    }
}
