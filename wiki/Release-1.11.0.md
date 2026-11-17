# 5DO 1.11.0

**Released:** 2026-11-17

5DO 1.11.0 is the **Linux platform validation and stability** release. Linux x64 is
validated end to end on bare-metal hardware — hardware OpenGL 4.2, Vulkan 1.2 through
X11 (probe and desktop shell), a 600-second real-device audio soak with zero drops and
underruns, evdev gamepads, display-sleep inhibition, fullscreen, save states,
screenshots, and clean shutdown. The release also adds the opt-in true 2x CEL path and
fixes the defects found during that pass.

## Linux x64

- **OpenGL:** 61 fps at 1920x1080 on Intel HD 4000 (Mesa 25.2.8, crocus), with
  `GLX_EXT_swap_control`.
- **Vulkan:** the X11 window probe matches the CPU oracle with 0 different pixels; the
  desktop shell presents through an X11 child window with no per-frame readback and
  survives resizing.
- **Audio:** 600 s on the default PipeWire endpoint, 26,461,445 samples, 0 dropped,
  0 underruns, 0 OpenAL errors.
- **Desktop:** fullscreen, slot save/load, pause, 320x240 screenshots, display sleep,
  evdev gamepads, and exit code 0 on software, OpenGL, and Vulkan.
- Wayland-only sessions use the OpenGL presenter; native Wayland Vulkan is not
  implemented.

## True 2x CEL (opt-in)

`--true-2x` (or Settings > Video > *True 2x CEL fields*) renders independent 480-line
CEL fields for **Jurassic Park Interactive** and **Family Feud**. Other titles fall back
to native 320x240 with a warning. Bitmap and SPORT content remains line-doubled, the
mode costs about 30% more emulation time, and native hashes are unchanged.

## Fixed

- **Linux gamepads were inert.** The evdev ioctl type/number bits were swapped, so no
  device was ever accepted.
- **Linux exit segfault.** The bundled OpenAL Soft PipeWire backend's `atexit` handler
  ran after the library was unloaded; `libopenal` is now kept mapped on Linux.
- **Software/Vulkan screenshots were 1x1 pixels.** `F12` now writes a 320x240 PNG from
  the CPU frame unless the OpenGL presenter is active.
- **Old save states no longer warn.** Version drift is an informational note; the CLI
  `run` JSON adds `notes`.
- **macOS Vulkan selections use OpenGL**, and Vulkan failures try OpenGL before the CPU
  presenter.
- **Linux Vsync reports `GLX_EXT_swap_control`.**

## Upgrading

- Install the **.NET 11 Runtime**; the bundles do not include it.
- Linux: gamepads need the `input` group or a udev rule; display-sleep inhibition uses
  `python3-dbus`.
- macOS GPU presentation and physical gamepad detection remain best-effort.

See the [full release notes](https://github.com/purelogiccode/5DO/blob/master/docs/ReleaseNotes-1.11.0.md).
