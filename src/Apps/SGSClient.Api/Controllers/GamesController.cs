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
[Route("api/[controller]")]
public sealed class GamesController(
    IGameRepository games,
    IGamePackagePublisher publisher,
    IGameMediaService media,
    IUserService users,
    IEntitlementService entitlements,
    IGameReviewService reviews,
    IAdminAuthorization adminAuth,
    IOptions<GameStorageSettings> storageOptions,
    IOptions<AzureAdSettings> azureOptions) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<GameDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GameDto>>> GetGames(
        [FromQuery] string? q,
        [FromQuery] string? genre,
        [FromQuery] string[]? genres,
        [FromQuery] string[]? engines,
        [FromQuery] GamePlatform? platform,
        [FromQuery] GameFeatures? features,
        [FromQuery] string? tag,
        [FromQuery] GameContentKind? kind,
        [FromQuery] GameReleaseStatus? releaseStatus,
        [FromQuery] bool? featured,
        [FromQuery] string? sort,
        CancellationToken cancellationToken)
    {
        var query = new GameCatalogQuery
        {
            Q = q,
            Genre = genre,
            Genres = genres,
            Engines = engines,
            Platform = platform,
            Features = features,
            Tag = tag,
            Kind = kind,
            ReleaseStatus = releaseStatus,
            Featured = featured,
            Sort = sort
        };
        var result = await games.GetPublishedGamesAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameDto>> GetGame(int id, CancellationToken cancellationToken)
    {
        var result = await games.GetPublishedGameByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }


    [HttpGet("{id:int}/reviews")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GameReviewsSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameReviewsSummaryDto>> GetReviews(
        int id,
        [FromQuery] string? sort,
        [FromQuery] bool? recommended,
        CancellationToken cancellationToken)
    {
        var viewerId = await ResolveOwnerUserIdAsync(cancellationToken);
        try
        {
            return Ok(await reviews.GetForGameAsync(id, viewerId, sort, recommended, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:int}/reviews/me")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GameReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameReviewDto>> UpsertMyReview(
        int id,
        [FromBody] UpsertGameReviewRequest request,
        CancellationToken cancellationToken)
    {
        var viewerId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (viewerId is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await reviews.UpsertAsync(id, viewerId.Value, request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
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

    [HttpDelete("{id:int}/reviews/me")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteMyReview(int id, CancellationToken cancellationToken)
    {
        var viewerId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (viewerId is null)
        {
            return Unauthorized();
        }

        await reviews.DeleteAsync(id, viewerId.Value, cancellationToken);
        return NoContent();
    }

    [HttpPost("reviews/{reviewId:long}/vote")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VoteReview(
        long reviewId,
        [FromBody] SetReviewVoteRequest request,
        CancellationToken cancellationToken)
    {
        var viewerId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (viewerId is null)
        {
            return Unauthorized();
        }

        try
        {
            await reviews.VoteAsync(reviewId, viewerId.Value, request.Helpful, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    [HttpGet("reviews/{reviewId:long}/comments")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<GameReviewCommentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<GameReviewCommentDto>>> GetReviewComments(
        long reviewId,
        CancellationToken cancellationToken)
    {
        var viewerId = await ResolveOwnerUserIdAsync(cancellationToken);
        try
        {
            return Ok(await reviews.GetCommentsAsync(reviewId, viewerId, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("reviews/{reviewId:long}/comments")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GameReviewCommentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameReviewCommentDto>> AddReviewComment(
        long reviewId,
        [FromBody] CreateGameReviewCommentRequest request,
        CancellationToken cancellationToken)
    {
        var viewerId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (viewerId is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await reviews.AddCommentAsync(reviewId, viewerId.Value, request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    [HttpDelete("reviews/comments/{commentId:long}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteReviewComment(long commentId, CancellationToken cancellationToken)
    {
        var viewerId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (viewerId is null)
        {
            return Unauthorized();
        }

        try
        {
            await reviews.DeleteCommentAsync(commentId, viewerId.Value, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { errors = new[] { ex.Message } });
        }
    }

    [HttpGet("{id:int}/dlcs")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<GameDlcDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<GameDlcDto>>> GetDlcs(int id, CancellationToken cancellationToken)
    {
        var game = await games.GetPublishedGameByIdAsync(id, cancellationToken);
        if (game is null || game.Kind != Core.Enums.GameContentKind.Game)
        {
            return NotFound();
        }

        var dlcs = await games.GetPublishedDlcsAsync(id, cancellationToken);
        return Ok(dlcs);
    }

    [HttpGet("{id:int}/builds/{buildId:int}/versions")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<GameBuildVersionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<GameBuildVersionDto>>> GetBuildVersions(
        int id,
        int buildId,
        CancellationToken cancellationToken)
    {
        var game = await games.GetPublishedGameByIdAsync(id, cancellationToken);
        if (game is null || game.Builds.All(b => b.Id != buildId))
        {
            return NotFound();
        }

        var versions = await games.GetBuildVersionHistoryAsync(id, buildId, cancellationToken);
        return Ok(versions);
    }

    [HttpGet("{id:int}/studio/versions")]
    [ProducesResponseType(typeof(IReadOnlyList<GameBuildVersionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<GameBuildVersionDto>>> GetOwnedVersions(
        int id,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (ownerUserId is null)
        {
            return Unauthorized();
        }

        var versions = await games.GetOwnedVersionHistoryAsync(id, ownerUserId.Value, cancellationToken);
        if (versions.Count == 0)
        {
            var owned = await games.GetOwnedGamesAsync(ownerUserId.Value, cancellationToken);
            if (owned.All(g => g.Id != id))
            {
                return NotFound();
            }
        }

        return Ok(versions);
    }

    [HttpGet("{id:int}/stats")]
    [ProducesResponseType(typeof(GameStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameStatsDto>> GetOwnedStats(int id, CancellationToken cancellationToken)
    {
        var ownerUserId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (ownerUserId is null)
        {
            return Unauthorized();
        }

        var stats = await games.GetOwnedGameStatsAsync(id, ownerUserId.Value, cancellationToken);
        return stats is null ? NotFound() : Ok(stats);
    }

    [HttpPatch("{id:int}/versions/{versionId:int}/notes")]
    [ProducesResponseType(typeof(GameBuildVersionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameBuildVersionDto>> UpdateVersionNotes(
        int id,
        int versionId,
        [FromBody] UpdateVersionNotesRequestDto request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (ownerUserId is null)
        {
            return Unauthorized();
        }

        try
        {
            var updated = await games.UpdateOwnedVersionNotesAsync(
                id,
                versionId,
                ownerUserId.Value,
                request.ReleaseNotes,
                cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    [HttpPatch("{id:int}")]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameDto>> UpdateGame(
        int id,
        [FromBody] UpdateGameRequestDto request,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (ownerUserId is null)
        {
            return Unauthorized();
        }

        try
        {
            var updated = await games.UpdateOwnedGameAsync(id, ownerUserId.Value, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGame(int id, CancellationToken cancellationToken)
    {
        var ownerUserId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (ownerUserId is null)
        {
            return Unauthorized();
        }

        try
        {
            var deleted = await games.DeleteOwnedGameAsync(id, ownerUserId.Value, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    [HttpPut("{id:int}/media/cover")]
    [RequestSizeLimit(SgsPackageRules.MaxMediaFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = SgsPackageRules.MaxMediaFileBytes)]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> UploadCover(int id, [FromForm] IFormFile? file, CancellationToken cancellationToken) =>
        UploadOwnedMediaAsync(id, file, OwnedMediaKind.Cover, cancellationToken);

    [HttpDelete("{id:int}/media/cover")]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> ClearCover(int id, CancellationToken cancellationToken) =>
        ClearOwnedMediaAsync(id, OwnedMediaKind.Cover, cancellationToken);

    [HttpPut("{id:int}/media/header")]
    [RequestSizeLimit(SgsPackageRules.MaxMediaFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = SgsPackageRules.MaxMediaFileBytes)]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> UploadHeader(int id, [FromForm] IFormFile? file, CancellationToken cancellationToken) =>
        UploadOwnedMediaAsync(id, file, OwnedMediaKind.Header, cancellationToken);

    [HttpDelete("{id:int}/media/header")]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> ClearHeader(int id, CancellationToken cancellationToken) =>
        ClearOwnedMediaAsync(id, OwnedMediaKind.Header, cancellationToken);

    [HttpPut("{id:int}/media/logo")]
    [RequestSizeLimit(SgsPackageRules.MaxMediaFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = SgsPackageRules.MaxMediaFileBytes)]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> UploadLogo(int id, [FromForm] IFormFile? file, CancellationToken cancellationToken) =>
        UploadOwnedMediaAsync(id, file, OwnedMediaKind.Logo, cancellationToken);

    [HttpDelete("{id:int}/media/logo")]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> ClearLogo(int id, CancellationToken cancellationToken) =>
        ClearOwnedMediaAsync(id, OwnedMediaKind.Logo, cancellationToken);

    [HttpPut("{id:int}/media/intro")]
    [RequestSizeLimit(SgsPackageRules.MaxMediaFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = SgsPackageRules.MaxMediaFileBytes)]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> UploadIntro(int id, [FromForm] IFormFile? file, CancellationToken cancellationToken) =>
        UploadOwnedMediaAsync(id, file, OwnedMediaKind.Intro, cancellationToken);

    [HttpDelete("{id:int}/media/intro")]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> ClearIntro(int id, CancellationToken cancellationToken) =>
        ClearOwnedMediaAsync(id, OwnedMediaKind.Intro, cancellationToken);

    [HttpPost("{id:int}/media/screenshots")]
    [RequestSizeLimit(SgsPackageRules.MaxMediaFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = SgsPackageRules.MaxMediaFileBytes)]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> AddScreenshot(int id, [FromForm] IFormFile? file, CancellationToken cancellationToken) =>
        UploadOwnedMediaAsync(id, file, OwnedMediaKind.Screenshot, cancellationToken);

    [HttpDelete("{id:int}/media/screenshots/{screenshotId:int}")]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameDto>> RemoveScreenshot(
        int id,
        int screenshotId,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (ownerUserId is null)
        {
            return Unauthorized();
        }

        var updated = await media.RemoveScreenshotAsync(id, screenshotId, ownerUserId.Value, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("uploads")]
    [RequestSizeLimit(SgsPackageRules.MaxZipBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = SgsPackageRules.MaxZipBytes)]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GameDto>> UploadPackage(
        IFormFile? package,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (ownerUserId is null)
        {
            return Unauthorized();
        }

        if (await EnsureCreatorAsync(ownerUserId.Value, cancellationToken) is { } forbidden)
        {
            return forbidden;
        }

        if (package is null || package.Length == 0)
        {
            return BadRequest(new { errors = new[] { "Dodaj plik ZIP z grą." } });
        }

        if (!package.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { errors = new[] { "Akceptujemy tylko pliki .zip." } });
        }

        var profile = await users.GetByIdAsync(ownerUserId.Value, cancellationToken);
        // Prefer claims email when the DB profile has none — B2C often omits email on access tokens
        // until /me syncs it, and IsSuperAdmin on the DTO alone would stay false.
        var allowOverwriteForeignGame = adminAuth.IsSuperAdmin(
            User,
            profile?.Email,
            profile?.Role ?? UserRole.User);

        await using var stream = package.OpenReadStream();
        var result = await publisher.PublishZipAsync(
            stream,
            package.Length,
            ownerUserId.Value,
            allowOverwriteForeignGame,
            cancellationToken);
        if (!result.Success || result.Game is null)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Game);
    }

    private async Task<ActionResult<GameDto>> UploadOwnedMediaAsync(
        int id,
        IFormFile? file,
        OwnedMediaKind kind,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (ownerUserId is null)
        {
            return Unauthorized();
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
                OwnedMediaKind.Cover => await media.SetCoverAsync(id, stream, file.FileName, ownerUserId.Value, cancellationToken),
                OwnedMediaKind.Header => await media.SetHeaderAsync(id, stream, file.FileName, ownerUserId.Value, cancellationToken),
                OwnedMediaKind.Logo => await media.SetLogoAsync(id, stream, file.FileName, ownerUserId.Value, cancellationToken),
                OwnedMediaKind.Intro => await media.SetIntroAsync(id, stream, file.FileName, ownerUserId.Value, cancellationToken),
                _ => await media.AddScreenshotAsync(id, stream, file.FileName, ownerUserId.Value, cancellationToken)
            };
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    private async Task<ActionResult<GameDto>> ClearOwnedMediaAsync(
        int id,
        OwnedMediaKind kind,
        CancellationToken cancellationToken)
    {
        var ownerUserId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (ownerUserId is null)
        {
            return Unauthorized();
        }

        var updated = kind switch
        {
            OwnedMediaKind.Cover => await media.ClearCoverAsync(id, ownerUserId.Value, cancellationToken),
            OwnedMediaKind.Header => await media.ClearHeaderAsync(id, ownerUserId.Value, cancellationToken),
            OwnedMediaKind.Logo => await media.ClearLogoAsync(id, ownerUserId.Value, cancellationToken),
            _ => await media.ClearIntroAsync(id, ownerUserId.Value, cancellationToken)
        };
        return updated is null ? NotFound() : Ok(updated);
    }

    private enum OwnedMediaKind
    {
        Cover,
        Header,
        Logo,
        Intro,
        Screenshot
    }

    /// <summary>Returns a 403 result when the account may not publish; null when it may.</summary>
    private async Task<ActionResult<GameDto>?> EnsureCreatorAsync(int userId, CancellationToken cancellationToken)
    {
        if (!azureOptions.Value.IsConfigured)
        {
            return null;
        }

        var profile = await users.GetByIdAsync(userId, cancellationToken);
        if (profile is not null && profile.Role >= UserRole.Creator)
        {
            return null;
        }

        return StatusCode(
            StatusCodes.Status403Forbidden,
            new { errors = new[] { "Twoje konto nie ma roli twórcy. Poproś administratora o nadanie uprawnień." } });
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

        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sub))
        {
            return null;
        }

        var displayName = User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Name) ?? "Player";
        var email = EmailClaimHelper.FromPrincipal(User);
        var profile = await users.UpsertFromClaimsAsync(sub, displayName, email, avatarUrl: null, cancellationToken);
        return profile.Id;
    }

    [HttpGet("packages/{symbol}/{version}/{fileName}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPackage(
        string symbol,
        string version,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (!SgsPackageRules.IsSafeStorageSegment(symbol)
            || string.IsNullOrWhiteSpace(version)
            || string.IsNullOrWhiteSpace(fileName)
            || fileName.Contains("..", StringComparison.Ordinal)
            || fileName.Contains('/')
            || fileName.Contains('\\'))
        {
            return NotFound();
        }

        var ownerUserId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (ownerUserId is null)
        {
            return Unauthorized();
        }

        var profile = await users.GetByIdAsync(ownerUserId.Value, cancellationToken);
        var isAdmin = profile is not null
            && adminAuth.IsAdmin(User, profile.Email, profile.Role);

        var allowed = await entitlements.CanDownloadPackageAsync(
            ownerUserId.Value,
            symbol,
            isAdmin,
            cancellationToken);
        if (!allowed)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { errors = new[] { "Brak uprawnień do pobrania tej gry. Dodaj ją do biblioteki." } });
        }

        var root = Path.GetFullPath(storageOptions.Value.StoragePath);
        var path = Path.GetFullPath(Path.Combine(root, symbol, version, fileName));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(path))
        {
            return NotFound();
        }

        try
        {
            var platformKey = Path.GetFileNameWithoutExtension(fileName);
            await games.RecordDownloadAsync(symbol, version, platformKey, cancellationToken);
        }
        catch
        {
            // Statistics must never block a download.
        }

        return PhysicalFile(path, "application/zip", fileName);
    }

    /// <summary>
    /// Streams the main file of a published book, track or artwork so the client can show, play or read it in place.
    /// The file is extracted from the package once and served with range support (seeking in audio).
    /// </summary>
    [HttpGet("{id:int}/file")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ViewFile(int id, CancellationToken cancellationToken)
    {
        var game = await games.GetPublishedGameByIdAsync(id, cancellationToken);
        var build = game?.Builds.FirstOrDefault(b => b.IsDefault) ?? game?.Builds.FirstOrDefault();
        if (game is null
            || build is null
            || game.Kind is not (GameContentKind.Book or GameContentKind.Music or GameContentKind.Art)
            || !SgsPackageRules.IsSafeStorageSegment(game.Symbol)
            || !SgsPackageRules.TryNormalizeEntryPath(build.ExeName, out var entryPath, out _))
        {
            return NotFound();
        }

        var root = Path.GetFullPath(storageOptions.Value.StoragePath);
        var zipName = Path.GetFileName(build.ZipLink);
        var zipPath = Path.GetFullPath(Path.Combine(root, game.Symbol, build.CurrentVersion, zipName));
        var viewPath = Path.GetFullPath(Path.Combine(root, game.Symbol, build.CurrentVersion, "_view", entryPath));
        if (!zipPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            || !viewPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            || !System.IO.File.Exists(zipPath))
        {
            return NotFound();
        }

        if (!System.IO.File.Exists(viewPath))
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
            var entry = archive.GetEntry(entryPath);
            if (entry is null)
            {
                return NotFound();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(viewPath)!);
            var temp = viewPath + $".{Guid.NewGuid():N}.tmp";
            System.IO.Compression.ZipFileExtensions.ExtractToFile(entry, temp, overwrite: true);
            System.IO.File.Move(temp, viewPath, overwrite: true);
        }

        new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider()
            .TryGetContentType(viewPath, out var contentType);
        return PhysicalFile(viewPath, contentType ?? "application/octet-stream", enableRangeProcessing: true);
    }

    [HttpGet("media/{symbol}/{fileName}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DownloadMedia(string symbol, string fileName)
    {
        if (!SgsPackageRules.IsSafeStorageSegment(symbol)
            || string.IsNullOrWhiteSpace(fileName)
            || fileName.Contains("..", StringComparison.Ordinal)
            || fileName.Contains('/')
            || fileName.Contains('\\'))
        {
            return NotFound();
        }

        var root = Path.GetFullPath(storageOptions.Value.StoragePath);
        var path = Path.GetFullPath(Path.Combine(root, symbol, "media", fileName));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(path))
        {
            return NotFound();
        }

        var contentType = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            _ => "application/octet-stream"
        };

        return PhysicalFile(path, contentType);
    }
}
