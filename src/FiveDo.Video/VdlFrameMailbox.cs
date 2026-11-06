using System;
using System.Threading;
using FiveDo.Core;

namespace FiveDo.Video;

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
    private readonly Lock _gate = new();
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
            Copy(frame, _snapshot);
            _sequence++;
        }
    }

    /// <summary>
    /// Copies the latest snapshot into a caller-owned frame and then invokes the reader
    /// without holding the mailbox lock. Use this on the render thread so GPU submission
    /// cannot stall the emulation thread.
    /// </summary>
    /// <param name="destination">Caller-owned scratch frame, reused across calls.</param>
    /// <param name="reader">Action that consumes the copy; it must not retain it.</param>
    public void Read(VdlFrame destination, Action<VdlFrame> reader)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(reader);
        lock (_gate)
        {
            Copy(_snapshot, destination);
        }

        reader(destination);
    }

    private static void Copy(VdlFrame source, VdlFrame destination)
    {
        destination.CopyFrom(source);
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
