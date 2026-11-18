using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence.Services;

public sealed class LobbyService(
    SgsServerDbContext db,
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

        await PurgeAsync(cancellationToken);
        // One open lobby per host and game: starting a new one replaces the old one.
        db.Lobbies.RemoveRange(await db.Lobbies
            .Where(l => l.HostUserId == hostUserId && l.GameId == request.GameId)
            .ToListAsync(cancellationToken));

        string newCode;
        do
        {
            newCode = NewCode();
        }
        while (await db.Lobbies.AnyAsync(l => l.Code == newCode, cancellationToken));

        var now = DateTimeOffset.UtcNow;
        var record = new LobbyEntity
        {
            Code = newCode,
            GameId = request.GameId,
            HostUserId = hostUserId,
            MaxPlayers = request.MaxPlayers,
            CreatedAtUtc = now,
            Address = address,
            Players = request.Players,
            LastSeenUtc = now,
            Members = [hostUserId],
            Version = Guid.NewGuid()
        };
        db.Lobbies.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        return (await ToDtosAsync([record], cancellationToken))[0];
    }

    public async Task<LobbyDto?> GetAsync(string code, CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(code, cancellationToken);
        return record is null ? null : (await ToDtosAsync([record], cancellationToken))[0];
    }

    public async Task<LobbyDto> UpdateAsync(
        int hostUserId,
        string code,
        UpdateLobbyRequest request,
        CancellationToken cancellationToken = default)
    {
        var record = await FindHostedAsync(hostUserId, code, cancellationToken);
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

        await SaveAsync(record, cancellationToken);
        return (await ToDtosAsync([record], cancellationToken))[0];
    }

    public async Task CloseAsync(int hostUserId, string code, CancellationToken cancellationToken = default)
    {
        var record = await FindHostedAsync(hostUserId, code, cancellationToken);
        db.Lobbies.Remove(record);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<NotificationDto> InviteAsync(
        int hostUserId,
        string code,
        int friendUserId,
        CancellationToken cancellationToken = default)
    {
        var record = await FindHostedAsync(hostUserId, code, cancellationToken);
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
        var record = await FindAsync(code, cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono lobby (mogło wygasnąć).");
        if (!record.Members.Contains(userId))
        {
            // The code works like an invite (e.g. pasted on Discord): owning the game is enough, no friendship needed.
            await EnsureOwnsGameAsync(userId, record.GameId, "Nie masz tej gry w bibliotece.", cancellationToken);
            if (record.Started)
            {
                throw new ArgumentException("Gra w tym lobby już wystartowała.");
            }

            if (record.Members.Count >= record.MaxPlayers)
            {
                throw new ArgumentException("Lobby jest pełne.");
            }

            record.Members = [.. record.Members, userId];
            record.Players = record.Members.Count;
        }

        record.LastSeenUtc = DateTimeOffset.UtcNow;
        await SaveAsync(record, cancellationToken);
        return (await ToDtosAsync([record], cancellationToken))[0];
    }

    public async Task<LobbyDto?> LeaveAsync(int userId, string code, CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(code, cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono lobby (mogło wygasnąć).");
        if (!record.Members.Contains(userId) && record.HostUserId != userId)
        {
            throw new UnauthorizedAccessException("Nie jesteś w tym lobby.");
        }

        RemoveMember(record, userId);
        if (record.Members.Count == 0)
        {
            db.Lobbies.Remove(record);
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (record.HostUserId == userId)
        {
            // The party stays together: the guest who joined first becomes the host.
            record.HostUserId = record.Members[0];
            record.Ready = record.Ready.Where(id => id != record.HostUserId).ToList();
        }

        await SaveAsync(record, cancellationToken);
        return (await ToDtosAsync([record], cancellationToken))[0];
    }

    public async Task<LobbyDto> StartAsync(int hostUserId, string code, CancellationToken cancellationToken = default)
    {
        var record = await FindHostedAsync(hostUserId, code, cancellationToken);
        if (record.Members.Any(id => id != record.HostUserId && !record.Ready.Contains(id)))
        {
            throw new ArgumentException("Nie wszyscy gracze są gotowi.");
        }

        record.Started = true;
        record.IsOpen = false;
        record.LastSeenUtc = DateTimeOffset.UtcNow;
        await SaveAsync(record, cancellationToken);
        return (await ToDtosAsync([record], cancellationToken))[0];
    }

    public async Task<LobbyDto> SetReadyAsync(int userId, string code, bool ready, CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(code, cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono lobby (mogło wygasnąć).");
        if (!record.Members.Contains(userId))
        {
            throw new UnauthorizedAccessException("Nie jesteś w tym lobby.");
        }

        if (record.HostUserId != userId)
        {
            record.Ready = ready
                ? record.Ready.Contains(userId) ? record.Ready : [.. record.Ready, userId]
                : record.Ready.Where(id => id != userId).ToList();
            await SaveAsync(record, cancellationToken);
        }

        return (await ToDtosAsync([record], cancellationToken))[0];
    }

    public async Task<LobbyDto> KickAsync(int hostUserId, string code, int userId, CancellationToken cancellationToken = default)
    {
        var record = await FindHostedAsync(hostUserId, code, cancellationToken);
        if (userId == hostUserId)
        {
            throw new ArgumentException("Nie możesz wyrzucić siebie. Wyjdź z lobby.");
        }

        if (!record.Members.Contains(userId))
        {
            throw new KeyNotFoundException("Tego gracza nie ma w lobby.");
        }

        RemoveMember(record, userId);
        await SaveAsync(record, cancellationToken);
        return (await ToDtosAsync([record], cancellationToken))[0];
    }

    public async Task<LobbyDto> FinishAsync(int hostUserId, string code, CancellationToken cancellationToken = default)
    {
        var record = await FindHostedAsync(hostUserId, code, cancellationToken);
        record.Started = false;
        record.IsOpen = true;
        record.Address = string.Empty;
        record.Ready = [];
        record.LastSeenUtc = DateTimeOffset.UtcNow;
        await SaveAsync(record, cancellationToken);
        return (await ToDtosAsync([record], cancellationToken))[0];
    }

    private static void RemoveMember(LobbyEntity record, int userId)
    {
        record.Members = record.Members.Where(id => id != userId).ToList();
        record.Ready = record.Ready.Where(id => id != userId).ToList();
        record.Players = record.Members.Count;
    }

    public async Task<IReadOnlyList<InvitableFriendDto>> ListInvitableAsync(
        int hostUserId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var record = await FindHostedAsync(hostUserId, code, cancellationToken);
        var overview = await friends.GetOverviewAsync(hostUserId, cancellationToken);
        var members = record.Members;

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
        await PurgeAsync(cancellationToken);
        var overview = await friends.GetOverviewAsync(userId, cancellationToken);
        var friendIds = overview.Friends.Select(f => f.Id).ToList();
        var visible = await db.Lobbies.AsNoTracking()
            .Where(l => friendIds.Contains(l.HostUserId) && l.IsOpen && !l.Started && l.Players < l.MaxPlayers)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ToListAsync(cancellationToken);
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

    private async Task<LobbyEntity?> FindAsync(string code, CancellationToken cancellationToken)
    {
        await PurgeAsync(cancellationToken);
        var key = (code ?? string.Empty).Trim().ToUpperInvariant();
        return await db.Lobbies.FirstOrDefaultAsync(l => l.Code == key, cancellationToken);
    }

    private async Task<LobbyEntity> FindHostedAsync(int hostUserId, string code, CancellationToken cancellationToken)
    {
        var record = await FindAsync(code, cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono lobby (mogło wygasnąć).");
        return record.HostUserId == hostUserId
            ? record
            : throw new UnauthorizedAccessException("To nie jest Twoje lobby.");
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow - ILobbyService.Lifetime;
        var stale = await db.Lobbies.Where(l => l.LastSeenUtc < cutoff).ToListAsync(cancellationToken);
        if (stale.Count > 0)
        {
            db.Lobbies.RemoveRange(stale);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another request purged or touched the same rows; the next call cleans up what is left.
                db.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>Saves a changed lobby; a concurrent change (two players taking the last slot) fails instead of overfilling it.</summary>
    private async Task SaveAsync(LobbyEntity record, CancellationToken cancellationToken)
    {
        record.Version = Guid.NewGuid();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ArgumentException("Lobby zmieniło się w tym samym momencie. Spróbuj ponownie.");
        }
    }

    private async Task<IReadOnlyList<LobbyDto>> ToDtosAsync(
        IReadOnlyList<LobbyEntity> records,
        CancellationToken cancellationToken)
    {
        var gameIds = records.Select(r => r.GameId).Distinct().ToList();
        var members = records.ToDictionary(r => r.Code, r => r.Members);
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
                .Select(id => new LobbyMemberDto
                {
                    UserId = id,
                    DisplayName = names.GetValueOrDefault(id),
                    IsReady = id == r.HostUserId || r.Ready.Contains(id)
                })
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
