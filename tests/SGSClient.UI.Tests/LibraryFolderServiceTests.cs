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
}
