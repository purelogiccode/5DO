# What's new in 5DO 1.16.1

**Released:** 2026-11-21

5DO 1.16.1 is an optical verification and CD-DA diagnostics patch release. It adds
`disc audio` for direct Red Book extraction, makes `disc verify` show progress and honor
mixed-mode track totals, and records the optical collection/BIOS sweep. Save states and
disc formats are unchanged from 1.16.0.

## Pick the right file

| Your system | Download |
|---|---|
| Windows 10/11 (x64) | `5do-1.16.1-win-x64.zip` |
| Windows on ARM | `5do-1.16.1-win-arm64.zip` |
| macOS on Apple Silicon | `5do-1.16.1-osx-arm64.dmg` |
| Debian, Ubuntu, Mint | `5do_1.16.1_amd64.deb` (arm64: `_arm64`) |
| Fedora, RHEL, openSUSE | `5do-1.16.1-1.linux.x86_64.rpm` (arm64: `aarch64`) |
| Any Linux, no install | `5do-1.16.1-linux-x86_64.AppImage` (arm64: `aarch64`) |
| Any Linux, sandboxed | `5do-1.16.1-linux-x86_64.flatpak` (arm64: `aarch64`) |
| Any Linux, portable | `5do-1.16.1-linux-x64.tar.gz` (arm64: `linux-arm64`) |

Extract the archive before running it: the apphosts load `libSkiaSharp`,
`libHarfBuzzSharp`, OpenAL, and (on Windows) ANGLE from the same folder.

The `.asc` files are signatures, the `.zsync` files feed `AppImageUpdate`, and
`SHA256SUMS.txt` lists hashes; you only need them to verify a download.
`5do-archive-keyring.gpg` is the key those signatures use.

## Direct CD-DA test

```powershell
5do disc tracks E:
5do disc audio E: --track 1 --out track1.wav
5do audio analyze track1.wav --json
```

`disc audio` writes a 44.1 kHz, 16-bit stereo WAV through the same
`ITrackedDiscSource.ReadTrackSector` path the emulator uses. It works for CUE/CHD audio
tracks and a host optical drive. Without `--track`, the first audio track is selected; a
data-only disc explains that it has no CD-DA tracks. This bypasses the FZ-10 BIOS
player's inert transport (SYS-01).

## `disc verify`

- Non-JSON runs print the opening path, total sector count, and periodic
  track/sector/percentage/MiB progress to stderr; `--json` stays one clean document.
- The verifier compares against `ITrackedDiscSource.TotalSectorCount`, so mixed-mode
  audio after a data volume is not falsely reported short.

## Evidence

- 1,366 CHDs / 496 unique game IDs opened and track-probed; 496 unique-title
  representatives, including all three audio-only discs, fully sector-verified with 0
  errors.
- 26-case BIOS matrix at 3,000 frames: FZ-1, FZ-10, GoldStar, Sanyo, FZ-10J+Kanji,
  FZ-10E Anvil, and patched FZ-10 + `3do_arcade_saot`.
- Issue #8 reporter's Way of the Warrior CD-R: 157,996/157,996 sectors, hash
  `0x84b2bef01d157a9f`, matching the CHD. The burn has no CD-DA track; the Red Book
  audio CD is still the hardware CD-DA test.
- A&M Music Sampler image track: non-silent 85.97 s WAV, peak 29920, RMS 4248.

## Verification

- 2,579/2,579 tests, build 0 warnings/0 errors, `dotnet format`, and `jb inspectcode`
  clean.
- Windows x64 bundle `selftest` 15/15 and `FRONTEND SMOKE OK 1.16.1`.
- Linux AppImage/tarball smoke, detached `.asc`, and `AppImageUpdate` resolution.
- Published at <https://github.com/purelogiccode/5DO/releases/tag/v1.16.1>; the five
  bundles match `SHA256SUMS.txt`.

## Known issues

- The FZ-10 audio-only BIOS player transport remains at STOP (SYS-01, matching 4DO).
  Use `disc audio` to verify the drive's CD-DA path.
- A data-only 3DO burn has no CD-DA tracks; use an audio CD or mixed-mode burn.
- `--opera-soft-reset` remains opt-in/experimental; `--opera-dipir` still blocks the
  reporter's boot and needs a CSTAT/DIPIR transcript.
- Physical in-game CD-DA still needs a real mixed-mode audio disc; the image and raw
  drive read paths are verified.
