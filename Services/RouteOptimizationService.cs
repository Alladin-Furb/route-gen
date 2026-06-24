using RouteGen.Domain;
using RouteGen.Geo;

namespace RouteGen.Services;

/// <summary>
/// Resultado da otimização: ordem das paradas (clusters) e distância total.
/// </summary>
public class OptimizedRoute
{
    public List<Cluster> Stops { get; set; } = new();

    public double TotalDistanceMeters { get; set; }
}

/// <summary>
/// Gera a "melhor rota" possível com heurísticas leves:
/// vizinho mais próximo (nearest-neighbor) para uma solução inicial e
/// melhoria local 2-opt para reduzir a distância total. Todas as distâncias
/// usam Haversine (coordenadas GPS reais). A rota parte do veículo e termina
/// no destino (faculdade/curso).
/// </summary>
public class RouteOptimizationService
{
    /// <summary>
    /// Ordena as paradas minimizando a distância total do trajeto
    /// origem → paradas → destino.
    /// </summary>
    public OptimizedRoute Optimize(
        IReadOnlyList<Cluster> stops,
        double originLat, double originLon,
        double destLat, double destLon)
    {
        var order = NearestNeighbor(stops, originLat, originLon);
        TwoOpt(order, originLat, originLon, destLat, destLon);

        return new OptimizedRoute
        {
            Stops = order,
            TotalDistanceMeters = TotalDistance(order, originLat, originLon, destLat, destLon)
        };
    }

    private static List<Cluster> NearestNeighbor(
        IReadOnlyList<Cluster> stops, double originLat, double originLon)
    {
        var remaining = new List<Cluster>(stops);
        var order = new List<Cluster>(stops.Count);

        double curLat = originLat;
        double curLon = originLon;

        while (remaining.Count > 0)
        {
            int bestIdx = 0;
            double bestDist = double.MaxValue;

            for (int i = 0; i < remaining.Count; i++)
            {
                double d = GeoUtils.HaversineMeters(
                    curLat, curLon, remaining[i].Latitude, remaining[i].Longitude);

                if (d < bestDist)
                {
                    bestDist = d;
                    bestIdx = i;
                }
            }

            var next = remaining[bestIdx];
            remaining.RemoveAt(bestIdx);
            order.Add(next);

            curLat = next.Latitude;
            curLon = next.Longitude;
        }

        return order;
    }

    private static void TwoOpt(
        List<Cluster> order,
        double originLat, double originLon,
        double destLat, double destLon)
    {
        if (order.Count < 3)
            return;

        bool improved = true;
        int guard = 0;
        int maxIterations = order.Count * order.Count;

        while (improved && guard++ < maxIterations)
        {
            improved = false;

            for (int i = 0; i < order.Count - 1; i++)
            {
                for (int k = i + 1; k < order.Count; k++)
                {
                    if (TryReverseImproves(order, i, k, originLat, originLon, destLat, destLon))
                    {
                        order.Reverse(i, k - i + 1);
                        improved = true;
                    }
                }
            }
        }
    }

    private static bool TryReverseImproves(
        List<Cluster> order, int i, int k,
        double originLat, double originLon,
        double destLat, double destLon)
    {
        (double aLat, double aLon) = i == 0
            ? (originLat, originLon)
            : (order[i - 1].Latitude, order[i - 1].Longitude);

        (double bLat, double bLon) = (order[i].Latitude, order[i].Longitude);
        (double cLat, double cLon) = (order[k].Latitude, order[k].Longitude);

        (double dLat, double dLon) = k == order.Count - 1
            ? (destLat, destLon)
            : (order[k + 1].Latitude, order[k + 1].Longitude);

        double before = GeoUtils.HaversineMeters(aLat, aLon, bLat, bLon)
                        + GeoUtils.HaversineMeters(cLat, cLon, dLat, dLon);

        double after = GeoUtils.HaversineMeters(aLat, aLon, cLat, cLon)
                       + GeoUtils.HaversineMeters(bLat, bLon, dLat, dLon);

        return after + 1e-6 < before;
    }

    private static double TotalDistance(
        IReadOnlyList<Cluster> order,
        double originLat, double originLon,
        double destLat, double destLon)
    {
        if (order.Count == 0)
            return GeoUtils.HaversineMeters(originLat, originLon, destLat, destLon);

        double total = GeoUtils.HaversineMeters(
            originLat, originLon, order[0].Latitude, order[0].Longitude);

        for (int i = 0; i < order.Count - 1; i++)
        {
            total += GeoUtils.HaversineMeters(
                order[i].Latitude, order[i].Longitude,
                order[i + 1].Latitude, order[i + 1].Longitude);
        }

        total += GeoUtils.HaversineMeters(
            order[^1].Latitude, order[^1].Longitude, destLat, destLon);

        return total;
    }
}
