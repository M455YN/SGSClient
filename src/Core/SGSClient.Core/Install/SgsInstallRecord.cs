using System.Text.Json;
using System.Text.Json.Serialization;
using SGSClient.Core.Enums;

namespace SGSClient.Core.Install;

/// <summary>
/// Written to each install folder as <c>sgs-install.json</c> so the client can
/// rediscover games after reinstall when the user points to the library folder.
/// </summary>
public sealed class SgsInstallRecord
{
    public const string FileName = "sgs-install.json";

    public int GameId { get; set; }
    public int BuildId { get; set; }
    public required string Symbol { get; set; }
    public required string Title { get; set; }
    public GamePlatform Platform { get; set; }
    public string? Architecture { get; set; }
    public required string Version { get; set; }
    public required string Executable { get; set; }
    public DateTimeOffset InstalledUtc { get; set; }
    public string? LibraryRoot { get; set; }
    public string? InstallPath { get; set; }

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
