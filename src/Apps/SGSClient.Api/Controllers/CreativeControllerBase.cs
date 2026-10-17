using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using SGSClient.Core.Auth;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;

namespace SGSClient.Api.Controllers;

/// <summary>Shared helpers for the art, music and stories controllers.</summary>
public abstract class CreativeControllerBase(
    IUserService users,
    IAdminAuthorization adminAuth,
    IOptions<AzureAdSettings> azureOptions) : ControllerBase
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    protected const long MultipartOverhead = 1024 * 1024;

    // Same resolution as GamesController: dev fallback user 1 when Azure AD is not configured.
    protected async Task<int?> ResolveOwnerUserIdAsync(CancellationToken cancellationToken)
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

    protected async Task<bool> IsAdminAsync(int userId, CancellationToken cancellationToken)
    {
        var profile = await users.GetByIdAsync(userId, cancellationToken);
        return adminAuth.IsAdmin(User, profile?.Email, profile?.Role ?? UserRole.User);
    }

    protected IActionResult Errors(string message) => BadRequest(new { errors = new[] { message } });

    protected IActionResult DeleteResult(CreativeDeleteResult result) => result switch
    {
        CreativeDeleteResult.Deleted => NoContent(),
        CreativeDeleteResult.Forbidden => StatusCode(
            StatusCodes.Status403Forbidden,
            new { errors = new[] { "Możesz usunąć tylko własne treści." } }),
        _ => NotFound()
    };

    protected IActionResult ServeFile(string? path)
    {
        if (path is null)
        {
            return NotFound();
        }

        if (!ContentTypes.TryGetContentType(path, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return PhysicalFile(path, contentType, enableRangeProcessing: true);
    }

    /// <summary>Runs an owner-or-admin edit and maps the outcome: null -> 404, validation -> 400, not allowed -> 403.</summary>
    protected async Task<IActionResult> EditAsync<T>(Func<int, bool, Task<T?>> edit, CancellationToken cancellationToken) where T : class
    {
        var userId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var admin = await IsAdminAsync(userId.Value, cancellationToken);
            return await edit(userId.Value, admin) is { } updated ? Ok(updated) : NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { errors = new[] { ex.Message } });
        }
        catch (ArgumentException ex)
        {
            return Errors(ex.Message);
        }
    }

    protected async Task<IActionResult> ListCommentsAsync(ICreativeCommentService comments, string section, int id, CancellationToken cancellationToken)
    {
        var viewer = await ResolveOwnerUserIdAsync(cancellationToken);
        var admin = viewer is not null && await IsAdminAsync(viewer.Value, cancellationToken);
        return await comments.ListAsync(section, id, viewer, admin, cancellationToken) is { } list ? Ok(list) : NotFound();
    }

    protected async Task<IActionResult> AddCommentAsync(
        ICreativeCommentService comments, string section, int id, AddCreativeCommentRequestDto? request, CancellationToken cancellationToken)
    {
        var userId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var admin = await IsAdminAsync(userId.Value, cancellationToken);
            return await comments.AddAsync(section, id, userId.Value, admin, request?.Text, cancellationToken) is { } created
                ? Ok(created)
                : NotFound();
        }
        catch (ArgumentException ex)
        {
            return Errors(ex.Message);
        }
    }

    protected async Task<IActionResult> DeleteCommentAsync(
        ICreativeCommentService comments, string section, int id, long commentId, CancellationToken cancellationToken)
    {
        var userId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        var admin = await IsAdminAsync(userId.Value, cancellationToken);
        return CommentDeleteResult(await comments.DeleteAsync(section, id, commentId, userId.Value, admin, cancellationToken));
    }

    private IActionResult CommentDeleteResult(CreativeDeleteResult result) => result == CreativeDeleteResult.Forbidden
        ? StatusCode(StatusCodes.Status403Forbidden, new { errors = new[] { "Nie możesz usunąć tego komentarza." } })
        : DeleteResult(result);

    protected static IReadOnlyList<string> SplitTags(string? tags) =>
        (tags ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

[ApiController]
[Route("api/art")]
public sealed class ArtController(
    IArtworkService art,
    ICreativeCommentService comments,
    IUserService users,
    IAdminAuthorization adminAuth,
    IOptions<AzureAdSettings> azureOptions) : CreativeControllerBase(users, adminAuth, azureOptions)
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ArtworkDto>>> List(
        [FromQuery] string? q, [FromQuery] string? tag, [FromQuery] string? sort, CancellationToken cancellationToken) =>
        Ok(await art.ListAsync(new CreativeQuery { Q = q, Tag = tag, Sort = sort }, cancellationToken));

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<ArtworkDto>>> Mine(CancellationToken cancellationToken)
    {
        var userId = await ResolveOwnerUserIdAsync(cancellationToken);
        return userId is null ? Unauthorized() : Ok(await art.ListMineAsync(userId.Value, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<ArtworkDto>> Get(int id, CancellationToken cancellationToken) =>
        await art.GetAsync(id, cancellationToken) is { } item ? Ok(item) : NotFound();

    [HttpGet("{id:int}/file")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFile(int id, CancellationToken cancellationToken) =>
        ServeFile(await art.GetFilePathAsync(id, cancellationToken));

    [HttpPost]
    [RequestSizeLimit(CreativeRules.MaxArtBytes + MultipartOverhead)]
    [RequestFormLimits(MultipartBodyLengthLimit = CreativeRules.MaxArtBytes + MultipartOverhead)]
    public async Task<IActionResult> Create(
        [FromForm] string? title,
        [FromForm] string? description,
        [FromForm] string? tags,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return Errors("Dodaj plik z obrazem.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var created = await art.CreateAsync(
                userId.Value, title ?? string.Empty, description, SplitTags(tags), stream, file.FileName, file.Length, cancellationToken);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return Errors(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        var admin = await IsAdminAsync(userId.Value, cancellationToken);
        return DeleteResult(await art.DeleteAsync(id, userId.Value, admin, cancellationToken));
    }

    [HttpPut("{id:int}")]
    public Task<IActionResult> Update(int id, [FromBody] UpdateCreativeRequestDto? request, CancellationToken cancellationToken) =>
        request is null
            ? Task.FromResult(Errors("Brak danych."))
            : EditAsync((userId, admin) => art.UpdateAsync(id, userId, admin, request, cancellationToken), cancellationToken);

    [HttpGet("{id:int}/comments")]
    [AllowAnonymous]
    public Task<IActionResult> Comments(int id, CancellationToken cancellationToken) =>
        ListCommentsAsync(comments, CreativeSections.Art, id, cancellationToken);

    [HttpPost("{id:int}/comments")]
    public Task<IActionResult> AddComment(int id, [FromBody] AddCreativeCommentRequestDto? request, CancellationToken cancellationToken) =>
        AddCommentAsync(comments, CreativeSections.Art, id, request, cancellationToken);

    [HttpDelete("{id:int}/comments/{commentId:long}")]
    public Task<IActionResult> DeleteComment(int id, long commentId, CancellationToken cancellationToken) =>
        DeleteCommentAsync(comments, CreativeSections.Art, id, commentId, cancellationToken);
}

[ApiController]
[Route("api/music")]
public sealed class MusicController(
    IMusicService music,
    ICreativeCommentService comments,
    IUserService users,
    IAdminAuthorization adminAuth,
    IOptions<AzureAdSettings> azureOptions) : CreativeControllerBase(users, adminAuth, azureOptions)
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<TrackDto>>> List(
        [FromQuery] string? q, [FromQuery] string? tag, [FromQuery] string? sort, CancellationToken cancellationToken) =>
        Ok(await music.ListAsync(new CreativeQuery { Q = q, Tag = tag, Sort = sort }, cancellationToken));

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<TrackDto>>> Mine(CancellationToken cancellationToken)
    {
        var userId = await ResolveOwnerUserIdAsync(cancellationToken);
        return userId is null ? Unauthorized() : Ok(await music.ListMineAsync(userId.Value, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<TrackDto>> Get(int id, CancellationToken cancellationToken) =>
        await music.GetAsync(id, cancellationToken) is { } item ? Ok(item) : NotFound();

    [HttpGet("{id:int}/file")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFile(int id, CancellationToken cancellationToken) =>
        ServeFile(await music.GetFilePathAsync(id, cancellationToken));

    [HttpGet("{id:int}/cover")]
    [AllowAnonymous]
    public async Task<IActionResult> Cover(int id, CancellationToken cancellationToken) =>
        ServeFile(await music.GetCoverPathAsync(id, cancellationToken));

    [HttpPost]
    [RequestSizeLimit(CreativeRules.MaxMusicBytes + CreativeRules.MaxCoverBytes + MultipartOverhead)]
    [RequestFormLimits(MultipartBodyLengthLimit = CreativeRules.MaxMusicBytes + CreativeRules.MaxCoverBytes + MultipartOverhead)]
    public async Task<IActionResult> Create(
        [FromForm] string? title,
        [FromForm] string? description,
        [FromForm] string? tags,
        IFormFile? file,
        IFormFile? cover,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return Errors("Dodaj plik audio.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            await using var coverStream = cover is { Length: > 0 } ? cover.OpenReadStream() : null;
            var created = await music.CreateAsync(
                userId.Value, title ?? string.Empty, description, SplitTags(tags), stream, file.FileName, file.Length,
                coverStream, cover?.FileName, cover?.Length ?? 0, cancellationToken);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return Errors(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        var admin = await IsAdminAsync(userId.Value, cancellationToken);
        return DeleteResult(await music.DeleteAsync(id, userId.Value, admin, cancellationToken));
    }

    [HttpPut("{id:int}")]
    public Task<IActionResult> Update(int id, [FromBody] UpdateCreativeRequestDto? request, CancellationToken cancellationToken) =>
        request is null
            ? Task.FromResult(Errors("Brak danych."))
            : EditAsync((userId, admin) => music.UpdateAsync(id, userId, admin, request, cancellationToken), cancellationToken);

    [HttpPost("{id:int}/cover")]
    [RequestSizeLimit(CreativeRules.MaxCoverBytes + MultipartOverhead)]
    [RequestFormLimits(MultipartBodyLengthLimit = CreativeRules.MaxCoverBytes + MultipartOverhead)]
    public async Task<IActionResult> SetCover(int id, IFormFile? cover, CancellationToken cancellationToken)
    {
        if (cover is null || cover.Length == 0)
        {
            return Errors("Dodaj plik okładki.");
        }

        await using var stream = cover.OpenReadStream();
        return await EditAsync(
            (userId, admin) => music.SetCoverAsync(id, userId, admin, stream, cover.FileName, cover.Length, cancellationToken),
            cancellationToken);
    }

    [HttpGet("{id:int}/comments")]
    [AllowAnonymous]
    public Task<IActionResult> Comments(int id, CancellationToken cancellationToken) =>
        ListCommentsAsync(comments, CreativeSections.Music, id, cancellationToken);

    [HttpPost("{id:int}/comments")]
    public Task<IActionResult> AddComment(int id, [FromBody] AddCreativeCommentRequestDto? request, CancellationToken cancellationToken) =>
        AddCommentAsync(comments, CreativeSections.Music, id, request, cancellationToken);

    [HttpDelete("{id:int}/comments/{commentId:long}")]
    public Task<IActionResult> DeleteComment(int id, long commentId, CancellationToken cancellationToken) =>
        DeleteCommentAsync(comments, CreativeSections.Music, id, commentId, cancellationToken);
}

[ApiController]
[Route("api/stories")]
public sealed class StoriesController(
    IStoryService stories,
    ICreativeCommentService comments,
    IUserService users,
    IAdminAuthorization adminAuth,
    IOptions<AzureAdSettings> azureOptions) : CreativeControllerBase(users, adminAuth, azureOptions)
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<StoryDto>>> List(
        [FromQuery] string? q, [FromQuery] string? tag, [FromQuery] string? sort, CancellationToken cancellationToken) =>
        Ok(await stories.ListAsync(new CreativeQuery { Q = q, Tag = tag, Sort = sort }, cancellationToken));

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<StoryDto>>> Mine(CancellationToken cancellationToken)
    {
        var userId = await ResolveOwnerUserIdAsync(cancellationToken);
        return userId is null ? Unauthorized() : Ok(await stories.ListMineAsync(userId.Value, cancellationToken));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<StoryDto>> Get(int id, CancellationToken cancellationToken) =>
        await stories.GetAsync(id, cancellationToken) is { } item ? Ok(item) : NotFound();

    [HttpGet("{id:int}/cover")]
    [AllowAnonymous]
    public async Task<IActionResult> Cover(int id, CancellationToken cancellationToken) =>
        ServeFile(await stories.GetCoverPathAsync(id, cancellationToken));

    // 200 chapters x 200 000 chars can reach ~40M chars; allow 64 MB of JSON.
    [HttpPost]
    [RequestSizeLimit(64L * 1024 * 1024)]
    public async Task<IActionResult> Create([FromBody] CreateStoryRequestDto? request, CancellationToken cancellationToken)
    {
        var userId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        if (request is null)
        {
            return Errors("Brak danych opowiadania.");
        }

        try
        {
            return Ok(await stories.CreateAsync(userId.Value, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return Errors(ex.Message);
        }
    }

    [HttpPost("{id:int}/cover")]
    [RequestSizeLimit(CreativeRules.MaxCoverBytes + MultipartOverhead)]
    [RequestFormLimits(MultipartBodyLengthLimit = CreativeRules.MaxCoverBytes + MultipartOverhead)]
    public async Task<IActionResult> SetCover(int id, IFormFile? cover, CancellationToken cancellationToken)
    {
        if (cover is null || cover.Length == 0)
        {
            return Errors("Dodaj plik okładki.");
        }

        await using var stream = cover.OpenReadStream();
        return await EditAsync(
            (userId, admin) => stories.SetCoverAsync(id, userId, admin, stream, cover.FileName, cover.Length, cancellationToken),
            cancellationToken);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = await ResolveOwnerUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        var admin = await IsAdminAsync(userId.Value, cancellationToken);
        return DeleteResult(await stories.DeleteAsync(id, userId.Value, admin, cancellationToken));
    }

    [HttpPut("{id:int}")]
    [RequestSizeLimit(64L * 1024 * 1024)]
    public Task<IActionResult> Update(int id, [FromBody] UpdateStoryRequestDto? request, CancellationToken cancellationToken) =>
        request is null
            ? Task.FromResult(Errors("Brak danych opowiadania."))
            : EditAsync((userId, admin) => stories.UpdateAsync(id, userId, admin, request, cancellationToken), cancellationToken);

    [HttpGet("{id:int}/comments")]
    [AllowAnonymous]
    public Task<IActionResult> Comments(int id, CancellationToken cancellationToken) =>
        ListCommentsAsync(comments, CreativeSections.Stories, id, cancellationToken);

    [HttpPost("{id:int}/comments")]
    public Task<IActionResult> AddComment(int id, [FromBody] AddCreativeCommentRequestDto? request, CancellationToken cancellationToken) =>
        AddCommentAsync(comments, CreativeSections.Stories, id, request, cancellationToken);

    [HttpDelete("{id:int}/comments/{commentId:long}")]
    public Task<IActionResult> DeleteComment(int id, long commentId, CancellationToken cancellationToken) =>
        DeleteCommentAsync(comments, CreativeSections.Stories, id, commentId, cancellationToken);
}

/// <summary>Moderation lists: every item of every author, newest first. Same admin check as AdminController.</summary>
[ApiController]
[Route("api/admin")]
public sealed class AdminCreativeController(
    IArtworkService art,
    IMusicService music,
    IStoryService stories,
    IUserService users,
    IAdminAuthorization adminAuth,
    IOptions<AzureAdSettings> azureOptions) : CreativeControllerBase(users, adminAuth, azureOptions)
{
    private readonly IAdminAuthorization _adminAuth = adminAuth;
    private readonly IOptions<AzureAdSettings> _azure = azureOptions;

    [HttpGet("art")]
    [AllowAnonymous]
    public async Task<IActionResult> Art(CancellationToken cancellationToken) =>
        await EnsureAdminAsync(cancellationToken) ? Ok(await art.ListAllAsync(cancellationToken)) : ForbidOrUnauthorized();

    [HttpGet("music")]
    [AllowAnonymous]
    public async Task<IActionResult> Music(CancellationToken cancellationToken) =>
        await EnsureAdminAsync(cancellationToken) ? Ok(await music.ListAllAsync(cancellationToken)) : ForbidOrUnauthorized();

    [HttpGet("stories")]
    [AllowAnonymous]
    public async Task<IActionResult> Stories(CancellationToken cancellationToken) =>
        await EnsureAdminAsync(cancellationToken) ? Ok(await stories.ListAllAsync(cancellationToken)) : ForbidOrUnauthorized();

    private async Task<bool> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        if (!_azure.Value.IsConfigured)
        {
            return _adminAuth.IsAdmin(User);
        }

        var userId = await ResolveOwnerUserIdAsync(cancellationToken);
        return userId is not null && await IsAdminAsync(userId.Value, cancellationToken);
    }

    private IActionResult ForbidOrUnauthorized() =>
        _azure.Value.IsConfigured && User.Identity?.IsAuthenticated != true ? Unauthorized() : Forbid();
}
