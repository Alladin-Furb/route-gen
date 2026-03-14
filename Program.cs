using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

int port = 5001;

if (args.Length > 0)
{
    int.TryParse(args[0], out port);
}

builder.Configuration["NodeId"] = (port - 5000).ToString();

Console.WriteLine($"Rodando na porta {port}");
Console.WriteLine($"NodeId = {port - 5000}");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddGrpc();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(port, o =>
    {
        o.UseHttps();
    });
});

builder.Services.AddSingleton<BullyElectionService>();

var app = builder.Build();

app.UseHttpsRedirection();

// Registra serviço gRPC
app.MapGrpcService<ElectionServiceImpl>();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var service = app.Services.GetRequiredService<BullyElectionService>();

    _ = Task.Run(service.DiscoverLeader);
    _ = Task.Run(service.MonitorLeader);
});

app.MapGet("/start", async (BullyElectionService service) =>
{
    await service.StartElection();
    return "Election started";
});

app.Run();