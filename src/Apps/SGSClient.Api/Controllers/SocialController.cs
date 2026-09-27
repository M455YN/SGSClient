using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SGSClient.Api.Hubs;
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
    IProfileCommentService profiles,
    IChatService chat,
    INotificationService notifications,
    IHubContext<ChatHub> hub,
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
        MutateAsync(id => friends.SendFriendRequestAsync(id, userId, cancellationToken), cancellationToken, notifyUserId: userId);

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

    [HttpGet("users/{userId:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PublicUserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicUserProfileDto>> GetUserProfile(int userId, CancellationToken cancellationToken)
    {
        var viewerId = await ResolveUserIdAsync(cancellationToken);
        try
        {
            return Ok(await profiles.GetPublicProfileAsync(userId, viewerId, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("users/by-tag/{tag}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PublicUserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicUserProfileDto>> GetUserProfileByTag(string tag, CancellationToken cancellationToken)
    {
        var viewerId = await ResolveUserIdAsync(cancellationToken);
        var profile = await profiles.GetPublicProfileByTagAsync(tag, viewerId, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPost("users/{userId:int}/comments")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProfileCommentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileCommentDto>> AddProfileComment(
        int userId,
        [FromBody] CreateProfileCommentRequest request,
        CancellationToken cancellationToken)
    {
        var viewerId = await ResolveUserIdAsync(cancellationToken);
        if (viewerId is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await profiles.AddCommentAsync(userId, viewerId.Value, request.Text, cancellationToken));
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

    [HttpDelete("users/{userId:int}/comments/{commentId:long}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProfileComment(int userId, long commentId, CancellationToken cancellationToken)
    {
        var viewerId = await ResolveUserIdAsync(cancellationToken);
        if (viewerId is null)
        {
            return Unauthorized();
        }

        try
        {
            await profiles.DeleteCommentAsync(commentId, viewerId.Value, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("me/chat/threads")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<ChatThreadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ChatThreadDto>>> ListChatThreads(CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await chat.ListThreadsAsync(userId.Value, cancellationToken));
    }

    [HttpGet("me/chat/{peerUserId:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ChatConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatConversationDto>> GetChat(int peerUserId, CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await chat.GetConversationAsync(userId.Value, peerUserId, cancellationToken));
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

    [HttpPost("me/chat/{peerUserId:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ChatMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ChatMessageDto>> SendChat(
        int peerUserId,
        [FromBody] SendChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var message = await chat.SendAsync(userId.Value, peerUserId, request.Text, cancellationToken);
            await hub.Clients.Group(ChatHub.UserGroup(peerUserId))
                .SendAsync("ReceiveMessage", message, cancellationToken);
            await hub.Clients.Group(ChatHub.UserGroup(userId.Value))
                .SendAsync("ReceiveMessage", message, cancellationToken);

            var unread = await notifications.ListAsync(peerUserId, take: 1, cancellationToken);
            if (unread.Count > 0)
            {
                await hub.Clients.Group(ChatHub.UserGroup(peerUserId))
                    .SendAsync("ReceiveNotification", unread[0], cancellationToken);
            }

            return Ok(message);
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

    private async Task<ActionResult<SocialOverviewDto>> MutateAsync(
        Func<int, Task<SocialOverviewDto>> action,
        CancellationToken cancellationToken,
        int? notifyUserId = null)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var overview = await action(userId.Value);
            if (notifyUserId is int peer)
            {
                await hub.Clients.Group(ChatHub.UserGroup(peer))
                    .SendAsync("NotificationsChanged", cancellationToken);
            }

            await hub.Clients.Group(ChatHub.UserGroup(userId.Value))
                .SendAsync("NotificationsChanged", cancellationToken);
            return Ok(overview);
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
