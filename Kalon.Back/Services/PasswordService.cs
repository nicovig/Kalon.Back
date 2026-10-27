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
        string GenerateSalt();
        string GeneratePassword(int length = 12);
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

        public string GenerateSalt()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        }

        public string GeneratePassword(int length = 12)
        {
            if (length < 4)
                throw new ArgumentOutOfRangeException(nameof(length), "Password length must be at least 4.");

            const string lower = "abcdefghjkmnpqrstuvwxyz";
            const string upper = "ABCDEFGHJKMNPQRSTUVWXYZ";
            const string digits = "23456789";
            const string special = "!@#*-_";
            var all = lower + upper + digits + special;

            var bytes = new byte[length];
            RandomNumberGenerator.Fill(bytes);

            var chars = new char[length];
            chars[0] = upper[bytes[0] % upper.Length];
            chars[1] = digits[bytes[1] % digits.Length];
            chars[2] = special[bytes[2] % special.Length];
            for (var i = 3; i < length; i++)
                chars[i] = all[bytes[i] % all.Length];

            for (var i = length - 1; i > 0; i--)
            {
                var j = bytes[i] % (i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars);
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
