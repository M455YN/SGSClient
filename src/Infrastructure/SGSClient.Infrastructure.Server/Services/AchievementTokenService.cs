using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Server.Services;

/// <summary>Token format: <c>sgs1.{base64url(userId:gameId:expUnix)}.{base64url(hmac)}</c>.</summary>
public sealed class AchievementTokenService : IAchievementTokenService
{
    private const string Prefix = "sgs1";
    private const int MinKeyLength = 32;

    private readonly byte[] _key;
    private readonly TimeSpan _lifetime;
    private readonly TimeProvider _time;

    public AchievementTokenService(
        IOptions<AchievementSettings> options,
        ILogger<AchievementTokenService> logger,
        TimeProvider? time = null)
    {
        var settings = options.Value;
        _time = time ?? TimeProvider.System;
        _lifetime = TimeSpan.FromHours(Math.Clamp(settings.TokenLifetimeHours, 1, 72));

        if (settings.SigningKey is { Length: >= MinKeyLength } configured)
        {
            _key = Encoding.UTF8.GetBytes(configured);
        }
        else
        {
            logger.LogWarning(
                "Achievements:SigningKey is missing or shorter than {Min} characters; using a random key. "
                + "Game tokens will stop working after a restart.",
                MinKeyLength);
            _key = RandomNumberGenerator.GetBytes(32);
        }
    }

    public AchievementSessionDto Issue(int userId, int gameId)
    {
        var expires = _time.GetUtcNow().Add(_lifetime);
        var payload = Encoding.UTF8.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"{userId}:{gameId}:{expires.ToUnixTimeSeconds()}"));
        var token = $"{Prefix}.{Base64Url.EncodeToString(payload)}.{Base64Url.EncodeToString(Sign(payload))}";
        return new AchievementSessionDto { GameId = gameId, Token = token, ExpiresAtUtc = expires };
    }

    public bool TryValidate(string? token, out int userId, out int gameId)
    {
        userId = 0;
        gameId = 0;

        var parts = token?.Split('.');
        if (parts is not [Prefix, var payloadPart, var macPart])
        {
            return false;
        }

        byte[] payload;
        byte[] mac;
        try
        {
            payload = Base64Url.DecodeFromChars(payloadPart);
            mac = Base64Url.DecodeFromChars(macPart);
        }
        catch (FormatException)
        {
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(Sign(payload), mac))
        {
            return false;
        }

        var fields = Encoding.UTF8.GetString(payload).Split(':');
        if (fields.Length != 3
            || !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var user)
            || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var game)
            || !long.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var expires)
            || expires <= _time.GetUtcNow().ToUnixTimeSeconds())
        {
            return false;
        }

        userId = user;
        gameId = game;
        return true;
    }

    private byte[] Sign(byte[] payload) => HMACSHA256.HashData(_key, payload);
}
