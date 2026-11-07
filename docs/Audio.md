# Audio

5DO emulates the 3DO DSP and produces **44,100 stereo samples per emulated second**.
The desktop app and CLI can write WAV files or play live through the bundled OpenAL
Soft 1.23.1 runtime. This document covers the DSP clock, the host pipeline, live output,
capture/analysis, pacing, diagnostics, and known limitations.

Source map:

| Area | Files |
|---|---|
| DSP core | `src/FiveDo.Dsp/Dsp.cs`, `DspInstruction.cs` |
| Clock/scheduler | `src/FiveDo.Machine/Quartz.cs` |
| Sample type | `src/FiveDo.Core/AudioSample.cs`, `IAudioSink.cs` |
| Ring buffer | `src/FiveDo.Audio/AudioRingBuffer.cs`, `AudioPipeline.cs` |
| Live output | `src/FiveDo.Audio/OpenAlAudioOutput.cs`, `AudioBufferPool.cs`, `AudioSoftLimiter.cs`, `MultimediaThread.cs` |
| WAV capture | `src/FiveDo.Audio/WavAudioSink.cs`, `Pcm16WavWriter.cs` |
| Analysis | `src/FiveDo.Audio/AudioAnalyzer.cs`, `AudioStatistics.cs` |
| Tap | `src/FiveDo.Audio/AudioTap.cs` |
| Pacing | `src/FiveDo.Host/AudioFramePacer.cs`, `CoreRunner.cs` |

---

## 1. The 44.1 kHz clock

The 3DO DSP sample rate is exactly **44,100 Hz**. FreeDO's original scheduler used an
8.24 fixed-point accumulator that quantized each ARM cycle batch to 1/2^24 second. For
the 16-32 cycle batches this emulator uses, that over-produced samples by up to ~0.7%
(444,097 samples instead of 441,015 for a 600-frame Lemmings boot), which made audio
slightly sharp and constantly overfilled the live ring.

The port keeps the reference's 8.24 accumulators for VDL and timers, but uses a separate
**exact rational DSP clock**:

```text
dspCycleRemainder += armCycles * 44_100
while dspCycleRemainder >= armClock:
    dspCycleRemainder -= armClock
    emit one stereo sample
```

With `armClock = 12,500,000`, this yields exactly 44,100 samples per emulated second.
At 60 Hz that is **735 samples/frame**. The DSP clock is independent of the VDL/timer
accumulators, so correcting it does not move video or timer events.

`Quartz` save-state section version 2 stores the exact cycle remainder. Version 1
sections are accepted and convert the legacy 8.24 fraction to ARM cycles.

### Sample format

The DSP emits a packed 32-bit value:

```text
bits  0..15  left  channel, signed 16-bit
bits 16..31  right channel, signed 16-bit
```

`AudioSample.FromPacked` unpacks the two channels. The core increments
`FiveDoMachine.AudioSamplesProduced` for every sample even when no `IAudioSink` is
attached; the host uses the delta for frame pacing. The DSP still runs without an audio
sink because the guest uses it for resource allocation and semaphore handshakes, not
only for sound.

---

## 2. Pipeline

```text
DSP.Loop() -> packed 32-bit sample
        |
        v
IAudioSink.PushSample(uint)
        |
        +--> NullAudioSink          (CLI --audio null; DSP still runs)
        +--> WavAudioSink           (CLI --audio wav; exact capture)
        +--> OpenAlAudioOutput      (desktop/CLI --audio openal; live)
        |
        v
AudioRingBuffer (lock-free SPSC, float interleaved stereo)
        |
        v
OpenAL Soft (Stereo16, 44.1 kHz)
        |
        v
Device / headphones / speakers
```

The emulation thread is the producer. The audio backend's fill thread is the consumer.
The ring buffer is a single-producer/single-consumer design with power-of-two capacity;
it never allocates after construction. When the ring is full, the newest sample is
dropped rather than blocking the emulation thread.

`IAudioSink.Reset` is called on machine reset and state load. Live output drops its
ring/queue so the new timeline does not replay the old one. `WavAudioSink.Reset` is a
no-op because a WAV capture intentionally spans resets and state loads.

---

## 3. Live OpenAL output

`OpenAlAudioOutput` owns:

| Parameter | Value |
|---|---|
| Sample rate | 44,100 Hz |
| Format | Stereo16 |
| Buffers | 8 |
| Maximum block | 512 stereo frames |
| Prefill lead | 3,072 frames |
| Fill-loop sleep | 2 ms |
| Ring capacity | 4,096 frames (CLI) / 8,192 frames (frontend) |
| Thread priority | Above normal; Windows "Pro Audio" multimedia thread |
| Volume | 0..1, applied per sample |
| Soft limiter | threshold 0.8, ceiling 0.9 |

### Why variable-size buffers

The core emits a burst of samples once per video frame while the device drains
steadily. The old implementation padded each block to a fixed 512 frames, which
inserted audible silence at nearly every block boundary. The current loop queues
**exactly the frames the DSP produced**; OpenAL accepts variable-size buffers.

### Why the free-buffer pool matters

The 2026-10-28 pass fixed a live crackle root cause. The fill loop used to choose the
next buffer with `_buffers[queued]`. After OpenAL unqueues the oldest buffers, that
index names buffers still attached to the playing source. Rewriting one corrupted audio
that was about to play, which was heard as constant crackle even though the underrun and
dropped-sample counters stayed at zero.

The current loop rents only buffers that OpenAL has released from a free-buffer pool,
tracks queued frames exactly, checks `alGetError` after every queue, and never rewrites
a buffer the source owns.

### Prefill and underruns

Playback starts only after `PrefillFrames = 3072` frames are buffered (about 70 ms).
This absorbs the bursty core cadence and UI/render jitter. If the queue is non-empty
but the ring is temporarily dry, the loop waits rather than padding. If both the queue
and ring are empty, it queues one silent 512-frame block to keep the source running and
increments `UnderrunBuffers`.

Counters exposed by the CLI/overlay:

| Counter | Meaning |
|---|---|
| `SamplesPushed` | Samples accepted by the ring. |
| `SamplesDropped` | Samples dropped because the ring was full. |
| `UnderrunBuffers` | Silent filler blocks queued after a dry ring. |
| `QueuedBuffers` | Buffers currently attached to the source. |
| `QueuedFrames` | Frames currently attached to the source. |
| `BufferedFrames` | Frames waiting in the emulation ring. |
| `AlErrors` | OpenAL errors observed while queueing. |
| `LastAlError` | Most recent OpenAL error string. |
| `LatencyMilliseconds` | `(queuedFrames + bufferedFrames) * 1000 / 44100`. |
| `DeviceFailed` | The fill thread stopped after a device/driver error. |
| `TakePeakLevel()` | Peak absolute level queued since the previous call (overlay meter). |

A lost device or driver failure stops the fill loop but does not crash emulation; the
DSP continues and the app runs silently.

### Device switching and concurrent outputs

OpenAL's standard current-context state is process-wide, not per thread. `OpenAlAudioOutput`
therefore makes its context current per thread through `ALC_EXT_thread_local_context`
(provided by the bundled OpenAL Soft runtime), falling back to the process-wide call only
when the extension is unavailable. This matters when two outputs overlap: changing
`Settings > Audio > Output device` creates the replacement before retiring the previous
output, and the Settings test tone plays through a second short-lived output while the game
is running. With a process-wide context, retiring the old output cleared or destroyed the
context the replacement fill thread was using; the new device then failed `alSourceQueueBuffers`
with `AL_INVALID_OPERATION`, filled the ring, and stayed silent until the frontend restarted.
Each output now owns its thread-local context, so live device changes route audio to the
newly selected device immediately and stopping/restarting a game is not required.

### Audio/video sync offset

`Settings > Audio` and `--audio-sync -250..250` apply a fixed audio offset relative to
video. Positive values insert silence frames at the head of the OpenAL stream; negative
values drop the first samples and advance it. `ComputeBufferConfiguration` converts the
offset to frames, reserves the extra ring capacity for positive values, and keeps the
existing queue shape. The offset is applied when output is created or reset, so changing
it recreates the OpenAL output without resetting the emulated machine. It changes only
host output, not DSP state, save states, or frame hashes. The UI shows the current queue
latency in the FPS overlay so the offset can be tuned against the display.

### Soft limiter

Some 3DO mixes reach full scale in the emulated samples (Road Rash's race mix is the
known example). Sending those samples to a host resampler at full scale makes the
resampler overshoot and hard-clip. `AudioSoftLimiter` passes content at or below 0.8
unchanged and compresses the excess into a 0.9 ceiling with a `tanh` knee. Quiet
material keeps its exact waveform; loud material preserves loudness without hard
clipping. WAV captures keep the raw DSP output (no limiter); the limiter is only on the
live OpenAL path.

### Volume

`OpenAlAudioOutput.Volume` is a 0..1 gain applied before the limiter. The frontend
updates it from Settings > Audio. The CLI uses `--volume`. The OpenAL source gain is
also set to 1.0; volume is applied in the sample loop so the limiter sees the scaled
signal.

---

## 4. WAV capture

`WavAudioSink` writes a 44.1 kHz, 16-bit, stereo RIFF/WAVE file. Samples are buffered
(64 KB) instead of one file write per frame; the header is patched on dispose, and the
tap patches periodically so a partially written file is still readable.

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 600 --audio wav --wav-out artifacts/capture.wav --json
```

600 frames at 60 Hz is 10.000 s = 441,000 stereo frames (441,015-441,020 observed for
600 frames depending on frame cadence). `run --json` reports duration, peak, RMS,
clipped/silent frames, and DC offset for the capture.

`WavAudioSink.Statistics` is streaming and allocation-free:

- `Peak`: largest absolute sample, 0..32768.
- `Rms`: `sqrt((sum(left^2) + sum(right^2)) / (2 * frames))`, in sample units.
- `ClippedFrames`: frames where either channel is `short.MinValue` or `short.MaxValue`.
- `SilentFrames`: frames where both channels are exactly zero.
- `DcOffsetLeft/Right`: mean sample value per channel.
- `DurationSeconds`: `frames / 44100`.

---

## 5. Audio tap (exact queued PCM)

Set `FIVEDO_AUDIO_TAP` to a path prefix **and use the OpenAL backend** (`--audio openal`
for the CLI; the desktop uses OpenAL when audio is enabled) before launching:

```powershell
$env:FIVEDO_AUDIO_TAP = "$PWD\artifacts\tap"
dotnet run --project src/FiveDo.Frontend.Avalonia
```

This writes:

- `<prefix>.wav`: the exact PCM the emulator queued to OpenAL, after volume and the
  soft limiter. This is the signal before the driver, Bluetooth link, and speakers.
- `<prefix>.csv`: one row per queued buffer:
  `elapsedMs,buffer,frames,ringFrames,queuedBuffers,queuedFrames,underrun,peak,openAlError`.

Comparing `tap.wav` with a loopback recording of the output device separates emulator
defects from driver/Bluetooth/speaker problems. If the tap is clean but the loopback
crackles, the problem is downstream of 5DO. If the tap itself has a discontinuity, the
emulator/queue path is the cause.

The 2026-10-28 validation recorded 0 OpenAL errors, 0 underruns, and 0 discontinuities
across 2,345 buffer boundaries. The tap is owned by the OpenAL output and written only by the fill thread; disposal
happens after that thread stops.

The status overlay shows a live peak meter and the ring/queue/underrun/drop counters.
The meter decays at 15% per status update so it follows the music rather than flickering
at buffer boundaries.

---

## 6. Offline analysis

```powershell
dotnet run --project src/FiveDo.Cli -- audio analyze artifacts/capture.wav --json
```

`AudioAnalyzer` supports 16-bit signed PCM, mono or stereo, any sample rate. It streams
the file, validates the RIFF/WAVE chunks, and reports format, duration, peak, RMS,
clipping, silence, and DC offset. See [`CliReference.md`](CliReference.md) section 9.

Typical checks:

- **Silence:** `silentFrames` equal to `frames` (or nearly so) means no DSP output or a
  muted path.
- **Clipping:** `clippedFrames > 0` means the raw DSP mix reached full scale. That is
  sometimes expected; live output applies the soft limiter.
- **DC offset:** a large non-zero mean suggests a DSP/decoder issue, not a host volume
  issue.
- **Duration:** 600 frames should be ~10.000 s at 60 Hz; a value near 10.07 s indicates
  the old fast clock.
- **Pitch:** compare spectral peaks with a reference capture. The Road Rash 90 s capture
  matched Opera's spectrum/RMS envelope (spectral cosine 0.9994, 441.4 Hz tone
  identical).

---

## 7. Audio-driven frame pacing

The audio clock is the stable timebase. `AudioFramePacer` receives the cumulative
`AudioSamplesProduced` after each frame:

- Keeps a 30-frame rolling window of samples/frame.
- `framesPerSecond = 44100 / averageSamplesPerFrame`.
- Clamps the target to 50-70 fps.
- Falls back to 60 Hz when the delta is zero, negative, or greater than 44,100.
- `FramePeriodTicks = Stopwatch.Frequency * (samplesPerFrame / 44100)`.

`CoreRunner` feeds the pacer after every `RunFrame`, then sets the `FramePacer` period.
On pause/resume, both pacers resync to the current sample count. The CLI's
`--realtime` uses the same pacer with a sleep/spin loop.

This keeps the host audio ring at a steady fill level even when the emulated CPU/DSP
cadence differs slightly from nominal 60 Hz. The status overlay shows measured/target
fps.

---

## 8. Settings

| Setting | Effect |
|---|---|
| `AudioEnabled` | Open OpenAL at startup. If no device is present, log a warning and run silently. |
| `Volume` | OpenAL gain, 0..1. |
| `AudioBufferMilliseconds` | Target OpenAL queue latency, 20-500 ms. |
| `AudioSyncMilliseconds` | Host audio/video offset, -250..250 ms. Positive delays audio; negative advances it. |
| `FIVEDO_AUDIO_TAP` | Enable the PCM/timing tap. |

Settings > Audio also has a **Play 440 Hz test tone** button. It creates a separate
short-lived OpenAL output, queues the complete one-second tone up front into a large
ring, and reports the OpenAL error if no device is available. Pacing a producer with
`Thread.Sleep(16)` is not reliable on Windows (the wait can round up to ~31 ms), which
drains the ring and inserts audible silent filler blocks; queueing the whole tone avoids
that crackle and lets the button test the selected device cleanly.

---

## 9. Tests and validation

| Test / evidence | What it covers |
|---|---|
| `AudioBufferPoolTests` | Free-buffer pool never rents a buffer twice; reset restores all. |
| `AudioSoftLimiterTests` | Below-threshold passthrough, ceiling, sign. |
| `AudioPipelineTests` | Ring conversion, drop-on-full, reset. |
| `AudioAnalyzerTests` | WAV chunk parsing, statistics, mono/stereo, truncation errors. |
| `AudioTapTests` | WAV/CSV contents, header patching. |
| `OpenAlAudioOutputTests` (opt-in `FIVEDO_AUDIO_TESTS=1`) | Real device opens, accepts ~40k samples, queues buffers. |
| `AudioBufferConfigurationTests` | Queue sizing, latency clamping, sync-offset frames, ring reserve. |
| `AudioFramePacerTests` | Rolling average, clamping, fallback, resync. |
| DSP tests | Instruction decode, ALU/multiplier, sample cadence, semaphore. |
| Real-content captures | 600-frame/10 s captures and per-title WAV evidence in [`TestCorpus.md`](TestCorpus.md). |

Run the opt-in audio test with a null backend on a headless machine:

```powershell
$env:FIVEDO_AUDIO_TESTS = "1"
$env:ALSOFT_DRIVERS = "null"
dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release --filter "FullyQualifiedName~OpenAlAudioOutputTests"
```

---

## 10. Troubleshooting

| Symptom | Cause / action |
|---|---|
| No sound, no error | Check `AudioEnabled`, `Volume`, and the selected device. The app logs an OpenAL warning when no device is found. The DSP still runs. |
| Constant crackle | Update to the 2026-10-28+ build (free-buffer pool). Set `FIVEDO_AUDIO_TAP` and compare tap vs loopback. |
| Crackle only on Bluetooth | The tap is clean but the loopback is not; the issue is the Bluetooth link/codec, not the emulator. |
| Pitch too high / capture 10.07 s for 600 frames | Old 8.24 DSP clock. Current builds produce 10.000 s. |
| Audio drifts/stutters | Check dropped/underrun counters. Increase the ring or reduce UI load; the frontend uses an 8192-frame ring. |
| Latency too high | The frontend intentionally runs a 3072-frame prefill (~70 ms) plus queued buffers. Current steady state is ~113 ms in the documented configuration. Lower the prefill only if you accept underruns. |
| OpenAL unavailable | The bundled runtime should be beside the executable (`OpenAL32.dll` on Windows). On Linux/macOS ensure the published `libopenal`/`libopenal.dylib` is present. |
| Test tone fails | No playback endpoint; use `ALSOFT_DRIVERS=null` for tests or install/configure a device. |
| WAV silent but live works | Check `--audio wav` and `--wav-out`; `--audio null` discards. |
| WAV has a click at state load | WAV capture intentionally spans resets/loads; split the capture at the load if needed. |
| CLI live audio is a short burst | Add `--realtime`; without it the CLI runs as fast as possible. |

---

## 11. Limitations

- No CD-DA/Red Book audio, multi-track playback, or CD swapping. The CD device streams
  the data track only.
- No platform-native audio backend beyond OpenAL Soft.
- No audio resampling in 5DO; OpenAL/the device handles the 44.1 kHz stream.
- Frontend NativeAOT audio is not validated; trimmed IL and CLI AOT paths are.
- The DSP mode-change listen is still a manual check; no current title was observed
  stopping the DSP mid-run.
- The live soft limiter changes the signal relative to the raw DSP/WAV capture by
  design; use the tap/WAV when comparing against a reference.
- `AudioPipeline` is a reusable host ring sink, but the live desktop/CLI path uses
  `OpenAlAudioOutput` and `WavAudioSink` directly.
