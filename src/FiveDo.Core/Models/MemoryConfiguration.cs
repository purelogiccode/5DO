namespace FiveDo.Core;

/// <summary>
/// The 3DO DRAM/VRAM capacity configuration.
/// </summary>
/// <remarks>
/// <para>
/// Values match Opera's <c>opera_mem_cfg_t</c> encoding: the high nibble is the DRAM size
/// in megabytes and the low nibble is the VRAM size in megabytes. Only the eight
/// configurations that shipped in 3DO hardware or the Opera core are valid.
/// </para>
/// <para>
/// The stock machine has 2 MB of DRAM and 1 MB of VRAM. Expanded configurations change the
/// CPU-visible RAM boundary and the VRAM base; they do not change the emulated CPU or video
/// timing. Opera exposes the active layout through the MADAM RED SYSBITS register so the
/// BIOS can size memory during boot.
/// </para>
/// </remarks>
public enum MemoryConfiguration
{
    /// <summary>2 MB DRAM and 1 MB VRAM (stock).</summary>
    Dram2MbVram1Mb = 0x21,

    /// <summary>2 MB DRAM and 2 MB VRAM.</summary>
    Dram2MbVram2Mb = 0x22,

    /// <summary>4 MB DRAM and 1 MB VRAM.</summary>
    Dram4MbVram1Mb = 0x41,

    /// <summary>4 MB DRAM and 2 MB VRAM.</summary>
    Dram4MbVram2Mb = 0x42,

    /// <summary>8 MB DRAM and 1 MB VRAM.</summary>
    Dram8MbVram1Mb = 0x81,

    /// <summary>8 MB DRAM and 2 MB VRAM.</summary>
    Dram8MbVram2Mb = 0x82,

    /// <summary>14 MB DRAM and 2 MB VRAM.</summary>
    Dram14MbVram2Mb = 0xE2,

    /// <summary>15 MB DRAM and 1 MB VRAM.</summary>
    Dram15MbVram1Mb = 0xF1,
}
