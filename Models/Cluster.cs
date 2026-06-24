namespace RouteGen.Domain;

/// <summary>
/// Agrupamento de alunos geograficamente próximos. Cada cluster vira uma
/// parada compartilhada na rota, com coordenada igual ao centroide dos membros.
/// </summary>
public class Cluster
{
    public int Id { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public List<Student> Members { get; set; } = new();
}
