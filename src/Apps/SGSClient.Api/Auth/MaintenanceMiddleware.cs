using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.Interfaces;

namespace SGSClient.Api.Auth;

/// <summary>During maintenance only what a download needs stays open; super admins keep everything.</summary>
public sealed partial class MaintenanceMiddleware(
    RequestDelegate next,
    IOptionsMonitor<MaintenanceSettings> settings,
    ILogger<MaintenanceMiddleware> logger)
{
    private const string RetryAfterSeconds = "300";

    // Exact routes, not a /api/games prefix: studio versions, stats and reviews live under it too.
    [GeneratedRegex(
        @"^/(health|api/games(/\d+(/dlcs|/builds/\d+/versions)?|/packages/[^/]+/[^/]+/[^/]+|/media/[^/]+/[^/]+)?|api/me(/library|/avatars/\d+/[^/]+)?)/?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AllowedGetRoutes();

    public static bool IsAllowed(string method, string? path) =>
        HttpMethods.IsGet(method) && path is not null && AllowedGetRoutes().IsMatch(path);

    public async Task InvokeAsync(
        HttpContext context,
        IUserService users,
        IAdminAuthorization adminAuth,
        IOptions<AzureAdSettings> azure)
    {
        if (!settings.CurrentValue.Enabled
            || IsAllowed(context.Request.Method, context.Request.Path.Value)
            || await IsSuperAdminAsync(context, users, adminAuth, azure.Value))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = RetryAfterSeconds;
        await context.Response.WriteAsJsonAsync(
            new { errors = new[] { "Trwa przerwa techniczna. Spróbuj ponownie później." } },
            context.RequestAborted);
    }

    private async Task<bool> IsSuperAdminAsync(
        HttpContext context,
        IUserService users,
        IAdminAuthorization adminAuth,
        AzureAdSettings azure)
    {
        try
        {
            var profile = await users.ResolveProfileAsync(context.User, azure, context.RequestAborted);
            return profile is not null && adminAuth.IsSuperAdmin(context.User, profile.Email, profile.Role);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The database may be the reason for the maintenance: fail closed with 503 rather than 500.
            logger.LogWarning(ex, "Could not check super admin access during maintenance");
            return false;
        }
    }
}
