using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Server.Persistence.Services;

/// <summary>
/// Lobbies live in memory: they are short-lived and only help friends find a host.
/// shortcut: a restart drops them and several API instances do not share them, move to the database or a cache then.
/// </summary>
public sealed class LobbyStore
{
    public ConcurrentDictionary<string, LobbyRecord> Lobbies { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class LobbyRecord
{
    public required string Code { get; init; }
    public required int GameId { get; init; }
    public required int HostUserId { get; init; }
    public required int MaxPlayers { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public string Address { get; set; } = string.Empty;
    public int Players { get; set; }
    public bool IsOpen { get; set; } = true;
    public DateTimeOffset LastSeenUtc { get; set; }
}

public sealed class LobbyService(
    SgsServerDbContext db,
    LobbyStore store,
    IFriendsService friends,
    INotificationService notifications) : ILobbyService
{
    // No 0/O/1/I so a code read aloud or typed from a screenshot is hard to get wrong.
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 6;
    private const int MaxPlayersLimit = 16;
    private const int MaxAddressLength = 100;

    public async Task<LobbyDto> CreateAsync(
        int hostUserId,
        CreateLobbyRequest request,
        CancellationToken cancellationToken = default)
    {
        var address = NormalizeAddress(request.Address);
        if (request.MaxPlayers is < 1 or > MaxPlayersLimit)
        {
            throw new ArgumentException($"Liczba graczy musi mieścić się w zakresie 1–{MaxPlayersLimit}.");
        }

        if (request.Players < 1 || request.Players > request.MaxPlayers)
        {
            throw new ArgumentException("Liczba graczy w lobby jest nieprawidłowa.");
        }

        var game = await db.Games.AsNoTracking()
            .Where(g => g.Id == request.GameId)
            .Select(g => new { g.UserId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono gry.");

        var owns = game.UserId == hostUserId
                   || await db.UserGameEntitlements.AsNoTracking()
                       .AnyAsync(e => e.UserId == hostUserId && e.GameId == request.GameId, cancellationToken);
        if (!owns)
        {
            throw new UnauthorizedAccessException("Nie masz tej gry w bibliotece.");
        }

        Purge();
        // One open lobby per host and game: starting a new one replaces the old one.
        foreach (var old in store.Lobbies.Values.Where(l => l.HostUserId == hostUserId && l.GameId == request.GameId))
        {
            store.Lobbies.TryRemove(old.Code, out _);
        }

        var now = DateTimeOffset.UtcNow;
        LobbyRecord record;
        do
        {
            record = new LobbyRecord
            {
                Code = NewCode(),
                GameId = request.GameId,
                HostUserId = hostUserId,
                MaxPlayers = request.MaxPlayers,
                CreatedAtUtc = now,
                Address = address,
                Players = request.Players,
                LastSeenUtc = now
            };
        }
        while (!store.Lobbies.TryAdd(record.Code, record));

        return (await ToDtosAsync([record], cancellationToken))[0];
    }

    public async Task<LobbyDto?> GetAsync(string code, CancellationToken cancellationToken = default)
    {
        var record = Find(code);
        return record is null ? null : (await ToDtosAsync([record], cancellationToken))[0];
    }

    public async Task<LobbyDto> UpdateAsync(
        int hostUserId,
        string code,
        UpdateLobbyRequest request,
        CancellationToken cancellationToken = default)
    {
        var record = FindHosted(hostUserId, code);
        if (request.Address is not null)
        {
            record.Address = NormalizeAddress(request.Address);
        }

        if (request.Players is int players)
        {
            if (players < 1 || players > record.MaxPlayers)
            {
                throw new ArgumentException("Liczba graczy w lobby jest nieprawidłowa.");
            }

            record.Players = players;
        }

        if (request.IsOpen is bool open)
        {
            record.IsOpen = open;
        }

        record.LastSeenUtc = DateTimeOffset.UtcNow;
        return (await ToDtosAsync([record], cancellationToken))[0];
    }

    public Task CloseAsync(int hostUserId, string code, CancellationToken cancellationToken = default)
    {
        var record = FindHosted(hostUserId, code);
        store.Lobbies.TryRemove(record.Code, out _);
        return Task.CompletedTask;
    }

    public async Task<NotificationDto> InviteAsync(
        int hostUserId,
        string code,
        int friendUserId,
        CancellationToken cancellationToken = default)
    {
        var record = FindHosted(hostUserId, code);
        var overview = await friends.GetOverviewAsync(hostUserId, cancellationToken);
        if (overview.Friends.All(f => f.Id != friendUserId))
        {
            throw new UnauthorizedAccessException("Zaprosić można tylko znajomego.");
        }

        var lobby = (await ToDtosAsync([record], cancellationToken))[0];
        return await notifications.CreateAsync(
            friendUserId,
            ILobbyService.InviteNotificationKind,
            "Zaproszenie do gry",
            $"{lobby.HostName ?? "Gracz"} zaprasza Cię do gry {lobby.GameTitle ?? "SGS"}.",
            System.Text.Json.JsonSerializer.Serialize(new { code = record.Code, gameId = record.GameId }),
            cancellationToken);
    }

    public async Task<IReadOnlyList<LobbyDto>> ListFriendLobbiesAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        Purge();
        var overview = await friends.GetOverviewAsync(userId, cancellationToken);
        var friendIds = overview.Friends.Select(f => f.Id).ToHashSet();
        var visible = store.Lobbies.Values
            .Where(l => friendIds.Contains(l.HostUserId) && l.IsOpen && l.Players < l.MaxPlayers)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToList();
        return await ToDtosAsync(visible, cancellationToken);
    }

    private LobbyRecord? Find(string code)
    {
        Purge();
        return store.Lobbies.TryGetValue((code ?? string.Empty).Trim(), out var record) ? record : null;
    }

    private LobbyRecord FindHosted(int hostUserId, string code)
    {
        var record = Find(code) ?? throw new KeyNotFoundException("Nie znaleziono lobby (mogło wygasnąć).");
        return record.HostUserId == hostUserId
            ? record
            : throw new UnauthorizedAccessException("To nie jest Twoje lobby.");
    }

    private void Purge()
    {
        var cutoff = DateTimeOffset.UtcNow - ILobbyService.Lifetime;
        foreach (var stale in store.Lobbies.Values.Where(l => l.LastSeenUtc < cutoff))
        {
            store.Lobbies.TryRemove(stale.Code, out _);
        }
    }

    private async Task<IReadOnlyList<LobbyDto>> ToDtosAsync(
        IReadOnlyList<LobbyRecord> records,
        CancellationToken cancellationToken)
    {
        var gameIds = records.Select(r => r.GameId).Distinct().ToList();
        var userIds = records.Select(r => r.HostUserId).Distinct().ToList();
        var titles = await db.Games.AsNoTracking()
            .Where(g => gameIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Title, cancellationToken);
        var names = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        return records.Select(r => new LobbyDto
        {
            Code = r.Code,
            GameId = r.GameId,
            GameTitle = titles.GetValueOrDefault(r.GameId),
            HostUserId = r.HostUserId,
            HostName = names.GetValueOrDefault(r.HostUserId),
            Address = r.Address,
            Players = r.Players,
            MaxPlayers = r.MaxPlayers,
            IsOpen = r.IsOpen,
            CreatedAtUtc = r.CreatedAtUtc
        }).ToList();
    }

    private static string NormalizeAddress(string? address)
    {
        var value = (address ?? string.Empty).Trim();
        if (value.Length == 0 || value.Length > MaxAddressLength || value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            throw new ArgumentException("Podaj adres hosta, np. 192.168.0.5:7777.");
        }

        return value;
    }

    private static string NewCode() =>
        string.Create(CodeLength, 0, static (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
            }
        });
}
