# Direct3D 11 Renderer (Windows, planned)

**Status:** planned (M9R.14/VID-15); **not part of v1.0**. OpenGL/GLES remains the
default desktop presenter, with the CPU `WriteableBitmap` fallback.

## Why

Windows users on drivers or remote sessions where OpenGL is unavailable or slow have no
native Direct3D path, even though Direct3D 11 is the best-supported Windows graphics API
and the API ANGLE uses underneath. The optional backend would mirror the existing
two-stage pipeline:

1. palette expansion at native resolution (`R16_UINT` indexed pixels, per-line
   `R8G8B8A8` CLUT, `R32G32_UINT` line parameters);
2. scaling/filtering at output resolution (accurate, nearest, sharp-bilinear, CRT,
   xBR-lv2, FSR 1.0 EASU + RCAS; HQx remains the shared CPU expansion).

## Planned scope

- `FiveDo.Video.Direct3D` implementing `IVideoRenderer`/`ICpuFrameRenderer` with
  Direct3D 11 Feature Level 11_0.
- HLSL ports of the palette and filter shaders, compiled at runtime; no native shader
  toolchain.
- Offscreen render target plus a staging texture for screenshots, `ReadPixels`, and the
  headless `--d3d-probe`.
- DXGI flip-model swapchain on a click-through native child HWND, with resize, DPI,
  vsync, and tearing support.
- Device-loss recovery with fallback to OpenGL, then the CPU presenter.
- `--gl-backend d3d11` (`dx11`/`directx`/`d3d` aliases), Settings > Video
  "DirectX 11 (Windows)", and `--d3d-probe`/`--d3d-window-probe`.
- 320x240 Accurate parity (`0xD6BD89C5`, 0 different pixels), 640x480, all filters,
  resize/DPI, zero steady-state allocations, and 4K timing.

OpenGL remains the default on every platform. Direct3D is Windows-only and opt-in; a
persisted DirectX selection is normalized to OpenGL on other platforms.

## Decision: Direct3D 11, not 12

D3D12 is a lower-level API, not a higher-quality renderer. The presenter submits only a
few full-screen passes per frame; D3D12's explicit command lists, descriptor heaps,
root signatures, pipeline state objects, resource barriers, and memory residency add
substantial code and failure modes without improving image quality or the measured
frame time. D3D11 is fully supported, is the API ANGLE uses underneath, and has simpler
debug/WARP/device-loss handling. A D3D12 backend can be added later behind the same
`IVideoRenderer` contract if GPU CEL compositing (VID-03) needs compute and explicit
scheduling.

## References

- [`Video.md`](Video.md)
- [`Vulkan.md`](Vulkan.md)
- `FiveDo.Video.OpenGL/OpenGlRenderer.cs`
- `FiveDo.Video.Vulkan/VulkanOffscreenRenderer.cs`
