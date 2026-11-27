using System;
using System.Collections.Generic;
using System.IO;
using FiveDo.Machine;
using FiveDo.Video;

namespace FiveDo.Host.Netplay;

/// <summary>
/// Captures and applies the compressed machine state used for join and reconnect.
/// </summary>
public static class NetplayStateTransfer
{
    /// <summary>Captures a compressed state and the hash of its last assembled frame.</summary>
    /// <param name="machine">Machine to snapshot. The caller must pause it first.</param>
    /// <param name="discHash">Disc identity stored in and checked against the state.</param>
    /// <returns>The state payload.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="machine"/> is null.</exception>
    public static NetplayStateMessage Capture(FiveDoMachine machine, string? discHash)
    {
        ArgumentNullException.ThrowIfNull(machine);
        using var stream = new MemoryStream();
        machine.SaveState(stream, discHash, compress: true);
        return new NetplayStateMessage(
            machine.FramesExecuted,
            FrameHasher.Hash(machine.Frame),
            stream.ToArray());
    }

    /// <summary>Loads a state and validates its frame, frame hash, and warnings.</summary>
    /// <param name="machine">Target machine. The caller must pause it first.</param>
    /// <param name="message">State payload from the host.</param>
    /// <param name="discHash">Expected disc identity.</param>
    /// <param name="error">Failure detail, or <see langword="null"/> on success.</param>
    /// <returns><see langword="true"/> when the state loaded and matched.</returns>
    public static bool TryApply(
        FiveDoMachine machine,
        NetplayStateMessage message,
        string? discHash,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(message);
        error = null;
        try
        {
            machine.LoadState(message.Data, discHash);
        }
        catch (Exception exception) when (
            exception is InvalidDataException
                or IOException
                or ArgumentException
                or InvalidOperationException)
        {
            error = $"The transferred state could not be loaded: {exception.Message}";
            return false;
        }

        if (machine.FramesExecuted != message.StartFrame)
        {
            error = $"The transferred state starts at frame {machine.FramesExecuted}, "
                + $"but the host announced {message.StartFrame}.";
            return false;
        }

        var frameHash = FrameHasher.Hash(machine.Frame);
        if (frameHash != message.FrameHash)
        {
            error = $"The transferred state frame hash is 0x{frameHash:X8}, "
                + $"but the host announced 0x{message.FrameHash:X8}.";
            return false;
        }

        if (machine.StateWarnings.Count > 0)
        {
            var warnings = new List<string>(machine.StateWarnings);
            error = "The transferred state reported warnings: " + string.Join("; ", warnings);
            return false;
        }

        return true;
    }
}
