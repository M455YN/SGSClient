using System.Text.Json;
using SGSClient.Core.Configuration;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Client.Services;

public sealed class ClientUserSettingsStore : IClientUserSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public ClientUserSettingsStore(string? dataRoot = null)
    {
        var root = dataRoot ?? Path.GetDirectoryName(DependencyInjection.GetDefaultLibraryDbPath()) ?? string.Empty;
        _settingsPath = Path.Combine(root, "client-settings.json");
    }

    public ClientUserSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new ClientUserSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<ClientUserSettings>(json, JsonOptions) ?? new ClientUserSettings();
        }
        catch
        {
            return new ClientUserSettings();
        }
    }

    public async Task SaveAsync(ClientUserSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(_settingsPath, json, cancellationToken).ConfigureAwait(false);
    }
}
