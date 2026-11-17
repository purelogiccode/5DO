#!/usr/bin/env bash
#
# Build a portable AppImage from a published linux bundle.
#
# The AppImage bundles both apphosts under usr/lib/5do, exposes /usr/bin/5do and
# /usr/bin/5do-cli wrappers, and keeps the AppStream metainfo inside the image.
#
# Usage:
#   APPIMAGETOOL=/path/to/appimagetool \
#   bash packaging/linux/build-appimage.sh \
#       --bundle artifacts/bundle/linux-x64 --version 1.12.0 \
#       --output artifacts/installers
#
# Optional:
#   --sign-key <gpg-key-id>   Embed an AppImage signature (appimagetool --sign).
#   --zsync-url <url>         Embed update information and write a .zsync file.
#   --name <file>             Output file name (default 5do-<version>-<arch>.AppImage).
#
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"

bundle=""
version=""
output_dir=""
arch="x86_64"
sign_key=""
zsync_url=""
name=""
appimagetool="${APPIMAGETOOL:-}"

usage() {
    sed -n '2,18p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --bundle) bundle="${2:-}"; shift 2 ;;
        --version) version="${2:-}"; shift 2 ;;
        --output) output_dir="${2:-}"; shift 2 ;;
        --arch) arch="${2:-}"; shift 2 ;;
        --sign-key) sign_key="${2:-}"; shift 2 ;;
        --zsync-url) zsync_url="${2:-}"; shift 2 ;;
        --name) name="${2:-}"; shift 2 ;;
        --appimagetool) appimagetool="${2:-}"; shift 2 ;;
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

for executable in FiveDo FiveDo.Cli; do
    if [[ ! -x "$bundle/$executable" ]]; then
        echo "Bundle is missing executable $executable: $bundle" >&2
        exit 1
    fi
done

if [[ -z "$appimagetool" ]]; then
    appimagetool="$(command -v appimagetool || true)"
fi
if [[ -z "$appimagetool" || ! -x "$appimagetool" ]]; then
    echo "appimagetool is required. Set APPIMAGETOOL or pass --appimagetool." >&2
    exit 1
fi

if [[ -z "$output_dir" ]]; then
    output_dir="$repo_root/artifacts/installers"
fi
mkdir -p "$output_dir"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
appdir="$work/AppDir"

install -d "$appdir/usr/bin" "$appdir/usr/lib/5do" \
    "$appdir/usr/share/applications" "$appdir/usr/share/metainfo" \
    "$appdir/usr/share/mime/packages" \
    "$appdir/usr/share/icons/hicolor/scalable/apps" \
    "$appdir/usr/share/icons/hicolor/256x256/apps" \
    "$appdir/usr/share/doc/5do"

cp -a "$bundle"/. "$appdir/usr/lib/5do/"
rm -f "$appdir/usr/lib/5do/5do.desktop"
find "$appdir/usr/lib/5do" -type f \( -name '*.pdb' -o -name '*.xml' \) -delete
find "$appdir/usr/lib/5do" -type d -exec chmod 0755 {} +
find "$appdir/usr/lib/5do" -type f -exec chmod 0644 {} +
chmod 0755 "$appdir/usr/lib/5do/FiveDo" "$appdir/usr/lib/5do/FiveDo.Cli"

cat >"$appdir/AppRun" <<'EOF'
#!/bin/sh
set -eu
HERE="$(dirname "$(readlink -f "$0")")"
export APPDIR="${APPDIR:-$HERE}"
exec "$HERE/usr/lib/5do/FiveDo" "$@"
EOF

cat >"$appdir/usr/bin/5do" <<'EOF'
#!/bin/sh
set -eu
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/../lib/5do/FiveDo" "$@"
EOF

cat >"$appdir/usr/bin/5do-cli" <<'EOF'
#!/bin/sh
set -eu
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/../lib/5do/FiveDo.Cli" "$@"
EOF

chmod 0755 "$appdir/AppRun" "$appdir/usr/bin/5do" "$appdir/usr/bin/5do-cli"

install -m 0644 "$repo_root/packaging/5do.desktop" "$appdir/5do.desktop"
install -m 0644 "$repo_root/packaging/5do.desktop" "$appdir/usr/share/applications/5do.desktop"
install -m 0644 "$repo_root/packaging/linux/5do.xml" "$appdir/usr/share/mime/packages/5do.xml"
install -m 0644 "$repo_root/packaging/linux/io.github.purelogiccode.FiveDO.metainfo.xml" \
    "$appdir/usr/share/metainfo/io.github.purelogiccode.FiveDO.metainfo.xml"
install -m 0644 "$repo_root/packaging/5do.svg" "$appdir/usr/share/icons/hicolor/scalable/apps/5do.svg"
install -m 0644 "$repo_root/packaging/linux/5do-256.png" "$appdir/usr/share/icons/hicolor/256x256/apps/5do.png"
install -m 0644 "$repo_root/packaging/5do.svg" "$appdir/5do.svg"
install -m 0644 "$repo_root/packaging/linux/5do-256.png" "$appdir/5do.png"
ln -sf 5do.png "$appdir/.DirIcon"

for document in README.md LICENSE THIRD-PARTY-NOTICES.md; do
    if [[ -f "$repo_root/$document" ]]; then
        install -m 0644 "$repo_root/$document" "$appdir/usr/share/doc/5do/$document"
    fi
done

# appimagetool shells out to gpg2 for --sign; provide a shim when only gpg exists.
if [[ -n "$sign_key" ]] && ! command -v gpg2 >/dev/null 2>&1 && command -v gpg >/dev/null 2>&1; then
    mkdir -p "$work/bin"
    ln -s "$(command -v gpg)" "$work/bin/gpg2"
    PATH="$work/bin:$PATH"
    export PATH
fi

arguments=(--comp zstd)
if "$appimagetool" --help 2>&1 | grep -q -- '--no-appstream'; then
    arguments+=(--no-appstream)
fi
if [[ -n "$zsync_url" ]]; then
    arguments+=(--updateinformation "zsync|$zsync_url")
fi
if [[ -n "$sign_key" ]]; then
    arguments+=(--sign --sign-key "$sign_key")
fi

output="$output_dir/5do-${version}-${arch}.AppImage"
if [[ -n "$name" ]]; then
    output="$output_dir/$name"
fi
rm -f "$output" "$output.zsync"
# Current appimagetool has no --arch option: it guesses from the AppDir or reads ARCH.
ARCH="$arch" "$appimagetool" "${arguments[@]}" "$appdir" "$output"

chmod 0755 "$output"

if [[ -n "$zsync_url" ]] && command -v zsyncmake >/dev/null 2>&1; then
    zsyncmake -u "$zsync_url" -o "$output.zsync" "$output"
fi

if [[ "$(uname -m)" == "$arch" ]]; then
    "$output" --appimage-extract-and-run --version
fi

sha256sum "$output" >"$output.sha256"
printf 'Built %s\n' "$output"
