namespace RouteGen.Data.Entities;

/// <summary>
/// Ponto de embarque persistido por aluno. Resolve a lacuna de coordenadas do
/// ecossistema (o serviço de cadastro não persiste latitude/longitude).
/// </summary>
public class PontoEmbarque
{
    public int Id { get; set; }

    /// <summary>Id do aluno no serviço de cadastro (register).</summary>
    public long AlunoId { get; set; }

    public string Matricula { get; set; } = "";

    public string Nome { get; set; } = "";

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public string? Endereco { get; set; }

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}
