#!/usr/bin/env bash
#
# Build a single-file Flatpak bundle from a published linux bundle.
#
# The manifest packages the published self-contained apphosts, so no dotnet SDK
# extension is needed. The resulting .flatpak depends on the org.freedesktop.Platform
# runtime, which flatpak installs from Flathub on the user's machine.
#
# Usage:
#   bash packaging/linux/build-flatpak.sh --bundle artifacts/bundle/linux-x64 \
#       --rid linux-x64 --version 1.12.0 --output artifacts/installers
#
# Options:
#   --arch <x86_64|aarch64>     Flatpak arch (default: derived from --rid)
#   --runtime-version <branch>  org.freedesktop.Platform branch (default 26.08)
#   --name <file>               Output name (default 5do-<version>-linux-<arch>.flatpak)
#   --repo-dir <dir>            Keep the exported OSTree repository, for
#                               flatpak build-update-repo (replaces the directory)
#   --deps-remote <name>        Remote that supplies runtime/SDK (default flathub)
#   --no-install-deps           Do not install a missing runtime/SDK
#
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"

app_id="io.github.purelogiccode.FiveDO"
branch="stable"

bundle=""
rid="linux-x64"
version=""
output_dir=""
arch=""
runtime_version="26.08"
name=""
repo_dir=""
deps_remote="flathub"
install_deps="true"

usage() {
    sed -n '2,21p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --bundle) bundle="${2:-}"; shift 2 ;;
        --rid) rid="${2:-}"; shift 2 ;;
        --version) version="${2:-}"; shift 2 ;;
        --output) output_dir="${2:-}"; shift 2 ;;
        --arch) arch="${2:-}"; shift 2 ;;
        --runtime-version) runtime_version="${2:-}"; shift 2 ;;
        --name) name="${2:-}"; shift 2 ;;
        --repo-dir) repo_dir="${2:-}"; shift 2 ;;
        --deps-remote) deps_remote="${2:-}"; shift 2 ;;
        --no-install-deps) install_deps="false"; shift ;;
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

if [[ -z "$arch" ]]; then
    case "$rid" in
        linux-x64) arch="x86_64" ;;
        linux-arm64) arch="aarch64" ;;
        *) echo "Unsupported --rid: $rid (expected linux-x64 or linux-arm64)" >&2; exit 2 ;;
    esac
fi

for executable in FiveDo FiveDo.Cli; do
    if [[ ! -x "$bundle/$executable" ]]; then
        echo "Bundle is missing executable $executable: $bundle" >&2
        exit 1
    fi
done

if ! command -v flatpak >/dev/null 2>&1; then
    echo "flatpak is required (install the flatpak package)." >&2
    exit 1
fi
if ! command -v flatpak-builder >/dev/null 2>&1; then
    echo "flatpak-builder is required (install the flatpak-builder package)." >&2
    exit 1
fi

host_arch="$(uname -m)"
if [[ "$host_arch" != "$arch" ]]; then
    echo "Warning: host arch $host_arch differs from --arch $arch; flatpak-builder" >&2
    echo "needs qemu and a matching runtime to cross-build." >&2
fi

if [[ -z "$output_dir" ]]; then
    output_dir="$repo_root/artifacts/installers"
fi
mkdir -p "$output_dir"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

stage="$work/source"
mkdir -p "$stage/app" "$stage/desktop"
cp -a "$bundle"/. "$stage/app/"
find "$stage/app" -type f \( -name '*.pdb' -o -name '*.xml' \) -delete

# Flatpak exports only a desktop file named after the app ID, and resolves the icon
# by the same ID, so the installed entry cannot keep the short 5do.desktop name.
sed 's/^Icon=.*/Icon=io.github.purelogiccode.FiveDO/' \
    "$repo_root/packaging/5do.desktop" \
    >"$stage/desktop/$app_id.desktop"
sed 's|<launchable type="desktop-id">5do.desktop</launchable>|<launchable type="desktop-id">io.github.purelogiccode.FiveDO.desktop</launchable>|' \
    "$repo_root/packaging/linux/$app_id.metainfo.xml" \
    >"$stage/desktop/$app_id.metainfo.xml"
grep -q "desktop-id\">$app_id.desktop<" "$stage/desktop/$app_id.metainfo.xml" || {
    echo "Failed to retarget <launchable> in the AppStream metainfo." >&2
    exit 1
}

install -m 0644 "$repo_root/packaging/linux/5do.xml" "$stage/desktop/5do.xml"
install -m 0644 "$repo_root/packaging/5do.svg" "$stage/desktop/$app_id.svg"
install -m 0644 "$repo_root/packaging/linux/5do-256.png" "$stage/desktop/$app_id.png"
for document in README.md LICENSE THIRD-PARTY-NOTICES.md; do
    if [[ -f "$repo_root/$document" ]]; then
        install -m 0644 "$repo_root/$document" "$stage/desktop/$document"
    fi
done
if [[ -f "$repo_root/docs/GPL-2.0.txt" ]]; then
    install -m 0644 "$repo_root/docs/GPL-2.0.txt" "$stage/desktop/GPL-2.0.txt"
fi

manifest="$work/$app_id.yml"
sed "s/^runtime-version: .*/runtime-version: '$runtime_version'/" \
    "$repo_root/packaging/linux/$app_id.yml" >"$manifest"

if [[ "$install_deps" == "true" ]]; then
    if ! flatpak remotes --user --columns=name 2>/dev/null | grep -qx "$deps_remote"; then
        if [[ "$deps_remote" == "flathub" ]]; then
            flatpak remote-add --user --if-not-exists flathub \
                https://flathub.org/repo/flathub.flatpakrepo
        else
            echo "Flatpak remote '$deps_remote' is not configured for the user installation." >&2
            echo "Add it with 'flatpak remote-add --user $deps_remote <url>' or pass --no-install-deps." >&2
            exit 1
        fi
    fi
fi

builder_args=(--user --force-clean --disable-rofiles-fuse --arch="$arch" --repo="$work/repo")
# flatpak-builder exports to "master" unless told otherwise, while build-bundle below
# asks for $branch. Pin both to the same ref or build-bundle fails with
# "Refspec app/<id>/<arch>/<branch> not found".
builder_args+=(--default-branch="$branch")
if [[ "$install_deps" == "true" ]]; then
    builder_args+=(--install-deps-from="$deps_remote")
fi
flatpak-builder "${builder_args[@]}" "$work/build" "$manifest"

output="$output_dir/5do-${version}-linux-${arch}.flatpak"
if [[ -n "$name" ]]; then
    output="$output_dir/$name"
fi
rm -f "$output"
flatpak build-bundle --arch="$arch" "$work/repo" "$output" "$app_id" "$branch"
chmod 0644 "$output"

if [[ -n "$repo_dir" ]]; then
    rm -rf "$repo_dir"
    mkdir -p "$repo_dir"
    cp -a "$work/repo"/. "$repo_dir"/
    printf 'Kept the OSTree repository at %s\n' "$repo_dir"
fi

sha256sum "$output" >"$output.sha256"
printf 'Built %s (5DO %s, %s, runtime %s)\n' \
    "$output" "$version" "$arch" "$runtime_version"
