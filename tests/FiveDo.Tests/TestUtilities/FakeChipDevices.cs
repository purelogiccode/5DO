using System;
using System.Collections.Generic;
using FiveDo.Core;

namespace FiveDo.Tests.TestUtilities;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "Test double exposes configurable inputs and captured outputs.")]
internal sealed class FakeClioXbus : IClioXbus
{
    private readonly Queue<uint> _data = new();

    public int SelectedDevice => (int)LastSelect;

    public uint LastSelect { get; private set; }

    public uint LastPoll { get; private set; }

    public uint Result { get; set; }

    public uint Poll { get; set; }

    public uint Status { get; set; }

    public int ResetCount { get; private set; }

    public void EnqueueData(params uint[] values)
    {
        foreach (var value in values)
        {
            _data.Enqueue(value);
        }
    }

    public void SetSelect(uint value) => LastSelect = value;

    public void SetPoll(uint value) => LastPoll = value;

    public void SetCommandFifo(uint value)
    {
    }

    public void SetDataFifo(uint value)
    {
    }

    public uint GetResult() => Result;

    public uint GetPoll() => Poll;

    public uint GetStatusFifo() => Status;

    public uint GetDataFifo() => _data.Count == 0 ? 0 : _data.Dequeue();

    public void Reset() => ResetCount++;
}

internal sealed class FakeVdlp : IVdlp
{
    public uint LastAddress { get; private set; }

    public int CallCount { get; private set; }

    public void ProcessVdl(uint address)
    {
        LastAddress = address;
        CallCount++;
    }
}

internal sealed class RecordingFrameSink : IFrameSink
{
    public int FrameCount { get; private set; }

    public int TriggerCount { get; private set; }

    public int LastWidth { get; private set; }

    public int LastHeight { get; private set; }

    public void OnFrame(VdlFrame frame)
    {
        FrameCount++;
        LastWidth = frame.SourceWidth;
        LastHeight = frame.SourceHeight;
    }

    public void OnFrameTrigger() => TriggerCount++;
}

internal sealed class ZeroPbusSource : IPbusSource
{
    public int GetPbusData(Span<byte> destination) => 0;
}

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "UnusedAutoPropertyAccessor.Global",
    Justification = "Test double exposes configurable inputs and captured outputs.")]
internal sealed class RecordingAudioSink : IAudioSink
{
    public int SampleCount { get; private set; }

    public uint LastSample { get; private set; }

    public void PushSample(uint packedSample)
    {
        SampleCount++;
        LastSample = packedSample;
    }
}
