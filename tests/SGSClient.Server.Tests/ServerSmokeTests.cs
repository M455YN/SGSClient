using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server;
using SGSClient.Infrastructure.Server.Persistence;

namespace SGSClient.Server.Tests;

public class ServerInfrastructureTests
{
    [Fact]
    public void AddServerInfrastructure_RegistersGameRepository()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SgsDatabase"] =
                    "Data Source=localhost;Initial Catalog=sgsclient_test;Integrated Security=True;TrustServerCertificate=True"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddServerInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<SgsServerDbContext>());
        Assert.NotNull(provider.GetService<IGameRepository>());
    }
}
