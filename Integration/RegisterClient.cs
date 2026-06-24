using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using RouteGen.Configuration;
using RouteGen.Domain;

namespace RouteGen.Integration;

/// <summary>
/// Cliente HTTP para o serviço de cadastro (register), de onde vêm alunos e veículos.
/// O cadastro não persiste coordenadas; o ponto de embarque é mantido localmente.
/// </summary>
public class RegisterClient
{
    private readonly HttpClient _http;
    private readonly ILogger<RegisterClient> _logger;

    public RegisterClient(HttpClient http, IOptions<ServicesOptions> options, ILogger<RegisterClient> logger)
    {
        _http = http;
        _logger = logger;

        var baseUrl = options.Value.RegisterBaseUrl;
        if (!string.IsNullOrWhiteSpace(baseUrl))
            _http.BaseAddress = new Uri(baseUrl);
    }

    public bool IsConfigured => _http.BaseAddress is not null;

    public async Task<List<AlunoDto>> ListarAlunosAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new();

        try
        {
            var alunos = await _http.GetFromJsonAsync<List<AlunoDto>>("/alunos", ct);
            return alunos ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao listar alunos do serviço de cadastro");
            return new();
        }
    }

    public async Task<VehicleDto?> ObterVeiculoAsync(long veiculoId, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return null;

        try
        {
            return await _http.GetFromJsonAsync<VehicleDto>($"/veiculos/{veiculoId}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao obter veículo {VeiculoId} do serviço de cadastro", veiculoId);
            return null;
        }
    }

    public record AlunoDto(
        long Id,
        string? Matricula,
        string? Nome,
        string? RotaTransporte,
        long? CursoId);

    public record VehicleDto(long Id, string? Placa, string? Modelo, int Capacidade);
}
