using System;

namespace FiveDo.Fuzz;

/// <summary>Deterministic mutation helpers shared by the targets.</summary>
internal static class Mutation
{
    /// <summary>Creates random bytes.</summary>
    /// <param name="random">Random source.</param>
    /// <param name="length">Byte count.</param>
    /// <returns>A new byte array.</returns>
    internal static byte[] RandomBytes(Random random, int length)
    {
        ArgumentNullException.ThrowIfNull(random);
        var bytes = new byte[length];
        random.NextBytes(bytes);
        return bytes;
    }

    /// <summary>Copies and mutates a byte array.</summary>
    /// <param name="random">Random source.</param>
    /// <param name="source">Source bytes.</param>
    /// <param name="minimumChanges">Minimum mutations.</param>
    /// <param name="maximumChanges">Maximum mutations.</param>
    /// <returns>A mutated copy.</returns>
    internal static byte[] Mutate(Random random, ReadOnlySpan<byte> source, int minimumChanges, int maximumChanges)
    {
        ArgumentNullException.ThrowIfNull(random);
        var result = source.ToArray();
        int changes = random.Next(minimumChanges, maximumChanges + 1);
        for (var change = 0; change < changes; change++)
        {
            if (result.Length == 0)
            {
                break;
            }

            int index = random.Next(result.Length);
            switch (random.Next(4))
            {
                case 0:
                    result[index] ^= (byte)(1 << random.Next(8));
                    break;
                case 1:
                    result[index] = (byte)random.Next(256);
                    break;
                case 2:
                    result[index] = 0;
                    break;
                default:
                    result[index] = 0xFF;
                    break;
            }
        }

        if (result.Length > 32 && random.Next(8) == 0)
        {
            Array.Resize(ref result, random.Next(1, result.Length));
        }

        return result;
    }

    /// <summary>Returns a random length within an inclusive range.</summary>
    /// <param name="random">Random source.</param>
    /// <param name="minimum">Minimum length.</param>
    /// <param name="maximum">Maximum length.</param>
    /// <returns>The selected length.</returns>
    internal static int RandomLength(Random random, int minimum, int maximum)
    {
        ArgumentNullException.ThrowIfNull(random);
        return random.Next(minimum, maximum + 1);
    }
}
