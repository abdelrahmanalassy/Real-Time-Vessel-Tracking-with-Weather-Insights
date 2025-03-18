using System;
using System.Configuration;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RealTimeVesselTracking.Data;
using RealTimeVesselTracking.Services;
using RealTimeVesselTracking.Utilities;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Real-Time Vessel Traking with Weather Insights\n");

        var databaseService = new DatabaseService("vessel_tracking.db");
        var aisApiKey = ConfigHelper.GetConfigValue("AISStream:AISAPIKey");
        var aisService = new AISStreamService(databaseService, aisApiKey);

        string _aisAPIKey = ConfigHelper.GetConfigValue("AISStream:AISAPIKey");
        //Console.WriteLine($"Retrieved API Key: {_aisAPIKey}");
    
        while (true)
        {
            Console.WriteLine("[1] Start Tracking Live Vessels for 3 min");
            Console.WriteLine("[2] View Vessel History");
            Console.WriteLine("[3] Expert Data to CSV");
            Console.WriteLine("[4] Exit");
            Console.Write("Enter Your Choice: ");

            string Choice = Console.ReadLine();
            switch (Choice)
            {
                case "1":
                    await aisService.TrackVesselsForThreeMinutes();
                    break;
                case "2":
                    databaseService.ViewVesselHistory();
                    break;
                case "3":
                    CSVService.ExportToCSV();
                    break;
                case "4":
                    Console.WriteLine("Exiting...");
                    return;
                default:
                    Console.WriteLine("Invalid Choice. Please enter a valid option.");
                    break;
            }
        }
    }
}