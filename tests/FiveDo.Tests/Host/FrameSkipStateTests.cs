using System.Collections.Generic;
using System.IO;
using FiveDo.Core;
using FiveDo.Machine;

namespace FiveDo.Tests.Host;

public sealed class FrameSkipStateTests
{
    [Fact]
    public void SkipFrame_DoesNotChangeSerializedEmulationState()
    {
        FiveDoMachine rendered = CreateMachine();
        FiveDoMachine skipped = CreateMachine();

        rendered.RunFrames(60);
        for (var frame = 0; frame < 60; frame++)
        {
            skipped.RunFrame(skipFrame: frame % 4 != 3);
        }

        Dictionary<uint, byte[]> renderedSections = ReadSections(rendered);
        Dictionary<uint, byte[]> skippedSections = ReadSections(skipped);

        foreach ((var id, var payload) in renderedSections)
        {
            Assert.True(skippedSections.TryGetValue(id, out var skippedPayload));
            Assert.Equal(payload, skippedPayload);
        }
    }

    private static Dictionary<uint, byte[]> ReadSections(FiveDoMachine machine)
    {
        using var stream = new MemoryStream();
        machine.SaveState(stream);
        var reader = new StateReader(stream.ToArray());
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt16();
        var headerFlags = reader.ReadUInt16();
        _ = reader.ReadString();
        _ = reader.ReadInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadString();
        _ = reader.ReadString();
        _ = reader.ReadInt64();
        if ((headerFlags & StateFormat.HeaderFlagFeatureFlags) != 0)
        {
            _ = reader.ReadUInt32();
        }

        if ((headerFlags & StateFormat.HeaderFlagSectionTable) != 0)
        {
            var count = reader.ReadUInt32();
            for (var i = 0; i < count; i++)
            {
                _ = reader.ReadUInt32();
                _ = reader.ReadUInt16();
                _ = reader.ReadUInt16();
            }
        }

        var result = new Dictionary<uint, byte[]>();
        while (reader.TryReadSection(out var section))
        {
            var payload = new byte[section.PayloadLength];
            section.ReadBytes(payload);
            result[section.Id] = payload;
        }

        return result;
    }

    private static FiveDoMachine CreateMachine()
    {
        var machine = new FiveDoMachine();
        machine.LoadBios(new byte[EmulatorConstants.RomBankSize]);
        machine.Reset();
        return machine;
    }
}
