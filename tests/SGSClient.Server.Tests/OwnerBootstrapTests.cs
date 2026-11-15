using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.Enums;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Services;
using SGSClient.Infrastructure.Server.Services;

namespace SGSClient.Server.Tests;

public sealed class OwnerBootstrapTests
{
    private const string OwnerEmail = "owner@example.com";

    [Fact]
    public async Task OwnerEmail_SeedsTheFirstSuperAdmin_ButNotALaterAccountWithTheSameEmail()
    {
        await using var db = new SgsServerDbContext(new DbContextOptionsBuilder<SgsServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
        var adminAuth = new AdminAuthorization(
            Options.Create(new AdminSettings { Emails = [OwnerEmail] }),
            Options.Create(new AzureAdSettings { Enabled = true, ClientId = "c", TenantName = "t", SigninPolicy = "p" }));
        var users = new EfUserService(db, adminAuth);

        var owner = await users.UpsertFromClaimsAsync("owner-sub", "Owner", OwnerEmail, null);
        // Someone else signs up with the owner's address (an IdP that does not verify email).
        var impostor = await users.UpsertFromClaimsAsync("other-sub", "Impostor", OwnerEmail, null);

        Assert.Equal(UserRole.SuperAdmin, owner.Role);
        Assert.Equal(UserRole.User, impostor.Role);
        Assert.False(adminAuth.IsAdmin(null, OwnerEmail, impostor.Role));
    }
}
