using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using FiveDo.Audio;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Host.Rewind;
using FiveDo.Input;
using FiveDo.Machine;
using FiveDo.Storage;
using FiveDo.Video;
using Serilog;

namespace FiveDo.Frontend.Avalonia.Audio;

/// <summary>Options for the headless NativeAOT frontend audio soak.</summary>
/// <param name="Seconds">Requested wall-clock duration.</param>
/// <param name="OutputPath">JSON result path.</param>
/// <param name="BufferMilliseconds">OpenAL target buffer latency.</param>
/// <param name="Device">OpenAL device name, or null for the default device.</param>
/// <param name="SyncMilliseconds">Audio/video sync offset.</param>
/// <param name="Region">Machine video region.</param>
/// <param name="AllowUnsignedDiscs">Apply the in-memory media RSA bypass.</param>
/// <param name="AutoRegion">Detect PAL from the loaded BIOS when <paramref name="Region"/> is a placeholder.</param>
internal sealed record FrontendAudioSoakOptions(
    double Seconds,
    string OutputPath,
    int BufferMilliseconds,
    string? Device,
    int SyncMilliseconds,
    VideoRegion Region,
    bool AllowUnsignedDiscs,
    bool AutoRegion = false);

/// <summary>
/// Runs the desktop frontend's real emulation/audio path without initializing Avalonia,
/// then records dropped samples and underrun buffers. It exists so the trimmed and
/// NativeAOT frontend binaries can be validated against the null and real OpenAL devices
/// with the same machine, CoreRunner, and <see cref="OpenAlAudioOutput"/> path the desktop
/// uses.
/// </summary>
internal static class FrontendAudioSoak
{
    internal const string SoakOption = "--audio-soak";
    internal const string OutputOption = "--audio-soak-output";

    /// <summary>Parses the soak options when <c>--audio-soak</c> is present.</summary>
    /// <param name="args">Process arguments.</param>
    /// <param name="options">Parsed options, or null when parsing failed.</param>
    /// <param name="error">A user-facing error when parsing failed.</param>
    /// <returns>True when the argument list requested a soak.</returns>
    internal static bool TryParse(
        string[] args,
        out FrontendAudioSoakOptions? options,
        out string? error)
    {
        options = null;
        error = null;
        var secondsText = Program.FindOptionValue(args, SoakOption);
        if (secondsText is null)
        {
            return false;
        }

        if (!double.TryParse(
                secondsText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var seconds)
            || seconds is < 1 or > 3600)
        {
            error = "--audio-soak must be between 1 and 3600 seconds.";
            return true;
        }

        var bufferText = Program.FindOptionValue(args, "--audio-buffer");
        if (bufferText is not null
            && (!int.TryParse(bufferText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var buffer)
                || buffer is < 20 or > 500))
        {
            error = "--audio-buffer must be between 20 and 500 milliseconds.";
            return true;
        }

        var syncText = Program.FindOptionValue(args, "--audio-sync");
        if (syncText is not null
            && (!int.TryParse(syncText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sync)
                || sync is < -250 or > 250))
        {
            error = "--audio-sync must be between -250 and 250 milliseconds.";
            return true;
        }

        VideoRegion region = VideoRegion.Ntsc;
        var regionText = Program.FindOptionValue(args, "--region");
        bool autoRegion = regionText is null
            || string.Equals(regionText, "auto", StringComparison.OrdinalIgnoreCase);
        if (!autoRegion && !TryParseRegion(regionText!, out region))
        {
            error = "--region must be auto, ntsc, pal1, or pal2.";
            return true;
        }

        var output = Program.FindOptionValue(args, OutputOption)
            ?? Path.Combine("artifacts", "audio-soak.json");

        options = new FrontendAudioSoakOptions(
            seconds,
            Path.GetFullPath(output),
            bufferText is null ? 100 : int.Parse(bufferText, CultureInfo.InvariantCulture),
            Program.FindOptionValue(args, "--audio-device"),
            syncText is null ? 0 : int.Parse(syncText, CultureInfo.InvariantCulture),
            region,
            Program.HasFlag(args, "--allow-unsigned-discs"),
            autoRegion);
        return true;
    }

    /// <summary>Runs the soak and writes its JSON result.</summary>
    /// <param name="options">Parsed soak options.</param>
    /// <returns>Zero when the run finished with zero drops and zero underruns.</returns>
    internal static int Run(FrontendAudioSoakOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = new SoakResult
        {
            RequestedSeconds = options.Seconds,
            Device = string.IsNullOrWhiteSpace(options.Device) ? "default" : options.Device,
            BufferMilliseconds = options.BufferMilliseconds,
            SyncMilliseconds = options.SyncMilliseconds,
            Region = options.Region.ToString(),
            Runtime = RuntimeInformation.FrameworkDescription,
            NativeAot = !RuntimeFeature.IsDynamicCodeSupported,
            StartedUtc = DateTimeOffset.UtcNow,
        };

        try
        {
            var discPath = Program.StartupDiscPath
                ?? throw new InvalidOperationException("--audio-soak requires a disc path.");
            var biosPath = Program.StartupBiosPath
                ?? throw new InvalidOperationException("--audio-soak requires --bios <path>.");

            result.DiscPath = discPath;
            result.BiosPath = biosPath;

            using IDiscSource disc = DiscImageFactory.Open(discPath);
            result.DiscHash = GameId.Compute(disc);

            BiosImage bios = BiosImage.Load(
                biosPath,
                bios2Path: null,
                allowUnsignedDiscs: options.AllowUnsignedDiscs);
            result.BiosMd5 = bios.Md5;
            result.BiosAnvil = bios.IsAnvil;

            VideoRegion region = options.Region;
            if (options.AutoRegion)
            {
                region = BiosCatalog.Default.TryGet(bios.SourceMd5, out BiosDescriptor descriptor)
                    && descriptor.VideoStandard == BiosVideoStandard.Pal
                    ? VideoRegion.Pal1
                    : VideoRegion.Ntsc;
                result.Region = region.ToString();
            }

            using OpenAlAudioOutput? audio = OpenAlAudioOutput.TryCreate(
                options.BufferMilliseconds,
                options.Device,
                options.SyncMilliseconds,
                out var openAlError);
            if (audio is null)
            {
                throw new InvalidOperationException(
                    $"OpenAL is unavailable: {openAlError ?? "unknown error"}");
            }

            audio.Volume = 1f;
            result.OpenAlReady = true;

            var settings = new AppSettings();
            EmulatorTuning tuning = settings.CreateEmulatorTuning() with
            {
                Region = region,
            };
            var machine = new FiveDoMachine(tuning);
            machine.LoadBios(bios.Bios1.Span, bios.Bios2.Span);
            machine.InsertDisc(disc);
            machine.Reset();
            machine.LoadNvram(NvramImage.CreateFresh().Data.Span);
            machine.PbusSource = new PbusSource();
            machine.AudioSink = audio;

            var targetFrames = (long)Math.Round(options.Seconds * machine.FramesPerSecond);
            result.TargetFrames = targetFrames;
            result.FieldRate = machine.RegionProfile.FieldRate;

            Exception? fault = null;
            using var runner = new CoreRunner(
                machine,
                rewindOptions: new RewindOptions { Enabled = false });
            runner.Faulted += (_, args) => fault = args.Exception;

            var clock = Stopwatch.StartNew();
            runner.Start();
            var timeout = TimeSpan.FromSeconds(options.Seconds + 90);
            while (runner.Frames < targetFrames
                   && fault is null
                   && clock.Elapsed < timeout)
            {
                Thread.Sleep(100);
            }

            runner.Stop();
            clock.Stop();

            result.Frames = runner.Frames;
            result.ElapsedSeconds = clock.Elapsed.TotalSeconds;
            result.SamplesProduced = machine.AudioSamplesProduced;
            result.SamplesPushed = audio.SamplesPushed;
            result.SamplesDropped = audio.SamplesDropped;
            result.UnderrunBuffers = audio.UnderrunBuffers;
            result.QueuedBuffers = audio.QueuedBuffers;
            result.QueuedFrames = audio.QueuedFrames;
            result.BufferedFrames = audio.BufferedFrames;
            result.LatencyMilliseconds = audio.LatencyMilliseconds;
            result.OpenAlErrors = audio.AlErrors;
            result.OpenAlLastError = audio.LastAlError;
            result.DeviceFailed = audio.DeviceFailed;
            result.DspRunning = machine.Dsp.IsRunning;
            result.SourceWidth = machine.Frame.SourceWidth;
            result.SourceHeight = machine.Frame.SourceHeight;
            result.FrameHash = FrameHasher.Hash(machine.Frame);

            if (fault is not null)
            {
                result.Error = $"Emulation fault: {fault.Message}";
            }
            else if (runner.Frames < targetFrames)
            {
                result.Error = $"The soak reached {runner.Frames} of {targetFrames} frames.";
            }

            result.Ok = result.Error is null
                && result.SamplesDropped == 0
                && result.UnderrunBuffers == 0
                && result.OpenAlErrors == 0
                && !result.DeviceFailed;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or InvalidOperationException
            or DllNotFoundException
            or EntryPointNotFoundException)
        {
            result.Error = exception.Message;
            Log.Error(exception, "Frontend audio soak failed.");
        }
        finally
        {
            result.FinishedUtc = DateTimeOffset.UtcNow;
            WriteResult(options.OutputPath, result);
        }

        if (!result.Ok)
        {
            Console.Error.WriteLine($"Frontend audio soak failed: {result.Error ?? "unknown error"}");
        }

        return result.Ok ? 0 : 1;
    }

    private static bool TryParseRegion(string text, out VideoRegion region)
    {
        switch (text.ToUpperInvariant())
        {
            case "AUTO":
                region = VideoRegion.Ntsc;
                return true;
            case "NTSC":
                region = VideoRegion.Ntsc;
                return true;
            case "PAL1":
                region = VideoRegion.Pal1;
                return true;
            case "PAL2":
                region = VideoRegion.Pal2;
                return true;
            default:
                region = VideoRegion.Ntsc;
                return false;
        }
    }

    private static void WriteResult(string path, SoakResult result)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = File.Create(path);
            using var writer = new Utf8JsonWriter(
                stream,
                new JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            writer.WriteBoolean("ok", result.Ok);
            writer.WriteString("mode", "frontend-nativeaot-audio-soak");
            writer.WriteString("runtime", result.Runtime);
            writer.WriteBoolean("nativeAot", result.NativeAot);
            writer.WriteString("startedUtc", result.StartedUtc);
            writer.WriteString("finishedUtc", result.FinishedUtc);
            writer.WriteNumber("requestedSeconds", result.RequestedSeconds);
            writer.WriteNumber("elapsedSeconds", result.ElapsedSeconds);
            writer.WriteNumber("frames", result.Frames);
            writer.WriteNumber("targetFrames", result.TargetFrames);
            writer.WriteNumber("fieldRate", result.FieldRate);
            writer.WriteString("region", result.Region);
            writer.WriteNumber("sourceWidth", result.SourceWidth);
            writer.WriteNumber("sourceHeight", result.SourceHeight);
            writer.WriteNumber("frameHash", result.FrameHash);
            writer.WriteNumber("samplesProduced", result.SamplesProduced);
            writer.WriteNumber("samplesPushed", result.SamplesPushed);
            writer.WriteNumber("samplesDropped", result.SamplesDropped);
            writer.WriteNumber("underrunBuffers", result.UnderrunBuffers);
            writer.WriteNumber("queuedBuffers", result.QueuedBuffers);
            writer.WriteNumber("queuedFrames", result.QueuedFrames);
            writer.WriteNumber("bufferedFrames", result.BufferedFrames);
            writer.WriteNumber("latencyMilliseconds", result.LatencyMilliseconds);
            writer.WriteNumber("openAlErrors", result.OpenAlErrors);
            writer.WriteBoolean("deviceFailed", result.DeviceFailed);
            writer.WriteBoolean("dspRunning", result.DspRunning);
            writer.WriteBoolean("openAlReady", result.OpenAlReady);
            writer.WriteString("device", result.Device);
            writer.WriteNumber("bufferMilliseconds", result.BufferMilliseconds);
            writer.WriteNumber("syncMilliseconds", result.SyncMilliseconds);
            writer.WriteString("discPath", result.DiscPath);
            writer.WriteString("discHash", result.DiscHash);
            writer.WriteString("biosPath", result.BiosPath);
            writer.WriteString("biosMd5", result.BiosMd5);
            writer.WriteBoolean("biosAnvil", result.BiosAnvil);
            if (result.OpenAlLastError is not null)
            {
                writer.WriteString("openAlLastError", result.OpenAlLastError);
            }

            if (result.Error is not null)
            {
                writer.WriteString("error", result.Error);
            }

            writer.WriteEndObject();
            writer.Flush();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.Error(exception, "Frontend audio soak result write failed.");
        }
    }

    private sealed class SoakResult
    {
        internal bool Ok { get; set; }
        internal string Runtime { get; init; } = string.Empty;
        internal bool NativeAot { get; init; }
        internal DateTimeOffset StartedUtc { get; init; }
        internal DateTimeOffset FinishedUtc { get; set; }
        internal double RequestedSeconds { get; init; }
        internal double ElapsedSeconds { get; set; }
        internal long Frames { get; set; }
        internal long TargetFrames { get; set; }
        internal int FieldRate { get; set; }
        internal string Region { get; set; } = string.Empty;
        internal int SourceWidth { get; set; }
        internal int SourceHeight { get; set; }
        internal uint FrameHash { get; set; }
        internal long SamplesProduced { get; set; }
        internal long SamplesPushed { get; set; }
        internal long SamplesDropped { get; set; }
        internal long UnderrunBuffers { get; set; }
        internal long QueuedBuffers { get; set; }
        internal long QueuedFrames { get; set; }
        internal int BufferedFrames { get; set; }
        internal double LatencyMilliseconds { get; set; }
        internal long OpenAlErrors { get; set; }
        internal string? OpenAlLastError { get; set; }
        internal bool DeviceFailed { get; set; }
        internal bool DspRunning { get; set; }
        internal bool OpenAlReady { get; set; }
        internal string Device { get; init; } = string.Empty;
        internal int BufferMilliseconds { get; init; }
        internal int SyncMilliseconds { get; init; }
        internal string? DiscPath { get; set; }
        internal string? DiscHash { get; set; }
        internal string? BiosPath { get; set; }
        internal string? BiosMd5 { get; set; }
        internal bool BiosAnvil { get; set; }
        internal string? Error { get; set; }
    }
}
