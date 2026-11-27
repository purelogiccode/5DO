using System;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FiveDo.Core;
namespace FiveDo.Host.Netplay;

/// <summary>
/// Builds the content fingerprint that every peer must agree on before a session starts.
/// </summary>
/// <remarks>
/// The fingerprint covers both BIOS images, the disc identity, and every tuning value that
/// can change emulated behavior. Two peers that disagree on any of those inputs would
/// diverge immediately, so the handshake rejects the session instead of starting.
/// </remarks>
public static class NetplayContentHash
{
    private const string Domain = "5DO-netplay-v1";

    /// <summary>Computes the 64-bit content fingerprint.</summary>
    /// <param name="bios1">Primary BIOS image bytes.</param>
    /// <param name="bios2">Optional secondary BIOS image bytes.</param>
    /// <param name="discId">Stable disc identity, normally <c>GameId.Compute</c>.</param>
    /// <param name="tuning">Emulator tuning that affects execution.</param>
    /// <returns>The first eight bytes of the SHA-256 fingerprint, little-endian.</returns>
    public static ulong Compute(
        ReadOnlySpan<byte> bios1,
        ReadOnlySpan<byte> bios2,
        string discId,
        EmulatorTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(discId);
        ArgumentNullException.ThrowIfNull(tuning);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(Domain));
        hash.AppendData(bios1);
        hash.AppendData(bios2);
        hash.AppendData(Encoding.UTF8.GetBytes(discId));
        hash.AppendData(Encoding.UTF8.GetBytes(FormatTuning(tuning)));
        Span<byte> digest = stackalloc byte[32];
        hash.GetHashAndReset(digest);
        return BinaryPrimitives.ReadUInt64LittleEndian(digest);
    }

    /// <summary>Formats the behavior-affecting tuning fields canonically.</summary>
    /// <param name="tuning">Tuning to format.</param>
    /// <returns>A stable invariant-culture representation.</returns>
    public static string FormatTuning(EmulatorTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        var seed = tuning.RandomSeed is { } value
            ? value.ToString("X8", CultureInfo.InvariantCulture)
            : "default";
        var builder = new StringBuilder();
        builder.Append(tuning.ArmClock.ToString(CultureInfo.InvariantCulture)).Append('|');
        builder.Append(tuning.Region.ToString()).Append('|');
        builder.Append(tuning.ArmTiming.ToString()).Append('|');
        builder.Append(tuning.ClioTimers.ToString()).Append('|');
        builder.Append(tuning.CelTiming.ToString()).Append('|');
        builder.Append(tuning.CdromTiming.ToString()).Append('|');
        builder.Append(tuning.CdSpeed.ToString()).Append('|');
        builder.Append(tuning.MemoryConfiguration.ToString()).Append('|');
        builder.Append(seed).Append('|');
        builder.Append(tuning.FixMode.ToString()).Append('|');
        builder.Append(tuning.CpuBackend.ToString()).Append('|');
        builder.Append(tuning.HighResolution ? '1' : '0').Append('|');
        builder.Append(tuning.TrueHighResolution ? '1' : '0').Append('|');
        builder.Append(tuning.OperaDipir ? '1' : '0').Append('|');
        builder.Append(tuning.OperaSoftReset ? '1' : '0').Append('|');
        builder.Append(tuning.SwiHle ? '1' : '0');
        return builder.ToString();
    }
}
