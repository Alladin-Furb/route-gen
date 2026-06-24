using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RouteGen.Configuration;
using RouteGen.Data;
using RouteGen.Data.Entities;
using RouteGen.Domain;
using RouteGen.Dtos;

namespace RouteGen.Services;

/// <summary>Erro de regra de negócio na geração de rota (ex.: capacidade excedida).</summary>
public class RouteGenerationException : Exception
{
    public RouteGenerationException(string message) : base(message) { }
}

/// <summary>
/// Orquestra a geração da "melhor rota": carrega pontos de embarque, agrupa
/// pontos próximos, otimiza a ordem (Haversine + 2-opt) e persiste a rota.
/// </summary>
public class RouteGenerationService
{
    private readonly RouteDbContext _db;
    private readonly Integration.RegisterClient _register;
    private readonly ClusteringService _clustering;
    private readonly RouteOptimizationService _optimizer;
    private readonly RoutingOptions _options;

    public RouteGenerationService(
        RouteDbContext db,
        Integration.RegisterClient register,
        ClusteringService clustering,
        RouteOptimizationService optimizer,
        IOptions<RoutingOptions> options)
    {
        _db = db;
        _register = register;
        _clustering = clustering;
        _optimizer = optimizer;
        _options = options.Value;
    }

    public async Task<Rota> GerarRotaAsync(GerarRotaRequest request, CancellationToken ct = default)
    {
        var students = await CarregarAlunosAsync(request, ct);

        if (students.Count == 0)
            throw new RouteGenerationException(
                "Nenhum aluno com ponto de embarque encontrado para os critérios informados.");

        var veiculo = await _register.ObterVeiculoAsync(request.VeiculoId, ct);
        if (veiculo is not null && veiculo.Capacidade > 0 && students.Count > veiculo.Capacidade)
            throw new RouteGenerationException(
                $"Capacidade do veículo excedida: {students.Count} alunos para {veiculo.Capacidade} lugares.");

        var clusters = _clustering.Cluster(students, _options.ClusterRadiusMeters);

        var optimized = _optimizer.Optimize(
            clusters,
            request.OrigemLatitude, request.OrigemLongitude,
            request.DestinoLatitude, request.DestinoLongitude);

        var rota = new Rota
        {
            VeiculoId = request.VeiculoId,
            RotaTransporte = request.RotaTransporte,
            CursoId = request.CursoId,
            Data = DateOnly.FromDateTime(DateTime.UtcNow),
            DistanciaTotalMetros = optimized.TotalDistanceMeters,
            Status = StatusRota.Gerada,
            DestinoLatitude = request.DestinoLatitude,
            DestinoLongitude = request.DestinoLongitude,
            DestinoNome = request.DestinoNome
        };

        int ordem = 0;
        foreach (var cluster in optimized.Stops)
        {
            foreach (var member in cluster.Members)
            {
                rota.Paradas.Add(new ParadaRota
                {
                    AlunoId = member.Id,
                    Matricula = member.Matricula,
                    Nome = member.Name,
                    Ordem = ordem,
                    Latitude = cluster.Latitude,
                    Longitude = cluster.Longitude,
                    ClusterId = cluster.Id
                });
            }
            ordem++;
        }

        _db.Rotas.Add(rota);
        await _db.SaveChangesAsync(ct);

        return rota;
    }

    private async Task<List<Student>> CarregarAlunosAsync(GerarRotaRequest request, CancellationToken ct)
    {
        var pontos = await _db.PontosEmbarque.AsNoTracking().ToListAsync(ct);
        var porAluno = pontos.ToDictionary(p => p.AlunoId);

        // Sem cadastro configurado: usa todos os pontos de embarque conhecidos.
        if (!_register.IsConfigured)
            return pontos.Select(ToStudent).ToList();

        var alunos = await _register.ListarAlunosAsync(ct);

        IEnumerable<Integration.RegisterClient.AlunoDto> filtrados = alunos;

        if (!string.IsNullOrWhiteSpace(request.RotaTransporte))
            filtrados = filtrados.Where(a =>
                string.Equals(a.RotaTransporte, request.RotaTransporte, StringComparison.OrdinalIgnoreCase));

        if (request.CursoId is not null)
            filtrados = filtrados.Where(a => a.CursoId == request.CursoId);

        var result = new List<Student>();
        foreach (var aluno in filtrados)
        {
            if (!porAluno.TryGetValue(aluno.Id, out var ponto))
                continue; // sem ponto de embarque cadastrado, não entra na rota

            result.Add(new Student
            {
                Id = aluno.Id,
                Matricula = aluno.Matricula ?? ponto.Matricula,
                Name = aluno.Nome ?? ponto.Nome,
                Latitude = ponto.Latitude,
                Longitude = ponto.Longitude
            });
        }

        return result;
    }

    private static Student ToStudent(PontoEmbarque p) => new()
    {
        Id = p.AlunoId,
        Matricula = p.Matricula,
        Name = p.Nome,
        Latitude = p.Latitude,
        Longitude = p.Longitude
    };
}
