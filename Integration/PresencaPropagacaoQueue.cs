using System.Threading.Channels;

namespace RouteGen.Integration;

/// <summary>
/// Fila em processo para propagação assíncrona de presenças ao attendance,
/// mantendo a confirmação de embarque fora do caminho crítico (resposta rápida).
/// </summary>
public class PresencaPropagacaoQueue
{
    private readonly Channel<PresencaPropagacao> _channel =
        Channel.CreateUnbounded<PresencaPropagacao>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(PresencaPropagacao item, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(item, ct);

    public IAsyncEnumerable<PresencaPropagacao> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}
