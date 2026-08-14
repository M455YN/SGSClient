using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGSClient.Core.DTOs.Rofw;
using SGSClient.Core.Interfaces;

namespace SGSClient.Api.Controllers.Legacy;

/// <summary>Compatibility routes from the previous SGSClient.Api (Realm of Five Winds).</summary>
[ApiController]
[AllowAnonymous]
[Route("api/rofw")]
public sealed class RofwController(IRofwLegacyService rofw) : ControllerBase
{
    [HttpPost("submit-score")]
    public async Task<IActionResult> SubmitScore([FromBody] RofwScoreRequest request, CancellationToken cancellationToken)
    {
        if (Unavailable() is { } unavailable)
        {
            return unavailable;
        }

        await rofw.SubmitScoreAsync(request, cancellationToken);
        return Ok(new { message = "Score saved successfully!" });
    }

    [HttpGet("get-scores")]
    public async Task<IActionResult> GetScores([FromQuery] int limit = 100, CancellationToken cancellationToken = default)
    {
        if (Unavailable() is { } unavailable)
        {
            return unavailable;
        }

        var scores = await rofw.GetScoresAsync(limit, cancellationToken);
        return Ok(scores);
    }

    [HttpPost("update-achievements")]
    public async Task<IActionResult> UpdateAchievements(
        [FromBody] RofwUpdateAchievementsRequest request,
        CancellationToken cancellationToken)
    {
        if (Unavailable() is { } unavailable)
        {
            return unavailable;
        }

        await rofw.UpdateAchievementsAsync(request, cancellationToken);
        return Ok(new { message = "Achievements updated successfully!" });
    }

    [HttpPost("update-face")]
    public async Task<IActionResult> UpdateFace([FromBody] RofwUpdateFaceRequest request, CancellationToken cancellationToken)
    {
        if (Unavailable() is { } unavailable)
        {
            return unavailable;
        }

        await rofw.UpdateFaceAsync(request, cancellationToken);
        return Ok(new { message = "Face updated successfully!" });
    }

    [HttpPost("save-stats")]
    public async Task<IActionResult> SaveStats([FromBody] RofwSaveStatsRequest request, CancellationToken cancellationToken)
    {
        if (Unavailable() is { } unavailable)
        {
            return unavailable;
        }

        await rofw.SaveStatsAsync(request, cancellationToken);
        return Ok(new { message = "Stats saved successfully!" });
    }

    [HttpPost("update-lp")]
    public async Task<IActionResult> UpdateLp([FromBody] RofwUpdateLpRequest request, CancellationToken cancellationToken)
    {
        if (Unavailable() is { } unavailable)
        {
            return unavailable;
        }

        await rofw.UpdateLpAsync(request, cancellationToken);
        return Ok(new { message = "LP updated successfully!" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] RofwLoginRequest request, CancellationToken cancellationToken)
    {
        if (Unavailable() is { } unavailable)
        {
            return unavailable;
        }

        var profile = await rofw.LoginAsync(request, cancellationToken);
        return profile is null ? Unauthorized() : Ok(profile);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RofwRegisterRequest request, CancellationToken cancellationToken)
    {
        if (Unavailable() is { } unavailable)
        {
            return unavailable;
        }

        await rofw.RegisterAsync(request, cancellationToken);
        return Ok(new { message = "Registration successful!" });
    }

    [HttpPost("update-synergies")]
    public async Task<IActionResult> UpdateSynergies(
        [FromBody] RofwUpdateSynergiesRequest request,
        CancellationToken cancellationToken)
    {
        if (Unavailable() is { } unavailable)
        {
            return unavailable;
        }

        await rofw.UpdateSynergiesAsync(request, cancellationToken);
        return Ok(new { message = "Synergies updated successfully!" });
    }

    private IActionResult? Unavailable() =>
        rofw.IsConfigured
            ? null
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                errors = new[] { "ROFW database is not configured (ConnectionStrings:RofwDatabase)." }
            });
}
