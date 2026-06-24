namespace RouteGen.Notifications;

public class NotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger) => _logger = logger;

    public Task NotifyDriver(string message)
    {
        _logger.LogInformation("NOTIFICAÇÃO MOTORISTA: {Message}", message);
        return Task.CompletedTask;
    }
}
