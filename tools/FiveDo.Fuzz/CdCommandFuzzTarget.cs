using System;
using FiveDo.Cdrom;
using FiveDo.Core;

namespace FiveDo.Fuzz;

/// <summary>Feeds random command, poll, tick, and data-read sequences to the CD device.</summary>
internal sealed class CdCommandFuzzTarget : IFuzzTarget
{
    private static readonly CdSpeed[] Speeds =
    [
        CdSpeed.Unbounded,
        CdSpeed.X1,
        CdSpeed.X2,
        CdSpeed.X4,
        CdSpeed.X8,
        CdSpeed.X16,
        CdSpeed.X24,
        CdSpeed.X32,
    ];

    /// <inheritdoc />
    public string Name => "cd-command";

    /// <inheritdoc />
    public FuzzCase CreateCase(FuzzContext context, int iteration)
    {
        ArgumentNullException.ThrowIfNull(context);
        var payload = new byte[Mutation.RandomLength(context.Random, 1, 2048)];
        context.Random.NextBytes(payload);
        return new FuzzCase("random CD command stream", payload);
    }

    /// <inheritdoc />
    public void Execute(FuzzCase fuzzCase, FuzzContext context)
    {
        ArgumentNullException.ThrowIfNull(fuzzCase);
        ArgumentNullException.ThrowIfNull(context);
        var device = new CdromDevice
        {
            ArmClock = EmulatorConstants.ArmClock,
            TimingMode = fuzzCase.Payload.Length > 0 && (fuzzCase.Payload[0] & 1) != 0
                ? CdromTimingMode.OperaCycleBased
                : CdromTimingMode.FreeDoCompatible,
            ConfiguredSpeed = Speeds[fuzzCase.Payload.Length > 1
                ? fuzzCase.Payload[1] % Speeds.Length
                : 0],
        };

        device.InsertDisc(new MemoryDiscSource(SyntheticOperaDisc.Build(16)));
        var index = 2;
        while (index < fuzzCase.Payload.Length)
        {
            byte operation = fuzzCase.Payload[index++];
            switch (operation & 0x0F)
            {
                case 0x01:
                    if (index < fuzzCase.Payload.Length)
                    {
                        device.SetCommand(fuzzCase.Payload[index++]);
                    }

                    break;
                case 0x02:
                    if (index < fuzzCase.Payload.Length)
                    {
                        device.SetPoll(fuzzCase.Payload[index++]);
                    }

                    break;
                case 0x03:
                    if (index + 4 <= fuzzCase.Payload.Length)
                    {
                        var cycles = (uint)(
                            fuzzCase.Payload[index]
                            | (fuzzCase.Payload[index + 1] << 8)
                            | (fuzzCase.Payload[index + 2] << 16)
                            | (fuzzCase.Payload[index + 3] << 24));
                        index += 4;
                        device.Tick(cycles % 1_000_001);
                    }

                    break;
                case 0x04:
                    EnsureByte(device.GetStatus());
                    break;
                case 0x05:
                    EnsureByte(device.GetData());
                    break;
                case 0x06:
                    device.InsertDisc(new MemoryDiscSource(SyntheticOperaDisc.Build(16)));
                    break;
                case 0x07:
                    device.EjectDisc();
                    break;
                case 0x08:
                    device.Reset();
                    break;
                default:
                    Drain(device);
                    break;
            }
        }
    }

    private static void Drain(CdromDevice device)
    {
        for (var count = 0; count < 32; count++)
        {
            EnsureByte(device.GetStatus());
            EnsureByte(device.GetData());
        }
    }

    private static void EnsureByte(uint value)
    {
        if (value > byte.MaxValue)
        {
            throw new InvalidOperationException($"The CD device returned a non-byte value ({value}).");
        }
    }
}
