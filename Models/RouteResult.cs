using TransportSystem.Nodes;

namespace TransportSystem.RouteResults;

public class RouteResult
{
    public List<Node> Route { get; set; } = new();

    public double TotalDistance { get; set; }
}