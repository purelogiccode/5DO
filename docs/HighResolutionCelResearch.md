# High-Resolution CEL Rendering Research

**Status:** post-1.0 research plan (2026-10-30; release scope updated 2026-11-02;
**VID-19 true 2× prototype implemented 2026-11-16**)
**Release status:** M14 is **not a v1.0 release blocker**. v1.0 ships the existing
opt-in `--high-res` mode and 320x240 + output filtering as the default. This document
tracks future research.
**Question:** can 5DO render CELs internally at a higher resolution than the native
320x240 framebuffer, without breaking game compatibility or the deterministic software
renderer?

This document separates three ideas that are often conflated:

1. **Output upscaling** — render at 320x240 and scale/filter the finished frame to
   1080p/4K. This is already implemented and is the compatibility-safe path.
2. **The existing `--high-res` mode** — an opt-in presentation-only 640x480 frame. The
   guest machine stays native and the frame is a 2x nearest upscale for 320-wide modes.
   The old FreeDO `RESSCALE` four-plane layout now belongs only to the experimental
   `--true-2x` path.
3. **True internal high-resolution CEL rendering** — rasterize CELs and bitmap lines
   into a larger logical framebuffer so geometry, text, and 3D edges are genuinely
   higher resolution.

The current mode is (2), not (3). The research question is whether (3) is feasible
without a GPU CEL compositor or a large compatibility break.

---

## 1. What the current code does

| Area | Current behavior |
|---|---|
| Mode flag | `EmulatorTuning.HighResolution`, `CompatibilityState.HighResolution`, CLI `--high-res`, Settings > Emulation > Hi-Res. |
| Frame size | `EmulatorConstants.HighResolutionWidth = 640`, `HighResolutionHeight = 480`. |
| VRAM | Unchanged by the legacy mode. The four-plane layout (`HighResolutionVramPlanes = 4`; `4 * VramSize`) is used only by the experimental true 2× path. |
| CEL rasterizer | `CelRasterizer.ResScale` is 0 unless `TrueHighResolution`; the legacy mode leaves CEL coordinates and VRAM writes native. |
| Clipping | Native clip rectangle in the legacy mode; doubled only for true 2×. |
| VDLP | Legacy: samples the native line into the region's double-width line and emits it twice. True 2×: reads all four planes, interleaves even/odd X, and writes two output lines for each low-resolution line. |
| SPORT | Unchanged by the legacy mode. True 2× mirrors fill/copy pages into the extra planes. |
| Presentation | The frame source becomes 640x480; the CPU assembler, uploader, screenshots, frame hash, and save-state frame sections carry the larger frame. |

The legacy `--high-res` mode is now **presentation-only**. The guest machine is
byte-identical to native: CELs, VRAM, CPU bitmap reads, and SPORT all use the native
320x240 layout, and the 640x480 frame is an exact 2x nearest-neighbor enlargement for
320-wide modes. This removes the old FreeDO `RESSCALE` side effects (left-half 2D CEL
cropping, stale bitmap planes, and gameplay hangs) while keeping the option safe for
every title. Wider VDLP modes are resampled to the 640-pixel frame.

The opt-in **`--true-2x`** path is the only mode that still changes guest-visible CEL
fields. `WritePixel` selects planes 0/1 for even output lines and planes 2/3 for odd
output lines; `RenderHighResolutionLine` emits two independent output lines. That is
true internal 640x480 CEL sampling, but it is validated only on the two titles listed
in §1 (VID-19) and remains experimental.

Evidence: Lemmings' 640x480 title still matches the reference because the game itself
uses a 640-wide VDL mode. 20th Century Video Almanac, Road Rash, Dragon's Lair, and the
Alone in the Dark demo produce a 0-pixel difference between the legacy high-resolution
frame and a 2x nearest upscale of the native frame at the same PC.

### VID-19 prototype result (2026-11-16)

The bounded spike is implemented as the opt-in **`--true-2x`** machine option
(Settings > Emulation > **True 2x CEL fields**, per-game **TrueHighResolution**, and a
save-state field). The legacy `--high-res` presentation mode (2x nearest upscale) remains
the fallback.

**Design.** The four-plane layout is kept, but the plane pairs now carry independent
fields instead of copies. For a high-resolution pixel `(x, y)`:

- output-line parity selects planes 0/1 (even) or planes 2/3 (odd);
- `x & 1` selects the even/odd column within the pair;
- the 3DO word layout still stores the two 240-line source rows in opposite halfwords,
  so VDLP reads the independent fields without changes.

Arbitrary (3D) CEL geometry already evaluates in 640x480 space; the true path routes its
writes to the correct field plane. Packed and scaled 2D CELs stay on their original
240-line coordinates and expand each source pixel to a 2x2 block, which keeps sprites,
text, and HUDs aligned with the 640x480 frame. Bitmap lines and SPORT copies remain
line-doubled; a title that reads the framebuffer or copies pages after CEL rendering can
still need the native path.

**Visual comparison.** Jurassic Park Interactive's in-park Raptor Maze is the named
CEL-heavy 3D title; Family Feud is the 2D control. At frame 63,000 the *old* legacy
`--high-res` output (pre-2026-11-19, FreeDO `RESSCALE` layout) showed only the
ceiling/upper half of the corridor and a half-width HUD, while `--true-2x` shows the
complete corridor with the HUD at 2x scale. Compared with a 2x nearest-neighbor upscale
of the native 320x240 frame, the true 2x frame changes **38.41%** of pixels
(117,992 / 307,200) on the Raptor Maze, proving that the difference is not just output
scaling. Family Feud at frame 9,000 changes **0.00%** of pixels, showing that 2D content
is a clean 2x scale with no missing columns or palette artifacts. The hashes below are
historical captures of the old legacy path; today's legacy path produces the same
0.00% 2x upscale as the native reference.

| Title / frame | Native 320x240 hash | Old legacy `--high-res` | True 2× hash | Result |
|---|---:|---|---:|---|
| Jurassic Park, 63,000 | `0xD5D6A993` | `0x79149ECD` (upper half only) | `0xF67CF55B` | Full corridor; 38.41% differs from a 2x upscale |
| Jurassic Park, 65,000 | `0x133DCBA8` | - | `0x130EDE0E` direct / `0xF75B56C7` harness | Raptor encounter renders at 640x480 |
| Family Feud, 9,000 | `0x8893A949` | black frame (972-byte PNG) | `0x92434A2D` | 0.00% differs from a 2x upscale |

The exact hashes and captures are recorded in `docs/TestCorpus.md` 1.1cl and
`benchmarks/diff/vid19-true2x.json`.

**Performance.** On a 10,000-frame segment from the same saved state and script:

| Mode | Emulation time (run 1 / run 2) | Effective rate |
|---|---:|---:|
| Native 320x240 | 40.94 s / 45.17 s | 244.3 / 221.4 fps |
| Legacy `--high-res` | 40.22 s | 248.6 fps |
| True 2× | 54.91 s / 57.88 s | 182.1 / 172.8 fps |

Across the two runs, true 2× averaged about 56.4 s versus 43.1 s native, about 30% more
emulation time (177 fps versus 232 fps), and still runs more than 3x real time. The legacy
path is faster because it only samples each native line twice.

**Default stability.** The native 63,000 frame hash remains `0xD5D6A993`, matching the
ACC-04 reference checkpoint, and the full suite passes (2,367 tests). The option is
disabled by default; enabling it changes frame hashes and costs performance, as expected.

**Validation gate (2026-11-16; unlocked for all titles 2026-11-19).** Road Rash
(`d7672550`) showed stale/rainbow rows at the bottom of the presented frame during scene
transitions, even though its CPU frame was clean. The OpenGL 480-line line-parameter
upload bug responsible for the bottom rainbow was fixed on 2026-11-19. The true 2× option
is now available for every title so it can be re-tested; Jurassic Park Interactive
(`18036597`) and Family Feud (`55f8ccf1`) remain the visually validated reference titles.
Unvalidated titles may still misrender bitmap/SPORT or framebuffer-read content, so the
mode remains experimental.

**Remaining production work (VID-01).** The prototype does not make high-resolution the
default and does not solve bitmap/SPORT detail, framebuffer-read semantics, per-title
validation, or a full-collection sweep. Those remain the production research items.

**Legacy mode correction (2026-11-19).** The older `--high-res` implementation used the
FreeDO `RESSCALE` four-plane layout for every title. That cropped 2D CEL content to the
left half of the frame, left stale planes on bitmap/video lines, and could hang games
such as Road Rash when gameplay started. The legacy option is now presentation-only:
the guest machine stays native and the 640x480 frame is an exact 2x nearest upscale.
Only `--true-2x` keeps the guest-visible four-plane layout.

---

## 2. Why the native resolution feels low

The 3DO's default output is 320x240. On a 1080p or 4K display, the presenter expands
each source pixel by 6x or 12x. Nearest/integer scaling preserves the pixel grid;
sharp-bilinear, bicubic, CRT, xBR, HQx, MMPX, and FSR reduce the blockiness but cannot recover detail
that was never rendered.

The quality gap has three separate causes:

- **Geometry aliasing** — CEL quads and texture edges are rasterized at 320x240, so
  diagonal edges have large steps.
- **Texture sampling** — CEL textures are sampled at the source resolution; the
  rasterizer's fixed-point stepping and texel function determine how much detail
  survives.
- **Presentation** — the final upscale/filter. This is the easiest layer to improve and
  is already mature.

True internal resolution mainly addresses the first two. It is also the riskiest
because games can read the framebuffer, rely on exact VRAM addresses, or use the
framebuffer as a texture.

---

## 3. Options

### Option A — Keep 320x240 and improve output filters

**What:** current path; add/tune xBR, FSR, CRT, HQx, or a high-quality scaler.

**Pros:** zero game-compatibility risk; deterministic; fast on current hardware;
already validated.

**Cons:** cannot create true geometric detail; high-frequency text and 3D edges remain
source-resolution.

**Verdict:** keep as the default and fallback for every title.

### Option B — Complete the FreeDO `RESSCALE` path (true 640x480)

**What:** keep the four-plane layout but stop duplicating lines. Render 480 real lines,
scale CEL Y coordinates by 2, and make VDLP emit 480 independent lines. Horizontal
sampling is already 640-wide.

**Pros:** builds on the existing `--high-res` plumbing; no GPU dependency; the
four-plane layout already exists; Lemmings-style 640-wide modes are a natural fit.

**Cons:** CEL Y coordinates, clipping, modulo, SPORT copies, VDLP line timing, save
states, and frame hashes all change; 320x240 games must be upscaled or rendered with a
2x Y transform; games that read VRAM may see a different layout; the 3DO hardware's
native high-res behavior is not fully documented in the reference.

**Research question:** does a 480-line VDLP path produce a correct picture for a game
that uses a 640x480 mode, and what happens to 320x240 games?

### Option C — Supersample the software CEL rasterizer (2x/4x)

**What:** rasterize CELs into a larger host-side framebuffer (for example 1280x960 or
2560x1920), then downsample to 640x480 or present directly. The guest still sees the
normal VRAM, but the host keeps a high-resolution color/depth target.

**Pros:** can improve geometry and texture edges without changing the guest-visible
VRAM; no GPU CEL shader required; can be tested behind a flag.

**Cons:** memory and CPU cost grow quadratically; the CEL rasterizer writes through
`CelMemory`/`IRam` with fixed-point coordinates and modulo, so it needs a parallel
target and a new write path; games that read the framebuffer must still see the
native image; clipping and transparency must match; 4x is likely too slow for 60 Hz on
the current single-threaded core.

**Research question:** can a 2x supersampled CEL target be added without touching
guest-visible VRAM semantics, and what is the frame-time cost?

### Option D — GPU CEL compositing

**What:** decode CELs on the CPU, upload textures/geometry, and rasterize the CEL
pipeline on the GPU at output resolution. The software path remains the accuracy
oracle.

**Pros:** true high-resolution output independent of the 3DO's 1 MB VRAM; the GPU can
handle 4K; no CPU supersampling cost.

**Cons:** the CEL pipeline is not a simple textured quad: packed/literal/LR CELs,
PLUT, pixel processor modes, projector modes, clipping, transparency, matrix
transforms, and CCB state must be reproduced; GL 3.3/GLES 3.0 constraints; blending
and palette behavior must match exactly; large implementation and test surface.

**Verdict:** the long-term "proper" solution, but too large for the next pass. Treat it
as a separate experiment after the compatibility sweep.

### Option E — Per-game high-resolution defaults

**What:** enable `--high-res` only for titles that are visually validated, with a
per-game setting.

**Pros:** immediate quality win for compatible titles; low risk; uses existing code.

**Cons:** does not solve the general case; requires the full-collection visual sweep.

**Verdict:** ship this first, after the sweep identifies which titles benefit.

---

## 4. Recommended research sequence

### Phase 1 — Measure the current mode (1-2 days)

- Run the collection sweep at 3,000 frames in both modes and record the rendered/black
  result, frame time, screenshot, and BIOS for every title that renders. Use the BIOS
  that boots the title (see [`Compatibility.md`](Compatibility.md) 13.2); a black frame
  caused by the wrong BIOS is not a high-resolution result.
- Instrument the CEL trace for a sample of 2D and 3D titles: log `ResScale`, clip
  rectangle, `Modulo`, CEL function, and X/Y coordinates.
- Identify titles that use 640-wide VDL modes and titles that read the framebuffer.
- Compare low-res + xBR/FSR against `--high-res` for the same frame.

**Deliverable:** a table of titles where `--high-res` is correct, cropped, slower, or
broken.

### Phase 2 — True 640x480 prototype (3-5 days)

**Delivered 2026-11-16 as the opt-in `--true-2x` path.** The presentation-only
`--high-res` mode remains the fallback (it was switched from the old `RESSCALE` layout
on 2026-11-19).

- Added `EmulatorTuning.TrueHighResolution` (CLI `--true-2x`, Settings > Emulation,
  per-game profile, and save-state field) without changing the default tuning.
- Removed the plane 2/3 duplication for CEL writes: even output lines use planes 0/1 and
  odd output lines use planes 2/3, with VDLP reading them unchanged.
- Arbitrary CEL geometry evaluates at 640x480; packed/scaled 2D CELs expand to 2x2 blocks.
- Validated Jurassic Park Interactive's Raptor Maze (3D CELs) and Family Feud (2D CELs);
  native 320x240 and the presentation-only legacy path are byte-identical to their
  pre-VID-19 native references.
- Bitmap/SPORT content remains line-doubled, and framebuffer-read semantics are not yet
  changed; these are the remaining production items.

**Deliverable:** screenshots, hashes, and a frame-time comparison are recorded in §1 and
`TestCorpus.md` 1.1cl; `benchmarks/diff/vid19-true2x.json` reproduces the captures.

### Phase 3 — Supersampling prototype (3-5 days)

- Add a host-side 2x CEL color target behind a flag.
- Route `WritePixel`/`ReadPixel` through the target while preserving the guest-visible
  VRAM for games that read it.
- Downsample to 640x480 with a deterministic box filter; compare against the native
  and true-640 paths.
- Measure frame time and allocations on the development machine.

**Deliverable:** a 2x screenshot comparison and a performance budget.

### Phase 4 — GPU CEL feasibility (research only)

- Write a design note that maps each CEL pipeline stage to a GL 3.3/GLES 3.0 pass.
- Prototype one simple packed CEL as a textured quad with palette lookup and clipping.
- Compare against the software oracle for one scene.
- Do not ship until parity is proven; keep the software path as the default.

**Deliverable:** a go/no-go recommendation with a rough effort estimate.

---

## 5. Measurement template

Record the M14 experiments in this table (one row per title/mode):

| Title | Mode | Frames | Frame time | Screenshot | Result |
|---|---|---|---:|---|---|
| Lemmings | Native 320x240 | 3,000 | | | |
| Lemmings | `--high-res` | 3,000 | | | |
| Lemmings | True 640 prototype | 3,000 | | | |
| Road Rash | Native 320x240 | 3,000 | | | |
| Road Rash | `--high-res` | 3,000 | | | |
| Road Rash | True 640 prototype | 3,000 | | | |
| 2D title | 2x supersample | 3,000 | | | |
| Jurassic Park | Native 320x240 | 10,000 (state segment) | 43.1 s (232 fps, two-run average) | `artifacts/vid19/jp-boot-native` | Full corridor |
| Jurassic Park | `--true-2x` | 10,000 (state segment) | 56.4 s (177 fps, two-run average) | `artifacts/vid19/jp-boot-true2x` | Full corridor; 38.41% differs from a 2x upscale |
| Family Feud | Native 320x240 | 9,000 | - | `artifacts/vid19/ff-native` | 320x240 reference |
| Family Feud | `--true-2x` | 9,000 | - | `artifacts/vid19/ff-true2x` | Clean 2x scale; 0.00% differs |

Use the same frame number and camera/input state for each pair. A visual difference is
not automatically an improvement: check for crop, missing HUD/text, wrong palette, and
game-side framebuffer reads.

---

## 6. Acceptance criteria for the research

The research is complete when:

- the current `--high-res` behavior is documented per tested title,
- a true 640x480 prototype either works on a defined title set or is rejected with
  evidence,
- a 2x supersampling prototype either fits the 60 Hz budget or is rejected with
  measurements,
- the GPU CEL note has a clear parity/performance estimate,
- the default path remains the compatibility-safe 320x240 + output filter unless a
  title is explicitly validated,
- `docs/Video.md`, `docs/Compatibility.md`, `docs/KnownIssues.md`, and
  [`GameCompatibilityList.md`](GameCompatibilityList.md) record the decision.

---

## 7. Reference code

- `src/FiveDo.Madam/CelRasterizer.cs` — `ResScale`, `ReadPixel`/`WritePixel`, clipping.
- `src/FiveDo.Video/Vdlp.cs` — `RenderHighResolutionLine` and the four-plane layout.
- `src/FiveDo.Sport/Sport.cs` — high-resolution page mirroring.
- `src/FiveDo.Core/EmulatorConstants.cs` — frame/VRAM constants.
- `References/4DO-master/FreeDOCore/FreeDO/Madam.cpp` — original `RESSCALE` paths.
- `References/4DO-master/FreeDOCore/FreeDO/vdlp.cpp` — original high-res line output.
- `References/4DO-master/FreeDOCore/FreeDO/SPORT.cpp` — original page mirroring.
- `References/opera-libretro-master/libopera/opera_madam.c` and
  `opera_vdlp.c` — Opera's maintained behavior; compare before changing timing.

---

## 8. Non-goals for this research

- 4K internal CEL rendering as a default.
- Replacing the software CEL renderer.
- Changing the guest-visible 3DO VRAM layout for every title.
- Making high-res the default before the full-collection sweep and input checks pass.
- GPU CEL compositing in the same pass as the compatibility sweep.

The compatibility-safe default remains: render at 320x240, present with the OpenGL
two-stage pipeline, and use xBR/FSR/CRT for output quality.
