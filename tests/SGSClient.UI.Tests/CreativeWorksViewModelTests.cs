using System.Net;
using System.Net.Http.Json;
using SGSClient.Core.DTOs;
using SGSClient.Infrastructure.Client.Services;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Tests;

/// <summary>CreativeWorksViewModel against the real HttpSgsApiService with a recording stub server.</summary>
public sealed class CreativeWorksViewModelTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath.TrimStart('/');
            Calls.Add($"{request.Method} {path}");
            object? body = (request.Method.Method, path) switch
            {
                ("GET", "api/art/mine") => new[] { Art(1, "Mine one", "me"), Art(2, "Mine two", "me") },
                ("GET", "api/admin/art") => new[] { Art(1, "Mine one", "me"), Art(3, "Other", "bob") },
                ("PUT", "api/art/1") => Art(1, "Renamed", "me"),
                ("DELETE", "api/art/1") => null,
                _ => null
            };
            var status = body is null && request.Method != HttpMethod.Delete ? HttpStatusCode.NotFound : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status) { Content = body is null ? null : JsonContent.Create(body, body.GetType()) });
        }
    }

    private static ArtworkDto Art(int id, string title, string author) => new()
    {
        Id = id, Title = title, AuthorDisplayName = author, FileUrl = $"api/art/{id}/file", CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-id)
    };

    private static (CreativeWorksViewModel Vm, StubHandler Handler) Create(CreativeWorksMode mode)
    {
        var handler = new StubHandler();
        var api = new HttpSgsApiService(new HttpClient(handler) { BaseAddress = new Uri("http://test/") });
        return (new CreativeWorksViewModel(api, "http://test/", "art", mode), handler);
    }

    [Fact]
    public async Task Load_Mine_UsesMineEndpoint_AndBuildsAbsoluteThumbs()
    {
        var (vm, handler) = Create(CreativeWorksMode.Mine);

        await vm.LoadAsync();

        Assert.Contains("GET api/art/mine", handler.Calls);
        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal("http://test/api/art/1/file", vm.Rows[0].ThumbUrl);
        Assert.Equal(1, vm.SelectedRow?.Id);
    }

    [Fact]
    public async Task Load_Admin_UsesAdminEndpoint_AndSearchFiltersByAuthor()
    {
        var (vm, handler) = Create(CreativeWorksMode.Admin);

        await vm.LoadAsync();
        Assert.Contains("GET api/admin/art", handler.Calls);
        Assert.Equal(2, vm.Rows.Count);

        vm.SearchText = "bob";
        Assert.Single(vm.Rows);
        Assert.Equal(3, vm.Rows[0].Id);
    }

    [Fact]
    public async Task Save_CallsUpdateEndpoint()
    {
        var (vm, handler) = Create(CreativeWorksMode.Mine);
        await vm.LoadAsync();
        vm.EditTitle = "Renamed";

        await vm.SaveAsync();

        Assert.Contains("PUT api/art/1", handler.Calls);
        Assert.Empty(vm.Issues);
    }

    [Fact]
    public async Task Delete_RemovesItem_AndHonoursConfirmation()
    {
        var (vm, handler) = Create(CreativeWorksMode.Mine);
        await vm.LoadAsync();

        vm.SetConfirmDeleteHandler(_ => Task.FromResult(false));
        await vm.DeleteAsync();
        Assert.Equal(2, vm.Rows.Count);

        vm.SetConfirmDeleteHandler(_ => Task.FromResult(true));
        await vm.DeleteAsync();

        Assert.Contains("DELETE api/art/1", handler.Calls);
        Assert.Single(vm.Rows);
        Assert.Equal(2, vm.SelectedRow?.Id);
    }
}
