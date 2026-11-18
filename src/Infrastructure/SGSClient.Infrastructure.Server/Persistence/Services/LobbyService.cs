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

    // Host is always the first member. Join/leave set Players = Members.Count; a game-created lobby keeps
    // only the host here and its game reports Players through PATCH as before. Lock the record to change it.
    public List<int> Members { get; } = [];
    public bool Started { get; set; }
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
        // A lobby formed in the client has no address until the host's game starts and PATCHes it.
        var address = string.IsNullOrEmpty(request.Address) ? string.Empty : NormalizeAddress(request.Address);
        if (request.MaxPlayers is < 1 or > MaxPlayersLimit)
        {
            throw new ArgumentException($"Liczba graczy musi mieścić się w zakresie 1–{MaxPlayersLimit}.");
        }

        if (request.Players < 1 || request.Players > request.MaxPlayers)
        {
            throw new ArgumentException("Liczba graczy w lobby jest nieprawidłowa.");
        }

        await EnsureOwnsGameAsync(hostUserId, request.GameId, "Nie masz tej gry w bibliotece.", cancellationToken);

        // The game's manifest caps the lobby; a client could otherwise ask for more slots than the game handles.
        var declaredMax = await db.Games.AsNoTracking()
            .Where(g => g.Id == request.GameId)
            .Select(g => g.LobbyMaxPlayers)
            .FirstOrDefaultAsync(cancellationToken);
        if (request.MaxPlayers > declaredMax)
        {
            throw new ArgumentException($"Ta gra obsługuje najwyżej {declaredMax} graczy.");
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
                LastSeenUtc = now,
                Members = { hostUserId }
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

        await EnsureOwnsGameAsync(friendUserId, record.GameId, "Znajomy nie ma tej gry w bibliotece.", cancellationToken);

        var lobby = (await ToDtosAsync([record], cancellationToken))[0];
        return await notifications.CreateAsync(
            friendUserId,
            ILobbyService.InviteNotificationKind,
            "Zaproszenie do gry",
            $"{lobby.HostName ?? "Gracz"} zaprasza Cię do gry {lobby.GameTitle ?? "SGS"}.",
            System.Text.Json.JsonSerializer.Serialize(new { code = record.Code, gameId = record.GameId }),
            cancellationToken);
    }

    public async Task<LobbyDto> JoinAsync(int userId, string code, CancellationToken cancellationToken = default)
    {
        var record = Find(code) ?? throw new KeyNotFoundException("Nie znaleziono lobby (mogło wygasnąć).");
        if (!record.Members.Contains(userId))
        {
            var overview = await friends.GetOverviewAsync(record.HostUserId, cancellationToken);
            if (overview.Friends.All(f => f.Id != userId))
            {
                throw new UnauthorizedAccessException("Dołączyć można tylko do lobby znajomego.");
            }

            await EnsureOwnsGameAsync(userId, record.GameId, "Nie masz tej gry w bibliotece.", cancellationToken);
            lock (record)
            {
                if (record.Started)
                {
                    throw new ArgumentException("Gra w tym lobby już wystartowała.");
                }

                if (!record.Members.Contains(userId))
                {
                    if (record.Members.Count >= record.MaxPlayers)
                    {
                        throw new ArgumentException("Lobby jest pełne.");
                    }

                    record.Members.Add(userId);
                    record.Players = record.Members.Count;
                }
            }
        }

        record.LastSeenUtc = DateTimeOffset.UtcNow;
        return (await ToDtosAsync([record], cancellationToken))[0];
    }

    public async Task<LobbyDto?> LeaveAsync(int userId, string code, CancellationToken cancellationToken = default)
    {
        var record = Find(code) ?? throw new KeyNotFoundException("Nie znaleziono lobby (mogło wygasnąć).");
        if (record.HostUserId == userId)
        {
            store.Lobbies.TryRemove(record.Code, out _);
            return null;
        }

        lock (record)
        {
            if (!record.Members.Remove(userId))
            {
                throw new UnauthorizedAccessException("Nie jesteś w tym lobby.");
            }

            record.Players = record.Members.Count;
        }

        return (await ToDtosAsync([record], cancellationToken))[0];
    }

    public async Task<LobbyDto> StartAsync(int hostUserId, string code, CancellationToken cancellationToken = default)
    {
        var record = FindHosted(hostUserId, code);
        record.Started = true;
        record.IsOpen = false;
        record.LastSeenUtc = DateTimeOffset.UtcNow;
        return (await ToDtosAsync([record], cancellationToken))[0];
    }

    public async Task<IReadOnlyList<InvitableFriendDto>> ListInvitableAsync(
        int hostUserId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var record = FindHosted(hostUserId, code);
        var overview = await friends.GetOverviewAsync(hostUserId, cancellationToken);
        List<int> members;
        lock (record)
        {
            members = record.Members.ToList();
        }

        var candidates = overview.Friends.Where(f => !members.Contains(f.Id)).ToList();
        var ids = candidates.Select(f => f.Id).ToList();
        var ownerId = await db.Games.AsNoTracking()
            .Where(g => g.Id == record.GameId)
            .Select(g => g.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        var entitled = await db.UserGameEntitlements.AsNoTracking()
            .Where(e => e.GameId == record.GameId && ids.Contains(e.UserId))
            .Select(e => e.UserId)
            .ToListAsync(cancellationToken);
        return candidates
            .Where(f => f.Id == ownerId || entitled.Contains(f.Id))
            .Select(f => new InvitableFriendDto { UserId = f.Id, DisplayName = f.DisplayName, IsOnline = f.IsOnline })
            .ToList();
    }

    public async Task<IReadOnlyList<LobbyDto>> ListFriendLobbiesAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        Purge();
        var overview = await friends.GetOverviewAsync(userId, cancellationToken);
        var friendIds = overview.Friends.Select(f => f.Id).ToHashSet();
        var visible = store.Lobbies.Values
            .Where(l => friendIds.Contains(l.HostUserId) && l.IsOpen && !l.Started && l.Players < l.MaxPlayers)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToList();
        return await ToDtosAsync(visible, cancellationToken);
    }

    /// <summary>The game's author or anyone with an entitlement row owns it.</summary>
    private async Task EnsureOwnsGameAsync(int userId, int gameId, string error, CancellationToken cancellationToken)
    {
        var game = await db.Games.AsNoTracking()
            .Where(g => g.Id == gameId)
            .Select(g => new { g.UserId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono gry.");

        var owns = game.UserId == userId
                   || await db.UserGameEntitlements.AsNoTracking()
                       .AnyAsync(e => e.UserId == userId && e.GameId == gameId, cancellationToken);
        if (!owns)
        {
            throw new UnauthorizedAccessException(error);
        }
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
        var members = records.ToDictionary(r => r.Code, r => { lock (r) { return r.Members.ToList(); } });
        var userIds = members.Values.SelectMany(m => m).Concat(records.Select(r => r.HostUserId)).Distinct().ToList();
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
            CreatedAtUtc = r.CreatedAtUtc,
            Members = members[r.Code]
                .Select(id => new LobbyMemberDto { UserId = id, DisplayName = names.GetValueOrDefault(id) })
                .ToList(),
            Started = r.Started
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
