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
        var azure = azureOptions.Value;
        var ownerUserId = 1;

        if (azure.IsConfigured)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(sub))
            {
                return Unauthorized();
            }

            var displayName = User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Name) ?? "Player";
            var email = User.FindFirstValue("emails")
                ?? User.FindFirstValue("email")
                ?? User.FindFirstValue(ClaimTypes.Email);
            var profile = await users.UpsertFromClaimsAsync(sub, displayName, email, avatarUrl: null, cancellationToken);
            ownerUserId = profile.Id;
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
        var result = await publisher.PublishZipAsync(stream, package.Length, ownerUserId, cancellationToken);
        if (!result.Success || result.Game is null)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Game);
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
