namespace Crawlers;

using System;
using System.Security.Cryptography;
using System.Text;

public static class Md5Hasher
{
    /// <summary>
    /// Computes the MD5 hash of multiple strings and/or binary data combined.
    /// The order of inputs matters for the hash result.
    /// </summary>
    /// <param name="parts">An array of objects (strings or byte arrays) to hash.</param>
    /// <returns>The MD5 hash as a hexadecimal string.</returns>
    public static string ComputeCombinedHash(params object[] parts)
    {
        using var md5 = MD5.Create();
        using var ms = new MemoryStream();
        foreach (var part in parts)
        {
            switch (part)
            {
                case string s:
                {
                    var stringBytes = Encoding.UTF8.GetBytes(s);
                    ms.Write(stringBytes, 0, stringBytes.Length);
                    break;
                }
                case byte[] b:
                    ms.Write(b, 0, b.Length);
                    break;
                default:
                    throw new ArgumentException("Unsupported data type for hashing. Only string and byte[] are supported.");
            }
        }

        // Reset stream position to the beginning before computing hash
        ms.Position = 0;
        var hashBytes = md5.ComputeHash(ms);
        return BytesToHexString(hashBytes);
    }    
    
    /// <summary>
    /// Converts a byte array hash to a hexadecimal string.
    /// </summary>
    /// <param name="hashBytes">The hash as a byte array.</param>
    /// <returns>The hexadecimal string representation of the hash.</returns>
    private static string BytesToHexString(byte[] hashBytes)
    {
        var builder = new StringBuilder();
        foreach (var t in hashBytes)
        {
            builder.Append(t.ToString("x2")); // "x2" formats as two lowercase hexadecimal digits
        }
        return builder.ToString();
    }

}
