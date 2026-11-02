---
title: Game Compatibility
description: Filterable 3DO game compatibility results for 5DO, with per-disc automated sweep evidence and manual grades.
hide:
  - toc
---

# 3DO Game Compatibility

<div id="compat-app" class="compat-app">
  <div class="compat-loading" id="compat-loading" hidden>Loading compatibility data…</div>
  <noscript>
    <div class="compat-error">
      <h2>JavaScript is required for the filterable view</h2>
      <p>The full 594-row table is still available in
      <a href="GameCompatibilityResults.html">Game compatibility results</a>.</p>
    </div>
  </noscript>
</div>

## How to read this page

The browser groups regional and revision variants under one game title. Open a game to see
every tested disc, the BIOS used, the frame count, the recorded reason, and the final-frame
screenshot.

| Badge | Meaning |
|---|---|
| **Rendered** | The automated boot test completed the requested frames and the final frame was non-black. This is a loadability result, not a playability grade. |
| **No video** | The boot test completed but the final frame was black or nearly blank. It needs visual review. |
| **Not loadable** | The emulator failed, timed out, or did not complete the requested frames. |
| **Manual grade** | A separate 5DO grade from the curated compatibility list: `Good`, `Playable`, `Unplayable`, or `Not Loadable`. Most discs are not manually graded yet. |
| **BIOS override** | At least one tested disc needed a BIOS other than the default `panafz1.bin`. The details panel lists the exact files. |

The automated sweep is a screening tool. A **Rendered** result means the game reached a
non-black frame at the tested depth; it does not prove that input, audio, or the full game
works. See [Compatibility notes](Compatibility.md) and
[Compatibility sweep](CompatibilitySweep.md) for the methodology.

## Data and contributions

- [Raw per-disc results](GameCompatibilityResults.md) — the full table with frame hashes and evidence paths.
- [4DO reference list](GameCompatibilityList.md) — the 241-title reference matrix and manual 5DO grades.
- [Compatibility sweep procedure](CompatibilitySweep.md) — how the automated pass is run.
- Found a regression or a game that is missing? [Report a compatibility issue](https://github.com/purelogiccode/5DO/issues/new?template=compatibility_report.yml).

Screenshots are final-frame captures from the local sweep and are included only as
compatibility evidence.

The data on this page is generated from the committed Markdown tables by
`tools/generate-compat-site.py`. If you update the sweep results, regenerate the JSON before
building the site:

```powershell
python tools/generate-compat-site.py
```
