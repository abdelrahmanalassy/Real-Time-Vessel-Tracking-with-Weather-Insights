using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RealTimeVesselTracking.Models;
using RealTimeVesselTracking.Data;
using System.Security.Permissions;
using CsvHelper.Configuration.Attributes;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Security;

namespace RealTimeVesselTracking.Services
{
    public class AISStreamService
    {
        private readonly DatabaseService _databaseService;
        private const string AIS_STREAM_URL = "wss://stream.aisstream.io/v0/stream";
        private readonly string _aisAPIKey;

        public AISStreamService(DatabaseService databaseService, string aisAPIKey)
        {
            _databaseService = databaseService;
            _aisAPIKey = aisAPIKey;
        }

        public async Task StartListeningAsync()
        {
            Console.WriteLine("Starting live AIS stream...");
            await ReceiveAndProcessAISData(TimeSpan.MaxValue);
        }

        public async Task TrackVesselsForThreeMinutes()
        {
            Console.WriteLine("Starting vessel tracking for 3 minutes.....");
            await ReceiveAndProcessAISData(TimeSpan.FromMinutes(3));
            Console.WriteLine("Tracking Complete.");
        }

        private async Task ReceiveAndProcessAISData(TimeSpan duration)
        {
            using (var client = new ClientWebSocket())
            {
                await client.ConnectAsync(new Uri(AIS_STREAM_URL), CancellationToken.None);
                Console.WriteLine("WebSocket Connected!");

                string request = $"{{ \"APIKey\": \"{_aisAPIKey}\", \"BoundingBoxes\": [[[-11.0, 178.0], [30.0, 74.0]]], \"FilterMessageTypes\": [\"PositionReport\"] }}";
                await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(request)), WebSocketMessageType.Text, true, CancellationToken.None);

                var buffer = new byte[8192];
                var stopwatch = Stopwatch.StartNew();

                while (stopwatch.Elapsed < duration)
                {
                    var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"Received Message: {message}");

                    if (message.Contains("PositionReport"))
                    {
                        Console.WriteLine("Position report detected, processing...");
                        await ProcessAISMessage(message);
                    }
                }

                Console.WriteLine("Stopping AIS stream.");
            }
        }
        private async Task ProcessAISMessage(string message)
        {
            Console.WriteLine("Processing AIS Message...");
            try
            {
                Console.WriteLine("Received AIS Messsage: " + message);
                var json = JObject.Parse(message);
                var metaData = json["MetaData"];
                var positionReport = json["Message"]?["PositionReport"];
                // var shipStaticData = json["Message"]?["ShipStaticData"];

                if (metaData == null || positionReport == null)
                {
                    Console.WriteLine("Warning: Message format invalid. Skipping...");
                    return;
                }

                long mmsi = metaData["MMSI"]?.Value<long>() ?? 0;
                double latitude = metaData["latitude"]?.Value<double>() ?? 0.0;
                double longitude = metaData["longitude"]?.Value<double>() ?? 0.0;
                double speed = positionReport["Sog"]?.Value<double>() ?? 0.0;
                double course = positionReport["Cog"]?.Value<double>() ?? 0.0;
                string timestamp = metaData["time_utc"]?.Value<string>() ?? DateTime.UtcNow.ToString("o");
                string shipName = metaData["ShipName"]?.Value<string>() ?? "Unknown";

                if (mmsi == 0 || latitude == 0.0 || longitude == 0.0)
                {
                    Console.WriteLine("ERROR: Invalid MMSI or missing latitude/longitude. Skipping insertion.");
                    return;
                }

                Console.WriteLine($"Vessel: {shipName}, MMSI: {mmsi}");
                Console.WriteLine($"Latitude: {latitude}, Longitude: {longitude}, Speed: {speed} Knots");
                Console.WriteLine("Fetching weather data......");

                // Fetch Weather Data
                WeatherService weatherService = new WeatherService();

                decimal temperature = weatherService.GetTemperature((decimal)latitude, (decimal)longitude);
                decimal windSpeed = weatherService.GetWindSpeed((decimal)latitude, (decimal)longitude);

                Console.WriteLine($"Temperature: {temperature}°C, WindSpeed: {windSpeed} Knots");
                Console.WriteLine("Position and Weather Stored in database.\n");

                //await _databaseService.InsertPositionData(mmsi, shipName, latitude, longitude, speed, course, temperature, windSpeed, timestamp);

                Console.WriteLine($"Extracted Data - MMSI: {mmsi}, Vessel Name: {shipName}, Latitude: {latitude}, Longitude: {longitude}, Speed: {speed}, Course: {course}, Temperature: {temperature}, Wind Speed: {windSpeed}");

                _databaseService.InsertPositionData(new Position
                {
                    VesselId = (int)mmsi,
                    Latitude = (decimal)latitude,
                    Longitude = (decimal)longitude,
                    Speed = (decimal)speed,
                    Course = (decimal)course,
                    Temperature = (decimal)temperature,
                    WindSpeed = (decimal)windSpeed,
                    Timestamp = DateTime.TryParse(timestamp, out DateTime parsedTimestamp) ? parsedTimestamp : DateTime.UtcNow
                }, shipName);

                Console.WriteLine($"Sent Position Data to DatabaseService for MMSI: {mmsi}");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing AIS message: {ex.Message}");
            }
        }

        // To Fetch the vessel ID From The Database using MMSI
        private int GetVesselId(string mmsi)
        {
            var vesselHistory = _databaseService.GetVesselHistory(mmsi);
            return vesselHistory.Count > 0 ? vesselHistory[0].VesselId : 0;
        }
    }
}