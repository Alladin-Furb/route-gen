using RouteGen.Domain;
using RouteGen.Geo;

namespace RouteGen.Services;

/// <summary>
/// Agrupa pontos de embarque próximos dentro de um raio configurável.
/// Usa um agrupamento incremental simples baseado em distância Haversine:
/// cada aluno entra no primeiro cluster cujo centroide esteja dentro do raio,
/// caso contrário inicia um novo cluster.
/// </summary>
public class ClusteringService
{
    public List<Cluster> Cluster(IEnumerable<Student> students, double radiusMeters)
    {
        var clusters = new List<Cluster>();
        int nextId = 0;

        foreach (var student in students)
        {
            Cluster? target = null;
            double best = double.MaxValue;

            foreach (var cluster in clusters)
            {
                double d = GeoUtils.HaversineMeters(
                    student.Latitude, student.Longitude,
                    cluster.Latitude, cluster.Longitude);

                if (d <= radiusMeters && d < best)
                {
                    best = d;
                    target = cluster;
                }
            }

            if (target is null)
            {
                target = new Cluster
                {
                    Id = nextId++,
                    Latitude = student.Latitude,
                    Longitude = student.Longitude
                };
                clusters.Add(target);
            }

            target.Members.Add(student);
            RecomputeCentroid(target);
        }

        return clusters;
    }

    private static void RecomputeCentroid(Cluster cluster)
    {
        cluster.Latitude = cluster.Members.Average(m => m.Latitude);
        cluster.Longitude = cluster.Members.Average(m => m.Longitude);
    }
}
