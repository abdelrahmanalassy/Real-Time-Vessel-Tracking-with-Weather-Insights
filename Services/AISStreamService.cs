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
                try
                {
                    Console.WriteLine("Connecting to AIS Data Stream...");
                    await client.ConnectAsync(new Uri(AIS_STREAM_URL), CancellationToken.None);
                    Console.WriteLine("Connected Successfully!");

                    // To Send a Authenticaton Message
                    var authData = new JObject
                    {
                        { "ApiKey", _aisAPIKey },
                        { "boundingBoxes", new JArray(new JArray(-90, -100), new JArray(90, 100))}
                    };

                    var authMessage = authData.ToString(Formatting.None);
                    Console.WriteLine($"Sending Authentication Message: {authMessage}");
                    await client.SendAsync(Encoding.UTF8.GetBytes(authMessage), WebSocketMessageType.Text, true, CancellationToken.None);

                    byte[] buffer = new byte[4096];

                    while (client.State == WebSocketState.Open)
                    {
                        try
                        {
                            var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                            }
                            else
                            {
                                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                                ProcessAISMessage(message);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Receive Error: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        public async Task TrackVesselsForThreeMinutes()
        {
            Console.WriteLine("Tracking Veseels For 3 Minutes...");
            var endTime = DateTime.UtcNow.AddMinutes(3);

            while (DateTime.UtcNow < endTime)
            {
                await StartListeningAsync();
                await Task.Delay(5000);
            }

            Console.WriteLine("Tracking Complete.");
        }

        private void ProcessAISMessage(string message)
        {
            try
            {
                var json = JObject.Parse(message);

                if (json["type"]?.ToString() == "dynamic")
                {
                    var mmsi = json["mmsi"]?.ToString();
                    var latitude = json["lat"]?.ToObject<double>();
                    var longitude = json["lon"]?.ToObject<double>();
                    var speed = json["speed"]?.ToObject<double>();
                    var course = json["course"]?.ToObject<double>();

                    if (mmsi != null && latitude.HasValue && longitude.HasValue)
                    {
                        var vessel = new Vessel
                        {
                            MMSI = mmsi,
                            Name = "Unkown",
                            Type = "Unkown"

                        };
                        _databaseService.InserVesselData(vessel);

                        var position = new Position
                        {
                            VesselId = GetVesselId(mmsi),
                            Latitude = latitude.HasValue ? (decimal)latitude.Value : 0m,
                            Longitude = longitude.HasValue ? (decimal)longitude.Value : 0m,
                            Speed = (decimal)(speed ?? 0.0),
                            Course = (decimal)(course ?? 0.0),
                            Temperature = 0m,
                            WindSpeed = 0m,
                            WaveHeight = 0m
                        };

                        _databaseService.InsertPositionData(position);
                        Console.WriteLine($"Vessel {mmsi}: Lat={latitude}, Lon={longitude}, Speed={speed} Knots, Course={course}");
                    }
                }
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