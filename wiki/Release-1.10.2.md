# 5DO 1.10.2

**Released:** 2026-11-15

5DO 1.10.2 is a **Windows audio and disc-container** patch release. It fixes the missing
`OpenAL32.dll` in the Windows bundles and adds DiscJuggler `.cdi` and Nero `.nrg` disc
images. Emulation accuracy, hashes, save states, and the documented compatibility model
are unchanged.

## Windows OpenAL audio

- The Windows bundles now ship **`OpenAL32.dll`** as well as `soft_oal.dll`.
- Silk.NET.OpenAL loads `OpenAL32.dll`; the old publish step copied only the package
  name, so a deployed copy could fall back to a system OpenAL build and fail with
  *The OpenAL context could not be made current*.
- The bundle script and CI reject an archive missing the loader-named runtime.
- If you are on 1.10.1 or older, copy `soft_oal.dll` to `OpenAL32.dll` beside
  `FiveDo.exe` and `FiveDo.Cli.exe` and restart.

## DiscJuggler `.cdi` and Nero `.nrg`

- `.cdi` and `.nrg` images open through the CLI, **File > Open Disc**, the game library,
  and the file associations.
- The new readers handle cooked 2048-byte, 2324/2336-byte Mode 2, raw 2352-byte, and the
  2353/2368/2448/2449-byte sector variants.
- Malformed images fail with typed errors and are covered by the parser fuzz target.

## Validation

- Full suite **2,365/2,365** passes in Debug.
- Build, locked restore, `dotnet format --verify-no-changes`, and
  `jb inspectcode --severity=WARNING` are clean.
- The win-x64 CLI and frontend publishes contain `OpenAL32.dll` and `soft_oal.dll`; the
  published CLI reports OpenAL Soft `1.23.1`, and the frontend writes
  `FRONTEND SMOKE OK 1.10.2`.
- `packaging/package-bundle.ps1` rejects a publish tree without `OpenAL32.dll`.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.10.2-win-x64.zip` |
| Windows arm64 | `5do-1.10.2-win-arm64.zip` |
| Linux x64 | `5do-1.10.2-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.10.2-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.10.2-osx-x64.dmg` |
| macOS arm64 | `5do-1.10.2-osx-arm64.dmg` |

Install the **.NET 11 Runtime** first; the bundles do not include it. See the
[release page](https://github.com/purelogiccode/5DO/releases/tag/v1.10.2) for
`SHA256SUMS.txt`.
