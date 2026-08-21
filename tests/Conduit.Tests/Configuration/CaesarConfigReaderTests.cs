using Conduit.Core.Configuration;
using Xunit;

namespace Conduit.Tests.Configuration;

public class CaesarConfigReaderTests
{
    private static string FixturePath() => Path.Combine(AppContext.BaseDirectory, "fixtures", "caesar.cfg");

    [Fact]
    public void RealExampleFile_ParsesKnownFields()
    {
        var config = CaesarConfigReader.Read(FixturePath());

        Assert.Equal(false, config.ZAxisUp);
        Assert.Equal("B31.3_2020", config.DefaultCode);
        Assert.Equal("SYSTEM", config.SystemDirectoryName);
        Assert.Equal("UMAT1.UMD", config.UserMaterialFileName);
        Assert.Equal("AISC89.BIN", config.StructuralDatabaseFileName);
    }

    [Fact]
    public void ZAxisUp_ParsesYesAsTrue()
    {
        var config = CaesarConfigReader.Parse(["Z_AXIS_UP=                         YES              129       1."]);

        Assert.Equal(true, config.ZAxisUp);
    }

    [Fact]
    public void MissingField_ReturnsNull()
    {
        var config = CaesarConfigReader.Parse(["DEFAULT_CODE =                    B31.3_2020        43      43."]);

        Assert.Null(config.ZAxisUp);
        Assert.Null(config.SystemDirectoryName);
    }

    [Fact]
    public void LinesWithoutEquals_AreSkippedRatherThanThrowing()
    {
        var config = CaesarConfigReader.Parse([" Ver. 15.010", "not a config line at all", "DEFAULT_CODE = B31.1_2018 43 43."]);

        Assert.Equal("B31.1_2018", config.DefaultCode);
    }
}
