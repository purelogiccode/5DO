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
/// When unsigned-disc support is requested, <see cref="BiosRsaPatch"/> disables the retail
/// media RSA check in the in-memory copy. The source file is never modified.
/// </remarks>
[SuppressMessage(
    "Security",
    "CA5351:Do Not Use Broken Cryptographic Algorithms",
    Justification = "MD5 is the 3DO BIOS/game compatibility identifier used by 4DO.")]
public sealed class BiosImage
{
    private BiosImage(
        byte[] bios1,
        byte[] bios2,
        string sourceMd5,
        bool rsaPatched,
        bool rsaDisabled,
        int rsaPatchSites)
    {
        Bios1 = bios1;
        Bios2 = bios2;
        SourceMd5 = sourceMd5;
        Md5 = Convert.ToHexStringLower(MD5.HashData(bios1));
        Md5Bios2 = bios2.Length == 0 ? string.Empty : Convert.ToHexStringLower(MD5.HashData(bios2));
        IsAnvil = bios1[28] == 234;
        AnvilKind = IsAnvil ? 30 : 0;
        RsaPatched = rsaPatched;
        RsaDisabled = rsaDisabled;
        RsaPatchSites = rsaPatchSites;
    }

    /// <summary>Gets BIOS1 bytes. Always exactly 1 MB.</summary>
    public ReadOnlyMemory<byte> Bios1 { get; }

    /// <summary>Gets BIOS2 bytes. Empty when no second BIOS was supplied; otherwise up to 1 MB.</summary>
    public ReadOnlyMemory<byte> Bios2 { get; }

    /// <summary>Gets the lowercase hexadecimal MD5 of the source BIOS file before any patch.</summary>
    public string SourceMd5 { get; }

    /// <summary>Gets the lowercase hexadecimal MD5 of the effective BIOS1 bytes loaded into the machine.</summary>
    public string Md5 { get; }

    /// <summary>Gets the lowercase hexadecimal MD5 of BIOS2, or an empty string.</summary>
    public string Md5Bios2 { get; }

    /// <summary>Gets a value indicating whether the Anvil workaround is required.</summary>
    public bool IsAnvil { get; }

    /// <summary>Gets the Anvil kind passed to the core. FreeDO: <c>SetAnvilFix(30)</c>.</summary>
    public int AnvilKind { get; }

    /// <summary>Gets a value indicating whether the in-memory RSA check patch was applied.</summary>
    public bool RsaPatched { get; }

    /// <summary>
    /// Gets a value indicating whether the effective BIOS has the media RSA check disabled,
    /// either because it was patched now, because it is a recognized No-RSA image, or
    /// because its file name marks it as one.
    /// </summary>
    public bool RsaDisabled { get; }

    /// <summary>Gets the number of unpatched RSA check sites found in the source image.</summary>
    public int RsaPatchSites { get; }

    /// <summary>Loads BIOS1 and optional BIOS2 from files.</summary>
    /// <param name="bios1Path">Path to BIOS1.</param>
    /// <param name="bios2Path">Optional path to BIOS2.</param>
    /// <param name="allowUnsignedDiscs">
    /// When <see langword="true"/>, disables the media RSA check in memory so unsigned,
    /// homebrew, and arcade discs can boot. The BIOS file is not modified.
    /// </param>
    /// <returns>The validated image.</returns>
    /// <exception cref="ArgumentException">Thrown when a path is empty.</exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when a BIOS size is invalid, or when unsigned-disc support was requested but
    /// the RSA patch site was not found and the image is not recognizable as No-RSA.
    /// </exception>
    public static BiosImage Load(string bios1Path, string? bios2Path = null, bool allowUnsignedDiscs = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bios1Path);
        var bios1 = File.ReadAllBytes(bios1Path);
        var bios2 = string.IsNullOrWhiteSpace(bios2Path) ? Array.Empty<byte>() : File.ReadAllBytes(bios2Path);
        return FromBytes(bios1, bios2, allowUnsignedDiscs, Path.GetFileName(bios1Path));
    }

    /// <summary>Validates BIOS bytes and creates an image.</summary>
    /// <param name="bios1">Exactly 1 MB of BIOS1 data.</param>
    /// <param name="bios2">Optional BIOS2 data, at most 1 MB.</param>
    /// <param name="allowUnsignedDiscs">
    /// When <see langword="true"/>, disables the media RSA check in memory.
    /// </param>
    /// <param name="sourceName">Optional source file name used to recognize known No-RSA images.</param>
    /// <returns>The validated image.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when a BIOS size is invalid, or when unsigned-disc support was requested but
    /// the RSA patch site was not found and the image is not recognizable as No-RSA.
    /// </exception>
    public static BiosImage FromBytes(
        ReadOnlySpan<byte> bios1,
        ReadOnlySpan<byte> bios2 = default,
        bool allowUnsignedDiscs = false,
        string? sourceName = null)
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

        var bytes = bios1.ToArray();
        var sourceMd5 = Convert.ToHexStringLower(MD5.HashData(bytes));
        var knownNoRsa = BiosCatalog.Default.TryGet(sourceMd5, out BiosDescriptor descriptor)
            && descriptor.IsRsaDisabled;
        var nameNoRsa = LooksLikeNoRsa(sourceName);
        var sites = BiosRsaPatch.CountSites(bytes);
        var patched = false;
        if (allowUnsignedDiscs && sites > 0)
        {
            BiosRsaPatch.Apply(bytes);
            patched = true;
        }
        else if (allowUnsignedDiscs && !knownNoRsa && !nameNoRsa)
        {
            var name = string.IsNullOrWhiteSpace(sourceName)
                ? "the BIOS image"
                : $"'{Path.GetFileName(sourceName)}'";
            throw new InvalidDataException(
                $"Unsigned-disc support is enabled, but the RSA check patch site was not found in {name}. "
                + "Use a compatible retail 1 MB BIOS or a known No-RSA image.");
        }

        var rsaDisabled = patched || knownNoRsa || nameNoRsa;
        return new BiosImage(bytes, bios2.ToArray(), sourceMd5, patched, rsaDisabled, sites);
    }

    private static bool LooksLikeNoRsa(string? sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return false;
        }

        var name = Path.GetFileNameWithoutExtension(sourceName);
        return name.Contains("norsa", StringComparison.OrdinalIgnoreCase)
            || name.Contains("no-rsa", StringComparison.OrdinalIgnoreCase)
            || name.Contains("no_rsa", StringComparison.OrdinalIgnoreCase)
            || name.Contains("no rsa", StringComparison.OrdinalIgnoreCase)
            || name.Contains("rsa disabled", StringComparison.OrdinalIgnoreCase)
            || name.Contains("rsa-disabled", StringComparison.OrdinalIgnoreCase)
            || name.Contains("rsa_disabled", StringComparison.OrdinalIgnoreCase)
            || name.Contains("rsa patched", StringComparison.OrdinalIgnoreCase)
            || name.Contains("rsa-patched", StringComparison.OrdinalIgnoreCase)
            || name.Contains("rsa_patched", StringComparison.OrdinalIgnoreCase)
            || name.Contains("unsigned", StringComparison.OrdinalIgnoreCase);
    }
}
