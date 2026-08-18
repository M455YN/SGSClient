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
[Route("api")]
public sealed class SocialController(
    IFriendsService friends,
    IUserService users,
    IOptions<AzureAdSettings> azureOptions) : ControllerBase
{
    [HttpGet("me/social")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SocialOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SocialOverviewDto>> GetSocial(CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await friends.GetOverviewAsync(userId.Value, cancellationToken));
    }

    [HttpGet("users/search")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<SocialUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<SocialUserDto>>> Search(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await friends.SearchUsersAsync(userId.Value, q ?? string.Empty, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    [HttpPost("me/friends/{userId:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SocialOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<SocialOverviewDto>> SendFriendRequest(int userId, CancellationToken cancellationToken) =>
        MutateAsync(id => friends.SendFriendRequestAsync(id, userId, cancellationToken), cancellationToken);

    [HttpPost("me/friends/{userId:int}/accept")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SocialOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<SocialOverviewDto>> AcceptFriendRequest(int userId, CancellationToken cancellationToken) =>
        MutateAsync(id => friends.AcceptFriendRequestAsync(id, userId, cancellationToken), cancellationToken);

    [HttpDelete("me/friends/{userId:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SocialOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<SocialOverviewDto>> RemoveFriend(int userId, CancellationToken cancellationToken) =>
        MutateAsync(id => friends.RemoveFriendAsync(id, userId, cancellationToken), cancellationToken);

    [HttpPut("me/follows/{userId:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SocialOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<SocialOverviewDto>> Follow(int userId, CancellationToken cancellationToken) =>
        MutateAsync(id => friends.FollowAsync(id, userId, cancellationToken), cancellationToken);

    [HttpDelete("me/follows/{userId:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SocialOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<SocialOverviewDto>> Unfollow(int userId, CancellationToken cancellationToken) =>
        MutateAsync(id => friends.UnfollowAsync(id, userId, cancellationToken), cancellationToken);

    private async Task<ActionResult<SocialOverviewDto>> MutateAsync(
        Func<int, Task<SocialOverviewDto>> action,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await action(userId.Value));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    private async Task<int?> ResolveUserIdAsync(CancellationToken cancellationToken)
    {
        var azure = azureOptions.Value;
        if (!azure.IsConfigured)
        {
            var local = await users.GetByIdAsync(1, cancellationToken);
            if (local is not null)
            {
                return local.Id;
            }

            var created = await users.UpsertFromClaimsAsync("local-dev", "Player", null, null, cancellationToken);
            return created.Id;
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

        var displayName = User.FindFirstValue("name")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? "Player";
        var email = EmailClaimHelper.FromPrincipal(User);
        var profile = await users.UpsertFromClaimsAsync(sub, displayName, email, avatarUrl: null, cancellationToken);
        return profile.Id;
    }
}
