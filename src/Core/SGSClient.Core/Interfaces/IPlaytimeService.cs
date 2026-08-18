using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IPlaytimeService
{
    Task<IReadOnlyList<PlaytimeDto>> GetAllAsync(int userId, CancellationToken cancellationToken = default);

    Task<PlaytimeDto?> GetAsync(int userId, int gameId, CancellationToken cancellationToken = default);

    Task<PlaytimeDto> RecordSessionAsync(
        int userId,
        RecordPlaytimeSessionRequest request,
        CancellationToken cancellationToken = default);
}
