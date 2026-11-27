using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FiveDo.Core;
using FiveDo.Host;
using FiveDo.Host.Netplay;
using FiveDo.Host.Osd;
using FiveDo.Input;
using FiveDo.Storage;
using FiveDo.Video;

namespace FiveDo.Frontend.Avalonia.Views;

public partial class MainWindow
{
    private NetplayRuntime? _netplay;
    private NetplayControlListener? _netplayListener;
    private bool _netplayEnding;
    private bool _netplayHostMode;
    private ulong _netplayContentHash;
    private ulong _netplaySessionId;
    private string _netplayDiscId = string.Empty;
    private string _netplayHost = "127.0.0.1";
    private int _netplayPort = 5600;
    private byte _netplayInputDelay = 4;
    private int _netplayReconnectTimeout = 30;
    private long _netplayLastFrame;
    private uint _netplayLastHash;
    private FrameBuffer? _netplayHashBuffer;

    private sealed class NetplayRuntime
    {
        public required LockstepSession Session { get; init; }

        public required NetplaySessionPbusSource Source { get; init; }

        public required NetplayAudioSink Audio { get; init; }
    }


    [SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Wired by MainWindow.axaml.")]
    private void OnNetplayHostAsync(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        AsyncEvent.Run(() => StartNetplayAsync(host: true, reconnect: false), "hosting netplay");
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Wired by MainWindow.axaml.")]
    private void OnNetplayJoinAsync(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        AsyncEvent.Run(() => StartNetplayAsync(host: false, reconnect: false), "joining netplay");
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Wired by MainWindow.axaml.")]
    private void OnNetplayStopAsync(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        AsyncEvent.Run(() => StopNetplayAsync(), "stopping netplay");
    }

    private async Task StartNetplayAsync(bool host, bool reconnect)
    {
        if (_netplay is not null && !reconnect)
        {
            ShowError("Netplay", "A netplay session is already active.");
            return;
        }

        if (_machine is null || _runner is null || _disc is null || _gameId.Length == 0
            || _biosBytes is null || !_biosLoaded)
        {
            ShowError(
                "Netplay",
                "Open a disc and BIOS, then start the game before hosting or joining netplay.");
            return;
        }

        if (_moviePlayback is not null || _movieRecorder is not null)
        {
            ShowError("Netplay", "Stop movie playback or recording before starting netplay.");
            return;
        }

        CancelFastForward();

        if (reconnect && (host ? _netplayListener is null : string.IsNullOrEmpty(_netplayHost)))
        {
            ShowError("Netplay", "There is no session to reconnect.");
            return;
        }

        _netplayHostMode = host;
        _netplayDiscId = GameId.Compute(_disc);
        _netplayContentHash = NetplayContentHash.Compute(
            _biosBytes,
            _biosBytes2 ?? [],
            _netplayDiscId,
            _machine.Tuning);

        var mode = reconnect
            ? host ? NetplayDialogMode.ReconnectHost : NetplayDialogMode.ReconnectJoin
            : host ? NetplayDialogMode.Host : NetplayDialogMode.Join;
        var options = new NetplayWindowOptions
        {
            Mode = mode,
            Host = _netplayHost,
            Port = _netplayPort,
            InputDelay = _netplayInputDelay,
            ReconnectTimeoutSeconds = _netplayReconnectTimeout,
            LastFrame = _netplayLastFrame,
            LastFrameHash = _netplayLastHash,
            DiscId = _netplayDiscId,
            ContentHash = _netplayContentHash,
            SessionId = _netplaySessionId,
            Listener = host ? _netplayListener : null,
            CaptureState = CaptureNetplayState,
            ApplyState = ApplyNetplayState,
        };

        _runner.Pause();
        NetplaySetup? setup;
        try
        {
            setup = await NetplayWindow.ShowAsync(this, options).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or InvalidOperationException
            or ArgumentException)
        {
            ShowError("Netplay", exception.Message);
            return;
        }

        if (setup is null)
        {
            if (reconnect)
            {
                _netplayListener?.Dispose();
                _netplayListener = null;
            }

            if (_netplay is null)
            {
                _runner.Resume();
            }

            return;
        }

        _netplayPort = setup.Port;
        _netplayInputDelay = setup.InputDelay;
        _netplaySessionId = setup.SessionId;
        _netplayHostMode = host;
        if (!host)
        {
            _netplayHost = setup.HostAddress;
        }

        if (setup.Listener is not null)
        {
            _netplayListener = setup.Listener;
        }

        if (!AttachNetplay(setup))
        {
            if (setup.Listener is not null)
            {
                setup.Listener.Dispose();
                _netplayListener = null;
            }

            return;
        }

        NetplayStopMenuItem.IsEnabled = true;
        ShowOsd(
            host
                ? $"Netplay: hosting as player 1 on port {setup.Port}."
                : $"Netplay: joined {setup.HostAddress}:{setup.Port} as player 2.");
    }

    private NetplayStateMessage CaptureNetplayState()
    {
        if (_machine is null || _runner is null)
        {
            throw new InvalidOperationException("The machine is not ready for netplay.");
        }

        NetplayStateMessage? state = null;
        _runner.Invoke(() => state = NetplayStateTransfer.Capture(_machine, _gameId));
        return state ?? throw new InvalidOperationException("The netplay state could not be captured.");
    }

    private string? ApplyNetplayState(NetplayStateMessage state)
    {
        if (_machine is null || _runner is null)
        {
            return "The machine is not ready for netplay.";
        }

        string? error = null;
        _runner.Invoke(() =>
        {
            _ = NetplayStateTransfer.TryApply(_machine, state, _gameId, out error);
        });
        return error;
    }

    private bool AttachNetplay(NetplaySetup setup)
    {
        if (_machine is null || _runner is null)
        {
            return false;
        }

        bool host = setup.Mode is NetplayDialogMode.Host or NetplayDialogMode.ReconnectHost;
        byte localPlayer = host ? (byte)0 : (byte)1;
        UdpNetplayTransport udp;
        if (host)
        {
            udp = new UdpNetplayTransport(setup.Port, remote: null, bindAddress: IPAddress.Any);
        }
        else
        {
            if (!IPAddress.TryParse(setup.HostAddress, out IPAddress? address))
            {
                ShowError("Netplay", "The host address is not a valid IP endpoint.");
                return false;
            }

            udp = new UdpNetplayTransport(
                0,
                new IPEndPoint(address, setup.Port),
                IPAddress.Any);
        }

        var session = new LockstepSession(
            udp,
            new NetplayOptions
            {
                Role = host ? NetplayRole.Host : NetplayRole.Join,
                InputDelayFrames = setup.InputDelay,
                SessionId = setup.SessionId,
                ContentHash = setup.ContentHash,
                FirstFrame = setup.StartFrame,
                StepTimeout = TimeSpan.FromSeconds(5),
            },
            _ => ReadLocalNetplayInput(localPlayer));
        if (!session.StartNegotiated())
        {
            string message = session.FailureMessage ?? "The netplay session did not start.";
            session.Dispose();
            ShowError("Netplay", message);
            return false;
        }

        var source = new NetplaySessionPbusSource(session);
        var audio = new NetplayAudioSink(_audio);
        _runner.ReplacePbusSource(source);
        _runner.Invoke(() => _machine.AudioSink = audio);
        _runner.RunAhead = 0;
        _runner.FrameDelay = 0;
        _runner.FrameSkip = FrameSkipMode.Off;
        _runner.SpeedMultiplier = 1;
        _netplay = new NetplayRuntime
        {
            Session = session,
            Source = source,
            Audio = audio,
        };
        _netplayLastFrame = setup.StartFrame;
        _netplayLastHash = setup.StartFrame == 0 ? 0 : _netplayLastHash;
        if (_runner.IsRunning)
        {
            _runner.Resume();
        }
        else
        {
            _runner.Start();
        }

        _running = true;
        SetPlayState(running: true);

        ShowEmulationView();
        StatusText.Text = host
            ? "Netplay: hosting as player 1."
            : "Netplay: connected as player 2.";
        return true;
    }

    private ControllerState ReadLocalNetplayInput(byte player)
    {
        _input.Update();
        ControllerState state = _input.Devices.GetJoypad(player);
        _input.Devices.ClearTransientMotion();
        return state;
    }

    private uint HashNetplayFrame()
    {
        VdlFrame frame = _machine!.Frame;
        if (_netplayHashBuffer is null
            || _netplayHashBuffer.Width != frame.SourceWidth
            || _netplayHashBuffer.Height != frame.SourceHeight)
        {
            _netplayHashBuffer = new FrameBuffer(frame.SourceWidth, frame.SourceHeight);
        }

        FrameAssembler.Assemble(
            frame,
            _netplayHashBuffer,
            _netplayHashBuffer.Width,
            _netplayHashBuffer.Height);
        return FrameHasher.HashBgra(_netplayHashBuffer.PixelSpan);
    }

    private async Task HandleNetplayFailureAsync(string? reason)
    {
        if (_netplayEnding || _netplay is null)
        {
            return;
        }

        _netplayEnding = true;
        try
        {
            _runner?.Pause();
            string text = reason ?? "The netplay connection was lost.";
            StatusText.Text = "Netplay: " + text;
            ShowOsd("Netplay: " + text, OsdSeverity.Warning);
            await StopNetplayAsync(keepListener: true).ConfigureAwait(true);
            if (_netplayHostMode ? _netplayListener is not null : !string.IsNullOrEmpty(_netplayHost))
            {
                await StartNetplayAsync(_netplayHostMode, reconnect: true).ConfigureAwait(true);
            }
        }
        finally
        {
            _netplayEnding = false;
        }
    }

    private async Task StopNetplayAsync(bool keepListener = false)
    {
        NetplayRuntime? netplay = _netplay;
        _netplay = null;
        if (netplay is not null)
        {
            netplay.Session.RequestStop(NetplayByeReason.Normal);
            _runner?.ReplacePbusSource(_input);
            if (_machine is not null)
            {
                _runner?.Invoke(() => _machine.AudioSink = _audio);
            }

            netplay.Session.Dispose();
        }

        if (!keepListener)
        {
            _netplayListener?.Dispose();
            _netplayListener = null;
        }

        NetplayStopMenuItem.IsEnabled = false;
        if (_runner is not null)
        {
            ApplyLatencySettings(_settings);
            _runner.FrameSkip = _settings.FrameSkip;
            _runner.SpeedMultiplier = 1;
        }

        if (!keepListener && !_netplayEnding && _running && _runner?.IsRunning == true)
        {
            _runner.Resume();
        }

        if (!keepListener)
        {
            _netplayLastFrame = 0;
            _netplayLastHash = 0;
        }

        await Task.CompletedTask.ConfigureAwait(true);
        ShowOsd("Netplay session stopped.");
    }

    private void OnFrameCompleted(object? sender, EventArgs e)
    {
        _achievements?.Tick();
        if (_netplay is { } netplay && !_netplayEnding && _machine is not null)
        {
            if (!netplay.Source.LastStepSucceeded)
            {
                Dispatcher.UIThread.Post(() => AsyncEvent.Run(
                    () => HandleNetplayFailureAsync(netplay.Session.FailureMessage),
                    "handling netplay failure"));
                return;
            }

            var frame = netplay.Session.Frame - 1;
            var hash = HashNetplayFrame();
            netplay.Session.PublishHash(frame, hash, netplay.Audio.Hash, netplay.Audio.SampleCount);
            _netplayLastFrame = frame;
            _netplayLastHash = hash;
            if (netplay.Session.State != NetplaySessionState.Running)
            {
                bool completed = netplay.Session.State == NetplaySessionState.Completed;
                Dispatcher.UIThread.Post(() => AsyncEvent.Run(
                    completed
                        ? () => StopNetplayAsync()
                        : () => HandleNetplayFailureAsync(netplay.Session.FailureMessage),
                    completed ? "closing netplay" : "handling netplay failure"));
            }
        }
    }

}
