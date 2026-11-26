#!/usr/bin/env bash
#
# Build a Debian package from a published linux bundle.
#
# The bundle apphosts are self-contained (the .NET 11 runtime is embedded), so the
# package does not depend on a dotnet-runtime package. It depends on the X11,
# fontconfig, freetype, and GL libraries that the Avalonia and Skia native code loads
# at run time.
#
# Usage:
#   bash packaging/linux/build-deb.sh --bundle artifacts/bundle/linux-x64 \
#       --rid linux-x64 --version 1.12.0 --output artifacts/installers
#
# Integrity: a .deb has no per-package signature that APT verifies. Release
# downloads are validated with SHA256SUMS only; no detached signatures are published.
# Operators who mirror the packages can still sign their own repository metadata.
#
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"

bundle=""
rid="linux-x64"
version=""
output_dir=""
maintainer="5DO contributors <noreply@localhost>"

usage() {
    sed -n '2,19p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --bundle) bundle="${2:-}"; shift 2 ;;
        --rid) rid="${2:-}"; shift 2 ;;
        --version) version="${2:-}"; shift 2 ;;
        --output) output_dir="${2:-}"; shift 2 ;;
        --maintainer) maintainer="${2:-}"; shift 2 ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
done

if [[ -z "$bundle" ]]; then
    echo "--bundle is required." >&2
    exit 2
fi
if [[ ! -d "$bundle" ]]; then
    echo "Bundle directory not found: $bundle" >&2
    exit 1
fi
bundle="$(cd "$bundle" && pwd)"

if [[ -z "$version" ]]; then
    version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$repo_root/Directory.Build.props" | head -n1)"
fi
version="${version#v}"
if [[ -z "$version" ]]; then
    version="0.0.0"
fi

case "$rid" in
    linux-x64) deb_arch="amd64" ;;
    linux-arm64) deb_arch="arm64" ;;
    *) echo "Unsupported --rid: $rid (expected linux-x64 or linux-arm64)" >&2; exit 2 ;;
esac

for executable in FiveDo FiveDo.Cli; do
    if [[ ! -x "$bundle/$executable" ]]; then
        echo "Bundle is missing executable $executable: $bundle" >&2
        exit 1
    fi
done

if ! command -v dpkg-deb >/dev/null 2>&1; then
    echo "dpkg-deb is required (install dpkg)." >&2
    exit 1
fi

if [[ -z "$output_dir" ]]; then
    output_dir="$repo_root/artifacts/installers"
fi
mkdir -p "$output_dir"

stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT

install -d "$stage/opt/5do" "$stage/usr/bin" \
    "$stage/usr/share/applications" "$stage/usr/share/mime/packages" \
    "$stage/usr/share/metainfo" \
    "$stage/usr/share/icons/hicolor/scalable/apps" \
    "$stage/usr/share/icons/hicolor/256x256/apps" \
    "$stage/usr/share/doc/5do" \
    "$stage/DEBIAN"

cp -a "$bundle"/. "$stage/opt/5do/"
rm -f "$stage/opt/5do/5do.desktop"
find "$stage/opt/5do" -type f \( -name '*.pdb' -o -name '*.xml' \) -delete
find "$stage/opt/5do" -type d -exec chmod 0755 {} +
find "$stage/opt/5do" -type f -exec chmod 0644 {} +
chmod 0755 "$stage/opt/5do/FiveDo" "$stage/opt/5do/FiveDo.Cli"
chmod 0755 "$stage"

ln -sf /opt/5do/FiveDo "$stage/usr/bin/5do"
ln -sf /opt/5do/FiveDo.Cli "$stage/usr/bin/5do-cli"
install -m 0644 "$repo_root/packaging/5do.desktop" "$stage/usr/share/applications/5do.desktop"
install -m 0644 "$repo_root/packaging/linux/5do.xml" "$stage/usr/share/mime/packages/5do.xml"
install -m 0644 "$repo_root/packaging/linux/io.github.purelogiccode.FiveDO.metainfo.xml" \
    "$stage/usr/share/metainfo/io.github.purelogiccode.FiveDO.metainfo.xml"
install -m 0644 "$repo_root/packaging/5do.svg" "$stage/usr/share/icons/hicolor/scalable/apps/5do.svg"
install -m 0644 "$repo_root/packaging/linux/5do-256.png" "$stage/usr/share/icons/hicolor/256x256/apps/5do.png"

for document in README.md THIRD-PARTY-NOTICES.md; do
    if [[ -f "$repo_root/$document" ]]; then
        install -m 0644 "$repo_root/$document" "$stage/usr/share/doc/5do/$document"
    fi
done

# Debian policy names the license file "copyright".
if [[ -f "$repo_root/LICENSE" ]]; then
    install -m 0644 "$repo_root/LICENSE" "$stage/usr/share/doc/5do/copyright"
fi
if [[ -f "$repo_root/docs/GPL-2.0.txt" ]]; then
    install -m 0644 "$repo_root/docs/GPL-2.0.txt" "$stage/usr/share/doc/5do/GPL-2.0.txt"
fi

installed_size="$(du -sk "$stage/opt/5do" "$stage/usr/share" | awk '{ total += $1 } END { print total }')"
cat >"$stage/DEBIAN/control" <<EOF
Package: 5do
Version: $version
Section: games
Priority: optional
Architecture: $deb_arch
Installed-Size: $installed_size
Maintainer: $maintainer
Homepage: https://github.com/purelogiccode/5DO
Depends: libc6, libgcc-s1, libstdc++6, zlib1g, libx11-6, libice6, libsm6, libxext6, libxrandr2, libxi6, libxcursor1, libgl1, libglib2.0-0 | libglib2.0-0t64, libfontconfig1 | libfontconfig1t64, libfreetype6 | libfreetype6t64
Recommends: python3-dbus
Description: 3DO Interactive Multiplayer emulator
 5DO is a FreeDO-derived 3DO emulator with a headless CLI and an Avalonia desktop
 frontend. This package installs both apphosts under /opt/5do, registers the 5do
 desktop entry, icon, and AppStream metadata, and adds the application/x-3do-disc
 MIME type for *.iso, *.cue, *.chd, *.bin, *.cdi, and *.nrg images.
 .
 The .NET 11 runtime is embedded in the apphosts; no separate runtime package is
 required. A 3DO BIOS image and game images must be supplied by the user.
EOF

cat >"$stage/DEBIAN/postinst" <<'EOF'
#!/bin/sh
set -e
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database -q /usr/share/applications || true
fi
if command -v update-mime-database >/dev/null 2>&1; then
    update-mime-database /usr/share/mime || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q /usr/share/icons/hicolor || true
fi
if command -v appstreamcli >/dev/null 2>&1; then
    appstreamcli refresh --force >/dev/null 2>&1 || true
fi
exit 0
EOF

cat >"$stage/DEBIAN/postrm" <<'EOF'
#!/bin/sh
set -e
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database -q /usr/share/applications || true
fi
if command -v update-mime-database >/dev/null 2>&1; then
    update-mime-database /usr/share/mime || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q /usr/share/icons/hicolor || true
fi
exit 0
EOF
chmod 0755 "$stage/DEBIAN/postinst" "$stage/DEBIAN/postrm"

# Lintian: the changelog is optional, but `dpkg-deb` warns about a missing
# /usr/share/doc/<pkg> when the package has no documentation at all. 5DO installs
# copyright, README, and the third-party notices instead of a Debian changelog.
cat >"$stage/usr/share/doc/5do/changelog.Debian" <<EOF
5do ($version) unstable; urgency=medium

  * Release $version. See /usr/share/doc/5do/README.md for details.

 -- 5DO contributors <noreply@localhost>  $(date -u -R)
EOF
gzip -9n "$stage/usr/share/doc/5do/changelog.Debian"

archive="$output_dir/5do_${version}_${deb_arch}.deb"
rm -f "$archive"
dpkg-deb --build --root-owner-group "$stage" "$archive" >/dev/null

sha256sum "$archive" >"$archive.sha256"
printf 'Built %s\n' "$archive"
