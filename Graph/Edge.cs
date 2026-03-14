using TransportSystem.Nodes;

namespace TransportSystem.Edges;

public class Edge
{
    public Node From { get; set; } = null!;

    public Node To { get; set; } = null!;

    public double Weight { get; set; }
}