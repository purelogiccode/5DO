# What's new in 5DO 1.10.2

**Released:** 2026-11-15

5DO 1.10.2 is a **Windows audio and disc-container** patch update. It restores the
bundled OpenAL Soft runtime on Windows and adds DiscJuggler `.cdi` and Nero `.nrg` disc
images. The emulation core, deterministic hashes, save states, and the documented
compatibility model are unchanged.

## Highlights

### Windows OpenAL audio

The Windows bundles now ship **`OpenAL32.dll`** as well as `soft_oal.dll`. Silk.NET.OpenAL
loads `OpenAL32.dll`; the old publish step copied only the package name, so a deployed
copy could fall back to a system OpenAL build and fail with *The OpenAL context could not
be made current*. The bundled OpenAL Soft 1.23.1 runtime is now the one the loader opens,
and the bundle script rejects an archive missing it.

Audio startup failures are logged with the failing path and the ALC error instead of
being submitted as bug reports. If you are on 1.10.1 or older, copy `soft_oal.dll` to
`OpenAL32.dll` beside `FiveDo.exe` and `FiveDo.Cli.exe` and restart.

### DiscJuggler `.cdi` and Nero `.nrg`

- `.cdi` and `.nrg` images open through the CLI, **File > Open Disc**, the game library,
  and the file associations.
- The new readers handle cooked 2048-byte, 2324/2336-byte Mode 2, raw 2352-byte, and the
  2353/2368/2448/2449-byte sector variants.
- Malformed images fail with typed errors and are covered by the parser fuzz target.

## Verification

- Full suite **2,365/2,365** passes in Debug.
- Build, locked restore, `dotnet format --verify-no-changes`, and
  `jb inspectcode --severity=WARNING` are clean.
- The win-x64 CLI and frontend publishes contain `OpenAL32.dll` and `soft_oal.dll`; the
  published CLI reports OpenAL Soft `1.23.1` and lists the `OpenAL Soft on ...` endpoints.
- The published frontend writes `FRONTEND SMOKE OK 1.10.2`.
- `packaging/package-bundle.ps1` rejects a publish tree without `OpenAL32.dll`.

See [ReleaseNotes-1.10.2.md](ReleaseNotes-1.10.2.md),
[OpenALSoft.md](OpenALSoft.md), and [CHANGELOG.md](../CHANGELOG.md).
