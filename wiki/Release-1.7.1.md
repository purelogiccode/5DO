# 5DO 1.7.1

**Released:** 2026-11-10

5DO 1.7.1 is a desktop playback and dependency update. It removes the startup stutter
and audio underruns caused by the host disc prefetcher shadowing CHDSharp's own
read-ahead, and refreshes the Avalonia, CLI, and test dependencies.

## Download

| System | Bundle |
|---|---|
| Windows x64 | `5do-1.7.1-win-x64.zip` |
| Windows arm64 | `5do-1.7.1-win-arm64.zip` |
| Linux x64 | `5do-1.7.1-linux-x64.tar.gz` |
| Linux arm64 | `5do-1.7.1-linux-arm64.tar.gz` |
| macOS x64 | `5do-1.7.1-osx-x64.dmg` |
| macOS arm64 | `5do-1.7.1-osx-arm64.dmg` |

The apps are framework-dependent. Install the **.NET 10 Runtime** from
<https://dotnet.microsoft.com/download/dotnet/10.0> first; the bundles do not include it.
See the [release page](https://github.com/purelogiccode/5DO/releases/tag/v1.7.1) for
`SHA256SUMS.txt`.

## Highlights

- **CHD images use CHDSharp read-ahead directly.** The extra host-sector prefetcher could
  hold the CHD reader lock while the emulation thread waited, causing multi-frame
  rendering stalls and audio underruns during the first seconds of a game.
- **Prefetch no longer outranks emulation.** The remaining sector worker runs at
  below-normal priority and yields between read-ahead sectors.
- **Dependencies.** Avalonia 12.1.6, System.CommandLine 3.0.0, and
  Microsoft.NET.Test.Sdk 18.11.1.

## Validation

- Full suite **2,125/2,125** passes in Debug.
- Build and `dotnet format --verify-no-changes` are clean; InspectCode reports 0 results.
- A 20-second desktop Road Rash run reports 0 audio underruns and no skipped frames.
- Six framework-dependent bundles are published from the `v1.7.1` tag.

## Known limits

- Playability follows the documented binary beyond-title rule, not a full playthrough.
- CD command cadence remains the residual differential on the 50-title panel.
- Windows x64 remains the validated GPU target; Linux GPU presentation under
  llvmpipe/SSH is preview/best-effort.
- Signing and notarization require a Windows certificate and an Apple developer account.
