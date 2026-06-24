namespace RouteGen.Domain;

/// <summary>
/// Aluno com ponto de embarque, usado como entrada para a geração de rota.
/// </summary>
public class Student
{
    public long Id { get; set; }

    public string Matricula { get; set; } = "";

    public string Name { get; set; } = "";

    public double Latitude { get; set; }

    public double Longitude { get; set; }
}
