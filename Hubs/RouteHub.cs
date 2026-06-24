using Microsoft.AspNetCore.SignalR;
using RouteGen.Auth;

namespace RouteGen.Hubs;

/// <summary>
/// Hub de tempo real para aluno e motorista. A autorização usa os headers que
/// o gateway injeta (<c>X-User-Role</c>/<c>X-Profile-Id</c>) no handshake.
/// Grupos:
///  - <c>rota-{rotaId}</c>: motorista/admin acompanham a rota e confirmações;
///  - <c>aluno-{alunoId}</c>: o aluno acompanha a posição da van e sua parada.
/// </summary>
public class RouteHub : Hub
{
    public static string RotaGroup(int rotaId) => $"rota-{rotaId}";

    public static string AlunoGroup(long alunoId) => $"aluno-{alunoId}";

    private GatewayUser CurrentUser =>
        GatewayUser.FromHeaders(Context.GetHttpContext()?.Request.Headers
            ?? new HeaderDictionary());

    /// <summary>Motorista/admin assina os eventos de uma rota.</summary>
    public async Task SubscreverRota(int rotaId)
    {
        var user = CurrentUser;
        if (!(user.IsMotorista || user.IsAdmin))
            throw new HubException("Apenas motorista ou administrador podem assinar a rota.");

        await Groups.AddToGroupAsync(Context.ConnectionId, RotaGroup(rotaId));
    }

    /// <summary>Aluno assina os eventos da própria parada/posição da van.</summary>
    public async Task SubscreverAluno(int rotaId)
    {
        var user = CurrentUser;
        if (user.ProfileId is null)
            throw new HubException("Identidade do aluno ausente.");

        // O aluno só pode acompanhar a si mesmo.
        await Groups.AddToGroupAsync(Context.ConnectionId, AlunoGroup(user.ProfileId.Value));
        await Groups.AddToGroupAsync(Context.ConnectionId, RotaGroup(rotaId));
    }
}
