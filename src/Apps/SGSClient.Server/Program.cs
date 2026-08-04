using SGSClient.Infrastructure.Server;
using SGSClient.Server;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddServerInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
