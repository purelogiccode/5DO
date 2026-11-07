using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using FiveDo.Audio;
using FiveDo.Input;
using FiveDo.Video;

namespace FiveDo.DiffHarness;

/// <summary>Runs the bundled OperaCore reference library and captures deterministic samples.</summary>
internal static class OperaCoreCaptureRunner
{
    private const string DefaultCorePath =
        "References/CPP_SimpleGui/cmake-build-release/SimpleGui/Core/OperaCore.dll";

    /// <summary>Captures one title with the Opera reference core.</summary>
    public static CaptureResult Capture(
        HarnessOptions harness,
        DiffManifest manifest,
        DiffTitle title,
        string repoRoot,
        string manifestDirectory,
        string outputDirectory,
        uint seed)
    {
        var frames = harness.Frames ?? DiffManifestLoader.ResolveFrames(manifest, title);
        var checkpoints = DiffManifestLoader.ResolveCheckpoints(manifest, title)
            .Where(frame => frame <= frames)
            .ToArray();
        if (checkpoints.Length == 0)
        {
            checkpoints = [frames];
        }

        var window = manifest.Defaults?.Compare?.FrameWindow ?? 0;
        var runFrames = frames + window;
        var captureCheckpoints = DiffManifestLoader.ExpandCheckpoints(checkpoints, window, runFrames);

        var corePath = harness.OperaCorePath
            ?? DiffManifestLoader.ResolvePath(
                repoRoot,
                manifestDirectory,
                manifest.Defaults?.OperaCore ?? DefaultCorePath);
        var bios = harness.BiosPath ?? DiffManifestLoader.ResolveBios(repoRoot, manifestDirectory, manifest, title);
        var disc = DiffManifestLoader.ResolvePath(repoRoot, manifestDirectory, title.Disc);
        var inputScript = DiffManifestLoader.ResolveInputScript(repoRoot, manifestDirectory, manifest, title);
        var titleDirectory = Path.Combine(outputDirectory, HarnessCommon.SafeName(title.Id));
        Directory.CreateDirectory(titleDirectory);

        if (!File.Exists(corePath))
        {
            return Failed(disc, bios, frames, "OperaCore not found: " + corePath);
        }

        if (!File.Exists(bios))
        {
            return Failed(disc, bios, frames, "BIOS not found: " + bios);
        }

        if (!File.Exists(disc) && string.IsNullOrWhiteSpace(title.Iso))
        {
            return Failed(disc, bios, frames, "disc image not found: " + disc);
        }

        var workRoot = DiffManifestLoader.ResolvePath(
            repoRoot,
            manifestDirectory,
            manifest.Defaults?.WorkDirectory ?? "artifacts/diff/work");
        var coreMd5 = HarnessCommon.Md5File(corePath);
        var biosMd5 = HarnessCommon.Md5File(bios);
        var runtime = StageRuntime(workRoot, corePath, coreMd5, bios, biosMd5);
        var iso = StageIso(workRoot, title, manifestDirectory, repoRoot, harness.Force);
        var nvramPath = iso + ".srm";
        HarnessCommon.DeleteFile(nvramPath);

        IReadOnlyList<InputScriptEntry>? script = null;
        if (inputScript is not null)
        {
            script = InputScript.Load(inputScript);
        }

        var timeout = harness.TimeoutSeconds ?? manifest.Defaults?.TimeoutSeconds ?? 180;
        using var host = new OperaCoreHost(
            runtime.CorePath,
            iso,
            runFrames,
            captureCheckpoints,
            script,
            timeout,
            seed,
            Path.Combine(titleDirectory, "opera.log.txt"),
            harness.NoImages ? null : Path.Combine(titleDirectory, "images"));

        CaptureResult result = host.Execute();
        return new CaptureResult
        {
            Engine = result.Engine,
            Ok = result.Ok,
            Error = result.Error,
            Disc = disc,
            Iso = iso,
            Bios = bios,
            BiosMd5 = biosMd5,
            CorePath = corePath,
            CoreMd5 = coreMd5,
            FramesRequested = frames,
            FramesRun = result.FramesRun,
            ElapsedSeconds = result.ElapsedSeconds,
            Width = result.Width,
            Height = result.Height,
            Checkpoints = result.Checkpoints,
            Warnings = result.Warnings,
            LogPath = result.LogPath,
            Seed = seed,
            CdCommands = result.CdCommands,
            CdCommandCycles = result.CdCommandCycles,
        };
    }

    private static OperaRuntime StageRuntime(
        string workRoot,
        string corePath,
        string coreMd5,
        string biosPath,
        string biosMd5)
    {
        var name = HarnessCommon.SafeName(Path.GetFileNameWithoutExtension(corePath))
            + "-" + coreMd5[..8]
            + "-" + biosMd5[..8];
        var directory = Path.Combine(workRoot, "opera-runtime", name);
        var stagedCore = Path.Combine(directory, "OperaCore.dll");
        var biosDirectory = Path.Combine(directory, "BIOS");
        var stagedBios = Path.Combine(biosDirectory, Path.GetFileName(biosPath));
        Directory.CreateDirectory(biosDirectory);
        if (!File.Exists(stagedCore))
        {
            File.Copy(corePath, stagedCore, overwrite: true);
        }

        if (!File.Exists(stagedBios))
        {
            File.Copy(biosPath, stagedBios, overwrite: true);
        }

        return new OperaRuntime(stagedCore);
    }

    private static string StageIso(
        string workRoot,
        DiffTitle title,
        string manifestDirectory,
        string repoRoot,
        bool force)
    {
        var directory = Path.Combine(workRoot, "discs", HarnessCommon.SafeName(title.Id));
        Directory.CreateDirectory(directory);
        var isoName = HarnessCommon.SafeName(title.Id) + ".iso";
        var staged = Path.Combine(directory, isoName);
        if (File.Exists(staged) && !force)
        {
            return staged;
        }

        if (!string.IsNullOrWhiteSpace(title.Iso))
        {
            var sourceIso = DiffManifestLoader.ResolvePath(repoRoot, manifestDirectory, title.Iso);
            if (!File.Exists(sourceIso))
            {
                throw new FileNotFoundException("ISO not found.", sourceIso);
            }

            if (force)
            {
                HarnessCommon.DeleteFile(staged);
            }

            HarnessCommon.StageFile(sourceIso, staged);
            return staged;
        }

        var disc = DiffManifestLoader.ResolvePath(repoRoot, manifestDirectory, title.Disc);
        HarnessCommon.DeleteFile(staged);
        HarnessCommon.ExtractIso(disc, staged);
        return staged;
    }

    private static CaptureResult Failed(
        string disc,
        string bios,
        int frames,
        string error)
    {
        return new CaptureResult
        {
            Engine = "opera",
            Ok = false,
            Error = error,
            Disc = disc,
            Bios = bios,
            FramesRequested = frames,
        };
    }

    private sealed record OperaRuntime(string CorePath);
}

/// <summary>A loaded OperaCore library and its run loop.</summary>
internal sealed class OperaCoreHost : IDisposable
{
    private const uint SgJoypadB = 0;
    private const uint SgJoypadY = 1;
    private const uint SgJoypadSelect = 2;
    private const uint SgJoypadStart = 3;
    private const uint SgJoypadUp = 4;
    private const uint SgJoypadDown = 5;
    private const uint SgJoypadLeft = 6;
    private const uint SgJoypadRight = 7;
    private const uint SgJoypadA = 8;
    private const uint SgJoypadL = 10;
    private const uint SgJoypadR = 11;

    private readonly IntPtr _library;
    private readonly string _isoPath;
    private readonly int _targetFrames;
    private readonly int[] _checkpoints;
    private readonly IReadOnlyList<InputScriptEntry>? _script;
    private readonly int _timeoutSeconds;
    private readonly uint _seed;
    private readonly string _logPath;
    private readonly string? _imageDirectory;
    private readonly List<string> _log = [];
    private readonly Lock _logLock = new();
    private readonly Dictionary<int, CaptureSample> _samples = [];
    private readonly PcmHasher _pcm = new();

    private readonly ApiVersion _apiVersion;
    private readonly Init _init;
    private readonly Deinit _deinit;
    private readonly LoadGame _loadGame;
    private readonly UnloadGame _unloadGame;
    private readonly GetAvInfo _getAvInfo;
    private readonly Run _run;
    private readonly Stop _stop;
    private readonly SetVideo _setVideo;
    private readonly SetAudio _setAudio;
    private readonly SetInputPoll _setInputPoll;
    private readonly SetInputState _setInputState;
    private readonly SetLog _setLog;
    private readonly DebugUInt32? _debugArmPc;
    private readonly DebugUInt32? _debugArmCpsr;
    private readonly DebugUInt32? _debugArmInstructions;
    private readonly DebugUInt32? _debugArmStateHash;
    private readonly DebugUInt8? _debugCdStatus;
    private readonly DebugUInt32? _debugCdPoll;
    private readonly DebugUInt32? _debugCdCommandCount;
    private readonly DebugUInt32? _debugCdCommandHash;
    private readonly DebugUInt32Index? _debugCdCommandAt;
    private readonly DebugUInt32Index32? _debugCdCycleAt;
    private readonly SetSeed? _debugSetSeed;

    private readonly VideoCallback _videoCallback;
    private readonly AudioCallback _audioCallback;
    private readonly InputPollCallback _inputPollCallback;
    private readonly InputStateCallback _inputStateCallback;
    private readonly LogCallback _logCallback;

    private int _frame;
    private int _inputFrame;
    private int _inputIndex;
    private ControllerButton _currentButtons;
    private uint _lastFrameHash;
    private int _lastWidth;
    private int _lastHeight;
    private int _pendingFrame;
    private int _stopRequested;
    private volatile string? _callbackError;
    private bool _disposed;
    private uint[] _lastImagePixels = [];
    private int _lastImageWidth;
    private int _lastImageHeight;
    private int _lastImageStride;

    public OperaCoreHost(
        string corePath,
        string isoPath,
        int targetFrames,
        int[] checkpoints,
        IReadOnlyList<InputScriptEntry>? script,
        int timeoutSeconds,
        uint seed,
        string logPath,
        string? imageDirectory)
    {
        _isoPath = isoPath;
        _targetFrames = targetFrames;
        _checkpoints = checkpoints;
        _script = script;
        _timeoutSeconds = timeoutSeconds;
        _seed = seed;
        _logPath = logPath;
        _imageDirectory = imageDirectory;

        _library = NativeLibrary.Load(corePath);
        _apiVersion = Load<ApiVersion>("sg_api_version");
        _init = Load<Init>("sg_init");
        _deinit = Load<Deinit>("sg_deinit");
        _loadGame = Load<LoadGame>("sg_load_game");
        _unloadGame = Load<UnloadGame>("sg_unload_game");
        _getAvInfo = Load<GetAvInfo>("sg_get_av_info");
        _run = Load<Run>("sg_run");
        _stop = Load<Stop>("sg_stop");
        _setVideo = Load<SetVideo>("sg_set_video_refresh");
        _setAudio = Load<SetAudio>("sg_set_audio_sample_batch");
        _setInputPoll = Load<SetInputPoll>("sg_set_input_poll");
        _setInputState = Load<SetInputState>("sg_set_input_state");
        _setLog = Load<SetLog>("sg_set_log");
        _debugArmPc = TryLoad<DebugUInt32>("sg_debug_arm_pc");
        _debugArmCpsr = TryLoad<DebugUInt32>("sg_debug_arm_cpsr");
        _debugArmInstructions = TryLoad<DebugUInt32>("sg_debug_arm_instructions");
        _debugArmStateHash = TryLoad<DebugUInt32>("sg_debug_arm_state_hash");
        _debugCdStatus = TryLoad<DebugUInt8>("sg_debug_cd_status");
        _debugCdPoll = TryLoad<DebugUInt32>("sg_debug_cd_poll");
        _debugCdCommandCount = TryLoad<DebugUInt32>("sg_debug_cd_command_count");
        _debugCdCommandHash = TryLoad<DebugUInt32>("sg_debug_cd_command_hash");
        _debugCdCommandAt = TryLoad<DebugUInt32Index>("sg_debug_cd_command_at");
        _debugCdCycleAt = TryLoad<DebugUInt32Index32>("sg_debug_cd_cycle_at");
        _debugSetSeed = TryLoad<SetSeed>("sg_debug_set_seed");

        _videoCallback = OnVideo;
        _audioCallback = OnAudio;
        _inputPollCallback = OnInputPoll;
        _inputStateCallback = OnInputState;
        _logCallback = OnLog;
    }

    public CaptureResult Execute()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var version = _apiVersion();
            if ((version >> 16) != 1)
            {
                return Failure($"Unsupported OperaCore ABI version {version >> 16}.{version & 0xffff}.");
            }

            _setVideo(_videoCallback);
            _setAudio(_audioCallback);
            _setInputPoll(_inputPollCallback);
            _setInputState(_inputStateCallback);
            _setLog(_logCallback);

            if (_debugSetSeed is null)
            {
                lock (_logLock)
                {
                    _log.Add(
                        "debug: sg_debug_set_seed is unavailable; reference PRNG is wall-clock seeded.");
                }
            }
            else
            {
                _debugSetSeed(_seed);
            }

            if (_init() == 0)
            {
                return Failure("OperaCore sg_init failed. Check that its BIOS/ directory contains a 1 MiB image.");
            }

            var pathPointer = Marshal.StringToCoTaskMemUTF8(_isoPath);
            try
            {
                var game = new SgGameInfo { Path = pathPointer, Data = IntPtr.Zero, Size = 0 };
                if (_loadGame(in game) == 0)
                {
                    return Failure("OperaCore sg_load_game failed.");
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(pathPointer);
            }

            _getAvInfo(out SgAvInfo video);
            _lastWidth = (int)video.BaseWidth;
            _lastHeight = (int)video.BaseHeight;

            var thread = new Thread(RunLoop) { IsBackground = true, Name = "OperaCore" };
            thread.Start();
            if (!thread.Join(_timeoutSeconds * 1000))
            {
                _stop();
                thread.Join(5000);
            }

            if (thread.IsAlive)
            {
                return Failure("OperaCore did not stop after the timeout.");
            }

            FinalizePending();
            var (cdCommands, cdCommandCycles) = ReadCdCommands();

            clock.Stop();
            var samples = _checkpoints
                .Where(_samples.ContainsKey)
                .Select(frame => _samples[frame])
                .ToArray();
            var missing = _checkpoints.Where(frame => !_samples.ContainsKey(frame)).ToArray();
            if (_callbackError is not null)
            {
                return Failure(_callbackError);
            }

            if (missing.Length > 0)
            {
                return Failure(
                    $"OperaCore did not reach checkpoint(s) {string.Join(',', missing)}; "
                    + $"reached {Volatile.Read(ref _frame)} frame(s).");
            }

            return new CaptureResult
            {
                Engine = "opera",
                Ok = true,
                Disc = _isoPath,
                FramesRequested = _targetFrames,
                FramesRun = Volatile.Read(ref _frame),
                ElapsedSeconds = clock.Elapsed.TotalSeconds,
                Width = _lastWidth,
                Height = _lastHeight,
                Checkpoints = samples,
                Warnings = SnapshotLog(),
                LogPath = _logPath,
                CdCommands = cdCommands,
                CdCommandCycles = cdCommandCycles,
            };
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return Failure(exception.GetType().Name + ": " + exception.Message);
        }
        finally
        {
            WriteLog();
            if (!_disposed)
            {
                try
                {
                    _unloadGame();
                    _deinit();
                }
                catch (Exception exception) when (exception is InvalidOperationException or AccessViolationException)
                {
                    lock (_logLock)
                    {
                        _log.Add("teardown: " + exception.Message);
                    }
                }
            }
        }
    }

    private void RunLoop()
    {
        try
        {
            _run();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _callbackError = "OperaCore run loop failed: " + exception.Message;
            _stop();
        }
    }

    private void OnVideo(IntPtr data, uint width, uint height, nuint pitch)
    {
        if (_callbackError is not null)
        {
            return;
        }

        try
        {
            var frame = Interlocked.Increment(ref _frame);
            if (_pendingFrame > 0)
            {
                FinalizePending();
            }

            var stride = checked((int)(pitch / sizeof(uint)));
            unsafe
            {
                var span = new ReadOnlySpan<uint>((void*)data, checked(stride * (int)height));
                _lastFrameHash = FrameHasher.HashXrgb8888(span, (int)width, (int)height, stride);
                if (_imageDirectory is not null && Array.IndexOf(_checkpoints, frame) >= 0)
                {
                    _lastImagePixels = span.ToArray();
                    _lastImageWidth = (int)width;
                    _lastImageHeight = (int)height;
                    _lastImageStride = stride;
                }
            }

            _lastWidth = (int)width;
            _lastHeight = (int)height;
            if (Array.IndexOf(_checkpoints, frame) >= 0)
            {
                _pendingFrame = frame;
            }

            if (frame >= _targetFrames)
            {
                RequestStop();
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _callbackError = "video callback failed: " + exception.Message;
            RequestStop();
        }
    }

    private nuint OnAudio(IntPtr data, nuint frames)
    {
        try
        {
            var count = checked((int)frames);
            unsafe
            {
                var span = new ReadOnlySpan<short>((void*)data, count * 2);
                _pcm.Add(span);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _callbackError = "audio callback failed: " + exception.Message;
            RequestStop();
        }

        return frames;
    }

    private void OnInputPoll()
    {
        var frame = _inputFrame++;
        if (_script is null || _script.Count == 0)
        {
            _currentButtons = ControllerButton.None;
            return;
        }

        while (_inputIndex < _script.Count && _script[_inputIndex].Frame <= frame)
        {
            _currentButtons = _script[_inputIndex].State.Buttons;
            _inputIndex++;
        }
    }

    private short OnInputState(uint port, uint device, uint id)
    {
        if (port != 0 || device != 1)
        {
            return 0;
        }

        ControllerButton button = id switch
        {
            SgJoypadUp => ControllerButton.Up,
            SgJoypadDown => ControllerButton.Down,
            SgJoypadLeft => ControllerButton.Left,
            SgJoypadRight => ControllerButton.Right,
            SgJoypadB => ControllerButton.B,
            SgJoypadY => ControllerButton.A,
            SgJoypadA => ControllerButton.C,
            SgJoypadStart => ControllerButton.P,
            SgJoypadSelect => ControllerButton.X,
            SgJoypadL => ControllerButton.L,
            SgJoypadR => ControllerButton.R,
            _ => ControllerButton.None,
        };
        return button != ControllerButton.None && _currentButtons.HasFlag(button) ? (short)1 : (short)0;
    }

    private void OnLog(int level, IntPtr message)
    {
        var text = Marshal.PtrToStringUTF8(message) ?? string.Empty;
        lock (_logLock)
        {
            _log.Add($"[{level}] {text}");
            if (_log.Count > 2000)
            {
                _log.RemoveRange(0, 500);
            }
        }
    }

    private void FinalizePending()
    {
        var frame = _pendingFrame;
        if (frame <= 0 || _samples.ContainsKey(frame))
        {
            _pendingFrame = 0;
            return;
        }

        _samples[frame] = new CaptureSample
        {
            Frame = frame,
            Pc = _debugArmPc?.Invoke(),
            Cpsr = _debugArmCpsr?.Invoke(),
            Instructions = _debugArmInstructions is null
                ? null
                : _debugArmInstructions.Invoke(),
            FrameHash = _lastFrameHash,
            Samples = _pcm.SampleCount,
            AudioHash = _pcm.Hash,
            ArmStateHash = _debugArmStateHash?.Invoke(),
            CdStatus = _debugCdStatus?.Invoke(),
            CdPoll = _debugCdPoll is null ? null : checked((int)_debugCdPoll.Invoke()),
            CdCommandCount = _debugCdCommandCount is null ? null : checked((int)_debugCdCommandCount.Invoke()),
            CdCommandHash = _debugCdCommandHash?.Invoke(),
            ImagePath = WritePendingImage(frame),
        };
        _pendingFrame = 0;
    }

    private string? WritePendingImage(int frame)
    {
        if (_imageDirectory is null || _lastImagePixels.Length == 0)
        {
            return null;
        }

        var path = Path.Combine(_imageDirectory, $"f{frame:D6}.bmp");
        HarnessCommon.WriteBmp(
            path,
            _lastImagePixels,
            _lastImageWidth,
            _lastImageHeight,
            _lastImageStride);
        _lastImagePixels = [];
        return path;
    }

    private void RequestStop()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) == 0)
        {
            _stop();
        }
    }

    private CaptureResult Failure(string error)
    {
        return new CaptureResult
        {
            Engine = "opera",
            Ok = false,
            Error = error,
            Disc = _isoPath,
            FramesRequested = _targetFrames,
            FramesRun = Volatile.Read(ref _frame),
            Width = _lastWidth == 0 ? null : _lastWidth,
            Height = _lastHeight == 0 ? null : _lastHeight,
            Checkpoints = _checkpoints
                .Where(_samples.ContainsKey)
                .Select(frame => _samples[frame])
                .ToArray(),
            Warnings = SnapshotLog(),
            LogPath = _logPath,
        };
    }

    private (byte[] Opcodes, uint[] Cycles) ReadCdCommands()
    {
        if (_debugCdCommandAt is null || _debugCdCommandCount is null)
        {
            return ([], []);
        }

        var count = _debugCdCommandCount();
        if (count == 0)
        {
            return ([], []);
        }

        var commands = new byte[count];
        var cycles = new uint[count];
        for (uint index = 0; index < count; index++)
        {
            commands[index] = _debugCdCommandAt(index);
            cycles[index] = _debugCdCycleAt?.Invoke(index) ?? 0;
        }

        return (commands, cycles);
    }

    private string[] SnapshotLog()
    {
        lock (_logLock)
        {
            return [.. _log];
        }
    }

    private void WriteLog()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_logPath))!);
            File.WriteAllLines(_logPath, SnapshotLog());
        }
        catch (IOException)
        {
            // Logging is diagnostic; never fail the capture because the log is unavailable.
        }
    }

    private T Load<T>(string export)
        where T : Delegate
    {
        return Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_library, export));
    }

    private T? TryLoad<T>(string export)
        where T : Delegate
    {
        return NativeLibrary.TryGetExport(_library, export, out var address)
            ? Marshal.GetDelegateForFunctionPointer<T>(address)
            : null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        NativeLibrary.Free(_library);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SgGameInfo
    {
        public IntPtr Path;
        public IntPtr Data;
        public nuint Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SgAvInfo
    {
        public uint BaseWidth;
        public uint BaseHeight;
        public uint MaxWidth;
        public uint MaxHeight;
        public double AspectRatio;
        public double Fps;
        public double SampleRate;
        public int PixelFormat;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint ApiVersion();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int Init();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void Deinit();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int LoadGame(in SgGameInfo game);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void UnloadGame();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GetAvInfo(out SgAvInfo info);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void Run();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void Stop();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetVideo(VideoCallback callback);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetAudio(AudioCallback callback);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetInputPoll(InputPollCallback callback);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetInputState(InputStateCallback callback);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetLog(LogCallback callback);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void VideoCallback(IntPtr data, uint width, uint height, nuint pitch);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nuint AudioCallback(IntPtr data, nuint frames);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void InputPollCallback();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate short InputStateCallback(uint port, uint device, uint id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void LogCallback(int level, IntPtr message);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint DebugUInt32();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetSeed(uint seed);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte DebugUInt32Index(uint index);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint DebugUInt32Index32(uint index);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte DebugUInt8();
}
