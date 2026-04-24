using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using USRP.Server.Utilities;
using USRP.Shared.Models;

namespace USRP.Server.Database
{
    /// <summary>
    /// Handles all database operations with connection pooling.
    /// </summary>
    public class DatabaseHandler
    {
        private readonly string _connectionString;
        private NpgsqlDataSource _dataSource;

        public DatabaseHandler(string host, int port, string database, string username, string password)
        {
            _connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};MaxPoolSize=20;MinPoolSize=5;";
        }

        /// <summary>
        /// Initialize the database connection pool.
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                var builder = new NpgsqlDataSourceBuilder(_connectionString);
                _dataSource = builder.Build();

                // Test connection
                using (var connection = await _dataSource.OpenConnectionAsync())
                {
                    using (var cmd = new NpgsqlCommand("SELECT 1", connection))
                    {
                        await cmd.ExecuteScalarAsync();
                    }
                }

                ServerLogger.Info("Database connection pool initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                ServerLogger.Critical($"Failed to initialize database: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Test database connectivity.
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using (var connection = await _dataSource.OpenConnectionAsync())
                {
                    using (var cmd = new NpgsqlCommand("SELECT 1", connection))
                    {
                        await cmd.ExecuteScalarAsync();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Database connection test failed: {ex.Message}");
                return false;
            }
        }

        // ==================== Account Operations ====================

        /// <summary>
        /// Register a new account.
        /// </summary>
        public async Task<(bool success, string message, int accountId)> RegisterAccountAsync(string username, string email, string password)
        {
            try
            {
                // Validate input
                if (!ValidationHelper.IsValidUsername(username))
                    return (false, "Invalid username format", -1);
                if (!ValidationHelper.IsValidEmail(email))
                    return (false, "Invalid email format", -1);
                if (!ValidationHelper.IsValidPassword(password))
                    return (false, "Password must be at least 6 characters with letters and numbers", -1);

                string passwordHash = PasswordHasher.HashPassword(password);

                using (var connection = await _dataSource.OpenConnectionAsync())
                {
                    using (var cmd = new NpgsqlCommand(
                        "INSERT INTO accounts (username, email, password_hash, status) VALUES (@username, @email, @hash, 'active') RETURNING id",
                        connection))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.Parameters.AddWithValue("@email", email);
                        cmd.Parameters.AddWithValue("@hash", passwordHash);

                        var result = await cmd.ExecuteScalarAsync();
                        int accountId = Convert.ToInt32(result);

                        ServerLogger.Info($"Account created: {username} (ID: {accountId})");
                        return (true, "Account created successfully", accountId);
                    }
                }
            }
            catch (NpgsqlException ex) when (ex.SqlState == "23505") // Unique constraint
            {
                if (ex.Message.Contains("username"))
                    return (false, "Username already taken", -1);
                if (ex.Message.Contains("email"))
                    return (false, "Email already in use", -1);
                return (false, "Account already exists", -1);
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error registering account: {ex.Message}");
                return (false, "Registration failed", -1);
            }
        }

        /// <summary>
        /// Authenticate account with username and password.
        /// </summary>
        public async Task<(bool success, int accountId, string status)> AuthenticateAsync(string username, string password)
        {
            try
            {
                using (var connection = await _dataSource.OpenConnectionAsync())
                {
                    using (var cmd = new NpgsqlCommand(
                        "SELECT id, password_hash, status FROM accounts WHERE username = @username",
                        connection))
                    {
                        cmd.Parameters.AddWithValue("@username", username);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync())
                                return (false, -1, "Invalid username");

                            int accountId = reader.GetInt32(0);
                            string passwordHash = reader.GetString(1);
                            string status = reader.GetString(2);

                            // Verify password
                            if (!PasswordHasher.VerifyPassword(password, passwordHash))
                                return (false, -1, "Invalid password");

                            // Check account status
                            if (status == "suspended")
                                return (false, accountId, "suspended");
                            if (status == "banned")
                                return (false, accountId, "banned");

                            // Update last login
                            await UpdateLastLoginAsync(accountId);

                            return (true, accountId, "active");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error authenticating account: {ex.Message}");
                return (false, -1, "Authentication failed");
            }
        }

        /// <summary>
        /// Update last login timestamp for account.
        /// </summary>
        public async Task UpdateLastLoginAsync(int accountId)
        {
            try
            {
                using (var connection = await _dataSource.OpenConnectionAsync())
                {
                    using (var cmd = new NpgsqlCommand(
                        "UPDATE accounts SET last_login = NOW() WHERE id = @id",
                        connection))
                    {
                        cmd.Parameters.AddWithValue("@id", accountId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                ServerLogger.Warning($"Failed to update last login: {ex.Message}");
            }
        }

        // ==================== Session Operations ====================

        /// <summary>
        /// Create a new session for an authenticated account.
        /// </summary>
        public async Task<(bool success, string sessionToken, DateTime expiresAt)> CreateSessionAsync(int accountId, string ipAddress)
        {
            try
            {
                string sessionToken = TokenGenerator.GenerateToken();
                var expiresAt = DateTime.UtcNow.AddHours(24);

                using (var connection = await _dataSource.OpenConnectionAsync())
                {
                    using (var cmd = new NpgsqlCommand(
                        "INSERT INTO sessions (account_id, session_token, ip_address, expires_at, is_active) VALUES (@account_id, @token, @ip, @expires, true)",
                        connection))
                    {
                        cmd.Parameters.AddWithValue("@account_id", accountId);
                        cmd.Parameters.AddWithValue("@token", sessionToken);
                        cmd.Parameters.AddWithValue("@ip", ipAddress ?? "Unknown");
                        cmd.Parameters.AddWithValue("@expires", expiresAt);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                ServerLogger.Info($"Session created for account {accountId}");
                return (true, sessionToken, expiresAt);
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error creating session: {ex.Message}");
                return (false, null, DateTime.MinValue);
            }
        }

        /// <summary>
        /// Validate a session token.
        /// </summary>
        public async Task<SessionData> ValidateSessionAsync(string sessionToken)
        {
            try
            {
                using (var connection = await _dataSource.OpenConnectionAsync())
                {
                    using (var cmd = new NpgsqlCommand(
                        "SELECT account_id, character_id, session_token, created_at, expires_at FROM sessions WHERE session_token = @token AND is_active = true AND expires_at > NOW()",
                        connection))
                    {
                        cmd.Parameters.AddWithValue("@token", sessionToken);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync())
                                return null;

                            return new SessionData
                            {
                                AccountId = reader.GetInt32(0),
                                CharacterId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1),
                                SessionToken = reader.GetString(2),
                                CreatedAt = reader.GetDateTime(3),
                                ExpiresAt = reader.GetDateTime(4)
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ServerLogger.Warning($"Error validating session: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// End a session (logout).
        /// </summary>
        public async Task<bool> EndSessionAsync(string sessionToken)
        {
            try
            {
                using (var connection = await _dataSource.OpenConnectionAsync())
                {
                    using (var cmd = new NpgsqlCommand(
                        "UPDATE sessions SET is_active = false WHERE session_token = @token",
                        connection))
                    {
                        cmd.Parameters.AddWithValue("@token", sessionToken);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                ServerLogger.Info($"Session ended: {sessionToken}");
                return true;
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error ending session: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cleanup expired sessions.
        /// </summary>
        public async Task CleanupExpiredSessionsAsync()
        {
            try
            {
                using (var connection = await _dataSource.OpenConnectionAsync())
                {
                    using (var cmd = new NpgsqlCommand(
                        "DELETE FROM sessions WHERE expires_at < NOW() OR (is_active = false AND last_activity < NOW() - INTERVAL '1 hour')",
                        connection))
                    {
                        int deleted = await cmd.ExecuteNonQueryAsync();
                        if (deleted > 0)
                            ServerLogger.Debug($"Cleaned up {deleted} expired sessions");
                    }
                }
            }
            catch (Exception ex)
            {
                ServerLogger.Warning($"Error cleaning up sessions: {ex.Message}");
            }
        }

        // ==================== Character Operations ====================

        /// <summary>
        /// Get all characters for an account.
        /// </summary>
        public async Task<List<CharacterData>> GetCharactersByAccountAsync(int accountId)
        {
            var characters = new List<CharacterData>();

            try
            {
                using (var connection = await _dataSource.OpenConnectionAsync())
                {
                    using (var cmd = new NpgsqlCommand(
                        "SELECT id, account_id, name, job, faction_id, health, armor, money, bank_balance, position_x, position_y, position_z, playtime_seconds, created_at FROM characters WHERE account_id = @account_id ORDER BY created_at",
                        connection))
                    {
                        cmd.Parameters.AddWithValue("@account_id", accountId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                characters.Add(new CharacterData
                                {
                                    Id = reader.GetInt32(0),
                                    AccountId = reader.GetInt32(1),
                                    Name = reader.GetString(2),
                                    Job = reader.GetString(3),
                                    Faction = reader.IsDBNull(4) ? "Civilian" : reader.GetInt32(4).ToString(),
                                    Health = reader.GetInt32(5),
                                    Armor = reader.GetInt32(6),
                                    Money = reader.GetInt32(7),
                                    BankBalance = reader.GetInt32(8),
                                    Position = new Vector3(reader.GetFloat(9), reader.GetFloat(10), reader.GetFloat(11)),
                                    PlaytimeSeconds = reader.GetInt32(12),
                                    CreatedAt = reader.GetDateTime(13)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error fetching characters: {ex.Message}");
            }

            return characters;
        }

        /// <summary>
        /// Get a specific character by ID.
        /// </summary>
        public async Task<CharacterData> GetCharacterByIdAsync(int characterId)
        {
            try
            {
                using (var connection = await _dataSource.OpenConnectionAsync())
                {
                    using (var cmd = new NpgsqlCommand(
                        "SELECT id, account_id, name, job, faction_id, health, armor, money, bank_balance, position_x, position_y, position_z, playtime_seconds, created_at FROM characters WHERE id = @id",
                        connection))
                    {
                        cmd.Parameters.AddWithValue("@id", characterId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new CharacterData
                                {
                                    Id = reader.GetInt32(0),
                                    AccountId = reader.GetInt32(1),
                                    Name = reader.GetString(2),
                                    Job = reader.GetString(3),
                                    Faction = reader.IsDBNull(4) ? "Civilian" : reader.GetInt32(4).ToString(),
                                    Health = reader.GetInt32(5),
                                    Armor = reader.GetInt32(6),
                                    Money = reader.GetInt32(7),
                                    BankBalance = reader.GetInt32(8),
                                    Position = new Vector3(reader.GetFloat(9), reader.GetFloat(10), reader.GetFloat(11)),
                                    PlaytimeSeconds = reader.GetInt32(12),
                                    CreatedAt = reader.GetDateTime(13)
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error fetching character: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Create a new character.
        /// </summary>
        public async Task<(bool success, string message, int characterId)> CreateCharacterAsync(int accountId, string characterName, string initialFaction)
        {
            try
            {
                // Validate character name
                if (!ValidationHelper.IsValidCharacterName(characterName))
                    return (false, "Invalid character name (3-32 letters only)", -1);

                // Check character count limit (5 per account)
                var characters = await GetCharactersByAccountAsync(accountId);
                if (characters.Count >= 5)
                    return (false, "Maximum 5 characters per account", -1);

                // Get faction ID
                int factionId = 5; // Default to Civilian
                if (!string.IsNullOrEmpty(initialFaction) && initialFaction != "Civilian")
                {
                    using (var connection = await _dataSource.OpenConnectionAsync())
                    {
                        using (var cmd = new NpgsqlCommand("SELECT id FROM factions WHERE name = @name", connection))
                        {
                            cmd.Parameters.AddWithValue("@name", initialFaction);
                            var result = await cmd.ExecuteScalarAsync();
                            if (result != null)
                                factionId = Convert.ToInt32(result);
                        }
                    }
                }

                using (var connection = await _dataSource.OpenConnectionAsync())
                {
                    using (var cmd = new NpgsqlCommand(
                        "INSERT INTO characters (account_id, name, faction_id, job, health, armor, money, position_x, position_y, position_z) VALUES (@account_id, @name, @faction_id, @job, 100, 0, 5000, 0, 0, 0) RETURNING id",
                        connection))
                    {
                        cmd.Parameters.AddWithValue("@account_id", accountId);
                        cmd.Parameters.AddWithValue("@name", characterName);
                        cmd.Parameters.AddWithValue("@faction_id", factionId);
                        cmd.Parameters.AddWithValue("@job", "Civilian");

                        var result = await cmd.ExecuteScalarAsync();
                        int characterId = Convert.ToInt32(result);

                        ServerLogger.Info($"Character created: {characterName} (ID: {characterId}, Account: {accountId})");
                        return (true, "Character created", characterId);
                    }
                }
            }
            catch (NpgsqlException ex) when (ex.SqlState == "23505") // Unique constraint
            {
                return (false, "Character name already taken", -1);
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error creating character: {ex.Message}");
                return (false, "Failed to create character", -1);
            }
        }

        /// <summary>
        /// Update character position.
        /// </summary>
        public async Task UpdateCharacterPositionAsync(int characterId, Vector3 position)
        {
            try
            {
                using (var connection = await _dataSource.OpenConnectionAsync())
                {
                    using (var cmd = new NpgsqlCommand(
                        "UPDATE characters SET position_x = @x, position_y = @y, position_z = @z, updated_at = NOW() WHERE id = @id",
                        connection))
                    {
                        cmd.Parameters.AddWithValue("@id", characterId);
                        cmd.Parameters.AddWithValue("@x", position.X);
                        cmd.Parameters.AddWithValue("@y", position.Y);
                        cmd.Parameters.AddWithValue("@z", position.Z);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                ServerLogger.Warning($"Failed to update character position: {ex.Message}");
            }
        }

        /// <summary>
        /// Update character stats (health, armor, money).
        /// </summary>
        public async Task UpdateCharacterStatsAsync(int characterId, int health, int armor, int money)
        {
            try
            {
                using (var connection = await _dataSource.OpenConnectionAsync())
                {
                    using (var cmd = new NpgsqlCommand(
                        "UPDATE characters SET health = @health, armor = @armor, money = @money, updated_at = NOW() WHERE id = @id",
                        connection))
                    {
                        cmd.Parameters.AddWithValue("@id", characterId);
                        cmd.Parameters.AddWithValue("@health", Math.Max(0, Math.Min(100, health)));
                        cmd.Parameters.AddWithValue("@armor", Math.Max(0, Math.Min(100, armor)));
                        cmd.Parameters.AddWithValue("@money", Math.Max(0, money));

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error updating character stats: {ex.Message}");
            }
        }

        /// <summary>
        /// Bind a character to a session (character selection).
        /// </summary>
        public async Task BindCharacterToSessionAsync(string sessionToken, int characterId)
        {
            try
            {
                using (var connection = await _dataSource.OpenConnectionAsync())
                {
                    using (var cmd = new NpgsqlCommand(
                        "UPDATE sessions SET character_id = @character_id WHERE session_token = @token",
                        connection))
                    {
                        cmd.Parameters.AddWithValue("@character_id", characterId);
                        cmd.Parameters.AddWithValue("@token", sessionToken);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                ServerLogger.Warning($"Failed to bind character to session: {ex.Message}");
            }
        }
    }
}
