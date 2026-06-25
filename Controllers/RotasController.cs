using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RouteGen.Auth;
using RouteGen.Configuration;
using RouteGen.Data;
using RouteGen.Data.Entities;
using RouteGen.Dtos;
using RouteGen.Geo;
using RouteGen.Integration;
using RouteGen.Notifications;
using RouteGen.Realtime;
using RouteGen.Services;

namespace RouteGen.Controllers;

[ApiController]
[Route("api/rotas")]
public class RotasController : ControllerBase
{
    private readonly RouteDbContext _db;
    private readonly RouteGenerationService _generation;
    private readonly IRouteNotifier _notifier;
    private readonly NotificationService _notification;
    private readonly RoutingOptions _options;
    private readonly ILogger<RotasController> _logger;

    public RotasController(
        RouteDbContext db,
        RouteGenerationService generation,
        IRouteNotifier notifier,
        NotificationService notification,
        IOptions<RoutingOptions> options,
        ILogger<RotasController> logger)
    {
        _db = db;
        _generation = generation;
        _notifier = notifier;
        _notification = notification;
        _options = options.Value;
        _logger = logger;
    }

    private GatewayUser CurrentUser => GatewayUser.FromHeaders(Request.Headers);

    // ─── Ponto de embarque ────────────────────────────────────────────────

    /// <summary>Cadastra/atualiza o ponto de embarque de um aluno.</summary>
    [HttpPut("ponto-embarque")]
    public async Task<ActionResult<object>> SalvarPontoEmbarque(
        [FromBody] PontoEmbarqueRequest request, CancellationToken ct)
    {
        var user = CurrentUser;
        // Aluno só pode alterar o próprio ponto; admin pode alterar qualquer um.
        if (user.IsAluno && user.ProfileId != request.AlunoId)
            return Forbidden();

        if (!IsValidCoordinate(request.Latitude, request.Longitude))
            return BadRequest(new { mensagem = "Coordenadas inválidas." });

        var ponto = await _db.PontosEmbarque.FirstOrDefaultAsync(p => p.AlunoId == request.AlunoId, ct);

        if (ponto is null)
        {
            ponto = new PontoEmbarque { AlunoId = request.AlunoId };
            _db.PontosEmbarque.Add(ponto);
        }

        ponto.Matricula = request.Matricula ?? ponto.Matricula;
        ponto.Nome = request.Nome ?? ponto.Nome;
        ponto.Latitude = request.Latitude;
        ponto.Longitude = request.Longitude;
        ponto.Endereco = request.Endereco ?? ponto.Endereco;
        ponto.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            ponto.AlunoId,
            ponto.Latitude,
            ponto.Longitude,
            ponto.Endereco,
            ponto.AtualizadoEm
        });
    }

    [HttpGet("ponto-embarque/{alunoId:guid}")]
    public async Task<ActionResult<object>> ObterPontoEmbarque(Guid alunoId, CancellationToken ct)
    {
        var user = CurrentUser;
        if (user.IsAluno && user.ProfileId != alunoId)
            return Forbidden();

        var ponto = await _db.PontosEmbarque.AsNoTracking()
            .FirstOrDefaultAsync(p => p.AlunoId == alunoId, ct);

        if (ponto is null)
            return NotFound();

        return Ok(new
        {
            ponto.AlunoId,
            ponto.Matricula,
            ponto.Nome,
            ponto.Latitude,
            ponto.Longitude,
            ponto.Endereco,
            ponto.AtualizadoEm
        });
    }

    // ─── Geração e consulta de rotas ──────────────────────────────────────

    /// <summary>Gera a melhor rota para um veículo e a persiste.</summary>
    [HttpPost("gerar")]
    public async Task<ActionResult<RotaResponse>> GerarRota(
        [FromBody] GerarRotaRequest request, CancellationToken ct)
    {
        var user = CurrentUser;
        if (!(user.IsAdmin || user.IsMotorista))
            return Forbidden();

        try
        {
            var rota = await _generation.GerarRotaAsync(request, user.Role, user.ProfileId, ct);
            var response = RotaMapper.ToResponse(rota);

            // Renderização imediata para quem estiver acompanhando.
            await _notifier.RotaAtualizadaAsync(response);
            await _notification.NotifyDriver(
                $"Nova rota gerada para o veículo {rota.VeiculoId} com {rota.Paradas.Count} paradas.");

            return CreatedAtAction(nameof(ObterRota), new { id = rota.Id }, response);
        }
        catch (RouteGenerationException ex)
        {
            return UnprocessableEntity(new { mensagem = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RotaResponse>> ObterRota(Guid id, CancellationToken ct)
    {
        var rota = await _db.Rotas
            .Include(r => r.Paradas)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (rota is null)
            return NotFound();

        var user = CurrentUser;
        // Aluno só enxerga a própria parada dentro da rota.
        if (user.IsAluno)
        {
            if (user.ProfileId is null || rota.Paradas.All(p => p.AlunoId != user.ProfileId))
                return Forbidden();

            rota.Paradas = rota.Paradas.Where(p => p.AlunoId == user.ProfileId).ToList();
        }

        return Ok(RotaMapper.ToResponse(rota));
    }

    /// <summary>Rota corrente (mais recente do dia) de um veículo do motorista.</summary>
    [HttpGet("motorista/atual")]
    public async Task<ActionResult<RotaResponse>> RotaAtualDoMotorista(
        [FromQuery] Guid veiculoId, CancellationToken ct)
    {
        var user = CurrentUser;
        if (!(user.IsAdmin || user.IsMotorista))
            return Forbidden();

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        var rota = await _db.Rotas
            .Include(r => r.Paradas)
            .AsNoTracking()
            .Where(r => r.VeiculoId == veiculoId && r.Data == hoje)
            .OrderByDescending(r => r.CriadoEm)
            .FirstOrDefaultAsync(ct);

        if (rota is null)
            return NotFound();

        return Ok(RotaMapper.ToResponse(rota));
    }

    /// <summary>
    /// Rota gerada para uma viagem (curso) em uma data — usada para mostrar ao
    /// aluno os detalhes da viagem (veículo, paradas) quando já existe rota.
    /// </summary>
    [HttpGet("viagem/{cursoId:guid}")]
    public async Task<ActionResult<RotaResponse>> RotaPorViagem(
        Guid cursoId, [FromQuery] DateOnly? data, CancellationToken ct)
    {
        var dia = data ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var rota = await _db.Rotas
            .Include(r => r.Paradas)
            .AsNoTracking()
            .Where(r => r.CursoId == cursoId && r.Data == dia)
            .OrderByDescending(r => r.CriadoEm)
            .FirstOrDefaultAsync(ct);

        if (rota is null)
            return NotFound();

        return Ok(RotaMapper.ToResponse(rota));
    }

    /// <summary>
    /// Gera múltiplas rotas para uma viagem, alocando a frota cadastrada por
    /// capacidade do veículo e proximidade geográfica dos alunos confirmados.
    /// </summary>
    [HttpPost("gerar-viagem")]
    public async Task<ActionResult<IReadOnlyList<RotaResponse>>> GerarRotasViagem(
        [FromBody] GerarRotaViagemRequest request, CancellationToken ct)
    {
        var user = CurrentUser;
        if (!(user.IsAdmin || user.IsMotorista))
            return Forbidden();

        try
        {
            var rotas = await _generation.GerarRotasPorViagemAsync(request, user.Role, user.ProfileId, ct);
            var responses = rotas.Select(RotaMapper.ToResponse).ToList();

            foreach (var response in responses)
                await _notifier.RotaAtualizadaAsync(response);

            await _notification.NotifyDriver(
                $"{responses.Count} rota(s) gerada(s) para a viagem {request.CursoId}.");

            return Ok(responses);
        }
        catch (RouteGenerationException ex)
        {
            return UnprocessableEntity(new { mensagem = ex.Message });
        }
    }

    /// <summary>Lista todas as rotas geradas para uma viagem em uma data.</summary>
    [HttpGet("viagem/{cursoId:guid}/rotas")]
    public async Task<ActionResult<IReadOnlyList<RotaResponse>>> RotasPorViagem(
        Guid cursoId, [FromQuery] DateOnly? data, CancellationToken ct)
    {
        var dia = data ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var rotas = await _db.Rotas
            .Include(r => r.Paradas)
            .AsNoTracking()
            .Where(r => r.CursoId == cursoId && r.Data == dia)
            .OrderBy(r => r.CriadoEm)
            .ToListAsync(ct);

        return Ok(rotas.Select(RotaMapper.ToResponse).ToList());
    }

    /// <summary>Rota em que o aluno autenticado foi alocado para uma viagem/data.</summary>
    [HttpGet("viagem/{cursoId:guid}/minha")]
    public async Task<ActionResult<RotaResponse>> MinhaRotaNaViagem(
        Guid cursoId, [FromQuery] DateOnly? data, CancellationToken ct)
    {
        var user = CurrentUser;
        if (user.ProfileId is null)
            return Forbidden();

        var dia = data ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var rota = await _db.Rotas
            .Include(r => r.Paradas)
            .AsNoTracking()
            .Where(r => r.CursoId == cursoId && r.Data == dia && r.Paradas.Any(p => p.AlunoId == user.ProfileId))
            .OrderByDescending(r => r.CriadoEm)
            .FirstOrDefaultAsync(ct);

        if (rota is null)
            return NotFound();

        rota.Paradas = rota.Paradas.Where(p => p.AlunoId == user.ProfileId).ToList();
        return Ok(RotaMapper.ToResponse(rota));
    }

    // ─── Telemetria e confirmação ─────────────────────────────────────────

    /// <summary>Motorista publica a posição atual da van (telemetria em tempo real).</summary>
    [HttpPost("{rotaId:guid}/posicao")]
    public async Task<IActionResult> PublicarPosicao(
        Guid rotaId, [FromBody] PosicaoRequest posicao, CancellationToken ct)
    {
        var user = CurrentUser;
        if (!(user.IsAdmin || user.IsMotorista))
            return Forbidden();

        if (!await _db.Rotas.AnyAsync(r => r.Id == rotaId, ct))
            return NotFound();

        await _notifier.PosicaoVanAsync(rotaId, posicao.Latitude, posicao.Longitude);
        return Accepted();
    }

    /// <summary>
    /// Confirmação de embarque rápida por geolocalização. Valida o raio,
    /// grava, emite o evento de tempo real na hora e propaga a presença ao
    /// attendance de forma assíncrona (fora do caminho crítico).
    /// </summary>
    [HttpPost("{rotaId:guid}/paradas/{alunoId:guid}/confirmar")]
    public async Task<ActionResult<ConfirmacaoResponse>> ConfirmarEmbarque(
        Guid rotaId, Guid alunoId, [FromBody] PosicaoRequest posicao, CancellationToken ct)
    {
        var user = CurrentUser;
        if (user.IsAluno && user.ProfileId != alunoId)
            return Forbidden();

        if (!IsValidCoordinate(posicao.Latitude, posicao.Longitude))
            return BadRequest(new { mensagem = "Coordenadas inválidas." });

        var rota = await _db.Rotas
            .Include(r => r.Paradas)
            .FirstOrDefaultAsync(r => r.Id == rotaId, ct);

        if (rota is null)
            return NotFound();

        var parada = rota.Paradas.FirstOrDefault(p => p.AlunoId == alunoId);
        if (parada is null)
            return NotFound(new { mensagem = "Aluno não pertence a esta rota." });

        double distancia = GeoUtils.HaversineMeters(
            posicao.Latitude, posicao.Longitude, parada.Latitude, parada.Longitude);

        double raio = _options.ConfirmationRadiusMeters;

        if (distancia > raio)
        {
            return UnprocessableEntity(new ConfirmacaoResponse(
                false, distancia, raio,
                "Você está fora do raio de embarque desta parada."));
        }

        // Idempotente: confirmações repetidas não reprocessam.
        if (!parada.Confirmada)
        {
            parada.Confirmada = true;
            parada.ConfirmadaEm = DateTime.UtcNow;

            // Outbox durável: a intenção de propagar a presença é gravada na
            // MESMA transação da confirmação (atomicidade). Um worker entrega ao
            // attendance com retentativa/backoff, fora do caminho crítico.
            if (rota.CursoId is Guid cursoId)
            {
                _db.PresencaOutbox.Add(new Data.Entities.PresencaOutbox
                {
                    AlunoId = alunoId,
                    CursoId = cursoId,
                    Role = user.Role ?? Roles.Aluno,
                    ProfileId = user.ProfileId ?? alunoId
                });
            }

            await _db.SaveChangesAsync(ct);

            var paradaDto = RotaMapper.ToResponse(parada);

            // Evento de tempo real imediato para motorista e aluno.
            await _notifier.EmbarqueConfirmadoAsync(rotaId, alunoId, paradaDto);
        }

        return Ok(new ConfirmacaoResponse(
            true, distancia, raio, "Embarque confirmado."));
    }

    private ObjectResult Forbidden() =>
        StatusCode(StatusCodes.Status403Forbidden,
            new { mensagem = "Acesso negado para o papel atual." });

    private static bool IsValidCoordinate(double lat, double lon) =>
        lat is >= -90 and <= 90 && lon is >= -180 and <= 180;
}
