namespace CDISharp;

/// <summary>Identifies how DiscJuggler stored each sector of a track.</summary>
public enum CdiReadMode
{
    /// <summary>Mode 1 cooked data, 2048 bytes per sector.</summary>
    Mode1 = 0,

    /// <summary>Mode 2 body without sync/header, 2336 bytes per sector.</summary>
    Mode2 = 1,

    /// <summary>Raw 2352-byte CD sectors.</summary>
    Raw = 2,

    /// <summary>Raw 2352-byte CD sectors followed by 16 bytes of PQ subchannel.</summary>
    RawPq = 3,

    /// <summary>Raw 2352-byte CD sectors followed by 96 bytes of P-W subchannel.</summary>
    RawPw = 4,
}
