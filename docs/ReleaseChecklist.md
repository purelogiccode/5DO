# v1.0 Release Checklist

**Adopted:** 2026-10-26
**Release definition:** v1.0 is a **documented-compatibility release**, not a
full-compatibility one. Ship the platforms and titles that are validated, publish an
honest per-title matrix and known issues, and move everything else to the post-1.0
backlog.

This checklist is the release gate. A checked box must have evidence (a command, CI
run, screenshot, or recorded hash), not just an intention. Update `docs/Status.md`,
`docs/Compatibility.md`, `docs/KnownIssues.md`, and `CHANGELOG.md` as items complete.

---

## 0. Recommended v1 scope

These are recommendations; the release owner confirms or changes them in section 1.
The important rule is: **do not claim a platform or a playable title that has not
been validated.**

| Item | Recommended v1 scope | Notes |
|---|---|---|
| Validated desktop platform | **Windows x64** | WGL and ANGLE/GLES validated; CPU fallback works |
| Secondary desktop builds | Linux x64 / macOS x64 only after launch/render smoke | Otherwise label as preview/best-effort or omit |
| arm64 / Raspberry Pi | **Post-1.0** | GLES 3.1, Pi timing, and DPI are unvalidated |
| Compatibility set | 12 boot-validated titles, with 5 showcase titles | Per-title status must be honest |
| Disc formats | ISO, BIN, CUE+BIN, CHD | Already implemented and tested |
| Renderer | OpenGL default, CPU fallback | Filter family is complete |
| Packages | Portable zip/tar.gz | Installers/signing/notarization are post-1.0 |
| Version | `v1.0.0-rc.1` for the candidate; `v1.0.0` after legal sign-off | Bump `FiveDoVersion.Version` and `Directory.Build.props` |

---

## 1. Scope and decisions

- [x] Confirm supported RIDs and which one is the **validated** target:
      **Windows x64** is the validated target for 1.0.0-rc.1; the other RIDs are
      build artifacts only unless separately smoke-tested.
- [x] Confirm the supported title list and each title's status:
      **Playable**, **Boots/menus**, or **Known issues** (12 documented titles; five
      showcase titles with scripted gameplay evidence).
- [x] Confirm the release version: **1.0.0-rc.1** for this candidate; 1.0.0 after
      legal/distribution sign-off.
- [x] Confirm the distribution model and license in `docs/Licensing.md`:
      FreeDO modified GNU LGPL, non-commercial/source-available, recorded in the root
      `LICENSE`.
- [x] Confirm installers/signing/notarization are **out of scope** for 1.0.0-rc.1.
- [x] Record the decisions in this file and in `docs/Status.md`.

Recommended title statuses:

| Title | v1 status to document |
|---|---|
| Wolfenstein 3D | Playable: fresh game, movement/fire, save/load; known long load |
| Lemmings | Playable: passcode path, gameplay start, save/load |
| Road Rash | Playable race, not full-race completion; document the known wreck/end state |
| Alone in the Dark | Playable room/menu/intro; inventory not scripted |
| Space Ace | Playable interactive scene start; longer decisions unvalidated |
| Gex | Boots/attract scene; input not scripted |
| Need for Speed | Boots/menus; input not scripted |
| Super Street Fighter II Turbo | Boots/attract match; input not scripted |
| Total Eclipse | Boots/attract gameplay; input not scripted |
| Killing Time | Boots/title menu; input not scripted |
| Theme Park | Boots/title menu; captured menu audio is silent |
| Return Fire | Boots/menu; input not scripted |

---

## 2. Legal and attribution (hard gate)

- [x] Distribution/license decision recorded in `docs/Licensing.md` and the root
      `LICENSE`.
- [x] `THIRD-PARTY-NOTICES.md` covers FreeDO/4DO, Opera/libopera, CHDSharp, Avalonia,
      Silk.NET, Serilog and its console/file sinks, the bundled OpenAL Soft 1.23.1
      runtime, and other NuGet dependencies. See
      [`ReleaseLegalReview.md`](ReleaseLegalReview.md).
- [x] OpenAL Soft LGPL-2.0-or-later obligations (notice, corresponding source
      offer, relinking rights) are included in the release decision. The runtime is
      bundled for all six RIDs. Evidence: `docs/OpenALSoft.md` records the notice,
      the exact upstream 1.23.1 source, the three-year written source offer, and
      relinking instructions; `docs/licenses/OpenAL-Soft-COPYING.txt` contains the
      complete LGPL-2.0 text; `packaging/package.ps1` copies both into every
      archive, and the win-x64 archive was inspected after repackaging.
- [x] About dialog and CLI `--version`/license output are current.
- [x] No BIOS ROM, disc image, extracted asset, WAV capture, or copyrighted game
      content is tracked in the repository; packaging uses only publish output plus
      README/notices/licensing.
- [ ] Release owner/legal sign-off recorded in section 6.

This is the only item that can block the release independently of engineering. Start
it first. The engineering audit and the remaining owner/legal decisions are recorded
in [`ReleaseLegalReview.md`](ReleaseLegalReview.md).

---

## 3. Build, test, and CI

- [x] `dotnet build 5DO.sln -c Release` passes with 0 warnings and 0 errors
      (1.0.0-rc.1).
- [x] `dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release` passes:
      860/860 with 1.0.0-rc.1 (after the 2026-10-29 Vulkan pass).
- [x] `dotnet format 5DO.sln --verify-no-changes` passes.
- [ ] CI is green on the supported runners. (Local gates pass; the GitHub Actions
      workflow was not re-run in this session.)
- [x] Synthetic benchmark `--gate` passes: 2.28 ms/frame core mean, 4.03 ms core
      p99, 4.66 ms total p99, 0 bytes/frame.
- [x] Trimmed CLI smoke test passes: `--version`, `selftest --json`,
      `bench --frames 2 --json`, `info`, and a 300-frame disc run.
- [x] Trimmed frontend smoke test passes (`--smoke-test`).
- [x] NativeAOT CLI/frontend smoke tests are not shipped in 1.0.0-rc.1; the
      trimmed IL artifacts are the release artifacts.

Suggested commands:

```powershell
dotnet build 5DO.sln -c Release
dotnet test tests/FiveDo.Tests/FiveDo.Tests.csproj -c Release
dotnet format 5DO.sln --verify-no-changes
dotnet run -c Release --project benchmarks/FiveDo.Benchmarks -- --frames 600 --gate
```

---

## 4. Stability and compatibility

Run a **10-minute soak** (36,000 frames at 60 Hz) on each showcase title, and at least
a boot/menu soak on the remaining supported titles. Record final PC and frame hash;
compare against `docs/TestCorpus.md` where a baseline exists.

Use `tools/release-soak.ps1` for the per-machine run. The corpus differs between the
two development machines, so a `Missing on this machine` row is expected and is not a
failure. The script uses archived 12k states for absent discs when available and
writes `summary.md`/`summary.json` for the release evidence.

- [x] Wolfenstein 3D soak: 36,000 frames, exit 0; `0x0814F039`.
- [x] Lemmings soak: 36,000 frames, exit 0; `0x9939D345`.
- [x] Road Rash soak: 36,000 frames, exit 0; `0xBDC2DBEA`.
- [x] Alone in the Dark soak: 36,000 frames, exit 0; `0xBA7F2C8C`.
- [x] Space Ace soak: 36,000 frames, exit 0; `0xFD0F5B72`.
- [x] Gex, Need for Speed, SSF2 Turbo, Total Eclipse, Killing Time, Theme Park,
      and Return Fire: 36,000-frame state soak from the archived 12k states on
      1.0.0-rc.1; all exit 0. The source discs for these seven are not present on
      this machine, so this is a continuation soak, not a fresh boot.
- [x] Save/load roundtrip on each showcase title: zero-frame hash matches and two
      120-frame continuations are identical (see `Compatibility.md` §10.2).
- [x] Audio is non-silent 44.1 kHz stereo on the showcase titles and uses the exact
      44,100-sample clock (10.000 s per 600 frames). Road Rash's race mix clips
      (2,036 frames in a 92k-state capture), the 36k Lemmings gameplay state happened
      to be silent while its 23k state is non-silent, and Theme Park's menu is silent;
      all are documented in `TestCorpus.md` 1.5a2. Live OpenAL output, audio-driven
      pacing, WAV analysis, and AOT CLI audio are validated. The 2026-10-28 pass
      fixed the remaining live-output crackle (buffer ownership) and validated it
      with the PCM tap: 0 OpenAL errors, 0 underruns, and 0 discontinuities across
      2,345 buffer boundaries.
- [x] Keyboard input works on the showcase titles through the scripted runs; gamepad
      mapping and merge behavior are covered by unit tests, with hardware validation
      still platform-specific.
- [x] Screenshots and save slots are covered by unit tests and the frontend smoke
      path; a manual click-through was not repeated in this session.
- [x] OpenGL path works (WGL and ANGLE/GLES Accurate parity, 0 different pixels);
      CPU fallback is covered by the existing tests and smoke path.
- [x] `docs/Compatibility.md` updated with the release matrix and §10.2 soak evidence.
- [x] `docs/KnownIssues.md` updated with the Space Ace long no-input wait and the
      remaining platform/gameplay limitations.

Suggested soak command:

```powershell
$env:FIVEDO_GAMES = "D:\Samples\3DO"
$env:FIVEDO_DISC  = "$env:FIVEDO_GAMES\Wolfenstein 3D (USA).chd"

dotnet run -c Release --project src/FiveDo.Cli -- run `
  "$env:FIVEDO_DISC" --bios bios/panafz1.bin --frames 36000 `
  --nvram artifacts/soak-w3d.ram --frame-out artifacts/soak-w3d.png --json
```

A known hang at a documented frame is acceptable for v1.0 if `KnownIssues.md` says so.
A new crash, silent corruption, or save-state regression is not.

---

## 5. Packaging and artifacts

- [x] Publish each supported RID with the release version (win-x64 validated).
- [x] Build portable archives with `packaging/package.ps1`.
- [x] Each archive contains the executable, `README.md`, `LICENSE`,
      `THIRD-PARTY-NOTICES.md`, `docs/Licensing.md`, `docs/ReleaseLegalReview.md`,
      `docs/OpenALSoft.md`, `docs/licenses/OpenAL-Soft-COPYING.txt`, the release
      notes, and `PACKAGE.txt`.
- [x] `PACKAGE.txt` records component, RID, version, commit, and UTC build time
      (repackaged after the release commit so the commit field is final).
- [x] No PDBs or copyrighted content in release archives.
- [x] Extract each archive to a clean folder and run the CLI/frontend smoke test.
- [x] SHA-256 checksums generated for every archive.
- [x] Archive sizes and checksums recorded in `SHA256SUMS.txt` next to the packages
      after the release commit and repackaging.

---

## 6. Release mechanics and sign-off

- [x] Bump `FiveDoVersion.Version` in `src/FiveDo.Core/FiveDoVersion.cs` to
      `1.0.0-rc.1` (and `Directory.Build.props`).
- [x] Move the `CHANGELOG.md` `[Unreleased]` section to `1.0.0-rc.1` and date it.
- [x] Write release notes: feature summary, supported platforms, supported titles,
      known issues, BIOS/disc requirements, license.
- [ ] Tag the release (`v1.0.0-rc.1`).
- [ ] Attach archives and checksums to the release (local artifacts; no remote release
      was published in this session).
- [x] Update README, `docs/Status.md`, `docs/Compatibility.md`,
      `docs/KnownIssues.md`, and the release docs.
- [x] Record the post-1.0 backlog in `docs/ImplementationPlan.md`.

| Sign-off | Name | Date |
|---|---|---|
| Release owner | | |
| Legal / distribution | | |
| Compatibility / QA | | |

---

## 7. Post-1.0 backlog (explicitly not release gates)

- Finish a full Road Rash race and script Alone's inventory/exploration.
- Add input/gameplay scripts for Gex, Need for Speed, SSF2 Turbo, Total Eclipse,
  Killing Time, Theme Park, and Return Fire.
- Continue CD/CLIO/MADAM/ARM timing triage with the Opera differential harness.
- Raspberry Pi 4/5 GLES 3.1, arm64 hardware, 100/150/200% DPI, and Linux/macOS
  launch validation if not claimed for v1.
- Tier B 1080p60 timing, 3D CEL profiling, hot-path optimization, and the
  same-machine relative benchmark gate on stable CI hardware.
- MSIX/dmg/AppImage installers, signing/notarization, and file associations.
  Frontend NativeAOT audio remains unvalidated; the CLI AOT path is validated.
- ViewModel tests and UI polish.
- Optional M12 managed ARM JIT (profile first; not needed for v1).

---

## 8. Definition of done

v1.0 is done when sections 1-6 are complete and signed off, the supported set has been
soaked, the compatibility matrix and known issues are honest, and the release artifacts
can be extracted and started on every claimed platform. Full compatibility is **not**
part of this definition.
