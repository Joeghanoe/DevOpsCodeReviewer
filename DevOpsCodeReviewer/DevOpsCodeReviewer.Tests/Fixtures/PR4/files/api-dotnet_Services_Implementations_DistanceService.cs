using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Api.Services.Implementations;

public class DistanceService : IDistanceService
{
    private const double EarthRadiusKm = 6371.0;

    public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    public async Task<List<AddressWithDistance>> GetAddressesSortedByDistance(
        string userId,
        double refLat,
        double refLon)
    {
        // TODO: Implement database lookup
        var addresses = await GetUserAddresses(userId);

        return addresses
            .Select(a => new AddressWithDistance
            {
                Address = a,
                DistanceKm = CalculateDistance(refLat, refLon, a.Latitude, a.Longitude)
            })
            .OrderBy(x => x.DistanceKm)
            .ToList();
    }

    private static double ToRadians(double degrees) => degrees * (Math.PI / 180);

    private Task<List<Address>> GetUserAddresses(string userId)
    {
        // Placeholder - would fetch from database
        throw new NotImplementedException();
    }
}

public class AddressWithDistance
{
    public Address Address { get; set; }
    public double DistanceKm { get; set; }
}

public class Address
{
    public string Id { get; set; }
    public string Street { get; set; }
    public string City { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
