using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SGSClient.Core.Auth;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Packaging;
using SGSClient.Infrastructure.Server.Services;

namespace SGSClient.Api.Controllers;

[ApiController]
[Route("api/me")]
public sealed class MeController(
    IUserService users,
    IUserAvatarService avatars,
    IGameRepository games,
    IPlaytimeService playtime,
    IAdminAuthorization adminAuth,
    IOptions<AzureAdSettings> azureOptions,
    IOptions<GameStorageSettings> storageOptions) : ControllerBase
{
    // Do NOT use [Authorize] here: when AzureAd is disabled, no auth scheme is registered
    // and [Authorize] throws 500. Mirror GamesController — AllowAnonymous + ResolveAzureSubAsync.

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileDto>> GetMe(CancellationToken cancellationToken)
    {
        var resolved = await ResolveIdentityAsync(cancellationToken);
        if (resolved is null)
        {
            return Unauthorized();
        }

        var profile = await users.UpsertFromClaimsAsync(
            resolved.AzureSub,
            resolved.DisplayName,
            resolved.Email,
            resolved.AvatarFromIdp,
            cancellationToken);
        return Ok(WithAdminFlag(profile));
    }

    [HttpPatch]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> UpdateMe(
        [FromBody] UpdateProfileRequestDto request,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveIdentityAsync(cancellationToken);
        if (resolved is null)
        {
            return Unauthorized();
        }

        try
        {
            var profile = await users.UpdateProfileAsync(resolved.AzureSub, request, cancellationToken);
            return profile is null ? NotFound() : Ok(WithAdminFlag(profile));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    [HttpPut("tag")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> ClaimPlayerTag(
        [FromBody] ClaimPlayerTagRequestDto request,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveIdentityAsync(cancellationToken);
        if (resolved is null)
        {
            return Unauthorized();
        }

        await users.UpsertFromClaimsAsync(
            resolved.AzureSub,
            resolved.DisplayName,
            resolved.Email,
            avatarUrl: null,
            cancellationToken);

        try
        {
            var profile = await users.ClaimPlayerTagAsync(resolved.AzureSub, request.Tag, cancellationToken);
            return profile is null ? NotFound() : Ok(WithAdminFlag(profile));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    [HttpGet("tag-available")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PlayerTagAvailabilityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PlayerTagAvailabilityDto>> CheckPlayerTag(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveIdentityAsync(cancellationToken);
        if (resolved is null)
        {
            return Unauthorized();
        }

        var profile = await users.GetByAzureSubAsync(resolved.AzureSub, cancellationToken);
        return Ok(await users.CheckPlayerTagAsync(q ?? string.Empty, profile?.Id, cancellationToken));
    }

    [HttpPut("avatar")]
    [AllowAnonymous]
    [RequestSizeLimit(UserAvatarService.MaxAvatarBytes + (1L * 1024 * 1024))]
    [RequestFormLimits(MultipartBodyLengthLimit = UserAvatarService.MaxAvatarBytes + (1L * 1024 * 1024))]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> UploadAvatar(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveIdentityAsync(cancellationToken);
        if (resolved is null)
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { errors = new[] { "Dodaj plik zdjęcia." } });
        }

        // Ensure the user row exists before writing the file (same as GetMe).
        await users.UpsertFromClaimsAsync(
            resolved.AzureSub,
            resolved.DisplayName,
            resolved.Email,
            avatarUrl: null,
            cancellationToken);

        try
        {
            await using var stream = file.OpenReadStream();
            var profile = await avatars.SetAvatarAsync(
                resolved.AzureSub,
                stream,
                file.FileName,
                cancellationToken);
            return profile is null ? NotFound() : Ok(WithAdminFlag(profile));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { errors = new[] { ex.Message } });
        }
    }

    [HttpDelete("avatar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> ClearAvatar(CancellationToken cancellationToken)
    {
        var resolved = await ResolveIdentityAsync(cancellationToken);
        if (resolved is null)
        {
            return Unauthorized();
        }

        var profile = await avatars.ClearAvatarAsync(resolved.AzureSub, cancellationToken);
        return profile is null ? NotFound() : Ok(WithAdminFlag(profile));
    }

    [HttpGet("avatars/{userId:int}/{fileName}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DownloadAvatar(int userId, string fileName)
    {
        if (userId <= 0
            || string.IsNullOrWhiteSpace(fileName)
            || fileName.Contains("..", StringComparison.Ordinal)
            || fileName.Contains('/')
            || fileName.Contains('\\')
            || !fileName.StartsWith("avatar.", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext) || !SgsPackageRules.AllowedImageExtensions.Contains(ext))
        {
            return NotFound();
        }

        var root = Path.GetFullPath(Path.Combine(storageOptions.Value.StoragePath, "users", userId.ToString()));
        var path = Path.GetFullPath(Path.Combine(root, fileName));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(path))
        {
            return NotFound();
        }

        var contentType = ext.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };

        return PhysicalFile(path, contentType);
    }

    [HttpGet("games")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<GameDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<GameDto>>> GetMyGames(CancellationToken cancellationToken)
    {
        var ownerUserId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (ownerUserId is null)
        {
            return Unauthorized();
        }

        var result = await games.GetOwnedGamesAsync(ownerUserId.Value, cancellationToken);
        return Ok(result);
    }

    [HttpGet("playtime")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<PlaytimeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PlaytimeDto>>> GetPlaytime(CancellationToken cancellationToken)
    {
        var ownerUserId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (ownerUserId is null)
        {
            return Unauthorized();
        }

        var result = await playtime.GetAllAsync(ownerUserId.Value, cancellationToken);
        return Ok(result);
    }

    [HttpGet("playtime/{gameId:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PlaytimeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlaytimeDto>> GetPlaytimeForGame(int gameId, CancellationToken cancellationToken)
    {
        var ownerUserId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (ownerUserId is null)
        {
            return Unauthorized();
        }

        var result = await playtime.GetAsync(ownerUserId.Value, gameId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("playtime/sessions")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PlaytimeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlaytimeDto>> RecordPlaytimeSession(
        [FromBody] RecordPlaytimeSessionRequest request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (ownerUserId is null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await playtime.RecordSessionAsync(ownerUserId.Value, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private UserProfileDto WithAdminFlag(UserProfileDto profile) => new()
    {
        Id = profile.Id,
        AzureSub = profile.AzureSub,
        DisplayName = profile.DisplayName,
        Email = EmailClaimHelper.Normalize(profile.Email) ?? profile.Email,
        AvatarUrl = profile.AvatarUrl,
        Bio = profile.Bio,
        Location = profile.Location,
        PlayerTag = profile.PlayerTag,
        CreatedAtUtc = profile.CreatedAtUtc,
        LastLoginUtc = profile.LastLoginUtc,
        Role = adminAuth.ResolveRole(profile.Role, profile.Email),
        IsAdmin = adminAuth.IsAdmin(User, profile.Email, profile.Role)
    };

    private async Task<ResolvedIdentity?> ResolveIdentityAsync(CancellationToken cancellationToken)
    {
        var azure = azureOptions.Value;
        if (!azure.IsConfigured)
        {
            // Same fallback as GamesController (owner user id 1).
            var local = await users.GetByIdAsync(1, cancellationToken);
            if (local is not null)
            {
                return new ResolvedIdentity(local.AzureSub, local.DisplayName, local.Email, AvatarFromIdp: null);
            }

            return new ResolvedIdentity("local-dev", "Player", Email: null, AvatarFromIdp: null);
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var sub = GetSub();
        if (sub is null)
        {
            return null;
        }

        var displayName = User.FindFirstValue("name")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? "Player";
        var email = EmailClaimHelper.FromPrincipal(User);
        var avatar = User.FindFirstValue("picture");
        return new ResolvedIdentity(sub, displayName, email, avatar);
    }

    private async Task<int?> ResolveOwnerUserIdAsync(CancellationToken cancellationToken)
    {
        var azure = azureOptions.Value;
        if (!azure.IsConfigured)
        {
            return 1;
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var sub = GetSub();
        if (sub is null)
        {
            return null;
        }

        var displayName = User.FindFirstValue("name")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? "Player";
        var email = EmailClaimHelper.FromPrincipal(User);
        var profile = await users.UpsertFromClaimsAsync(sub, displayName, email, avatarUrl: null, cancellationToken);
        return profile.Id;
    }

    private string? GetSub() =>
        User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    private sealed record ResolvedIdentity(
        string AzureSub,
        string DisplayName,
        string? Email,
        string? AvatarFromIdp);
}
