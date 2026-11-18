#!/usr/bin/env bash
#
# Build (or update) a signed APT repository from 5DO .deb packages.
#
# APT verifies the repository index (Release/InRelease), not the individual .deb
# files, so signing this metadata is what makes `apt install 5do` trustworthy.
#
# Usage:
#   bash packaging/linux/build-apt-repo.sh \
#       --repo-dir artifacts/mirror/apt \
#       --base-url https://mirror.example.org/5do/apt \
#       --key-id 5DO0123456789ABCDEF \
#       artifacts/installers
#
# The repository keeps every package it has seen (pool/), so run this against the
# directory from the previous release to preserve history. Indexes are regenerated
# from the whole pool on every run.
#
# Signing also publishes 5do-archive-keyring.gpg (+ .asc and .fingerprint) at the
# repository root, which is the URL written into 5do.list and the generated README.
# Pass --no-keyring to manage the keyring yourself.
#
# Environment:
#   LINUX_GPG_KEY_ID      default for --key-id
#   LINUX_GPG_HOME        optional GNUPGHOME override
#
set -euo pipefail

repo_dir=""
suite="stable"
component="main"
origin="5DO"
label="5DO"
description="5DO 3DO Interactive Multiplayer emulator"
base_url=""
key_id="${LINUX_GPG_KEY_ID:-}"
unsigned="false"
publish_keyring="true"
inputs=()

usage() {
    sed -n '2,26p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --repo-dir) repo_dir="${2:-}"; shift 2 ;;
        --suite) suite="${2:-}"; shift 2 ;;
        --component) component="${2:-}"; shift 2 ;;
        --origin) origin="${2:-}"; shift 2 ;;
        --label) label="${2:-}"; shift 2 ;;
        --description) description="${2:-}"; shift 2 ;;
        --base-url) base_url="${2:-}"; shift 2 ;;
        --key-id) key_id="${2:-}"; shift 2 ;;
        --unsigned) unsigned="true"; shift ;;
        --no-keyring) publish_keyring="false"; shift ;;
        -h|--help) usage; exit 0 ;;
        --) shift; inputs+=("$@"); break ;;
        -*) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
        *) inputs+=("$1"); shift ;;
    esac
done

if [[ -z "$repo_dir" ]]; then
    echo "--repo-dir is required." >&2
    exit 2
fi
if [[ -z "$base_url" ]]; then
    echo "--base-url is required (it is written into 5do.list)." >&2
    exit 2
fi
base_url="${base_url%/}"

if [[ -n "${LINUX_GPG_HOME:-}" ]]; then
    export GNUPGHOME="$LINUX_GPG_HOME"
fi

for tool in dpkg-deb dpkg-scanpackages apt-ftparchive gzip; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "$tool is required (install dpkg-dev and apt-utils)." >&2
        exit 1
    fi
done

if [[ "$unsigned" != "true" ]]; then
    if [[ -z "$key_id" ]]; then
        echo "--key-id or LINUX_GPG_KEY_ID is required (or pass --unsigned)." >&2
        exit 2
    fi
    if ! command -v gpg >/dev/null 2>&1; then
        echo "gpg is required to sign the repository." >&2
        exit 1
    fi
fi

packages=()
if [[ ${#inputs[@]} -gt 0 ]]; then
    for input in "${inputs[@]}"; do
        if [[ -d "$input" ]]; then
            while IFS= read -r -d '' file; do
                packages+=("$file")
            done < <(find "$input" -maxdepth 1 -type f -name '*.deb' -print0 | sort -z)
        elif [[ -f "$input" ]]; then
            packages+=("$input")
        else
            echo "Not a file or directory: $input" >&2
            exit 1
        fi
    done
fi

mkdir -p "$repo_dir/pool/$component"
repo_dir="$(cd "$repo_dir" && pwd)"

for package in "${packages[@]}"; do
    package_name="$(dpkg-deb -f "$package" Package)"
    if [[ -z "$package_name" ]]; then
        echo "Cannot read Package from $package" >&2
        exit 1
    fi
    case "$package_name" in
        lib*) pool_letter="${package_name:0:4}" ;;
        *) pool_letter="${package_name:0:1}" ;;
    esac
    destination="$repo_dir/pool/$component/$pool_letter/$package_name"
    mkdir -p "$destination"
    cp -f "$package" "$destination/"
    printf 'Pooled %s\n' "$(basename "$package")"
done

architectures=()
while IFS= read -r arch; do
    if [[ -n "$arch" ]]; then
        architectures+=("$arch")
    fi
done < <(find "$repo_dir/pool" -type f -name '*.deb' -print0 |
    xargs -0 -r -n1 dpkg-deb -f 2>/dev/null |
    sed -n 's/^Architecture: *//p' | sort -u)

if [[ ${#architectures[@]} -eq 0 ]]; then
    echo "No packages found in $repo_dir/pool." >&2
    exit 1
fi

cd "$repo_dir"
for arch in "${architectures[@]}"; do
    packages_dir="dists/$suite/$component/binary-$arch"
    mkdir -p "$packages_dir"
    dpkg-scanpackages --arch "$arch" --multiversion "pool" /dev/null >"$packages_dir/Packages"
    gzip -9nc "$packages_dir/Packages" >"$packages_dir/Packages.gz"
    printf 'Indexed %s (%s packages)\n' "$arch" \
        "$(grep -c '^Package:' "$packages_dir/Packages" || true)"
done

rm -f "dists/$suite/Release" "dists/$suite/InRelease" "dists/$suite/Release.gpg"
# Write the Release file outside dists/ so it does not checksum itself.
apt-ftparchive \
    -o APT::FTPArchive::Release::Origin="$origin" \
    -o APT::FTPArchive::Release::Label="$label" \
    -o APT::FTPArchive::Release::Suite="$suite" \
    -o APT::FTPArchive::Release::Codename="$suite" \
    -o APT::FTPArchive::Release::Architectures="${architectures[*]}" \
    -o APT::FTPArchive::Release::Components="$component" \
    -o APT::FTPArchive::Release::Description="$description" \
    release "dists/$suite" >"$repo_dir/.Release.tmp"
mv "$repo_dir/.Release.tmp" "dists/$suite/Release"

cat >5do.list <<EOF
deb [signed-by=/etc/apt/keyrings/5do-archive-keyring.gpg] $base_url $suite $component
EOF

if [[ "$unsigned" == "true" ]]; then
    echo "WARNING: building an UNSIGNED APT repository; APT will reject it unless" >&2
    echo "         the user passes [trusted=yes]." >&2
    if [[ "$publish_keyring" == "true" ]]; then
        echo "         No keyring is published for an unsigned repository." >&2
    fi
else
    gpg --batch --yes --local-user "$key_id" --clearsign \
        --output "dists/$suite/InRelease" "dists/$suite/Release"
    gpg --batch --yes --armor --local-user "$key_id" --detach-sign \
        --output "dists/$suite/Release.gpg" "dists/$suite/Release"
    gpg --batch --verify "dists/$suite/Release.gpg" "dists/$suite/Release" >/dev/null 2>&1
    printf 'Signed %s\n' "dists/$suite/InRelease"

    if [[ "$publish_keyring" == "true" ]]; then
        # 5do.list points at $base_url/5do-archive-keyring.gpg, so the public key has
        # to live at the repository root next to it.
        rm -f 5do-archive-keyring.gpg 5do-archive-keyring.gpg.asc
        gpg --batch --export "$key_id" >5do-archive-keyring.gpg
        gpg --batch --export --armor "$key_id" >5do-archive-keyring.gpg.asc
        gpg --batch --with-colons --fingerprint "$key_id" |
            awk -F: '/^fpr:/ { print $10; exit }' >5do-archive-keyring.gpg.fingerprint
        gpg --batch --show-keys --with-colons 5do-archive-keyring.gpg >/dev/null 2>&1 ||
            { echo "The published keyring is not a valid OpenPGP key." >&2; exit 1; }
        printf 'Published 5do-archive-keyring.gpg (fingerprint %s)\n' \
            "$(cat 5do-archive-keyring.gpg.fingerprint)"
    else
        echo "NOTE: --no-keyring is set; 5do.list expects" >&2
        echo "      $base_url/5do-archive-keyring.gpg to be published by your pipeline." >&2
    fi
fi

cat >README.md <<EOF
# 5DO APT repository

Packages: \`5do\` for amd64 and arm64. Signed with the 5DO release key; the keyring is
published as \`5do-archive-keyring.gpg\` next to this file.

\`\`\`sh
sudo install -d -m 0755 /etc/apt/keyrings
curl -fsSL $base_url/5do-archive-keyring.gpg \\
  | sudo tee /etc/apt/keyrings/5do-archive-keyring.gpg >/dev/null
curl -fsSL $base_url/5do.list \\
  | sudo tee /etc/apt/sources.list.d/5do.list >/dev/null
sudo apt update
sudo apt install 5do
\`\`\`

The keyring must be the one published by this repository. Verify the fingerprint
against the value in the project documentation before trusting it.
EOF

printf 'APT repository ready: %s\n' "$repo_dir"
