using System;
using System.Text.Json.Serialization;
using USRP.Shared.Models;

namespace USRP.Shared.Protocol
{
    // ==================== Message Type Enum ====================
    public enum NetMessageType
    {
        // Authentication
        AuthLoginRequest = 1,
        AuthLoginResponse = 2,
        AuthRegisterRequest = 3,
        AuthRegisterResponse = 4,
        AuthLogoutRequest = 5,
        AuthLogoutResponse = 6,

        // Character Management
        CharacterListRequest = 10,
        CharacterListResponse = 11,
        CharacterSelectRequest = 12,
        CharacterSelectResponse = 13,
        CharacterCreateRequest = 14,
        CharacterCreateResponse = 15,
        CharacterDeleteRequest = 16,
        CharacterDeleteResponse = 17,

        // Real-time Synchronization
        PlayerPositionUpdate = 20,
        PlayerAnimationUpdate = 21,
        PlayerHealthUpdate = 22,
        PlayerArmorUpdate = 23,
        PlayerStatsUpdate = 24,
        PlayerJoinedNotification = 25,
        PlayerLeftNotification = 26,

        // Chat & Communication
        ChatMessage = 30,
        FactionChatMessage = 31,
        PrivateChatMessage = 32,
        RadioMessage = 33,

        // World State
        TimeUpdate = 40,
        WeatherUpdate = 41,
        VehicleStateUpdate = 42,
        WorldStateSync = 43,

        // Utility
        PingRequest = 50,
        PingResponse = 51,
        ErrorMessage = 100
    }

    // ==================== Base Message Classes ====================
    [Serializable]
    public class NetMessage
    {
        public NetMessageType Type { get; set; }
        public int ClientId { get; set; }
        public byte[] Payload { get; set; }

        public NetMessage() { }

        public NetMessage(NetMessageType type, int clientId = 0)
        {
            Type = type;
            ClientId = clientId;
        }
    }

    // ==================== Authentication Messages ====================
    [Serializable]
    public class AuthLoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public enum AuthResponseStatus
    {
        Success = 0,
        InvalidCredentials = 1,
        AccountSuspended = 2,
        AccountBanned = 3,
        ServerError = 4
    }

    [Serializable]
    public class AuthLoginResponse
    {
        public AuthResponseStatus Status { get; set; }
        public string Message { get; set; }
        public int AccountId { get; set; }
        public string SessionToken { get; set; }
        public long SessionExpiresAt { get; set; }
    }

    [Serializable]
    public class AuthRegisterRequest
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
    }

    [Serializable]
    public class AuthRegisterResponse
    {
        public AuthResponseStatus Status { get; set; }
        public string Message { get; set; }
        public int AccountId { get; set; }
    }

    [Serializable]
    public class AuthLogoutRequest
    {
        public string SessionToken { get; set; }
    }

    [Serializable]
    public class AuthLogoutResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    // ==================== Character Management Messages ====================
    [Serializable]
    public class CharacterListRequest
    {
        public string SessionToken { get; set; }
    }

    [Serializable]
    public class CharacterListResponse
    {
        public CharacterData[] Characters { get; set; }
    }

    [Serializable]
    public class CharacterSelectRequest
    {
        public string SessionToken { get; set; }
        public int CharacterId { get; set; }
    }

    [Serializable]
    public class CharacterSelectResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int CharacterId { get; set; }
        public string CharacterName { get; set; }
        public string SpawnFaction { get; set; }
        public Vector3 SpawnPosition { get; set; }
    }

    [Serializable]
    public class CharacterCreateRequest
    {
        public string SessionToken { get; set; }
        public string CharacterName { get; set; }
        public string InitialFaction { get; set; }
    }

    [Serializable]
    public class CharacterCreateResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int CharacterId { get; set; }
    }

    [Serializable]
    public class CharacterDeleteRequest
    {
        public string SessionToken { get; set; }
        public int CharacterId { get; set; }
    }

    [Serializable]
    public class CharacterDeleteResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    // ==================== Real-time Synchronization Messages ====================
    [Serializable]
    public class PlayerPositionUpdate
    {
        public int PlayerId { get; set; }
        public string CharacterName { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float RotX { get; set; }
        public float RotY { get; set; }
        public float RotZ { get; set; }
    }

    [Serializable]
    public class PlayerAnimationUpdate
    {
        public int PlayerId { get; set; }
        public string AnimationName { get; set; }
        public float Speed { get; set; }
    }

    [Serializable]
    public class PlayerHealthUpdate
    {
        public int PlayerId { get; set; }
        public int Health { get; set; }
        public string CharacterName { get; set; }
    }

    [Serializable]
    public class PlayerArmorUpdate
    {
        public int PlayerId { get; set; }
        public int Armor { get; set; }
        public string CharacterName { get; set; }
    }

    [Serializable]
    public class PlayerStatsUpdate
    {
        public int PlayerId { get; set; }
        public string CharacterName { get; set; }
        public int Health { get; set; }
        public int Armor { get; set; }
        public int Money { get; set; }
    }

    [Serializable]
    public class PlayerJoinedNotification
    {
        public int PlayerId { get; set; }
        public string CharacterName { get; set; }
        public string Faction { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
    }

    [Serializable]
    public class PlayerLeftNotification
    {
        public int PlayerId { get; set; }
        public string CharacterName { get; set; }
        public string Reason { get; set; }
    }

    // ==================== Chat Messages ====================
    [Serializable]
    public class ChatMessage
    {
        public int SenderId { get; set; }
        public string SenderName { get; set; }
        public string Message { get; set; }
        public float SenderX { get; set; }
        public float SenderY { get; set; }
        public float SenderZ { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    [Serializable]
    public class FactionChatMessage
    {
        public int SenderId { get; set; }
        public string SenderName { get; set; }
        public string Faction { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    [Serializable]
    public class PrivateChatMessage
    {
        public int SenderId { get; set; }
        public string SenderName { get; set; }
        public int ReceiverId { get; set; }
        public string ReceiverName { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    [Serializable]
    public class RadioMessage
    {
        public int SenderId { get; set; }
        public string SenderName { get; set; }
        public string Faction { get; set; }
        public int Frequency { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    // ==================== World State Messages ====================
    [Serializable]
    public class TimeUpdate
    {
        public int Hour { get; set; }
        public int Minute { get; set; }
    }

    [Serializable]
    public class WeatherUpdate
    {
        public string Weather { get; set; } // clear, rain, snow, fog
        public int Temperature { get; set; }
        public float WindSpeed { get; set; }
    }

    [Serializable]
    public class VehicleStateUpdate
    {
        public int VehicleId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float RotX { get; set; }
        public float RotY { get; set; }
        public float RotZ { get; set; }
        public int Health { get; set; }
        public float Fuel { get; set; }
        public int? DriverId { get; set; }
    }

    [Serializable]
    public class WorldStateSync
    {
        public WorldStateData State { get; set; }
        public VehicleData[] Vehicles { get; set; }
        public PlayerPosition[] NearbyPlayers { get; set; }
    }

    // ==================== Utility Messages ====================
    [Serializable]
    public class PingMessage
    {
        public long ClientTimestamp { get; set; }
        public long ServerTimestamp { get; set; }
    }

    [Serializable]
    public class ErrorMessage
    {
        public int ErrorCode { get; set; }
        public string ErrorDescription { get; set; }
    }
}
