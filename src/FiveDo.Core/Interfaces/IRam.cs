
using System;

namespace FiveDo.Core;

/// <summary>
/// Raw RAM/VRAM access used by the CEL engine, SPORT, and VDLP.
/// </summary>
/// <remarks>
/// These methods bypass the MMIO map and operate on the same word-swapped representation
/// as FreeDOCore's <c>_mem_read8/16/32</c> and <c>_mem_write8/16/32</c>. Offsets are byte
/// offsets into the RAM allocation, not CPU bus addresses.
/// </remarks>
public interface IRam
{
    /// <summary>Gets the total backing size in bytes, including reserved high-resolution mirrors.</summary>
    int Size { get; }

    /// <summary>
    /// Gets the raw word backing store, or an empty span when the implementation does not
    /// expose one.
    /// </summary>
    /// <remarks>
    /// The span is in the same word-swapped representation as <see cref="Read32"/>. It is
    /// intended for bulk scanline copies; callers must still apply the address lane rules.
    /// </remarks>
    ReadOnlySpan<uint> Words => ReadOnlySpan<uint>.Empty;

    /// <summary>
    /// Gets a value indicating whether the low-ROM overlay is active. While it is set,
    /// CPU reads below the ROM base come from the BIOS rather than DRAM.
    /// </summary>
    bool RomOverlay => false;

    /// <summary>
    /// Gets the active DRAM/VRAM layout. The default is the stock 2 MB DRAM / 1 MB VRAM
    /// layout so test doubles remain simple; the machine memory overrides it.
    /// </summary>
    MemoryLayout Layout => MemoryLayout.Stock;

    /// <summary>Reads one byte at a raw RAM offset.</summary>
    /// <param name="offset">Byte offset.</param>
    /// <returns>The byte, or zero when the offset is outside the allocation.</returns>
    byte Read8(uint offset);

    /// <summary>Reads one halfword at a raw RAM offset.</summary>
    /// <param name="offset">Byte offset.</param>
    /// <returns>The halfword, or zero when the offset is outside the allocation.</returns>
    ushort Read16(uint offset);

    /// <summary>Reads one word at a raw RAM offset.</summary>
    /// <param name="offset">Byte offset.</param>
    /// <returns>The word, or zero when the offset is outside the allocation.</returns>
    uint Read32(uint offset);

    /// <summary>Writes one byte at a raw RAM offset.</summary>
    /// <param name="offset">Byte offset.</param>
    /// <param name="value">Byte value.</param>
    void Write8(uint offset, byte value);

    /// <summary>Writes one halfword at a raw RAM offset.</summary>
    /// <param name="offset">Byte offset.</param>
    /// <param name="value">Halfword value.</param>
    void Write16(uint offset, ushort value);

    /// <summary>Writes one word at a raw RAM offset.</summary>
    /// <param name="offset">Byte offset.</param>
    /// <param name="value">Word value.</param>
    void Write32(uint offset, uint value);
}
