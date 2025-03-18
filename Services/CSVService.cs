using System;
using System.Data.SQLite;
using System.IO;
using System.Text;

namespace RealTimeVesselTracking.Services
{
    public static class CSVService
    {
        private const string ConnectionString = "Data Source=vessel_tracking.db;Version=3;";

        public static void ExportToCSV()
        {
            Console.Write("Enter File Name (Without Extenstion): ");
            String fileName = Console.ReadLine();
            String filePath = $"{fileName}.csv";

            using (var connection = new SQLiteConnection(ConnectionString))
            {
                connection.Open();
                string query = @"SELECT V.MMSI, V.Name, P.Timestamp, P.Latitude, P.Longitude, P.Speed, P.Temperature, P.WindSpeed, P.WaveHeight
                                FROM Positions P
                                JOIN Vessels V ON P.VesselId = v.Id";
                
                using (var command = new SQLiteCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.HasRows)
                    {
                        Console.WriteLine("No Data Available To Export.");
                        return;
                    }

                    StringBuilder csvContent = new StringBuilder();
                    csvContent.AppendLine("MMSI,Name,TimeStamp,Latitude,Longitude,Speed,Temperature,WindSpeed,WaveHeight");

                    while (reader.Read())
                    {
                        csvContent.AppendLine($"{reader["MMSI"]},{reader["Name"]},{reader["TimeStamp"]},{reader["Latitude"]},{reader["Longitude"]},{reader["Speed"]},{reader["Temperature"]},{reader["WindSpeed"]},{reader["WaveHeight"]}");
                    }

                    File.WriteAllText(filePath, csvContent.ToString());
                    Console.WriteLine($"Data Exported Successfully to {filePath}");
                }
            }
        }
    }
}