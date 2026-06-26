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

// ─── Schema: cria tabelas via SQL idempotente (EnsureCreated não funciona
//     quando o banco postgres já existe no Cloud SQL) ────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RouteDbContext>();
    var conn = db.Database.GetDbConnection();
    conn.Open();

    // Remove tabelas com schema Long-based (pré-UUID) se ainda existirem
    using var checkCmd = conn.CreateCommand();
    checkCmd.CommandText = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'presenca_outbox')";
    var schemaOk = (bool)(checkCmd.ExecuteScalar() ?? false);
    if (!schemaOk)
    {
        using var dropCmd = conn.CreateCommand();
        dropCmd.CommandText = @"
            DROP TABLE IF EXISTS paradas_rota CASCADE;
            DROP TABLE IF EXISTS pontos_embarque CASCADE;
            DROP TABLE IF EXISTS rotas CASCADE;
            DROP TABLE IF EXISTS ""__EFMigrationsHistory"" CASCADE;
        ";
        dropCmd.ExecuteNonQuery();
    }

    using var createCmd = conn.CreateCommand();
    createCmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS pontos_embarque (
            ""Id""           uuid                        NOT NULL,
            ""AlunoId""      uuid                        NOT NULL,
            ""Matricula""    character varying(50)       NOT NULL,
            ""Nome""         character varying(255)      NOT NULL,
            ""Latitude""     double precision            NOT NULL,
            ""Longitude""    double precision            NOT NULL,
            ""Endereco""     character varying(255),
            ""AtualizadoEm"" timestamp with time zone   NOT NULL,
            CONSTRAINT ""PK_pontos_embarque"" PRIMARY KEY (""Id"")
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_pontos_embarque_AlunoId""
            ON pontos_embarque (""AlunoId"");

        CREATE TABLE IF NOT EXISTS rotas (
            ""Id""                   uuid                        NOT NULL,
            ""VeiculoId""            uuid                        NOT NULL,
            ""RotaTransporte""       character varying(100),
            ""CursoId""              uuid,
            ""Data""                 date                        NOT NULL,
            ""DistanciaTotalMetros"" double precision            NOT NULL,
            ""Status""               integer                     NOT NULL,
            ""DestinoLatitude""      double precision            NOT NULL,
            ""DestinoLongitude""     double precision            NOT NULL,
            ""DestinoNome""          character varying(255),
            ""CriadoEm""             timestamp with time zone   NOT NULL,
            CONSTRAINT ""PK_rotas"" PRIMARY KEY (""Id"")
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_rotas_VeiculoId_Data""
            ON rotas (""VeiculoId"", ""Data"");

        CREATE TABLE IF NOT EXISTS paradas_rota (
            ""Id""           uuid                        NOT NULL,
            ""RotaId""       uuid                        NOT NULL,
            ""AlunoId""      uuid                        NOT NULL,
            ""Matricula""    character varying(50)       NOT NULL,
            ""Nome""         character varying(255)      NOT NULL,
            ""Ordem""        integer                     NOT NULL,
            ""Latitude""     double precision            NOT NULL,
            ""Longitude""    double precision            NOT NULL,
            ""ClusterId""    integer                     NOT NULL,
            ""Confirmada""   boolean                     NOT NULL,
            ""ConfirmadaEm"" timestamp with time zone,
            CONSTRAINT ""PK_paradas_rota"" PRIMARY KEY (""Id""),
            CONSTRAINT ""FK_paradas_rota_rotas_RotaId""
                FOREIGN KEY (""RotaId"") REFERENCES rotas (""Id"") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_paradas_rota_RotaId_AlunoId""
            ON paradas_rota (""RotaId"", ""AlunoId"");

        CREATE TABLE IF NOT EXISTS presenca_outbox (
            ""Id""                   uuid                        NOT NULL,
            ""AlunoId""              uuid                        NOT NULL,
            ""CursoId""              uuid                        NOT NULL,
            ""Role""                 character varying(50)       NOT NULL,
            ""ProfileId""            uuid                        NOT NULL,
            ""Status""               integer                     NOT NULL,
            ""Tentativas""           integer                     NOT NULL,
            ""ProximaTentativaEm""   timestamp with time zone   NOT NULL,
            ""CriadoEm""             timestamp with time zone   NOT NULL,
            ""EnviadoEm""            timestamp with time zone,
            ""UltimoErro""           character varying(1000),
            CONSTRAINT ""PK_presenca_outbox"" PRIMARY KEY (""Id"")
        );
        CREATE INDEX IF NOT EXISTS ""IX_presenca_outbox_Status_ProximaTentativaEm""
            ON presenca_outbox (""Status"", ""ProximaTentativaEm"");
    ";
    createCmd.ExecuteNonQuery();

    conn.Close();
}

app.MapControllers();
app.MapHub<RouteHub>("/api/rotas/hub");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/rotas/health", () => Results.Ok(new { status = "ok" }));

app.Run();
