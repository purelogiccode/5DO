using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using FiveDo.Core;

namespace FiveDo.Storage;

/// <summary>
/// A validated 3DO BIOS image (BIOS1 plus optional BIOS2) with MD5 hashes and Anvil detection.
/// </summary>
/// <remarks>
/// FreeDO/4DO load exactly 1 MB for BIOS1. BIOS2 is optional and may be shorter than 1 MB;
/// the missing bytes are zero-filled, matching 4DO's <c>GameConsole</c> behaviour. Anvil
/// BIOSes are detected by <c>byte[28] == 234</c> and select Anvil kind 30.
/// </remarks>
[SuppressMessage(
    "Security",
    "CA5351:Do Not Use Broken Cryptographic Algorithms",
    Justification = "MD5 is the 3DO BIOS/game compatibility identifier used by 4DO.")]
public sealed class BiosImage
{
    private BiosImage(byte[] bios1, byte[] bios2)
    {
        Bios1 = bios1;
        Bios2 = bios2;
        Md5 = Convert.ToHexStringLower(MD5.HashData(bios1));
        Md5Bios2 = bios2.Length == 0 ? string.Empty : Convert.ToHexStringLower(MD5.HashData(bios2));
        IsAnvil = bios1[28] == 234;
        AnvilKind = IsAnvil ? 30 : 0;
    }

    /// <summary>Gets BIOS1 bytes. Always exactly 1 MB.</summary>
    public ReadOnlyMemory<byte> Bios1 { get; }

    /// <summary>Gets BIOS2 bytes. Empty when no second BIOS was supplied; otherwise up to 1 MB.</summary>
    public ReadOnlyMemory<byte> Bios2 { get; }

    /// <summary>Gets the lowercase hexadecimal MD5 of BIOS1.</summary>
    public string Md5 { get; }

    /// <summary>Gets the lowercase hexadecimal MD5 of BIOS2, or an empty string.</summary>
    public string Md5Bios2 { get; }

    /// <summary>Gets a value indicating whether the Anvil workaround is required.</summary>
    public bool IsAnvil { get; }

    /// <summary>Gets the Anvil kind passed to the core. FreeDO: <c>SetAnvilFix(30)</c>.</summary>
    public int AnvilKind { get; }

    /// <summary>Loads BIOS1 and optional BIOS2 from files.</summary>
    /// <param name="bios1Path">Path to BIOS1.</param>
    /// <param name="bios2Path">Optional path to BIOS2.</param>
    /// <returns>The validated image.</returns>
    /// <exception cref="ArgumentException">Thrown when a path is empty.</exception>
    /// <exception cref="InvalidDataException">Thrown when a BIOS size is invalid.</exception>
    public static BiosImage Load(string bios1Path, string? bios2Path = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bios1Path);
        var bios1 = File.ReadAllBytes(bios1Path);
        var bios2 = string.IsNullOrWhiteSpace(bios2Path) ? Array.Empty<byte>() : File.ReadAllBytes(bios2Path);
        return FromBytes(bios1, bios2);
    }

    /// <summary>Validates BIOS bytes and creates an image.</summary>
    /// <param name="bios1">Exactly 1 MB of BIOS1 data.</param>
    /// <param name="bios2">Optional BIOS2 data, at most 1 MB.</param>
    /// <returns>The validated image.</returns>
    /// <exception cref="InvalidDataException">Thrown when a BIOS size is invalid.</exception>
    public static BiosImage FromBytes(ReadOnlySpan<byte> bios1, ReadOnlySpan<byte> bios2 = default)
    {
        if (bios1.Length != EmulatorConstants.RomBankSize)
        {
            throw new InvalidDataException(
                $"BIOS1 must be exactly {EmulatorConstants.RomBankSize} bytes; got {bios1.Length}.");
        }

        if (bios2.Length > EmulatorConstants.RomBankSize)
        {
            throw new InvalidDataException(
                $"BIOS2 must be at most {EmulatorConstants.RomBankSize} bytes; got {bios2.Length}.");
        }

        return new BiosImage(bios1.ToArray(), bios2.ToArray());
    }
}
