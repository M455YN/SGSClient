using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SGSClient.Api.Auth;
using SGSClient.Api.Hubs;
using SGSClient.Core.Configuration;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server;
using SGSClient.Infrastructure.Server.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Services.Configure<AzureAdSettings>(builder.Configuration.GetSection(AzureAdSettings.SectionName));
builder.Services.Configure<AdminSettings>(builder.Configuration.GetSection(AdminSettings.SectionName));
builder.Services.Configure<MaintenanceSettings>(builder.Configuration.GetSection(MaintenanceSettings.SectionName));
var azureAd = builder.Configuration.GetSection(AzureAdSettings.SectionName).Get<AzureAdSettings>() ?? new AzureAdSettings();

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = SGSClient.Core.Packaging.SgsPackageRules.MaxZipBytes;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = SGSClient.Core.Packaging.SgsPackageRules.MaxZipBytes;
});
builder.Services.AddServerInfrastructure(builder.Configuration);
builder.Services.AddAuthorization();

if (azureAd.IsConfigured)
{
    JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

    var audience = string.IsNullOrWhiteSpace(azureAd.ApiAudience)
        ? azureAd.ClientId
        : azureAd.ApiAudience;

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.MetadataAddress = azureAd.MetadataAddress;
            options.Audience = audience;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = "name",
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                // Accept app client id and optional dedicated API audience (ID vs access token).
                ValidAudiences = string.IsNullOrWhiteSpace(azureAd.ApiAudience)
                    ? [azureAd.ClientId]
                    : [azureAd.ApiAudience, azureAd.ClientId]
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken)
                        && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetService<ILoggerFactory>()
                        ?.CreateLogger("SGSClient.Api.Jwt");
                    logger?.LogWarning(context.Exception, "JWT authentication failed for {Path}",
                        context.Request.Path);
                    return Task.CompletedTask;
                }
            };
        });
}
else
{
    // Without Azure AD every caller is local user 1 with admin rights: development only.
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "AzureAd is not configured. Set AzureAd:Enabled=true, AzureAd:ClientId, AzureAd:TenantName and AzureAd:SigninPolicy.");
    }

    // Always register a default scheme so AuthorizationMiddleware Challenge does not 500
    // when AzureAd is disabled (see MeController / [Authorize] without JwtBearer).
    builder.Services
        .AddAuthentication("Disabled")
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DisabledAuthenticationHandler>(
            "Disabled",
            _ => { });
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Traefik / nginx / Portainer proxy sits in another container.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

await SgsServerDbInitializer.InitializeAsync(app.Services);

app.UseForwardedHeaders();

var httpsRedirect = app.Configuration.GetValue("HttpsRedirection:Enabled", app.Environment.IsDevelopment());
if (httpsRedirect)
{
    app.UseHttpsRedirection();
}

app.UseCors();

app.MapGet("/health", (IOptionsMonitor<MaintenanceSettings> maintenance) =>
    Results.Ok(new { status = "ok", maintenance = maintenance.CurrentValue.Enabled }));

LogLegacyApi(
    app,
    app.Services.GetRequiredService<IRofwLegacyService>().IsConfigured,
    "ROFW compatibility API is enabled (/api/rofw/* and /rofw/*).",
    "ROFW compatibility API is disabled. Set ConnectionStrings:RofwDatabase to keep Realm of Five Winds working.");
LogLegacyApi(
    app,
    app.Services.GetRequiredService<IReportingoLegacyService>().IsConfigured,
    "Reportingo compatibility API is enabled (/api/reportingo/* and /reportingo/*).",
    "Reportingo compatibility API is disabled. Set ConnectionStrings:ReportingoDatabase to keep bug reports working.");

app.UseAuthentication();
app.UseMiddleware<MaintenanceMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/social");

app.Run();

public partial class Program
{
    private static void LogLegacyApi(WebApplication app, bool enabled, string enabledMessage, string disabledMessage)
    {
        if (enabled)
        {
            app.Logger.LogInformation(enabledMessage);
        }
        else
        {
            app.Logger.LogWarning(disabledMessage);
        }
    }
}
