using System;

namespace FiveDo.Host.Netplay;

/// <summary>
/// Counters collected by one lockstep session for probe reports and latency diagnostics.
/// </summary>
public sealed class NetplayMetrics
{
    /// <summary>Gets the number of datagrams sent.</summary>
    public long PacketsSent { get; private set; }

    /// <summary>Gets the number of datagrams received.</summary>
    public long PacketsReceived { get; private set; }

    /// <summary>Gets the number of payload bytes sent, excluding UDP and IP headers.</summary>
    public long BytesSent { get; private set; }

    /// <summary>Gets the number of payload bytes received, excluding UDP and IP headers.</summary>
    public long BytesReceived { get; private set; }

    /// <summary>Gets the number of input packets resent because the remote frame was late.</summary>
    public long InputResends { get; private set; }

    /// <summary>Gets the number of historical input entries received as redundancy.</summary>
    public long RedundantInputs { get; private set; }

    /// <summary>Gets the number of duplicate input entries ignored.</summary>
    public long DuplicateInputs { get; private set; }

    /// <summary>Gets the longest single wait for a remote input frame, in milliseconds.</summary>
    public double MaxWaitMilliseconds { get; private set; }

    /// <summary>Gets the total time spent waiting for remote input, in milliseconds.</summary>
    public double TotalWaitMilliseconds { get; private set; }

    /// <summary>Records one sent datagram.</summary>
    /// <param name="bytes">Payload length.</param>
    public void RecordSent(int bytes)
    {
        PacketsSent++;
        BytesSent += bytes;
    }

    /// <summary>Records one received datagram.</summary>
    /// <param name="bytes">Payload length.</param>
    public void RecordReceived(int bytes)
    {
        PacketsReceived++;
        BytesReceived += bytes;
    }

    /// <summary>Records one input packet resend.</summary>
    public void RecordResend() => InputResends++;

    /// <summary>Records one redundant historical input entry.</summary>
    public void RecordRedundantInput() => RedundantInputs++;

    /// <summary>Records one duplicate input entry.</summary>
    public void RecordDuplicateInput() => DuplicateInputs++;

    /// <summary>Records one wait for a remote input frame.</summary>
    /// <param name="milliseconds">Elapsed wait time.</param>
    public void RecordWait(double milliseconds)
    {
        if (milliseconds < 0 || double.IsNaN(milliseconds))
        {
            return;
        }

        TotalWaitMilliseconds += milliseconds;
        MaxWaitMilliseconds = Math.Max(MaxWaitMilliseconds, milliseconds);
    }
}
