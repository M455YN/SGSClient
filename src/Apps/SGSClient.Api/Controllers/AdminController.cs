using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SGSClient.Core.Auth;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;

namespace SGSClient.Api.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController(
    IGameRepository games,
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

    private async Task<bool> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        if (!azureOptions.Value.IsConfigured)
        {
            return adminAuth.IsAdmin(User);
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sub))
        {
            return false;
        }

        var displayName = User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Name) ?? "Player";
        var email = EmailClaimHelper.FromPrincipal(User);
        var profile = await users.UpsertFromClaimsAsync(sub, displayName, email, avatarUrl: null, cancellationToken);
        return adminAuth.IsAdmin(User, profile.Email ?? email);
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
