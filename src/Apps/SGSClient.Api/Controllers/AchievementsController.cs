using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SGSClient.Api.Auth;
using SGSClient.Core.Auth;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Packaging;

namespace SGSClient.Api.Controllers;

/// <summary>
/// Achievements. Three audiences share one service:
/// creators (<c>studio</c> routes), signed-in players (<c>api/me</c>, <c>api/games</c>) and launched
/// games, which authenticate with the short-lived <c>X-SGS-Token</c> from <c>api/sdk</c>.
/// </summary>
[ApiController]
[Route("api")]
public sealed class AchievementsController(
    IAchievementService achievements,
    IAchievementTokenService tokens,
    IUserService users,
    IAdminAuthorization adminAuth,
    IOptions<AzureAdSettings> azureOptions) : ControllerBase
{
    public const string TokenHeader = "X-SGS-Token";

    // ---- creator ---------------------------------------------------------------------------

    [HttpGet("games/{gameId:int}/studio/achievements")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<AchievementDefinitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AchievementDefinitionDto>>> GetDefinitions(
        int gameId,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        var isAdmin = await IsAdminAsync(userId.Value, cancellationToken);
        return await RunAsync(() => achievements.GetDefinitionsAsync(gameId, userId.Value, isAdmin, cancellationToken));
    }

    [HttpGet("games/{gameId:int}/studio/stats")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<GameStatDefinitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<GameStatDefinitionDto>>> GetStatDefinitions(
        int gameId,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        var isAdmin = await IsAdminAsync(userId.Value, cancellationToken);
        return await RunAsync(() => achievements.GetStatDefinitionsAsync(gameId, userId.Value, isAdmin, cancellationToken));
    }

    [HttpPut("games/{gameId:int}/studio/achievements")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<AchievementDefinitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AchievementDefinitionDto>>> SyncDefinitions(
        int gameId,
        [FromBody] SyncGameAchievementsRequest request,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        var isAdmin = await IsAdminAsync(userId.Value, cancellationToken);
        return await RunAsync(() =>
            achievements.SyncDefinitionsAsync(gameId, userId.Value, request, isAdmin, cancellationToken));
    }

    [HttpPut("games/{gameId:int}/studio/achievements/{key}/icon")]
    [AllowAnonymous]
    [RequestSizeLimit(SgsPackageRules.MaxMediaFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = SgsPackageRules.MaxMediaFileBytes)]
    [ProducesResponseType(typeof(AchievementDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AchievementDefinitionDto>> UploadIcon(
        int gameId,
        string key,
        [FromForm] IFormFile? file,
        [FromQuery] bool locked,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { errors = new[] { "Dodaj plik obrazu." } });
        }

        var isAdmin = await IsAdminAsync(userId.Value, cancellationToken);
        await using var stream = file.OpenReadStream();
        return await RunAsync(() => achievements.SetIconAsync(
            gameId, userId.Value, key, stream, file.FileName, isAdmin, locked, cancellationToken));
    }

    [HttpDelete("games/{gameId:int}/studio/achievements/progress")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetMyProgress(int gameId, CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            await achievements.ResetProgressAsync(
                gameId,
                userId.Value,
                await IsAdminAsync(userId.Value, cancellationToken),
                cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ---- player ----------------------------------------------------------------------------

    [HttpGet("games/{gameId:int}/achievements")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GameAchievementsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameAchievementsDto>> GetForGame(
        int gameId,
        [FromQuery] string? lang,
        CancellationToken cancellationToken)
    {
        var viewerId = await ResolveUserIdAsync(cancellationToken);
        var result = await achievements.GetForGameAsync(gameId, viewerId, lang, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("me/achievements")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<UserAchievementSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<UserAchievementSummaryDto>>> GetMine(
        CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await achievements.GetSummariesAsync(userId.Value, includeLocked: true, cancellationToken: cancellationToken));
    }

    [HttpPost("me/games/{gameId:int}/achievements/session")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AchievementSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AchievementSessionDto>> CreateSession(int gameId, CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        // The token only identifies user + game; every write re-checks that the user owns the game.
        var game = await achievements.GetForGameAsync(gameId, userId, cancellationToken: cancellationToken);
        return game is null ? NotFound() : Ok(tokens.Issue(userId.Value, gameId));
    }

    [HttpPost("me/games/{gameId:int}/achievements/{key}/unlock")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AchievementUpdateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AchievementUpdateResultDto>> UnlockMine(
        int gameId,
        string key,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        return await RunAsync(() => achievements.UnlockAsync(userId.Value, gameId, key, cancellationToken));
    }

    [HttpPost("me/games/{gameId:int}/achievements/{key}/progress")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AchievementUpdateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AchievementUpdateResultDto>> ProgressMine(
        int gameId,
        string key,
        [FromBody] SetAchievementProgressRequest request,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        return await RunAsync(() => achievements.SetProgressAsync(userId.Value, gameId, key, request.Value, cancellationToken));
    }

    [HttpPost("me/games/{gameId:int}/stats/{key}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StatUpdateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StatUpdateResultDto>> SetStatMine(
        int gameId,
        string key,
        [FromBody] SetStatRequest request,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        return await RunAsync(() => achievements.SetStatAsync(userId.Value, gameId, key, request.Value, cancellationToken));
    }

    // ---- game (SDK) ------------------------------------------------------------------------

    [HttpGet("sdk/achievements")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GameAchievementsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameAchievementsDto>> SdkList(
        [FromQuery] string? lang,
        CancellationToken cancellationToken)
    {
        if (!TryReadToken(out var userId, out var gameId))
        {
            return Unauthorized();
        }

        var result = await achievements.GetForGameAsync(gameId, userId, lang, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("sdk/achievements/{key}/unlock")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AchievementUpdateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AchievementUpdateResultDto>> SdkUnlock(string key, CancellationToken cancellationToken)
    {
        if (!TryReadToken(out var userId, out var gameId))
        {
            return Unauthorized();
        }

        return await RunAsync(() => achievements.UnlockAsync(userId, gameId, key, cancellationToken));
    }

    [HttpPost("sdk/achievements/{key}/progress")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AchievementUpdateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AchievementUpdateResultDto>> SdkProgress(
        string key,
        [FromBody] SetAchievementProgressRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadToken(out var userId, out var gameId))
        {
            return Unauthorized();
        }

        return await RunAsync(() => achievements.SetProgressAsync(userId, gameId, key, request.Value, cancellationToken));
    }

    [HttpPost("sdk/stats/{key}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StatUpdateResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StatUpdateResultDto>> SdkSetStat(
        string key,
        [FromBody] SetStatRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReadToken(out var userId, out var gameId))
        {
            return Unauthorized();
        }

        return await RunAsync(() => achievements.SetStatAsync(userId, gameId, key, request.Value, cancellationToken));
    }

    // ---- helpers ---------------------------------------------------------------------------

    private bool TryReadToken(out int userId, out int gameId)
    {
        var header = Request.Headers[TokenHeader].ToString();
        return tokens.TryValidate(header, out userId, out gameId);
    }

    /// <summary>Maps the service's exceptions to the same status codes the other controllers use.</summary>
    private async Task<ActionResult<T>> RunAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { errors = new[] { ex.Message } });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { errors = new[] { ex.Message } });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    private async Task<bool> IsAdminAsync(int userId, CancellationToken cancellationToken)
    {
        if (!azureOptions.Value.IsConfigured)
        {
            return adminAuth.IsAdmin(User);
        }

        var profile = await users.GetByIdAsync(userId, cancellationToken);
        return profile is not null
            && adminAuth.IsAdmin(User, profile.Email ?? EmailClaimHelper.FromPrincipal(User), profile.Role);
    }

    private Task<int?> ResolveUserIdAsync(CancellationToken cancellationToken) =>
        users.ResolveUserIdAsync(User, azureOptions.Value, cancellationToken);
}
