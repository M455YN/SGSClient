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
    IAdminAuthorization adminAuth,
    IOptions<AzureAdSettings> azureOptions,
    IOptions<GameStorageSettings> storageOptions) : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileDto>> GetMe(CancellationToken cancellationToken)
    {
        var sub = GetSub();
        if (sub is null)
        {
            return Unauthorized();
        }

        var displayName = User.FindFirstValue("name")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? "Player";
        var email = EmailClaimHelper.FromPrincipal(User);
        var avatar = User.FindFirstValue("picture");

        var profile = await users.UpsertFromClaimsAsync(sub, displayName, email, avatar, cancellationToken);
        return Ok(WithAdminFlag(profile));
    }

    [HttpPatch]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> UpdateMe(
        [FromBody] UpdateProfileRequestDto request,
        CancellationToken cancellationToken)
    {
        var sub = GetSub();
        if (sub is null)
        {
            return Unauthorized();
        }

        var profile = await users.UpdateProfileAsync(sub, request, cancellationToken);
        return profile is null ? NotFound() : Ok(WithAdminFlag(profile));
    }

    [HttpPut("avatar")]
    [Authorize]
    // Multipart overhead — limit must be above MaxAvatarBytes.
    [RequestSizeLimit(UserAvatarService.MaxAvatarBytes + (1L * 1024 * 1024))]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> UploadAvatar(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        var sub = GetSub();
        if (sub is null)
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { errors = new[] { "Dodaj plik zdjęcia." } });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var profile = await avatars.SetAvatarAsync(sub, stream, file.FileName, cancellationToken);
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
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> ClearAvatar(CancellationToken cancellationToken)
    {
        var sub = GetSub();
        if (sub is null)
        {
            return Unauthorized();
        }

        var profile = await avatars.ClearAvatarAsync(sub, cancellationToken);
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

    private UserProfileDto WithAdminFlag(UserProfileDto profile) => new()
    {
        Id = profile.Id,
        AzureSub = profile.AzureSub,
        DisplayName = profile.DisplayName,
        Email = EmailClaimHelper.Normalize(profile.Email) ?? profile.Email,
        AvatarUrl = profile.AvatarUrl,
        Bio = profile.Bio,
        Location = profile.Location,
        CreatedAtUtc = profile.CreatedAtUtc,
        LastLoginUtc = profile.LastLoginUtc,
        IsAdmin = adminAuth.IsAdmin(User, profile.Email)
    };

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
}
