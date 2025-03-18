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
            using (var client = new ClientWebSocket())
            {
                CancellationTokenSource source = new CancellationTokenSource();
                CancellationToken token = source.Token;
                using (var ws = new ClientWebSocket())
                {
                    {
                        await ws.ConnectAsync(new Uri("wss://stream.aisstream.io/v0/stream"), token);
                        await ws.SendAsync(
                            new ArraySegment<byte>(
                                Encoding.UTF8.GetBytes(
                                    $"{{ \"APIKey\": \"{_aisAPIKey}\", \"BoundingBoxes\": [[[-11.0, 178.0], [30.0, 74.0]]]}}")
                                ), WebSocketMessageType.Text,
                                true, token
                        );
                        byte[] buffer = new byte[4096];
                        while (ws.State == WebSocketState.Open)
                        {
                            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, token);
                            }
                            else
                            {
                                Console.WriteLine($"Received {Encoding.Default.GetString(buffer, 0, result.Count)}");
                            }
                        }
                    }
                }
            }
        }

        public async Task TrackVesselsForThreeMinutes()
        {
            Console.WriteLine("Starting vessel tracking for 3 minutes.....");
            using (var client = new ClientWebSocket())
            {
                await client.ConnectAsync(new Uri("wss://stream.aisstream.io/v0/stream"), CancellationToken.None);
                Console.WriteLine("WebSocket Connected!");

                await client.SendAsync(
                            new ArraySegment<byte>(
                                Encoding.UTF8.GetBytes(
                                    $"{{ \"APIKey\": \"{_aisAPIKey}\", \"BoundingBoxes\": [[[-11.0, 178.0], [30.0, 74.0]]]}}")
                                ), WebSocketMessageType.Text,
                                true, CancellationToken.None
                            );
                var buffer = new byte[8192];
                var stopwatch = Stopwatch.StartNew();

                while (stopwatch.Elapsed < TimeSpan.FromMinutes(3))
                {
                    var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"Received Message: {message}");

                    if (message.Contains("PositionReport"))
                    {
                        Console.WriteLine("Position report detected, processing...");
                        await ProcessAISMessage(message);
                    }
                }

                Console.WriteLine("Stopping vessel tracking after 3 minutes.");
            }


            var endTime = DateTime.UtcNow.AddMinutes(3);

            while (DateTime.UtcNow < endTime)
            {
                await StartListeningAsync();
                await Task.Delay(5000);
            }

            Console.WriteLine("Tracking Complete.");
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
                var shipStaticData = json["Message"]?["ShipStaticData"];

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
                string shipType = shipStaticData["Type"]?.ToString() ?? "Unknown";

                if (mmsi == 0 || latitude == 0.0 || longitude == 0.0)
                {
                    Console.WriteLine("ERROR: Invalid MMSI or missing latitude/longitude. Skipping insertion.");
                    return;
                }

                Console.WriteLine($"Vessel: {shipName}, Type: {shipType}, MMSI: {mmsi}");
                Console.WriteLine($"Latitude: {latitude}, Longitude: {longitude}, Speed: {speed} Knots");
                Console.WriteLine("Fetching weather data......");

                // Fetch Weather Data
                WeatherService weatherService = new WeatherService();

                decimal temperature = weatherService.GetTemperature((decimal)latitude, (decimal)longitude);
                decimal windSpeed = weatherService.GetWindSpeed((decimal)latitude, (decimal)longitude);
                decimal waveHeight = weatherService.GetWaveHeight((decimal)latitude, (decimal)longitude);

                Console.WriteLine($"Temperature: {temperature}°C, WindSpeed: {windSpeed} Knots, WaveHeight: {waveHeight} meters");
                Console.WriteLine("Position and Weather Stored in database.\n");

                //await _databaseService.InsertPositionData(mmsi, shipName, shipType, latitude, longitude, speed, course, temperature, windSpeed, waveHeight, timestamp);

                Console.WriteLine($"Extracted Data - MMSI: {mmsi}, Vessel Name: {shipName}, Vessel Type: {shipType}, Latitude: {latitude}, Longitude: {longitude}, Speed: {speed}, Course: {course}, Temperature: {temperature}, Wind Speed: {windSpeed}, Wave Height: {waveHeight}");

                _databaseService.InsertPositionData(new Position
                {
                    VesselId = (int)mmsi,
                    Latitude = (decimal)latitude,
                    Longitude = (decimal)longitude,
                    Speed = (decimal)speed,
                    Course = (decimal)course,
                    Temperature = (decimal)temperature,
                    WindSpeed = (decimal)windSpeed,
                    WaveHeight = (decimal)waveHeight,
                    Timestamp = DateTime.TryParse(timestamp, out DateTime parsedTimestamp) ? parsedTimestamp : DateTime.UtcNow
                }, shipName, shipType);

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