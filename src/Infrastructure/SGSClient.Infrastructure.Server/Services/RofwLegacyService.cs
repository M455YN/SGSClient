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
            WHERE Id = @playerId
              AND (@onlyIfLonger = 0 OR LEN(@value) > LEN(ISNULL(Achievements, '')));
            """,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@value", request.NewValue);
                cmd.Parameters.AddWithValue("@playerId", request.PlayerId);
                cmd.Parameters.AddWithValue("@onlyIfLonger", request.OnlyIfLonger);
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
            reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
            reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
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

    public async Task<RofwRankingResponse> GetRankingAsync(
        int offset,
        int take,
        int playerId,
        CancellationToken cancellationToken = default)
    {
        offset = Math.Max(0, offset);
        take = take <= 0 ? 7 : Math.Min(take, 100);

        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);

        var ids = new List<int>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT Id
                FROM Players
                ORDER BY LP DESC
                OFFSET @offset ROWS FETCH NEXT @take ROWS ONLY;
                """;
            cmd.Parameters.AddWithValue("@offset", offset);
            cmd.Parameters.AddWithValue("@take", take);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetInt32(0));
            }
        }

        var position = -1;
        if (playerId > 0)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                WITH RankedPlayers AS (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY LP DESC) AS Position
                    FROM Players
                )
                SELECT Position
                FROM RankedPlayers
                WHERE Id = @playerId;
                """;
            cmd.Parameters.AddWithValue("@playerId", playerId);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result is not null && result != DBNull.Value)
            {
                position = Convert.ToInt32(result);
            }
        }

        var bestName = "";
        var bestWin = 0;
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT TOP 1 p.Name, s.Win
                FROM Stats s
                JOIN Players p ON s.PlayerId = p.Id
                ORDER BY s.Round DESC;
                """;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                bestName = reader.IsDBNull(0) ? "" : reader.GetString(0);
                bestWin = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            }
        }

        return new RofwRankingResponse(ids, position, bestName, bestWin);
    }

    public async Task<RofwPlayerCardResponse?> GetPlayerCardAsync(
        int playerId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
              p.Id
            , p.Name
            , p.Face
            , p.LP
            , (SELECT ROUND(AVG(CAST(s.Win AS float)), 1)
               FROM Stats s
               WHERE s.PlayerId = p.Id AND (s.Round - s.Win != 0 OR s.Lose = 3)) AS AvgWin
            , (SELECT TOP 1 s.Fraction
               FROM Stats s
               WHERE s.PlayerId = p.Id
               GROUP BY s.Fraction
               ORDER BY COUNT(*) DESC) AS Fraction
            FROM Players p
            WHERE p.Id = @playerId;
            """;
        cmd.Parameters.AddWithValue("@playerId", playerId);
        await connection.OpenAsync(cancellationToken);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new RofwPlayerCardResponse(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? "" : reader.GetString(1),
            reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
            reader.IsDBNull(4) ? 0 : Convert.ToDouble(reader.GetValue(4)),
            reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
            reader.IsDBNull(5) ? "00000" : reader.GetString(5));
    }

    public async Task<RofwOpponentsResponse> GetOpponentsAsync(
        RofwOpponentsRequest request,
        CancellationToken cancellationToken = default)
    {
        var rounds = Math.Clamp(request.Rounds, 0, 40);
        var items = new List<RofwOpponent>(rounds);

        await using var connection = Open();
        await connection.OpenAsync(cancellationToken);

        for (var round = 0; round < rounds; round++)
        {
            // Nazwa tabeli pochodzi tylko z AutoTable (stała lista), nigdy wprost z requestu.
            var table = AutoTable(request.LP, round, request.Ranked);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = request.Ranked
                ? $"""
                    SELECT TOP 1 Id, Version, Name, FaceId, LP, PlayerId, Comp, Round
                    FROM (
                        SELECT TOP 15 *
                        FROM {table}
                        WHERE Round = @round
                          AND Name != @playerName
                          AND PlayerId != @playerId
                        ORDER BY ABS(LP - @lp)
                    ) AS Top15Diff
                    ORDER BY NEWID();
                    """
                : $"""
                    SELECT TOP 1 Id, Version, Name, FaceId, LP, PlayerId, Comp, Round
                    FROM {table}
                    WHERE Round = @round
                      AND Name != @playerName
                      AND PlayerId != @playerId
                    ORDER BY NEWID();
                    """;
            cmd.Parameters.AddWithValue("@round", round);
            cmd.Parameters.AddWithValue("@playerName", request.PlayerName ?? "");
            cmd.Parameters.AddWithValue("@playerId", request.PlayerId);
            cmd.Parameters.AddWithValue("@lp", request.LP);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new RofwOpponent(
                    true,
                    Convert.ToInt32(reader["Id"]),
                    reader["Version"]?.ToString() ?? "",
                    reader["Name"]?.ToString() ?? "",
                    Convert.ToInt32(reader["FaceId"]),
                    Convert.ToInt32(reader["LP"]),
                    Convert.ToInt32(reader["PlayerId"]),
                    reader["Comp"]?.ToString() ?? "",
                    Convert.ToInt32(reader["Round"])));
            }
            else
            {
                items.Add(new RofwOpponent(false, 0, "", "", 0, 0, 0, "", round));
            }
        }

        return new RofwOpponentsResponse(items);
    }

    public Task AddGhostAsync(RofwAddGhostRequest request, CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            $"""
            INSERT INTO {AutoTable(request.LP, request.CurrentRound, request.Ranked)}
                (Version, Name, FaceId, Date, LP, PlayerId, Comp, Round)
            VALUES (@version, @name, @faceId, @date, @lp, @playerId, @comp, @round);
            """,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@version", request.Version ?? "");
                cmd.Parameters.AddWithValue("@name", request.Name ?? "");
                cmd.Parameters.AddWithValue("@faceId", request.FaceId);
                cmd.Parameters.AddWithValue("@date", DateTime.Now);
                cmd.Parameters.AddWithValue("@lp", request.Ranked ? request.LP : 0);
                cmd.Parameters.AddWithValue("@playerId", request.PlayerId);
                cmd.Parameters.AddWithValue("@comp", request.Comp ?? "");
                cmd.Parameters.AddWithValue("@round", request.CurrentRound - 1);
            },
            cancellationToken);

    /// <summary>Ta sama logika co EnemyManager.GetAutoName w grze.</summary>
    private static string AutoTable(int lp, int round, bool ranked)
    {
        if (!ranked)
        {
            return round < 6 ? "AutoNormalLow" : "AutoNormalHigh";
        }

        return (lp / 300) switch
        {
            0 => "AutoBronze",
            1 => "AutoSilver",
            2 => "AutoGold",
            3 => "AutoDiament",
            4 => "AutoRubin",
            _ => "AutoSigma",
        };
    }

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
