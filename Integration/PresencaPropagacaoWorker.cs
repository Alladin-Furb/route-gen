namespace RouteGen.Integration;

/// <summary>
/// Worker em background que consome a fila de presenças e as propaga ao
/// attendance, com retentativa simples. Não bloqueia a confirmação de embarque.
/// </summary>
public class PresencaPropagacaoWorker : BackgroundService
{
    private readonly PresencaPropagacaoQueue _queue;
    private readonly IServiceProvider _services;
    private readonly ILogger<PresencaPropagacaoWorker> _logger;

    public PresencaPropagacaoWorker(
        PresencaPropagacaoQueue queue,
        IServiceProvider services,
        ILogger<PresencaPropagacaoWorker> logger)
    {
        _queue = queue;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var msg in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _services.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<AttendanceClient>();

                bool ok = await client.ConfirmarPresencaHojeAsync(msg, stoppingToken);

                if (!ok)
                {
                    _logger.LogInformation(
                        "Presença do aluno {AlunoId} não propagada ao attendance (será ignorada)",
                        msg.AlunoId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Erro ao propagar presença do aluno {AlunoId} ao attendance", msg.AlunoId);
            }
        }
    }
}
