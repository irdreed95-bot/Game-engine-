# UNITED STATE | RP - Multiplayer Game Engine

A professional multiplayer roleplay game engine built with C# and .NET, supporting up to 500 concurrent players.

## Architecture Overview

```
[Unity Client] <-- TCP (9000) / UDP (9001) --> [Authoritative Server] <-- PostgreSQL --> [Database]
```

### Key Features

- ✅ **Secure Authentication**: PBKDF2 password hashing + session tokens
- ✅ **Dual Protocol Networking**: TCP for reliability, UDP for speed
- ✅ **500 CCU Scalability**: Connection pooling, async I/O, optimized queries
- ✅ **Character System**: Multiple characters per account, stats, inventory, positions
- ✅ **Faction/Job System**: Police, Hospital, Fire, Taxi, custom factions
- ✅ **Real-Time Synchronization**: Position, animation, health, armor updates
- ✅ **Proximity-Based Chat**: 50m radius communication + faction radio
- ✅ **World State Sync**: Time, weather, vehicle states
- ✅ **Authoritative Server**: All logic server-side to prevent exploits

## Project Structure

```
/Game-engine-/
├── Database/
│   └── schema.sql                 # PostgreSQL schema
├── Server/
│   ├── Authentication/
│   │   └── AuthenticationManager.cs
│   ├── Database/
│   │   └── DatabaseHandler.cs
│   ├── Networking/
│   │   └── NetworkServer.cs
│   ├── Utilities/
│   │   └── SecurityHelper.cs
│   ├── Program.cs
│   └── Server.csproj
├── Shared/
│   ├── Models/
│   │   └── DataModels.cs
│   ├── Protocol/
│   │   └── NetMessages.cs
│   └── Shared.csproj
└── README.md
```

## Quick Start

### 1. Prerequisites

- .NET 7.0+
- PostgreSQL 13+
- Windows, Linux, or macOS

### 2. Setup Database

```bash
# Create database
creatdb usrp_game

# Load schema
psql usrp_game < Database/schema.sql
```

### 3. Configure Server

Edit `Server/Program.cs`:
```csharp
const string DB_PASSWORD = "your_secure_password";
const string DB_HOST = "your_database_host";
```

### 4. Build and Run

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run server
dotnet run --project Server/Server.csproj
```

### 5. Expected Output

```
=====================================
UNITED STATE | RP - Multiplayer Server
=====================================

[2026-04-24 12:00:00] [INFO] Initializing database...
[2026-04-24 12:00:01] [INFO] Database connection pool initialized successfully
[2026-04-24 12:00:01] [INFO] Starting network server...
[2026-04-24 12:00:02] [INFO] TCP Server listening on port 9000
[2026-04-24 12:00:02] [INFO] UDP Server listening on port 9001
[2026-04-24 12:00:02] [INFO] Server started successfully!
[2026-04-24 12:00:02] [INFO] Server is running. Press CTRL+C to stop.
```

## Client Integration

### Example: Connect and Login

```csharp
using USRP.Shared.Protocol;
using System.Text.Json;

// Create login request
var loginRequest = new AuthLoginRequest
{
    Username = "player",
    Password = "password123"
};

// Serialize to JSON
byte[] payload = Encoding.UTF8.GetBytes(
    JsonSerializer.Serialize(loginRequest)
);

// Create network message
var message = new NetMessage(NetMessageType.AuthLoginRequest)
{
    Payload = payload
};

// Send via TCP to server:9000
// (Implementation depends on your networking library)
```

## Message Types

### Authentication
- `AuthLoginRequest` → `AuthLoginResponse`
- `AuthRegisterRequest` → `AuthRegisterResponse`
- `AuthLogoutRequest` → `AuthLogoutResponse`

### Character Management
- `CharacterListRequest` → `CharacterListResponse`
- `CharacterSelectRequest` → `CharacterSelectResponse`
- `CharacterCreateRequest` → `CharacterCreateResponse`

### Real-Time Sync
- `PlayerPositionUpdate` - Position synchronization (50m radius)
- `PlayerAnimationUpdate` - Animation state
- `PlayerJoinedNotification` - Player join event
- `PlayerLeftNotification` - Player disconnect event

### Communication
- `ChatMessage` - Proximity-based chat (50m radius)
- `FactionChatMessage` - Faction/radio communication
- `PrivateChatMessage` - Direct messaging

### World State
- `TimeUpdate` - Server time synchronization
- `WeatherUpdate` - Weather changes
- `VehicleStateUpdate` - Vehicle synchronization

## Database Schema

### Tables
- `accounts` - Player accounts
- `sessions` - Active sessions
- `characters` - Character data
- `items` - Item definitions
- `character_inventory` - Per-character inventory
- `vehicles` - Vehicle ownership and state
- `factions` - Faction definitions
- `faction_members` - Faction memberships
- `world_state` - Dynamic world elements
- `chat_logs` - Communication history

## Performance Specifications

- **Concurrent Players**: Up to 500 per server
- **Server Tick Rate**: 60 ticks/second
- **Update Frequency**: Every ~16ms
- **Connection Pool**: 20 concurrent DB connections
- **Message Size Limit**: 1MB per message
- **Position Update Broadcast**: 100m radius
- **Chat Broadcast Radius**: 50m
- **Session Timeout**: 24 hours
- **Keep-Alive Interval**: 30 seconds

## Security Features

- ✅ **PBKDF2 Password Hashing**: 10,000 iterations with SHA256
- ✅ **Cryptographically Secure Tokens**: 256-bit random tokens
- ✅ **SQL Injection Prevention**: Parameterized queries throughout
- ✅ **Timing Attack Prevention**: Constant-time comparison
- ✅ **Input Validation**: Username, email, password, character name
- ✅ **Authoritative Server**: All game logic server-side
- ✅ **Session Management**: Token-based, automatic cleanup
- ✅ **Account Status**: Active, suspended, banned flags

## Troubleshooting

### Database Connection Failed
```
Error: Failed to initialize database
```
- Verify PostgreSQL is running
- Check connection string (host, port, credentials)
- Ensure `usrp_game` database exists

### Port Already in Use
```
Error: Address already in use
```
- Change TCP_PORT or UDP_PORT in Program.cs
- Or kill the process: `lsof -ti:9000 | xargs kill -9`

### Performance Issues

- Increase `MaxPoolSize` in connection string
- Monitor database query times
- Check network latency to clients
- Verify server CPU/RAM availability

## Future Enhancements

- [ ] Binary protocol for optimized message size
- [ ] Distributed server system (multi-zone)
- [ ] Automatic server clustering
- [ ] Web admin dashboard
- [ ] Advanced anti-cheat
- [ ] Voice chat integration (Vivox/Dissonance)
- [ ] Dynamic LOD system
- [ ] Persistent economy system

## License

Private - UNITED STATE | RP

## Support

For issues or questions, contact the development team.
