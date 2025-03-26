// This cs to Fetchs Weather Data from PenWeather API
using System;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RealTimeVesselTracking.Utilities;

namespace RealTimeVesselTracking.Services
{
    public class WeatherService
    {
        private static readonly string ApiKey = ConfigHelper.GetConfigValue("OpenWeather:ApiKey");
        private static readonly HttpClient HttpClient = new HttpClient();
        public static async Task<(decimal Temperature, decimal WindSpeed)> GetWeatherDataAsync(decimal latitude, decimal longitude)
        {
            try
            {
                string url = $"https://api.openweathermap.org/data/2.5/weather?lat={latitude}&lon={longitude}&appid={ApiKey}&units=metric";
                HttpResponseMessage response = await HttpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();
                JObject weatherData = JObject.Parse(responseBody);

                decimal temperature = weatherData["main"]?["temp"]?.Value<decimal>() ?? 0;
                decimal windSpeed = weatherData["wind"]?["speed"]?.Value<decimal>() ?? 0;

                Console.WriteLine($"Weather API Response: {responseBody}");

                return (temperature, windSpeed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching weather data: {ex.Message}");
                return (0, 0);
            }
        }

        public decimal GetTemperature(decimal latitude, decimal longitude)
        {
            var weatherData = GetWeatherDataAsync(latitude, longitude). Result;
            return weatherData.Temperature;
        }

        public decimal GetWindSpeed(decimal latitude, decimal longitude)
        {
            var weatherData = GetWeatherDataAsync(latitude, longitude). Result;
            return weatherData.WindSpeed;
        }
    }
}