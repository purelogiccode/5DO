namespace CDISharp;

/// <summary>Identifies the recording mode of a DiscJuggler track.</summary>
public enum CdiTrackMode
{
    /// <summary>Red Book CD-DA audio.</summary>
    Audio = 0,

    /// <summary>Mode 1 data.</summary>
    Mode1 = 1,

    /// <summary>Mode 2 data (Form 1, Form 2, or mixed).</summary>
    Mode2 = 2,
}
