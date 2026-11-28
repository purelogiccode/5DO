using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FiveDo.Core;
using FiveDo.Diagnostics.Logging;
using FiveDo.Host;
using FiveDo.Host.Netplay;
using FiveDo.Host.Netplay.Lobby;
using FiveDo.Host.Osd;
using FiveDo.Input;
using FiveDo.Lobby.Protocol;
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
    private bool _netplayUseLobby;
    private string _netplayLobbyUrl = "https://lobby.purelogiccode.com";
    private string _netplayLobbyName = Environment.UserName;
    private LobbyNetplaySession? _netplayLobby;
    private FrameBuffer? _netplayHashBuffer;
    private ControllerState _lastNetplayLocalInput;

    private sealed class NetplayRuntime
    {
        public required LockstepSession Session { get; init; }

        public required NetplaySessionPbusSource Source { get; init; }

        public required NetplayAudioSink Audio { get; init; }

        public Action? PreviousBeforeFrame { get; init; }

        public LobbyNetplaySession? Lobby { get; init; }
    }


    [SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Wired by MainWindow.axaml.")]
    private void OnNetplayHostAsync(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        AsyncEvent.Run(() => StartNetplayAsync(host: true, reconnect: false), "hosting netplay");
    }

    private void TryStartAutomatedNetplay()
    {
        if (!Program.NetplayHostMode && string.IsNullOrWhiteSpace(Program.NetplayJoinAddress))
        {
            return;
        }

        if (!Program.NetplayHostMode)
        {
            _netplayHost = Program.NetplayJoinAddress!;
        }

        _netplayUseLobby = Program.NetplayUseLobby;
        if (Program.NetplayLobbyUrl is not null)
        {
            _netplayLobbyUrl = Program.NetplayLobbyUrl;
        }

        _netplayPort = Program.NetplayPort;
        _netplayInputDelay = Program.NetplayDelay;
        _logger.ForCategory(LogCategory.Host).Information(
            "Automated netplay requested: host={Host}, address={Address}, port={Port}, delay={Delay}.",
            Program.NetplayHostMode,
            Program.NetplayJoinAddress,
            Program.NetplayPort,
            Program.NetplayDelay);
        AsyncEvent.Run(
            () => StartNetplayAsync(Program.NetplayHostMode, reconnect: false, autoStart: true),
            "starting automated netplay");
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

    private async Task StartNetplayAsync(bool host, bool reconnect, bool autoStart = false)
    {
        _logger.ForCategory(LogCategory.Host).Information(
            "Netplay start requested: host={Host}, reconnect={Reconnect}, auto={Auto}.",
            host,
            reconnect,
            autoStart);
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

        if (reconnect
            && _netplayLobby is null
            && (host ? _netplayListener is null : string.IsNullOrEmpty(_netplayHost)))
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
            UseLobby = _netplayUseLobby,
            LobbyUrl = _netplayLobbyUrl,
            DisplayName = _netplayLobbyName,
            JoinCode = _netplayLobby?.JoinCode
                ?? (_netplayUseLobby && !host ? _netplayHost : string.Empty),
            LobbySession = reconnect ? _netplayLobby : null,
            CaptureState = CaptureNetplayState,
            ApplyState = ApplyNetplayState,
        };

        _runner.Pause();
        NetplaySetup? setup;
        try
        {
            setup = await NetplayWindow.ShowAsync(this, options, autoStart).ConfigureAwait(true);
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
        _netplayLobby = setup.Lobby;
        _netplayUseLobby = setup.Lobby is not null;
        if (setup.Lobby is not null)
        {
            _netplayLobbyUrl = setup.Lobby.LobbyUri.ToString();
        }

        if (!string.IsNullOrWhiteSpace(setup.DisplayName))
        {
            _netplayLobbyName = setup.DisplayName;
        }

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

        if (setup.Lobby is { } resultLobby)
        {
            Program.ReportNetplayResult(
                $"{(host ? "host" : "join")} {resultLobby.JoinCode}");
        }

        NetplayStopMenuItem.IsEnabled = true;
        if (setup.Lobby is { } lobby)
        {
            ShowOsd(
                host
                    ? $"Netplay: hosting room {lobby.JoinCode}."
                    : $"Netplay: joined room {lobby.JoinCode} as player 2.");
        }
        else
        {
            ShowOsd(
                host
                    ? $"Netplay: hosting as player 1 on port {setup.Port}."
                    : $"Netplay: joined {setup.HostAddress}:{setup.Port} as player 2.");
        }
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
        INetplayTransport udp;
        if (setup.Lobby is { } lobby)
        {
            udp = new LobbyNetplayTransport(
                new LobbyTransportOptions
                {
                    Host = lobby.Room.UdpRendezvous.Host,
                    RendezvousPort = lobby.Room.UdpRendezvous.Port,
                    RelayPort = lobby.Room.UdpRelay.Port,
                    RoomId = lobby.RoomId,
                    Token = lobby.Token,
                    Slot = lobby.Slot,
                    Mode = lobby.Mode,
                    DirectTimeout = lobby.DirectTimeout,
                });
        }
        else if (host)
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
                CompareAudioHashes = true,
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
        Action? previousBeforeFrame = _machine.BeforeFrame;
        _runner.Invoke(() =>
        {
            _machine.AudioSink = audio;
            _machine.BeforeFrame = () =>
            {
                previousBeforeFrame?.Invoke();
                source.Advance();
            };
        });
        _runner.RunAhead = 0;
        _runner.FrameDelay = 0;
        _runner.FrameSkip = FrameSkipMode.Off;
        _runner.SpeedMultiplier = 1;
        _netplay = new NetplayRuntime
        {
            Session = session,
            Source = source,
            Audio = audio,
            PreviousBeforeFrame = previousBeforeFrame,
            Lobby = setup.Lobby,
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
        if (setup.Lobby is { } lobbyStatus)
        {
            StatusText.Text = host
                ? $"Netplay: hosting room {lobbyStatus.JoinCode}."
                : $"Netplay: joined room {lobbyStatus.JoinCode} as player 2.";
        }
        else
        {
            StatusText.Text = host
                ? "Netplay: hosting as player 1."
                : "Netplay: connected as player 2.";
        }

        return true;
    }

    private ControllerState ReadLocalNetplayInput(byte player)
    {
        _input.Update();
        ControllerState state = _input.Devices.GetJoypad(player);
        _input.Devices.ClearTransientMotion();
        if (state != _lastNetplayLocalInput)
        {
            _lastNetplayLocalInput = state;
            _logger.ForCategory(LogCategory.Host).Information(
                "Netplay local player {Player} input: {Buttons}.",
                player + 1,
                state.Buttons);
        }

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
            if (_netplayLobby is not null
                || (_netplayHostMode ? _netplayListener is not null : !string.IsNullOrEmpty(_netplayHost)))
            {
                bool autoStart = Program.NetplayHostMode
                    || !string.IsNullOrWhiteSpace(Program.NetplayJoinAddress);
                await StartNetplayAsync(_netplayHostMode, reconnect: true, autoStart).ConfigureAwait(true);
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
        LobbyNetplaySession? lobby = netplay?.Lobby ?? _netplayLobby;
        _netplay = null;
        if (netplay is not null)
        {
            netplay.Session.RequestStop(NetplayByeReason.Normal);
            _runner?.ReplacePbusSource(_input);
            if (_machine is not null)
            {
                _runner?.Invoke(() =>
                {
                    _machine.AudioSink = _audio;
                    _machine.BeforeFrame = netplay.PreviousBeforeFrame;
                });
            }

            netplay.Session.Dispose();
        }

        if (!keepListener)
        {
            _netplayListener?.Dispose();
            _netplayListener = null;
            _netplayLobby = null;
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

        if (!keepListener && lobby is { IsHost: true, HostToken: not null })
        {
            await CloseLobbyRoomAsync(lobby).ConfigureAwait(true);
        }

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
            if (frame % 60 == 0)
            {
                _logger.ForCategory(LogCategory.Host).Debug(
                    "Netplay frame {Frame}: hash=0x{Hash:X8}, audio=0x{Audio:X8}/{Samples}.",
                    frame,
                    hash,
                    netplay.Audio.Hash,
                    netplay.Audio.SampleCount);
            }
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

    private static async Task CloseLobbyRoomAsync(LobbyNetplaySession lobby)
    {
        if (!lobby.IsHost || string.IsNullOrWhiteSpace(lobby.HostToken))
        {
            return;
        }

        try
        {
            using var client = new LobbyApiClient(lobby.LobbyUri);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client
                .CloseRoomAsync(lobby.JoinCode, lobby.HostToken, timeout.Token)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is LobbyApiException
            or HttpRequestException
            or SocketException
            or IOException
            or JsonException
            or TaskCanceledException)
        {
            Serilog.Log.Debug(exception, "Could not close the lobby room {Room}.", lobby.JoinCode);
        }
    }
}
