using SGSClient.Core.Packaging;

namespace SGSClient.Core.Tests;

public sealed class SgsPackageManifestSerializerTests
{
    [Fact]
    public void TryDeserialize_AcceptsCamelCaseInstance()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "game": {
                "title": "Demo Runner",
                "symbol": "demo-runner",
                "genre": "action",
                "engine": "custom",
                "media": {
                  "cover": "media/cover.png",
                  "header": "media/header.png",
                  "screenshots": ["media/screen-01.png"]
                }
              },
              "build": {
                "version": "0.1.0",
                "platform": "windows",
                "executable": "bin/DemoRunner.exe"
              }
            }
            """;

        var (manifest, error) = SgsPackageManifestSerializer.TryDeserialize(json);

        Assert.Null(error);
        Assert.NotNull(manifest);
        Assert.Equal("demo-runner", manifest!.Game.Symbol);
        Assert.Equal("action", manifest.Game.Genre);
        Assert.Equal("custom", manifest.Game.Engine);
        Assert.Equal("media/cover.png", manifest.Game.Media!.Cover);
        Assert.Equal("media/header.png", manifest.Game.Media.Header);
        Assert.Equal("bin/DemoRunner.exe", manifest.Build.Executable);
    }

    [Fact]
    public void TryDeserialize_RejectsJsonSchemaDocument()
    {
        const string json = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.local/schemas/sgs-manifest.v1.json",
              "title": "SGS Game Package Manifest",
              "type": "object",
              "required": ["schemaVersion", "game", "build"],
              "properties": {
                "schemaVersion": { "type": "integer", "const": 1 }
              }
            }
            """;

        var (manifest, error) = SgsPackageManifestSerializer.TryDeserialize(json);

        Assert.Null(manifest);
        Assert.NotNull(error);
        Assert.Contains("schemat JSON", error, StringComparison.OrdinalIgnoreCase);
    }
}
