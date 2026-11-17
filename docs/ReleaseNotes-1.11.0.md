# 5DO 1.11.0

**Released:** 2026-11-17

5DO 1.11.0 is the **Linux platform validation and stability** release. The Linux x64
desktop is validated end-to-end on bare-metal hardware: OpenGL, Vulkan through X11
(offscreen probe and desktop shell), real-device audio, evdev gamepads, display-sleep
inhibition, fullscreen, save states, screenshots, and clean shutdown. The release also
adds the opt-in true 2x CEL path and fixes the defects found during that validation.
Emulation accuracy, deterministic frame/state hashes, and the documented compatibility
model are unchanged.

## Highlights

### Linux x64 is validated on real hardware

The validation host is a physical Linux Mint 22.3 machine (Intel Core i7-3615QM,
Intel HD 4000, Mesa 25.2.8, 1920x1080 X11, HDA Intel PCH audio). It replaces the
llvmpipe-only VM for platform work.

- **OpenGL:** 1920x1080 sharp-bilinear probe at 61 fps with hardware OpenGL 4.2
  (crocus), present mean 1.9-2.5 ms and GPU 1.2-1.3 ms, zero renderer allocations per
  frame. Vsync reports the real `GLX_EXT_swap_control` backend.
- **Vulkan:** the X11 window probe presents through `VK_KHR_xlib_surface` at 1080p FIFO
  (60.1 fps, 0 bytes/frame) and a 320x240 accurate capture matches the CPU oracle with
  **0 different pixels**. `--gl-backend vulkan` runs in the desktop shell through a
  depth-24 X11 child window with no per-frame readback, including resize.
- **Audio:** a 600 s real-device soak on the default PipeWire endpoint pushed 26,461,445
  samples with **0 dropped samples**, **0 underruns**, and 0 OpenAL errors.
- **Gamepads:** evdev devices are detected and mapped; a virtual kernel pad drives the
  3DO and the Joypad Test disc reports the pressed button.
- **Desktop integration:** display-sleep inhibition holds the freedesktop screen-saver
  inhibitor while an unpaused game runs and releases it on exit. `F11` fullscreen,
  `F5`/`F8` slot save/load, `Escape` pause, and `F12` screenshots (320x240 PNG) pass.
- **Shutdown:** software, OpenGL, and Vulkan sessions all exit with code 0.
- **Wayland-only sessions** use the OpenGL presenter. Native Wayland Vulkan surfaces
  are not implemented.

### True 2x CEL path (opt-in)

The experimental `--true-2x` machine option (Settings > Video > *True 2x CEL fields*)
renders CELs into independent 480-line fields instead of line-doubling each 240-line
field. It is gated to the two visually validated titles:

- **Jurassic Park Interactive** - the Raptor Maze renders the full corridor with
  independent vertical detail (38.41% of pixels differ from a 2x nearest upscale).
- **Family Feud** - matches a clean 2x scale exactly (0.00%).

Every other title falls back to native 320x240 with a warning. Bitmap and SPORT content
remains line-doubled, and the mode costs about 30% more emulation time. Native frame
hashes are unchanged.

## Fixed

- **Linux gamepads were completely inert.** The evdev ioctl request encoded the type
  and request number in the wrong bit positions, so every capability query failed and
  no device was ever accepted. A virtual uinput pad now reports `connected=1`, and
  `--gamepad-probe` samples for two seconds so a short press is not missed.
- **Linux sessions crashed on exit.** The bundled OpenAL Soft 1.23.1 PipeWire backend
  registers an `atexit` handler; Silk.NET unloaded `libopenal.so` when the last context
  closed, leaving the handler pointing at unmapped code. The library is now pinned for
  the process lifetime on Linux.
- **Software and Vulkan screenshots were 1x1 pixels.** `F12` took the OpenGL capture
  branch even when the OpenGL presenter was not active. The capture now requires the
  OpenGL presenter, and both backends write a native 320x240 PNG from the CPU frame.
- **Old save states no longer raise warnings.** A state written by another emulator
  version loads completely; version drift is an informational note, not a warning. The
  warning toast and Warning log entry are reserved for BIOS/disc/option mismatches and
  ignored unknown sections. The CLI `run` JSON adds a `notes` array.
- **macOS Vulkan selections fell back to the CPU presenter.** Vulkan has no Metal
  surface yet, so the shell now selects OpenGL on macOS, and any Vulkan presentation
  failure tries OpenGL before the CPU presenter on every platform.
- **Linux GL swap-interval reported the wrong backend.** The probe claimed
  `WGL_EXT_swap_control`; it now reports `GLX_EXT_swap_control`.

## Compatibility and requirements

- The corpus result is unchanged: **375 / 378 playable**, 0 `Behind`, 0 `Regression`.
- The bundles remain framework-dependent: install the **.NET 11 runtime** on the host.
- Linux: users in the `input` group (or a udev rule) get evdev gamepads; display-sleep
  inhibition uses `python3-dbus`, which desktop installations normally provide.
- macOS GPU presentation and physical gamepad detection remain best-effort.

## Known limitations

- Native Wayland Vulkan surfaces, GLES 3.1 hardware, DPI 100/150/200% live validation,
  and Tier B timing remain best-effort (`PLAT-06`, `PLAT-08A`).
- True 2x presentation still line-doubles bitmap/SPORT content and the bottom VDL rows;
  it remains gated to Jurassic Park Interactive and Family Feud (`VID-01`).
- Physical optical-drive boot and signed/notarized installers remain hardware- and
  credential-blocked.
