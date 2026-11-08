# Plan: GPU HQx 2x/3x/4x

## Goal

Move HQx 2x/3x/4x from the CPU to the GPU in all three accelerated backends
(OpenGL/GLES, Direct3D 11, Vulkan). The CPU `HqxFilter` remains the reference
oracle for tests. The existing sharp-bilinear finishing pass is unchanged.

Current bottleneck: all three renderers assembled the frame and ran
`HqxFilter.Expand` on the CPU, then uploaded the expanded frame. `docs/Status.md`
recorded 4K HQx as CPU-bound (6.82/10.93 ms present). The GPU path now lands in
the same class as the existing xBR/FSR passes.

## Design

### Rule table

The HQx switches are regular enough to encode as data:

- 256 patterns.
- Only four color-difference conditions occur:
  `Diff(w2,w4)`, `Diff(w2,w6)`, `Diff(w4,w8)`, `Diff(w6,w8)`.
- Each output subpixel has at most one condition.
- Each output is a blend of the center and at most two neighbours.
- Unique rule counts: 41 (2x), 29 (3x), 77 (4x).

Packed rule (uint32):

```text
bits 0-3    tap0 weight (0-15)
bits 4-7    tap0 index  (0-8, 4 = center)
bits 8-11   tap1 weight
bits 12-15  tap1 index
bits 16-19  tap2 weight
bits 20-23  tap2 index
bits 24-26  shift (0-4)
bits 27-29  condition (0-3, 7 = none)
```

Table layout, uploaded as an `RG32UI` texture:

```text
row    = scale row offset + output subpixel
column = pattern (0-255)
R      = rule when condition is false
G      = rule when condition is true
```

Row offsets: 2x = 0, 3x = 4, 4x = 13. Texture size 256 x 32, 64 KB.

### GPU pass

One fragment per HQx output pixel:

1. Fetch the 3x3 palette-expanded source pixels (`texelFetch`, clamped).
2. Compute the 8-bit HQx pattern with the same YUV thresholds as
   `HqxFilter.ComputePattern`.
3. Evaluate the four condition bits.
4. Fetch the rule pair for `(pattern, subpixel)`.
5. Select base/alternate rule.
6. Evaluate `(c0*w0 + c1*w1 + c2*w2) >> shift` with integer math, matching
   `Interpolate2/3`.
7. Write RGBA8.

The HQx target is `source * scale`, then the existing sharp-bilinear scale pass
consumes it exactly like the xBR output.

## Steps

### 1. Rule table

- [x] Add `tools/generate_hqx_rules.py` that parses the 4DO
  `hq2x.cpp`/`hq3x.cpp`/`hq4x.cpp` sources and emits
  `src/FiveDo.Video/HqxRules.cs`.
- [x] Emit `HqxRuleTable` with:
  - `Rules` (combined 256 x 32 table in texture order).
  - `RowOffset(scale)`.
  - `PatternCount`, `RowCount`.
- [x] Add `InternalsVisibleTo` in `FiveDo.Video.csproj` for the three renderer
  assemblies and `FiveDo.Tests`.
- [x] Add `HqxRuleTableTests`:
  - weights sum to `1 << shift`;
  - indices/conditions valid;
  - table length correct;
  - table-driven evaluation matches `HqxFilter.Expand` on the reference image.

### 2. OpenGL/GLES

- [x] Add `Shaders.FragmentHqx(bool es)` (GLSL 330 / ES 300).
- [x] Add `_hqxProgram`, `_hqxFbo`, `_hqxRuleTexture`, uniforms.
- [x] Create the HQx target as an FBO attachment (`RGBA8`).
- [x] Create/upload the 256 x 32 `RG32UI` rule texture once.
- [x] Replace the CPU `FrameAssembler` + `HqxFilter.Expand` block with the GPU
  pass.
- [x] Remove `_hqxProcessor`, `EnsureHqxSource`, CPU frames.
- [x] Keep the `MaxTextureSize` fallback to sharp-bilinear.
- [x] Add shader-source tests.

### 3. Direct3D 11

- [x] Add `Hqx.frag` to `Direct3D11Shaders.All`.
- [x] Add `_hqxShader`, rule texture/view, HQx RTV.
- [x] Change the HQx texture to `RenderTarget | ShaderResource`.
- [x] Add `RecordHqxPass`; remove `PrepareHqx`/CPU frames.
- [x] Update `Direct3D11ShaderTests` expected shader list.
- [x] Add an opt-in HQx CPU-parity test.

### 4. Vulkan

- [x] Add `Hqx.frag`.
- [x] Add `_hqxLayout` (two samplers), `_hqxPipelineLayout`, `_hqxPipeline`,
  `_hqxSet`.
- [x] Add the 256 x 32 `R32G32Uint` rule image and one-time upload.
- [x] Change `EnsureHqxTarget` to a color-attachment image + framebuffer.
- [x] Add `RecordHqxPass`; remove `PrepareHqx`, staging upload, CPU frames.
- [x] Update `RecreateExpandedTarget` descriptors.
- [x] Add an opt-in HQx CPU-parity test.

### 5. Validation and docs

- [x] `dotnet build 5DO.sln -c Debug`
- [x] `dotnet test 5DO.sln -c Debug --no-build --no-restore`
- [x] `dotnet format 5DO.sln --verify-no-changes --no-restore`
- [x] `jb inspectcode 5DO.sln --severity=WARNING --no-updates`
- [x] Run opt-in D3D/Vulkan tests.
- [x] Measure 4K `hqx`, `hqx3x`, `hqx4x` with the GL/VK/D3D probes.
- [x] Update `docs/Compatibility.md` §6, `docs/Status.md`,
  `docs/Architecture.md`, `docs/DeveloperGuide.md`, `docs/Direct3D.md`,
  `docs/Vulkan.md`, `docs/Configuration.md`, `docs/FAQ.md`,
  `docs/Glossary.md`, `docs/ImplementationPlan.md`.

## Risks

| Risk | Mitigation |
|---|---|
| fp32 vs double YUV | At most 1 LSB for ~0.07% of colours; visually indistinguishable. Document; allow a tiny tolerance in GPU parity tests. |
| GLES `RG32UI` | Core in ES 3.0; verified on ANGLE/llvmpipe. Fall back to sharp-bilinear if creation fails. |
| 4x at 1024-wide | 4096x2048 target; keep the existing `MaxTextureSize` fallback. |
| CPU fallback | HQx was never applied there; no regression. Document GPU-only HQx. |

## Result

Implemented on 2026-11-07.

- `tools/generate_hqx_rules.py` generates `src/FiveDo.Video/HqxRules.cs`
  (16,384 uints, 256 x 32 `RG32UI`/`R32G32_UInt` table).
- OpenGL, Direct3D 11, and Vulkan all render HQx 2x/3x/4x as GPU passes and feed the
  result into the existing sharp-bilinear finish.
- The CPU `HqxFilter` now uses the same rule table; the ~14k-line
  `HqxPatterns*.cs` switch files and the obsolete `tools/generate_hqx34.py` were
  deleted. The reference hashes still pass.
- `HqxRuleTableTests` validates the rule encoding and compares the table-driven
  evaluation with `HqxFilter.Expand`.
- Opt-in D3D/Vulkan `Hqx_MatchesCpuExpansion` tests pass for 2x/3x/4x with 0
  differing pixels.
- The D3D and Vulkan synthetic-probe tests (multi-present, phase 0..3) also match
  the CPU expansion with 0 differing pixels.
- WGL, ANGLE/GLES, Vulkan, and D3D 640x480 HQx captures are byte-identical to each
  other and to the CPU `HqxFilter` expansion (SHA-256
  `865222bd6280766bee7bf4b9e296857be479f6ee68c9489ab910c81d0be71bb5`).
- The shaders use explicit `trunc` for both the YUV integer conversion and the
  `fetchColor` byte conversion. NVIDIA's desktop GLSL compiler rounded `int(float)`
  in the first GL/VK version, and ANGLE's GLES float-to-uint conversion rounded
  `k + 0.5`; both produced a different HQx pattern from the CPU/D3D path.
- Full suite: 1,939 tests passed; `dotnet format` clean; `jb inspectcode` reported
  0 issues.
- 4K probe results (RTX 4070):
  - WGL: present 0.229/0.322/0.250 ms, GPU 0.109/0.132/0.163 ms.
  - Vulkan: present 0.264/0.287/0.344 ms, GPU 0.099/0.119/0.150 ms.
  - Direct3D 11: present 0.183/0.150/0.140 ms, GPU 0.086/0.108/0.138 ms.
  - 0 managed bytes/frame and `filterFallback: false` in every case.
- A pre-existing stale Vulkan `Palette.frag` background path was corrected to 24-bit
  RGB, matching the CPU/OpenGL/Direct3D shaders.
- The D3D/Vulkan split-frame tests were updated from the old 5-bit background word to
  the current 24-bit background register.

## Optional follow-up (done)

The CPU `HqxFilter.Expand` now uses the same `HqxRuleTable` as the GPU passes. The
generated `HqxPatterns*.cs` switches and the obsolete `tools/generate_hqx34.py` were
deleted. `tools/generate_hqx_rules.py` parses the 4DO C++ sources directly and
regenerates an identical `HqxRules.cs` table. The existing reference hashes in
`HqxFilterTests` validate the table-driven CPU implementation.
