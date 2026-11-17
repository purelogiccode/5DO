# What's new in 5DO 1.11.0

**Released:** 2026-11-17

5DO 1.11.0 is the **Linux platform validation and stability** update. Linux x64 is now
validated on a real machine — hardware OpenGL, Vulkan through X11, real-device audio,
gamepads, display sleep, fullscreen, save states, screenshots, and clean shutdown — and
the release adds the opt-in true 2x CEL path plus fixes for four defects found during
that pass. Emulation accuracy and deterministic hashes are unchanged.

## Highlights

### Linux x64 is validated

Validated on a bare-metal Linux Mint 22.3 host (Intel HD 4000, Mesa 25.2.8, 1920x1080
X11, HDA audio):

- **OpenGL 4.2 hardware presentation** at 61 fps / 1080p, with `GLX_EXT_swap_control`.
- **Vulkan 1.2** presents through an X11 swapchain in both the window probe and the
  desktop shell. A 320x240 capture matches the CPU oracle with **0 different pixels**;
  the shell has no per-frame readback and survives resizing.
- **600 s real-device audio soak:** 26,461,445 samples, **0 dropped**, **0 underruns**,
  0 OpenAL errors.
- **evdev gamepads**, **display-sleep inhibition**, `F11` fullscreen, `F5`/`F8` slot
  save/load, `Escape` pause, `F12` 320x240 screenshots, and exit code 0 on the software,
  OpenGL, and Vulkan presenters.
- Wayland-only sessions use the OpenGL presenter; native Wayland Vulkan is not
  implemented.

### True 2x CEL fields (opt-in)

`--true-2x` (or Settings > Video > *True 2x CEL fields*) renders CELs into independent
480-line fields for **Jurassic Park Interactive** and **Family Feud**. Other titles fall
back to native 320x240 with a warning. Bitmap and SPORT content remains line-doubled,
the mode costs about 30% more emulation time, and native hashes are unchanged.

## Fixed

- **Linux gamepads now work.** The evdev ioctl request had the type and number bits
  swapped, so no device was ever accepted.
- **Linux exit segfault fixed.** The bundled OpenAL Soft PipeWire backend's `atexit`
  handler ran after the library was unloaded; `libopenal` is now kept mapped on Linux.
- **Screenshots on software/Vulkan are 320x240**, not 1x1 pixels.
- **Old save states no longer warn.** Version drift is an informational note; the CLI
  `run` JSON adds `notes`.
- **macOS Vulkan selections now use OpenGL**, and Vulkan failures try OpenGL before the
  CPU presenter.
- **Linux Vsync now reports `GLX_EXT_swap_control`** instead of a WGL name.

## Upgrading

- Install the **.NET 11 runtime**; the bundles do not include it.
- Linux: gamepads need the `input` group or a udev rule; display-sleep inhibition uses
  `python3-dbus`, normally installed with the desktop.
- macOS GPU presentation and physical gamepad detection remain best-effort.

See [ReleaseNotes-1.11.0.md](ReleaseNotes-1.11.0.md) for the full list and evidence.
