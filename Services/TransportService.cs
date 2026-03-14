using TransportSystem.Notifications;
using TransportSystem.RouteResults;

namespace TransportSystem.Services;

public class TransportService
{
    private readonly GraphBuilderService _graphBuilder = new();
    private readonly RouteOptimizationService _routeService = new();
    private readonly NotificationService _notification = new();

    private RouteResult? _currentRoute;

    public RouteResult GenerateRoute(Vehicle vehicle, List<Student> students)
    {
        if (students.Count > vehicle.Capacity)
            throw new Exception("Capacidade do veículo excedida");

        var graph = _graphBuilder.BuildGraph(students);

        var route = _routeService.GenerateRoute(graph, graph.Nodes.First());

        var distance = _routeService.CalculateDistance(route);

        _currentRoute = new RouteResult
        {
            Route = route,
            TotalDistance = distance
        };

        return _currentRoute;
    }

    public async Task CancelStudent(int studentId)
    {
        if (_currentRoute == null)
            return;

        var student = _currentRoute.Route.FirstOrDefault(s => s.Id == studentId);

        if (student != null)
        {
            _currentRoute.Route.Remove(student);

            await _notification.NotifyDriver(
                $"Aluno {student.Name} cancelou a viagem");
        }
    }
}