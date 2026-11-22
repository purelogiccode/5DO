# 5DO 1.16.1

**Released:** 2026-11-21

5DO 1.16.1 is an optical verification and CD-DA diagnostics patch release. It adds a
direct way to extract Red Book audio from an image or a host optical drive, makes
`disc verify` show progress and handle mixed-mode track totals, and records the full
optical collection/BIOS sweep. Save states and disc formats are unchanged from 1.16.0.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.16.1-win-x64.zip` |
| Windows on ARM | `5do-1.16.1-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.16.1-osx-arm64.dmg` |
| Debian, Ubuntu, Linux Mint (x64) | `5do_1.16.1_amd64.deb` |
| Debian, Ubuntu, Linux Mint (arm64) | `5do_1.16.1_arm64.deb` |
| Fedora, RHEL, openSUSE (x64) | `5do-1.16.1-1.linux.x86_64.rpm` |
| Fedora, RHEL, openSUSE (arm64) | `5do-1.16.1-1.linux.aarch64.rpm` |
| Any Linux, no install (x64) | `5do-1.16.1-linux-x86_64.AppImage` |
| Any Linux, no install (arm64) | `5do-1.16.1-linux-aarch64.AppImage` |
| Any Linux, sandboxed (x64) | `5do-1.16.1-linux-x86_64.flatpak` |
| Any Linux, sandboxed (arm64) | `5do-1.16.1-linux-aarch64.flatpak` |
| Any Linux, portable folder | `5do-1.16.1-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Extract the archive before running it: the apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder.

The `.asc` files are signatures, the `.zsync` files feed `AppImageUpdate`, and
`SHA256SUMS.txt` lists hashes; you only need them to verify a download.
`5do-archive-keyring.gpg` is the key those signatures use.

## New CD-DA diagnostic

```powershell
5do disc tracks E:
5do disc audio E: --track 1 --out track1.wav
5do audio analyze track1.wav --json
```

`disc audio` writes a 44.1 kHz, 16-bit stereo WAV through
`ITrackedDiscSource.ReadTrackSector`, so it tests the same CD-DA path used by the
emulator without depending on the FZ-10 BIOS player transport (SYS-01). Without
`--track`, it selects the first audio track. A data-only disc reports that it has no
CD-DA tracks instead of failing silently.

This is also useful for a burned game disc: if `disc tracks E:` shows only `type=data`,
the burn contains no Red Book audio, even if the game has data-streamed music.

## `disc verify` progress and mixed-mode totals

- Non-JSON verification now prints the opening path, total sectors, and periodic
  track/sector/percentage/MiB progress to stderr. A slow physical drive no longer looks
  frozen. `--json` remains a single clean document.
- The verifier uses `ITrackedDiscSource.TotalSectorCount`, so audio tracks after a data
  volume do not make a healthy mixed-mode disc report short.

## Optical collection and hardware evidence

- **1,366 CHDs / 496 unique game IDs** opened and track-probed with 0 errors.
- **496/496 unique-title representatives, including all three audio-only discs, fully
  sector-verified** with 0 read errors.
- **26-case BIOS matrix** at 3,000 frames: FZ-1, FZ-10, GoldStar, Sanyo, FZ-10J+Kanji,
  FZ-10E Anvil, and patched FZ-10 + `3do_arcade_saot`.
- The issue #8 reporter's Way of the Warrior CD-R read **157,996/157,996 sectors**, hash
  `0x84b2bef01d157a9f`, matching the CHD. That burn has only a data track; the test
  correctly reports no CD-DA.
- The same reporter's **Red Book audio CD** is the hardware CD-DA pass: `disc tracks E:`
  listed six audio tracks and `disc audio E:` extracted track 1 as a 164.28 s WAV with
  peak 31659 and RMS 7248.66 (0 clipped).
- The A&M Music Sampler image track produced a non-silent 85.97 s WAV (peak 29920,
  RMS 4248) with automatic first-audio selection.
- All **672** Redump 3DO cue sheets are single-track `MODE1/2352`; no known retail 3DO
  game has a CD-DA track. Retail 3DO music is data-streamed, so a music CD is the correct
  CD-DA test disc.

## Verification

- 2,579/2,579 tests, `dotnet build` 0 warnings/0 errors,
  `dotnet format --verify-no-changes`, and `jb inspectcode` at warning severity.
- CI covers Windows, Linux, and macOS, the NativeAOT apphosts, trimmed publishes, Linux
  packages, and all five bundles.
- Published at <https://github.com/purelogiccode/5DO/releases/tag/v1.16.1>; release run
  <https://github.com/purelogiccode/5DO/actions/runs/44962202917> passed all five bundle
  jobs, both AppImage/Flatpak package jobs, signing, and the publish job. The five
  downloaded bundles match `SHA256SUMS.txt`.
- Windows x64 bundle: 1.16.1, `selftest` 15/15, `FRONTEND SMOKE OK 1.16.1`, and
  `disc audio` extracts a non-silent A&M track 2 WAV (112.33 s, peak 32312, RMS 4777.15).
- Linux AppImage: FUSE reports 1.16.1, writes `FRONTEND SMOKE OK 1.16.1`, the detached
  `.asc` verifies, and the embedded `.upd_info`/`.zsync` target the versioned image.

## Known issues

- The FZ-10 audio-only BIOS player still lists the TOC but leaves the transport at
  **STOP** (SYS-01, matching 4DO). Use `disc audio` to verify the drive's CD-DA path;
  that path is verified on real hardware.
- A data-only 3DO burn has no CD-DA tracks; use an audio CD. All 672 Redump 3DO cue
  sheets are single-track `MODE1/2352`, so no known retail 3DO game has a CD-DA track.
- `--opera-soft-reset` remains opt-in/experimental. The default path boots the tested
  CD-Rs; `--opera-dipir` still blocks the reporter's boot and needs a CSTAT/DIPIR
  transcript.
