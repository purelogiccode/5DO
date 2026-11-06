# Compatibility

The first full-collection loadability sweep covered **594 disc images**. The current
library catalogues **653 games across 1,366 images**, including three audio-only CHDs.

| Result | First pass | After triage |
|---|---:|---:|
| Loadable / rendered | 576 | **593** |
| Loadable / no video | 13 | **1** |
| Not loadable | 5 | **0** |

The remaining known no-video case is `Crash 'n Burn` (Japan). The four Arcade CD-ROM
titles require `panafz10`-class BIOS behavior. Several blank/black titles render by
6,000–9,000 frames or with the correct BIOS/region.

This is a **documented-compatibility** project, not a claim of full 3DO library
compatibility.

## Per-title evidence

- [Compatibility notes](https://purelogiccode.github.io/5DO/Compatibility.html)
- [Game compatibility list](https://purelogiccode.github.io/5DO/GameCompatibilityList.html)
- [Game compatibility results](https://purelogiccode.github.io/5DO/GameCompatibilityResults.html)
- [Known issues](https://purelogiccode.github.io/5DO/KnownIssues.html)

## Reporting a result

Use the compatibility issue form with:

- game title and region;
- disc format and `info --json` game ID;
- BIOS name and MD5 (never upload the ROM);
- 5DO version, renderer, region, and options;
- frame count and result;
- frame hash, screenshot, state roundtrip, or diagnostics bundle.

See [Contributing](Contributing).
