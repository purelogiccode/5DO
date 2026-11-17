# AUR `5do-bin`

This directory is the reference copy of the AUR package. The published package lives in
its own AUR git repository (`ssh://aur@aur.archlinux.org/5do-bin.git`); AUR does not
build from this repository.

## Before submitting

1. Publish the release so `5do-<version>-linux-x64.tar.gz` and
   `5do-<version>-linux-arm64.tar.gz` exist.
2. Update `pkgver` and `pkgrel` in `PKGBUILD`.
3. Set `sha256sums_x86_64` and `sha256sums_aarch64` from the published
   `SHA256SUMS.txt` (one line per architecture asset).
4. Build and test locally:

```sh
makepkg -si
5do --version
5do-cli selftest --json
5do doctor
```

5. Regenerate the metadata and push:

```sh
makepkg --printsrcinfo > .SRCINFO
git clone ssh://aur@aur.archlinux.org/5do-bin.git aur-5do-bin
cp PKGBUILD .SRCINFO aur-5do-bin/
cd aur-5do-bin
git add PKGBUILD .SRCINFO
git commit -m "Update to 5DO <version>"
git push
```

## Notes

- `options=('!strip')` is required: the apphost is a self-contained single-file
  executable and stripping it can corrupt the embedded runtime.
- The package installs to `/opt/5do` and exposes `/usr/bin/5do` and `/usr/bin/5do-cli`,
  matching the `.deb` and `.rpm` packages.
- The desktop entry, MIME definition, AppStream metainfo, and icons come from the
  `packaging/` directory inside the release tarball.
- AUR rules require a legally redistributable package. The FreeDO modified LGPL
  prohibits commercial use without the rights holders' approval, so keep the license
  and attribution intact and do not charge for the package. See
  `docs/LinuxDistribution.md` §9.
