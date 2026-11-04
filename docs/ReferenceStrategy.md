# Reference Strategy: Opera/libopera as the Primary Behavioral Reference

**Adopted:** 2026-10-23
**Status:** active
**Supersedes:** the earlier rule that `References/4DO-master/FreeDOCore` is the primary
reference for all new behavior.

---

## 1. Decision

The C# emulator was originally ported from FreeDOCore (the 4DO fork of FreeDO).
That port remains the baseline for the existing code and tests. **For all new
accuracy and compatibility work, `References/opera-libretro-master/libopera` is now
the primary behavioral reference.**

This is a reference change, not a rewrite:

- The C# project layout, public APIs, deterministic scheduler, and test strategy stay.
- Existing code keeps its FreeDOCore provenance comments unless the behavior is
  deliberately changed.
- New or changed behavior is traced to Opera and covered by a test.
- FreeDO/4DO remains available as the historical baseline and as a tie-breaker for
  behavior the current tests intentionally preserve.

### Reference hierarchy

| Priority | Reference | Use |
|---|---|---|
| 1 | `References/opera-libretro-master/libopera` | New compatibility/accuracy fixes; game behavior; ARM, CLIO, MADAM, DSP, CD-ROM, VDLP, PBUS |
| 2 | `References/4DO-master/FreeDOCore` | Original C# port baseline; existing golden tests; FreeDO-specific quirks |
| 3 | `References/freedo-master` | Historical/differential comparison only |
| 4 | `References/CPP_SimpleGui/Cores/OperaCore` | Windows empirical harness for Opera (not product code) |
| 5 | `References/Docs3/*` | Hardware truth when the software references disagree |

Do not commit anything from `References/`; it is gitignored.

---

## 2. Why Opera is primary

Opera is a fork of 4DO, which is a fork of FreeDO, but it has been actively
maintained and contains fixes that are not in the FreeDO/4DO code we originally
ported. Concrete examples:

| Area | FreeDO/4DO baseline | Opera |
|---|---|---|
| ARM cycle classes | `Ncycle = 4` | `Ncycle = 2`, with references to the ARM60 datasheet and 3DO SDK |
| CLIO timers | 25 MHz clock plus a simpler event model | 16 timer slots with explicit slack, modeled from Portfolio OS |
| CD-ROM | `Iso.cpp` + `XBUS.cpp`, ~1.7k lines total | `opera_cdrom.c`, ~4.4k lines: command state machine, DIPIR, ODE, subcode, chunks, personas, CD audio |
| MADAM | CEL engine and PBUS DMA | CEL busy pacing, DMA8/PBUS control-port semantics, projector/clipping fixes |
| VDLP | one display path | multiple pixel formats, high-resolution option, CLUT DMA checks |
| State/tests | no shared state format | versioned state chunks, deterministic PRNG, headless test harness |

The practical difference is larger than the line counts suggest: Opera models
several handshakes and timing paths that FreeDO approximated.

### Empirical result (2026-10-23)

The data track of each local CHD was extracted to ISO with the project CLI, then run
with the bundled Windows OperaCore (`References/CPP_SimpleGui/Cores/OperaCore`) and
the same `panafz1.bin` BIOS:

| Title | Our C# port | Opera reference |
|---|---|---|
| Road Rash | Stops at SAudio channel 0 (`-6996`) | Renders the intro FMV for 60 s |
| Wolfenstein 3D | Stops at SAudio channel 0 (`-6996`) | Renders the intro for 60 s |
| Alone in the Dark | Stops during AudioFolio initialization (`-6996`) | Renders the intro/loading sequence for 60 s |
| Space Ace | Renders the demo intro, then runs away at frame ~681 | Plays the demo intro for 60 s with no runaway |

The same ISO still fails in our port, so the problem is not the CHD reader. This
**disproves the earlier "shared FreeDO limitation" conclusion**: the remaining audio
and Space Ace failures are bugs or missing behavior in our port. Opera is the better
oracle.

The 2026-10-24 pass decoded the common failure:

- SAudio `-6996` is `SAudioErrAllocInstFailed`; the raw AudioFolio error is lost by
  the SDK subscriber.
- The DSP was not executing at all in headless runs because
  `AudioSink?.PushSample(Dsp.Loop())` short-circuited the call. That is fixed.
- With the DSP executing, the ARM's Sema4 command still sits at `status=0x8,
  data=0`, and the DSP trace shows a repeating sample loop that never reads
  `Sema4Data` or writes the ACK.
- Space Ace's frame-681 runaway is the player's audio task copying to a null
  `[audioState+0x9C]` after the same failed `AllocInstrument` path, not a random DMA
  corruption.

**Audio clock differential (2026-10-27).** The Opera test harness
(`tools/test_harness.c` + `make harness`) was built in WSL and run against
`panafz1.bin` and the local CHDs. A 600-frame Lemmings capture produced 441,441
samples / 10.010 s at Opera's 59.94 Hz field rate, i.e. exactly 44.1 kHz. Our
FreeDO-based scheduler produced 444,097 samples / 10.070 s (~44.4 kHz); the exact
DSP clock now produces 441,015-441,020 samples / 10.000 s. Road Rash 6,000-frame
captures have identical peak (29,951), RMS within 0.2%, and spectral peaks
(442.1 Hz) before/after, confirming the content did not change. The harness is a
developer reference only; see `TestCorpus.md` 1.5a2 for the stats. Reproduce it in
WSL from `References/opera-libretro-master`:

```bash
make -j8
make harness
./opera-test-harness --core ./opera_libretro.so \
  --bios /path/to/panafz1.bin --title "/mnt/e/Panasonic 3DO/Lemmings (USA).chd" \
  --frames 600 --audio /tmp/lem-opera.wav --metrics /tmp/lem-opera.json \
  --wall-timeout 120
```

The next differential is therefore **DSP-only**: `opera_dsp.c` against `Dsp.cs`,
starting with the special registers, flags, `exact`/branch table, operand loader,
and the head/tail command entry. Do not touch the CD path or the ARM core until the
DSP handshake is understood.

Caveats:

- The comparison verified video/game progress, not audio output.
- Space Ace's `d52bf113` mixer error also appears in Opera but is non-fatal; our
  runaway is a downstream consequence of the same failed instrument allocation.
- Opera has its own HLE paths and a built-in BIOS option. We run a real BIOS, so do
  not adopt those paths blindly.
- The XBUS DMA byte order is correct as-is (`b0,b1,b2,b3` through the raw `IRam`
  accessor). Opera's `b3,b2,b1,b0` applies to its CPU-visible `opera_mem_write8`.
  A byte-order change was tried on 2026-10-24 and broke every CD-booting title.

---

## 3. Subsystem map

| Subsystem | C# implementation | Opera reference | FreeDO origin |
|---|---|---|---|
| CPU | `src/FiveDo.Cpu/Arm60.cs` | `libopera/opera_arm.c` | `FreeDO/arm.cpp` |
| Memory/bus | `src/FiveDo.Memory/FiveDoMemory.cs` | `libopera/opera_mem.c` | `_3do_sys.cpp` |
| Scheduler/clock | `src/FiveDo.Machine/Quartz.cs` | `libopera/opera_clock.c` | `quarz.cpp` |
| CLIO | `src/FiveDo.Clio/Clio.cs` | `libopera/opera_clio.c` | `Clio.cpp` |
| MADAM | `src/FiveDo.Madam/*` | `libopera/opera_madam.c` | `Madam.cpp` |
| VDLP | `src/FiveDo.Video/Vdlp.cs` | `libopera/opera_vdlp.c` | `vdlp.cpp` |
| DSP | `src/FiveDo.Dsp/Dsp.cs` | `libopera/opera_dsp.c` | `DSP.cpp` |
| CD-ROM | `src/FiveDo.Cdrom/CdromDevice.cs` | `libopera/opera_cdrom.c` | `Iso.cpp` |
| XBUS/PBUS | `src/FiveDo.Xbus/Xbus.cs`, `Madam.cs` | `libopera/opera_xbus.c`, `opera_pbus.c` | `XBUS.cpp`, `Madam.cpp` |
| SPORT | `src/FiveDo.Sport/Sport.cs` | `libopera/opera_sport.c` | `SPORT.cpp` |
| Save states | `src/FiveDo.Core` state sections | `libopera/opera_state.c` | none |
| PRNG | current CPU/DSP paths | `libopera/prng16.c`, `prng32.c` | `fastrand` |
| Input/PBUS | `src/FiveDo.Input/*` | `libopera/opera_pbus.c` | 4DO input layer |
| Reference tests | `tests/FiveDo.Tests` | `tools/test_harness.c`, `.agents/skills/opera-test-harness` | FreeDO harness |

---

## 4. Porting rules

1. **Reimplement in C#.** Do not add a native dependency, P/Invoke, C++/CLI, or a
   copied C file to the product. Opera is a behavioral reference.
2. **One reference per change.** State whether the behavior comes from Opera,
   FreeDOCore, or a hardware document. Use comments such as
   `// Opera: opera_cdrom.c: cdrom_cmd_read_data`.
3. **Tests first.** Every behavior change needs a unit, golden, or differential test.
   If the behavior is timing-related, add a frame/state assertion.
4. **Preserve the existing baseline.** Do not "clean up" unrelated FreeDO behavior in
   the same change. Keep diffs small and reviewable.
5. **Document intentional divergences.** Put them in `docs/PortingNotes.md` and
   `docs/Compatibility.md`. The CD disc-end fix is the first example.
6. **Do not copy code verbatim.** Reimplement the behavior. Preserve attribution for
   FreeDO, 4DO, and Opera in source headers and release notes.
7. **No copyrighted content.** Never commit BIOS ROMs, game images, extracted assets,
   or Opera binaries.
8. **Hardware wins.** If Opera and FreeDO disagree and the hardware documentation is
   clear, follow the hardware and record why.

### Licensing

Opera inherits FreeDO's modified GNU LGPL with additional restrictions, including a
prohibition on commercial use without the rights holders' approval. See
[`Licensing.md`](Licensing.md). The reference change does not change the project's
license position; it adds another upstream project to the attribution list.

### Distribution

The release bundles contain only first-party compiled output, documentation, and
license/notice files. Nothing from `References/` (FreeDO, 4DO, Opera/libopera,
SimpleGui/OperaCore, or Docs3) is copied into a bundle. Opera remains a behavioral
reference; it is not a shipped dependency. The .NET 10 runtime is a host prerequisite
and is **not** bundled.

The bundle layout does not change the reference hierarchy or the licensing position:
the CLI and desktop app are framework-dependent single-file apphosts built from this
repository, and the OpenAL Soft native runtime stays beside them so its LGPL replacement
rights remain practical.

---

## 5. Differential test procedure

Use the same BIOS, the same disc data, and the same frame count on both sides.

**The automated path is `tools/FiveDo.DiffHarness`.** It stages the OperaCore
library and BIOS, extracts a cached ISO from the CHD when needed, runs both
engines with the same options, and compares checkpoint frame hashes. See
[`DifferentialHarness.md`](DifferentialHarness.md) for the manifest schema,
commands, allowlist policy, and current limitations. The manual procedure below
is retained for ad-hoc investigation and for reference-side fields the ABI does
not expose yet.

### 5.1 Prepare the disc

```powershell
# Get the sector count first
dotnet run -c Release --project src/FiveDo.Cli -- info "$env:FIVEDO_GAMES\Road Rash (USA).chd" --json

# Extract the data track to ISO (example: 328130 sectors)
dotnet run -c Release --project src/FiveDo.Cli -- disc dump `
  "$env:FIVEDO_GAMES\Road Rash (USA).chd" --sector 0 --count 328130 `
  --out "$env:TEMP\roadrash.iso"
```

The harness does this automatically with `DiscImageFactory`; the staged ISO is
cached under `artifacts/diff/work/discs/<title>/`.

### 5.2 Run the Opera reference

The bundled SimpleGui/OperaCore build expects a 1 MB BIOS in a `BIOS` directory next
to `OperaCore.dll`:

```powershell
# One-time setup in the ignored References tree
$core = "References\CPP_SimpleGui\cmake-build-release\SimpleGui\Core"
New-Item -ItemType Directory -Force "$core\BIOS" | Out-Null
Copy-Item bios\panafz1.bin "$core\BIOS\panafz1.bin" -Force

# Run the game (disable the other cores first if the registry picks one)
& "References\CPP_SimpleGui\cmake-build-release\SimpleGui\SimpleGui.exe" `
  --debug-messages -n "$env:TEMP\roadrash.iso"
```

The harness copies the core and BIOS into a private runtime directory instead of
editing the reference tree.

### 5.3 Run our emulator

```powershell
dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$env:TEMP\roadrash.iso" --bios bios\panafz1.bin --frames 3000 `
  --checkpoints 3000 --audio-hash --state-hash `
  --kprint "$env:TEMP\rr-kprint.txt" --frame-out "$env:TEMP\rr.png" --json
```

### 5.4 Compare

- KPRINT stream and the first differing line.
- Checkpoint PC, frame hash, audio hash, state hash, DSP sample count, CD command transcript.
- Screenshot at the same frame.
- Save state at a fixed frame, if the behavior is state-dependent.

The automated harness compares the frame hash by default. Audio hashes are
informational until the reference core accepts a fixed PRNG seed; PC/state/CD
transcript fields are not exposed by the SimpleGui ABI and remain 5DO-side
goldens.

Record the result in [`Compatibility.md`](Compatibility.md),
[`TestCorpus.md`](TestCorpus.md), or the harness report. A differential run is
evidence only if the BIOS, disc hash, frame count, build, and manifest are
recorded.

---

## 6. Known findings and starting points

### SAudio / `-6996`

- Opera passes the point where our port stops.
- Changing `Arm60.Ncycle` from 4 to 2 changed the execution point but did **not** fix
  `-6996` or Space Ace. CPU timing alone is not the cause.
- Using the extracted ISO instead of the CHD does not change the failure, so it is
  not a CHD-reader bug.
- Next: trace `AllocInstrument`/`LoadInsTemplate`, the instrument file read, and the
  DSP resource tables. Compare the CD command/status stream and the file contents
  against Opera.

### Space Ace runaway

- Opera plays the demo for 60 s; our port takes a FIQ at frame ~681 with the vector
  table at `0x1C` zeroed and a stack full of `0x11111111`.
- Next: add a low-memory write watchpoint (`0x0-0x100`) and log the writer (CPU, CEL,
  DMA, or PBUS). Compare the MADAM/DMA8 path with Opera.

### 4K60

- The CPU side is ~2.1-2.3 ms/frame, leaving ~14 ms at 60 Hz.
- 4K output is a single fullscreen quad after the native-resolution palette pass; it
  is not 4K internal CEL rendering.
- Desktop 4K60 is high confidence. Tier B desktop 1080p60 is plausible but unmeasured.
  Measure GPU time, p99, allocations, and present-to-present before claiming
  the target.

---

## 7. Definition of done for the migration

- [x] Decision recorded and linked from the plan, status, porting notes, and README.
- [x] Empirical comparison recorded for the five local titles.
- [x] Differential harness documented and reproducible.
- [x] Release bundles contain no reference binaries or copyrighted content.
- [ ] First targeted port (CD-ROM read/status semantics) merged with tests.
- [ ] SAudio `-6996` root cause identified and fixed.
- [ ] Space Ace low-memory writer identified and fixed.
- [x] ARM cycle-class change evaluated behind a test/option (`--arm-timing`, `--cycle-classes`, exact class-cost tests).
- [ ] 4K GPU/p99 measurements recorded on Tier A and Tier B.
