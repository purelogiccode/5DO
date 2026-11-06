namespace FiveDo.Core;

/// <summary>
/// The CPU-visible address bus.
/// </summary>
/// <remarks>
/// Implementations preserve FreeDOCore's decode order and word-swapped byte convention.
/// Byte and halfword accesses are not simply the low bits of a word access: the reference
/// XORs the address before indexing memory.
/// </remarks>
public interface IAddressBus
{
    /// <summary>Reads one byte.</summary>
    /// <param name="address">CPU address.</param>
    /// <returns>The byte, or the low byte of <see cref="EmulatorConstants.UnmappedValue"/> when unmapped.</returns>
    byte Read8(uint address);

    /// <summary>Reads one halfword.</summary>
    /// <param name="address">CPU address.</param>
    /// <returns>The halfword, or the low halfword of <see cref="EmulatorConstants.UnmappedValue"/> when unmapped.</returns>
    /// <remarks>RAM and ROM accesses apply the reference's <c>address ^ 2</c> lane convention.</remarks>
    ushort Read16(uint address);

    /// <summary>Reads one word. The address is aligned to four bytes.</summary>
    /// <param name="address">CPU address.</param>
    /// <returns>The word, or <see cref="EmulatorConstants.UnmappedValue"/> when unmapped.</returns>
    uint Read32(uint address);

    /// <summary>
    /// Reads the instruction word at the program counter.
    /// </summary>
    /// <param name="address">Instruction address. Aligned to four bytes.</param>
    /// <returns>The instruction word.</returns>
    /// <remarks>
    /// The default implementation forwards to <see cref="Read32"/>. Memory buses can
    /// override it with a direct RAM/ROM fast path for the interpreter loop.
    /// </remarks>
    uint ReadInstruction(uint address) => Read32(address);

    /// <summary>Writes one byte.</summary>
    /// <param name="address">CPU address.</param>
    /// <param name="value">Byte value.</param>
    void Write8(uint address, byte value);

    /// <summary>Writes one halfword.</summary>
    /// <param name="address">CPU address.</param>
    /// <param name="value">Halfword value.</param>
    /// <remarks>RAM writes apply the reference's <c>address ^ 2</c> lane convention.</remarks>
    void Write16(uint address, ushort value);

    /// <summary>Writes one word. The address is aligned to four bytes.</summary>
    /// <param name="address">CPU address.</param>
    /// <param name="value">Word value.</param>
    void Write32(uint address, uint value);
}
