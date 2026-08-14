using SGSClient.Core.DTOs.Rofw;

namespace SGSClient.Core.Interfaces;

public interface IRofwLegacyService
{
    bool IsConfigured { get; }

    Task SubmitScoreAsync(RofwScoreRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RofwScoreResponse>> GetScoresAsync(int limit, CancellationToken cancellationToken = default);

    Task UpdateAchievementsAsync(RofwUpdateAchievementsRequest request, CancellationToken cancellationToken = default);

    Task UpdateFaceAsync(RofwUpdateFaceRequest request, CancellationToken cancellationToken = default);

    Task SaveStatsAsync(RofwSaveStatsRequest request, CancellationToken cancellationToken = default);

    Task UpdateLpAsync(RofwUpdateLpRequest request, CancellationToken cancellationToken = default);

    Task<RofwLoginResponse?> LoginAsync(RofwLoginRequest request, CancellationToken cancellationToken = default);

    Task RegisterAsync(RofwRegisterRequest request, CancellationToken cancellationToken = default);

    Task UpdateSynergiesAsync(RofwUpdateSynergiesRequest request, CancellationToken cancellationToken = default);
}
