
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
