using System;
using ThreeDo.Core;

namespace ThreeDo.Video;

/// <summary>
/// A thread-safe copy of the current VDL frame for the presentation loop.
/// </summary>
/// <remarks>
/// The machine reuses its <see cref="VdlFrame"/>, so a background emulation thread cannot hand
/// that object directly to the UI thread. This mailbox reuses its own lines and copies only the
/// visible width. Readers receive a consistent snapshot under a short lock.
/// </remarks>
public sealed class VdlFrameMailbox
{
    private readonly VdlFrame _snapshot = new();
    private readonly object _gate = new();
    private long _sequence;

    /// <summary>Gets the number of completed frames written.</summary>
    public long Sequence
    {
        get
        {
            lock (_gate)
            {
                return _sequence;
            }
        }
    }

    /// <summary>Copies a completed machine frame into the mailbox.</summary>
    /// <param name="frame">Source frame. The machine may reuse it after this call.</param>
    public void Write(VdlFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        lock (_gate)
        {
            _snapshot.SourceWidth = frame.SourceWidth;
            _snapshot.SourceHeight = frame.SourceHeight;
            var height = Math.Min(frame.SourceHeight, VdlFrame.MaxLines);
            for (var i = 0; i < height; i++)
            {
                var source = frame[i];
                var destination = _snapshot[i];
                destination.Width = source.Width;
                destination.OutControl = source.OutControl;
                destination.ClutDma = source.ClutDma;
                destination.Background = source.Background;
                source.ClutB.CopyTo(destination.ClutB);
                source.ClutG.CopyTo(destination.ClutG);
                source.ClutR.CopyTo(destination.ClutR);
                source.PixelBuffer[..VdlLine.MaxPixels].CopyTo(destination.PixelBuffer);
            }

            _sequence++;
        }
    }

    /// <summary>Runs an action with a stable snapshot while holding the mailbox lock.</summary>
    /// <param name="reader">Action that consumes the snapshot; it must not retain the frame.</param>
    public void Read(Action<VdlFrame> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        lock (_gate)
        {
            reader(_snapshot);
        }
    }
}
