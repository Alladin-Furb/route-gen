namespace RouteGen.Dtos;

/// <summary>Cadastro/atualização do ponto de embarque de um aluno.</summary>
public record PontoEmbarqueRequest(
    Guid AlunoId,
    string? Matricula,
    string? Nome,
    double Latitude,
    double Longitude,
    string? Endereco);

/// <summary>Pedido de geração de rota para um veículo.</summary>
public record GerarRotaRequest(
    Guid VeiculoId,
    string? RotaTransporte,
    Guid? CursoId,
    double OrigemLatitude,
    double OrigemLongitude,
    double DestinoLatitude,
    double DestinoLongitude,
    string? DestinoNome);

/// <summary>
/// Pedido de geração de MÚLTIPLAS rotas para uma viagem (curso): a frota é
/// alocada automaticamente, dividindo os alunos confirmados por capacidade do
/// veículo e proximidade geográfica.
/// </summary>
public record GerarRotaViagemRequest(
    Guid CursoId,
    DateOnly? Data,
    double OrigemLatitude,
    double OrigemLongitude,
    double DestinoLatitude,
    double DestinoLongitude,
    string? DestinoNome);

/// <summary>Posição atual do cliente (van ou aluno) para validação/telemetria.</summary>
public record PosicaoRequest(double Latitude, double Longitude);

public record ParadaResponse(
    Guid Id,
    Guid AlunoId,
    string Matricula,
    string Nome,
    int Ordem,
    double Latitude,
    double Longitude,
    int ClusterId,
    bool Confirmada,
    DateTime? ConfirmadaEm);

public record RotaResponse(
    Guid Id,
    Guid VeiculoId,
    string? RotaTransporte,
    Guid? CursoId,
    DateOnly Data,
    double DistanciaTotalMetros,
    string Status,
    double DestinoLatitude,
    double DestinoLongitude,
    string? DestinoNome,
    DateTime CriadoEm,
    IReadOnlyList<ParadaResponse> Paradas);

public record ConfirmacaoResponse(
    bool Confirmada,
    double DistanciaMetros,
    double RaioMetros,
    string Mensagem);
