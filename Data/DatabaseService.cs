using System;
using System.Data.SQLite;
using System.IO;
using System.Collections.Generic;
using RealTimeVesselTracking.Models;
using Dapper;
using SQLitePCL;
using System.Security.Permissions;

namespace RealTimeVesselTracking.Data
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string dbPath)
        {
            _connectionString = $"Data Source={dbPath};Version=3;";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string CreateVesselsTable = @"
                CREATE TABLE IF NOT EXISTS Vessels (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MMSI TEXT UNIQUE NOT NULL,
                Name TEXT,
                Type TEXT);";

                string CreatePositionsTable = @"
                CREATE TABLE IF NOT EXISTS Positions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                VesselId INTEGER NOT NULL,
                Latitude DECIMAL(10,6),
                Longitude DECIMAL(10,6),
                Speed DECIMAL(10,2),
                Course DECIMAL(10,2),
                Temperature DECIMAL(5,2),
                WindSpeed DECIMAL(5,2),
                WaveSpeed DECIMAL(5,2),
                WaveHeight DECIMAL(5,2),
                TimeStamp DATETIME DEFAULT CURREN_TIMESTAMP,
                FOREIGN KEY (VesselId) REFERENCES Vessels(Id));";

                connection.Execute(CreateVesselsTable);
                connection.Execute(CreatePositionsTable);
            }
        }

        public void ViewVesselHistory()
        {
            Console.Write("Enter MMSI: ");
            string mmsi = Console.ReadLine();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string query = @"SELECT P.Timestamp, P.Latitude, P.Longitude, P.Speed, P.Temperature, P.WindSpeed, P.WaveHeight
                                FROM Positions P
                                JOIN Vessels V ON P.VesselId = V.Id
                                WHERE V.MMSI = @MMSI";
                using (var command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@MMSI", mmsi);
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.HasRows)
                        {
                            Console.WriteLine("No history found for this MMSI.");
                            return;
                        }
                        while (reader.Read())
                        {
                            Console.WriteLine($"Time: {reader["Timestamp"]}, Lat: {reader["Latitude"]}, Long: {reader["Longitude"]}, Speed: {reader["Speed"]} knots, Temp: {reader["Temperature"]}°C, Wind: {reader["WindSpeed"]} knots, Wave: {reader["WaveHeight"]}m");
                        }
                    }
                }
            }
        }

        public void InserVesselData(Vessel vessel)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string insertQuery = @"
                INSERT INTO Vessels (MMSI, Name, Type)
                VALUES (@MMSI, @Name, @Type)
                ON CONFLICT(MMSI) DO UPDATE SET Name = excluded.Name, Type=excluded.Type;";

                connection.Execute(insertQuery, vessel);
            }
        }

        public void InsertPositionData(Position position)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string insertQuery = @"
                INSERT INTO Positions (VesselId, Latitude, Longitude, Speed, Course, Temperature, WindSpeed, WaveHeight)
                VALUES (@VesselId, @Latitude, @Longitude, @Speed, @Course, @Temperature, @WindSpeed, @WaveHeight)";

                connection.Execute(insertQuery, position);
            }
        }

        public List<Position> GetVesselHistory(string mmsi)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string query = @"
                SELECT p.* FROM Positions p
                JOIN Vessels v ON p.VesselId = v.Id
                WHERE v.MMSI = @MMSI
                ORDER BY p.Timestamp DESC;";

                return connection.Query<Position>(query, new { MMSI = mmsi }).AsList();
            }
        }
    }
}