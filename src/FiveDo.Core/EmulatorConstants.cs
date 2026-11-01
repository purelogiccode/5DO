namespace FiveDo.Core;

/// <summary>
/// Fixed hardware constants shared by the emulation core.
/// </summary>
/// <remarks>
/// Values are taken from FreeDOCore (<c>arm.cpp</c>, <c>quarz.cpp</c>, <c>vdlp.cpp</c>,
/// and <c>freedocore.h</c>) and must not be changed without a compatibility test.
/// </remarks>
public static class EmulatorConstants
{
    /// <summary>Default ARM60 clock in hertz. FreeDO: <c>ARM_CLOCK</c>.</summary>
    public const int ArmClock = 12_500_000;

    /// <summary>Audio sample rate in hertz. FreeDO: <c>SND_CLOCK</c>.</summary>
    public const int SoundClock = 44_100;

    /// <summary>Timer clock in hertz used by the quartz scheduler.</summary>
    public const int TimerClock = 25_000_000;

    /// <summary>Number of VDL events in one full 526-line cycle. FreeDO: <c>VDL_FS</c>.</summary>
    public const int VdlFrameLines = 526;

    /// <summary>Number of VDL lines in one half-frame. FreeDO: <c>VDL_HS</c>.</summary>
    public const int VdlLinesPerHalfFrame = 263;

    /// <summary>VDL events per second. FreeDO: <c>VDL_CLOCK = VDL_FS * 30</c>.</summary>
    public const int VdlClock = 15_780;

    /// <summary>ARM cycles in one 60 Hz frame. FreeDO uses integer division: <c>12_500_000 / 60</c>.</summary>
    public const int FrameCycles = ArmClock / 60;

    /// <summary>Size of the CPU-visible DRAM/VRAM region in bytes.</summary>
    public const int RamSize = 3 * 1024 * 1024;

    /// <summary>Number of extra 1 MB VRAM mirrors reserved for the future high-resolution mode.</summary>
    public const int RamMirrorCount = 16;

    /// <summary>Total backing size of the RAM array, including reserved high-resolution mirrors.</summary>
    public const int RamAllocationSize = RamSize + (RamMirrorCount * 1024 * 1024);

    /// <summary>Size of one BIOS ROM bank in bytes.</summary>
    public const int RomBankSize = 1 * 1024 * 1024;

    /// <summary>Total size of both BIOS ROM banks in bytes.</summary>
    public const int RomSize = 2 * RomBankSize;

    /// <summary>Size of NVRAM in bytes. FreeDO maps one NVRAM byte per 32-bit word.</summary>
    public const int NvramSize = 32 * 1024;

    /// <summary>Value returned for unmapped word reads. FreeDO: <c>0xBADACCE5</c>.</summary>
    public const uint UnmappedValue = 0xBADACCE5;

    /// <summary>Base address of DRAM/VRAM.</summary>
    public const uint RamBase = 0x00000000;

    /// <summary>Base address of the active BIOS ROM bank window.</summary>
    public const uint RomBase = 0x03000000;

    /// <summary>Base address of the shared NVRAM/diagnostic-port decode window.</summary>
    public const uint NvramDiagWindowBase = 0x03100000;

    /// <summary>Base address of the NVRAM window.</summary>
    public const uint NvramBase = 0x03140000;

    /// <summary>Base address of the diagnostic port window.</summary>
    public const uint DiagBase = 0x03180000;

    /// <summary>Base address of the SPORT window.</summary>
    public const uint SportBase = 0x03200000;

    /// <summary>Base address of the MADAM window.</summary>
    public const uint MadamBase = 0x03300000;

    /// <summary>Base address of the CLIO window.</summary>
    public const uint ClioBase = 0x03400000;

    /// <summary>Default visible frame width in pixels.</summary>
    public const int VideoWidth = 320;

    /// <summary>Default visible frame height in lines.</summary>
    public const int VideoHeight = 240;

    /// <summary>Visible width of the high-resolution (2x) display mode.</summary>
    public const int HighResolutionWidth = VideoWidth * 2;

    /// <summary>Visible height of the high-resolution (2x) display mode.</summary>
    public const int HighResolutionHeight = VideoHeight * 2;

    /// <summary>Size of the CPU-visible VRAM region in bytes. FreeDO: <c>VRAM_SIZE</c>.</summary>
    public const int VramSize = RamSize - (2 * 1024 * 1024);

    /// <summary>Base offset of VRAM within the RAM backing array. FreeDO: <c>vram</c>.</summary>
    public const int VramBase = RamSize - VramSize;

    /// <summary>Number of VRAM planes used by high-resolution rendering (4x 1 MB).</summary>
    public const int HighResolutionVramPlanes = 4;

    /// <summary>Maximum pixels stored for one VDL line, including wider VDLP modes.</summary>
    public const int VdlLineMaxPixels = 1280;

    /// <summary>Maximum VDL lines stored in a frame, including PAL and high-resolution modes.</summary>
    public const int VdlFrameMaxLines = 576;
}
