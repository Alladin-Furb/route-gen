namespace RouteGen.Data.Entities;

/// <summary>Estado de uma mensagem de propagação de presença no outbox.</summary>
public enum OutboxStatus
{
    Pendente = 0,
    Enviado = 1,
    Falhou = 2
}

/// <summary>
/// Outbox durável para propagação de presença ao attendance. A intenção de
/// propagar é persistida na MESMA transação da confirmação de embarque, e um
/// worker em background entrega ao attendance com retentativa/backoff. Garante
/// que uma confirmação não se perca se o route-gen reiniciar ou o attendance
/// estiver temporariamente indisponível.
/// </summary>
public class PresencaOutbox
{
    public Guid Id { get; set; }

    public Guid AlunoId { get; set; }

    public Guid CursoId { get; set; }

    public string Role { get; set; } = "";

    public Guid ProfileId { get; set; }

    public OutboxStatus Status { get; set; } = OutboxStatus.Pendente;

    public int Tentativas { get; set; }

    public DateTime ProximaTentativaEm { get; set; } = DateTime.UtcNow;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public DateTime? EnviadoEm { get; set; }

    public string? UltimoErro { get; set; }
}
