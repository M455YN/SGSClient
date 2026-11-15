using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Library;
using SGSClient.Infrastructure.Client;
using SGSClient.Infrastructure.Client.Services;

namespace SGSClient.UI.Tests;

public class LibraryFolderServiceTests
{
    [Fact]
    public async Task AddFolderAsync_WritesMarkerAndRegistry()
    {
        // The registry lives next to the database, so each run needs its own directory.
        var testRoot = Path.Combine(Path.GetTempPath(), $"sgs-lib-folder-{Guid.NewGuid():N}");
        var libraryRoot = Path.Combine(testRoot, "library");
        var dbPath = Path.Combine(testRoot, "library.db");

        try
        {
            var services = new ServiceCollection();
            services.AddClientInfrastructure(sqliteDatabasePath: dbPath);
            await using var provider = services.BuildServiceProvider();

            var folderService = provider.GetRequiredService<ILibraryFolderService>();
            var entry = await folderService.AddFolderAsync(libraryRoot, "Test D:");

            Assert.Equal(Path.GetFullPath(libraryRoot), Path.GetFullPath(entry.Path));
            Assert.False(string.IsNullOrWhiteSpace(entry.ContentId));
            Assert.True(File.Exists(Path.Combine(libraryRoot, SgsLibraryFolderMarker.FileName)));
            Assert.True(Directory.Exists(SgsLibraryLayout.GetCommonRoot(libraryRoot)));
            Assert.True(Directory.Exists(SgsLibraryLayout.GetManifestsRoot(libraryRoot)));
            Assert.Single(folderService.GetFolders());
            Assert.True(File.Exists(Path.Combine(testRoot, SgsLibraryFoldersRegistry.FileName)));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SetPrimaryFolderAsync_MovesFolderToFront_AndKeepsOthers()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"sgs-lib-primary-{Guid.NewGuid():N}");
        var first = Path.Combine(testRoot, "first");
        var second = Path.Combine(testRoot, "second");

        try
        {
            var services = new ServiceCollection();
            services.AddClientInfrastructure(sqliteDatabasePath: Path.Combine(testRoot, "library.db"));
            await using var provider = services.BuildServiceProvider();
            var folderService = provider.GetRequiredService<ILibraryFolderService>();

            await folderService.AddFolderAsync(first);
            await folderService.SetPrimaryFolderAsync(second);
            await folderService.SetPrimaryFolderAsync(second);

            var folders = folderService.GetFolders().Select(f => Path.GetFullPath(f.Path)).ToList();
            Assert.Equal([Path.GetFullPath(second), Path.GetFullPath(first)], folders);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }
}
