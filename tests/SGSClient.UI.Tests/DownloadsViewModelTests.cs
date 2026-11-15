using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Tests;

public class DownloadsViewModelTests
{
    [Fact]
    public async Task ActiveDownload_ShowsCoverSpeedSizeAndEta_AndRestIsUpNext()
    {
        var queue = new FakeQueue(
            new InstallQueueEntry { GameId = 1, Title = "One", CoverUrl = "/media/one.jpg", State = InstallState.Downloading },
            new InstallQueueEntry { GameId = 2, Title = "Two", State = InstallState.Queued });
        var vm = new DownloadsViewModel(queue, apiBaseUrl: "https://api.example/");

        Assert.Equal(1, vm.ActiveRow?.GameId);
        Assert.Equal([2], vm.UpNext.Select(r => r.GameId));
        Assert.Equal("https://api.example/media/one.jpg", vm.ActiveCoverUrl);

        const long mb = 1L << 20;
        queue.Report(1, 10 * mb, 100 * mb);
        await Task.Delay(700);
        queue.Report(1, 16 * mb, 100 * mb);
        // The view model marshals progress to the context captured at construction (xUnit's), so wait for it.
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && vm.ActiveSizeText != "16 MB / 100 MB")
        {
            await Task.Delay(20);
        }

        Assert.NotEqual("—", vm.ActiveSpeedText);
        Assert.Equal("16 MB / 100 MB", vm.ActiveSizeText);
        Assert.False(string.IsNullOrEmpty(vm.ActiveEtaText));
    }

    private sealed class FakeQueue(params InstallQueueEntry[] entries) : IInstallQueueService
    {
        public IReadOnlyList<InstallQueueEntry> Entries { get; } = entries;

        public event EventHandler? Changed;

        public event EventHandler<InstallQueueProgressEventArgs>? ProgressChanged;

        public void Report(int gameId, long received, long total) =>
            ProgressChanged?.Invoke(this, new InstallQueueProgressEventArgs
            {
                GameId = gameId,
                State = InstallState.Downloading,
                Progress = InstallProgress.Of(InstallPhase.Downloading, received / (double)total, "x", received, total)
            });

        public Task EnqueueInstallAsync(int gameId, string? version = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task EnqueueUpdateAsync(int gameId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Cancel(int gameId) => Changed?.Invoke(this, EventArgs.Empty);

        public void Pause(int gameId)
        {
        }

        public void Resume(int gameId)
        {
        }

        public void MoveUp(int gameId)
        {
        }

        public void MoveDown(int gameId)
        {
        }

        public bool IsQueuedOrActive(int gameId) => true;

        public Task CheckForUpdatesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RestorePersistedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
