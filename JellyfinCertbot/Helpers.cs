using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace JellyfinCertbot
{
    /// <summary>
    /// Validation and utility helpers.
    /// </summary>
    public static partial class Helpers
    {
        private const string AllowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        /// <summary>
        /// Validates a domain name.
        /// </summary>
        /// <param name="domain">The domain to validate.</param>
        /// <exception cref="ArgumentException">The domain is empty or invalid.</exception>
        public static void ValidateDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                throw new ArgumentException("Domain cannot be empty.", nameof(domain));
            }

            if (!DomainRegex().IsMatch(domain.Trim()))
            {
                throw new ArgumentException($"Invalid domain: {domain}", nameof(domain));
            }
        }

        /// <summary>
        /// Validates an email address.
        /// </summary>
        /// <param name="email">The email address to validate.</param>
        /// <exception cref="ArgumentException">The email is empty or invalid.</exception>
        public static void ValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email cannot be empty.", nameof(email));
            }

            if (!EmailRegex().IsMatch(email.Trim()))
            {
                throw new ArgumentException($"Invalid email: {email}", nameof(email));
            }
        }

        /// <summary>
        /// Gets the registrable domain (last two labels) used for the DNS zone lookup,
        /// e.g. <c>media.example.com</c> becomes <c>example.com</c>.
        /// </summary>
        /// <param name="domain">The full domain name.</param>
        /// <returns>The last two labels of the domain.</returns>
        public static string GetTopLevelDomain(string domain)
        {
            var parts = domain.Split('.');
            if (parts.Length < 2)
            {
                throw new ArgumentException("Invalid domain format.", nameof(domain));
            }

            return string.Join('.', parts.Skip(parts.Length - 2));
        }

        /// <summary>
        /// Generates a cryptographically random alphanumeric password.
        /// </summary>
        /// <param name="length">The password length.</param>
        /// <returns>The generated password.</returns>
        public static string GeneratePassword(int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
            return RandomNumberGenerator.GetString(AllowedChars, length);
        }

        [GeneratedRegex(@"^(?!-)([A-Za-z0-9-]{1,63}\.)+[A-Za-z]{2,63}$", RegexOptions.IgnoreCase)]
        private static partial Regex DomainRegex();

        [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase)]
        private static partial Regex EmailRegex();
    }
}
