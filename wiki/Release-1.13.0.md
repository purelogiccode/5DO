# 5DO 1.13.0

**Released:** 2026-11-18

5DO 1.13.0 adds the **native Metal presenter** on macOS, four **presentation options**
from Phoenix and 4DO, and **GPG-signed Linux artifacts**.

Download the Windows, Linux, or macOS bundle from
[GitHub Releases](https://github.com/purelogiccode/5DO/releases/tag/v1.13.0).
No BIOS ROMs or game images are included.

## Highlights

### macOS presents through Metal

macOS arm64 now presents frames through an `NSView` + `CAMetalLayer` surface
(`VK_EXT_metal_surface`) instead of copying every frame back to the CPU:

- 1080p FIFO at 60.1 fps with **0 bytes/frame** allocated by the renderer.
- Live window resize and fullscreen keep a 4:3 picture (1440x900 fullscreen shows
  960x720 with side bars).
- 320x240 capture parity with the CPU oracle and 3840x2160 offscreen output.
- **Retina 2x is not validated yet:** the validation host only exposes 1:1 modes.

### Presentation options

Settings > Video and Settings > Audio gain presentation-only options that never change
frame hashes, audio hashes, input, or save states:

- **Swap left and right audio channels** (also `--audio-swap`); WAV captures keep the
  emulated order.
- **Void patterns** 4DO, Bumps, and Metal next to the existing fills.
- **Void color on the GPU presenters**, so OpenGL, GLES, Vulkan, and Direct3D 11 paint
  the letterbox/pillarbox in your color instead of black.
- **Emulation-health badge** (Idle / OK / Watch / Issue) in the status bar.

### Signed Linux downloads

- Every `.deb`, `.rpm`, AppImage, Flatpak, and portable tarball now carries a detached
  `.asc`, and `SHA256SUMS-linux.txt` is signed too.
- The release publishes `5do-archive-keyring.gpg`. Check the fingerprint before trusting
  it: `E69FF266D1FB01D3411578775C4C1E8EAFD44075`.
- Verify with `gpg --verify SHA256SUMS-linux.txt.asc SHA256SUMS-linux.txt` and
  `sha256sum -c SHA256SUMS-linux.txt`.
- Both architectures now ship a Flatpak, and arm64 has an AppImage.

### Fixes

- Flatpak builds finish correctly (`/media` + `/run/media` permissions, `stable` branch
  export).
- The arm64 AppImage is retried instead of silently skipped when its tool download
  flakes.
- The Linux publish job installs the mirror tools before verifying the signed APT/RPM
  trees.
- The documentation site builds cleanly again.

## Verification

- 2,439/2,439 tests pass; `dotnet format`, `jb inspectcode`, and `mkdocs --strict` are
  clean.
- CI builds and installs the `.deb`/`.rpm` sets, installs the Flatpak bundles and runs
  `5do-cli selftest` inside the sandbox, verifies the signed mirror trees, and signs
  every push.

## Known limits

- macOS Retina 2x is unvalidated (no HiDPI host).
- The Linux arm64 AppImage is built and smoke-tested in CI but has not been run on arm64
  hardware.
- No hosted package repository or Flathub listing; releases are GitHub assets plus
  optional signed mirror tooling.
- The release key is a single sign key stored only as a repository secret; rotation
  publishes a new key and keeps the old keyring available.
- Intel macOS is unsupported.

Full details: [ReleaseNotes-1.13.0.md](https://github.com/purelogiccode/5DO/blob/master/docs/ReleaseNotes-1.13.0.md).
