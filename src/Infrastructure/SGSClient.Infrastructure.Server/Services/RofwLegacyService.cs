using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SGSClient.Core.DTOs.Rofw;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Server.Services;

/// <summary>
/// Drop-in compatibility with the old SGSClient.Api /api/rofw/* endpoints
/// (Realm of Five Winds). Uses the existing SZYRROGI_AUTOBATTLER database.
/// </summary>
public sealed class RofwLegacyService(IConfiguration configuration) : IRofwLegacyService
{
    private readonly string? _connectionString =
        configuration.GetConnectionString("RofwDatabase")
        ?? configuration.GetConnectionString("ROFWConnection");

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

    public Task SubmitScoreAsync(RofwScoreRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            """
            INSERT INTO Scores (PlayerName, Score)
            VALUES (@playerName, @score);
            """,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@playerName", request.PlayerName);
                cmd.Parameters.AddWithValue("@score", request.Score);
            },
            cancellationToken);

    public async Task<IReadOnlyList<RofwScoreResponse>> GetScoresAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var take = limit <= 0 ? 100 : Math.Min(limit, 1000);
        var rows = new List<RofwScoreResponse>();
        await using var connection = Open();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT TOP (@limit)
              p.Id
            , p.LP
            FROM Players p
            ORDER BY p.LP DESC;
            """;
        cmd.Parameters.AddWithValue("@limit", take);
        await connection.OpenAsync(cancellationToken);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new RofwScoreResponse(reader.GetInt32(0), reader.GetInt32(1)));
        }

        return rows;
    }

    public Task UpdateAchievementsAsync(
        RofwUpdateAchievementsRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            """
            UPDATE Players
            SET Achievements = @value
            WHERE Id = @playerId;
            """,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@value", request.NewValue);
                cmd.Parameters.AddWithValue("@playerId", request.PlayerId);
            },
            cancellationToken);

    public Task UpdateFaceAsync(RofwUpdateFaceRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            """
            UPDATE Players
            SET Face = @face
            WHERE Id = @playerId;
            """,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@face", request.NewFaceId);
                cmd.Parameters.AddWithValue("@playerId", request.PlayerId);
            },
            cancellationToken);

    public Task SaveStatsAsync(RofwSaveStatsRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            """
            INSERT INTO Stats (PlayerId, Round, IsWin, Comp, Win, Lose, Fraction)
            VALUES (@playerId, @round, @isWin, @comp, @win, @lose, @fraction);
            """,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@playerId", request.PlayerId);
                cmd.Parameters.AddWithValue("@round", request.Round);
                cmd.Parameters.AddWithValue("@isWin", request.IsWin);
                cmd.Parameters.AddWithValue("@comp", (object?)request.Comp ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@win", request.Win);
                cmd.Parameters.AddWithValue("@lose", request.Lose);
                cmd.Parameters.AddWithValue("@fraction", (object?)request.Fraction ?? DBNull.Value);
            },
            cancellationToken);

    public Task UpdateLpAsync(RofwUpdateLpRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            """
            UPDATE Players
            SET LP = @lp
            WHERE Id = @playerId;
            """,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@lp", request.NewLP);
                cmd.Parameters.AddWithValue("@playerId", request.PlayerId);
            },
            cancellationToken);

    public async Task<RofwLoginResponse?> LoginAsync(
        RofwLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
              p.Id
            , p.Name
            , p.Email
            , p.Password
            , p.LP
            , p.Face
            , p.Synergies
            , p.Achievements
            FROM Players p
            WHERE p.Name = @name AND p.Password = @password;
            """;
        cmd.Parameters.AddWithValue("@name", request.Username);
        cmd.Parameters.AddWithValue("@password", request.Password);
        await connection.OpenAsync(cancellationToken);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new RofwLoginResponse(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? "" : reader.GetString(1),
            reader.IsDBNull(2) ? "" : reader.GetString(2),
            reader.IsDBNull(3) ? "" : reader.GetString(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.IsDBNull(6) ? "" : reader.GetString(6),
            reader.IsDBNull(7) ? "" : reader.GetString(7));
    }

    public Task RegisterAsync(RofwRegisterRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            """
            INSERT INTO Players (Name, Email, Password, LP, Face, Synergies, Achievements)
            VALUES (@name, @email, @password, 0, 0, @synergies, @achievements);
            """,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@name", request.Username);
                cmd.Parameters.AddWithValue("@email", request.Email);
                cmd.Parameters.AddWithValue("@password", request.Password);
                cmd.Parameters.AddWithValue("@synergies", "S");
                cmd.Parameters.AddWithValue("@achievements", "S");
            },
            cancellationToken);

    public Task UpdateSynergiesAsync(
        RofwUpdateSynergiesRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            """
            UPDATE Players
            SET Synergies = @value
            WHERE Id = @playerId;
            """,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@value", request.NewValue);
                cmd.Parameters.AddWithValue("@playerId", request.PlayerId);
            },
            cancellationToken);

    private SqlConnection Open()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Connection string 'RofwDatabase' is missing. Set ConnectionStrings:RofwDatabase.");
        }

        return new SqlConnection(_connectionString);
    }

    private async Task ExecuteAsync(
        string sql,
        Action<SqlCommand> bind,
        CancellationToken cancellationToken)
    {
        await using var connection = Open();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        bind(cmd);
        await connection.OpenAsync(cancellationToken);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
