using SGSClient.Core.Enums;

namespace SGSClient.Core.Tests;

public class InstallStateTests
{
    [Fact]
    public void InstallState_HasExpectedMembers()
    {
        Assert.Equal(0, (int)InstallState.NotInstalled);
        Assert.Equal(4, (int)InstallState.Installed);
        Assert.True(Enum.IsDefined(InstallState.Downloading));
    }
}
