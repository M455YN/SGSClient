using Microsoft.Extensions.Configuration;
using SGSClient.Core.Configuration;
using SGSClient.Core.Interfaces;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Tests;

public class SettingsViewModelTests
{
    private sealed class FakeStore : IClientUserSettingsStore
    {
        public ClientUserSettings Current { get; set; } = new();
        public int Saves { get; private set; }

        public ClientUserSettings Load() => Current;

        public Task SaveAsync(ClientUserSettings settings, CancellationToken cancellationToken = default)
        {
            Current = settings;
            Saves++;
            return Task.CompletedTask;
        }
    }

    private static SettingsViewModel Create(FakeStore store) =>
        new(new ConfigurationBuilder().Build(), null!, null!, userSettings: store);

    [Fact]
    public void Toggles_PersistWithoutTouchingOtherSettings()
    {
        var store = new FakeStore { Current = { DownloadMaxKbps = 500 } };
        var vm = Create(store);

        vm.CloseToTray = false;
        vm.AutoUpdateEnabled = false;

        Assert.False(store.Current.CloseToTray);
        Assert.False(store.Current.AutoUpdateEnabled);
        Assert.Equal(500, store.Current.DownloadMaxKbps);
    }

    [Fact]
    public void DownloadLimit_DefaultsToNoLimit()
    {
        Assert.Equal(0, new DownloadsViewModel(null, new FakeStore()).DownloadMaxMbps);
    }

    [Fact]
    public void DownloadLimit_ShownInMegabytesAndRoundedValueDoesNotOverwriteStored()
    {
        var store = new FakeStore { Current = { DownloadMaxKbps = 100000 } };
        var vm = new DownloadsViewModel(null, store);

        Assert.Equal(97.66, vm.DownloadMaxMbps);
        vm.DownloadMaxMbps = 97.66;
        Assert.Equal(100000, store.Current.DownloadMaxKbps);
        Assert.Equal(0, store.Saves);

        vm.DownloadMaxMbps = 5;
        Assert.Equal(5120, store.Current.DownloadMaxKbps);
        vm.DownloadMaxMbps = 0.5;
        Assert.Equal(512, store.Current.DownloadMaxKbps);
        vm.DownloadMaxMbps = 0;
        Assert.Equal(0, store.Current.DownloadMaxKbps);
    }

    [Fact]
    public void RefreshPreferences_PicksUpOutsideChangesWithoutSaving()
    {
        var store = new FakeStore();
        var vm = Create(store);

        store.Current = new ClientUserSettings { AutoUpdateEnabled = false, CloseToTray = false };
        vm.RefreshPreferences();

        Assert.False(vm.AutoUpdateEnabled);
        Assert.False(vm.CloseToTray);
        Assert.Equal(0, store.Saves);

        vm.CloseToTray = true;
        Assert.Equal(1, store.Saves);
    }
}
