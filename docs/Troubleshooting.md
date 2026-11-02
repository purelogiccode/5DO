# Troubleshooting

This is the symptom-driven guide. For tool details, see [`Diagnostics.md`](Diagnostics.md);
for every CLI option, see [`CliReference.md`](CliReference.md). Current known limitations
are in [`KnownIssues.md`](KnownIssues.md).

---

## 0. First response

1. **Check the version.** `5do --version` or **Help > About**. This documentation
   describes `1.0.0-rc.1`.
2. **Check the Logs.** The desktop **Logs** dock and **Log Viewer** show renderer
   initialization/fallback, BIOS/disc loads, save/load, screenshots, input changes, and
   faults.
3. **Reproduce with `--json`.** A CLI run gives machine-readable state (`pc`, `frameHash`,
   `cdStatus`, `cdPoll`, DSP fields, warnings) without guessing.
4. **Try a minimal case.** BIOS-only boot, then BIOS+disc, then a state load. This
   separates BIOS, disc, and state problems.
5. **Check the known issues.** The five showcase titles have documented per-title
   limitations; do not assume every behavior is a regression.

---

## 1. The app does not start

| Symptom | Cause / action |
|---|---|
| Nothing happens / console closes | Launch from a terminal to see the error: `dotnet run --project src/FiveDo.Frontend.Avalonia`. |
| `You must install .NET` | The published package is self-contained; a source build needs the .NET 10 SDK/runtime. |
| `--gl-backend does not recognize ...` | Use `auto`, `wgl`, `angle`, or `software`. |
| Crash before the window appears | Rename `settings.json` (see [`Configuration.md`](Configuration.md)); a malformed file normally falls back to defaults, but a bad custom path can still fail. |
| Crash on close | Known Silk.NET/Avalonia shutdown ordering issue; the shell intentionally does not dispose the GL surface during close. If it persists, capture the stack trace. |
| Missing `OpenAL32.dll` | Rebuild so `Directory.Build.targets` copies OpenAL Soft, or place the package's `soft_oal.dll` beside the executable as `OpenAL32.dll`. |

---

## 2. BIOS problems

| Symptom | Cause / action |
|---|---|
| "Load a BIOS first" | Open a BIOS before Play. |
| `BIOS1 must be exactly 1048576 bytes` | The file is not a 1 MB 3DO BIOS. Check the size; do not truncate/pad unless you know the image is valid. |
| `bios info` shows an unexpected MD5 | Verify the dump. The MD5 is the identity used in save states. |
| Game hangs at boot | Try a known-good BIOS (for example `panafz1.bin`). Check the game's required BIOS/region. |
| Anvil detection | `bios info` reports `anvil`/`anvilKind`; the core applies the Anvil workaround automatically. |
| BIOS2 | The desktop opens one BIOS; the CLI `BiosImage.Load` supports BIOS2 only through the API, not a CLI option in this release. |

BIOS ROMs are not included. Use a legally obtained image.

---

## 3. Disc problems

| Symptom | Cause / action |
|---|---|
| `Disc image ... does not exist` | Check the path; quote spaces. |
| `Cannot determine the disc image format` | Use ISO (2048-byte sectors), BIN (2352), CUE+BIN (single data track), or CHD (CD image). |
| `BIN file has neither a CD sync header nor 2048-byte sectors` | The BIN is not a raw 2352-byte CD image or a 2048-byte ISO. Convert/extract correctly. |
| `CHD is not a CD image` | 3DO software is CD-ROM; DVD/HDD/raw CHDs are rejected. |
| `CHD has no data track` | Audio-only CHD; 5DO needs a data track. |
| CUE fails | Only the first data track is used; multi-file/multi-track CUEs may not match. Try extracting the data track to ISO/CHD. |
| Wrong game title/ID | Check `info game.chd --json`; the ID is the MD5 of the first two sectors. A modified/patched disc has a different ID and may not match the database. |
| `fs` fails | The image is not a 3DO Opera volume, or its volume/directory records are malformed. Try `info` and `disc dump` to inspect the container. |
| `disc tracks` fails | The image is not ISO/BIN/CUE/CHD or its track table is malformed. |

Disc images are not included.

---

## 4. Boot or gameplay hangs

| Symptom | Cause / action |
|---|---|
| BIOS splash but no shell | Wait longer; the Portfolio idle loop can look static. Check `--progress`/`--json` `pc`. |
| Black screen after boot | Capture `--frame-out`; if the CPU frame is black too, inspect CEL/RAM; if only the window is black, check the GL backend/fallback. |
| Stuck on loading/race screen | Run with `--progress 1000`, `--cd-trace`, and `--json`; check `cdStatus`/`cdPoll`. The old READ_ID-only wait signature is `0xE1`/`0x0C`. |
| Static READ_ID wait | The CD media-access pulse should advance it; if not, record the trace and frame count. |
| Game-specific regression | Check [`KnownIssues.md`](KnownIssues.md) and [`Compatibility.md`](Compatibility.md) for the title. |
| Wrong fix mode | Try `--no-game-fixes` or `--fix-mode 0x...`; see [`Compatibility.md`](Compatibility.md) section 5. |
| High-res mode breaks a game | `--high-res` is opt-in; 320x240 titles must not be upscaled. Disable it. |
| State load diverges | See section 9. |

Useful commands:

```powershell
dotnet run --project src/FiveDo.Cli -- run game.chd --bios panafz1.bin `
  --frames 100000 --progress 1000 --cd-trace artifacts/cd.txt --json > artifacts/run.json

dotnet run --project src/FiveDo.Cli -- trace game.chd --bios panafz1.bin `
  --frames 40000 --trace-frames 2 --stop-cd 0x83 --count 200000 `
  --out artifacts/trace.txt --dump-ram artifacts/ram.bin
```

---

## 5. Video / OpenGL problems

| Symptom | Cause / action |
|---|---|
| "OpenGL unavailable" / CPU fallback | Update the GPU driver; check Logs for the exact init error. The CPU fallback still runs. |
| Black window but audio runs | Try `--gl-backend software`; if the CPU frame is visible, the GL path is at fault. Run the GPU probe parity capture. |
| Black screenshots | Fixed in the 2026-10-28 pass (float uniform casts). Update; if it persists, use `--gl-backend software` and compare. |
| Flicker/tearing | Try `Vsync` in Settings; the Avalonia compositor may pace independently. Use `--gl-probe-vsync` to check the platform result. |
| xBR/HQx slow at 4K | Use SharpBilinear/Accurate or lower output resolution. HQx 3x/4x are CPU-heavy; xBR runs a GPU 4x pass over the GPU-expanded frame. |
| FSR looks soft/sharp | Adjust Settings > Video > FSR sharpness. 1.0 is maximum sharpening; 0 is soft. |
| CRT too dark/glowy | Adjust Bloom; scanline strength is a renderer default (0.25) not exposed in Settings. |
| Wrong aspect/stretch | Settings > Video > Aspect/Integer scaling. `Stretch` fills the window; `FourThree`/`Integer` letterbox. |
| High-res mode shows a 640x480 game stretched | Hi-Res is opt-in; it is for 640x480 titles. Disable it for 320x240 games. |
| Linux/macOS GL launch fails | Desktop GL launch validation is incomplete for those platforms in this release; try `software` or a different driver/context. |

GPU probe:

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --gl-probe artifacts/gl-probe/check.json `
  --gl-probe-size 320x240 --gl-probe-filter accurate --gl-probe-offscreen `
  --gl-probe-capture artifacts/gl-probe/check.png
```

If `Parity.Matches` is false, the GPU palette path and CPU oracle disagree; record the
report and backend.

---

## 6. Audio problems

| Symptom | Cause / action |
|---|---|
| No sound at all | Check Settings > Audio (enabled/volume), OS output device, and Logs. If no OpenAL device is found, the app logs a warning and runs silently. |
| Test tone fails | No playback endpoint; use `ALSOFT_DRIVERS=null` for tests or configure a device. |
| Constant crackle | Update to the 2026-10-28+ build (free-buffer pool fix). Capture `FIVEDO_AUDIO_TAP` and compare tap vs loopback. |
| Crackle only on Bluetooth | Tap is clean but loopback is not; the link/codec is the issue. |
| Pitch too high / 600 frames = 10.07 s | Old 8.24 DSP clock; current builds produce 10.000 s. |
| Audio drifts/stutters | Check dropped/underrun counters; reduce UI load; the frontend ring is 8192 frames. |
| High latency | The frontend uses a 3072-frame prefill (~70 ms) plus queued buffers (~113 ms steady state). Lower prefill only if you accept underruns. |
| CLI live audio is a short burst | Add `--realtime`. |
| WAV silent | `--audio null` discards; use `--audio wav --wav-out <path>`. |
| WAV click at state load | WAV capture spans resets/loads by design; split the capture. |
| Frontend AOT audio is not validated | Use the trimmed IL build for audio testing; CLI AOT live OpenAL is validated. |

Audio analysis:

```powershell
dotnet run --project src/FiveDo.Cli -- audio analyze artifacts/capture.wav --json
```

See [`Audio.md`](Audio.md) for metric definitions and the tap workflow.

---

## 7. Input problems

| Symptom | Cause / action |
|---|---|
| Keyboard does nothing | Click the game view to focus it; the Library view and TextBox focus deliberately swallow game keys. |
| Wrong buttons | Check **Input Bindings**; the defaults are arrows, Z=A, X=B, C=C, A=L, S=R, Enter=P, Backspace=X. |
| Gamepad not detected (Windows) | Use XInput-compatible controller; check **About** for the connected count. |
| Gamepad not detected (Linux) | Ensure the user is in the `input` group and `/dev/input/event*` is readable. |
| Gamepad not detected (macOS) | No platform backend yet; use a keyboard. |
| Analog stick does nothing | Left-stick directions fall back to D-pad bindings; explicit stick bindings win. Triggers fall back to shoulders. |
| Hotkeys F5/F8/F6/F7/F9/F12/F10 do nothing | The shell dispatches the bindings in the input profile; check **Input Bindings** for a custom map, and make sure the game view has focus. The Library view and TextBox focus deliberately swallow game keys. |
| Scripted input seems offset after a state load | Scripts are absolute by default; add `--script-base state` to shift by the loaded frame count. |
| Autopilot won't combine with a script | `--autopilot road` and `--input-script` are mutually exclusive. |

---

## 8. Library problems

| Symptom | Cause / action |
|---|---|
| "No games found" | Add a folder containing `.iso`, `.bin`, `.cue`, or `.chd`; scanning is recursive to depth 4. |
| Game shown as Unknown | The 4DO database does not recognize the ID; the file still boots. |
| Cover art missing | Put a sibling image with the same base name: `game.png`, `.jpg`, `.jpeg`, or `.webp`. |
| Duplicate entries | A CUE and its BIN may both be scanned; use the CUE or remove the duplicate. |
| Wrong title | Check `info <disc> --json`; the database is keyed by the two-sector MD5. |
| Library slow | Large folders/CHDs are scanned on a background task; narrow the folders or use list mode. |

---

## 9. Save-state problems

| Symptom | Cause / action |
|---|---|
| Save is slow / disk fills | Format version 2 compresses large sections with Brotli; a typical state is 0.6-2.0 MB on disk (the uncompressed payload is ~25 MB: 19 MB RAM backing + 2 MB ROM + VDL frame + devices). Version-1 states are ~25 MB each. Ten compressed slots use a few MB; the 100-slot maximum stays well under 1 GB. |
| `invalid magic value` | Not a `.tdos`, or corrupted/truncated. |
| `failed its CRC check` | Corruption/partial copy; restore from backup. |
| `missing sections` | Truncated/incomplete state. |
| BIOS hash warning | The loaded BIOS differs from the one used to write the state; the load continues but may diverge. |
| Different disc accepted | The loader records the disc hash but does not reject a mismatch; verify the slot metadata/game ID. |
| State loads but audio glitches | Live audio is reset on load; the DSP resumes. A WAV may span both timelines. |
| Thumbnail missing | Optional; the state is still valid. |
| Metadata missing | The sidecar is optional; the header has game ID/frame count. |
| `state inspect` fails | It validates every section CRC; `state slots` uses a faster header-only path. |

See [`SaveStates.md`](SaveStates.md).

---

## 10. Screenshot problems

| Symptom | Cause / action |
|---|---|
| Screenshot is black | Update to the float-uniform fix; try `software` backend. |
| Screenshot is the CPU image, not the filter | GL was unavailable or readback failed; Logs show the fallback. |
| Native screenshot missing | Enable Settings > Video > "Also save the native CPU frame". |
| Wrong directory | Settings > Paths; empty means Pictures. |
| Filename collisions | Names include a timestamp and frame count; two captures in the same second/frame can collide. |

---

## 11. CLI problems

| Symptom | Cause / action |
|---|---|
| `run needs --bios ... unless --state-in` | Supply `--bios` or load a state. |
| `--script-base state requires --state-in` | Add `--state-in` or use `absolute`. |
| `--autopilot` + `--input-script` error | They are mutually exclusive. |
| `fs`/`disc tracks` fails on ISO/BIN | The image may not contain a valid 3DO volume/track table; check `info` and `disc dump`. |
| `state inspect` JSON is PascalCase | Historical contract; other CLI JSON is camelCase. |
| Address options behave oddly | `--watch-start`, `--watch-end`, `--stop-pc`, `--stop-cd`, `--dump-ram-offset`, `--base` are hex even without `0x`. |
| `--capture-dir` writes nothing | Add `--capture-every N` (>0). |
| `--audio openal` warns and is silent | No device; the run continues. |
| Unknown command exit 2 | Check the command name; `--help` lists commands. |

---

## 12. Performance problems

| Symptom | Cause / action |
|---|---|
| Benchmark gate fails | Run on a quiet machine; check p99 vs mean; use `--baseline` for same-machine comparison. |
| 4K stutters | Use SharpBilinear/Accurate; xBR/HQx/FSR cost more. Check GPU probe percentiles. |
| CPU fallback is slow | Expected; it is not a 4K60 path. Use a GL/GLES context. |
| High-res mode is slow | 640x480 increases CEL/VDLP/SPORT work; disable it for 320x240 titles. |
| Gameplay is slow when started from the IDE | Debug builds optimize the core projects by default (`-p:FiveDoOptimizeDebug=false` opts out). If Visual Studio's debugger suppresses JIT optimization on module load, use Start Without Debugging or disable that debugger option. Check the actual build: a Debug run with optimization disabled measured ~25 ms/frame on the Road Rash race. |
| Frame pacing oscillates | Check audio samples/frame; the pacer clamps 50-70 fps and falls back to 60 Hz. |
| Auto frame skip does not raise the presented rate | Auto is presentation-only. If the emulated CPU/CEL/DSP is the bottleneck, reducing filter cost or output resolution will not help either; use a Release/optimized build and check the benchmark core time. Fixed frame-skip modes are available when the GPU/presenter is the bottleneck. |
| Memory grows | Check for per-frame allocations with the benchmark/probe; steady state should be 0 bytes/frame. |

---

## 13. Build/test problems

| Symptom | Cause / action |
|---|---|
| `NETSDK1045` | Install .NET 10 SDK 10.0.301 (the version pinned by `global.json`). |
| Restore fails | Check NuGet connectivity/proxy; run `dotnet restore 5DO.sln`. |
| Warnings are errors | The build uses `TreatWarningsAsErrors`; fix the warning or suppress with justification. |
| GPU test fails headless | `FIVEDO_GPU_TESTS` is opt-in; do not set it without a desktop GL/ANGLE context. |
| Audio test fails headless | Set `ALSOFT_DRIVERS=null` or skip `FIVEDO_AUDIO_TESTS`. |
| Frontend smoke times out | It needs a desktop window station; run interactively. |
| `dotnet format` fails | Run `dotnet format 5DO.sln`, then rebuild/test. |
| OpenAL native missing | Rebuild; check `OpenAL32.dll` beside the executable on Windows. |
| CHD test fails | CHDSharp only reads valid MAME CD CHDs. |

See [`BuildAndTest.md`](BuildAndTest.md).

---

## 14. What to include in a bug report

- 5DO version/commit (`--version`, `git rev-parse --short HEAD`).
- OS, CPU/GPU, driver version, audio device.
- Exact command line and working directory.
- BIOS/disc hashes (`bios info`, `info --json`), not the files.
- Frame count, `run --json`, `state inspect --json` (if a state is involved).
- Relevant Logs lines.
- For audio: `tap.wav`/`tap.csv` (or a short WAV capture) and `audio analyze --json`.
- For video: `--gl-probe` JSON, backend, and a CPU/GPU capture pair.
- For performance: benchmark JSON, baseline, and whether the machine was idle.
- For hangs: `--progress`, `--cd-trace`, `trace` ring, and RAM dump around the PC.
- Whether the issue reproduces from a clean BIOS/disc/state.

Do **not** attach BIOS ROMs, game images, or extracted copyrighted assets.

---

## 15. Legal / distribution

- 5DO is distributed under the FreeDO modified GNU LGPL; see [`LICENSE`](https://github.com/purelogiccode/5DO/blob/master/LICENSE).
  FreeDO/4DO are not standard OSI-licensed, and the modified LGPL includes a
  non-commercial restriction. 5DO is **donationware** (non-commercial,
  source-available; voluntary donations only). Read [`Licensing.md`](Licensing.md) and
  [`THIRD-PARTY-NOTICES.md`](https://github.com/purelogiccode/5DO/blob/master/THIRD-PARTY-NOTICES.md) before distributing builds.
- The bundled OpenAL Soft 1.23.1 runtime adds an LGPL-2.0-or-later binary distribution
  obligation; the release legal review is still pending.
- "3DO", "Panasonic", and "GoldStar" are trademarks of their respective owners. This
  project is not affiliated with or endorsed by them.
