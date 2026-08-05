using System.Text.Json;

namespace SGSClient.Core.Packaging;

/// <summary>
/// Shared System.Text.Json settings and friendly parse errors for <see cref="SgsPackageManifest"/>.
/// </summary>
public static class SgsPackageManifestSerializer
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public static async Task<(SgsPackageManifest? Manifest, string? Error)> TryDeserializeAsync(
        Stream utf8Json,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                utf8Json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                },
                cancellationToken);

            if (LooksLikeJsonSchema(document.RootElement))
            {
                return (null,
                    $"Plik {SgsPackageManifest.FileName} wygląda na schemat JSON (JSON Schema), a nie na manifest gry. " +
                    "Skopiuj przykład z schemas/examples/ albo utwórz plik z polami schemaVersion, game i build.");
            }

            var manifest = document.RootElement.Deserialize<SgsPackageManifest>(Options);
            if (manifest is null)
            {
                return (null, "Manifest jest pusty lub ma nieprawidłowy format.");
            }

            return (manifest, null);
        }
        catch (JsonException ex)
        {
            return (null, FormatJsonError(ex));
        }
        catch (Exception ex)
        {
            return (null, $"Nie udało się odczytać manifestu: {ex.Message}");
        }
    }

    public static (SgsPackageManifest? Manifest, string? Error) TryDeserialize(string utf8Json)
    {
        try
        {
            using var document = JsonDocument.Parse(
                utf8Json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });

            if (LooksLikeJsonSchema(document.RootElement))
            {
                return (null,
                    $"Plik {SgsPackageManifest.FileName} wygląda na schemat JSON (JSON Schema), a nie na manifest gry. " +
                    "Skopiuj przykład z schemas/examples/ albo utwórz plik z polami schemaVersion, game i build.");
            }

            var manifest = document.RootElement.Deserialize<SgsPackageManifest>(Options);
            if (manifest is null)
            {
                return (null, "Manifest jest pusty lub ma nieprawidłowy format.");
            }

            return (manifest, null);
        }
        catch (JsonException ex)
        {
            return (null, FormatJsonError(ex));
        }
        catch (Exception ex)
        {
            return (null, $"Nie udało się odczytać manifestu: {ex.Message}");
        }
    }

    private static bool LooksLikeJsonSchema(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (root.TryGetProperty("$schema", out _) || root.TryGetProperty("$id", out _))
        {
            return true;
        }

        // Schema documents describe structure; instance manifests have nested game/build objects with title/version.
        if (root.TryGetProperty("properties", out _)
            && root.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String
            && string.Equals(type.GetString(), "object", StringComparison.OrdinalIgnoreCase)
            && !root.TryGetProperty("game", out _))
        {
            return true;
        }

        return false;
    }

    private static string FormatJsonError(JsonException ex)
    {
        var detail = ex.Message;
        if (detail.Contains("missing required properties", StringComparison.OrdinalIgnoreCase))
        {
            return "Manifest jest niekompletny — wymagane są m.in. pola game (title, symbol) i build (version, platform, executable). "
                   + detail;
        }

        return $"Nie udało się odczytać manifestu: {detail}";
    }
}
