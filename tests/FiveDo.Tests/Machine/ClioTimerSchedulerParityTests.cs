using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FiveDo.Core;
using FiveDo.Machine;
using ClioChip = FiveDo.Clio.Clio;
using ClioTimerUsage = FiveDo.Clio.ClioTimerUsage;

namespace FiveDo.Tests.Machine;

/// <summary>
/// Proves that <see cref="Quartz"/> plus <see cref="ClioChip.DoTimers(int)"/> reproduce
/// <c>opera_clock.c</c> and <c>opera_clio.c</c> exactly for an identical cycle and
/// register-write sequence.
/// </summary>
public sealed class ClioTimerSchedulerParityTests
{
    private const int ArmClock = 12_500_000;
    private const uint Decrement = 0x1;
    private const uint Reload = 0x2;
    private const uint Cascade = 0x4;

    [Fact]
    public void OperaScheduler_MatchesTheReferenceClockAndTimerModel()
    {
        var tuning = new EmulatorTuning { ClioTimers = ClioTimerMode.OperaScheduler };
        var clio = new ClioChip(tuning) { TimerTelemetryEnabled = true };
        var quartz = new Quartz(tuning, () => clio.TimerDelay);
        var model = new OperaTimerModel(ArmClock);
        var random = new Random(0x5D0);
        var slotEvents = 0;

        // Timer 0 counts down and reloads; timer 1 is cascade-gated by timer 0; timer 2
        // stops on underflow; timer 3 counts freely without reload.
        WriteControl(clio, model, 0x200, 0x0 | (Decrement | Reload) | (Decrement | Cascade) << 4);
        WriteControl(clio, model, 0x208, (Decrement << 8) | (Decrement | Reload) << 12);

        for (var step = 0; step < 6_000; step++)
        {
            if (step % 31 == 0)
            {
                var address = 0x100 + (uint)random.Next(0, 0x20) * 4;
                var value = (uint)random.NextInt64(0, 0x1_0000_0000);
                clio.Poke(address, value);
                model.Poke(address, value);
            }

            if (step % 97 == 0)
            {
                var address = random.Next(0, 4) switch
                {
                    0 => 0x200u,
                    1 => 0x204u,
                    2 => 0x208u,
                    _ => 0x20Cu,
                };
                var value = (uint)random.NextInt64(0, 0x1_0000_0000);
                clio.Poke(address, value);
                model.Poke(address, value);
            }

            if (step % 53 == 0)
            {
                var slack = (uint)random.Next(0, 0x400);
                clio.Poke(0x220, slack);
                model.Poke(0x220, slack);
            }

            var cycles = random.Next(1, 256);
            quartz.PushArmCycles((uint)cycles);
            model.Push((uint)cycles);

            while (true)
            {
                var fiveDoDue = quartz.QueueTimer(out var fiveDoSlot);
                var modelDue = model.Queue(out var modelSlot);
                Assert.Equal(modelDue, fiveDoDue);
                if (!fiveDoDue)
                {
                    break;
                }

                Assert.Equal(modelSlot, fiveDoSlot);
                slotEvents++;
                clio.DoTimers(fiveDoSlot);
                model.Execute((uint)fiveDoSlot);
                Assert.Equal(model.TimerCarry != 0, clio.TimerCarry);

                for (uint address = 0x100; address <= 0x17C; address += 4)
                {
                    Assert.Equal(model.Registers[address], clio.Peek(address));
                }

                Assert.Equal(model.Registers[0x200], clio.Peek(0x200));
                Assert.Equal(model.Registers[0x208], clio.Peek(0x208));
                Assert.Equal(model.Registers[0x40] & 0x7FF, clio.Peek(0x40) & 0x7FF);
            }
        }

        IReadOnlyList<ClioTimerUsage> usage = clio.GetTimerUsage();
        Assert.True(slotEvents > 50_000, $"Only {slotEvents} timer slots were exercised.");
        for (var timer = 0; timer < 16; timer++)
        {
            Assert.Equal(model.Executions[timer], usage.SingleOrDefault(row => row.Timer == timer).Executions);
            Assert.Equal(model.Underflows[timer], usage.SingleOrDefault(row => row.Timer == timer).Underflows);
            Assert.Equal(model.Reloads[timer], usage.SingleOrDefault(row => row.Timer == timer).Reloads);
            Assert.Equal(model.Stops[timer], usage.SingleOrDefault(row => row.Timer == timer).Stops);
            Assert.Equal(model.Fiqs[timer], usage.SingleOrDefault(row => row.Timer == timer).Fiqs);
        }
    }

    [Fact]
    public void OperaScheduler_MasksTimerRegistersToSixteenBits()
    {
        var opera = new ClioChip(new EmulatorTuning { ClioTimers = ClioTimerMode.OperaScheduler });
        opera.Poke(0x100, 0x1234_5678);
        opera.Poke(0x104, 0xFFFF_0000);
        opera.Poke(0x120, 0xDEAD_BEEF);

        Assert.Equal(0x5678u, opera.Peek(0x100));
        Assert.Equal(0u, opera.Peek(0x104));
        Assert.Equal(0xBEEFu, opera.Peek(0x120));

        // The FreeDO-compatible default keeps the historical FreeDO register behavior.
        var freeDo = new ClioChip();
        freeDo.Poke(0x100, 0x1234_5678);
        Assert.Equal(0x1234_5678u, freeDo.Peek(0x100));
    }

    [Fact]
    public void TimerTrace_RecordsUnderflowsControlWritesAndSlack()
    {
        var clio = new ClioChip(new EmulatorTuning { ClioTimers = ClioTimerMode.OperaScheduler });
        using var trace = new StringWriter();
        clio.TimerTraceWriter = trace;

        clio.Poke(0x200, Decrement | Reload);
        clio.Poke(0x100, 0);
        clio.Poke(0x104, 2);
        clio.Poke(0x220, 32);
        clio.DoTimers(0);

        var lines = trace.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(lines, line => line.Contains("event=control register=200", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("slot=0 event=counter write=00000000 value=0000", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("slot=0 event=counter write=00000002 value=0002", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("event=slack write=00000020 value=20", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("slot=0 event=underflow reload=0002 carry=1", StringComparison.Ordinal));
    }

    [Fact]
    public void TimerTelemetry_CountsExecutionsUnderflowsReloadsStopsAndFiqs()
    {
        var clio = new ClioChip(new EmulatorTuning { ClioTimers = ClioTimerMode.OperaScheduler })
        {
            TimerTelemetryEnabled = true,
        };

        clio.Poke(0x200, (Decrement | Reload) | Decrement << 4);
        clio.Poke(0x100, 0);
        clio.Poke(0x104, 3);
        clio.Poke(0x108, 0);

        clio.DoTimers(0);
        clio.DoTimers(0);
        clio.DoTimers(1);

        IReadOnlyList<ClioTimerUsage> usage = clio.GetTimerUsage();
        ClioTimerUsage timer0 = usage.Single(row => row.Timer == 0);
        Assert.Equal(2, timer0.Executions);
        Assert.Equal(1, timer0.Underflows);
        Assert.Equal(1, timer0.Reloads);
        Assert.Equal(0, timer0.Stops);
        Assert.Equal(0, timer0.Fiqs);
        Assert.Equal(2u, clio.Peek(0x100));

        ClioTimerUsage timer1 = usage.Single(row => row.Timer == 1);
        Assert.Equal(1, timer1.Executions);
        Assert.Equal(1, timer1.Underflows);
        Assert.Equal(0, timer1.Reloads);
        Assert.Equal(1, timer1.Stops);
        Assert.Equal(1, timer1.Fiqs);
        Assert.Equal(0xFFFFu, clio.Peek(0x108));

        clio.ResetTimerTelemetry();
        Assert.Empty(clio.GetTimerUsage());
    }

    [Fact]
    public void TimerTelemetry_CountsTheFreeDoAllTimerScan()
    {
        var clio = new ClioChip { TimerTelemetryEnabled = true };
        clio.Poke(0x200, Decrement);
        clio.Poke(0x100, 1);

        clio.DoTimers();
        clio.DoTimers();

        ClioTimerUsage timer0 = clio.GetTimerUsage().Single();
        Assert.Equal(0, timer0.Timer);
        Assert.Equal(2, timer0.Executions);
        Assert.Equal(1, timer0.Underflows);
        Assert.Equal(0, timer0.Reloads);
        Assert.Equal(1, timer0.Stops);
        Assert.Equal(0, timer0.Fiqs);
    }

    private static void WriteControl(ClioChip clio, OperaTimerModel model, uint address, uint value)
    {
        clio.Poke(address, value);
        model.Poke(address, value);
    }

    /// <summary>
    /// A direct transcription of <c>opera_clock_timer_queued</c> and
    /// <c>opera_clio_timer_execute</c>, including the int32 accumulator and threshold
    /// arithmetic from the reference C sources.
    /// </summary>
    private sealed class OperaTimerModel
    {
        private readonly int _armClock;
        private readonly uint[] _registers = new uint[0x210];
        private int _timerAcc;
        private uint _slack = 64;
        private uint _slot;
        private bool _inGap;
        private int _cyclesPerTimerSlot;
        private int _cyclesPerTimerGap;
        private int _cyclesUntilTimer;

        internal OperaTimerModel(int armClock)
        {
            _armClock = armClock;
            Recalculate();
        }

        internal uint[] Registers => _registers;

        internal uint TimerCarry { get; private set; }

        internal long[] Executions { get; } = new long[16];

        internal long[] Underflows { get; } = new long[16];

        internal long[] Reloads { get; } = new long[16];

        internal long[] Stops { get; } = new long[16];

        internal long[] Fiqs { get; } = new long[16];

        internal void Poke(uint address, uint value)
        {
            if (address is >= 0x100 and <= 0x17C)
            {
                _registers[address] = value & 0xFFFF;
            }
            else if (address == 0x200)
            {
                _registers[0x200] |= value;
            }
            else if (address == 0x204)
            {
                _registers[0x200] &= ~value;
            }
            else if (address == 0x208)
            {
                _registers[0x208] |= value;
            }
            else if (address == 0x20C)
            {
                _registers[0x208] &= ~value;
            }
            else if (address == 0x220)
            {
                _slack = value & 0x3FF;
                Recalculate();
            }
        }

        internal void Push(uint cycles)
        {
            _timerAcc = unchecked(_timerAcc + (int)(cycles << 16));
        }

        internal bool Queue(out int timer)
        {
            if (_timerAcc >= _cyclesUntilTimer)
            {
                _timerAcc -= _cyclesUntilTimer;
                timer = (int)_slot;
                _slot++;
                if (_slot >= 16)
                {
                    _slot = 0;
                    _inGap = true;
                    _cyclesUntilTimer = _cyclesPerTimerGap;
                }
                else
                {
                    _inGap = false;
                    _cyclesUntilTimer = _cyclesPerTimerSlot;
                }

                return true;
            }

            timer = -1;
            return false;
        }

        internal void Execute(uint timer)
        {
            timer &= 0x0F;
            if (timer == 0)
            {
                TimerCarry = 0;
            }

            var flags = TimerFlags(timer);
            if ((flags & Decrement) == 0)
            {
                TimerCarry = 0;
                return;
            }

            if ((flags & Cascade) != 0 && TimerCarry == 0)
            {
                TimerCarry = 0;
                return;
            }

            Executions[timer]++;
            var counterAddress = 0x100 + (timer << 3);
            var count = _registers[counterAddress] & 0xFFFF;
            var underflow = 0u;
            if (count == 0)
            {
                underflow = 1;
                Underflows[timer]++;
                if ((timer & 1) != 0)
                {
                    Fiqs[timer]++;
                    _registers[0x40] |= 1u << (10 - (int)(timer >> 1));
                }

                if ((flags & Reload) != 0)
                {
                    Reloads[timer]++;
                    _registers[counterAddress] = _registers[counterAddress + 4] & 0xFFFF;
                }
                else
                {
                    Stops[timer]++;
                    _registers[counterAddress] = 0xFFFF;
                    _registers[timer < 8 ? 0x200 : 0x208] &= ~(Decrement << Shift(timer));
                }
            }
            else
            {
                _registers[counterAddress] = count - 1;
            }

            TimerCarry = underflow;
        }

        private uint TimerFlags(uint timer)
        {
            return (_registers[timer < 8 ? 0x200 : 0x208] >> Shift(timer)) & 0xF;
        }

        private static int Shift(uint timer)
        {
            return (int)((timer & 7) << 2);
        }

        private void Recalculate()
        {
            _cyclesPerTimerSlot = (int)(((ulong)_armClock * 4UL << 16) / 25_000_000UL);
            _cyclesPerTimerGap = (int)(((ulong)_armClock * (4UL + _slack) << 16) / 25_000_000UL);
            _cyclesUntilTimer = _inGap ? _cyclesPerTimerGap : _cyclesPerTimerSlot;
        }
    }
}
