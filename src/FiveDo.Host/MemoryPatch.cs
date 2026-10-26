using System;
using System.Collections.Generic;
using System.Globalization;
using FiveDo.Machine;

namespace FiveDo.Host;

/// <summary>One RAM patch parsed from a <c>--poke</c> specification.</summary>
/// <param name="Address">RAM byte address.</param>
/// <param name="Bytes">Bytes to write.</param>
public readonly record struct MemoryPatchEntry(uint Address, byte[] Bytes);

/// <summary>
/// Parses and applies diagnostic RAM patches. The syntax is
/// <c>address=hexbytes[;address=hexbytes...]</c>, where the address is decimal or
/// <c>0x</c>-prefixed and the byte string may contain spaces or commas.
/// </summary>
public static class MemoryPatch
{
    /// <summary>Parses a patch specification.</summary>
    /// <param name="specification">Patch text.</param>
    /// <returns>One entry per patch, in specification order.</returns>
    /// <exception cref="FormatException">Thrown when the specification is malformed.</exception>
    public static IReadOnlyList<MemoryPatchEntry> Parse(string specification)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(specification);
        var entries = new List<MemoryPatchEntry>();
        foreach (var rawPart in specification.Split([';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var equals = rawPart.IndexOf('=');
            if (equals <= 0 || equals == rawPart.Length - 1)
            {
                throw new FormatException($"Patch '{rawPart}' must be address=hexbytes.");
            }

            var addressText = rawPart[..equals].Trim();
            var bytesText = rawPart[(equals + 1)..].Trim();
            if (!TryParseAddress(addressText, out var address))
            {
                throw new FormatException($"Patch address '{addressText}' is not a valid 32-bit address.");
            }

            var normalized = bytesText
                .Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace(",", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal);
            byte[] bytes;
            try
            {
                bytes = Convert.FromHexString(normalized);
            }
            catch (FormatException exception)
            {
                throw new FormatException($"Patch bytes '{bytesText}' are not valid hexadecimal.", exception);
            }

            if (bytes.Length == 0)
            {
                throw new FormatException($"Patch at {addressText} has no bytes.");
            }

            entries.Add(new MemoryPatchEntry(address, bytes));
        }

        if (entries.Count == 0)
        {
            throw new FormatException("The patch specification is empty.");
        }

        return entries;
    }

    /// <summary>Applies a patch specification to machine RAM.</summary>
    /// <param name="machine">Target machine.</param>
    /// <param name="specification">Patch text.</param>
    /// <returns>The number of bytes written.</returns>
    public static int Apply(FiveDoMachine machine, string specification)
    {
        ArgumentNullException.ThrowIfNull(machine);
        var entries = Parse(specification);
        var written = 0;
        foreach (var entry in entries)
        {
            for (var i = 0; i < entry.Bytes.Length; i++)
            {
                machine.Memory.Ram.Write8(unchecked(entry.Address + (uint)i), entry.Bytes[i]);
                written++;
            }
        }

        return written;
    }

    private static bool TryParseAddress(string text, out uint address)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address);
        }

        return uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out address);
    }
}
