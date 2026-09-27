using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IChatService
{
    Task<IReadOnlyList<ChatThreadDto>> ListThreadsAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<ChatConversationDto> GetConversationAsync(
        int userId,
        int peerUserId,
        CancellationToken cancellationToken = default);

    Task<ChatMessageDto> SendAsync(
        int senderUserId,
        int recipientUserId,
        string text,
        CancellationToken cancellationToken = default);
}
