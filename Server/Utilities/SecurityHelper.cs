using System;
using System.Crypto;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace USRP.Server.Utilities
{
    /// <summary>
    /// Password hashing using PBKDF2 (NIST-approved algorithm).
    /// </summary>
    public static class PasswordHasher
    {
        private const int HASH_ITERATIONS = 10000;
        private const int HASH_SIZE = 32; // 256 bits
        private const int SALT_SIZE = 32; // 256 bits

        /// <summary>
        /// Hash a password with a random salt.
        /// </summary>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be empty");

            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] salt = new byte[SALT_SIZE];
                rng.GetBytes(salt);

                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, HASH_ITERATIONS, HashAlgorithmName.SHA256))
                {
                    byte[] hash = pbkdf2.GetBytes(HASH_SIZE);

                    // Combine salt and hash: salt + hash
                    byte[] result = new byte[SALT_SIZE + HASH_SIZE];
                    Array.Copy(salt, 0, result, 0, SALT_SIZE);
                    Array.Copy(hash, 0, result, SALT_SIZE, HASH_SIZE);

                    return Convert.ToBase64String(result);
                }
            }
        }

        /// <summary>
        /// Verify a password against a hash.
        /// </summary>
        public static bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
                return false;

            try
            {
                byte[] hashBytes = Convert.FromBase64String(hash);
                byte[] salt = new byte[SALT_SIZE];
                Array.Copy(hashBytes, 0, salt, 0, SALT_SIZE);

                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, HASH_ITERATIONS, HashAlgorithmName.SHA256))
                {
                    byte[] computedHash = pbkdf2.GetBytes(HASH_SIZE);

                    // Compare hashes in constant time to prevent timing attacks
                    return ConstantTimeEquals(computedHash, 0, hashBytes, SALT_SIZE, HASH_SIZE);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Constant-time comparison to prevent timing attacks.
        /// </summary>
        private static bool ConstantTimeEquals(byte[] a, int aOffset, byte[] b, int bOffset, int length)
        {
            int result = 0;
            for (int i = 0; i < length; i++)
            {
                result |= a[aOffset + i] ^ b[bOffset + i];
            }
            return result == 0;
        }
    }

    /// <summary>
    /// Session token generation and validation.
    /// </summary>
    public static class TokenGenerator
    {
        private const int TOKEN_SIZE = 32; // 256 bits

        /// <summary>
        /// Generate a cryptographically secure random token.
        /// </summary>
        public static string GenerateToken()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] tokenBytes = new byte[TOKEN_SIZE];
                rng.GetBytes(tokenBytes);
                return Convert.ToBase64String(tokenBytes);
            }
        }
    }

    /// <summary>
    /// Validation helpers for user input.
    /// </summary>
    public static class ValidationHelper
    {
        /// <summary>
        /// Validate username format.
        /// </summary>
        public static bool IsValidUsername(string username)
        {
            if (string.IsNullOrEmpty(username))
                return false;

            // 3-32 alphanumeric + underscore
            return Regex.IsMatch(username, @"^[a-zA-Z0-9_]{3,32}$");
        }

        /// <summary>
        /// Validate email format.
        /// </summary>
        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validate password strength.
        /// </summary>
        public static bool IsValidPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return false;

            // At least 6 characters, contains letters and numbers
            return password.Length >= 6 && Regex.IsMatch(password, @"[a-zA-Z]") && Regex.IsMatch(password, @"[0-9]");
        }

        /// <summary>
        /// Validate character name format.
        /// </summary>
        public static bool IsValidCharacterName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            // 3-32 characters, letters and spaces only
            return Regex.IsMatch(name, @"^[a-zA-Z ]{3,32}$");
        }
    }

    /// <summary>
    /// Multi-level logging system.
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }

    public static class ServerLogger
    {
        private static LogLevel _currentLevel = LogLevel.Debug;

        public static void SetLogLevel(LogLevel level) => _currentLevel = level;

        public static void Debug(string message) => Log(LogLevel.Debug, "DEBUG", message);
        public static void Info(string message) => Log(LogLevel.Info, "INFO", message);
        public static void Warning(string message) => Log(LogLevel.Warning, "WARNING", message);
        public static void Error(string message) => Log(LogLevel.Error, "ERROR", message);
        public static void Critical(string message) => Log(LogLevel.Critical, "CRITICAL", message);

        private static void Log(LogLevel level, string levelName, string message)
        {
            if (level < _currentLevel)
                return;

            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            string logEntry = $"[{timestamp}] [{levelName}] {message}";

            switch (level)
            {
                case LogLevel.Critical:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogLevel.Error:
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    break;
                case LogLevel.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogLevel.Info:
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
            }

            Console.WriteLine(logEntry);
            Console.ResetColor();
        }
    }
}
