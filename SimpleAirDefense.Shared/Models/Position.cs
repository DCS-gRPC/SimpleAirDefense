using System;

namespace RurouniJones.SimpleAirDefense.Shared.Models
{
    public class Position
    {
        public double Latitude { get; }
        public double Longitude { get; }

        public Position(double latitude, double longitude)
        {
            Latitude = Math.Min(Math.Max(latitude, -90.0), 90.0);
            Longitude = longitude;
        }
    }
}
