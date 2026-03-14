
using TransportSystem.Graphs;
using TransportSystem.Nodes;

namespace TransportSystem.Services;

public class RouteOptimizationService
{
    public List<Node> GenerateRoute(Graph graph, Node start)
    {
        var route = new List<Node>();
        var visited = new HashSet<Node>();

        var current = start;

        route.Add(current);
        visited.Add(current);

        while (visited.Count < graph.Nodes.Count)
        {
            var nextEdge = graph.AdjacencyList[current]
                .Where(e => !visited.Contains(e.To))
                .OrderBy(e => e.Weight)
                .FirstOrDefault();

            if (nextEdge == null)
                break;

            current = nextEdge.To;

            route.Add(current);
            visited.Add(current);
        }

        return route;
    }

    public double CalculateDistance(List<Node> route)
    {
        double total = 0;

        for (int i = 0; i < route.Count - 1; i++)
        {
            total += Distance(route[i], route[i + 1]);
        }

        return total;
    }

    private double Distance(Node a, Node b)
    {
        double dx = a.Latitude - b.Latitude;
        double dy = a.Longitude - b.Longitude;

        return Math.Sqrt(dx * dx + dy * dy);
    }
}