using Microsoft.Extensions.Options;
using RouteGen.Configuration;
using System.Net.Http.Json;

namespace RouteGen.Integration;

/// <summary>
/// Mensagem de propagação de presença para o serviço de attendance.
/// </summary>
public record PresencaPropagacao(
    Guid AlunoId,
    Guid CursoId,
    string Role,
    Guid ProfileId);

/// <summary>
/// Cliente HTTP para o serviço de presença (attendance). Usado fora do caminho
/// crítico da confirmação de embarque (via fila/worker em background).
/// </summary>
public class AttendanceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<AttendanceClient> _logger;

    public AttendanceClient(HttpClient http, IOptions<ServicesOptions> options, ILogger<AttendanceClient> logger)
    {
        _http = http;
        _logger = logger;

        var baseUrl = options.Value.AttendanceBaseUrl;
        if (!string.IsNullOrWhiteSpace(baseUrl))
            _http.BaseAddress = new Uri(baseUrl);
    }

    public bool IsConfigured => _http.BaseAddress is not null;

    /// <summary>
    /// Confirma a presença de hoje no attendance:
    /// POST /api/v1/presencas/aluno/{alunoId}/curso/{cursoId}/confirmar-hoje?status=PRESENTE
    /// repassando os headers de identidade que o gateway exige.
    /// </summary>
    public async Task<bool> ConfirmarPresencaHojeAsync(PresencaPropagacao msg, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return false;

        var url = $"/api/v1/presencas/aluno/{msg.AlunoId}/curso/{msg.CursoId}/confirmar-hoje?status=PRESENTE";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("X-User-Role", msg.Role);
        request.Headers.Add("X-Profile-Id", msg.ProfileId.ToString());

        var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Attendance respondeu {Status} ao confirmar presença do aluno {AlunoId}",
                (int)response.StatusCode, msg.AlunoId);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Lê os alunos que confirmaram a viagem em uma data:
    /// GET /api/v1/presencas/confirmados?data=yyyy-MM-dd[&amp;cursoId=...]
    /// É a fonte de "quem viaja hoje" usada para montar a rota.
    /// </summary>
    public async Task<List<ConfirmadoDto>> ListarConfirmadosAsync(
        DateOnly data, Guid? cursoId, string? role, Guid? profileId, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new();

        var url = $"/api/v1/presencas/confirmados?data={data:yyyy-MM-dd}";
        if (cursoId is not null)
            url += $"&cursoId={cursoId}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-User-Role", string.IsNullOrWhiteSpace(role) ? "ROLE_ADMIN" : role);
        if (profileId is not null)
            request.Headers.Add("X-Profile-Id", profileId.Value.ToString());

        try
        {
            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Attendance respondeu {Status} ao listar confirmados de {Data}",
                    (int)response.StatusCode, data);
                return new();
            }

            var confirmados = await response.Content
                .ReadFromJsonAsync<List<ConfirmadoDto>>(cancellationToken: ct);
            return confirmados ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao listar confirmados do attendance para {Data}", data);
            return new();
        }
    }

    /// <summary>Confirmação de viagem (presença) retornada pelo attendance.</summary>
    public record ConfirmadoDto(
        Guid AlunoId,
        string? AlunoMatricula,
        string? AlunoNome,
        Guid? CursoId,
        string? Status);
}
