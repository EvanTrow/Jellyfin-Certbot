using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace JellyfinCertbot
{
    public class Helpers
    {

        private static readonly Regex DomainRegex = new Regex(@"^(?!-)([A-Za-z0-9-]{1,63}\.)+[A-Za-z]{2,63}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Validates a domain string. Throws ArgumentException if invalid.
        public static void ValidateDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                throw new ArgumentException("Domain cannot be empty.");

            domain = domain.Trim();

            if (!DomainRegex.IsMatch(domain))
                throw new ArgumentException($"Invalid domain: {domain}");
        }

        private static readonly Regex EmailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        // Validates an email string. Throws ArgumentException if invalid.
        public static void ValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty.");

            email = email.Trim();

            if (!EmailRegex.IsMatch(email))
                throw new ArgumentException($"Invalid email: {email}");
        }


        // Extract top-level domain for Cloudflare zone lookup
        public static string GetTopLevelDomain(string domain)
        {
            var parts = domain.Split('.');
            if (parts.Length < 2)
                throw new ArgumentException("Invalid domain format.", nameof(domain));
            // Handles domains like "sub.example.com" -> "example.com"
            // For deeper subdomains, always take last two parts
            return string.Join('.', parts.Skip(parts.Length - 2));
        }

        // Generate a random password of specified length
        private const string AllowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        public static string GeneratePassword(int length)
        {
            if (length <= 0)
                throw new ArgumentException("Password length must be greater than zero.", nameof(length));

            var password = new StringBuilder(length);

            // Use a cryptographically strong random number generator
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] randomBytes = new byte[4];

                for (int i = 0; i < length; i++)
                {
                    rng.GetBytes(randomBytes);
                    int index = BitConverter.ToInt32(randomBytes, 0) & int.MaxValue;
                    index %= AllowedChars.Length;

                    password.Append(AllowedChars[index]);
                }
            }

            return password.ToString();
        }
    }
}
