using System;
using System.Threading.Tasks;
using USRP.Server.Database;
using USRP.Server.Networking;
using USRP.Server.Utilities;

namespace USRP.Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=====================================");
            Console.WriteLine("UNITED STATE | RP - Multiplayer Server");
            Console.WriteLine("=====================================");
            Console.WriteLine();

            // Configuration
            const string DB_HOST = "localhost";
            const int DB_PORT = 5432;
            const string DB_NAME = "usrp_game";
            const string DB_USER = "postgres";
            const string DB_PASSWORD = "password"; // Change this!
            const int TCP_PORT = 9000;
            const int UDP_PORT = 9001;
            const int MAX_PLAYERS = 500;

            try
            {
                // Initialize database
                ServerLogger.Info("Initializing database...");
                var db = new DatabaseHandler(DB_HOST, DB_PORT, DB_NAME, DB_USER, DB_PASSWORD);
                bool dbReady = await db.InitializeAsync();

                if (!dbReady)
                {
                    ServerLogger.Critical("Failed to initialize database. Exiting.");
                    return;
                }

                // Start network server
                ServerLogger.Info("Starting network server...");
                var server = new NetworkServer(TCP_PORT, UDP_PORT, MAX_PLAYERS, db);
                await server.StartAsync();

                // Keep server running
                ServerLogger.Info("Server is running. Press CTRL+C to stop.");
                Console.CancelKeyPress += async (s, e) =>
                {
                    e.Cancel = true;
                    await server.StopAsync();
                    Environment.Exit(0);
                };

                while (true)
                {
                    await Task.Delay(1000);

                    // Optionally print server stats periodically
                    // var stats = server.GetServerStats();
                    // ServerLogger.Info($"Connected: {stats[\"TotalConnectedClients\"]}/{stats[\"MaxPlayers\"]}");
                }
            }
            catch (Exception ex)
            {
                ServerLogger.Critical($"Fatal error: {ex.Message}");
            }
        }
    }
}
