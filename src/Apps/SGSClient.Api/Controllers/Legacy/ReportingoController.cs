using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGSClient.Core.DTOs.Reportingo;
using SGSClient.Core.Interfaces;

namespace SGSClient.Api.Controllers.Legacy;

/// <summary>Compatibility routes from the previous SGSClient.Api (Reportingo).</summary>
[ApiController]
[AllowAnonymous]
[Route("api/reportingo")]
[Route("reportingo")]
public sealed class ReportingoController(IReportingoLegacyService reportingo) : ControllerBase
{
    [HttpPost("report-bug")]
    public async Task<IActionResult> ReportBug(
        [FromBody] ReportingoBugReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!reportingo.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                errors = new[] { "Reportingo database is not configured (ConnectionStrings:ReportingoDatabase)." }
            });
        }

        await reportingo.ReportBugAsync(request, cancellationToken);
        return Ok(new { message = "Bug report saved!" });
    }
}
