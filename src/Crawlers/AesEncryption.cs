using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace Crawlers
{

    /// <summary>
    /// Provides methods for AES encryption and decryption of strings.
    /// Uses a password-based key derivation function (PBKDF2) for key generation
    /// and generates a random Initialization Vector (IV) for each encryption.
    /// </summary>
    public class AesEncryption
    {
        public const string AesPrePost = "----\n";
        public static string DataAesKey = "";
        private const int IvLengthBytes = 12; // Standard GCM IV length
        private const int GcmTagLength = 16; // Standard GCM Tag length in bytes (128 bits)
        private const int MaxStringLength = 1024 * 1024; // 1MB, Example max string length for safety
        private const int InitialSize = 4096;

        public string Encrypt(string plainText)
        {
            if (DataAesKey.Length == 0)
                throw new InvalidOperationException("AES Key is not set");
            return Encrypt(plainText, DataAesKey);
        }

        public string Decrypt(string plainText)
        {
            if (DataAesKey.Length == 0)
                throw new InvalidOperationException("AES Key is not set");
            return Decrypt(plainText, DataAesKey);
        }

        /// <summary>
        /// Encrypts a plain text string with a password using AES/GCM and returns the encrypted hexadecimal string.
        /// The output format includes IV, Ciphertext, Tag, and custom line breaks/hyphens.
        /// </summary>
        /// <param name="str">The plain text data to encrypt.</param>
        /// <param name="password">The password for key derivation.</param>
        /// <returns>The encrypted hexadecimal string, formatted with prefix/postfix and separators.</returns>
        /// <exception cref="ArgumentException">Thrown if password is empty.</exception>
        /// <exception cref="Exception">Thrown on encryption failure.</exception>
        public static string Encrypt(string str, string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Invalid Password (empty)");
            }

            try
            {
                // 1. Key Derivation and Cipher Setup
                var passwordHash = PasswordToHash(password); // Assumed to be SHA256 (32 bytes)
                var cipher = new AesGcm(passwordHash, GcmTagLength);

                var iv = CreateIv(); // Generate a unique IV for this encryption

                // 2. Prepare Plaintext Data for Encryption (using custom binary format)
                var plainTextBytes = Encoding.UTF8.GetBytes(str); // Convert string to bytes
                using (BinarySerializer binarySerializer = new BinarySerializer(InitialSize))
                {
                    // This method encapsulates writing length, data, and 8-byte padding
                    // It returns the entire prepared byte array to be encrypted.
                    plainTextBytes = binarySerializer.WriteLengthPrefixedAndAligned(plainTextBytes);
                }
                // Now plainTextBytes contains: [4-byte length] + [UTF8 string data] + [padding to 8-byte multiple]


                // 3. Perform Encryption
                byte[] cipherText = new byte[plainTextBytes.Length]; // Ciphertext will be same length as plaintext input for GCM (if no padding is added by crypto)
                byte[] tag = new byte[GcmTagLength];       // GCM authentication tag (16 bytes)

                // The Encrypt method takes IV, plaintext, and outputs ciphertext and tag.
                // It does NOT add padding to plaintext. Our BinarySerializer does custom padding.
                cipher.Encrypt(iv, plainTextBytes, cipherText, tag);

                // 4. Combine IV, Ciphertext, and Tag for transmission
                // The Kotlin code implies: iv + encBytesPreIV (which is ciphertext + tag)
                byte[] encBytesCombined = new byte[iv.Length + cipherText.Length + tag.Length];
                Buffer.BlockCopy(iv, 0, encBytesCombined, 0, iv.Length);
                Buffer.BlockCopy(cipherText, 0, encBytesCombined, iv.Length, cipherText.Length);
                Buffer.BlockCopy(tag, 0, encBytesCombined, iv.Length + cipherText.Length, tag.Length);

                // 5. Convert combined bytes to Hexadecimal string
                string hexStr = BytesToHex(encBytesCombined); // Reusing the helper from Decrypt side

                // 6. Apply Custom String Formatting (hyphens and newlines)
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hexStr.Length; i++)
                {
                    // Add separators based on index, but skip before the very first character
                    if (i > 0 && i % 8 == 0) // Add a separator every 8 hex characters (4 bytes)
                    {
                        if (i % 32 == 0) // Add a newline every 32 hex characters (16 bytes)
                            sb.Append("\n");
                        else
                            sb.Append("-"); // Add a hyphen for other 8-char breaks
                    }
                    sb.Append(hexStr[i]);
                }

                // 7. Add Prefix and Postfix
                // Kotlin: "$AES_PRE_POST${sb.toString().trim()}\n$AES_PRE_POST"
                return $"{AesPrePost}{sb.ToString().Trim()}\n{AesPrePost}";
            }
            catch (CryptographicException ex)
            {
                throw new Exception($"Security algorithm failed: Cryptographic error - {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Security algorithm failed: {ex.Message}", ex);
            }
        }

    
        /// <summary>
        /// Decrypts a Base64 encoded string that was encrypted using AES-256.
        /// The input string is expected to have the IV prepended to the ciphertext.
        /// </summary>
        /// <param name="cipherTextWithIvBase64">The Base64 encoded string of the IV + Ciphertext.</param>
        /// <param name="password">The password used to derive the decryption key.</param>
        /// <returns>The original plaintext string.</returns>
        /// <exception cref="ArgumentNullException">Thrown if cipherTextWithIvBase64 or password is null or empty.</exception>
        /// <exception cref="FormatException">Thrown if the input string is not a valid Base64 string or has insufficient length for IV.</exception>
        /// <exception cref="CryptographicException">Thrown if decryption fails (e.g., incorrect password/key).</exception>
        public static string Decrypt(string cipherTextWithIvBase64, string password)
        {
            if (string.IsNullOrEmpty(cipherTextWithIvBase64))
                throw new ArgumentNullException(nameof(cipherTextWithIvBase64), "Cipher text cannot be null or empty.");
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password), "Password cannot be null or empty.");

            // do we have escaped \n ?
            var finalStr = cipherTextWithIvBase64;
            if (cipherTextWithIvBase64.Contains("\\n"))
            {
                finalStr = cipherTextWithIvBase64.Replace("\\n", "\n");
            }
            // not escaped properly?
            if (!finalStr.StartsWith(AesPrePost))
            {
                return cipherTextWithIvBase64;
            }

            // clean up the input string - only take hex characters
            var cleanEncryptedHexStr = new string(finalStr.ToLower().Where(char.IsLetterOrDigit).ToArray());

            try
            {
                var passwordHash = PasswordToHash(password);
                var cipher = new AesGcm(passwordHash, GcmTagLength);

                // Decode the hex string to bytes (IV + Encrypted Data)
                var ivAndEncryptedBytes = DecodeHex(cleanEncryptedHexStr);

                // Read IV
                if (ivAndEncryptedBytes.Length < IvLengthBytes)
                {
                    throw new Exception("Security algorithm failed: IV length wrong.");
                }
                byte[] iv = new byte[IvLengthBytes];
                Buffer.BlockCopy(ivAndEncryptedBytes, 0, iv, 0, IvLengthBytes);

                // Read Encrypted Data (remaining bytes after IV)
                byte[] encBytes = new byte[ivAndEncryptedBytes.Length - IvLengthBytes];
                Buffer.BlockCopy(ivAndEncryptedBytes, IvLengthBytes, encBytes, 0, encBytes.Length);

                // Decrypt
                byte[] tag = new byte[GcmTagLength]; // GCM authentication tag

                // In GCM, the tag is typically appended to the ciphertext.
                // The Kotlin code suggests tag is part of encBytes, so we need to separate.
                // Let's assume encBytes contains ciphertext + tag.
                // (Kotlin's ByteBuffer.remaining() after getting IV implicitly leaves ciphertext + tag)
                // C# AesGcm.Decrypt requires tag separately.

                if (encBytes.Length < GcmTagLength)
                {
                    throw new Exception("Security algorithm failed: Encrypted data too short to contain GCM tag.");
                }

                // Separate ciphertext and tag from encBytes
                byte[] cipherTextOnly = new byte[encBytes.Length - GcmTagLength];
                byte[] decryptedBytes = new byte[cipherTextOnly.Length]; // Decrypted output buffer
                Buffer.BlockCopy(encBytes, 0, cipherTextOnly, 0, cipherTextOnly.Length);
                Buffer.BlockCopy(encBytes, cipherTextOnly.Length, tag, 0, GcmTagLength);

                cipher.Decrypt(iv, cipherTextOnly, tag, decryptedBytes);

                // Post-decryption processing based on Kotlin's BinarySerializer logic
                // The decryptedBytes are expected to contain: [4-byte length] + [data]
                using (var binarySerializer = new BinarySerializer(decryptedBytes))
                {
                    if (binarySerializer.Size < 4) // Must have at least length bytes
                    {
                        throw new Exception("Security algorithm failed: Decrypted data too short to read string length.");
                    }

                    int size = binarySerializer.ReadInt(); // Read the string length
                    if (size < 0 || size > MaxStringLength || size > binarySerializer.Size - 4)
                    {
                        throw new Exception($"Security algorithm failed: invalid string length {size}");
                    }

                    byte[] data = binarySerializer.ReadByteArray(); // Read the actual string bytes
                    if (data.Length > 0)
                    {
                        // Ensure the length matches what was read from the header
                        if (data.Length != size)
                        {
                            throw new Exception($"Security algorithm failed: Read data length {data.Length} does not match header length {size}.");
                        }
                        return Encoding.UTF8.GetString(data);
                    }
                    else
                    {
                        // If data is null or empty but size > 0, it's an error in custom format or empty string
                        return ""; // if data is empty or null
                    }
                }
            }
            catch (CryptographicException ex)
            {
                // Catch specific crypto exceptions for more detail
                throw new Exception($"Security algorithm failed: Cryptographic error - {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                // Throw a prettier exception, wrapping the original
                throw new Exception($"Security algorithm failed: {ex.Message}", ex);
            }
            
        }


        /// <summary>
        /// Generates a cryptographically secure Initialization Vector (IV).
        /// </summary>
        /// <returns>A byte array of the standard IV length for GCM (12 bytes).</returns>
        private static byte[] CreateIv()
        {
            var iv = new byte[IvLengthBytes]; // Constants.IvLengthBytes should be 12
            RandomNumberGenerator.Fill(iv); // Fills the IV with cryptographically strong random bytes
            return iv;
        }
        
        
        // Helper to convert byte array to hex string
        private static string BytesToHex(byte[] bytes)
        {
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
        
        /// <summary>
        /// Converts a password string to a 32-byte hashed array using SHA-512,
        /// then folding the 64-byte result into 32 bytes by XORing the halves.
        /// This mimics the behavior of the provided Kotlin code.
        /// </summary>
        /// <param name="password">The password string to hash.</param>
        /// <returns>A 32-byte array representing the folded hash of the password.</returns>
        private static byte[] PasswordToHash(string? password)
        {
            // Handle null password input gracefully.
            // The Kotlin code would likely throw a NullPointerException if 'password' was null
            // before .toByteArray(). We'll treat null as an empty string for hashing.
            password ??= string.Empty;

            // 1. Get the UTF-8 bytes of the password.
            // This is equivalent to Kotlin's password.toByteArray(charset("UTF-8"))
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

            // 2. Compute the SHA-512 hash.
            // Using 'using' statement ensures the SHA512 object is properly disposed.
            // SHA512.Create() is the standard way to get an SHA512 instance in C#.
            // For .NET 5+ you could also use SHA512.HashData(passwordBytes) for a simpler one-liner.
            byte[] sha512Hash;
            using (SHA512 sha512 = SHA512.Create())
            {
                // ComputeHash takes the input byte array and returns the hash byte array.
                // This is equivalent to Kotlin's sha512.update(...) and sha512.digest()
                sha512Hash = sha512.ComputeHash(passwordBytes);
            }

            // SHA-512 always produces a 64-byte (512-bit) hash.
            // The Kotlin code's 'if (data.size != 32)' condition will always be true
            // because SHA-512 output is 64 bytes, not 32.
            // So, we will always proceed with the folding logic.

            // 3. Fold the 64-byte hash into a 32-byte array by XORing the two halves.
            byte[] foldedHash = new byte[32];

            // Iterate through the first 32 bytes of the SHA-512 hash
            for (int i = 0; i < 32; i++)
            {
                // XOR the byte from the first half with the corresponding byte from the second half.
                // sha512Hash[i] is the first half (bytes 0-31)
                // sha512Hash[i + 32] is the second half (bytes 32-63)
                foldedHash[i] = (byte)(sha512Hash[i] ^ sha512Hash[i + 32]);
            }

            return foldedHash;
        }
    
        /// <summary>
        /// Converts a hexadecimal string to a byte array.
        /// (Uses Convert.FromHexString which is .NET 5+).
        /// </summary>
        /// <param name="hex">The hexadecimal string.</param>
        /// <returns>The byte array.</returns>
        private static byte[] DecodeHex(string hex)
        {
            if (hex.Length % 2 != 0)
            {
                throw new ArgumentException("Hex string must have an even number of characters.");
            }
            return Convert.FromHexString(hex);
        }
        
        /// <summary>
        /// Decodes a hexadecimal string back into its original string representation.
        /// </summary>
        /// <param name="hexString">The hexadecimal string to decode.</param>
        /// <returns>The decoded string.</returns>
        /// <exception cref="ArgumentException">Thrown if the input string is not a valid hexadecimal string.</exception>
        private static string DecodeHexString(string hexString)
        {
            // Check for null or empty input
            if (string.IsNullOrEmpty(hexString))
            {
                return string.Empty;
            }

            // Ensure the string has an even number of characters, as each byte is represented by two hex characters.
            if (hexString.Length % 2 != 0)
            {
                throw new ArgumentException("Hexadecimal string must have an even number of characters.",
                    nameof(hexString));
            }

            try
            {
                // Convert the hexadecimal string to a byte array.
                // Convert.FromHexString is available in .NET 6 and later.
                // For older .NET versions, you would need to parse it manually or use a third-party library.
                byte[] bytes = Convert.FromHexString(hexString);

                // Convert the byte array back to a string using UTF-8 encoding.
                // UTF-8 is a common and widely compatible encoding.
                return Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException ex)
            {
                // Catch specific FormatException if the string contains invalid hex characters.
                throw new ArgumentException("Input string is not a valid hexadecimal format.", nameof(hexString), ex);
            }
            catch (Exception ex)
            {
                // Catch any other unexpected exceptions.
                throw new Exception($"An error occurred during hexadecimal decoding: {ex.Message}", ex);
            }
        }


    }
    
}

namespace Crawlers.Test
{
    [TestFixture]
    public class AesEncryptionTest
    {
        [Test]
        public void DecryptTest()
        {
            var aes = new AesEncryption();
            var str = "Password1";
            var encryptedString = aes.Encrypt(str);
            var decryptedString = aes.Decrypt(encryptedString);
            Assert.Equals(str, decryptedString);
        }
        
    }

}
