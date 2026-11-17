using SGSClient.Core.Localization;
namespace SGSClient.Core.DTOs;

public sealed class NotificationDto
{
    /// <summary>Client-only notice about a game pack/upload; offers a jump back to the publish form.</summary>
    public const string PublishKind = "publish";

    [System.Text.Json.Serialization.JsonIgnore]
    public bool OpensPublishForm => Kind == PublishKind;

    public long Id { get; init; }
    public required string Kind { get; init; }
    public required string Title { get => Loc.TS(_title); init => _title = value; }
    public string? Body { get => _body is null ? null : Loc.TS(_body); init => _body = value; }

    private string _title = string.Empty;
    private string? _body;
    public string? DataJson { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? ReadAtUtc { get; init; }
    public bool IsRead => ReadAtUtc is not null;
}
