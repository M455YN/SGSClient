using SGSClient.Core.Enums;

namespace SGSClient.Infrastructure.Server.Persistence.Entities;

public sealed class UserRelationshipEntity
{
    public int Id { get; set; }
    public int RequesterUserId { get; set; }
    public int AddresseeUserId { get; set; }
    public UserRelationshipKind Kind { get; set; }
    public UserRelationshipStatus Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? RespondedAtUtc { get; set; }

    public UserEntity? Requester { get; set; }
    public UserEntity? Addressee { get; set; }
}
