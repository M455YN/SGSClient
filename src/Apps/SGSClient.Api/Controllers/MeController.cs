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
[Route("api/me")]
public sealed class MeController(
    IUserService users,
    IGameRepository games,
    IAdminAuthorization adminAuth,
    IOptions<AzureAdSettings> azureOptions) : ControllerBase
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
