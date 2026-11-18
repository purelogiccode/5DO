# What's new in 5DO 1.13.0

**Released:** 2026-11-18

5DO 1.13.0 brings the **native Metal presenter** to macOS, four **presentation options**
borrowed from Phoenix and 4DO, and a **signed Linux release chain**. No BIOS, game images,
or extracted assets are included; you must supply legally obtained files.

## Highlights

### macOS presents through Metal

The macOS arm64 shell now presents frames through an `NSView` + `CAMetalLayer` surface
instead of copying every frame back to the CPU:

- **1080p FIFO at 60.1 fps** with **0 bytes/frame** allocated by the renderer.
- **Window resize and fullscreen** keep a 4:3 picture: 900x620 and 1250x706 windows,
  1440x900 fullscreen with 960x720 and black-free pillarboxes.
- **4K offscreen output** (3840x2160) at 843 fps, and a 320x240 capture that matches the
  CPU oracle exactly.
- **Retina 2x** is not validated yet: the rented validation host only exposes 1:1 display
  modes, so the 2x row stays open.

### Four presentation options

Settings > Video and Settings > Audio gain options that change the picture or the live
sound without touching emulation state, frame hashes, audio hashes, or save states:

- **Swap left and right audio channels** (`--audio-swap` in the CLI). WAV captures and the
  audio hash keep the emulated channel order.
- **Named void patterns**: 4DO, Bumps, and Metal, next to solid, checkerboard, grid, and
  scanline.
- **Void color on the GPU presenters**: the letterbox and pillarbox use your
  `#RRGGBB`/`#AARRGGBB` color on OpenGL, GLES, Vulkan, and Direct3D 11 instead of black.
- **Emulation-health badge**: Idle, OK, Watch, or Issue from the counters the FPS overlay
  already collects, with a tooltip that explains the verdict.

### Signed Linux downloads

- **Every Linux artifact is signed**: `.deb`, `.rpm`, AppImage, Flatpak, and the portable
  tarballs carry a detached `.asc`, and `SHA256SUMS-linux.txt` is signed too.
- **Published keyring**: `5do-archive-keyring.gpg` ships with the release. Check it
  against fingerprint `E69FF266D1FB01D3411578775C4C1E8EAFD44075` before trusting it.
- **Verify in three commands**: `gpg --verify SHA256SUMS-linux.txt.asc`, then
  `sha256sum -c SHA256SUMS-linux.txt`.
- **Flatpak for both architectures**, an arm64 AppImage, and GNOME Software metadata
  (name, developer, summary, screenshots, Open button).

### Fixes

- Flatpak builds now finish and bundle correctly (`/media` + `/run/media` permissions,
  `stable` branch).
- The arm64 AppImage no longer disappears when the tool download flakes.
- The Linux publish job installs the mirror tools it needs before verifying the signed
  APT/RPM trees.
- The documentation site builds cleanly again.

## Verification

- **2,439/2,439 tests** pass; `dotnet format` and `mkdocs --strict` are clean.
- **CI**: Windows x64/arm64, Linux x64/arm64, macOS arm64 bundles; `.deb`/`.rpm` install
  tests; Flatpak install plus sandboxed `selftest`; signed mirror verification.

## Known limits

- macOS Retina 2x remains unvalidated (no HiDPI host).
- The Linux arm64 AppImage is built and smoke-tested in CI but not run on arm64 hardware.
- 5DO does not host a package repository or a Flathub listing; releases are GitHub assets
  plus optional signed mirror tooling.
- The release key is a single sign key kept only as a repository secret; rotation
  publishes a new key and keeps the old keyring available.
- Intel macOS is unsupported.
