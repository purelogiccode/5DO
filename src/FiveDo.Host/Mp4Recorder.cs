using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Audio;
using FiveDo.Core;
using FiveDo.Video;

namespace FiveDo.Host;

/// <summary>
/// Records presented frames and DSP audio to an H.264/AAC MP4 using an external FFmpeg.
/// </summary>
/// <remarks>
/// <para>
/// The recorder is an <see cref="IFrameSink"/> for video and exposes a
/// <see cref="WavAudioSink"/> through <see cref="AudioSink"/> for the host's audio tee.
/// Video is streamed to an FFmpeg encoder as raw BGRA frames; audio is written to a
/// temporary 44.1 kHz stereo WAV. On <see cref="Complete"/> the encoded video and WAV are
/// muxed into the requested MP4 with <c>-c:v copy</c> and AAC audio.
/// </para>
/// <para>
/// FFmpeg is not bundled. The recorder looks for <c>FIVEDO_FFMPEG</c>, then an
/// <c>ffmpeg</c>/<c>ffmpeg.exe</c> beside the apphost, then <c>PATH</c>. It prefers
/// <c>libx264</c>, then a platform H.264 encoder, and falls back to FFmpeg's native
/// <c>mpeg4</c> encoder. The host owns the live audio sink; this class only records the
/// samples it receives.
/// </para>
/// </remarks>
public sealed class Mp4Recorder : IFrameSink, IDisposable
{
    private const int AudioBitrate = 192_000;
    private const int ProcessTimeoutMilliseconds = 120_000;
    private static readonly string[] EncoderPreference =
    [
        "libx264",
        "h264_nvenc",
        "h264_qsv",
        "h264_amf",
        "h264_videotoolbox",
    ];

    private readonly string _outputPath;
    private readonly string _ffmpegPath;
    private readonly string _tempDirectory;
    private readonly string _videoPath;
    private readonly string _audioPath;
    private readonly int _frameRateNumerator;
    private readonly int _frameRateDenominator;
    private readonly int _maxFrames;
    private readonly VideoRecordingQuality _quality;
    private readonly WavAudioSink _audio;
    private readonly ConcurrentQueue<string> _videoErrors = new();
    private readonly ConcurrentQueue<string> _muxErrors = new();
    private Process? _videoProcess;
    private Stream? _videoInput;
    private FrameBuffer? _frameBuffer;
    private FrameBuffer? _scalingBuffer;
    private int _width;
    private int _height;
    private int _capturedFrames;
    private bool _completed;
    private bool _failed;

    private Mp4Recorder(
        string outputPath,
        string ffmpegPath,
        string tempDirectory,
        int frameRateNumerator,
        int frameRateDenominator,
        int maxFrames,
        VideoRecordingQuality quality)
    {
        _outputPath = outputPath;
        _ffmpegPath = ffmpegPath;
        _tempDirectory = tempDirectory;
        _videoPath = Path.Combine(tempDirectory, "video.mp4");
        _audioPath = Path.Combine(tempDirectory, "audio.wav");
        _frameRateNumerator = frameRateNumerator;
        _frameRateDenominator = frameRateDenominator;
        _maxFrames = maxFrames;
        _quality = quality;
        _audio = new WavAudioSink(_audioPath);
    }

    /// <summary>Gets the audio sink that the host must tee into the machine's audio path.</summary>
    public IAudioSink AudioSink => _audio;

    /// <summary>Gets the requested output path.</summary>
    public string OutputPath => _outputPath;

    /// <summary>Gets the number of video frames written.</summary>
    public int CapturedFrames => _capturedFrames;

    /// <summary>Gets the video width, or zero before the first frame.</summary>
    public int Width => _width;

    /// <summary>Gets the video height, or zero before the first frame.</summary>
    public int Height => _height;

    /// <summary>Gets the first recorder failure, or <see langword="null"/> when none occurred.</summary>
    public string? Failure { get; private set; }

    /// <summary>
    /// Finds an FFmpeg executable without starting a process.
    /// </summary>
    /// <returns>The full path, or <see langword="null"/> when FFmpeg is not available.</returns>
    public static string? FindFfmpeg()
    {
        var explicitPath = Environment.GetEnvironmentVariable("FIVEDO_FFMPEG");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
        {
            return explicitPath;
        }

        var executable = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var local = Path.Combine(AppContext.BaseDirectory, executable);
        if (File.Exists(local))
        {
            return local;
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim('"'), executable);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Starts an MP4 recording.
    /// </summary>
    /// <param name="outputPath">Destination <c>.mp4</c> path.</param>
    /// <param name="sourceFramesPerSecond">Emulated frame rate, used for validation.</param>
    /// <param name="frameRateNumerator">Exact frame-rate numerator (for example 60000).</param>
    /// <param name="frameRateDenominator">Exact frame-rate denominator (for example 1001).</param>
    /// <param name="maxFrames">Maximum frames to capture, or zero for unlimited.</param>
    /// <param name="quality">Encoder quality preset.</param>
    /// <param name="ffmpegPath">Optional explicit FFmpeg path; otherwise auto-detected.</param>
    /// <returns>A recorder ready to receive frames and audio.</returns>
    /// <exception cref="ArgumentException">The output path is missing.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A frame rate or frame limit is invalid.</exception>
    /// <exception cref="InvalidOperationException">FFmpeg is not available.</exception>
    public static Mp4Recorder Start(
        string outputPath,
        double sourceFramesPerSecond,
        int frameRateNumerator,
        int frameRateDenominator,
        int maxFrames = 0,
        VideoRecordingQuality quality = VideoRecordingQuality.Medium,
        string? ffmpegPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sourceFramesPerSecond, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(frameRateNumerator, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(frameRateDenominator, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(maxFrames);

        var ffmpeg = ffmpegPath ?? FindFfmpeg();
        if (string.IsNullOrWhiteSpace(ffmpeg) || !File.Exists(ffmpeg))
        {
            throw new InvalidOperationException(
                "FFmpeg was not found. Install FFmpeg, place ffmpeg beside 5DO, or set FIVEDO_FFMPEG to its executable path.");
        }

        var fullOutput = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullOutput);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "5do-mp4-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return new Mp4Recorder(
            fullOutput,
            Path.GetFullPath(ffmpeg),
            tempDirectory,
            frameRateNumerator,
            frameRateDenominator,
            maxFrames,
            quality);
    }

    /// <inheritdoc />
    public void OnFrameTrigger()
    {
    }

    /// <inheritdoc />
    public void OnFrame(VdlFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (_completed || _failed || (_maxFrames > 0 && _capturedFrames >= _maxFrames))
        {
            return;
        }

        if (_videoProcess is null && !StartVideo(frame))
        {
            return;
        }

        var width = Math.Max(1, frame.SourceWidth);
        var height = Math.Max(1, frame.SourceHeight);
        FrameBuffer destination = _frameBuffer!;
        if (width == _width && height == _height)
        {
            FrameAssembler.Assemble(frame, destination, width, height);
        }
        else
        {
            if (_scalingBuffer is null
                || _scalingBuffer.Width != width
                || _scalingBuffer.Height != height)
            {
                _scalingBuffer = new FrameBuffer(width, height);
            }

            FrameAssembler.Assemble(frame, _scalingBuffer, width, height);
            ScaleNearest(_scalingBuffer, destination);
        }

        try
        {
            _videoInput!.Write(destination.PixelSpan);
            _capturedFrames++;
        }
        catch (IOException exception)
        {
            _failed = true;
            Failure = "The FFmpeg video encoder stopped accepting frames: " + exception.Message;
        }
    }

    /// <summary>Finalizes the video, audio, and MP4 mux. Safe to call twice.</summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no frames were captured or FFmpeg fails.
    /// </exception>
    public void Complete()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        try
        {
            if (_videoProcess is null || _capturedFrames == 0)
            {
                throw new InvalidOperationException("No video frames were captured.");
            }

            CloseVideoEncoder();
            _audio.Dispose();
            Mux();
        }
        finally
        {
            Cleanup();
        }
    }

    /// <summary>Finalizes the recording on a worker thread.</summary>
    /// <param name="cancellationToken">Cancels the wait for the final mux.</param>
    /// <returns>A task that completes when the MP4 is written.</returns>
    public Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(Complete, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_completed)
        {
            try
            {
                Complete();
            }
            catch (Exception exception) when (exception is IOException
                or InvalidOperationException
                or System.ComponentModel.Win32Exception)
            {
                Failure ??= exception.Message;
                KillVideoProcess();
                Cleanup();
            }
        }

        _audio.Dispose();
    }

    private bool StartVideo(VdlFrame frame)
    {
        _width = Math.Max(2, frame.SourceWidth & ~1);
        _height = Math.Max(2, frame.SourceHeight & ~1);
        _frameBuffer = new FrameBuffer(_width, _height);
        var encoder = SelectEncoder(_ffmpegPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        AddArguments(startInfo, "-hide_banner", "-loglevel", "error", "-y");
        AddArguments(
            startInfo,
            "-f", "rawvideo",
            "-pixel_format", "bgra",
            "-video_size", string.Create(CultureInfo.InvariantCulture, $"{_width}x{_height}"),
            "-framerate", string.Create(
                CultureInfo.InvariantCulture,
                $"{_frameRateNumerator}/{_frameRateDenominator}"),
            "-i", "pipe:0",
            "-an",
            "-c:v", encoder);
        AddQualityArguments(startInfo, encoder);
        AddArguments(startInfo, "-pix_fmt", "yuv420p", _videoPath);

        try
        {
            var process = new Process { StartInfo = startInfo };
            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    _videoErrors.Enqueue(args.Data);
                    while (_videoErrors.Count > 32)
                    {
                        _videoErrors.TryDequeue(out _);
                    }
                }
            };
            if (!process.Start())
            {
                throw new InvalidOperationException("FFmpeg did not start.");
            }

            process.BeginErrorReadLine();
            _videoProcess = process;
            _videoInput = process.StandardInput.BaseStream;
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            _failed = true;
            Failure = "Could not start the FFmpeg video encoder: " + exception.Message;
            return false;
        }
    }

    private void CloseVideoEncoder()
    {
        try
        {
            _videoInput?.Dispose();
        }
        catch (IOException)
        {
            // FFmpeg may have already closed the pipe after a failure.
        }

        var process = _videoProcess;
        if (process is null)
        {
            return;
        }

        if (!process.WaitForExit(ProcessTimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(10_000);
            throw new InvalidOperationException("The FFmpeg video encoder did not finish in time.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "The FFmpeg video encoder failed: " + LastError(_videoErrors));
        }
    }

    private void Mux()
    {
        var audioBytes = _audio.DataBytes;
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        AddArguments(startInfo, "-hide_banner", "-loglevel", "error", "-y", "-i", _videoPath);
        if (audioBytes > 0)
        {
            AddArguments(startInfo, "-i", _audioPath, "-map", "0:v:0", "-map", "1:a:0");
        }
        else
        {
            AddArguments(startInfo, "-map", "0:v:0");
        }

        AddArguments(startInfo, "-c:v", "copy");
        if (audioBytes > 0)
        {
            AddArguments(
                startInfo,
                "-c:a", "aac",
                "-b:a", AudioBitrate.ToString(CultureInfo.InvariantCulture),
                "-shortest");
        }

        AddArguments(startInfo, "-movflags", "+faststart", _outputPath);
        RunMux(startInfo);
    }

    private void RunMux(ProcessStartInfo startInfo)
    {
        using var process = new Process();
        process.StartInfo = startInfo;
        process.ErrorDataReceived += (sender, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data))
            {
                _muxErrors.Enqueue(args.Data);
                while (_muxErrors.Count > 32)
                {
                    _muxErrors.TryDequeue(out _);
                }
            }
        };
        if (!process.Start())
        {
            throw new InvalidOperationException("FFmpeg did not start for the MP4 mux.");
        }

        process.BeginErrorReadLine();
        if (!process.WaitForExit(ProcessTimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(10_000);
            throw new InvalidOperationException("The FFmpeg MP4 mux did not finish in time.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "The FFmpeg MP4 mux failed: " + LastError(_muxErrors));
        }
    }

    private string SelectEncoder(string ffmpeg)
    {
        var output = RunFfmpegCapture(ffmpeg, ["-hide_banner", "-encoders"]);
        foreach (var encoder in EncoderPreference)
        {
            if (output.Contains(encoder, StringComparison.Ordinal))
            {
                return encoder;
            }
        }

        return "mpeg4";
    }

    private static string RunFfmpegCapture(string ffmpeg, IReadOnlyList<string> arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            return string.Empty;
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(10_000);
        return output + error;
    }

    private void AddQualityArguments(ProcessStartInfo startInfo, string encoder)
    {
        var crf = _quality switch
        {
            VideoRecordingQuality.Low => 30,
            VideoRecordingQuality.High => 18,
            _ => 23,
        };
        var value = crf.ToString(CultureInfo.InvariantCulture);
        switch (encoder)
        {
            case "libx264":
                AddArguments(startInfo, "-preset", "veryfast", "-crf", value);
                break;
            case "h264_nvenc":
                AddArguments(startInfo, "-preset", "p4", "-cq", value, "-b:v", "0");
                break;
            case "h264_qsv":
                AddArguments(startInfo, "-global_quality", value);
                break;
            case "h264_amf":
                AddArguments(startInfo, "-quality", "balanced", "-qp_i", value, "-qp_p", value);
                break;
            case "h264_videotoolbox":
                AddArguments(
                    startInfo,
                    "-b:v",
                    _quality switch
                    {
                        VideoRecordingQuality.Low => "4M",
                        VideoRecordingQuality.High => "16M",
                        _ => "8M",
                    });
                break;
            default:
                AddArguments(
                    startInfo,
                    "-q:v",
                    _quality switch
                    {
                        VideoRecordingQuality.Low => "6",
                        VideoRecordingQuality.High => "2",
                        _ => "3",
                    });
                break;
        }
    }

    private static void AddArguments(ProcessStartInfo startInfo, params string[] arguments)
    {
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
    }

    private static string LastError(ConcurrentQueue<string> errors)
    {
        var lines = errors.ToArray();
        var text = string.Join(Environment.NewLine, lines).Trim();
        if (text.Length <= 800)
        {
            return text.Length == 0 ? "unknown error" : text;
        }

        return text[^800..];
    }

    private void KillVideoProcess()
    {
        var process = _videoProcess;
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        finally
        {
            process.Dispose();
            _videoProcess = null;
            _videoInput = null;
        }
    }

    private void Cleanup()
    {
        _videoInput?.Dispose();
        _videoInput = null;
        _videoProcess?.Dispose();
        _videoProcess = null;
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void ScaleNearest(FrameBuffer source, FrameBuffer destination)
    {
        ReadOnlySpan<byte> sourcePixels = source.PixelSpan;
        Span<byte> destinationPixels = destination.PixelSpan;
        for (var y = 0; y < destination.Height; y++)
        {
            var sourceY = Math.Min(source.Height - 1, y * source.Height / destination.Height);
            var sourceRow = sourceY * source.Stride;
            var destinationRow = y * destination.Stride;
            for (var x = 0; x < destination.Width; x++)
            {
                var sourceX = Math.Min(source.Width - 1, x * source.Width / destination.Width);
                sourcePixels.Slice(sourceRow + (sourceX * 4), 4)
                    .CopyTo(destinationPixels[(destinationRow + (x * 4))..]);
            }
        }
    }
}
