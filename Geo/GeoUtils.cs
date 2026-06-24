namespace RouteGen.Geo;

/// <summary>
/// Utilitários geográficos para cálculo de distância entre coordenadas GPS.
/// </summary>
public static class GeoUtils
{
    private const double EarthRadiusMeters = 6_371_000d;

    /// <summary>
    /// Distância em metros entre dois pontos (lat/long em graus) usando a fórmula de Haversine.
    /// </summary>
    public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                   + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
                     * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
}
