using RouteGen.Data.Entities;
using RouteGen.Dtos;

namespace RouteGen.Services;

/// <summary>Mapeia entidades persistidas para DTOs de resposta.</summary>
public static class RotaMapper
{
    public static RotaResponse ToResponse(Rota rota) => new(
        rota.Id,
        rota.VeiculoId,
        rota.RotaTransporte,
        rota.CursoId,
        rota.Data,
        rota.DistanciaTotalMetros,
        rota.Status.ToString(),
        rota.DestinoLatitude,
        rota.DestinoLongitude,
        rota.DestinoNome,
        rota.CriadoEm,
        rota.Paradas
            .OrderBy(p => p.Ordem)
            .Select(ToResponse)
            .ToList());

    public static ParadaResponse ToResponse(ParadaRota p) => new(
        p.Id,
        p.AlunoId,
        p.Matricula,
        p.Nome,
        p.Ordem,
        p.Latitude,
        p.Longitude,
        p.ClusterId,
        p.Confirmada,
        p.ConfirmadaEm);
}
