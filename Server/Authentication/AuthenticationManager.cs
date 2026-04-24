using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using USRP.Server.Database;
using USRP.Server.Utilities;
using USRP.Shared.Protocol;

namespace USRP.Server.Authentication
{
    /// <summary>
    /// Manages player authentication and session handling.
    /// </summary>
    public class AuthenticationManager
    {
        private readonly DatabaseHandler _db;
        private Dictionary<string, SessionInfo> _activeSessions; // Token -> SessionInfo

        public class SessionInfo
        {
            public int AccountId { get; set; }
            public int? CharacterId { get; set; }
            public string SessionToken { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
            public DateTime LastActivity { get; set; }
        }

        public AuthenticationManager(DatabaseHandler db)
        {
            _db = db;
            _activeSessions = new Dictionary<string, SessionInfo>();
        }

        // ==================== Login Handler ====================

        /// <summary>
        /// Handle login request from client.
        /// </summary>
        public async Task<AuthLoginResponse> HandleLoginAsync(AuthLoginRequest request, string clientIp)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return new AuthLoginResponse
                    {
                        Status = AuthResponseStatus.InvalidCredentials,
                        Message = "Username and password required"
                    };
                }

                // Authenticate with database
                var (success, accountId, status) = await _db.AuthenticateAsync(request.Username, request.Password);

                if (!success)
                {
                    if (status == "suspended")
                    {
                        return new AuthLoginResponse
                        {
                            Status = AuthResponseStatus.AccountSuspended,
                            Message = "Account is suspended",
                            AccountId = accountId
                        };
                    }
                    else if (status == "banned")
                    {
                        return new AuthLoginResponse
                        {
                            Status = AuthResponseStatus.AccountBanned,
                            Message = "Account is banned",
                            AccountId = accountId
                        };
                    }
                    else
                    {
                        return new AuthLoginResponse
                        {
                            Status = AuthResponseStatus.InvalidCredentials,
                            Message = "Invalid credentials"
                        };
                    }
                }

                // Create session
                var (sessionCreated, sessionToken, expiresAt) = await _db.CreateSessionAsync(accountId, clientIp);

                if (!sessionCreated)
                {
                    return new AuthLoginResponse
                    {
                        Status = AuthResponseStatus.ServerError,
                        Message = "Failed to create session"
                    };
                }

                // Add to active sessions cache
                var sessionInfo = new SessionInfo
                {
                    AccountId = accountId,
                    SessionToken = sessionToken,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    LastActivity = DateTime.UtcNow
                };
                _activeSessions[sessionToken] = sessionInfo;

                ServerLogger.Info($"Login successful: {request.Username} (Account: {accountId}, IP: {clientIp})");

                return new AuthLoginResponse
                {
                    Status = AuthResponseStatus.Success,
                    Message = "Login successful",
                    AccountId = accountId,
                    SessionToken = sessionToken,
                    SessionExpiresAt = new DateTimeOffset(expiresAt).ToUnixTimeSeconds()
                };
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error during login: {ex.Message}");
                return new AuthLoginResponse
                {
                    Status = AuthResponseStatus.ServerError,
                    Message = "Login failed"
                };
            }
        }

        // ==================== Registration Handler ====================

        /// <summary>
        /// Handle registration request from client.
        /// </summary>
        public async Task<AuthRegisterResponse> HandleRegisterAsync(AuthRegisterRequest request, string clientIp)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return new AuthRegisterResponse
                    {
                        Status = AuthResponseStatus.InvalidCredentials,
                        Message = "All fields required"
                    };
                }

                // Validate password match
                if (request.Password != request.ConfirmPassword)
                {
                    return new AuthRegisterResponse
                    {
                        Status = AuthResponseStatus.InvalidCredentials,
                        Message = "Passwords do not match"
                    };
                }

                // Register account in database
                var (success, message, accountId) = await _db.RegisterAccountAsync(request.Username, request.Email, request.Password);

                if (!success)
                {
                    return new AuthRegisterResponse
                    {
                        Status = AuthResponseStatus.InvalidCredentials,
                        Message = message
                    };
                }

                ServerLogger.Info($"Registration successful: {request.Username} (Account: {accountId}, IP: {clientIp})");

                return new AuthRegisterResponse
                {
                    Status = AuthResponseStatus.Success,
                    Message = "Account created successfully",
                    AccountId = accountId
                };
            }
            catch (Exception ex)
            {
                ServerLogger.Error($"Error during registration: {ex.Message}");
                return new AuthRegisterResponse
                {
                    Status = AuthResponseStatus.ServerError,
                    Message = "Registration failed"
                };
            }
        }

        // ==================== Session Management ====================

        /// <summary>
        /// Validate a session token.
        /// </summary>
        public SessionInfo ValidateSession(string sessionToken)
        {
            if (string.IsNullOrEmpty(sessionToken))
                return null;

            // Check in-memory cache first
            if (_activeSessions.TryGetValue(sessionToken, out var sessionInfo))
            {
                if (sessionInfo.ExpiresAt > DateTime.UtcNow)
                {
                    sessionInfo.LastActivity = DateTime.UtcNow;
                    return sessionInfo;
                }
                else
                {
                    _activeSessions.Remove(sessionToken);
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Select a character for the session.
        /// </summary>
        public bool SelectCharacter(string sessionToken, int characterId, string characterName)
        {
            if (!_activeSessions.TryGetValue(sessionToken, out var sessionInfo))
                return false;

            sessionInfo.CharacterId = characterId;
            sessionInfo.LastActivity = DateTime.UtcNow;

            ServerLogger.Info($"Character selected: {characterName} (Session: {sessionToken}, Character: {characterId})");
            return true;
        }

        /// <summary>
        /// Create a new character for an account.
        /// </summary>
        public async Task<(bool success, string message)> CreateCharacterAsync(string sessionToken, string characterName, string faction)
        {
            var sessionInfo = ValidateSession(sessionToken);
            if (sessionInfo == null)
                return (false, "Invalid session");

            var (success, message, characterId) = await _db.CreateCharacterAsync(sessionInfo.AccountId, characterName, faction);
            return (success, message);
        }

        /// <summary>
        /// End a session (logout).
        /// </summary>
        public async Task<bool> EndSessionAsync(string sessionToken)
        {
            _activeSessions.Remove(sessionToken);
            return await _db.EndSessionAsync(sessionToken);
        }

        /// <summary>
        /// Get active sessions count.
        /// </summary>
        public Dictionary<string, SessionInfo> GetActiveSessions()
        {
            return _activeSessions;
        }

        /// <summary>
        /// Cleanup expired sessions (run periodically).
        /// </summary>
        public void CleanupExpiredSessions()
        {
            var expiredTokens = _activeSessions
                .Where(x => x.Value.ExpiresAt < DateTime.UtcNow)
                .Select(x => x.Key)
                .ToList();

            foreach (var token in expiredTokens)
            {
                _activeSessions.Remove(token);
                ServerLogger.Debug($"Cleaned up expired session: {token}");
            }
        }
    }
}
