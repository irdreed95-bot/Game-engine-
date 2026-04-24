using System;
using System.Collections.Generic;

namespace USRP.Shared.Models
{
    // ==================== Vector3 Helper ====================
    [Serializable]
    public class Vector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Vector3() { }

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static float Distance(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    // ==================== Account Data ====================
    [Serializable]
    public class AccountData
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Status { get; set; } // active, suspended, banned
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
    }

    // ==================== Session Data ====================
    [Serializable]
    public class SessionData
    {
        public int AccountId { get; set; }
        public int? CharacterId { get; set; }
        public string SessionToken { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    // ==================== Character Data ====================
    [Serializable]
    public class CharacterData
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public string Name { get; set; }
        public string Faction { get; set; }
        public string Job { get; set; }
        public int Health { get; set; }
        public int Armor { get; set; }
        public int Money { get; set; }
        public int BankBalance { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public int PlaytimeSeconds { get; set; }
        public List<InventoryEntry> Inventory { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastSeen { get; set; }
    }

    // ==================== Item Data ====================
    [Serializable]
    public class ItemData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ItemType { get; set; } // weapon, food, tool, misc
        public string Description { get; set; }
        public float Weight { get; set; }
        public int MaxStack { get; set; }
    }

    [Serializable]
    public class InventoryEntry
    {
        public int Id { get; set; }
        public int CharacterId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public int Amount { get; set; }
        public int Slot { get; set; }
    }

    // ==================== Vehicle Data ====================
    [Serializable]
    public class VehicleData
    {
        public int Id { get; set; }
        public int? OwnerId { get; set; }
        public string VehicleType { get; set; }
        public string Model { get; set; }
        public string Plate { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public int Health { get; set; }
        public float Fuel { get; set; }
        public bool IsParked { get; set; }
        public int? DriverId { get; set; }
    }

    // ==================== Faction Data ====================
    [Serializable]
    public class FactionData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Color { get; set; }
    }

    // ==================== World State ====================
    [Serializable]
    public class WorldStateData
    {
        public int TimeHour { get; set; }
        public int TimeMinute { get; set; }
        public string Weather { get; set; }
        public int Temperature { get; set; }
    }

    // ==================== Player Position ====================
    [Serializable]
    public class PlayerPosition
    {
        public int PlayerId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float RotX { get; set; }
        public float RotY { get; set; }
        public float RotZ { get; set; }
    }

    // ==================== Animation State ====================
    [Serializable]
    public class AnimationState
    {
        public int PlayerId { get; set; }
        public string AnimationName { get; set; }
        public float AnimationSpeed { get; set; }
    }
}
