using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RealTimeVesselTracking.Utilities
{
    public static class ConfigHelper
    {
        private static readonly string ConfigFilePath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        public static string GetConfigValue(string Key)
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    
                    Console.WriteLine("Configuration file not found.");
                    return string.Empty;
                }

                string json = File.ReadAllText(ConfigFilePath);
                JObject config = JObject.Parse(json);

                string[] path = Key.Split(':');
                JToken token = config;
                foreach (var segment in path)
                {
                    token = token?[segment];
                    if (token == null)
                    {
                        Console.WriteLine($"Key '{Key}' not found in configuration.");
                        return string.Empty;
                    }
                }

                string value = token.ToString();
                //Console.WriteLine($"Retrieved value for '{Key}': {value}");
                return value;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading configuration: {ex.Message}");
                return string.Empty; 
            }
        }
    }
}