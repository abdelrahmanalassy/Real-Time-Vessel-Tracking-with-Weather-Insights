using System;

namespace RealTimeVesselTracking.Models
{
    public class Vessel
    {
        public int Id { get; set;}
        public string MMSI { get; set;}
        public string Name { get; set;}
        public string Type { get; set;}
    }
}