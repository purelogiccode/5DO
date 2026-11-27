using System.Collections.Generic;

namespace FiveDo.NetplayProbe;

public sealed class ProbeReport
{
    public string Disc { get; set; } = string.Empty;

    public string Bios { get; set; } = string.Empty;

    public string Profile { get; set; } = string.Empty;

    public int Frames { get; set; }

    public int InputDelay { get; set; }

    public int LatencyMilliseconds { get; set; }

    public int DropInputEvery { get; set; }

    public string ContentHash { get; set; } = string.Empty;

    public bool Ok { get; set; }

    public double WallSeconds { get; set; }

    public double FramesPerSecond { get; set; }

    public PeerRunReport Host { get; set; } = new();

    public PeerRunReport Join { get; set; } = new();

    public ReferenceRunReport? Reference { get; set; }

    public StateCostReport? StateCost { get; set; }

    public int CheckpointMismatches { get; set; }

    public int ReferenceMismatches { get; set; }

    public IList<string> Problems { get; } = new List<string>();
}

public sealed class PeerRunReport
{
    public string Role { get; set; } = string.Empty;

    public bool Started { get; set; }

    public bool Completed { get; set; }

    public string State { get; set; } = string.Empty;

    public string? Failure { get; set; }

    public long Frames { get; set; }

    public double Seconds { get; set; }

    public double FramesPerSecond { get; set; }

    public long? DesyncFrame { get; set; }

    public string? LocalFrameHash { get; set; }

    public string? RemoteFrameHash { get; set; }

    public string? LocalAudioHash { get; set; }

    public string? RemoteAudioHash { get; set; }

    public long? LocalAudioSamples { get; set; }

    public long? RemoteAudioSamples { get; set; }

    public long PacketsSent { get; set; }

    public long PacketsReceived { get; set; }

    public long BytesSent { get; set; }

    public long BytesReceived { get; set; }

    public long InputResends { get; set; }

    public long RedundantInputs { get; set; }

    public long DuplicateInputs { get; set; }

    public double MaxWaitMilliseconds { get; set; }

    public double TotalWaitMilliseconds { get; set; }

    public IList<CheckpointReport> Checkpoints { get; } = new List<CheckpointReport>();
}

public sealed class CheckpointReport
{
    public long Frame { get; set; }

    public string FrameHash { get; set; } = string.Empty;

    public string AudioHash { get; set; } = string.Empty;

    public long AudioSamples { get; set; }
}

public sealed class ReferenceRunReport
{
    public bool Ran { get; set; }

    public long Frames { get; set; }

    public double Seconds { get; set; }

    public double FramesPerSecond { get; set; }

    public string FinalFrameHash { get; set; } = string.Empty;

    public IList<CheckpointReport> Checkpoints { get; } = new List<CheckpointReport>();
}

public sealed class StateCostReport
{
    public int SnapshotBytes { get; set; }

    public double SnapshotCaptureMilliseconds { get; set; }

    public double SnapshotRestoreMilliseconds { get; set; }

    public int CompressedBytes { get; set; }

    public double CompressedSaveMilliseconds { get; set; }

    public double CompressedLoadMilliseconds { get; set; }
}
