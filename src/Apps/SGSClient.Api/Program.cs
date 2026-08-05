using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SGSClient.Core.Configuration;
using SGSClient.Infrastructure.Server;
using SGSClient.Infrastructure.Server.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Services.Configure<AzureAdSettings>(builder.Configuration.GetSection(AzureAdSettings.SectionName));
var azureAd = builder.Configuration.GetSection(AzureAdSettings.SectionName).Get<AzureAdSettings>() ?? new AzureAdSettings();

builder.Services.AddControllers();
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

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MetadataAddress = azureAd.MetadataAddress;
            options.Audience = string.IsNullOrWhiteSpace(azureAd.ApiAudience)
                ? azureAd.ClientId
                : azureAd.ApiAudience;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = "name",
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true
            };
        });
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

await SgsServerDbInitializer.InitializeAsync(app.Services);

app.UseHttpsRedirection();
app.UseCors();

if (azureAd.IsConfigured)
{
    app.UseAuthentication();
}

app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
