namespace Crawlers;

using System;

/// <summary>
/// A pseudo-random number generator that is compatible with java.util.Random.
/// It uses a 48-bit seed and can be initialized with a 64-bit long.
/// </summary>
public class PseudoRandomGenerator
{
    private const long _multiplier = 0x5DEECE66DL;
    private const long _addend = 0xBL;
    private const long _mask = (1L << 48) - 1;
    private long _seed;

    /// <summary>
    /// Initializes the generator with a 64-bit seed.
    /// </summary>
    /// <param name="seed">The seed to initialize the generator.</param>
    public PseudoRandomGenerator(long seed)
    {
        _seed = (seed ^ _multiplier) & _mask;
    }

    /// <summary>
    /// Generates the next pseudo-random number.
    /// </summary>
    /// <param name="bits">The number of random bits to generate (1-32).</param>
    /// <returns>The generated random bits.</returns>
    private int Next(int bits)
    {
        _seed = (_seed * _multiplier + _addend) & _mask;
        return (int)(_seed >>> (48 - bits));
    }

    /// <summary>
    /// Fills the specified byte array with pseudo-random bytes.
    /// </summary>
    /// <param name="data">The byte array to fill with random bytes.</param>
    public void FillBytes(byte[] data)
    {
        for (int i = 0; i < data.Length;)
        {
            int randomInt = Next(32);
            for (int j = 0; j < 4 && i < data.Length; j++)
            {
                data[i++] = (byte)randomInt;
                randomInt >>= 8;
            }
        }
    }
}
