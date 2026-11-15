using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SGSClient.Api.Controllers;
using SGSClient.Core.Configuration;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Services;
using SGSClient.Infrastructure.Server.Services;

namespace SGSClient.Server.Tests;

public sealed class AdminControllerGuardTests
{
    [Fact]
    public async Task EveryAdminAction_IsBlockedForACallerWhoIsNotAnAdmin()
    {
        await using var db = new SgsServerDbContext(new DbContextOptionsBuilder<SgsServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
        var azure = Options.Create(new AzureAdSettings { Enabled = true, ClientId = "c", TenantName = "t", SigninPolicy = "p" });
        var adminAuth = new AdminAuthorization(Options.Create(new AdminSettings()), azure);
        var controller = new AdminController(null!, null!, new EfUserService(db, adminAuth), adminAuth, azure)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var context = new ActionExecutingContext(
            new ActionContext(controller.HttpContext, new RouteData(), new ActionDescriptor()),
            [],
            new Dictionary<string, object?>(),
            controller);
        var actionRan = false;

        await controller.OnActionExecutionAsync(context, () =>
        {
            actionRan = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        Assert.False(actionRan);
        Assert.IsType<UnauthorizedResult>(context.Result);
    }
}
