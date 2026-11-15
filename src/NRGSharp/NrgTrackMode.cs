namespace NRGSharp;

/// <summary>Identifies the track mode stored in a Nero DAO or TAO chunk.</summary>
public enum NrgTrackMode : ushort
{
    /// <summary>Mode 1 data, usually cooked to 2048 bytes per sector.</summary>
    Data = 0x0000,

    /// <summary>Mode 2 Form 1 data.</summary>
    DataMode2Form1 = 0x0002,

    /// <summary>Mode 2 Form 2 data.</summary>
    DataMode2Form2 = 0x0003,

    /// <summary>Raw Mode 1 data, 2352 bytes per sector.</summary>
    RawData = 0x0005,

    /// <summary>Raw Mode 2 data, 2352 bytes per sector.</summary>
    RawMode2 = 0x0006,

    /// <summary>Red Book CD-DA audio.</summary>
    Audio = 0x0007,

    /// <summary>Alternate audio marker used by some Nero versions.</summary>
    AudioAlt = 0x0008,

    /// <summary>Raw Mode 1 data followed by 96 bytes of subchannel.</summary>
    RawDataSubchannel = 0x000F,

    /// <summary>Audio followed by 96 bytes of subchannel.</summary>
    AudioSubchannel = 0x0010,

    /// <summary>Raw Mode 2 data followed by 96 bytes of subchannel.</summary>
    RawMode2Subchannel = 0x0011,
}
