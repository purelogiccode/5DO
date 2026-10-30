
namespace FiveDo.Host;


/// <summary>One RAM patch parsed from a <c>--poke</c> specification.</summary>
/// <param name="Address">RAM byte address.</param>
/// <param name="Bytes">Bytes to write.</param>
public readonly record struct MemoryPatchEntry(uint Address, byte[] Bytes);
