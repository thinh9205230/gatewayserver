using GatewayServer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "GatewayServerService";
});

builder.Services.AddSingleton<SqliteRepository>();
builder.Services.AddSingleton<HeartbeatProcessor>();
builder.Services.AddSingleton<CommandStateStore>();
builder.Services.AddSingleton<FrontendCallbackService>();

builder.Services.AddSingleton<MqttHostedService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttHostedService>());

builder.Services.AddSingleton<HttpCommandServer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<HttpCommandServer>());

builder.Services.AddHostedService<GatewayTimeoutMonitor>();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();
app.Run();