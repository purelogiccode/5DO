using System;
using System.Collections.Generic;
using FiveDo.Host.Netplay;

namespace FiveDo.NetplayProbe;

internal sealed class DelayedNetplayTransport : INetplayTransport
{
    private readonly INetplayTransport _inner;
    private readonly int _delayMilliseconds;
    private readonly Queue<(long Due, byte[] Packet)> _pending = new();

    public DelayedNetplayTransport(INetplayTransport inner, int delayMilliseconds)
    {
        _inner = inner;
        _delayMilliseconds = delayMilliseconds;
    }

    public bool HasRemote => _inner.HasRemote;

    public void Send(ReadOnlySpan<byte> packet)
    {
        if (_delayMilliseconds == 0)
        {
            _inner.Send(packet);
            return;
        }

        _pending.Enqueue((Environment.TickCount64 + _delayMilliseconds, packet.ToArray()));
    }

    public bool TryReceive(Span<byte> destination, out int length)
    {
        var now = Environment.TickCount64;
        while (_pending.Count > 0 && _pending.Peek().Due <= now)
        {
            _inner.Send(_pending.Dequeue().Packet);
        }

        return _inner.TryReceive(destination, out length);
    }

    public void Dispose() => _inner.Dispose();
}

internal sealed class InputDropNetplayTransport : INetplayTransport
{
    private readonly INetplayTransport _inner;
    private readonly int _every;
    private int _inputPackets;

    public InputDropNetplayTransport(INetplayTransport inner, int every)
    {
        _inner = inner;
        _every = every;
    }

    public bool HasRemote => _inner.HasRemote;

    public void Send(ReadOnlySpan<byte> packet)
    {
        if (_every > 0
            && NetplayCodec.TryPeekType(packet, out NetplayMessageType type)
            && type == NetplayMessageType.Input)
        {
            _inputPackets++;
            if (_inputPackets % _every == 0)
            {
                return;
            }
        }

        _inner.Send(packet);
    }

    public bool TryReceive(Span<byte> destination, out int length)
        => _inner.TryReceive(destination, out length);

    public void Dispose() => _inner.Dispose();
}
