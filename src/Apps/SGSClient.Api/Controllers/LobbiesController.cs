using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SGSClient.Api.Auth;
using SGSClient.Api.Hubs;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;

namespace SGSClient.Api.Controllers;

/// <summary>Lobbies let a host share an address by a short code and invite friends to it.</summary>
[ApiController]
[Route("api")]
public sealed class LobbiesController(
    ILobbyService lobbies,
    IUserService users,
    IHubContext<ChatHub> hub,
    IOptions<AzureAdSettings> azureOptions) : ControllerBase
{
    [HttpPost("lobbies")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LobbyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<LobbyDto>> Create([FromBody] CreateLobbyRequest request, CancellationToken cancellationToken) =>
        RunAsync((userId, ct) => lobbies.CreateAsync(userId, request, ct), cancellationToken);

    [HttpGet("lobbies/{code}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LobbyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<LobbyDto>> Get(string code, CancellationToken cancellationToken) =>
        RunAsync(
            async (_, ct) => await lobbies.GetAsync(code, ct)
                ?? throw new KeyNotFoundException("Nie znaleziono lobby (mogło wygasnąć)."),
            cancellationToken);

    [HttpPatch("lobbies/{code}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LobbyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<LobbyDto>> Update(
        string code,
        [FromBody] UpdateLobbyRequest request,
        CancellationToken cancellationToken) =>
        RunAsync((userId, ct) => lobbies.UpdateAsync(userId, code, request, ct), cancellationToken);

    [HttpDelete("lobbies/{code}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Close(string code, CancellationToken cancellationToken)
    {
        var result = await RunAsync(async (userId, ct) =>
        {
            await lobbies.CloseAsync(userId, code, ct);
            return true;
        }, cancellationToken);
        return result.Result is OkObjectResult ? NoContent() : result.Result!;
    }

    [HttpPost("lobbies/{code}/invite")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Invite(
        string code,
        [FromBody] InviteLobbyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(async (userId, ct) =>
        {
            var notification = await lobbies.InviteAsync(userId, code, request.FriendUserId, ct);
            await hub.Clients.Group(ChatHub.UserGroup(request.FriendUserId))
                .SendAsync("ReceiveNotification", notification, ct);
            return true;
        }, cancellationToken);
        return result.Result is OkObjectResult ? NoContent() : result.Result!;
    }

    [HttpGet("me/lobbies/friends")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<LobbyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<IReadOnlyList<LobbyDto>>> FriendLobbies(CancellationToken cancellationToken) =>
        RunAsync((userId, ct) => lobbies.ListFriendLobbiesAsync(userId, ct), cancellationToken);

    /// <summary>Maps the service's exceptions to the status codes the other controllers use.</summary>
    private async Task<ActionResult<T>> RunAsync<T>(
        Func<int, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await action(userId.Value, cancellationToken));
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

    private Task<int?> ResolveUserIdAsync(CancellationToken cancellationToken) =>
        users.ResolveUserIdAsync(User, azureOptions.Value, cancellationToken);
}
