namespace RouteGen.Configuration;

public class RoutingOptions
{
    public const string SectionName = "Routing";

    /// <summary>Raio (m) para agrupar pontos de embarque próximos em um cluster.</summary>
    public double ClusterRadiusMeters { get; set; } = 300;

    /// <summary>Raio (m) máximo entre o cliente e a parada para confirmar embarque.</summary>
    public double ConfirmationRadiusMeters { get; set; } = 100;
}

public class ServicesOptions
{
    public const string SectionName = "Services";

    /// <summary>URL base do serviço de cadastro (register) — via gateway ou direta.</summary>
    public string? RegisterBaseUrl { get; set; }

    /// <summary>URL base do serviço de presença (attendance) — via gateway ou direta.</summary>
    public string? AttendanceBaseUrl { get; set; }
}
