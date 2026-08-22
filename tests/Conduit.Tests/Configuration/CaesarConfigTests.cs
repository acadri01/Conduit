using Conduit.Core.Configuration;
using Xunit;

namespace Conduit.Tests.Configuration;

public class CaesarConfigTests
{
    [Fact]
    public void EffectiveCode_PrefersConfigsOwnDefaultCode_WhenPresent()
    {
        var config = CaesarConfigReader.Parse(["DEFAULT_CODE =                    B31.1_2018        43      43."]);

        Assert.Equal("B31.1_2018", CaesarConfig.EffectiveCode(config));
    }

    [Fact]
    public void EffectiveCode_FallsBackToDefaultAssumedCode_WhenConfigHasNoDefaultCode()
    {
        var config = CaesarConfigReader.Parse(["Z_AXIS_UP=                         NO              129       0."]);

        Assert.Equal(CaesarConfig.DefaultAssumedCode, CaesarConfig.EffectiveCode(config));
    }

    [Fact]
    public void EffectiveCode_FallsBackToDefaultAssumedCode_WhenConfigIsNull()
    {
        Assert.Equal(CaesarConfig.DefaultAssumedCode, CaesarConfig.EffectiveCode(null));
    }

    [Fact]
    public void DefaultAssumedCode_IsTheLatestB31_3Edition()
    {
        Assert.Equal("B31.3_2024", CaesarConfig.DefaultAssumedCode);
    }
}
