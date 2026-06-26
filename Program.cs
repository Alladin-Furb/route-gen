using Microsoft.EntityFrameworkCore;
using RouteGen.Configuration;
using RouteGen.Data;
using RouteGen.Hubs;
using RouteGen.Integration;
using RouteGen.Notifications;
using RouteGen.Realtime;
using RouteGen.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── Configuração ────────────────────────────────────────────────────────────
builder.Services.Configure<RoutingOptions>(
    builder.Configuration.GetSection(RoutingOptions.SectionName));
builder.Services.Configure<ServicesOptions>(
    builder.Configuration.GetSection(ServicesOptions.SectionName));

// ─── Banco (PostgreSQL: Cloud SQL em produção, route-gen-db local) ───────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<RouteDbContext>(options =>
    options.UseNpgsql(connectionString));

// ─── Tempo real (SignalR), com backplane Redis opcional para múltiplas réplicas
var redisConnection = builder.Configuration.GetConnectionString("Redis");
var signalR = builder.Services.AddSignalR();
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    signalR.AddStackExchangeRedis(redisConnection);
}

// ─── Serviços de domínio ─────────────────────────────────────────────────────
builder.Services.AddScoped<ClusteringService>();
builder.Services.AddScoped<RouteOptimizationService>();
builder.Services.AddScoped<RouteGenerationService>();
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddSingleton<IRouteNotifier, RouteNotifier>();

// ─── Propagação assíncrona de presença ao attendance (outbox durável) ────────
builder.Services.AddHostedService<PresencaPropagacaoWorker>();

// ─── Clientes HTTP dos serviços vizinhos ─────────────────────────────────────
builder.Services.AddHttpClient<RegisterClient>();
builder.Services.AddHttpClient<AttendanceClient>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ─── Schema idempotente: detecta schema Long-based (pré-UUID) e recria ────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RouteDbContext>();
    var conn = db.Database.GetDbConnection();
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'presenca_outbox')";
    var schemaUpToDate = (bool)(cmd.ExecuteScalar() ?? false);
    conn.Close();
    if (!schemaUpToDate)
    {
        db.Database.EnsureDeleted();
    }
    db.Database.EnsureCreated();
}

app.MapControllers();
app.MapHub<RouteHub>("/api/rotas/hub");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/rotas/health", () => Results.Ok(new { status = "ok" }));

app.Run();
