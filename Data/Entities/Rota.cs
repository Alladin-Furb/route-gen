namespace RouteGen.Data.Entities;

public enum StatusRota
{
    Gerada = 0,
    EmAndamento = 1,
    Concluida = 2,
    Cancelada = 3
}

/// <summary>
/// Rota gerada para um veículo, persistida para leitura imediata por aluno e motorista.
/// </summary>
public class Rota
{
    public int Id { get; set; }

    public long VeiculoId { get; set; }

    /// <summary>Agrupador de transporte vindo do cadastro (Aluno.rotaTransporte).</summary>
    public string? RotaTransporte { get; set; }

    public long? CursoId { get; set; }

    public DateOnly Data { get; set; }

    public double DistanciaTotalMetros { get; set; }

    public StatusRota Status { get; set; } = StatusRota.Gerada;

    public double DestinoLatitude { get; set; }

    public double DestinoLongitude { get; set; }

    public string? DestinoNome { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public List<ParadaRota> Paradas { get; set; } = new();
}
