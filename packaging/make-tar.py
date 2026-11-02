#!/usr/bin/env python3
"""Create a deterministic .tar.gz for a 5DO bundle with sane Unix modes.

The Windows tar implementation does not preserve the executable bit, so the
release bundles are created with Python on every platform. Apphosts, shared
libraries, and the Linux desktop entry are marked executable; everything else
is 0644.
"""

from __future__ import annotations

import os
import sys
import tarfile
import time

EXECUTABLES = {"FiveDo.Cli", "FiveDo.Frontend.Avalonia", "5do.desktop"}


def mode_for(name: str) -> int:
    if name in EXECUTABLES:
        return 0o755
    if name.endswith((".so", ".dylib")) or ".so." in name:
        return 0o755
    return 0o644


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: make-tar.py <source-directory> <archive.tar.gz>", file=sys.stderr)
        return 2

    source = os.path.abspath(sys.argv[1])
    destination = os.path.abspath(sys.argv[2])
    if not os.path.isdir(source):
        print(f"source directory not found: {source}", file=sys.stderr)
        return 2

    mtime = int(os.environ.get("SOURCE_DATE_EPOCH", str(int(time.time()))))

    with tarfile.open(destination, "w:gz", format=tarfile.PAX_FORMAT) as archive:
        for root, directories, files in os.walk(source):
            directories.sort()
            files.sort()

            for name in directories:
                path = os.path.join(root, name)
                info = archive.gettarinfo(path, arcname=os.path.relpath(path, source))
                info.mode = 0o755
                info.uid = info.gid = 0
                info.uname = info.gname = "root"
                info.mtime = mtime
                archive.addfile(info)

            for name in files:
                path = os.path.join(root, name)
                info = archive.gettarinfo(path, arcname=os.path.relpath(path, source))
                info.uid = info.gid = 0
                info.uname = info.gname = "root"
                info.mtime = mtime
                if info.issym() or info.islnk():
                    archive.addfile(info)
                    continue
                info.mode = mode_for(name)
                with open(path, "rb") as handle:
                    archive.addfile(info, handle)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
