using Microsoft.EntityFrameworkCore;
using RouteGen.Data;
using RouteGen.Data.Entities;

namespace RouteGen.Integration;

/// <summary>
/// Worker em background que entrega as presenças do outbox ao attendance, com
/// retentativa e backoff exponencial. A confirmação de embarque grava a
/// pendência no banco (durável); aqui ela é consumida fora do caminho crítico.
/// </summary>
public class PresencaPropagacaoWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int MaxTentativas = 10;
    private const int LoteMax = 50;

    private readonly IServiceProvider _services;
    private readonly ILogger<PresencaPropagacaoWorker> _logger;

    public PresencaPropagacaoWorker(
        IServiceProvider services,
        ILogger<PresencaPropagacaoWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await ProcessarLoteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha no ciclo de propagação de presença");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessarLoteAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RouteDbContext>();
        var client = scope.ServiceProvider.GetRequiredService<AttendanceClient>();

        // Sem attendance configurado não há o que propagar; tenta no próximo ciclo.
        if (!client.IsConfigured)
            return;

        var agora = DateTime.UtcNow;

        var pendentes = await db.PresencaOutbox
            .Where(o => o.Status == OutboxStatus.Pendente && o.ProximaTentativaEm <= agora)
            .OrderBy(o => o.Id)
            .Take(LoteMax)
            .ToListAsync(ct);

        if (pendentes.Count == 0)
            return;

        foreach (var item in pendentes)
        {
            bool ok;
            try
            {
                ok = await client.ConfirmarPresencaHojeAsync(
                    new PresencaPropagacao(item.AlunoId, item.CursoId, item.Role, item.ProfileId), ct);
            }
            catch (Exception ex)
            {
                ok = false;
                item.UltimoErro = ex.Message;
            }

            item.Tentativas++;

            if (ok)
            {
                item.Status = OutboxStatus.Enviado;
                item.EnviadoEm = DateTime.UtcNow;
                item.UltimoErro = null;
            }
            else if (item.Tentativas >= MaxTentativas)
            {
                item.Status = OutboxStatus.Falhou;
                _logger.LogWarning(
                    "Presença do aluno {AlunoId} marcada como FALHOU após {Tentativas} tentativas",
                    item.AlunoId, item.Tentativas);
            }
            else
            {
                // Backoff exponencial limitado a 5 minutos: 2^tentativas segundos.
                var segundos = Math.Min(300, Math.Pow(2, item.Tentativas));
                item.ProximaTentativaEm = DateTime.UtcNow.AddSeconds(segundos);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
