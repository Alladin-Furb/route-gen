namespace RouteGen.Auth;

/// <summary>
/// Papéis propagados pelo API Gateway via header <c>X-User-Role</c>.
/// </summary>
public static class Roles
{
    public const string Admin = "ROLE_ADMIN";
    public const string Motorista = "ROLE_MOTORISTA";
    public const string Aluno = "ROLE_ALUNO";
}

/// <summary>
/// Contexto do usuário autenticado, derivado dos headers que o gateway injeta
/// após validar o JWT (<c>X-User-Role</c> e <c>X-Profile-Id</c>). O serviço
/// confia nesses headers e não revalida o token (mesmo padrão dos demais
/// microsserviços downstream).
/// </summary>
public sealed class GatewayUser
{
    public const string RoleHeader = "X-User-Role";
    public const string ProfileIdHeader = "X-Profile-Id";

    public string? Role { get; init; }

    public long? ProfileId { get; init; }

    public bool IsAdmin => Role == Roles.Admin;

    public bool IsMotorista => Role == Roles.Motorista;

    public bool IsAluno => Role == Roles.Aluno;

    public static GatewayUser FromHeaders(IHeaderDictionary headers)
    {
        long? profileId = null;
        if (long.TryParse(headers[ProfileIdHeader].FirstOrDefault(), out var pid))
            profileId = pid;

        return new GatewayUser
        {
            Role = headers[RoleHeader].FirstOrDefault(),
            ProfileId = profileId
        };
    }
}
