# Next Session Handoff

**Date:** 2026-10-23
**Goal:** start the Opera-based differential port and remove the two P0 real-game
blockers: SAudio `-6996` and Space Ace's low-memory runaway.

This is the first session after adopting Opera/libopera as the primary behavioral
reference. Do **not** start with UI polish, 4K tuning, or a wholesale CD-ROM rewrite.
The next session is a debugging session with a small, evidence-driven code change.

---

## 1. Preconditions

- Repository is on `master`, clean, and in sync with `origin/master`.
- `References/` is ignored and contains the reference trees; it is not committed.
- `bios/panafz1.bin` and the five CHDs in `D:\Samples\3DO` are available locally.
- `dotnet build -c Release`, `dotnet test tests/ThreeDo.Tests -c Release`, and
  `dotnet format --verify-no-changes` pass.
- The Opera reference was built/used through
  `References/CPP_SimpleGui/cmake-build-release/SimpleGui/SimpleGui.exe`; its
  `Core/BIOS/panafz1.bin` is present.

Read these first:

- [`ReferenceStrategy.md`](ReferenceStrategy.md) - reference hierarchy and porting rules.
- [`Status.md`](Status.md) - current position and ordered next steps.
- [`Compatibility.md`](Compatibility.md) - game matrix and Space Ace findings.
- [`TestCorpus.md`](TestCorpus.md) - corpus, baselines, and diagnostic commands.
- [`PortingNotes.md`](PortingNotes.md) - C# porting contract.

---

## 2. What is already known

- Opera runs Road Rash, Wolfenstein 3D, Alone in the Dark, and Space Ace past the
  points where our port stops. The comparison used the same BIOS and an ISO extracted
  from the same CHD.
- Our port stops at SAudio channel 0 (`-6996`) in Road Rash, Wolfenstein, and Alone.
- Space Ace renders the demo intro, then takes a FIQ at frame ~681 with the vector
  table at `0x1C` zeroed and a stack full of `0x11111111`.
- `Arm60.Ncycle = 2` changed the execution point but did not fix either problem.
- Running from ISO instead of CHD did not change the SAudio failure, so the CHD reader
  is not the cause.
- Opera's Space Ace mixer error `d52bf113` is `AF_ERR_BADFILETYPE` and is non-fatal;
  our runaway is a separate bug.

---

## 3. Ordered plan

### Step 1 - Freeze a reproducible baseline

Run Road Rash and Space Ace from the extracted ISO and keep the logs:

```powershell
$temp = "$env:TEMP\3do-next"
New-Item -ItemType Directory -Force $temp | Out-Null

dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "$env:TEMP\3do-opera\roadrash.iso" --bios bios\panafz1.bin --frames 3000 `
  --kprint "$temp\rr-kprint.txt" --cd-trace "$temp\rr-cd.txt" `
  --fiq-trace "$temp\rr-fiq.txt" --frame-out "$temp\rr.png"

dotnet run -c Release --project src/ThreeDo.Cli -- run `
  "$env:TEMP\3do-opera\spaceace.iso" --bios bios\panafz1.bin --frames 1200 `
  --kprint "$temp\sa-kprint.txt" --fiq-trace "$temp\sa-fiq.txt" `
  --frame-out "$temp\sa.png"
```

If the extracted ISOs are gone, recreate them with `3do disc dump`; the sector counts
are in [`TestCorpus.md`](TestCorpus.md). Record the final PC, frame hash, and first
failing KPRINT line.

### Step 2 - Decode the SAudio failure

The error text is from the game's SAudio library, not necessarily the BIOS. Work from
the guest side:

1. Find the `Initializaion for SAudio channel 0 failed` string and its callers in the
   game's OS. The game OS is on the disc; extract the relevant module or search the
   ISO for the string.
2. Identify the value `-6996` at the call site. Determine whether it is:
   - an AudioFolio `AF_ERR_*` code,
   - a DSP resource-allocation failure,
   - a file-read/return-count error, or
   - a game-specific SAudio code.
3. Trace `AllocInstrument`/`LoadInsTemplate` and the instrument file read. Compare:
   - the CD command/status transcript,
   - the instrument file bytes,
   - the DSP program/data/FIFO resource tables,
   - the ARM/DSP semaphore and FIQ sequence.
4. Compare the same point in Opera. If guest KPRINT is needed, rebuild the bundled
   OperaCore wrapper with `opera_madam_kprint_enable()` or use the libretro test
   harness; the prebuilt wrapper does not enable it.

Do not change DSP behavior until the failing resource or return code is identified.

### Step 3 - Find the Space Ace low-memory writer

Add a temporary diagnostic watchpoint for writes to `0x0000-0x0100` (or the first
4 KB) in the memory/bus layer. Log:

- the guest PC,
- the writer (CPU, CEL, DMA8/PBUS, CD/XBUS, or DSP),
- the address, value, and size,
- the frame number.

The goal is to catch the write that replaces the FIQ vector with zero and fills the
stack with `0x11111111`. Compare the same scene in Opera. Likely areas:

- `Madam.HandleDma8` / `DmaPbus` and the first control-port word,
- CEL destination/clipping arithmetic,
- PBUS packet length/destination,
- a missing memory fence around DMA.

Keep the watchpoint developer-only; do not leave per-write logging enabled in the
default build.

### Step 4 - Make the smallest fix and prove it

- Add a focused unit or integration test for the fixed behavior.
- Re-run Road Rash, Wolfenstein, Alone, and Space Ace for at least 3,000 frames.
- Confirm the KPRINT error is gone or that the game advances past it.
- Confirm Space Ace reaches 3,000 frames without a FIQ at `0x1C`.
- Record before/after frame hashes and logs.
- Update `Compatibility.md`, `TestCorpus.md`, and the progress log in
  `ImplementationPlan.md`.

---

## 4. Acceptance criteria for the next session

- [ ] A reproducible baseline exists for Road Rash and Space Ace with logs and frame
      hashes.
- [ ] The SAudio `-6996` failure has a specific root cause (file/return code/resource),
      not just "audio is broken".
- [ ] Space Ace's low-memory writer is identified with PC and writer type.
- [ ] At least one of the two P0 issues is fixed with a test, or a minimal reproducer
      and a concrete next hypothesis are recorded.
- [ ] `dotnet build -c Release`, `dotnet test`, and `dotnet format` pass.
- [ ] Docs are updated and the repository is clean and pushed.

If the root cause is deeper than one session, stop at a written diagnosis and a
minimal reproducer. Do not start a speculative DSP or CD-ROM rewrite.

---

## 5. Do not do in this session

- Do not change `Arm60.Ncycle` from 4 to 2 as a default. If you want to evaluate it,
  put it behind an explicit experiment/option and a differential test.
- Do not port `opera_cdrom.c` wholesale. Port the specific command/status/read
  behavior that the trace proves is wrong.
- Do not touch the UI, renderer filters, 4K settings, or packaging.
- Do not commit BIOS ROMs, game images, extracted assets, or Opera binaries.
- Do not copy Opera source verbatim; reimplement the behavior in C# and preserve
  attribution.

---

## 6. Useful commands

```powershell
# Build/test/format
dotnet build -c Release
dotnet test tests/ThreeDo.Tests -c Release
dotnet format --verify-no-changes

# Disc geometry and extraction
dotnet run -c Release --project src/ThreeDo.Cli -- info "D:\Samples\3DO\Road Rash (USA).chd" --json
dotnet run -c Release --project src/ThreeDo.Cli -- disc dump `
  "D:\Samples\3DO\Road Rash (USA).chd" --sector 0 --count 328130 `
  --out "$env:TEMP\3do-opera\roadrash.iso"

# Instruction-ring trace around a known stop
dotnet run -c Release --project src/ThreeDo.Cli -- trace `
  "D:\Samples\3DO\Road Rash (USA).chd" --bios bios\panafz1.bin `
  --frames 3000 --trace-frames 200 --ring 256 --out "$env:TEMP\rr-trace.txt"

# Opera reference (same BIOS, extracted ISO)
& "References\CPP_SimpleGui\cmake-build-release\SimpleGui\SimpleGui.exe" `
  --debug-messages -n "$env:TEMP\3do-opera\roadrash.iso"
```

---

## 7. Open questions

1. Is `-6996` an AudioFolio error, a DSP resource error, or a game-specific SAudio
   code? The printed value does not match the standard `AF_ERR_*` encoding, so decode
   it from the guest call site rather than guessing.
2. Does the instrument file read return the expected byte count and contents? Compare
   the CD transcript and the file bytes with Opera.
3. Is the Space Ace corruption caused by CEL/DMA8/PBUS, or by a CPU write through a
   bad pointer? The watchpoint will answer this.
4. Does Opera's `DMAPBus` first-word write (`0xFFFFFFFF` at the control-port word)
   matter for Space Ace? Compare the DMA destination/length registers.
5. Is the current `Madam.HandleDma8` missing the CEL-busy pacing that Opera added?
   If so, the CPU may overwrite CEL state before the engine finishes.

When these are answered, the next session can move from diagnosis to a targeted port
with confidence.
