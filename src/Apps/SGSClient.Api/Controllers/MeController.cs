using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;

namespace SGSClient.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public sealed class MeController(IUserService users) : ControllerBase
{
    [HttpGet]
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
        var email = User.FindFirstValue("emails")
            ?? User.FindFirstValue("email")
            ?? User.FindFirstValue(ClaimTypes.Email);
        var avatar = User.FindFirstValue("picture");

        var profile = await users.UpsertFromClaimsAsync(sub, displayName, email, avatar, cancellationToken);
        return Ok(profile);
    }

    [HttpPatch]
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
        return profile is null ? NotFound() : Ok(profile);
    }

    private string? GetSub() =>
        User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
