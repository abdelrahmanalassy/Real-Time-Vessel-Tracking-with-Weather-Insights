using System;

namespace RealTimeVesselTracking.Models
{
    public class Position
    {
        public int  Id { get; set; }
        public int VesselId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public decimal Speed { get; set; }
        public decimal Course { get; set; }
        public decimal Temperature { get; set; }
        public decimal WindSpeed { get; set; }
        public decimal WaveHeight { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}