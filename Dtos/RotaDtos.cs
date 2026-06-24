namespace RouteGen.Dtos;

/// <summary>Cadastro/atualização do ponto de embarque de um aluno.</summary>
public record PontoEmbarqueRequest(
    long AlunoId,
    string? Matricula,
    string? Nome,
    double Latitude,
    double Longitude,
    string? Endereco);

/// <summary>Pedido de geração de rota para um veículo.</summary>
public record GerarRotaRequest(
    long VeiculoId,
    string? RotaTransporte,
    long? CursoId,
    double OrigemLatitude,
    double OrigemLongitude,
    double DestinoLatitude,
    double DestinoLongitude,
    string? DestinoNome);

/// <summary>Posição atual do cliente (van ou aluno) para validação/telemetria.</summary>
public record PosicaoRequest(double Latitude, double Longitude);

public record ParadaResponse(
    int Id,
    long AlunoId,
    string Matricula,
    string Nome,
    int Ordem,
    double Latitude,
    double Longitude,
    int ClusterId,
    bool Confirmada,
    DateTime? ConfirmadaEm);

public record RotaResponse(
    int Id,
    long VeiculoId,
    string? RotaTransporte,
    long? CursoId,
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
