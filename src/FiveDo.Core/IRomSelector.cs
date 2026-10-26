namespace FiveDo.Core;

/// <summary>
/// Selects the BIOS ROM bank visible in the CPU address window.
/// </summary>
/// <remarks>FreeDO: <c>SelectROM</c>, called from CLIO register <c>0x84</c>.</remarks>
public interface IRomSelector
{
    /// <summary>Selects a ROM bank.</summary>
    /// <param name="bank">Zero for BIOS1, non-zero for BIOS2.</param>
    void SelectRom(int bank);
}
