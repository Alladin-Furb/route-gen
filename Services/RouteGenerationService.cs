using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RouteGen.Configuration;
using RouteGen.Data;
using RouteGen.Data.Entities;
using RouteGen.Domain;
using RouteGen.Dtos;
using RouteGen.Geo;

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
    private readonly Integration.AttendanceClient _attendance;
    private readonly ClusteringService _clustering;
    private readonly RouteOptimizationService _optimizer;
    private readonly RoutingOptions _options;

    public RouteGenerationService(
        RouteDbContext db,
        Integration.RegisterClient register,
        Integration.AttendanceClient attendance,
        ClusteringService clustering,
        RouteOptimizationService optimizer,
        IOptions<RoutingOptions> options)
    {
        _db = db;
        _register = register;
        _attendance = attendance;
        _clustering = clustering;
        _optimizer = optimizer;
        _options = options.Value;
    }

    public async Task<Rota> GerarRotaAsync(
        GerarRotaRequest request, string? callerRole, Guid? callerProfileId, CancellationToken ct = default)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var students = await CarregarAlunosAsync(request, hoje, callerRole, callerProfileId, ct);

        if (students.Count == 0)
            throw new RouteGenerationException(
                "Nenhum aluno confirmado com ponto de embarque encontrado para os critérios informados.");

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
            Data = hoje,
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

    /// <summary>
    /// Gera MÚLTIPLAS rotas para uma viagem (curso): aloca a frota cadastrada
    /// (maior capacidade primeiro), divide os alunos confirmados por capacidade
    /// do veículo e agrupa quem está geograficamente próximo na mesma rota.
    /// </summary>
    public async Task<List<Rota>> GerarRotasPorViagemAsync(
        GerarRotaViagemRequest request, string? callerRole, Guid? callerProfileId, CancellationToken ct = default)
    {
        var dia = request.Data ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var students = await CarregarAlunosPorViagemAsync(request.CursoId, dia, callerRole, callerProfileId, ct);
        if (students.Count == 0)
            throw new RouteGenerationException(
                "Nenhum aluno confirmado com ponto de embarque encontrado para esta viagem.");

        var veiculos = (await _register.ListarVeiculosAsync(ct))
            .Where(v => v.Capacidade > 0)
            .OrderByDescending(v => v.Capacidade)
            .ToList();
        if (veiculos.Count == 0)
            throw new RouteGenerationException("Nenhum veículo cadastrado para alocar as rotas.");

        var capacidadeTotal = veiculos.Sum(v => v.Capacidade);
        if (students.Count > capacidadeTotal)
            throw new RouteGenerationException(
                $"Frota insuficiente: {students.Count} alunos para {capacidadeTotal} lugares disponíveis.");

        // Ordena os alunos por proximidade (cadeia nearest-neighbor a partir da
        // origem) para que, ao fatiar por capacidade, alunos próximos fiquem juntos.
        var ordenados = OrdenarPorProximidade(students, request.OrigemLatitude, request.OrigemLongitude);

        // Regeração idempotente: remove rotas anteriores da viagem nessa data.
        var antigas = await _db.Rotas
            .Where(r => r.CursoId == request.CursoId && r.Data == dia)
            .ToListAsync(ct);
        if (antigas.Count > 0)
            _db.Rotas.RemoveRange(antigas);

        var rotas = new List<Rota>();
        int idx = 0;
        foreach (var veiculo in veiculos)
        {
            if (idx >= ordenados.Count)
                break;

            var fatia = ordenados.Skip(idx).Take(veiculo.Capacidade).ToList();
            idx += fatia.Count;

            var clusters = _clustering.Cluster(fatia, _options.ClusterRadiusMeters);
            var optimized = _optimizer.Optimize(
                clusters,
                request.OrigemLatitude, request.OrigemLongitude,
                request.DestinoLatitude, request.DestinoLongitude);

            var rota = new Rota
            {
                VeiculoId = veiculo.Id,
                RotaTransporte = null,
                CursoId = request.CursoId,
                Data = dia,
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
            rotas.Add(rota);
        }

        await _db.SaveChangesAsync(ct);
        return rotas;
    }

    /// <summary>Ordena alunos por vizinho-mais-próximo a partir da origem.</summary>
    private static List<Student> OrdenarPorProximidade(List<Student> students, double origemLat, double origemLon)
    {
        var restantes = new List<Student>(students);
        var ordenados = new List<Student>(students.Count);
        double curLat = origemLat, curLon = origemLon;

        while (restantes.Count > 0)
        {
            int melhor = 0;
            double melhorDist = double.MaxValue;
            for (int i = 0; i < restantes.Count; i++)
            {
                double d = GeoUtils.HaversineMeters(curLat, curLon, restantes[i].Latitude, restantes[i].Longitude);
                if (d < melhorDist)
                {
                    melhorDist = d;
                    melhor = i;
                }
            }
            var escolhido = restantes[melhor];
            restantes.RemoveAt(melhor);
            ordenados.Add(escolhido);
            curLat = escolhido.Latitude;
            curLon = escolhido.Longitude;
        }

        return ordenados;
    }

    private async Task<List<Student>> CarregarAlunosPorViagemAsync(
        Guid cursoId, DateOnly data, string? callerRole, Guid? callerProfileId, CancellationToken ct)
    {
        var pontos = await _db.PontosEmbarque.AsNoTracking().ToListAsync(ct);
        var porAluno = pontos.ToDictionary(p => p.AlunoId);

        if (_attendance.IsConfigured)
        {
            var confirmados = await _attendance.ListarConfirmadosAsync(data, cursoId, callerRole, callerProfileId, ct);
            var result = new List<Student>();
            foreach (var confirmado in confirmados)
            {
                if (!porAluno.TryGetValue(confirmado.AlunoId, out var ponto))
                    continue;
                result.Add(new Student
                {
                    Id = confirmado.AlunoId,
                    Matricula = confirmado.AlunoMatricula ?? ponto.Matricula,
                    Name = confirmado.AlunoNome ?? ponto.Nome,
                    Latitude = ponto.Latitude,
                    Longitude = ponto.Longitude
                });
            }
            return result;
        }

        if (!_register.IsConfigured)
            return pontos.Select(ToStudent).ToList();

        var alunos = await _register.ListarAlunosAsync(ct);
        var resultRegister = new List<Student>();
        foreach (var aluno in alunos.Where(a => a.CursoId == cursoId))
        {
            if (!porAluno.TryGetValue(aluno.Id, out var ponto))
                continue;
            resultRegister.Add(new Student
            {
                Id = aluno.Id,
                Matricula = aluno.Matricula ?? ponto.Matricula,
                Name = aluno.Nome ?? ponto.Nome,
                Latitude = ponto.Latitude,
                Longitude = ponto.Longitude
            });
        }
        return resultRegister;
    }

    private async Task<List<Student>> CarregarAlunosAsync(
        GerarRotaRequest request, DateOnly data, string? callerRole, Guid? callerProfileId, CancellationToken ct)
    {
        var pontos = await _db.PontosEmbarque.AsNoTracking().ToListAsync(ct);
        var porAluno = pontos.ToDictionary(p => p.AlunoId);

        // Caminho principal: a fonte da verdade de "quem viaja hoje" é o attendance.
        // Cruzamos os alunos confirmados com os pontos de embarque conhecidos.
        if (_attendance.IsConfigured)
        {
            var confirmados = await _attendance.ListarConfirmadosAsync(
                data, request.CursoId, callerRole, callerProfileId, ct);

            var result = new List<Student>();
            foreach (var confirmado in confirmados)
            {
                if (!porAluno.TryGetValue(confirmado.AlunoId, out var ponto))
                    continue; // confirmou viagem mas ainda não definiu o ponto de embarque

                result.Add(new Student
                {
                    Id = confirmado.AlunoId,
                    Matricula = confirmado.AlunoMatricula ?? ponto.Matricula,
                    Name = confirmado.AlunoNome ?? ponto.Nome,
                    Latitude = ponto.Latitude,
                    Longitude = ponto.Longitude
                });
            }

            return result;
        }

        // Sem attendance configurado: usa o cadastro (register) como fonte de alunos.
        if (!_register.IsConfigured)
            return pontos.Select(ToStudent).ToList();

        var alunos = await _register.ListarAlunosAsync(ct);

        IEnumerable<Integration.RegisterClient.AlunoDto> filtrados = alunos;

        if (!string.IsNullOrWhiteSpace(request.RotaTransporte))
            filtrados = filtrados.Where(a =>
                string.Equals(a.RotaTransporte, request.RotaTransporte, StringComparison.OrdinalIgnoreCase));

        if (request.CursoId is not null)
            filtrados = filtrados.Where(a => a.CursoId == request.CursoId);

        var resultRegister = new List<Student>();
        foreach (var aluno in filtrados)
        {
            if (!porAluno.TryGetValue(aluno.Id, out var ponto))
                continue; // sem ponto de embarque cadastrado, não entra na rota

            resultRegister.Add(new Student
            {
                Id = aluno.Id,
                Matricula = aluno.Matricula ?? ponto.Matricula,
                Name = aluno.Nome ?? ponto.Nome,
                Latitude = ponto.Latitude,
                Longitude = ponto.Longitude
            });
        }

        return resultRegister;
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
