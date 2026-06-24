using Microsoft.Extensions.Options;
using RouteGen.Configuration;

namespace RouteGen.Integration;

/// <summary>
/// Mensagem de propagação de presença para o serviço de attendance.
/// </summary>
public record PresencaPropagacao(
    long AlunoId,
    long CursoId,
    string Role,
    long ProfileId);

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
}
