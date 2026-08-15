using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
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
    IOptions<GameStorageSettings> storageOptions,
    IOptions<AzureAdSettings> azureOptions) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<GameDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GameDto>>> GetGames(CancellationToken cancellationToken)
    {
        var result = await games.GetPublishedGamesAsync(cancellationToken);
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
    public Task<ActionResult<GameDto>> UploadCover(int id, IFormFile? file, CancellationToken cancellationToken) =>
        UploadOwnedMediaAsync(id, file, OwnedMediaKind.Cover, cancellationToken);

    [HttpDelete("{id:int}/media/cover")]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> ClearCover(int id, CancellationToken cancellationToken) =>
        ClearOwnedMediaAsync(id, OwnedMediaKind.Cover, cancellationToken);

    [HttpPut("{id:int}/media/intro")]
    [RequestSizeLimit(SgsPackageRules.MaxMediaFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = SgsPackageRules.MaxMediaFileBytes)]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<GameDto>> UploadIntro(int id, IFormFile? file, CancellationToken cancellationToken) =>
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
    public Task<ActionResult<GameDto>> AddScreenshot(int id, IFormFile? file, CancellationToken cancellationToken) =>
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

        if (package is null || package.Length == 0)
        {
            return BadRequest(new { errors = new[] { "Dodaj plik ZIP z grą." } });
        }

        if (!package.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { errors = new[] { "Akceptujemy tylko pliki .zip." } });
        }

        await using var stream = package.OpenReadStream();
        var result = await publisher.PublishZipAsync(stream, package.Length, ownerUserId.Value, cancellationToken);
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

        var updated = kind == OwnedMediaKind.Cover
            ? await media.ClearCoverAsync(id, ownerUserId.Value, cancellationToken)
            : await media.ClearIntroAsync(id, ownerUserId.Value, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    private enum OwnedMediaKind
    {
        Cover,
        Intro,
        Screenshot
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
        var email = User.FindFirstValue("emails")
            ?? User.FindFirstValue("email")
            ?? User.FindFirstValue(ClaimTypes.Email);
        var profile = await users.UpsertFromClaimsAsync(sub, displayName, email, avatarUrl: null, cancellationToken);
        return profile.Id;
    }

    [HttpGet("packages/{symbol}/{version}/{fileName}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DownloadPackage(string symbol, string version, string fileName)
    {
        if (!SgsPackageRules.IsValidSymbol(symbol)
            || string.IsNullOrWhiteSpace(version)
            || string.IsNullOrWhiteSpace(fileName)
            || fileName.Contains("..", StringComparison.Ordinal)
            || fileName.Contains('/')
            || fileName.Contains('\\'))
        {
            return NotFound();
        }

        var root = Path.GetFullPath(storageOptions.Value.StoragePath);
        var path = Path.GetFullPath(Path.Combine(root, symbol, version, fileName));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(path))
        {
            return NotFound();
        }

        return PhysicalFile(path, "application/zip", fileName);
    }

    [HttpGet("media/{symbol}/{fileName}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DownloadMedia(string symbol, string fileName)
    {
        if (!SgsPackageRules.IsValidSymbol(symbol)
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
