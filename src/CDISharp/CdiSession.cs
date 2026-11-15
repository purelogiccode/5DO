namespace CDISharp;

/// <summary>Describes one session in a DiscJuggler image.</summary>
/// <param name="Number">One-based session number.</param>
/// <param name="FirstTrackNumber">One-based number of the first track in the session.</param>
/// <param name="TrackCount">Number of tracks in the session.</param>
public sealed record CdiSession(int Number, int FirstTrackNumber, int TrackCount)
{
    /// <summary>Gets the one-based number of the last track in the session.</summary>
    public int LastTrackNumber => FirstTrackNumber + TrackCount - 1;
}
