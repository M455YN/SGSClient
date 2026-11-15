using SGSClient.Core.Configuration;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Client.Services;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Tests;

public class FirstRunWizardTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContinueSetup_DropsTheUntouchedDefaultFolder_ButNeverOneWithGames(bool defaultHasGame)
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"sgs-wizard-{Guid.NewGuid():N}");
        var defaultRoot = Path.Combine(testRoot, "default");
        var chosen = Path.Combine(testRoot, "chosen");
        try
        {
            var settings = new FakeSettings();
            var folders = new LibraryFolderService(new LibraryFoldersStore(Path.Combine(testRoot, "data")), settings);
            // The app registers the default folder on first use, before the wizard asks.
            await folders.AddFolderAsync(defaultRoot);
            var gameFile = Path.Combine(defaultRoot, "sgsapps", "common", "demo", "1.0.0", "game.exe");
            if (defaultHasGame)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(gameFile)!);
                await File.WriteAllTextAsync(gameFile, "stub");
            }

            var wizard = new LoginGateViewModel(null, null, settings, folders, defaultRoot) { GamesFolder = chosen };
            await wizard.ContinueSetupCommand.ExecuteAsync(null);

            Assert.True(settings.Current.SetupCompleted);
            Assert.Equal(Path.GetFullPath(chosen), Path.GetFullPath(folders.GetFolders()[0].Path));
            Assert.Equal(defaultHasGame, Directory.Exists(defaultRoot));
            Assert.Equal(defaultHasGame, File.Exists(gameFile));
            Assert.Equal(defaultHasGame ? 2 : 1, folders.GetFolders().Count);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private sealed class FakeSettings : IClientUserSettingsStore
    {
        public ClientUserSettings Current { get; private set; } = new();

        public ClientUserSettings Load() => Current;

        public Task SaveAsync(ClientUserSettings settings, CancellationToken cancellationToken = default)
        {
            Current = settings;
            return Task.CompletedTask;
        }
    }
}
