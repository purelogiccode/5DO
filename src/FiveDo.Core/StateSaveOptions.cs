using System.IO.Compression;

namespace FiveDo.Core;

/// <summary>
/// Options that control how a machine state is serialized.
/// </summary>
/// <remarks>
/// The default matches the persisted `.tdos` format: Brotli compression at
/// <see cref="CompressionLevel.Optimal"/>. The rewind history uses
/// <see cref="CompressionLevel.Fastest"/> so a snapshot captured on the emulation thread
/// does not stall a frame.
/// </remarks>
public sealed record StateSaveOptions
{
    /// <summary>Gets the default save options.</summary>
    public static StateSaveOptions Default { get; } = new();

    /// <summary>Gets a value indicating whether large section payloads are compressed.</summary>
    public bool CompressionEnabled { get; init; } = true;

    /// <summary>Gets the Brotli compression level used when compression is enabled.</summary>
    public CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Optimal;

    /// <summary>
    /// Gets a value indicating whether each section carries a CRC-32 trailer. Disabling it
    /// is only appropriate for transient in-memory snapshots such as the rewind history.
    /// </summary>
    public bool ChecksumsEnabled { get; init; } = true;

    /// <summary>
    /// Gets the initial state buffer size in bytes. Zero lets the writer grow from its
    /// default capacity.
    /// </summary>
    public int InitialCapacity { get; init; }
}
