# High-Resolution CEL Rendering Research

**Status:** research plan (2026-10-30)
**Question:** can 5DO render CELs internally at a higher resolution than the native
320x240 framebuffer, without breaking game compatibility or the deterministic software
renderer?

This document separates three ideas that are often conflated:

1. **Output upscaling** — render at 320x240 and scale/filter the finished frame to
   1080p/4K. This is already implemented and is the compatibility-safe path.
2. **The existing `--high-res` mode** — FreeDO's `RESSCALE` path, which stores a
   640x480 frame in four 1 MB VRAM planes and line-doubles the 240-line VDL output.
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
| VRAM | Four 1 MB planes in the reserved RAM mirrors (`HighResolutionVramPlanes = 4`). Plane 0/1 hold even/odd X columns; planes 2/3 mirror the second interlace field. |
| CEL rasterizer | `CelRasterizer.ResScale` is 0 or 1. `ReadPixel`/`WritePixel` shift X by `ResScale` and select the plane with `x & 1`; Y remains a 240-line coordinate. |
| Clipping | `TestClip` doubles the clip rectangle with `(ClipX + 1) << ResScale`. |
| VDLP | `Vdlp.RenderHighResolutionLine` reads all four planes, interleaves even/odd X, and writes two output lines for each low-resolution line. |
| SPORT | `Sport` mirrors fill/copy pages into the extra planes. |
| Presentation | The frame source becomes 640x480; the CPU assembler, uploader, screenshots, frame hash, and save-state frame sections carry the larger frame. |

The important limitation is visible in `WritePixel`: the code writes the same texel to
planes 2/3 as well as planes 0/1, and `RenderHighResolutionLine` emits two identical
output lines. That is line doubling, not two independently rendered 480-line fields.
It improves horizontal sampling for CEL X coordinates and matches the FreeDO
`RESSCALE` behavior, but it cannot add vertical geometric detail.

Evidence: Lemmings' 640x480 title matches the reference because the game itself uses a
640-wide VDL mode; it is not proof that 3D CELs are rendered at true 640x480.

---

## 2. Why the native resolution feels low

The 3DO's default output is 320x240. On a 1080p or 4K display, the presenter expands
each source pixel by 6x or 12x. Nearest/integer scaling preserves the pixel grid;
sharp-bilinear, CRT, xBR, HQx, and FSR reduce the blockiness but cannot recover detail
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
  result, frame time, and screenshot for every title that renders.
- Instrument the CEL trace for a sample of 2D and 3D titles: log `ResScale`, clip
  rectangle, `Modulo`, CEL function, and X/Y coordinates.
- Identify titles that use 640-wide VDL modes and titles that read the framebuffer.
- Compare low-res + xBR/FSR against `--high-res` for the same frame.

**Deliverable:** a table of titles where `--high-res` is correct, cropped, slower, or
broken.

### Phase 2 — True 640x480 prototype (3-5 days)

- Add a separate tuning flag (for example `HighResolutionMode.True640`) so the existing
  `RESSCALE` behavior remains available as the reference.
- Remove the plane 2/3 duplication and emit independent 480-line output.
- Scale CEL Y coordinates, clip Y, and VDLP line timing by 2.
- Keep guest VRAM semantics unchanged where possible; if a game reads the framebuffer,
  fall back to the native path for that title.
- Validate on Lemmings (640x480 title), a 2D title, and a 3D title (Road Rash or Total
  Eclipse).

**Deliverable:** screenshots and frame hashes for the prototype; a compatibility list
of titles that work.

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
