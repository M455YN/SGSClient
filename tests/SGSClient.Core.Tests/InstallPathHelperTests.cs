using SGSClient.Core.Install;

namespace SGSClient.Core.Tests;

public class InstallPathHelperTests
{
    [Theory]
    [InlineData("https://localhost:7142/", "/api/games/packages/foo/1.0/foo.zip",
        "https://localhost:7142/api/games/packages/foo/1.0/foo.zip")]
    [InlineData("https://localhost:7142", "api/games/packages/foo/1.0/foo.zip",
        "https://localhost:7142/api/games/packages/foo/1.0/foo.zip")]
    [InlineData("https://api.example.com/v1/", "/packages/game.zip",
        "https://api.example.com/v1/packages/game.zip")]
    public void ResolveDownloadUrl_CombinesRelativeZipLinkWithBaseUrl(
        string baseUrl,
        string zipLink,
        string expected)
    {
        var result = InstallPathHelper.ResolveDownloadUrl(baseUrl, zipLink);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveDownloadUrl_ReturnsAbsoluteUrlUnchanged()
    {
        const string absolute = "https://cdn.example.com/game.zip";
        var result = InstallPathHelper.ResolveDownloadUrl("https://localhost:7142/", absolute);
        Assert.Equal(absolute, result);
    }

    [Fact]
    public void ResolveDownloadUrl_ReturnsEmptyForBlankZipLink()
    {
        Assert.Equal(string.Empty, InstallPathHelper.ResolveDownloadUrl("https://localhost:7142/", ""));
        Assert.Equal(string.Empty, InstallPathHelper.ResolveDownloadUrl("https://localhost:7142/", "   "));
    }

    [Theory]
    [InlineData("bin/Game.exe")]
    [InlineData("/bin/Game.exe")]
    [InlineData("bin\\Game.exe")]
    public void CombineExecutablePath_JoinsNestedExeRelativePath(string exeRelativePath)
    {
        var installDir = Path.Combine("C:", "games", "my-game", "1.0.0");
        var result = InstallPathHelper.CombineExecutablePath(installDir, exeRelativePath);

        Assert.Equal(Path.Combine(installDir, "bin", "Game.exe"), result);
    }

    [Fact]
    public void GetInstallDirectory_UsesSgsAppsCommonLayout()
    {
        var root = Path.Combine("C:", "SGSClient", "games");
        var result = InstallPathHelper.GetInstallDirectory(root, "my-game", "2.1.0");

        Assert.Equal(Path.Combine(root, "sgsapps", "common", "my-game", "2.1.0"), result);
    }

    [Fact]
    public void GetLegacyInstallDirectory_KeepsFlatLayout()
    {
        var root = Path.Combine("C:", "SGSClient", "games");
        var result = InstallPathHelper.GetLegacyInstallDirectory(root, "my-game", "2.1.0");

        Assert.Equal(Path.Combine(root, "my-game", "2.1.0"), result);
    }
}
