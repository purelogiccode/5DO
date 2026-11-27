using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FiveDo.NetplayProbe;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine(ProbeOptions.BuildUsage());
            return args.Length == 0 ? 2 : 0;
        }

        try
        {
            ProbeOptions options = ProbeOptions.Parse(args);
            ProbeReport report = ProbeRunner.Run(options);
            if (options.JsonPath is not null)
            {
                File.WriteAllText(options.JsonPath, JsonSerializer.Serialize(report, JsonOptions));
            }

            Print(report);
            return report.Ok ? 0 : 1;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Print(ProbeReport report)
    {
        Console.WriteLine($"disc:          {report.Disc}");
        Console.WriteLine($"bios:          {report.Bios}");
        Console.WriteLine($"profile:       {report.Profile}");
        Console.WriteLine($"frames:        {report.Frames}");
        Console.WriteLine($"input delay:   {report.InputDelay} frames");
        Console.WriteLine($"latency:       {report.LatencyMilliseconds} ms one-way");
        Console.WriteLine($"input loss:    {(report.DropInputEvery == 0 ? "none" : $"1 in {report.DropInputEvery} input packets")}");
        Console.WriteLine($"content hash:  {report.ContentHash}");
        Console.WriteLine();
        PrintPeer(report.Host);
        PrintPeer(report.Join);
        if (report.Reference is { Ran: true } reference)
        {
            Console.WriteLine(
                $"reference:     {reference.Frames} frames in {reference.Seconds:F2}s "
                + $"({reference.FramesPerSecond:F1} fps), final {reference.FinalFrameHash}");
        }

        if (report.StateCost is { } state)
        {
            Console.WriteLine(
                $"state:         {state.SnapshotBytes:N0} bytes snapshot, "
                + $"{state.SnapshotCaptureMilliseconds:F2} ms capture, "
                + $"{state.SnapshotRestoreMilliseconds:F2} ms restore; "
                + $"{state.CompressedBytes:N0} bytes compressed, "
                + $"{state.CompressedSaveMilliseconds:F0} ms save, "
                + $"{state.CompressedLoadMilliseconds:F0} ms load");
        }

        Console.WriteLine();
        Console.WriteLine($"wall:          {report.WallSeconds:F2}s ({report.FramesPerSecond:F1} fps)");
        Console.WriteLine(
            $"checkpoints:   {report.CheckpointMismatches} host/join mismatches, "
            + $"{report.ReferenceMismatches} netplay/reference mismatches");
        foreach (string problem in report.Problems)
        {
            Console.WriteLine($"problem:       {problem}");
        }

        Console.WriteLine(report.Ok ? "result:        PASS" : "result:        FAIL");
    }

    private static void PrintPeer(PeerRunReport peer)
    {
        Console.WriteLine(
            $"{peer.Role,-13}  {peer.Frames} frames in {peer.Seconds:F2}s "
            + $"({peer.FramesPerSecond:F1} fps), {peer.State}, {peer.PacketsSent} sent/"
            + $"{peer.PacketsReceived} received, {peer.BytesSent + peer.BytesReceived:N0} bytes");
        if (peer.Failure is not null)
        {
            Console.WriteLine($"  failure:     {peer.Failure}");
        }

        if (peer.DesyncFrame is { } frame)
        {
            Console.WriteLine(
                $"  desync:      frame {frame}, {peer.LocalFrameHash} vs {peer.RemoteFrameHash}, "
                + $"audio {peer.LocalAudioHash} vs {peer.RemoteAudioHash} "
                + $"({peer.LocalAudioSamples} vs {peer.RemoteAudioSamples} samples)");
        }

        Console.WriteLine(
            $"  waits:       max {peer.MaxWaitMilliseconds:F2} ms, total {peer.TotalWaitMilliseconds:F1} ms, "
            + $"{peer.InputResends} resends, {peer.RedundantInputs} redundant, {peer.DuplicateInputs} duplicate");
    }
}
