using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using USRP.Shared.Models;
using USRP.Shared.Protocol;
using USRP.Server.Authentication;
using USRP.Server.Database;
using USRP.Server.Utilities;

namespace USRP.Server.Networking
{
    /// <summary>
    /// Represents a connected client.
    /// </summary>
    public class ConnectedClient
    {
        public int ClientId;
        public string SessionToken;
        public int AccountId;
        public int CharacterId;
        public string CharacterName;
        public Vector3 Position;
        public TcpClient TcpClient;
        public UdpClient UdpClient;
        public IPEndPoint UdpEndpoint;
        public DateTime ConnectedAt;
        public DateTime LastMessageAt;
        public bool IsAuthenticated;
        public bool IsInGame;
    }

    /// <summary>
    /// Main server networking manager.
    /// Handles TCP (reliable) and UDP (unreliable) communication with clients.
    /// Supports up to 500 concurrent players.
    /// </summary>
    public class NetworkServer
    {
        private readonly int _tcpPort;
        private readonly int _udpPort;
        private readonly int _maxPlayers;
        private TcpListener _tcpListener;
        private UdpClient _udpServer;
        private readonly DatabaseHandler _db;
        private readonly AuthenticationManager _authManager;

        // Connected clients
        private Dictionary<int, ConnectedClient> _connectedClients;
        private int _nextClientId = 1;

        // Server state
        private bool _isRunning;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private const int SERVER_TICK_RATE = 60; // 60 ticks per second
        private const int TICK_INTERVAL = 1000 / SERVER_TICK_RATE; // ~16ms per tick

        public NetworkServer(int tcpPort, int udpPort, int maxPlayers, DatabaseHandler db)
        {
            _tcpPort = tcpPort;
            _udpPort = udpPort;
            _maxPlayers = maxPlayers;
            _db = db;
            _authManager = new AuthenticationManager(db);
            _connectedClients = new Dictionary<int, ConnectedClient>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        // ==================== Server Lifecycle ====================

        /// <summary>
        /// Start the server and begin listening for connections.
        /// </summary>
        public async Task StartAsync()
        {
            try
            {
                // Test database connection
                bool dbOk = await _db.TestConnectionAsync();
                if (!dbOk)
                {
                    ServerLogger.Critical("Failed to connect to database. Aborting startup.");
                    return;
                }

                _isRunning = true;

                // Start TCP listener
                _tcpListener = new TcpListener(IPAddress.Any, _tcpPort);
                _tcpListener.Start();
                ServerLogger.Info($"TCP Server listening on port {_tcpPort}");

                // Start UDP listener
                _udpServer = new UdpClient(_udpPort);
                ServerLogger.Info($"UDP Server listening on port {_udpPort}");

                // Start accept loop for TCP
                _ = AcceptClientsAsync();

                // Start UDP receive loop
                _ = ReceiveUdpMessagesAsync();

                // Start server tick loop
                _ = ServerTickLoopAsync();

                ServerLogger.Info("Server started successfully!");
            }
            catch (Exception ex)
            {
                ServerLogger.Critical($"Failed to start server: {ex.Message}");
                _isRunning = false;
            }
        }

        /// <summary>
        /// Stop the server gracefully.
        /// </summary>
        public async Task StopAsync()
        {
            ServerLogger.Info("Shutting down server...");
            _isRunning = false;
            _cancellationTokenSource.Cancel();

            // Disconnect all clients
            var clientIds = _connectedClients.Keys.ToList();
            foreach (var clientId in clientIds)
            {
                await DisconnectClientAsync(clientId, "Server shutting down");
            }

            // Close listeners
            _tcpListener?.Stop();
            _udpServer?.Close();

            ServerLogger.Info("Server stopped.");
        }

        // ==================== Client Connection Handling ====================

        /// <summary>
        /// Accept incoming TCP connections from clients.
        /// </summary>
        private async Task AcceptClientsAsync()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    string clientIp = ((IPEndPoint)tcpClient.Client.RemoteEndPoint)?.Address.ToString() ?? "Unknown";
                    ServerLogger.Info($"New TCP connection from {clientIp}");

                    // Create client entry
                    int clientId = Interlocked.Increment(ref _nextClientId);
                    var client = new ConnectedClient
                    {
                        ClientId = clientId,
                        TcpClient = tcpClient,
                        ConnectedAt = DateTime.UtcNow,
                        LastMessageAt = DateTime.UtcNow,
                        IsAuthenticated = false,
                        IsInGame = false
                    };

                    _connectedClients[clientId] = client;
                    ServerLogger.Info($"Client {clientId} added. Total clients: {_connectedClients.Count}");

                    // Start handling this client
                    _ = HandleClientTcpAsync(clientId);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ServerLogger.Error($"Error accepting client: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Handle TCP communication with a specific client.
        /// </summary>
        private async Task HandleClientTcpAsync(int clientId)
        {
            if (!_connectedClients.TryGetValue(clientId, out var client))
                return;

            try
            {
                using (var stream = client.TcpClient.GetStream())
                {
                    byte[] lengthBuffer = new byte[4];

                    while (_isRunning && client.TcpClient.Connected)
                    {
                        // Read message length
                        int bytesRead = await stream.ReadAsync(lengthBuffer, 0, 4);
                        if (bytesRead == 0)
                        {
                            ServerLogger.Warning($"Client {clientId} disconnected (no data)");
                            break;
                        }

                        int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                        if (messageLength <= 0 || messageLength > 1024 * 1024) // 1MB max message
                        {
                            ServerLogger.Warning($"Client {clientId} sent invalid message length: {messageLength}");
                            break;
                        }

                        // Read message data
                        byte[] messageData = new byte[messageLength];
                        bytesRead = 0;
                        while (bytesRead < messageLength)
                        {
                            int read = await stream.ReadAsync(messageData, bytesRead, messageLength - bytesRead);
                            if (read == 0)
                            {
                                ServerLogger.Warning($"Client {clientId} disconnected while reading message");
                                break;
                            }
                            bytesRead += read;
                        }

                        if (bytesRead != messageLength)
                            break;

                        // Process the message
                        await ProcessTcpMessageAsync(clientId, messageData);
                        client.LastMessageAt = DateTime.UtcNow;
                    }
                }
            }
            catch (IOException ex)
            {
                ServerLogger.Info($"Client {clientId} TCP connection closed: {ex.Message}");
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error handling client {clientId} TCP: {ex.Message}");
            }
            finally
            {
                await DisconnectClientAsync(clientId, "Connection closed");
            }
        }

        /// <summary>
        /// Process incoming TCP message from client.
        /// </summary>
        private async Task ProcessTcpMessageAsync(int clientId, byte[] messageData)
        {
            try
            {
                if (!_connectedClients.TryGetValue(clientId, out var client))
                    return;

                // Deserialize message
                var message = DeserializeMessage(messageData);
                if (message == null)
                {
                    ServerLogger.Warning($"Failed to deserialize message from client {clientId}");
                    return;
                }

                ServerLogger.Debug($"Client {clientId} sent message type: {message.Type}");

                // Route message to appropriate handler
                switch (message.Type)
                {
                    case NetMessageType.AuthLoginRequest:
                        await HandleAuthLoginAsync(clientId, message.Payload);
                        break;

                    case NetMessageType.AuthRegisterRequest:
                        await HandleAuthRegisterAsync(clientId, message.Payload);
                        break;

                    case NetMessageType.CharacterSelectRequest:
                        await HandleCharacterSelectAsync(clientId, message.Payload);
                        break;

                    case NetMessageType.CharacterCreateRequest:
                        await HandleCharacterCreateAsync(clientId, message.Payload);
                        break;

                    case NetMessageType.CharacterListRequest:
                        await HandleCharacterListAsync(clientId, message.Payload);
                        break;

                    case NetMessageType.ChatMessage:
                        await HandleChatMessageAsync(clientId, message.Payload);
                        break;

                    case NetMessageType.PlayerPositionUpdate:
                        await HandlePlayerPositionUpdateAsync(clientId, message.Payload);
                        break;

                    case NetMessageType.PingRequest:
                        await HandlePingAsync(clientId);
                        break;

                    default:
                        ServerLogger.Warning($"Unknown message type from client {clientId}: {message.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error processing TCP message from client {clientId}: {ex.Message}");
            }
        }

        // ==================== Message Handlers ====================

        /// <summary>
        /// Handle authentication login request.
        /// </summary>
        private async Task HandleAuthLoginAsync(int clientId, byte[] payload)
        {
            try
            {
                var request = JsonDeserialize<AuthLoginRequest>(payload);
                if (request == null)
                {
                    await SendErrorAsync(clientId, 1, "Invalid login request");
                    return;
                }

                string clientIp = ((IPEndPoint)_connectedClients[clientId].TcpClient.Client.RemoteEndPoint)?.Address.ToString() ?? "Unknown";
                var response = await _authManager.HandleLoginAsync(request, clientIp);

                if (response.Status == AuthResponseStatus.Success)
                {
                    _connectedClients[clientId].SessionToken = response.SessionToken;
                    _connectedClients[clientId].AccountId = response.AccountId;
                    _connectedClients[clientId].IsAuthenticated = true;
                }

                await SendTcpMessageAsync(clientId, NetMessageType.AuthLoginResponse, JsonSerialize(response));
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error handling auth login: {ex.Message}");
                await SendErrorAsync(clientId, 1, "Login failed");
            }
        }

        /// <summary>
        /// Handle authentication registration request.
        /// </summary>
        private async Task HandleAuthRegisterAsync(int clientId, byte[] payload)
        {
            try
            {
                var request = JsonDeserialize<AuthRegisterRequest>(payload);
                if (request == null)
                {
                    await SendErrorAsync(clientId, 1, "Invalid registration request");
                    return;
                }

                string clientIp = ((IPEndPoint)_connectedClients[clientId].TcpClient.Client.RemoteEndPoint)?.Address.ToString() ?? "Unknown";
                var response = await _authManager.HandleRegisterAsync(request, clientIp);

                await SendTcpMessageAsync(clientId, NetMessageType.AuthRegisterResponse, JsonSerialize(response));
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error handling registration: {ex.Message}");
                await SendErrorAsync(clientId, 1, "Registration failed");
            }
        }

        /// <summary>
        /// Handle character selection request.
        /// </summary>
        private async Task HandleCharacterSelectAsync(int clientId, byte[] payload)
        {
            try
            {
                if (!_connectedClients.TryGetValue(clientId, out var client) || !client.IsAuthenticated)
                {
                    await SendErrorAsync(clientId, 2, "Not authenticated");
                    return;
                }

                var request = JsonDeserialize<CharacterSelectRequest>(payload);
                if (request == null)
                {
                    await SendErrorAsync(clientId, 1, "Invalid character select request");
                    return;
                }

                // Validate session token
                var sessionInfo = _authManager.ValidateSession(request.SessionToken);
                if (sessionInfo == null)
                {
                    await SendErrorAsync(clientId, 2, "Invalid session");
                    return;
                }

                // Get character data
                var charData = await _db.GetCharacterByIdAsync(request.CharacterId);
                if (charData == null || charData.AccountId != sessionInfo.AccountId)
                {
                    await SendErrorAsync(clientId, 1, "Character not found");
                    return;
                }

                // Register character selection
                _authManager.SelectCharacter(request.SessionToken, request.CharacterId, charData.Name);
                client.CharacterId = request.CharacterId;
                client.CharacterName = charData.Name;
                client.Position = charData.Position;
                client.IsInGame = true;

                // Send response
                var response = new CharacterSelectResponse
                {
                    Success = true,
                    Message = "Character selected",
                    CharacterId = charData.Id,
                    CharacterName = charData.Name,
                    SpawnFaction = charData.Faction
                };

                await SendTcpMessageAsync(clientId, NetMessageType.CharacterSelectResponse, JsonSerialize(response));

                // Broadcast player join notification to other players
                await BroadcastPlayerJoinedAsync(clientId, charData.Name, charData.Faction);

                ServerLogger.Info($"Player {charData.Name} joined (Client {clientId})");
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error handling character select: {ex.Message}");
                await SendErrorAsync(clientId, 1, "Character selection failed");
            }
        }

        /// <summary>
        /// Handle character creation request.
        /// </summary>
        private async Task HandleCharacterCreateAsync(int clientId, byte[] payload)
        {
            try
            {
                if (!_connectedClients.TryGetValue(clientId, out var client) || !client.IsAuthenticated)
                {
                    await SendErrorAsync(clientId, 2, "Not authenticated");
                    return;
                }

                var request = JsonDeserialize<CharacterCreateRequest>(payload);
                if (request == null)
                {
                    await SendErrorAsync(clientId, 1, "Invalid character create request");
                    return;
                }

                // Validate session token
                var sessionInfo = _authManager.ValidateSession(request.SessionToken);
                if (sessionInfo == null)
                {
                    await SendErrorAsync(clientId, 2, "Invalid session");
                    return;
                }

                // Create character
                var (success, message, characterId) = await _db.CreateCharacterAsync(sessionInfo.AccountId, request.CharacterName, request.InitialFaction);

                var response = new CharacterCreateResponse
                {
                    Success = success,
                    Message = message,
                    CharacterId = characterId
                };

                await SendTcpMessageAsync(clientId, NetMessageType.CharacterCreateResponse, JsonSerialize(response));
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error handling character creation: {ex.Message}");
                await SendErrorAsync(clientId, 1, "Character creation failed");
            }
        }

        /// <summary>
        /// Handle character list request.
        /// </summary>
        private async Task HandleCharacterListAsync(int clientId, byte[] payload)
        {
            try
            {
                if (!_connectedClients.TryGetValue(clientId, out var client) || !client.IsAuthenticated)
                {
                    await SendErrorAsync(clientId, 2, "Not authenticated");
                    return;
                }

                // Get character list for account
                var characters = await _db.GetCharactersByAccountAsync(client.AccountId);

                var response = new CharacterListResponse
                {
                    Characters = characters.ToArray()
                };

                await SendTcpMessageAsync(clientId, NetMessageType.CharacterListResponse, JsonSerialize(response));
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error handling character list: {ex.Message}");
                await SendErrorAsync(clientId, 1, "Failed to fetch characters");
            }
        }

        /// <summary>
        /// Handle chat message from client.
        /// </summary>
        private async Task HandleChatMessageAsync(int clientId, byte[] payload)
        {
            try
            {
                if (!_connectedClients.TryGetValue(clientId, out var client) || !client.IsInGame)
                    return;

                var chatMsg = JsonDeserialize<ChatMessage>(payload);
                if (chatMsg == null || string.IsNullOrEmpty(chatMsg.Message))
                    return;

                chatMsg.SenderId = client.CharacterId;
                chatMsg.SenderName = client.CharacterName;
                chatMsg.SenderX = client.Position.X;
                chatMsg.SenderY = client.Position.Y;
                chatMsg.SenderZ = client.Position.Z;

                // Broadcast to nearby players (proximity chat)
                const float CHAT_DISTANCE = 50f;
                await BroadcastNearbyAsync(clientId, NetMessageType.ChatMessage, JsonSerialize(chatMsg), CHAT_DISTANCE);

                ServerLogger.Debug($"Chat from {client.CharacterName}: {chatMsg.Message}");
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error handling chat message: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle player position update from client.
        /// </summary>
        private async Task HandlePlayerPositionUpdateAsync(int clientId, byte[] payload)
        {
            try
            {
                if (!_connectedClients.TryGetValue(clientId, out var client) || !client.IsInGame)
                    return;

                var update = JsonDeserialize<PlayerPositionUpdate>(payload);
                if (update == null)
                    return;

                // Update client position
                client.Position = new Vector3(update.X, update.Y, update.Z);

                // Save to database periodically (not every frame to reduce DB load)
                // This would be called from the server tick loop for all players

                // Broadcast to nearby players
                const float SYNC_DISTANCE = 100f;
                await BroadcastNearbyAsync(clientId, NetMessageType.PlayerPositionUpdate, payload, SYNC_DISTANCE);
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error handling position update: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle ping request for latency measurement.
        /// </summary>
        private async Task HandlePingAsync(int clientId)
        {
            try
            {
                var pongMsg = new PingMessage
                {
                    ClientTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                await SendTcpMessageAsync(clientId, NetMessageType.PingResponse, JsonSerialize(pongMsg));
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error handling ping: {ex.Message}");
            }
        }

        // ==================== UDP Handling ====================

        /// <summary>
        /// Receive UDP messages from clients.
        /// </summary>
        private async Task ReceiveUdpMessagesAsync()
        {
            while (_isRunning)
            {
                try
                {
                    var result = await _udpServer.ReceiveAsync();
                    // UDP messages typically for movement/position updates (fire and forget)
                    // Can be expanded for real-time sync optimization
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ServerLogger.Warning($"Error receiving UDP message: {ex.Message}");
                }
            }
        }

        // ==================== Server Tick Loop ====================

        /// <summary>
        /// Main server loop - runs at fixed tick rate.
        /// </summary>
        private async Task ServerTickLoopAsync()
        {
            long lastTickTime = DateTime.UtcNow.Ticks;

            while (_isRunning)
            {
                long tickStart = DateTime.UtcNow.Ticks;

                try
                {
                    // Update all game logic
                    await ServerTickAsync();

                    // Cleanup expired sessions
                    _authManager.CleanupExpiredSessions();

                    // Calculate frame time and sleep
                    long tickEnd = DateTime.UtcNow.Ticks;
                    long tickTime = (tickEnd - tickStart) / 10000; // Convert to milliseconds
                    int sleepTime = Math.Max(0, TICK_INTERVAL - (int)tickTime);

                    if (sleepTime > 0)
                        await Task.Delay(sleepTime);
                }
                catch (Exception ex)
                {
                    ServerLogger.Error($"Error in server tick: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Execute one server tick.
        /// </summary>
        private async Task ServerTickAsync()
        {
            // This is where you would:
            // 1. Update world state
            // 2. Process physics
            // 3. Broadcast world updates
            // 4. Save periodically to database

            // For now, just keep-alive check
            var now = DateTime.UtcNow;
            var clientIds = _connectedClients.Keys.ToList();

            foreach (var clientId in clientIds)
            {
                if (_connectedClients.TryGetValue(clientId, out var client))
                {
                    // Check for timeout
                    if ((now - client.LastMessageAt).TotalSeconds > 30)
                    {
                        ServerLogger.Warning($"Client {clientId} timeout");
                        await DisconnectClientAsync(clientId, "Timeout");
                    }
                }
            }
        }

        // ==================== Broadcast Methods ====================

        /// <summary>
        /// Broadcast message to all clients.
        /// </summary>
        private async Task BroadcastAllAsync(NetMessageType messageType, byte[] payload)
        {
            var clientIds = _connectedClients.Keys.ToList();
            foreach (var clientId in clientIds)
            {
                await SendTcpMessageAsync(clientId, messageType, payload);
            }
        }

        /// <summary>
        /// Broadcast message to nearby clients (proximity-based).
        /// </summary>
        private async Task BroadcastNearbyAsync(int senderId, NetMessageType messageType, byte[] payload, float distance)
        {
            if (!_connectedClients.TryGetValue(senderId, out var sender))
                return;

            var clientIds = _connectedClients.Keys.ToList();
            foreach (var clientId in clientIds)
            {
                if (clientId == senderId)
                    continue;

                if (_connectedClients.TryGetValue(clientId, out var client) && client.IsInGame)
                {
                    float dist = Vector3.Distance(sender.Position, client.Position);
                    if (dist <= distance)
                    {
                        await SendTcpMessageAsync(clientId, messageType, payload);
                    }
                }
            }
        }

        /// <summary>
        /// Broadcast player joined notification.
        /// </summary>
        private async Task BroadcastPlayerJoinedAsync(int clientId, string characterName, string faction)
        {
            var notification = new PlayerJoinedNotification
            {
                PlayerId = clientId,
                CharacterName = characterName,
                Faction = faction,
                PositionX = _connectedClients[clientId].Position.X,
                PositionY = _connectedClients[clientId].Position.Y,
                PositionZ = _connectedClients[clientId].Position.Z
            };

            await BroadcastAllAsync(NetMessageType.PlayerJoinedNotification, JsonSerialize(notification));
        }

        /// <summary>
        /// Broadcast player left notification.
        /// </summary>
        private async Task BroadcastPlayerLeftAsync(int clientId, string characterName, string reason)
        {
            var notification = new PlayerLeftNotification
            {
                PlayerId = clientId,
                CharacterName = characterName,
                Reason = reason
            };

            await BroadcastAllAsync(NetMessageType.PlayerLeftNotification, JsonSerialize(notification));
        }

        // ==================== Message Sending ====================

        /// <summary>
        /// Send TCP message to a specific client.
        /// </summary>
        private async Task SendTcpMessageAsync(int clientId, NetMessageType messageType, byte[] payload)
        {
            try
            {
                if (!_connectedClients.TryGetValue(clientId, out var client) || !client.TcpClient.Connected)
                    return;

                var message = new NetMessage(messageType, clientId)
                {
                    Payload = payload
                };

                byte[] serialized = SerializeMessage(message);
                byte[] lengthPrefix = BitConverter.GetBytes(serialized.Length);

                using (var stream = client.TcpClient.GetStream())
                {
                    await stream.WriteAsync(lengthPrefix, 0, 4);
                    await stream.WriteAsync(serialized, 0, serialized.Length);
                    await stream.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                ServerLogger.Warning($"Failed to send TCP message to client {clientId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Send error message to client.
        /// </summary>
        private async Task SendErrorAsync(int clientId, int errorCode, string message)
        {
            var errorMsg = new ErrorMessage
            {
                ErrorCode = errorCode,
                ErrorDescription = message
            };

            await SendTcpMessageAsync(clientId, NetMessageType.ErrorMessage, JsonSerialize(errorMsg));
        }

        // ==================== Cleanup ====================

        /// <summary>
        /// Disconnect a client gracefully.
        /// </summary>
        private async Task DisconnectClientAsync(int clientId, string reason)
        {
            if (!_connectedClients.TryGetValue(clientId, out var client))
                return;

            // End session if authenticated
            if (!string.IsNullOrEmpty(client.SessionToken))
            {
                await _authManager.EndSessionAsync(client.SessionToken);
            }

            // Broadcast leave notification if in game
            if (client.IsInGame && !string.IsNullOrEmpty(client.CharacterName))
            {
                await BroadcastPlayerLeftAsync(clientId, client.CharacterName, reason);
            }

            // Close TCP connection
            try
            {
                client.TcpClient?.Close();
                client.TcpClient?.Dispose();
            }
            catch { }

            // Remove from connected clients
            _connectedClients.Remove(clientId);

            ServerLogger.Info($"Client {clientId} disconnected ({reason}). Total clients: {_connectedClients.Count}");
        }

        // ==================== Serialization Helpers ====================

        /// <summary>
        /// Serialize message using JSON (simple approach - can optimize with binary format).
        /// </summary>
        private byte[] SerializeMessage(NetMessage message)
        {
            // In production, use proper serialization (protobuf, msgpack, etc.)
            string json = System.Text.Json.JsonSerializer.Serialize(message);
            return Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// Deserialize message from bytes.
        /// </summary>
        private NetMessage DeserializeMessage(byte[] data)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data);
                return System.Text.Json.JsonSerializer.Deserialize<NetMessage>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// JSON serialize to bytes.
        /// </summary>
        private byte[] JsonSerialize<T>(T obj)
        {
            string json = System.Text.Json.JsonSerializer.Serialize(obj);
            return Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// JSON deserialize from bytes.
        /// </summary>
        private T JsonDeserialize<T>(byte[] data)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data);
                return System.Text.Json.JsonSerializer.Deserialize<T>(json);
            }
            catch
            {
                return default(T);
            }
        }

        // ==================== Server Info ====================

        /// <summary>
        /// Get current server statistics.
        /// </summary>
        public Dictionary<string, object> GetServerStats()
        {
            return new Dictionary<string, object>
            {
                { "TotalConnectedClients", _connectedClients.Count },
                { "MaxPlayers", _maxPlayers },
                { "IsRunning", _isRunning },
                { "TCPPort", _tcpPort },
                { "UDPPort", _udpPort },
                { "TickRate", SERVER_TICK_RATE },
                { "ActiveSessions", _authManager.GetActiveSessions().Count },
                { "Uptime", DateTime.UtcNow }
            };
        }
    }

    /// <summary>
    /// Additional message type for character list response.
    /// </summary>
    [Serializable]
    public class CharacterListResponse
    {
        public CharacterData[] Characters;
    }
}
