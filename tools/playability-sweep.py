#!/usr/bin/env python3
"""Run a 5DO-only playability pass for a Tier B title list.

This is deliberately not the differential harness: the first Tier B question is
"does 5DO reach and sustain gameplay?", and Opera is real-time paced. The script
runs one CLI process per title, captures a few checkpoints as PNGs, and records
the CLI JSON so a human can grade the title.

Only the Python standard library is required.
"""

from __future__ import annotations

import argparse
import csv
import json
import os
import re
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

DEFAULT_CLI = "src/FiveDo.Cli/bin/Release/net10.0/FiveDo.Cli.dll"
DEFAULT_OUT = "artifacts/tierb"


def expand(value: str, roots: dict[str, str]) -> str:
    for key, root in roots.items():
        value = value.replace("${" + key + "}", root)
    return value


def load_manifest(path: Path, only: set[str]) -> tuple[dict, list[dict]]:
    data = json.loads(path.read_text(encoding="utf-8"))
    defaults = data.get("defaults", {})
    roots = {key: str(value) for key, value in data.get("roots", {}).items()}
    if not roots:
        roots["root"] = str(Path(defaults.get("root", ".")))
    titles = []
    for title in data.get("titles", []):
        merged = {**defaults, **{key: value for key, value in title.items() if value is not None}}
        if only and merged["id"] not in only:
            continue
        titles.append(merged)
    return defaults, titles


def run_title(cli: list[str], title: dict, out_root: Path, roots: dict[str, str]) -> dict:
    title_id = title["id"]
    work = out_root / title_id
    work.mkdir(parents=True, exist_ok=True)
    result_path = work / "result.json"
    stderr_path = work / "stderr.txt"
    nvram = work / "nvram.ram"
    if nvram.exists():
        nvram.unlink()

    disc = expand(title["disc"], roots)
    bios = title.get("bios") or ""
    if bios and not Path(bios).is_absolute():
        bios = str(Path(bios).resolve())
    bios_auto = title.get("biosAuto") or ""
    if bios_auto and not Path(bios_auto).is_absolute():
        bios_auto = str(Path(bios_auto).resolve())
    script = title.get("inputScript") or ""
    if script and not Path(script).is_absolute():
        script = str(Path(script).resolve())
    captures = ",".join(str(frame) for frame in title.get("captureFrames", []))

    args = [*cli, "run", disc]
    if bios_auto:
        args += ["--bios-auto", bios_auto]
    elif bios:
        args += ["--bios", bios]
    args += [
        "--frames", str(title.get("frames", 3600)),
        "--audio", str(title.get("audio", "null")),
        "--json",
        "--nvram", str(nvram),
        "--capture-dir", str(work),
    ]
    if captures:
        args += ["--capture-frames", captures]
    if script:
        args += ["--input-script", script]
    if title.get("prefetch"):
        args += ["--prefetch"]
    if title.get("allowUnsigned"):
        args += ["--allow-unsigned-discs"]
    if title.get("inputDevice"):
        args += ["--input-device", str(title["inputDevice"])]
    if title.get("fixMode") is not None:
        args += ["--fix-mode", str(title["fixMode"])]

    started = time.perf_counter()
    try:
        completed = subprocess.run(
            args,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=int(title.get("timeoutSeconds", 900)),
            check=False,
        )
        elapsed = time.perf_counter() - started
        result_path.write_text(completed.stdout, encoding="utf-8")
        stderr_path.write_text(completed.stderr, encoding="utf-8")
        payload = json.loads(completed.stdout) if completed.stdout.strip() else {}
        status = "Completed" if completed.returncode == 0 and payload.get("ok") else "Error"
        record = {
            "id": title_id,
            "name": title.get("name", title_id),
            "disc": Path(disc).name,
            "bios": Path(bios).name if bios else "",
            "inputDevice": title.get("inputDevice") or "",
            "frames": title.get("frames", 3600),
            "elapsedSeconds": f"{elapsed:.2f}",
            "exitCode": completed.returncode,
            "status": status,
            "pc": payload.get("pc", ""),
            "instructions": payload.get("instructions", ""),
            "frameHash": payload.get("frameHash", ""),
            "nonBlack": payload.get("nonBlackPixels", ""),
            "colors": payload.get("distinctColors", ""),
            "error": payload.get("error", ""),
            "note": title.get("note", ""),
        }
    except subprocess.TimeoutExpired:
        result_path.write_text("", encoding="utf-8")
        stderr_path.write_text("timeout", encoding="utf-8")
        record = {
            "id": title_id,
            "name": title.get("name", title_id),
            "disc": Path(disc).name,
            "bios": Path(bios).name if bios else "",
            "inputDevice": title.get("inputDevice") or "",
            "frames": title.get("frames", 3600),
            "elapsedSeconds": "",
            "exitCode": "",
            "status": "Timeout",
            "pc": "",
            "instructions": "",
            "frameHash": "",
            "nonBlack": "",
            "colors": "",
            "error": "timeout",
            "note": title.get("note", ""),
        }
    return record


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True)
    parser.add_argument("--cli", default=DEFAULT_CLI)
    parser.add_argument("--out", default=DEFAULT_OUT)
    parser.add_argument("--jobs", type=int, default=3)
    parser.add_argument("--only", default="", help="Comma-separated title IDs.")
    args = parser.parse_args()

    manifest = Path(args.manifest)
    only = {item.strip() for item in args.only.split(",") if item.strip()}
    _defaults, titles = load_manifest(manifest, only)
    if not titles:
        print("no titles selected", file=sys.stderr)
        return 2

    cli = [args.cli] if args.cli.lower().endswith(".exe") else ["dotnet", args.cli]
    manifest_data = json.loads(manifest.read_text(encoding="utf-8"))
    roots = {key: str(value) for key, value in manifest_data.get("roots", {}).items()}
    if not roots and manifest_data.get("defaults", {}).get("root"):
        roots = {"root": str(manifest_data["defaults"]["root"])}

    out_root = Path(args.out)
    out_root.mkdir(parents=True, exist_ok=True)
    print(f"running {len(titles)} Tier B titles with {args.jobs} job(s)", file=sys.stderr)

    records = []
    with ThreadPoolExecutor(max_workers=max(1, args.jobs)) as pool:
        for index, record in enumerate(pool.map(lambda t: run_title(cli, t, out_root, roots), titles), start=1):
            records.append(record)
            print(f"  [{index}/{len(titles)}] {record['id']}: {record['status']} ({record['elapsedSeconds']}s)", file=sys.stderr)

    fields = [
        "id", "name", "disc", "bios", "inputDevice", "frames", "elapsedSeconds",
        "exitCode", "status", "pc", "instructions", "frameHash", "nonBlack", "colors",
        "error", "note",
    ]
    with (out_root / "summary.csv").open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        for record in records:
            writer.writerow(record)

    completed = sum(1 for record in records if record["status"] == "Completed")
    print(f"wrote {out_root / 'summary.csv'}: {completed}/{len(records)} completed")
    return 0 if completed == len(records) else 1


if __name__ == "__main__":
    raise SystemExit(main())
