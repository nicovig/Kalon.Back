using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Services
{
    public class PasswordOptions
    {
        public string Pepper { get; set; } = null!;
        public int Iterations { get; set; } = 120000;
        public int HashSize { get; set; } = 32;
    }

    public interface IPasswordService
    {
        string HashPassword(string password, string salt);
        bool VerifyPassword(string password, string passwordHash, string salt);
    }

    public class PasswordService : IPasswordService
    {
        private readonly PasswordOptions _options;

        public PasswordService(IOptions<PasswordOptions> options)
        {
            _options = options.Value;
        }

        public string HashPassword(string password, string salt)
        {
            password = password.Trim();
            salt = salt.Trim();

            var saltBytes = BuildSaltBytes(salt);
            var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, _options.Iterations, HashAlgorithmName.SHA256, _options.HashSize);
            return Convert.ToBase64String(hashBytes);
        }

        public bool VerifyPassword(string password, string passwordHash, string salt)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
            {
                return false;
            }

            password = password.Trim();
            passwordHash = passwordHash.Trim();

            if (passwordHash.StartsWith("$2", StringComparison.Ordinal))
            {
                return BCrypt.Net.BCrypt.Verify(password, passwordHash);
            }

            if (string.IsNullOrWhiteSpace(salt) || string.IsNullOrWhiteSpace(_options.Pepper))
            {
                return false;
            }

            salt = salt.Trim();

            byte[] storedHash;
            try
            {
                storedHash = Convert.FromBase64String(passwordHash);
            }
            catch (FormatException)
            {
                return false;
            }

            var saltBytes = BuildSaltBytes(salt);

            var computedHash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, _options.Iterations, HashAlgorithmName.SHA256, storedHash.Length);
            return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
        }

        private byte[] BuildSaltBytes(string salt)
        {
            return Encoding.UTF8.GetBytes($"{salt}:{_options.Pepper}");
        }
    }
}
