# 5DO 1.10.2

**Released:** 2026-11-15

5DO 1.10.2 is a **Windows audio and disc-container** patch release. It fixes the missing
`OpenAL32.dll` in the Windows bundles and adds DiscJuggler `.cdi` and Nero `.nrg` disc
image support. Emulation accuracy, deterministic frame/state hashes, save states, and the
documented compatibility model are unchanged.

## Highlights

### Windows OpenAL audio is fixed

The 1.9.x and 1.10.0/1.10.1 Windows bundles shipped `soft_oal.dll` but not `OpenAL32.dll`.
Silk.NET.OpenAL loads `OpenAL32.dll`, so a deployed copy could silently fall back to a
system OpenAL build. On affected machines the reported symptom was:

```text
OpenAL audio output could not be created.
The OpenAL context could not be made current.
```

The publish target now copies **both** names after `dotnet publish`, so the bundled
OpenAL Soft 1.23.1 runtime is the one the loader opens. The bundle script and CI reject
an archive that is missing the loader-named runtime. Audio startup failures now record
the failing path and the ALC error in the log instead of being submitted as bug reports.

If you are still on 1.10.1 or older, copy `soft_oal.dll` to `OpenAL32.dll` beside
`FiveDo.exe` and `FiveDo.Cli.exe` and restart. The 1.10.2 bundles already contain both
files. See [OpenALSoft.md](https://github.com/purelogiccode/5DO/blob/v1.10.2/docs/OpenALSoft.md)
and [Troubleshooting.md](https://github.com/purelogiccode/5DO/blob/v1.10.2/docs/Troubleshooting.md).

### DiscJuggler `.cdi` and Nero `.nrg` images

- New standalone `CDISharp` and `NRGSharp` readers parse the trailing CDI descriptor and
  the NERO/NER5 chunk chain, including 2048, 2324/2336, 2352, 2353, 2368, 2448, and 2449
  byte sector layouts.
- `CdiDiscImage` and `NrgDiscImage` normalize both formats to the shared
  `ITrackedDiscSource` contract.
- `.cdi` and `.nrg` images open through the CLI, **File > Open Disc**, the game library,
  and the Windows/Linux file associations.
- Malformed images fail with typed errors and are covered by the parser fuzz target.

## Verification

- Full suite **2,365/2,365** passes in Debug.
- `dotnet build`, `dotnet restore --locked-mode`, `dotnet format --verify-no-changes`,
  and `jb inspectcode --severity=WARNING` are clean.
- The win-x64 CLI and frontend publishes contain `OpenAL32.dll` and `soft_oal.dll`; the
  published CLI loads OpenAL Soft 1.23.1 (`ALSOFT_LOGLEVEL=3`) and lists the
  `OpenAL Soft on ...` endpoints through `audio devices --json`.
- The published frontend writes `FRONTEND SMOKE OK 1.10.2`.
- `packaging/package-bundle.ps1` fails with a clear error when `OpenAL32.dll` is removed
  from both publish trees.

## Packages

Six framework-dependent bundles are produced for Windows x64/arm64, Linux x64/arm64, and
macOS x64/arm64 (macOS as a drag-to-Applications DMG), with `SHA256SUMS.txt`. The .NET 11
runtime is a host prerequisite and is not bundled.

See [CHANGELOG.md](https://github.com/purelogiccode/5DO/blob/v1.10.2/CHANGELOG.md) and
[What's new](https://github.com/purelogiccode/5DO/blob/v1.10.2/docs/WhatsNew.md).
