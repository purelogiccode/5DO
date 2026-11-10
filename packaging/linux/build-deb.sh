#!/usr/bin/env bash
#
# Build an unsigned Debian package from a published linux bundle.
#
# Usage:
#   bash packaging/linux/build-deb.sh --bundle artifacts/bundle/linux-x64 \
#       --rid linux-x64 --version 1.6.0 --output artifacts/installers
#
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"

bundle=""
rid="linux-x64"
version=""
output_dir=""

usage() {
    sed -n '2,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --bundle) bundle="${2:-}"; shift 2 ;;
        --rid) rid="${2:-}"; shift 2 ;;
        --version) version="${2:-}"; shift 2 ;;
        --output) output_dir="${2:-}"; shift 2 ;;
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

for executable in FiveDo.Frontend.Avalonia FiveDo.Cli; do
    if [[ ! -x "$bundle/$executable" ]]; then
        echo "Bundle is missing executable $executable: $bundle" >&2
        exit 1
    fi
done

if ! command -v dpkg-deb >/dev/null 2>&1; then
    echo "dpkg-deb is required (install dpkg-dev)." >&2
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
    "$stage/usr/share/icons/hicolor/scalable/apps" "$stage/usr/share/doc/5do" \
    "$stage/DEBIAN"

cp -a "$bundle"/. "$stage/opt/5do/"
rm -f "$stage/opt/5do/5do.desktop"
find "$stage/opt/5do" -type f \( -name '*.pdb' -o -name '*.xml' \) -delete
find "$stage/opt/5do" -type d -exec chmod 0755 {} +
find "$stage/opt/5do" -type f -exec chmod 0644 {} +
chmod 0755 "$stage/opt/5do/FiveDo.Frontend.Avalonia" "$stage/opt/5do/FiveDo.Cli"
chmod 0755 "$stage"

ln -sf /opt/5do/FiveDo.Frontend.Avalonia "$stage/usr/bin/5do"
ln -sf /opt/5do/FiveDo.Cli "$stage/usr/bin/5do-cli"
install -m 0644 "$repo_root/packaging/5do.desktop" "$stage/usr/share/applications/5do.desktop"
install -m 0644 "$repo_root/packaging/linux/5do.xml" "$stage/usr/share/mime/packages/5do.xml"
install -m 0644 "$repo_root/packaging/5do.svg" "$stage/usr/share/icons/hicolor/scalable/apps/5do.svg"

for document in LICENSE THIRD-PARTY-NOTICES.md README.md; do
    if [[ -f "$repo_root/$document" ]]; then
        install -m 0644 "$repo_root/$document" "$stage/usr/share/doc/5do/$document"
    fi
done

installed_size="$(du -sk "$stage/opt/5do" | awk '{print $1}')"
cat >"$stage/DEBIAN/control" <<EOF
Package: 5do
Version: $version
Section: games
Priority: optional
Architecture: $deb_arch
Installed-Size: $installed_size
Maintainer: 5DO <noreply@localhost>
Homepage: https://github.com/purelogiccode/5DO
Depends: dotnet-runtime-10.0
Description: 3DO Interactive Multiplayer emulator
 5DO is a FreeDO-derived 3DO emulator with a headless CLI and an Avalonia desktop
 frontend. This package installs both apphosts under /opt/5do and registers the
 5do desktop entry, icon, and disc-image MIME type.
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
exit 0
EOF
chmod 0755 "$stage/DEBIAN/postinst" "$stage/DEBIAN/postrm"

archive="$output_dir/5do_${version}_${deb_arch}.deb"
rm -f "$archive"
dpkg-deb --build --root-owner-group "$stage" "$archive" >/dev/null

sha256sum "$archive" >"$archive.sha256"
printf 'Built %s\n' "$archive"
