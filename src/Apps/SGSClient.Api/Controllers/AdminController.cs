using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SGSClient.Core.Auth;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Packaging;

namespace SGSClient.Api.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController(
    IGameRepository games,
    IGameMediaService media,
    IUserService users,
    IAdminAuthorization adminAuth,
    IOptions<AzureAdSettings> azureOptions) : ControllerBase
{
    [HttpGet("games")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<GameDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<GameDto>>> GetGames(CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken))
        {
            return ForbidOrUnauthorized();
        }

        var result = await games.GetAllGamesForAdminAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPatch("games/{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameDto>> UpdateGame(
        int id,
        [FromBody] UpdateGameRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken))
        {
            return ForbidOrUnauthorized();
        }

        try
        {
            var updated = await games.UpdateGameAsAdminAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    [HttpGet("games/{id:int}/stats")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GameStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameStatsDto>> GetGameStats(int id, CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken))
        {
            return ForbidOrUnauthorized();
        }

        var stats = await games.GetGameStatsAsync(id, cancellationToken);
        return stats is null ? NotFound() : Ok(stats);
    }

    [HttpGet("games/{id:int}/versions")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<GameBuildVersionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<GameBuildVersionDto>>> GetGameVersions(
        int id,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken))
        {
            return ForbidOrUnauthorized();
        }

        var versions = await games.GetVersionHistoryAsync(id, cancellationToken);
        return Ok(versions);
    }

    [HttpPatch("games/{id:int}/versions/{versionId:int}/notes")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GameBuildVersionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameBuildVersionDto>> UpdateVersionNotes(
        int id,
        int versionId,
        [FromBody] UpdateVersionNotesRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken))
        {
            return ForbidOrUnauthorized();
        }

        try
        {
            var updated = await games.UpdateVersionNotesAsAdminAsync(
                id,
                versionId,
                request.ReleaseNotes,
                cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    [HttpDelete("games/{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGame(int id, CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken))
        {
            return ForbidOrUnauthorized();
        }

        try
        {
            var deleted = await games.DeleteGameAsAdminAsync(id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    [HttpGet("users")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<AdminUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> GetUsers(CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken))
        {
            return ForbidOrUnauthorized();
        }

        var result = await users.GetAllUsersAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPatch("users/{id:int}/role")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AdminUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserDto>> UpdateUserRole(
        int id,
        [FromBody] UpdateUserRoleRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken))
        {
            return ForbidOrUnauthorized();
        }

        if (!Enum.IsDefined(request.Role))
        {
            return BadRequest(new { errors = new[] { "Nieznana rola." } });
        }

        var caller = await ResolveAdminCallerAsync(cancellationToken);
        if (azureOptions.Value.IsConfigured && caller is null)
        {
            return ForbidOrUnauthorized();
        }

        // Local dev without Azure has no signed-in account; treat it as the owner.
        var callerRole = caller?.Role ?? UserRole.SuperAdmin;

        if (caller is not null && caller.Id == id)
        {
            return BadRequest(new { errors = new[] { "Nie możesz zmienić własnej roli." } });
        }

        var target = await users.GetByIdAsync(id, cancellationToken);
        if (target is null)
        {
            return NotFound();
        }

        if (callerRole < UserRole.SuperAdmin && (target.Role >= UserRole.Admin || request.Role >= UserRole.Admin))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { errors = new[] { "Tylko superadmin może nadawać i odbierać rolę admina." } });
        }

        try
        {
            var updated = await users.SetUserRoleAsync(id, request.Role, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    [HttpPut("games/{id:int}/media/cover")]
    [AllowAnonymous]
    [RequestSizeLimit(SgsPackageRules.MaxMediaFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = SgsPackageRules.MaxMediaFileBytes)]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> UploadCover(int id, IFormFile? file, CancellationToken cancellationToken) =>
        UploadMediaAsync(id, file, AdminMediaKind.Cover, cancellationToken);

    [HttpDelete("games/{id:int}/media/cover")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> ClearCover(int id, CancellationToken cancellationToken) =>
        ClearMediaAsync(id, AdminMediaKind.Cover, cancellationToken);

    [HttpPut("games/{id:int}/media/header")]
    [AllowAnonymous]
    [RequestSizeLimit(SgsPackageRules.MaxMediaFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = SgsPackageRules.MaxMediaFileBytes)]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> UploadHeader(int id, IFormFile? file, CancellationToken cancellationToken) =>
        UploadMediaAsync(id, file, AdminMediaKind.Header, cancellationToken);

    [HttpDelete("games/{id:int}/media/header")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> ClearHeader(int id, CancellationToken cancellationToken) =>
        ClearMediaAsync(id, AdminMediaKind.Header, cancellationToken);

    [HttpPut("games/{id:int}/media/intro")]
    [AllowAnonymous]
    [RequestSizeLimit(SgsPackageRules.MaxMediaFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = SgsPackageRules.MaxMediaFileBytes)]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> UploadIntro(int id, IFormFile? file, CancellationToken cancellationToken) =>
        UploadMediaAsync(id, file, AdminMediaKind.Intro, cancellationToken);

    [HttpDelete("games/{id:int}/media/intro")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> ClearIntro(int id, CancellationToken cancellationToken) =>
        ClearMediaAsync(id, AdminMediaKind.Intro, cancellationToken);

    [HttpPost("games/{id:int}/media/screenshots")]
    [AllowAnonymous]
    [RequestSizeLimit(SgsPackageRules.MaxMediaFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = SgsPackageRules.MaxMediaFileBytes)]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> AddScreenshot(int id, IFormFile? file, CancellationToken cancellationToken) =>
        UploadMediaAsync(id, file, AdminMediaKind.Screenshot, cancellationToken);

    [HttpDelete("games/{id:int}/media/screenshots/{screenshotId:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameDto>> RemoveScreenshot(
        int id,
        int screenshotId,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken))
        {
            return ForbidOrUnauthorized();
        }

        var updated = await media.RemoveScreenshotAsync(id, screenshotId, requireOwnerUserId: null, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    private async Task<ActionResult<GameDto>> UploadMediaAsync(
        int id,
        IFormFile? file,
        AdminMediaKind kind,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken))
        {
            return ForbidOrUnauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { errors = new[] { "Dodaj plik media." } });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var updated = kind switch
            {
                AdminMediaKind.Cover => await media.SetCoverAsync(id, stream, file.FileName, requireOwnerUserId: null, cancellationToken),
                AdminMediaKind.Header => await media.SetHeaderAsync(id, stream, file.FileName, requireOwnerUserId: null, cancellationToken),
                AdminMediaKind.Intro => await media.SetIntroAsync(id, stream, file.FileName, requireOwnerUserId: null, cancellationToken),
                _ => await media.AddScreenshotAsync(id, stream, file.FileName, requireOwnerUserId: null, cancellationToken)
            };
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    private async Task<ActionResult<GameDto>> ClearMediaAsync(
        int id,
        AdminMediaKind kind,
        CancellationToken cancellationToken)
    {
        if (!await EnsureAdminAsync(cancellationToken))
        {
            return ForbidOrUnauthorized();
        }

        var updated = kind switch
        {
            AdminMediaKind.Cover => await media.ClearCoverAsync(id, requireOwnerUserId: null, cancellationToken),
            AdminMediaKind.Header => await media.ClearHeaderAsync(id, requireOwnerUserId: null, cancellationToken),
            _ => await media.ClearIntroAsync(id, requireOwnerUserId: null, cancellationToken)
        };
        return updated is null ? NotFound() : Ok(updated);
    }

    private enum AdminMediaKind
    {
        Cover,
        Header,
        Intro,
        Screenshot
    }

    private async Task<bool> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        if (!azureOptions.Value.IsConfigured)
        {
            return adminAuth.IsAdmin(User);
        }

        return await ResolveAdminCallerAsync(cancellationToken) is not null;
    }

    /// <summary>The signed-in admin, or null when the request is not from an admin.</summary>
    private async Task<UserProfileDto?> ResolveAdminCallerAsync(CancellationToken cancellationToken)
    {
        if (!azureOptions.Value.IsConfigured)
        {
            return await users.GetByIdAsync(1, cancellationToken);
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sub))
        {
            return null;
        }

        var displayName = User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Name) ?? "Player";
        var email = EmailClaimHelper.FromPrincipal(User);
        var profile = await users.UpsertFromClaimsAsync(sub, displayName, email, avatarUrl: null, cancellationToken);
        return adminAuth.IsAdmin(User, profile.Email ?? email, profile.Role) ? profile : null;
    }

    private ActionResult ForbidOrUnauthorized()
    {
        if (azureOptions.Value.IsConfigured && User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        return Forbid();
    }
}
