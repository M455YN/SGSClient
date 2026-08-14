namespace SGSClient.Core.DTOs.Rofw;

public sealed record RofwScoreRequest(string PlayerName, int Score);

public sealed record RofwScoreResponse(int Id, int LP);

public sealed record RofwUpdateAchievementsRequest(int PlayerId, string NewValue);

public sealed record RofwUpdateFaceRequest(int PlayerId, int NewFaceId);

public sealed record RofwSaveStatsRequest(
    int PlayerId,
    int Round,
    bool IsWin,
    string Comp,
    int Win,
    int Lose,
    string Fraction);

public sealed record RofwUpdateLpRequest(int PlayerId, int NewLP);

public sealed record RofwLoginRequest(string Username, string Password);

public sealed record RofwLoginResponse(
    int Id,
    string Name,
    string Email,
    string Password,
    int LP,
    int Face,
    string Synergies,
    string Achievements);

public sealed record RofwRegisterRequest(string Username, string Email, string Password);

public sealed record RofwUpdateSynergiesRequest(int PlayerId, string NewValue);
