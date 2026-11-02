using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Infrastructure.Client.Services;

namespace SGSClient.UI.Tests;

public class NetworkStatusServiceTests
{
    private sealed class Handler(string healthJson, HttpStatusCode healthStatus) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.RequestUri!.AbsolutePath == "/health"
                ? new HttpResponseMessage(healthStatus) { Content = new StringContent(healthJson, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.OK));
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    [Theory]
    [InlineData("""{"status":"ok","maintenance":true}""", HttpStatusCode.OK, true)]
    [InlineData("""{"status":"ok","maintenance":false}""", HttpStatusCode.OK, false)]
    [InlineData("""{"status":"ok"}""", HttpStatusCode.OK, false)] // API sprzed trybu maintenance
    [InlineData("nie json", HttpStatusCode.OK, false)]
    [InlineData("", HttpStatusCode.InternalServerError, false)]
    public async Task Probe_ReadsMaintenanceFromHealth(string json, HttpStatusCode status, bool expected)
    {
        using var sut = new NetworkStatusService(
            Options.Create(new ApiClientSettings { BaseUrl = "https://api.test" }),
            new Factory(new Handler(json, status)));

        await sut.StartAsync();

        // Sonda sprawdza też lokalną sieć; bez niej tryb maintenance jest zawsze wyłączony.
        Assert.Equal(expected && System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable(), sut.IsMaintenance);
    }
}
