namespace RouteGen.Data.Entities;

/// <summary>
/// Parada de uma rota: representa um aluno (ou um cluster de alunos próximos)
/// e o estado da confirmação de embarque.
/// </summary>
public class ParadaRota
{
    public int Id { get; set; }

    public int RotaId { get; set; }

    public Rota? Rota { get; set; }

    public long AlunoId { get; set; }

    public string Matricula { get; set; } = "";

    public string Nome { get; set; } = "";

    /// <summary>Ordem de visita da parada na rota (0 = primeira).</summary>
    public int Ordem { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>Identificador do cluster ao qual o ponto pertence.</summary>
    public int ClusterId { get; set; }

    public bool Confirmada { get; set; }

    public DateTime? ConfirmadaEm { get; set; }
}
