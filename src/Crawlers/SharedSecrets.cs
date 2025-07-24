using System.Buffers.Binary;

namespace Crawlers;

public static class SharedSecrets
{

    private static byte[] _sharedSecrets = [];
    private const ulong XorMask = 0xffcdcdcdffcdcdcd;
    private const int SizeInKb = 1024;


    /// <summary>
    /// Gets one of the shared secrets in the form of a shared random Guid at an offset
    /// in the shared-secret set.
    /// </summary>
    /// <param name="aes">The AES key for generating data from.</param>
    /// <param name="offset">A random integer of any value from int.MinValue to int.MaxValue.</param>
    /// <returns>A deterministically generated Guid.</returns>
    public static Guid GetRandomGuid(string aes, int offset)
    {
        if (_sharedSecrets.Length == 0)
        {
            GenerateData(Guid.Parse(aes), SizeInKb);
        }
        
        // Take the lowest bit as a reversal indicator.
        var reversed = offset & 1;
        // Take the second bit as an XOR mask indicator.
        var operation = offset & 2;

        // Note: Casting to long before Abs prevents overflow if offset is int.MinValue.
        // We right-shift by 2 to discard the flag bits, get the absolute value,
        // and use the modulo operator to ensure the offset is within the array bounds.
        var tempOffset = Math.Abs((long)offset >> 2);
        var byteOffset = (int)(tempOffset % _sharedSecrets.Length);

        // Ensure we can safely read 16 bytes from the calculated offset.
        // If not, adjust it to the last possible valid position.
        if (byteOffset + 16 >= _sharedSecrets.Length)
        {
            byteOffset = _sharedSecrets.Length - 17;
        }

        long l1; // Most significant 64 bits of the Guid.
        long l2; // Least significant 64 bits of the Guid.

        // The 'reversed' flag swaps the order of the two longs.
        if (reversed == 0)
        {
            // Read the two longs in standard order (big-endian).
            l1 = BinaryPrimitives.ReadInt64BigEndian(_sharedSecrets.AsSpan(byteOffset));
            l2 = BinaryPrimitives.ReadInt64BigEndian(_sharedSecrets.AsSpan(byteOffset + 8));
        }
        else
        {
            // Read the two longs in swapped order. The second 8-byte chunk becomes
            // the most significant bits (l1).
            l1 = ReadLongInReverse(_sharedSecrets, byteOffset + 8);
            l2 = ReadLongInReverse(_sharedSecrets, byteOffset + 16);
        }

        // Apply the XOR operation if the flag is set.
        if (operation != 0)
        {
            l1 ^= unchecked((long)XorMask);
            l2 ^= unchecked((long)XorMask);
        }

        // A Java UUID(l1, l2) is composed of the big-endian bytes of l1 followed
        // by the big-endian bytes of l2. We replicate that to create the Guid.
        Span<byte> javaBytes = stackalloc byte[16];
        Span<byte> dotnetBytes = stackalloc byte[16];

        if (reversed == 0)
        {
            BinaryPrimitives.WriteInt64BigEndian(javaBytes, l1);
            BinaryPrimitives.WriteInt64BigEndian(javaBytes.Slice(8), l2);
        }
        else
        {
            BinaryPrimitives.WriteInt64BigEndian(javaBytes, l2);
            BinaryPrimitives.WriteInt64BigEndian(javaBytes.Slice(8), l1);

            dotnetBytes[0] = javaBytes[3];
            dotnetBytes[1] = javaBytes[2];
            dotnetBytes[2] = javaBytes[1]; //
            dotnetBytes[3] = javaBytes[0]; //
            dotnetBytes[4] = javaBytes[5];
            dotnetBytes[5] = javaBytes[4];
            dotnetBytes[6] = javaBytes[7];
            dotnetBytes[7] = javaBytes[6];
            
            dotnetBytes[8] = javaBytes[8];
            dotnetBytes[9] = javaBytes[9];
            dotnetBytes[10] = javaBytes[10];
            dotnetBytes[11] = javaBytes[11];
            dotnetBytes[12] = javaBytes[12];
            dotnetBytes[13] = javaBytes[13];
            dotnetBytes[14] = javaBytes[14];
            dotnetBytes[15] = javaBytes[15];

            return new Guid(dotnetBytes);
        }

        
        dotnetBytes[0] = javaBytes[4];
        dotnetBytes[1] = javaBytes[5];
        dotnetBytes[2] = javaBytes[6]; //
        dotnetBytes[3] = javaBytes[7]; //
        dotnetBytes[4] = javaBytes[2];
        dotnetBytes[5] = javaBytes[3];
        dotnetBytes[6] = javaBytes[0];
        dotnetBytes[7] = javaBytes[1];

        dotnetBytes[8] = javaBytes[15];
        dotnetBytes[9] = javaBytes[14];
        dotnetBytes[10] = javaBytes[13];
        dotnetBytes[11] = javaBytes[12];
        dotnetBytes[12] = javaBytes[11];
        dotnetBytes[13] = javaBytes[10];
        dotnetBytes[14] = javaBytes[9];
        dotnetBytes[15] = javaBytes[8];

        return new Guid(dotnetBytes);
    }
        
        
    private static long ReadLongInReverse(byte[] data, int offset) {
        return ((long)data[offset] & 0xff) +
               ((long)(data[offset - 1] & 0xff) << 8) +
               ((long)(data[offset - 2] & 0xff) << 16) +
               ((long)(data[offset - 3] & 0xff) << 24) +
               ((long)(data[offset - 4] & 0xff) << 32) +
               ((long)(data[offset - 5] & 0xff) << 40) +
               ((long)(data[offset - 6] & 0xff) << 48) +
               ((long)(data[offset - 7] & 0xff) << 56);
    }

    
    /// <summary>
    /// Generates a consistent block of pseudo-random binary data from a seed Guid.
    /// </summary>
    /// <param name="seedGuid">The Guid to use as a seed.</param>
    /// <param name="sizeInKb">The desired size of the data in kilobytes.</param>
    /// <returns>A byte array containing the random data.</returns>
    private static void GenerateData(Guid seedGuid, int sizeInKb)
    {
        // 1. Define the size in bytes (1 KB = 1024 bytes).
        var dataSize = sizeInKb * 1024;

        byte[] guidBytes = seedGuid.ToByteArray();
        Span<byte> mostSignificantBytes = stackalloc byte[8];
        // --- Manually reorder the first 8 bytes to match Java's big-endian layout ---
        mostSignificantBytes[0] = guidBytes[3];
        mostSignificantBytes[1] = guidBytes[2];
        mostSignificantBytes[2] = guidBytes[1];
        mostSignificantBytes[3] = guidBytes[0];
        mostSignificantBytes[4] = guidBytes[5];
        mostSignificantBytes[5] = guidBytes[4];
        mostSignificantBytes[6] = guidBytes[7];
        mostSignificantBytes[7] = guidBytes[6];

        var mostSignificantBits = BinaryPrimitives.ReadInt64BigEndian(mostSignificantBytes);
        var leastSignificantBits = BinaryPrimitives.ReadInt64BigEndian(guidBytes.AsSpan(8));

        var seed = mostSignificantBits ^ leastSignificantBits;        
        // 3. Create a Random instance with the derived seed.
        var random = new PseudoRandomGenerator(seed);

        // 4. Create a byte array and fill it with pseudo-random bytes.
        var data = new byte[dataSize];
        random.FillBytes(data);
        _sharedSecrets = data;
    }

    
}

