using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using FiveDo.Core;
using FiveDo.Host.Rewind;
using FiveDo.Input;
using FiveDo.Machine;
using FiveDo.Video;

namespace FiveDo.Host;

/// <summary>
/// Runs the emulated machine on a dedicated thread with a 60 Hz frame pacer.
/// </summary>
/// <remarks>
/// <para>
/// The machine is not thread-safe. All access must go through <see cref="Invoke"/>, which
/// pauses the loop, runs the action on the caller's thread, and resumes the loop. The UI uses
/// this for reset, save/load, disc changes, and screenshots.
/// </para>
/// <para>
/// Frames are handed to the optional sink on the core thread. The sink must be thread-safe;
/// <see cref="VdlFrameMailbox"/> and the Avalonia video surface both are.
/// </para>
/// </remarks>
public sealed class CoreRunner : IDisposable
{
    private readonly FiveDoMachine _machine;
    private readonly Action<VdlFrame>? _frameSink;
    private readonly Action<CoreStatus>? _statusSink;
    private readonly RewindBuffer _rewind;
    private readonly MachineStateSnapshot _runAheadSnapshot = new();
    private readonly LatencyTelemetry _latency = new();
    private readonly object _stateGate = new();
    private readonly Lock _invokeGate = new();
    private Thread? _thread;
    private bool _stop;
    private bool _pauseRequested;
    private bool _paused;
    private bool _faulted;
    private long _frames;
    private long _presentedFrames;
    private int _frameSkipMode = (int)FrameSkipMode.Auto;
    private int _speedMultiplier = 1;
    private int _runAheadFrames;
    private int _runAheadOverruns;
    private bool _runAheadAutoDisabled;
    private int _frameDelayFrames;
    private VdlFrame[] _frameDelayBuffer = [];
    private long[] _frameDelayInputTimestamps = [];
    private int _frameDelayWrite;
    private int _frameDelayFilled;
    private int _frameDelayConfigured;
    private long _lastInputPollTimestamp;
    private long _lastPresentedMachineFrame = -1;
    private IPbusSource? _originalPbusSource;
    private TimestampedPbusSource? _timestampedPbusSource;
    private long _frameSkipIndex;
    private int _autoSkipLevel;
    private int _autoBehindFrames;
    private int _autoRecoverFrames;
    private bool _disposed;
    private ControllerState _lastPlayer1;
    private bool _trayOpen;
    private bool _discPresent;
    private int _guestEjectCount;
    private long _lagFrames;
    private long _lastPbusReadCount;
    private bool _lastFrameWasLag;
    private int _inputPlayer = 1;

    /// <summary>Initializes a new instance of the <see cref="CoreRunner"/> class.</summary>
    /// <param name="machine">Machine to run. The runner does not own it.</param>
    /// <param name="frameSink">Optional per-frame sink, invoked on the core thread.</param>
    /// <param name="statusSink">Optional low-rate status sink, invoked on the core thread.</param>
    /// <param name="rewindOptions">Optional rewind-history configuration. Null uses the defaults.</param>
    public CoreRunner(
        FiveDoMachine machine,
        Action<VdlFrame>? frameSink = null,
        Action<CoreStatus>? statusSink = null,
        RewindOptions? rewindOptions = null)
    {
        ArgumentNullException.ThrowIfNull(machine);
        _machine = machine;
        _frameSink = frameSink;
        _statusSink = statusSink;
        _rewind = new RewindBuffer(machine, rewindOptions);
    }

    /// <summary>Raised on the core thread when the emulation loop faults.</summary>
    public event EventHandler<CoreFaultedEventArgs>? Faulted;

    /// <summary>
    /// Raised on the core thread when the guest issues a CD eject command.
    /// </summary>
    /// <remarks>
    /// Frontends use this for ODE-style disc-swap prompts. Handlers must not block:
    /// marshal UI work to the UI thread and return promptly.
    /// </remarks>
    public event EventHandler? GuestDiscEjected;

    /// <summary>Gets the number of frames whose guest did not poll the controller.</summary>
    public long LagFrames => Interlocked.Read(ref _lagFrames);

    /// <summary>Gets a value indicating whether the most recent frame was a lag frame.</summary>
    public bool LastFrameWasLag => _lastFrameWasLag;

    /// <summary>Gets or sets the one-based controller port captured for the input display.</summary>
    public int InputPlayer
    {
        get => _inputPlayer;
        set
        {
            if (value is < 1 or > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The input display player must be 1-6.");
            }

            _inputPlayer = value;
        }
    }

    /// <summary>Gets a value indicating whether the core thread is running.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_stateGate)
            {
                return _thread is { IsAlive: true } && !_faulted;
            }
        }
    }

    /// <summary>Gets the number of frames executed by this runner.</summary>
    public long Frames => Interlocked.Read(ref _frames);

    /// <summary>Gets the number of frames rendered and presented by this runner.</summary>
    public long PresentedFrames => Interlocked.Read(ref _presentedFrames);

    /// <summary>
    /// Gets or sets the presentation frame-skip policy. The default is
    /// <see cref="FrameSkipMode.Auto"/>.
    /// </summary>
    /// <remarks>
    /// Only presentation is skipped. The emulated machine still executes every frame,
    /// so game speed, audio, input, and save states are unchanged. The setting can be
    /// changed while the core loop is running.
    /// </remarks>
    public FrameSkipMode FrameSkip
    {
        get => (FrameSkipMode)Volatile.Read(ref _frameSkipMode);
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown frame-skip mode.");
            }

            Volatile.Write(ref _frameSkipMode, (int)value);
        }
    }

    /// <summary>
    /// Gets or sets the fast-forward multiplier. The default is 1 (normal speed).
    /// </summary>
    /// <remarks>
    /// Values from 2 to 10 run that many emulated frames per presentation tick. Intermediate
    /// frames skip VDLP rendering, and audio is muted while the multiplier is above one.
    /// Emulated state and input are unchanged.
    /// </remarks>
    public int SpeedMultiplier
    {
        get => Volatile.Read(ref _speedMultiplier);
        set
        {
            if (value is < 1 or > 10)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The speed multiplier must be between 1 and 10.");
            }

            Volatile.Write(ref _speedMultiplier, value);
        }
    }

    /// <summary>
    /// Gets or sets the number of speculative frames run ahead of presentation (0-2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Run-ahead saves the machine state every displayed frame, runs the configured number
    /// of hidden frames, and then re-runs the visible frame from the saved point. The result
    /// is that the presented frame is based on the most recent input rather than input
    /// sampled one or two frames earlier, at the cost of one extra emulated frame per
    /// configured depth.
    /// </para>
    /// <para>
    /// The default is zero. Rewind recording and deterministic movie/TAS playback are not
    /// compatible with run-ahead; the frontend disables those features while it is active.
    /// </para>
    /// </remarks>
    public int RunAhead
    {
        get => Volatile.Read(ref _runAheadFrames);
        set
        {
            if (value is < 0 or > AppSettings.MaxRunAheadFrames)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"The run-ahead depth must be between 0 and {AppSettings.MaxRunAheadFrames} frames.");
            }

            Volatile.Write(ref _runAheadFrames, value);
            _runAheadOverruns = 0;
            _runAheadAutoDisabled = false;
            _runAheadSnapshot.Invalidate();
        }
    }

    /// <summary>Gets a value indicating whether run-ahead disabled itself because frames overran the budget.</summary>
    public bool RunAheadAutoDisabled => Volatile.Read(ref _runAheadAutoDisabled);

    /// <summary>
    /// Gets or sets the number of frames held by the presenter before submission (0-8).
    /// </summary>
    /// <remarks>
    /// A positive delay intentionally increases input-to-photon latency. It is useful for
    /// matching a high-latency audio path or inspecting a frame while the game continues to
    /// run. Zero is the default and keeps the render-on-demand path unchanged.
    /// </remarks>
    public int FrameDelay
    {
        get => Volatile.Read(ref _frameDelayFrames);
        set
        {
            if (value is < 0 or > AppSettings.MaxFrameDelayFrames)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    $"The presentation frame delay must be between 0 and {AppSettings.MaxFrameDelayFrames} frames.");
            }

            Volatile.Write(ref _frameDelayFrames, value);
        }
    }

    /// <summary>Gets the rolling software input-poll-to-frame-submit telemetry.</summary>
    public LatencyTelemetry Latency => _latency;

    /// <summary>Gets a value indicating whether rewind history is being recorded.</summary>
    public bool RewindEnabled => _rewind.Enabled;

    /// <summary>Gets a value indicating whether at least one frame can currently be rewound.</summary>
    public bool CanRewind => _rewind.CanRewind;

    /// <summary>Gets the oldest frame retained by the rewind history, or -1 when empty.</summary>
    public long RewindOldestFrame => _rewind.OldestFrame;

    /// <summary>Gets the newest frame retained by the rewind history, or -1 when empty.</summary>
    public long RewindNewestFrame => _rewind.NewestFrame;

    /// <summary>Gets the configured rewind history length.</summary>
    public TimeSpan RewindWindow => _rewind.Window;

    /// <summary>Starts the core thread. Does nothing when already running.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_stateGate)
        {
            if (_thread is { IsAlive: true } && !_faulted)
            {
                return;
            }

            _stop = false;
            _pauseRequested = false;
            _paused = false;
            _faulted = false;
            _rewind.ResetHistory();
            _runAheadSnapshot.Invalidate();
            _runAheadOverruns = 0;
            _runAheadAutoDisabled = false;
            ResetFrameDelayBuffer();
            _latency.Reset();
            _lastPresentedMachineFrame = _machine.FramesExecuted;
            WrapPbusSource();
            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "3DO core",
            };
            _thread.Start();
        }
    }

    /// <summary>Pauses the core loop after the current frame.</summary>
    public void Pause()
    {
        lock (_stateGate)
        {
            if (_thread is not { IsAlive: true } || _stop || _thread == Thread.CurrentThread)
            {
                return;
            }

            _pauseRequested = true;
            Monitor.PulseAll(_stateGate);
            while (!_paused && !_stop && !_faulted)
            {
                Monitor.Wait(_stateGate);
            }
        }
    }

    /// <summary>Resumes a paused core loop.</summary>
    public void Resume()
    {
        lock (_stateGate)
        {
            if (_thread is not { IsAlive: true } || _faulted)
            {
                return;
            }

            _pauseRequested = false;
            Monitor.PulseAll(_stateGate);
        }
    }

    /// <summary>Stops the core thread and waits briefly for it to exit.</summary>
    public void Stop()
    {
        Thread? thread;
        lock (_stateGate)
        {
            _stop = true;
            _pauseRequested = false;
            Monitor.PulseAll(_stateGate);
            thread = _thread;
            _thread = null;
        }

        if (thread is not null && thread.IsAlive && thread != Thread.CurrentThread)
        {
            _ = thread.Join(TimeSpan.FromSeconds(2));
        }

        _rewind.Clear();
        UnwrapPbusSource();
    }

    /// <summary>
    /// Runs an action while the core loop is paused, then resumes it.
    /// </summary>
    /// <param name="action">Action to run. It may touch the machine directly.</param>
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_stateGate)
        {
            if (_thread is null || !_thread.IsAlive || _thread == Thread.CurrentThread || _stop || _faulted)
            {
                action();
                return;
            }
        }

        lock (_invokeGate)
        {
            bool wasPaused;
            lock (_stateGate)
            {
                if (_thread is null || !_thread.IsAlive || _stop || _faulted)
                {
                    action();
                    return;
                }

                wasPaused = _pauseRequested;
                _pauseRequested = true;
                Monitor.PulseAll(_stateGate);
                while (!_paused && !_stop && !_faulted)
                {
                    Monitor.Wait(_stateGate);
                }
            }

            try
            {
                action();
            }
            finally
            {
                // A state load, reset, or disc change can move the machine timeline.
                // Preserve the latency queue for ordinary pauses (screenshots, menu
                // toggles) and invalidate only when the frame counter actually jumped.
                if (_machine.FramesExecuted != _lastPresentedMachineFrame)
                {
                    InvalidateLatencyState();
                }

                lock (_stateGate)
                {
                    if (!wasPaused && !_stop)
                    {
                        _pauseRequested = false;
                        Monitor.PulseAll(_stateGate);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Runs exactly one emulated frame on the caller's thread.
    /// </summary>
    /// <remarks>
    /// The loop is paused for the duration of the frame and restored to its previous
    /// state afterward. The desktop shell pauses before calling this so frame advance
    /// leaves emulation stopped on the new frame.
    /// </remarks>
    public void StepFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Invoke(() =>
        {
            ConfigureFrameDelay();
            _machine.RunFrame();
            Interlocked.Increment(ref _frames);
            Interlocked.Increment(ref _presentedFrames);
            CaptureFrameState();
            _rewind.Capture(_machine.FramesExecuted);
            SubmitPresentedFrame(_machine.Frame, bypassDelay: false);
            _lastPresentedMachineFrame = _machine.FramesExecuted;
            _frameSkipIndex = 0;
            _runAheadSnapshot.Invalidate();
        });
    }

    /// <summary>
    /// Rewinds the machine by a number of frames and presents the restored frame.
    /// </summary>
    /// <param name="frames">Number of frames to rewind. The request is clamped to the retained history.</param>
    /// <returns><see langword="true"/> when the machine was restored; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="frames"/> is negative.</exception>
    public bool Rewind(int frames)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        var rewound = false;
        Invoke(() =>
        {
            rewound = _rewind.Rewind(frames);
            if (!rewound)
            {
                return;
            }

            Interlocked.Exchange(ref _frames, _machine.FramesExecuted);
            InvalidateLatencyState();
            _frameSink?.Invoke(_machine.Frame);
            Interlocked.Increment(ref _presentedFrames);
            _lastPresentedMachineFrame = _machine.FramesExecuted;
            _frameSkipIndex = 0;
            _autoSkipLevel = 0;
            _autoBehindFrames = 0;
            _autoRecoverFrames = 0;
        });

        return rewound;
    }

    /// <summary>Clears the rewind history and records the current machine state.</summary>
    public void ResetRewind()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Invoke(_rewind.ResetHistory);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _rewind.Dispose();
    }

    private void Run()
    {
        TimerResolution.Begin();
        try
        {
            var pacer = new FramePacer(_machine.FramesPerSecond);
            var audioPacer = new AudioFramePacer(_machine.FramesPerSecond);
            var presentedAudioSamples = _machine.AudioSamplesProduced;
            var statusClock = Stopwatch.StartNew();
            var statusFrames = 0L;
            var statusPresentedFrames = 0L;
            var previousMode = FrameSkipMode.Off;
            var previousSpeed = 1;
            TimeSpan statusStart = statusClock.Elapsed;

            while (true)
            {
                lock (_stateGate)
                {
                    while (_pauseRequested && !_stop)
                    {
                        _paused = true;
                        Monitor.PulseAll(_stateGate);
                        while (_pauseRequested && !_stop)
                        {
                            Monitor.Wait(_stateGate);
                        }

                        _paused = false;
                        Monitor.PulseAll(_stateGate);
                        pacer.Resync();
                        presentedAudioSamples = _machine.AudioSamplesProduced;
                        audioPacer.Resync(presentedAudioSamples);
                        statusClock.Restart();
                        statusStart = TimeSpan.Zero;
                        statusFrames = 0;
                        statusPresentedFrames = 0;
                        _frameSkipIndex = 0;
                        _autoSkipLevel = 0;
                        _autoBehindFrames = 0;
                        _autoRecoverFrames = 0;
                        previousMode = FrameSkip;
                        previousSpeed = SpeedMultiplier;
                    }

                    if (_stop)
                    {
                        return;
                    }
                }

                FrameSkipMode mode = FrameSkip;
                if (mode != previousMode)
                {
                    previousMode = mode;
                    _frameSkipIndex = 0;
                    _autoSkipLevel = 0;
                    _autoBehindFrames = 0;
                    _autoRecoverFrames = 0;
                }

                var speed = SpeedMultiplier;
                if (speed != previousSpeed)
                {
                    previousSpeed = speed;
                    pacer.Resync();
                    presentedAudioSamples = _machine.AudioSamplesProduced;
                    audioPacer.Resync(presentedAudioSamples);
                    _runAheadSnapshot.Invalidate();
                }

                var runAhead = speed == 1 ? RunAhead : 0;
                var present = FrameSkipPolicy.ShouldPresent(mode, _frameSkipIndex, _autoSkipLevel + 1);
                if (runAhead > 0)
                {
                    // Run-ahead owns the timing-sensitive path. Frame skipping would
                    // leave the snapshot and the visible timeline out of step, so the
                    // latency feature always presents every displayed frame.
                    present = true;
                }

                ConfigureFrameDelay();
                var frameStart = Stopwatch.GetTimestamp();
                IAudioSink? mutedAudio = speed > 1 ? _machine.AudioSink : null;
                if (mutedAudio is not null)
                {
                    _machine.AudioSink = null;
                }

                try
                {
                    if (runAhead > 0)
                    {
                        bool ranAhead = RunAheadFrame(runAhead, mutedAudio, out long visibleSamples);
                        if (!ranAhead)
                        {
                            // The snapshot path failed (for example an incompatible
                            // state section). Fall back to one normal frame so the
                            // session keeps running, and try again next iteration.
                            long beforeFallback = _machine.AudioSamplesProduced;
                            _machine.RunFrame(false);
                            visibleSamples = _machine.AudioSamplesProduced - beforeFallback;
                            runAhead = 0;
                        }

                        Interlocked.Increment(ref _frames);
                        CaptureFrameState();
                        // Rewind history cannot follow the speculative timeline. The
                        // frontend disables it while run-ahead is active; keep the
                        // buffer clear here as a safety net.
                        _rewind.Clear();
                        presentedAudioSamples += visibleSamples;
                        audioPacer.FrameCompleted(presentedAudioSamples);
                        Interlocked.Increment(ref _presentedFrames);
                        SubmitPresentedFrame(_machine.Frame, bypassDelay: false);
                        statusPresentedFrames++;
                        _lastPresentedMachineFrame = _machine.FramesExecuted;
                    }
                    else
                    {
                        for (var step = 0; step < speed; step++)
                        {
                            var presentStep = present && step == speed - 1;
                            long audioBefore = _machine.AudioSamplesProduced;
                            _machine.RunFrame(!presentStep);
                            Interlocked.Increment(ref _frames);
                            CaptureFrameState();
                            _rewind.Capture(_machine.FramesExecuted);
                            presentedAudioSamples += _machine.AudioSamplesProduced - audioBefore;
                            audioPacer.FrameCompleted(presentedAudioSamples);
                            if (!presentStep)
                            {
                                continue;
                            }

                            Interlocked.Increment(ref _presentedFrames);
                            SubmitPresentedFrame(_machine.Frame, bypassDelay: speed > 1);
                            statusPresentedFrames++;
                            _lastPresentedMachineFrame = _machine.FramesExecuted;
                        }
                    }
                }
                finally
                {
                    if (mutedAudio is not null)
                    {
                        mutedAudio.Reset();
                        _machine.AudioSink = mutedAudio;
                    }
                }

                var frameDuration = Stopwatch.GetTimestamp() - frameStart;
                pacer.SetPeriod(audioPacer.FramePeriodTicks);
                if (runAhead > 0 && pacer.PeriodTicks > 0)
                {
                    // Run-ahead doubles or triples core work. If a title cannot hold
                    // the audio-derived deadline, disable it instead of producing a
                    // permanently late 50 Hz presentation.
                    if (frameDuration > pacer.PeriodTicks * 1.35)
                    {
                        if (++_runAheadOverruns >= 45)
                        {
                            Volatile.Write(ref _runAheadFrames, 0);
                            Volatile.Write(ref _runAheadAutoDisabled, true);
                            _runAheadOverruns = 0;
                            Serilog.Log.Warning(
                                "Run-ahead disabled automatically after repeated frame overruns.");
                        }
                    }
                    else
                    {
                        _runAheadOverruns = 0;
                    }
                }
                else
                {
                    _runAheadOverruns = 0;
                }

                statusFrames += speed;
                _frameSkipIndex += speed;

                TimeSpan elapsed = statusClock.Elapsed;
                if (elapsed - statusStart >= TimeSpan.FromMilliseconds(250))
                {
                    var seconds = (elapsed - statusStart).TotalSeconds;
                    _statusSink?.Invoke(new CoreStatus(
                        Frames,
                        _machine.Cpu.ProgramCounter,
                        _machine.LastFrameCycles,
                        seconds > 0 ? statusFrames / seconds : 0,
                        audioPacer.FramesPerSecond)
                    {
                        PresentedFramesPerSecond = seconds > 0 ? statusPresentedFrames / seconds : 0,
                        FrameSkipMode = mode,
                        FrameSkipCadence = runAhead > 0
                            ? 1
                            : mode == FrameSkipMode.Auto ? _autoSkipLevel + 1 : FrameSkipPolicy.Cadence(mode),
                        Player1 = _lastPlayer1,
                        InputPlayer = _inputPlayer,
                        SkippedFrames = Frames - PresentedFrames,
                        LagFrames = LagFrames,
                        IsLagFrame = _lastFrameWasLag,
                        TrayOpen = _trayOpen,
                        DiscPresent = _discPresent,
                        RunAheadFrames = runAhead,
                        RunAheadAutoDisabled = _runAheadAutoDisabled,
                        FrameDelayFrames = _frameDelayConfigured,
                        InputLatencyMilliseconds = _latency.MeanMilliseconds,
                        InputLatencyMaxMilliseconds = _latency.MaxMilliseconds,
                        LatencySamples = _latency.Samples,
                    });
                    statusStart = elapsed;
                    statusFrames = 0;
                    statusPresentedFrames = 0;
                }

                pacer.Wait();
                if (runAhead == 0)
                {
                    UpdateAutoFrameSkip(mode, present, frameDuration, pacer.PeriodTicks);
                }
            }
        }
        catch (Exception exception)
        {
            lock (_stateGate)
            {
                _faulted = true;
                Monitor.PulseAll(_stateGate);
            }

            _statusSink?.Invoke(new CoreStatus(
                Frames,
                _machine.Cpu.ProgramCounter,
                _machine.LastFrameCycles,
                0,
                0)
            {
                Player1 = _lastPlayer1,
                InputPlayer = _inputPlayer,
                SkippedFrames = Frames - PresentedFrames,
                LagFrames = LagFrames,
                IsLagFrame = _lastFrameWasLag,
                TrayOpen = _trayOpen,
                DiscPresent = _discPresent,
            });
            Faulted?.Invoke(this, new CoreFaultedEventArgs(exception));
            Serilog.Log.Error(exception, "Core thread stopped.");
        }
        finally
        {
            TimerResolution.End();
        }
    }

    /// <summary>
    /// Runs the hidden and visible frames for one run-ahead presentation.
    /// </summary>
    /// <param name="depth">Configured run-ahead depth, 1-2.</param>
    /// <param name="mutedAudio">Audio sink temporarily removed for fast-forward, or null.</param>
    /// <param name="visibleSamples">Receives the DSP samples produced by the visible frame.</param>
    /// <returns><see langword="true"/> when the visible frame ran; otherwise false.</returns>
    private bool RunAheadFrame(int depth, IAudioSink? mutedAudio, out long visibleSamples)
    {
        visibleSamples = 0;
        IAudioSink? audioSink = mutedAudio ?? _machine.AudioSink;
        IFrameSink? frameSink = _machine.FrameSink;
        try
        {
            if (!_runAheadSnapshot.IsValid)
            {
                _runAheadSnapshot.Capture(_machine);
            }

            // Hidden frames must be silent and must not reach the host frame sink.
            _machine.AudioSink = null;
            _machine.FrameSink = null;

            // Restore the speculative start point, run the first hidden frame, and
            // keep its result as the next iteration's start. This advances the
            // visible timeline by exactly one frame while the displayed state stays
            // 'depth' frames ahead.
            //
            // Hidden frames must render normally: VDLP's line delay and bitmap
            // cursors advance in DoLine, so a skip-frame shortcut would corrupt the
            // next visible field. The host frame sink is null instead, which keeps
            // the hidden output out of the presenter.
            _runAheadSnapshot.Restore(_machine);
            _machine.RunFrame(false);
            _runAheadSnapshot.Capture(_machine);
            for (var i = 1; i < depth; i++)
            {
                _machine.RunFrame(false);
            }

            _machine.AudioSink = audioSink;
            _machine.FrameSink = frameSink;
            long beforeVisible = _machine.AudioSamplesProduced;
            _machine.RunFrame(false);
            visibleSamples = _machine.AudioSamplesProduced - beforeVisible;
            return true;
        }
        catch (InvalidStateException exception)
        {
            Serilog.Log.Warning(exception, "Run-ahead state snapshot was rejected; falling back to the normal frame path.");
            _runAheadSnapshot.Invalidate();
            Volatile.Write(ref _runAheadFrames, 0);
            Volatile.Write(ref _runAheadAutoDisabled, true);
            return false;
        }
        finally
        {
            _machine.AudioSink = audioSink;
            _machine.FrameSink = frameSink;
        }
    }

    private void ConfigureFrameDelay()
    {
        int delay = FrameDelay;
        if (delay == _frameDelayConfigured)
        {
            return;
        }

        _frameDelayConfigured = delay;
        if (delay == 0)
        {
            _frameDelayBuffer = [];
            _frameDelayInputTimestamps = [];
        }
        else
        {
            var frames = new VdlFrame[delay + 1];
            for (var i = 0; i < frames.Length; i++)
            {
                frames[i] = new VdlFrame();
            }

            _frameDelayBuffer = frames;
            _frameDelayInputTimestamps = new long[frames.Length];
        }

        _frameDelayWrite = 0;
        _frameDelayFilled = 0;
        Array.Clear(_frameDelayInputTimestamps);
    }

    private void ResetFrameDelayBuffer()
    {
        _frameDelayConfigured = -1;
        ConfigureFrameDelay();
    }

    private void InvalidateLatencyState()
    {
        _runAheadSnapshot.Invalidate();
        _lastInputPollTimestamp = 0;
        _latency.Reset();
        ResetFrameDelayBuffer();
    }

    /// <summary>Queues or submits the visible frame and records its software latency.</summary>
    /// <param name="frame">Completed frame owned by the machine; the delay path copies it.</param>
    /// <param name="bypassDelay">When true, submit immediately (fast-forward and stepping).</param>
    private void SubmitPresentedFrame(VdlFrame frame, bool bypassDelay)
    {
        var inputTimestamp = _lastInputPollTimestamp;
        if (bypassDelay || _frameDelayBuffer.Length == 0)
        {
            _frameSink?.Invoke(frame);
            RecordLatency(inputTimestamp);
            return;
        }

        _frameDelayBuffer[_frameDelayWrite].CopyFrom(frame);
        _frameDelayInputTimestamps[_frameDelayWrite] = inputTimestamp;
        _frameDelayWrite = (_frameDelayWrite + 1) % _frameDelayBuffer.Length;
        if (_frameDelayFilled < _frameDelayBuffer.Length)
        {
            _frameDelayFilled++;
            if (_frameDelayFilled < _frameDelayBuffer.Length)
            {
                return;
            }
        }

        // Once the ring is full, the write cursor points at the oldest frame.
        int oldest = _frameDelayWrite;
        _frameSink?.Invoke(_frameDelayBuffer[oldest]);
        RecordLatency(_frameDelayInputTimestamps[oldest]);
    }

    private void RecordLatency(long inputTimestamp)
    {
        if (inputTimestamp == 0)
        {
            return;
        }

        _latency.Record(inputTimestamp, Stopwatch.GetTimestamp());
    }

    private void WrapPbusSource()
    {
        if (_timestampedPbusSource is not null || _originalPbusSource is not null)
        {
            return;
        }

        _originalPbusSource = _machine.PbusSource;
        if (_originalPbusSource is null)
        {
            return;
        }

        _timestampedPbusSource = new TimestampedPbusSource(_originalPbusSource, this);
        _machine.PbusSource = _timestampedPbusSource;
    }

    private void UnwrapPbusSource()
    {
        if (_timestampedPbusSource is null)
        {
            return;
        }

        if (ReferenceEquals(_machine.PbusSource, _timestampedPbusSource))
        {
            _machine.PbusSource = _originalPbusSource;
        }

        _timestampedPbusSource = null;
        _originalPbusSource = null;
    }

    private void RecordInputPoll()
    {
        _lastInputPollTimestamp = Stopwatch.GetTimestamp();
    }

    private void CaptureFrameState()
    {
        _lastPlayer1 = PbusDecoder.DecodeController(_machine.Madam.PbusQueue, _inputPlayer - 1);
        long pbusReads = _machine.Madam.PbusReadCount;
        _lastFrameWasLag = pbusReads == _lastPbusReadCount;
        _lastPbusReadCount = pbusReads;
        if (_lastFrameWasLag)
        {
            Interlocked.Increment(ref _lagFrames);
        }

        bool wasTrayOpen = _trayOpen;
        _trayOpen = _machine.Cdrom.TrayOpen;
        _discPresent = _machine.Cdrom.DiscPresent;
        var ejects = _machine.Cdrom.GuestEjectCount;
        if (ejects != _guestEjectCount)
        {
            _guestEjectCount = ejects;
            if (_trayOpen && !wasTrayOpen)
            {
                GuestDiscEjected?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void UpdateAutoFrameSkip(FrameSkipMode mode, bool present, long frameDuration, double targetPeriod)
    {
        if (mode != FrameSkipMode.Auto)
        {
            _autoSkipLevel = 0;
            _autoBehindFrames = 0;
            _autoRecoverFrames = 0;
            return;
        }

        if (targetPeriod <= 0 || double.IsNaN(targetPeriod) || double.IsInfinity(targetPeriod))
        {
            return;
        }

        // Measure actual work per *presented* frame rather than the pacer's schedule
        // offset: a one-time startup stall leaves a constant offset, but it does not
        // mean the machine is still too slow. Skipping presentation only helps when
        // the frame work itself exceeds the budget.
        if (!present)
        {
            return;
        }

        if (frameDuration > targetPeriod * 1.05)
        {
            _autoRecoverFrames = 0;
            if (++_autoBehindFrames < 3)
            {
                return;
            }

            _autoBehindFrames = 0;
            if (_autoSkipLevel < 2)
            {
                _autoSkipLevel++;
                _frameSkipIndex = 0;
            }

            return;
        }

        if (frameDuration < targetPeriod * 0.90)
        {
            _autoBehindFrames = 0;
            if (++_autoRecoverFrames < 120)
            {
                return;
            }

            _autoRecoverFrames = 0;
            if (_autoSkipLevel > 0)
            {
                _autoSkipLevel--;
                _frameSkipIndex = 0;
            }

            return;
        }

        _autoBehindFrames = 0;
        _autoRecoverFrames = 0;
    }

    /// <summary>Records when the guest last read the controller packet.</summary>
    private sealed class TimestampedPbusSource : IPbusSource
    {
        private readonly IPbusSource _inner;
        private readonly CoreRunner _owner;

        internal TimestampedPbusSource(IPbusSource inner, CoreRunner owner)
        {
            _inner = inner;
            _owner = owner;
        }

        public int GetPbusData(Span<byte> destination)
        {
            _owner.RecordInputPoll();
            return _inner.GetPbusData(destination);
        }
    }

    /// <summary>
    /// A monotonic 60 Hz pacer. Sleeps for the bulk of the wait and spins for the last
    /// millisecond so frame times stay close to the target on Windows.
    /// </summary>
    private sealed class FramePacer
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private double _periodTicks;
        private double _next;

        internal double PeriodTicks => _periodTicks;

        internal FramePacer(double framesPerSecond)
        {
            _periodTicks = Stopwatch.Frequency / framesPerSecond;
            _next = _clock.ElapsedTicks;
        }

        internal void SetPeriod(double periodTicks)
        {
            if (periodTicks > 0 && !double.IsNaN(periodTicks) && !double.IsInfinity(periodTicks))
            {
                _periodTicks = periodTicks;
            }
        }

        internal void Resync()
        {
            _next = _clock.ElapsedTicks;
        }

        internal void Wait()
        {
            _next += _periodTicks;
            var remaining = _next - _clock.ElapsedTicks;
            var spinTicks = Stopwatch.Frequency / 1000.0;
            if (remaining > spinTicks * 2)
            {
                var sleepMs = (int)((remaining - spinTicks) * 1000.0 / Stopwatch.Frequency);
                if (sleepMs > 0)
                {
                    Thread.Sleep(sleepMs);
                }
            }

            while (_clock.ElapsedTicks < _next)
            {
                Thread.SpinWait(64);
            }

            // A long stall (debugger, GC pause, disc load) should not make the pacer chase
            // the past forever.
            if (_clock.ElapsedTicks - _next > Stopwatch.Frequency / 10.0)
            {
                _next = _clock.ElapsedTicks;
            }
        }
    }

    /// <summary>Requests 1 ms timer resolution on Windows for the duration of the run.</summary>
    private static class TimerResolution
    {
        private static int _depth;

        internal static void Begin()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            if (Interlocked.Increment(ref _depth) == 1)
            {
                _ = timeBeginPeriod(1);
            }
        }

        internal static void End()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            if (Interlocked.Decrement(ref _depth) == 0)
            {
                _ = timeEndPeriod(1);
            }
        }

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint timeBeginPeriod(uint period);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint timeEndPeriod(uint period);
    }
}
