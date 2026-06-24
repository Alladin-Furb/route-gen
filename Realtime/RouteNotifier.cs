using Microsoft.AspNetCore.SignalR;
using RouteGen.Dtos;
using RouteGen.Hubs;

namespace RouteGen.Realtime;

public interface IRouteNotifier
{
    Task RotaAtualizadaAsync(RotaResponse rota);

    Task PosicaoVanAsync(int rotaId, double latitude, double longitude);

    Task EmbarqueConfirmadoAsync(int rotaId, long alunoId, ParadaResponse parada);
}

/// <summary>
/// Publica eventos de tempo real para os grupos do <see cref="RouteHub"/>.
/// </summary>
public class RouteNotifier : IRouteNotifier
{
    private readonly IHubContext<RouteHub> _hub;

    public RouteNotifier(IHubContext<RouteHub> hub) => _hub = hub;

    public Task RotaAtualizadaAsync(RotaResponse rota) =>
        _hub.Clients.Group(RouteHub.RotaGroup(rota.Id)).SendAsync("RotaAtualizada", rota);

    public Task PosicaoVanAsync(int rotaId, double latitude, double longitude) =>
        _hub.Clients.Group(RouteHub.RotaGroup(rotaId))
            .SendAsync("PosicaoVan", new { rotaId, latitude, longitude });

    public async Task EmbarqueConfirmadoAsync(int rotaId, long alunoId, ParadaResponse parada)
    {
        // Motorista (grupo da rota) e o próprio aluno recebem imediatamente.
        await _hub.Clients.Group(RouteHub.RotaGroup(rotaId))
            .SendAsync("EmbarqueConfirmado", parada);
        await _hub.Clients.Group(RouteHub.AlunoGroup(alunoId))
            .SendAsync("EmbarqueConfirmado", parada);
    }
}
