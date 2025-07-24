using System.Security.Cryptography;
using System.Text;

namespace Crawlers;

/// <summary>
/// C# equivalent of the Sha512 Kotlin object.
/// This class computes a salted SHA512 hash.
/// </summary>
public static class Sha512
{
    private const string Pepper = "0ca5784d-1925-4833-baf6-8af2dcf83467";

    /// <summary>
    /// Generates a proper SHA512 hash with a salt and pepper.
    /// </summary>
    /// <param name="aes">The AES system key</param>
    /// <param name="password">The input string to hash.</param>
    /// <returns>A 128-character lowercase SHA512 hex string.</returns>
    public static string GenerateSha512Hash(string aes, string password)
    {
        // 1. Concatenate salt, password, and pepper.
        var saltedString = $"{aes}:{password}:{Pepper}";

        // 2. Use the .NET cryptography classes to compute the hash.
        using (var sha512 = SHA512.Create())
        {
            // Convert the string to a byte array (using UTF-8).
            var stringBytes = Encoding.UTF8.GetBytes(saltedString);

            // Compute the hash.
            var hashBytes = sha512.ComputeHash(stringBytes);

            // 3. Convert the resulting byte array to a lowercase hex string.
            // This is the modern and recommended approach in .NET 5+.
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
