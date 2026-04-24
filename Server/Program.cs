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
            try
            {
                Console.WriteLine("=====================================");
                Console.WriteLine("UNITED STATE | RP - Multiplayer Server");
                Console.WriteLine("=====================================\n");

                // Database Configuration
                const string dbHost = "localhost";
                const int dbPort = 5432;
                const string dbName = "usrp_game";
                const string dbUser = "postgres";
                const string dbPassword = "password"; // Change this!

                // Server Configuration
                const int tcpPort = 9000;
                const int udpPort = 9001;
                const int maxPlayers = 500;

                // Initialize database connection
                ServerLogger.Info("Main", "Initializing database...");
                var database = new DatabaseHandler(dbHost, dbPort, dbName, dbUser, dbPassword);
                await database.InitializeAsync();

                // Initialize network server
                ServerLogger.Info("Main", "Starting network server...");
                var networkServer = new NetworkServer(tcpPort, udpPort, maxPlayers, database);
                await networkServer.StartAsync();

                ServerLogger.Info("Main", $"Server running on TCP:{tcpPort} UDP:{udpPort}");
                ServerLogger.Info("Main", $"Maximum players: {maxPlayers}");

                // Monitor server
                var monitoringTask = MonitorServerAsync(networkServer);
                await monitoringTask;

                // Shutdown
                await networkServer.StopAsync();
                await database.CloseAsync();
            }
            catch (Exception ex)
            {
                ServerLogger.Critical("Main", "Fatal error", ex);
                Environment.Exit(1);
            }
        }

        static async Task MonitorServerAsync(NetworkServer server)
        {
            try
            {
                while (true)
                {
                    await Task.Delay(30000); // Every 30 seconds

                    var stats = server.GetServerStats();
                    ServerLogger.Info("Monitor", 
                        $"Clients: {stats.TotalConnectedClients}/{stats.MaxPlayers} | " +
                        $"Authenticated: {stats.AuthenticatedPlayers} | " +
                        $"Msgs Sent: {stats.TotalMessagesSent} | " +
                        $"Msgs Recv: {stats.TotalMessagesReceived} | " +
                        $"Uptime: {stats.UptimeSeconds}s");
                }
            }
            catch (Exception ex)
            {
                ServerLogger.Error("Monitor", "Error in monitoring task", ex);
            }
        }
    }
}
