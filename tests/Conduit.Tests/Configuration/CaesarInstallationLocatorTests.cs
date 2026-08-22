using Conduit.Core.Configuration;
using Xunit;

namespace Conduit.Tests.Configuration;

public class CaesarInstallationLocatorTests
{
    [Fact]
    public void FindInstallations_ReturnsOnlyVersionsAtOrAboveTheMinimum_NewestFirst()
    {
        using var root = new TempDirectory();
        root.CreateSubdirectory("14.50");
        root.CreateSubdirectory("15.00");
        root.CreateSubdirectory("15.01");
        root.CreateSubdirectory("16.00");
        root.CreateSubdirectory("not-a-version");

        var installations = CaesarInstallationLocator.FindInstallations(root.Path);

        Assert.Equal(
            [new Version(16, 0), new Version(15, 1), new Version(15, 0)],
            installations.Select(i => i.Version).ToList());
    }

    [Fact]
    public void FindInstallations_SystemDirectory_IsTheVersionFoldersSystemSubfolder()
    {
        using var root = new TempDirectory();
        var versionDir = root.CreateSubdirectory("15.01");

        var installations = CaesarInstallationLocator.FindInstallations(root.Path);

        var installation = Assert.Single(installations);
        Assert.Equal(versionDir, installation.RootDirectory);
        Assert.Equal(Path.Combine(versionDir, "System"), installation.SystemDirectory);
    }

    [Fact]
    public void FindInstallations_MissingRoot_ReturnsEmptyRatherThanThrowing()
    {
        var installations = CaesarInstallationLocator.FindInstallations(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        Assert.Empty(installations);
    }

    [Fact]
    public void FindLatest_ReturnsTheNewestSupportedVersion()
    {
        using var root = new TempDirectory();
        root.CreateSubdirectory("15.00");
        root.CreateSubdirectory("15.01");

        var latest = CaesarInstallationLocator.FindLatest(root.Path);

        Assert.Equal(new Version(15, 1), latest?.Version);
    }

    [Fact]
    public void FindLatest_ReturnsNull_WhenNoSupportedVersionExists()
    {
        using var root = new TempDirectory();
        root.CreateSubdirectory("14.50");

        Assert.Null(CaesarInstallationLocator.FindLatest(root.Path));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("conduit-tests-").FullName;

        public string CreateSubdirectory(string name) => Directory.CreateDirectory(System.IO.Path.Combine(Path, name)).FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
