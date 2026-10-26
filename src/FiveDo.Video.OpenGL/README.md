# FiveDo.Video.OpenGL

Primary desktop renderer for 5DO.

Planned contents:

- `OpenGlRenderer` - context lifecycle, textures, framebuffer, present.
- `FrameTextureUploader` - uploads raw 16-bit VDL pixels plus per-line palette/control data.
- `Shaders/` - GLSL 330 core shaders:
  - palette expansion equivalent to `frame.cpp: Get_Frame_Bitmap`
  - fixed grayscale CLUT path
  - background color path
  - nearest / sharp-bilinear / CRT / xBR-family filters
  - integer scaling and aspect-ratio handling
- `RendererSettings` - filter, scale, aspect, vsync, screenshot options.
- `GlContextAdapter` - thin adapter over Avalonia's `OpenGlControlBase` / Silk.NET.OpenGL.

The renderer is a host-level project. It references `FiveDo.Core` and `FiveDo.Video` but is not referenced by the emulation core. The core stays platform-neutral and deterministic; the GPU only consumes completed VDL frames.

Accuracy rule: the "Accurate" filter mode must match the CPU `FrameAssembler` output pixel-for-pixel. Enhanced filters are opt-in and must not feed back into emulated state.
