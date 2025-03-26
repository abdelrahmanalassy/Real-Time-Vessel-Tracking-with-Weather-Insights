using System;
using System.Data.SQLite;
using System.IO;
using System.Collections.Generic;
using RealTimeVesselTracking.Models;
using RealTimeVesselTracking.Services;
using Dapper;
using SQLitePCL;
using System.Security.Permissions;
using System.Data;

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
                Name TEXT);";

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
                string query = @"SELECT P.Timestamp, P.Latitude, P.Longitude, P.Speed, P.Temperature, P.WindSpeed
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
                            Console.WriteLine($"Time: {reader["Timestamp"]}, Lat: {reader["Latitude"]}, Long: {reader["Longitude"]}, Speed: {reader["Speed"]} knots, Temp: {reader["Temperature"]}°C, Wind: {reader["WindSpeed"]} knots");
                        }
                    }
                }
            }
        }

        public void InsertVesselData(Vessel vessel)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string insertQuery = @"
                INSERT INTO Vessels (MMSI, Name)
                VALUES (@MMSI, @Name)
                ON CONFLICT(MMSI) DO UPDATE SET Name = excluded.Name;";

                connection.Execute(insertQuery, vessel);
            }
        }

        public void InsertPositionData(Position position, string shipName)
        {

            Console.WriteLine($"Trying to insert data for MMSI {position.VesselId}");

            using (var connection = new SQLiteConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    Console.WriteLine("Database Connection Opened Successfully!");

                    using (var transaction = connection.BeginTransaction())
                    {
                        string checkVesselQuery = "SELECT COUNT(*) FROM Vessels WHERE MMSI = @MMSI";
                        int vesselCount = connection.ExecuteScalar<int>(checkVesselQuery, new { MMSI = position.VesselId });

                        if (vesselCount == 0)
                        {
                            string finalShipName = string.IsNullOrWhiteSpace(shipName) ? "Unknown" : shipName.Trim();

                            Console.WriteLine($"Vessel with MMSI {position.VesselId} not found. Adding it to Vessels with Name: {finalShipName}");

                            string insertVesselQuery = "INSERT INTO Vessels (MMSI, Name) VALUES (@MMSI, @Name)";
                            connection.Execute(insertVesselQuery, new { MMSI = position.VesselId, Name = finalShipName }, transaction);
                            Console.WriteLine("Vessel Inserted Successfully!");

                        }

                        // To Validate Data Before Insert it
                        if (position.VesselId == 0 || (position.Latitude == 0.0m || position.Longitude == 0.0m))
                        {
                            Console.WriteLine("Error: Missing Required Data. Skipping Insertion.");
                            return;
                        }

                        Console.WriteLine($"Insert Data: MMSI={position.VesselId}, Lat={position.Latitude}, Lon={position.Longitude}, Speed={position.Speed}, Course={position.Course}, Temp={position.Temperature}, Wind={position.WindSpeed}");

                        // Insert Data
                        string insertQuery = @"
                        INSERT INTO Positions (VesselId, Latitude, Longitude, Speed, Course, Temperature, WindSpeed, Timestamp)
                        VALUES ((SELECT Id FROM Vessels WHERE MMSI = @MMSI), @Latitude, @Longitude, @Speed, @Course, @Temperature, @WindSpeed, @Timestamp)";

                        Console.WriteLine($"Executing SQL Query: {insertQuery}");

                        using (var command = new SQLiteCommand(insertQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@MMSI", position.VesselId);
                            command.Parameters.AddWithValue("@Latitude", position.Latitude);
                            command.Parameters.AddWithValue("@Longitude", position.Longitude);
                            command.Parameters.AddWithValue("@Speed", position.Speed);
                            command.Parameters.AddWithValue("@Course", position.Course);
                            command.Parameters.AddWithValue("@Temperature", position.Temperature);
                            command.Parameters.AddWithValue("@WindSpeed", position.WindSpeed);
                            command.Parameters.AddWithValue("@Timestamp", position.Timestamp);

                            Console.WriteLine("Final Data Before Insert:");
                            Console.WriteLine($"MMSI: {position.VesselId}");
                            Console.WriteLine($"Latitude: {position.Latitude}, Longitude: {position.Longitude}");
                            Console.WriteLine($"Speed: {position.Speed}, Course: {position.Course}");
                            Console.WriteLine($"Temperature: {position.Temperature}, WindSpeed: {position.WindSpeed}");

                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        Console.WriteLine("Data Inserted Successfully!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Database Insert Error: {ex.Message}");
                }
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