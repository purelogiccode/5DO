using FiveDo.Core;

namespace FiveDo.Tests.TestUtilities;

internal sealed class FakeMadamBus : IMadamBus
{
    public uint LastReadOffset { get; private set; }

    public uint LastWriteOffset { get; private set; }

    public uint LastWriteValue { get; private set; }

    public uint ReadValue { get; set; }

    public uint Peek(uint offset)
    {
        LastReadOffset = offset;
        return ReadValue;
    }

    public void Poke(uint offset, uint value)
    {
        LastWriteOffset = offset;
        LastWriteValue = value;
    }
}

internal sealed class FakeClioBus : IClioBus
{
    public uint LastReadOffset { get; private set; }

    public uint LastWriteOffset { get; private set; }

    public uint LastWriteValue { get; private set; }

    public uint ReadValue { get; set; }

    public bool PokeResult { get; set; }

    public uint Peek(uint offset)
    {
        LastReadOffset = offset;
        return ReadValue;
    }

    public bool Poke(uint offset, uint value)
    {
        LastWriteOffset = offset;
        LastWriteValue = value;
        return PokeResult;
    }
}

internal sealed class FakeSportBus : ISportBus
{
    public uint LastSourceOffset { get; private set; }

    public uint LastWriteOffset { get; private set; }

    public uint LastWriteValue { get; private set; }

    public uint SourceValue { get; set; }

    public uint SetSource(uint offset)
    {
        LastSourceOffset = offset;
        return SourceValue;
    }

    public void WriteAccess(uint offset, uint value)
    {
        LastWriteOffset = offset;
        LastWriteValue = value;
    }
}

internal sealed class FakeDiagPort : IDiagPort
{
    public uint LastSent { get; private set; }

    public uint ReadValue { get; set; }

    public uint Get() => ReadValue;

    public void Send(uint value) => LastSent = value;
}
