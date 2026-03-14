using TransportSystem.Edges;
using TransportSystem.Nodes;

namespace TransportSystem.Graphs;

public class Graph
{
    public List<Node> Nodes { get; set; } = new();

    public Dictionary<Node, List<Edge>> AdjacencyList { get; set; } = new();

    public void AddNode(Node node)
    {
        Nodes.Add(node);
        AdjacencyList[node] = new List<Edge>();
    }

    public void AddEdge(Node from, Node to, double weight)
    {
        AdjacencyList[from].Add(new Edge
        {
            From = from,
            To = to,
            Weight = weight
        });
    }
}