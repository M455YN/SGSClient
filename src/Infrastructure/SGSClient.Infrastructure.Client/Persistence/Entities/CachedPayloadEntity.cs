namespace SGSClient.Infrastructure.Client.Persistence.Entities;

public sealed class CachedPayloadEntity
{
    public string Key { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "[]";
    public DateTimeOffset SavedAtUtc { get; set; }
}
