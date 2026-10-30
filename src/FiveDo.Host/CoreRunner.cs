using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using FiveDo.Core;
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
    private readonly ManualResetEventSlim _paused = new(false);
    private readonly ManualResetEventSlim _wake = new(false);
    private readonly Lock _invokeGate = new();
    private Thread? _thread;
    private volatile bool _stop;
    private volatile bool _pauseRequested;
    private long _frames;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="CoreRunner"/> class.</summary>
    /// <param name="machine">Machine to run. The runner does not own it.</param>
    /// <param name="frameSink">Optional per-frame sink, invoked on the core thread.</param>
    /// <param name="statusSink">Optional low-rate status sink, invoked on the core thread.</param>
    public CoreRunner(FiveDoMachine machine, Action<VdlFrame>? frameSink = null, Action<CoreStatus>? statusSink = null)
    {
        ArgumentNullException.ThrowIfNull(machine);
        _machine = machine;
        _frameSink = frameSink;
        _statusSink = statusSink;
    }

    /// <summary>Raised on the core thread when the emulation loop faults.</summary>
    public event EventHandler<CoreFaultedEventArgs>? Faulted;

    /// <summary>Gets a value indicating whether the core thread is running.</summary>
    public bool IsRunning => _thread is { IsAlive: true };

    /// <summary>Gets the number of frames executed by this runner.</summary>
    public long Frames => Interlocked.Read(ref _frames);

    /// <summary>Starts the core thread. Does nothing when already running.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning)
        {
            return;
        }

        _stop = false;
        _pauseRequested = false;
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "3DO core",
        };
        _thread.Start();
    }

    /// <summary>Pauses the core loop after the current frame.</summary>
    public void Pause()
    {
        if (!IsRunning)
        {
            return;
        }

        _pauseRequested = true;
        _paused.Wait();
    }

    /// <summary>Resumes a paused core loop.</summary>
    public void Resume()
    {
        if (!IsRunning)
        {
            return;
        }

        _pauseRequested = false;
        _wake.Set();
    }

    /// <summary>Stops the core thread and waits briefly for it to exit.</summary>
    public void Stop()
    {
        _stop = true;
        _wake.Set();
        Thread? thread = _thread;
        _thread = null;
        if (thread is not null && thread.IsAlive && thread != Thread.CurrentThread)
        {
            _ = thread.Join(TimeSpan.FromSeconds(2));
        }
    }

    /// <summary>
    /// Runs an action while the core loop is paused, then resumes it.
    /// </summary>
    /// <param name="action">Action to run. It may touch the machine directly.</param>
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_thread is null || !_thread.IsAlive || _thread == Thread.CurrentThread)
        {
            action();
            return;
        }

        lock (_invokeGate)
        {
            var wasPaused = _pauseRequested;
            _pauseRequested = true;
            _paused.Wait();
            try
            {
                action();
            }
            finally
            {
                _pauseRequested = wasPaused;
                if (!wasPaused)
                {
                    _wake.Set();
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
            _machine.RunFrame();
            Interlocked.Increment(ref _frames);
            _frameSink?.Invoke(_machine.Frame);
        });
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
        _paused.Dispose();
        _wake.Dispose();
    }

    private void Run()
    {
        TimerResolution.Begin();
        try
        {
            var pacer = new FramePacer(60.0);
            var audioPacer = new AudioFramePacer();
            var statusClock = Stopwatch.StartNew();
            var statusFrames = 0L;
            TimeSpan statusStart = statusClock.Elapsed;

            while (!_stop)
            {
                if (_pauseRequested)
                {
                    _paused.Set();
                    _wake.Wait();
                    _wake.Reset();
                    _paused.Reset();
                    pacer.Resync();
                    audioPacer.Resync(_machine.AudioSamplesProduced);
                    statusClock.Restart();
                    statusStart = TimeSpan.Zero;
                    statusFrames = 0;
                    continue;
                }

                _machine.RunFrame();
                Interlocked.Increment(ref _frames);
                _frameSink?.Invoke(_machine.Frame);
                audioPacer.FrameCompleted(_machine.AudioSamplesProduced);
                pacer.SetPeriod(audioPacer.FramePeriodTicks);
                statusFrames++;

                TimeSpan elapsed = statusClock.Elapsed;
                if (elapsed - statusStart >= TimeSpan.FromMilliseconds(250))
                {
                    var seconds = (elapsed - statusStart).TotalSeconds;
                    _statusSink?.Invoke(new CoreStatus(
                        Frames,
                        _machine.Cpu.ProgramCounter,
                        _machine.LastFrameCycles,
                        seconds > 0 ? statusFrames / seconds : 0,
                        audioPacer.FramesPerSecond));
                    statusStart = elapsed;
                    statusFrames = 0;
                }

                pacer.Wait();
            }
        }
        catch (Exception exception)
        {
            _statusSink?.Invoke(new CoreStatus(
                Frames,
                _machine.Cpu.ProgramCounter,
                _machine.LastFrameCycles,
                0,
                0));
            Faulted?.Invoke(this, new CoreFaultedEventArgs(exception));
            Serilog.Log.Error(exception, "Core thread stopped.");
        }
        finally
        {
            TimerResolution.End();
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

        internal void Resync() => _next = _clock.ElapsedTicks;

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
