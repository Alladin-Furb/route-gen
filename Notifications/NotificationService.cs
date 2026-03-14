namespace TransportSystem.Notifications;

public class NotificationService
{
    public Task NotifyDriver(string message)
    {
        Console.WriteLine($"NOTIFICAÇÃO MOTORISTA: {message}");

        return Task.CompletedTask;
    }
}