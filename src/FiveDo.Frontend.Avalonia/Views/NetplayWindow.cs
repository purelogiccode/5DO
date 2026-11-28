using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using FiveDo.Host.Netplay;
using FiveDo.Host.Netplay.Lobby;
using FiveDo.Lobby.Protocol;

namespace FiveDo.Frontend.Avalonia.Views;

internal enum NetplayDialogMode
{
    Host = 0,
    Join = 1,
    ReconnectHost = 2,
    ReconnectJoin = 3,
}

[SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "Consumed by the MainWindow partial class; ReSharper does not follow .axaml.cs usage.")]
internal sealed class NetplayWindowOptions
{
    public required NetplayDialogMode Mode { get; init; }

    public string Host { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 5600;

    public byte InputDelay { get; init; } = 4;

    public int ReconnectTimeoutSeconds { get; init; } = 30;

    public long LastFrame { get; init; }

    public uint LastFrameHash { get; init; }

    public required string DiscId { get; init; }

    public required ulong ContentHash { get; init; }

    public ulong SessionId { get; init; }

    public NetplayControlListener? Listener { get; init; }

    public bool UseLobby { get; init; }

    public string LobbyUrl { get; init; } = "https://lobby.purelogiccode.com";

    public string DisplayName { get; init; } = Environment.UserName;

    public string JoinCode { get; init; } = string.Empty;

    public LobbyNetplaySession? LobbySession { get; init; }

    public required Func<NetplayStateMessage> CaptureState { get; init; }

    public required Func<NetplayStateMessage, string?> ApplyState { get; init; }
}

[SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "Consumed by the MainWindow partial class; ReSharper does not follow .axaml.cs usage.")]
internal sealed class NetplaySetup
{
    public required NetplayDialogMode Mode { get; init; }

    public required ulong SessionId { get; init; }

    public required ulong ContentHash { get; init; }

    public required long StartFrame { get; init; }

    public required byte InputDelay { get; init; }

    public required string HostAddress { get; init; }

    public required int Port { get; init; }

    public NetplayControlListener? Listener { get; init; }

    public LobbyNetplaySession? Lobby { get; init; }

    public string DisplayName { get; init; } = string.Empty;
}

/// <summary>
/// Netplay setup dialog. It performs the reliable control handshake and state transfer
/// over direct TCP or a lobby control relay, then returns the settings needed to attach
/// the UDP lockstep session.
/// </summary>
internal sealed class NetplayWindow : Window
{
    private readonly NetplayWindowOptions _options;
    private readonly CheckBox _useLobby;
    private readonly TextBox _host;
    private readonly NumericUpDown _port;
    private readonly NumericUpDown _delay;
    private readonly NumericUpDown _reconnect;
    private readonly TextBox _lobbyUrl;
    private readonly TextBox _lobbyName;
    private readonly TextBox _joinCode;
    private readonly TextBox _roomCode;
    private readonly ComboBox _transport;
    private readonly Grid _hostRow;
    private readonly Grid _portRow;
    private readonly Grid _lobbyUrlRow;
    private readonly Grid _lobbyNameRow;
    private readonly Grid _joinCodeRow;
    private readonly Grid _roomCodeRow;
    private readonly Grid _transportRow;
    private readonly TextBlock _status;
    private readonly ProgressBar _progress;
    private readonly Button _start;
    private readonly Button _cancel;
    private CancellationTokenSource? _cancellation;
    private NetplayControlListener? _ownedListener;
    private bool _transferOwnsListener;

    private NetplayWindow(NetplayWindowOptions options, bool autoStart)
    {
        _options = options;
        bool reconnect = options.Mode is NetplayDialogMode.ReconnectHost or NetplayDialogMode.ReconnectJoin;
        bool host = options.Mode is NetplayDialogMode.Host or NetplayDialogMode.ReconnectHost;
        bool useLobby = options.LobbySession is not null || options.UseLobby;
        Title = reconnect ? "Reconnect Netplay" : "Netplay";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var heading = new TextBlock
        {
            Text = reconnect
                ? "Reconnect the netplay session"
                : host ? "Host a netplay session" : "Join a netplay session",
            FontSize = 19,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };

        var body = new TextBlock
        {
            Text = reconnect
                ? $"The host will transfer the current state. Last confirmed frame: {options.LastFrame}."
                : "Both players must open the same disc and BIOS with matching emulation settings. "
                    + "Use a direct address or an online lobby room.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
        };

        _useLobby = new CheckBox
        {
            Content = "Use the online lobby",
            IsChecked = useLobby,
            IsEnabled = !reconnect,
        };
        _host = new TextBox
        {
            Text = options.Host,
            PlaceholderText = "Host address",
            IsEnabled = !host,
        };
        _port = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 65535,
            Value = options.Port,
            Increment = 1,
            FormatString = "0",
            IsEnabled = !host || options.Listener is null,
        };
        _delay = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 30,
            Value = options.InputDelay,
            Increment = 1,
            FormatString = "0",
            IsEnabled = !reconnect,
        };
        _reconnect = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 300,
            Value = options.ReconnectTimeoutSeconds,
            Increment = 5,
            FormatString = "0",
        };
        _lobbyUrl = new TextBox
        {
            Text = options.LobbySession?.LobbyUri.ToString() ?? options.LobbyUrl,
            PlaceholderText = "https://lobby.purelogiccode.com",
            IsEnabled = !reconnect,
        };
        _lobbyName = new TextBox
        {
            Text = options.DisplayName,
            PlaceholderText = "Display name",
            MaxLength = 64,
            IsEnabled = !reconnect,
        };
        _joinCode = new TextBox
        {
            Text = options.JoinCode,
            PlaceholderText = "Join code (for example ABC123)",
            MaxLength = 6,
            IsEnabled = !reconnect,
        };
        _roomCode = new TextBox
        {
            Text = options.LobbySession?.JoinCode ?? string.Empty,
            PlaceholderText = "Room code",
            IsReadOnly = true,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
        };
        _transport = new ComboBox
        {
            ItemsSource = new[] { "Automatic (direct, then relay)", "Force relay", "Direct only" },
            SelectedIndex = options.LobbySession?.Mode switch
            {
                LobbyTransportMode.Relay => 1,
                LobbyTransportMode.Direct => 2,
                _ => 0,
            },
            IsEnabled = !reconnect,
        };

        _hostRow = FieldRow("Host", _host);
        _portRow = FieldRow("Port", _port);
        _lobbyUrlRow = FieldRow("Lobby URL", _lobbyUrl);
        _lobbyNameRow = FieldRow("Display name", _lobbyName);
        _joinCodeRow = FieldRow("Join code", _joinCode);
        _roomCodeRow = FieldRow("Room code", _roomCode);
        _transportRow = FieldRow("Transport", _transport);
        var delayRow = FieldRow("Input delay (frames)", _delay);
        var reconnectRow = FieldRow("Wait timeout (seconds)", _reconnect);

        _progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 6,
            IsIndeterminate = true,
            IsVisible = false,
        };
        _status = new TextBlock
        {
            Text = reconnect ? "Ready to reconnect." : "Ready.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
        };

        _start = new Button
        {
            IsDefault = true,
        };
        _start.Classes.Add("accent");
        _start.Click += OnStart;
        _cancel = new Button
        {
            Content = "Cancel",
            IsCancel = true,
        };
        _cancel.Click += OnCancel;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        buttons.Children.Add(_cancel);
        buttons.Children.Add(_start);

        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 10,
        };
        panel.Children.Add(heading);
        panel.Children.Add(body);
        panel.Children.Add(_useLobby);
        panel.Children.Add(_lobbyUrlRow);
        panel.Children.Add(_lobbyNameRow);
        panel.Children.Add(_joinCodeRow);
        panel.Children.Add(_roomCodeRow);
        panel.Children.Add(_hostRow);
        panel.Children.Add(_portRow);
        panel.Children.Add(delayRow);
        panel.Children.Add(reconnectRow);
        panel.Children.Add(_transportRow);
        panel.Children.Add(_progress);
        panel.Children.Add(_status);
        panel.Children.Add(buttons);
        Content = panel;

        _useLobby.IsCheckedChanged += (_, _) => UpdateMode();
        UpdateMode();
        if (options.LobbySession is not null)
        {
            _roomCodeRow.IsVisible = true;
        }

        Closed += (_, _) =>
        {
            _cancellation?.Cancel();
            if (!_transferOwnsListener)
            {
                _ownedListener?.Dispose();
                _ownedListener = null;
            }
        };
        if (autoStart)
        {
            Opened += (_, _) => AsyncEvent.Run(StartAsync, "starting automated netplay");
        }
    }

    public static Task<NetplaySetup?> ShowAsync(
        Window owner,
        NetplayWindowOptions options,
        bool autoStart = false)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(options);
        var dialog = new NetplayWindow(options, autoStart);
        return dialog.ShowDialog<NetplaySetup?>(owner);
    }

    private static Grid FieldRow(string label, Control field)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("170,*"),
        };
        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(text, 0);
        Grid.SetColumn(field, 1);
        grid.Children.Add(text);
        grid.Children.Add(field);
        return grid;
    }

    private void UpdateMode()
    {
        bool lobby = _useLobby.IsChecked == true;
        bool host = _options.Mode is NetplayDialogMode.Host or NetplayDialogMode.ReconnectHost;
        _lobbyUrlRow.IsVisible = lobby;
        _lobbyNameRow.IsVisible = lobby;
        _joinCodeRow.IsVisible = lobby && !host;
        _transportRow.IsVisible = lobby;
        if (_options.Mode is NetplayDialogMode.Host or NetplayDialogMode.Join)
        {
            _roomCodeRow.IsVisible = false;
        }

        _hostRow.IsVisible = !lobby && !host;
        _portRow.IsVisible = !lobby;
        _start.Content = _options.Mode switch
        {
            NetplayDialogMode.ReconnectHost or NetplayDialogMode.ReconnectJoin => "Reconnect",
            NetplayDialogMode.Host when lobby => "Create lobby room",
            NetplayDialogMode.Host => "Start hosting",
            _ when lobby => "Join lobby room",
            _ => "Join session",
        };
    }

    private void OnStart(object? sender, RoutedEventArgs e)
    {
        AsyncEvent.Run(StartAsync, "starting netplay");
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        _cancellation?.Cancel();
        if (!_transferOwnsListener)
        {
            _ownedListener?.Dispose();
            _ownedListener = null;
        }

        Close(null);
    }

    private async Task StartAsync()
    {
        if (_cancellation is not null)
        {
            return;
        }

        _cancellation = new CancellationTokenSource();
        _start.IsEnabled = false;
        _progress.IsVisible = true;
        _status.Text = "Connecting...";
        try
        {
            NetplaySetup? setup = _options.Mode is NetplayDialogMode.Host or NetplayDialogMode.ReconnectHost
                ? await HostAsync(_cancellation.Token).ConfigureAwait(true)
                : await JoinAsync(_cancellation.Token).ConfigureAwait(true);
            if (setup is null)
            {
                return;
            }

            Close(setup);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Canceled.";
        }
        catch (Exception exception) when (exception is IOException
            or SocketException
            or InvalidDataException
            or InvalidOperationException
            or TimeoutException
            or ArgumentException
            or LobbyApiException
            or HttpRequestException
            or JsonException)
        {
            _status.Text = exception.Message;
            Serilog.Log.Error(exception, "Netplay setup failed.");
        }
        finally
        {
            _progress.IsVisible = false;
            _start.IsEnabled = true;
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    private async Task<NetplaySetup?> HostAsync(CancellationToken cancellationToken)
    {
        if (_options.LobbySession is { } lobbySession)
        {
            return await ReconnectLobbyAsync(lobbySession, cancellationToken).ConfigureAwait(true);
        }

        if (_useLobby.IsChecked == true)
        {
            return await HostLobbyAsync(cancellationToken).ConfigureAwait(true);
        }

        NetplayControlListener listener = _options.Listener ?? new NetplayControlListener(
            (int)(_port.Value ?? 5600),
            IPAddress.Any);
        if (_options.Listener is null)
        {
            _ownedListener = listener;
        }

        _port.Value = listener.LocalEndPoint.Port;
        _status.Text = $"Waiting for player 2 on port {listener.LocalEndPoint.Port}...";
        NetplayControlChannel channel;
        try
        {
            using var accept = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            int seconds = (int)(_reconnect.Value ?? 30);
            accept.CancelAfter(TimeSpan.FromSeconds(seconds > 0 ? seconds : 1));
            channel = await listener.AcceptAsync(accept.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Timed out waiting for player 2.";
            return null;
        }

        using (channel)
        {
            return await CompleteHostAsync(
                channel,
                listener,
                listener.LocalEndPoint.Port,
                lobby: null,
                cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task<NetplaySetup?> JoinAsync(CancellationToken cancellationToken)
    {
        if (_options.LobbySession is { } lobbySession)
        {
            return await ReconnectLobbyAsync(lobbySession, cancellationToken).ConfigureAwait(true);
        }

        if (_useLobby.IsChecked == true)
        {
            return await JoinLobbyAsync(cancellationToken).ConfigureAwait(true);
        }

        string host = _host.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(host))
        {
            _status.Text = "Enter the hosting peer's address.";
            return null;
        }

        IPAddress address = ResolveAddress(host);
        int port = (int)(_port.Value ?? 5600);
        _status.Text = $"Connecting to {address}:{port}...";
        using NetplayControlChannel channel = await NetplayControlChannel.ConnectAsync(
            address.ToString(),
            port,
            cancellationToken).ConfigureAwait(true);
        return await CompleteJoinAsync(channel, lobby: null, address.ToString(), port, cancellationToken)
            .ConfigureAwait(true);
    }

    private async Task<NetplaySetup?> HostLobbyAsync(CancellationToken cancellationToken)
    {
        Uri lobbyUri = ParseLobbyUri(_lobbyUrl.Text ?? string.Empty);
        using var client = new LobbyApiClient(lobbyUri);
        _status.Text = "Creating a lobby room...";
        CreateRoomResponse room = await client
            .CreateRoomAsync(
                new CreateRoomRequest
                {
                    Emulator = LobbyProtocol.Emulator5Do,
                    ProtocolVersion = LobbyProtocol.Version,
                    ContentId = _options.DiscId,
                    ContentHash = FormatContentHash(),
                    DisplayName = DisplayNameText(),
                    MaxPlayers = 2,
                },
                cancellationToken)
            .ConfigureAwait(true);
        LobbyNetplaySession lobby = LobbyNetplaySession.ForHost(
            lobbyUri,
            room,
            SelectedTransportMode(),
            TimeSpan.FromSeconds(3));
        _roomCode.Text = lobby.JoinCode;
        _roomCodeRow.IsVisible = true;
        Program.ReportNetplayJoinCode(lobby.JoinCode);
        _status.Text = $"Room {lobby.JoinCode} - share this code. Waiting for player 2...";
        var success = false;
        try
        {
            using var accept = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            int seconds = (int)(_reconnect.Value ?? 30);
            accept.CancelAfter(TimeSpan.FromSeconds(seconds > 0 ? seconds : 1));
            NetplayControlChannel channel;
            try
            {
                channel = await LobbyControlRelay.ConnectAsync(
                    lobby.Room.ControlRelay.Host,
                    lobby.Room.ControlRelay.Port,
                    lobby.RoomId,
                    lobby.Token,
                    lobby.Slot,
                    accept.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _status.Text = "Timed out waiting for player 2.";
                return null;
            }

            using (channel)
            {
                NetplaySetup? setup = await CompleteHostAsync(
                    channel,
                    listener: null,
                    lobby.Room.ControlRelay.Port,
                    lobby,
                    cancellationToken).ConfigureAwait(true);
                success = setup is not null;
                return setup;
            }
        }
        finally
        {
            if (!success)
            {
                await CloseLobbyRoomAsync(lobby).ConfigureAwait(true);
            }
        }
    }

    private async Task<NetplaySetup?> JoinLobbyAsync(CancellationToken cancellationToken)
    {
        Uri lobbyUri = ParseLobbyUri(_lobbyUrl.Text ?? string.Empty);
        string code = (_joinCode.Text ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length != 6)
        {
            _status.Text = "Enter the six-character room code.";
            return null;
        }

        using var client = new LobbyApiClient(lobbyUri);
        _status.Text = "Joining the lobby room...";
        JoinRoomResponse joined = await client
            .JoinRoomAsync(
                code,
                new JoinRoomRequest
                {
                    Emulator = LobbyProtocol.Emulator5Do,
                    ProtocolVersion = LobbyProtocol.Version,
                    ContentId = _options.DiscId,
                    ContentHash = FormatContentHash(),
                    DisplayName = DisplayNameText(),
                },
                cancellationToken)
            .ConfigureAwait(true);
        LobbyNetplaySession lobby = LobbyNetplaySession.ForJoin(
            lobbyUri,
            joined,
            SelectedTransportMode(),
            TimeSpan.FromSeconds(3));
        _roomCode.Text = lobby.JoinCode;
        _roomCodeRow.IsVisible = true;
        _status.Text = $"Room {lobby.JoinCode} joined. Connecting to the host...";
        using NetplayControlChannel channel = await LobbyControlRelay.ConnectAsync(
            lobby.Room.ControlRelay.Host,
            lobby.Room.ControlRelay.Port,
            lobby.RoomId,
            lobby.Token,
            lobby.Slot,
            cancellationToken).ConfigureAwait(true);
        return await CompleteJoinAsync(
            channel,
            lobby,
            string.Empty,
            lobby.Room.ControlRelay.Port,
            cancellationToken).ConfigureAwait(true);
    }

    private async Task<NetplaySetup?> ReconnectLobbyAsync(
        LobbyNetplaySession lobby,
        CancellationToken cancellationToken)
    {
        _roomCode.Text = lobby.JoinCode;
        _roomCodeRow.IsVisible = true;
        _status.Text = $"Reconnecting to room {lobby.JoinCode}...";
        using NetplayControlChannel channel = await LobbyControlRelay.ConnectAsync(
            lobby.Room.ControlRelay.Host,
            lobby.Room.ControlRelay.Port,
            lobby.RoomId,
            lobby.Token,
            lobby.Slot,
            cancellationToken).ConfigureAwait(true);
        return lobby.IsHost
            ? await CompleteHostAsync(
                channel,
                listener: null,
                lobby.Room.ControlRelay.Port,
                lobby,
                cancellationToken).ConfigureAwait(true)
            : await CompleteJoinAsync(
                channel,
                lobby,
                string.Empty,
                lobby.Room.ControlRelay.Port,
                cancellationToken).ConfigureAwait(true);
    }

    private async Task<NetplaySetup?> CompleteHostAsync(
        NetplayControlChannel channel,
        NetplayControlListener? listener,
        int port,
        LobbyNetplaySession? lobby,
        CancellationToken cancellationToken)
    {
        byte[]? payload = await channel.ReceiveAsync(cancellationToken).ConfigureAwait(true);
        if (payload is null
            || !NetplayControlCodec.TryDecodeJoinRequest(payload, out NetplayJoinRequest request))
        {
            _status.Text = "The joining peer sent an invalid request.";
            return null;
        }

        if (request.ContentHash != _options.ContentHash
            || request.PlayerCount != 2
            || !string.Equals(request.DiscId, _options.DiscId, StringComparison.Ordinal))
        {
            await SendResponseAsync(channel, false, 0, 0, "BIOS, disc, or tuning does not match.").ConfigureAwait(true);
            _status.Text = "The joining peer does not match this BIOS, disc, and tuning.";
            return null;
        }

        _status.Text = "Transferred state is being prepared...";
        NetplayStateMessage state;
        try
        {
            state = _options.CaptureState();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            await SendResponseAsync(channel, false, 0, 0, exception.Message).ConfigureAwait(true);
            throw;
        }

        ulong sessionId = request.SessionId;
        if (sessionId == 0)
        {
            Span<byte> bytes = stackalloc byte[8];
            Random.Shared.NextBytes(bytes);
            sessionId = BitConverter.ToUInt64(bytes);
        }

        NetplayJoinResponse response = await SendResponseAsync(
            channel,
            true,
            sessionId,
            state.StartFrame,
            string.Empty).ConfigureAwait(true);
        _status.Text = "Sending the machine state...";
        byte[] statePayload = NetplayControlCodec.Encode(state);
        await channel.SendAsync(statePayload, cancellationToken).ConfigureAwait(true);

        byte[]? ackPayload = await channel.ReceiveAsync(cancellationToken).ConfigureAwait(true);
        if (ackPayload is null
            || !NetplayControlCodec.TryDecodeStateAck(ackPayload, out NetplayStateAck ack)
            || !ack.Ok
            || ack.StartFrame != state.StartFrame
            || ack.FrameHash != state.FrameHash)
        {
            _status.Text = "The joining peer could not validate the state.";
            return null;
        }

        if (listener is not null)
        {
            _transferOwnsListener = true;
        }

        return new NetplaySetup
        {
            Mode = _options.Mode,
            SessionId = response.SessionId,
            ContentHash = _options.ContentHash,
            StartFrame = state.StartFrame,
            InputDelay = (byte)(_delay.Value ?? _options.InputDelay),
            HostAddress = string.Empty,
            Port = port,
            Listener = listener,
            Lobby = lobby,
            DisplayName = DisplayNameText(),
        };
    }

    private async Task<NetplaySetup?> CompleteJoinAsync(
        NetplayControlChannel channel,
        LobbyNetplaySession? lobby,
        string hostAddress,
        int port,
        CancellationToken cancellationToken)
    {
        var request = new NetplayJoinRequest(
            _options.Mode == NetplayDialogMode.ReconnectJoin
                ? NetplayJoinKind.Reconnect
                : NetplayJoinKind.Fresh,
            _options.ContentHash,
            _options.SessionId,
            _options.LastFrame,
            _options.LastFrameHash,
            (byte)(_delay.Value ?? _options.InputDelay),
            2,
            _options.DiscId);
        await channel.SendAsync(NetplayControlCodec.Encode(request), cancellationToken).ConfigureAwait(true);
        byte[]? responsePayload = await channel.ReceiveAsync(cancellationToken).ConfigureAwait(true);
        if (responsePayload is null
            || !NetplayControlCodec.TryDecodeJoinResponse(responsePayload, out NetplayJoinResponse response)
            || !response.Accepted)
        {
            _status.Text = "The hosting peer rejected the session.";
            return null;
        }

        if (response.ContentHash != _options.ContentHash)
        {
            _status.Text = "The hosting peer's BIOS, disc, or tuning does not match.";
            return null;
        }

        _status.Text = "Receiving the machine state...";
        byte[]? statePayload = await channel.ReceiveAsync(cancellationToken).ConfigureAwait(true);
        if (statePayload is null
            || !NetplayControlCodec.TryDecodeState(statePayload, out NetplayStateMessage? state)
            || state is null)
        {
            _status.Text = "The host did not send a readable state.";
            return null;
        }

        string? error = _options.ApplyState(state);
        var ack = new NetplayStateAck(state.StartFrame, state.FrameHash, error is null, error ?? string.Empty);
        await channel.SendAsync(NetplayControlCodec.Encode(ack), cancellationToken).ConfigureAwait(true);
        if (error is not null)
        {
            _status.Text = error;
            return null;
        }

        return new NetplaySetup
        {
            Mode = _options.Mode,
            SessionId = response.SessionId,
            ContentHash = response.ContentHash,
            StartFrame = state.StartFrame,
            InputDelay = response.InputDelay,
            HostAddress = hostAddress,
            Port = port,
            Lobby = lobby,
            DisplayName = DisplayNameText(),
        };
    }

    private async Task<NetplayJoinResponse> SendResponseAsync(
        NetplayControlChannel channel,
        bool accepted,
        ulong sessionId,
        long startFrame,
        string message)
    {
        var response = new NetplayJoinResponse(
            accepted,
            accepted ? NetplayControlReason.Accepted : NetplayControlReason.ContentMismatch,
            sessionId,
            _options.ContentHash,
            startFrame,
            (byte)(_delay.Value ?? _options.InputDelay),
            2,
            message);
        await channel.SendAsync(NetplayControlCodec.Encode(response), CancellationToken.None).ConfigureAwait(true);
        return response;
    }

    private static Uri ParseLobbyUri(string text)
    {
        text = text.Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Enter an absolute http or https lobby URL.", nameof(text));
        }

        return uri;
    }

    private LobbyTransportMode SelectedTransportMode()
    {
        return _transport.SelectedIndex switch
        {
            1 => LobbyTransportMode.Relay,
            2 => LobbyTransportMode.Direct,
            _ => LobbyTransportMode.Auto,
        };
    }

    private string DisplayNameText()
    {
        string name = (_lobbyName.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = Environment.UserName;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = "5DO player";
        }

        return name.Length <= 64 ? name : name[..64];
    }

    private string FormatContentHash()
    {
        return _options.ContentHash.ToString("x16", CultureInfo.InvariantCulture);
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
            await client
                .CloseRoomAsync(lobby.JoinCode, lobby.HostToken, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is LobbyApiException
            or HttpRequestException
            or SocketException
            or IOException
            or TaskCanceledException)
        {
            Serilog.Log.Debug(exception, "Could not close the lobby room {Room}.", lobby.JoinCode);
        }
    }

    private static IPAddress ResolveAddress(string host)
    {
        if (IPAddress.TryParse(host, out IPAddress? address))
        {
            return address;
        }

        IPAddress[] addresses = Dns.GetHostAddresses(host);
        foreach (IPAddress candidate in addresses)
        {
            if (candidate.AddressFamily == AddressFamily.InterNetwork)
            {
                return candidate;
            }
        }

        throw new ArgumentException("The host address has no IPv4 endpoint.", nameof(host));
    }
}
