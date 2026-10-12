using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

/// <summary>
/// Signed, expiring tokens that let a launched game post achievements for one user and one game
/// without ever seeing the user's sign-in token.
/// </summary>
public interface IAchievementTokenService
{
    AchievementSessionDto Issue(int userId, int gameId);

    bool TryValidate(string? token, out int userId, out int gameId);
}
