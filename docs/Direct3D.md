# Optional Direct3D 11 Backend

`FiveDo.Video.Direct3D` is an optional Windows presentation backend. **OpenGL/GLES
remains the default and required desktop presenter on every platform.** Direct3D 11 is
opt-in and is only offered on Windows.

## Pipeline

The backend mirrors the OpenGL and Vulkan two-stage presenters:

```text
VdlFrame
  -> R16_UINT pixel texture / R8G8B8A8 CLUT / R32G32_UINT line params
  -> palette pass at native resolution (HLSL)
  -> optional xBR 4x pass or FSR EASU pass
  -> scale/filter pass (or FSR RCAS) at output resolution
  -> B8G8R8A8 output target
  -> staging-texture readback for screenshots/probes
  -> DXGI flip-model swapchain for the desktop path
```

HLSL sources live in `Shaders.cs` and are compiled at runtime with the Windows
D3DCompiler (`d3dcompiler_47.dll`). No shader compiler or Windows SDK is required to
build the project. **Screenshot** uses the same staging readback when the Direct3D
presenter is active, so captures include the selected filter; the optional native CPU
screenshot remains unfiltered.

## Selection

| Surface | How to select it |
|---|---|
| Desktop | `--gl-backend d3d11` (aliases `dx11`, `directx`, `d3d`) |
| Settings > Video > Graphics API | **DirectX 11 (Windows)** |
| Headless probe | `--d3d-probe <json>` |
| Native swapchain probe | `--d3d-window-probe <json>` |

The persisted setting is normalized to OpenGL when the data directory is opened on a
non-Windows platform. If the Direct3D device or swapchain cannot be created, the shell
tries OpenGL and then the CPU presenter. OpenGL is never replaced as the fresh-install
default.

## Filters

All filters in the shared `VideoFilter` enum are supported:

- `Accurate`/`Nearest`: integer `Load` sampling, pixel-identical to the CPU oracle when
  the output is 1:1.
- `SharpBilinear`: the same edge-aware sharp-bilinear sampling as OpenGL/Vulkan.
- `Bicubic`: Catmull-Rom 4x4 sampling at output resolution.
- `Crt`: scanlines, aperture mask, barrel warp, and optional bloom.
- `Xbr`: GPU 4x xBR-lv2 pass, then sharp-bilinear.
- `Fsr`: FSR 1.0 EASU into a viewport-sized target, then RCAS into the output.
- `Hqx`/`Hqx3x`/`Hqx4x`: GPU rule-table pass over the palette texture, then
  sharp-bilinear. The 256x32 `R32G32_UInt` rule table is uploaded once at startup.
- `Mmpx`: GPU MMPX 2x rule pass over the palette texture, then sharp-bilinear.

If a GPU filter cannot allocate its intermediate target, the renderer reports
`FilterFallback` and falls back to sharp-bilinear.

## Probes

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --d3d-probe artifacts/d3d-probe/4k-sharp.json `
  --d3d-probe-size 3840x2160 --d3d-probe-frames 30 --d3d-probe-warmup 10 `
  --d3d-probe-filter sharp-bilinear
```

`--d3d-probe` renders offscreen and is the recommended validation path. The
`--d3d-window-probe` variant creates a Win32 window and presents through the DXGI
flip-model swapchain, including vsync reporting. Both use the same JSON report shape as
the OpenGL/Vulkan probes:

```powershell
dotnet run -c Release --project src/FiveDo.Frontend.Avalonia -- `
  --d3d-window-probe artifacts/d3d-probe/window.json `
  --d3d-window-probe-size 1280x720 --d3d-window-probe-frames 60
```

The probe options accept `--d3d-probe-*` and `--d3d-window-probe-*` for size, frames,
warm-up, filter, capture, and timeout.

## Evidence

On the development machine (Windows 11, Ryzen 9 7900, RTX 4070):

| Test | Result |
|---|---|
| 320x240 Accurate parity | `Matches: true`, 0 different pixels, CPU/GPU hash `0xB74C8AC5`, no vertical flip |
| 4K sharp-bilinear | present p99 0.23 ms, GPU p99 0.07 ms, 0 managed allocations/frame |
| Window probe 640x480 | `offscreen: false`, `vsyncBackend: "fifo"`, `vsyncActive: true` |
| Filters | Accurate, sharp-bilinear, CRT, xBR, FSR, HQx 2x/3x/4x render without fallback |
| 4K HQx 2x/3x/4x | present 0.183/0.150/0.140 ms, GPU 0.086/0.108/0.138 ms, 0 managed allocations/frame |
| Shaders | All eight HLSL modules compile for `vs_5_0`/`ps_5_0` |

The test evidence is reproducible with `FIVEDO_D3D_TESTS=1`; the shader compile test is
not opt-in on Windows.

## Tests

| Test | Coverage |
|---|---|
| `Direct3D11ShaderTests` | Every HLSL module compiles for shader model 5.0. |
| `Direct3D11RendererTests` (opt-in `FIVEDO_D3D_TESTS=1`) | CPU parity, source/output size changes, letterboxing, xBR/FSR/HQx/MMPX orientation, HQx 2x/3x/4x and MMPX CPU-expansion parity, and the multi-frame synthetic probe frame. |
| `D3dProbeIntegrationTests` (opt-in `FIVEDO_D3D_TESTS=1`) | Runs the built probe and checks the parity report. |
| `D3dProbeOptionsTests` | Probe option defaults, aliases, validation, and shared option names. |

## Decision: Direct3D 11, not 12

D3D12 is a lower-level API, not a higher-quality renderer. The presenter submits only a
few full-screen passes per frame; D3D12's explicit command lists, descriptor heaps, root
signatures, pipeline state objects, resource barriers, and memory residency add
substantial code and failure modes without improving image quality or the measured frame
time. D3D11 is fully supported, is the API ANGLE uses underneath, and has simpler
debug/WARP/device-loss handling. A D3D12 backend can be added later behind the same
`IVideoRenderer` contract if GPU CEL compositing needs compute and explicit scheduling.

## Limitations

- The desktop swapchain is Win32-only. Non-Windows builds keep the project for
  cross-compilation and headless validation, but the desktop option normalizes to OpenGL.
- The backend requires Direct3D 11 feature level 11_0 and the Windows D3DCompiler
  runtime (present on supported Windows versions).
- GPU timing is read one frame later so the renderer never blocks presentation. The
  first frame after initialization or a resize reports no GPU time.
- Device loss triggers a surface/device restart; if that fails, the shell falls back
  through OpenGL to the CPU presenter.
- OpenGL/GLES remains the required, validated, full-performance desktop path.
