using TransportSystem.Graphs;
using TransportSystem.Nodes;

namespace TransportSystem.Services;

public class GraphBuilderService
{
    public Graph BuildGraph(List<Student> students)
    {
        var graph = new Graph();

        foreach (var student in students)
        {
            var node = new Node
            {
                Id = student.Id,
                Name = student.Name,
                Latitude = student.Latitude,
                Longitude = student.Longitude
            };

            graph.AddNode(node);
        }

        foreach (var a in graph.Nodes)
        {
            foreach (var b in graph.Nodes)
            {
                if (a == b) continue;

                graph.AddEdge(a, b, Distance(a, b));
            }
        }

        return graph;
    }

    private double Distance(Node a, Node b)
    {
        double dx = a.Latitude - b.Latitude;
        double dy = a.Longitude - b.Longitude;

        return Math.Sqrt(dx * dx + dy * dy);
    }
}