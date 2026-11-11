#!/usr/bin/env python3
"""Classify unique 3DO titles with the beyond-title playable rule.

The runner executes one deterministic 5DO CLI process per canonical disc. It
captures boot, title, and later screens, then repeats the first minutes without
the input script to prove that scripted input changes the machine trajectory.

A title is ``Playable`` when it renders a non-black frame, scripted input
changes the emulated trajectory (or later captures show auto-advancing content),
and a distinct screen beyond the title is reached. A title that never leaves a
static title/attract screen is ``Title-only``. A title with no usable frame is
``No video``; a CLI failure or timeout is ``Not loadable``.

Outputs:

* ``artifacts/beyond-title/<id>/`` - captures, CLI JSON, control JSON, stderr;
* ``artifacts/beyond-title/summary.csv`` - one row per tested title;
* optional committed ledger (``--ledger``) consumed by the comparison tool.

Only the Python standard library is required.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import re
import struct
import subprocess
import sys
import time
import zlib
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

DEFAULT_CLI = "src/FiveDo.Cli/bin/Release/net11.0/FiveDo.Cli.dll"
DEFAULT_COMPARISON = "docs/data/compatibility-comparison.json"
DEFAULT_SCRIPT = "benchmarks/diff/scripts/tierb-10min.txt"
DEFAULT_OUT = "artifacts/beyond-title"
DEFAULT_LEDGER = "docs/data/beyond-title-results.csv"

CAPTURE_FRAMES = (120, 600, 1200, 2400, 3600, 4800, 6000, 7200, 8400, 9600, 10800, 12000)
CONTROL_FRAMES = 3600
CONTROL_CAPTURES = (120, 600, 1800, 3600)
CHECKPOINTS = (120, 600, 1800, 3600, 7200, 12000)

PLAYABLE = "Playable"
TITLE_ONLY = "Title-only"
NO_VIDEO = "No video"
NOT_LOADABLE = "Not loadable"


def slug(value: str) -> str:
    value = re.sub(r"\[[^\]]*\]", " ", value or "")
    value = re.sub(r"\([^)]*\)", " ", value)
    value = re.sub(r"[^A-Za-z0-9]+", "-", value).strip("-").lower()
    return value[:80] or "title"


# ---------------------------------------------------------------------------
# PNG analysis (PngDumper writes 8-bit RGB, filter 0, non-interlaced)


class Image:
    __slots__ = ("width", "height", "pixels")

    def __init__(self, width: int, height: int, pixels: bytes) -> None:
        self.width = width
        self.height = height
        self.pixels = pixels


def read_png(path: Path) -> Image | None:
    try:
        data = path.read_bytes()
    except OSError:
        return None
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        return None
    offset = 8
    width = height = bit_depth = color_type = interlace = 0
    idat = bytearray()
    while offset + 8 <= len(data):
        length = struct.unpack(">I", data[offset : offset + 4])[0]
        kind = data[offset + 4 : offset + 8]
        payload = data[offset + 8 : offset + 8 + length]
        offset += 12 + length
        if kind == b"IHDR":
            width, height, bit_depth, color_type, _, _, interlace = struct.unpack(
                ">IIBBBBB", payload
            )
        elif kind == b"IDAT":
            idat.extend(payload)
        elif kind == b"IEND":
            break
    if width <= 0 or height <= 0 or bit_depth != 8 or color_type != 2 or interlace != 0:
        return None
    try:
        raw = zlib.decompress(bytes(idat))
    except zlib.error:
        return None
    stride = width * 3
    rows = []
    previous = bytearray(stride)
    position = 0
    for _ in range(height):
        if position >= len(raw):
            return None
        filter_type = raw[position]
        position += 1
        line = bytearray(raw[position : position + stride])
        position += stride
        if filter_type == 1:
            for index in range(3, stride):
                line[index] = (line[index] + line[index - 3]) & 0xFF
        elif filter_type == 2:
            for index in range(stride):
                line[index] = (line[index] + previous[index]) & 0xFF
        elif filter_type == 3:
            for index in range(stride):
                left = line[index - 3] if index >= 3 else 0
                line[index] = (line[index] + ((left + previous[index]) >> 1)) & 0xFF
        elif filter_type == 4:
            for index in range(stride):
                left = line[index - 3] if index >= 3 else 0
                upper = previous[index]
                upper_left = previous[index - 3] if index >= 3 else 0
                estimate = left + upper - upper_left
                pa = abs(estimate - left)
                pb = abs(estimate - upper)
                pc = abs(estimate - upper_left)
                predictor = left if pa <= pb and pa <= pc else (upper if pb <= pc else upper_left)
                line[index] = (line[index] + predictor) & 0xFF
        elif filter_type != 0:
            return None
        rows.append(bytes(line))
        previous = line
    return Image(width, height, b"".join(rows))


def luma(r: int, g: int, b: int) -> int:
    return (r * 299 + g * 587 + b * 114) // 1000


def image_metrics(image: Image) -> dict[str, float]:
    pixels = image.pixels
    width = image.width
    height = image.height
    count = width * height
    non_black = 0
    colored = 0
    colors: set[int] = set()
    luma_values = []
    edges = 0
    edge_checks = 0
    for y in range(height):
        row = y * width * 3
        previous_luma = None
        for x in range(width):
            offset = row + (x * 3)
            r = pixels[offset]
            g = pixels[offset + 1]
            b = pixels[offset + 2]
            if (r | g | b) > 16:
                non_black += 1
            high = max(r, g, b)
            low = min(r, g, b)
            if high > 40 and high - low > 40:
                colored += 1
            colors.add(((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3))
            value = luma(r, g, b)
            luma_values.append(value)
            if previous_luma is not None:
                edge_checks += 1
                if abs(value - previous_luma) > 24:
                    edges += 1
            previous_luma = value
    mean = sum(luma_values) / count
    variance = sum((value - mean) ** 2 for value in luma_values) / count
    return {
        "nonBlack": non_black / count,
        "colored": colored / count,
        "colors": len(colors),
        "edges": edges / max(1, edge_checks),
        "lumaMean": mean,
        "lumaStd": variance ** 0.5,
    }


def dhash(image: Image) -> int:
    """64-bit difference hash from a 9x8 luma downsample."""
    width = image.width
    height = image.height
    pixels = image.pixels
    values = []
    for gy in range(8):
        y0 = (gy * height) // 8
        y1 = max(y0 + 1, ((gy + 1) * height) // 8)
        for gx in range(9):
            x0 = (gx * width) // 9
            x1 = max(x0 + 1, ((gx + 1) * width) // 9)
            total = 0
            samples = 0
            for y in range(y0, y1):
                row = y * width * 3
                for x in range(x0, x1):
                    offset = row + x * 3
                    total += luma(pixels[offset], pixels[offset + 1], pixels[offset + 2])
                    samples += 1
            values.append(total // max(1, samples))
    bits = 0
    index = 0
    for gy in range(8):
        for gx in range(8):
            bits |= (1 if values[gy * 9 + gx] > values[gy * 9 + gx + 1] else 0) << index
            index += 1
    return bits


def hamming(a: int, b: int) -> int:
    return (a ^ b).bit_count()


def pixel_similarity(a: Image, b: Image) -> float:
    if a.width != b.width or a.height != b.height:
        return 0.0
    total = 0
    same = 0
    step = max(1, (a.width * a.height) // 12000)
    pixels_a = a.pixels
    pixels_b = b.pixels
    count = a.width * a.height
    for index in range(0, count, step):
        offset = index * 3
        total += 1
        if (
            abs(pixels_a[offset] - pixels_b[offset])
            + abs(pixels_a[offset + 1] - pixels_b[offset + 1])
            + abs(pixels_a[offset + 2] - pixels_b[offset + 2])
            <= 24
        ):
            same += 1
    return same / max(1, total)


def content_like(metrics: dict[str, float]) -> bool:
    return (
        metrics["nonBlack"] >= 0.05
        and metrics["colors"] >= 24
        and (metrics["edges"] >= 0.012 or metrics["lumaStd"] >= 16.0)
    )


def analyze_captures(work: Path, frames: list[int]) -> dict[int, dict]:
    captures: dict[int, dict] = {}
    for frame in frames:
        path = work / f"f{frame:06d}.png"
        image = read_png(path)
        if image is None:
            continue
        metrics = image_metrics(image)
        captures[frame] = {
            "frame": frame,
            "path": str(path),
            "hash": dhash(image),
            "metrics": metrics,
            "image": image,
        }
    return captures


# ---------------------------------------------------------------------------
# CLI execution


def cli_command(cli: str) -> list[str]:
    return [cli] if cli.lower().endswith(".exe") else ["dotnet", cli]


def run_cli(
    cli: list[str],
    disc: str,
    work: Path,
    frames: int,
    captures: tuple[int, ...],
    checkpoints: tuple[int, ...],
    script: str | None,
    bios_auto: str,
    bios: str | None,
    bios2: str | None,
    input_device: str | None,
    fix_mode: str | None,
    allow_unsigned: bool,
    audio: str,
    result_name: str,
    control: bool = False,
) -> tuple[dict, int, str, float]:
    result_path = work / result_name
    stderr_path = work / (result_name.replace(".json", ".stderr.txt"))
    capture_dir = work / ("control-captures" if control else "captures")
    capture_dir.mkdir(parents=True, exist_ok=True)
    nvram = work / ("control.nvram.ram" if control else "input.nvram.ram")
    if nvram.exists():
        nvram.unlink()
    args = [*cli, "run", disc]
    if bios:
        args += ["--bios", bios]
    else:
        args += ["--bios-auto", bios_auto]
    if bios2:
        args += ["--bios2", bios2]
    args += [
        "--frames",
        str(frames),
        "--audio",
        audio,
        "--json",
        "--nvram",
        str(nvram),
        "--capture-dir",
        str(capture_dir),
        "--capture-frames",
        ",".join(str(frame) for frame in captures),
        "--checkpoints",
        ",".join(str(frame) for frame in checkpoints),
    ]
    if script and not control:
        args += ["--input-script", script]
    if input_device:
        args += ["--input-device", input_device]
    if fix_mode:
        args += ["--fix-mode", fix_mode]
    if allow_unsigned:
        args += ["--allow-unsigned-discs"]

    started = time.perf_counter()
    try:
        completed = subprocess.run(
            args,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=max(180, int(frames / 40)),
            check=False,
        )
        elapsed = time.perf_counter() - started
        stderr_path.write_text(completed.stderr, encoding="utf-8")
        result_path.write_text(completed.stdout, encoding="utf-8")
        payload = json.loads(completed.stdout) if completed.stdout.strip() else {}
        return payload, completed.returncode, completed.stderr, elapsed
    except subprocess.TimeoutExpired:
        stderr_path.write_text("timeout", encoding="utf-8")
        result_path.write_text("", encoding="utf-8")
        return {"error": "timeout"}, -1, "timeout", time.perf_counter() - started


def checkpoint_hashes(payload: dict) -> dict[int, int]:
    values = {}
    for checkpoint in payload.get("checkpoints") or []:
        frame = checkpoint.get("frame")
        value = checkpoint.get("frameHash")
        if isinstance(frame, int) and isinstance(value, int):
            values[frame] = value
    return values


def classify(
    input_payload: dict,
    control_payload: dict,
    input_captures: dict[int, dict],
    control_captures: dict[int, dict],
) -> tuple[str, str, bool, dict]:
    """Return (status, reason, review, signals)."""
    if not input_payload or input_payload.get("error") or not input_payload.get("ok"):
        return NOT_LOADABLE, str(input_payload.get("error") or "CLI did not complete"), True, {}
    if not input_captures:
        return NOT_LOADABLE, "no captures", True, {}

    frames = sorted(input_captures)
    metrics = {frame: input_captures[frame]["metrics"] for frame in frames}
    max_non_black = max(item["nonBlack"] for item in metrics.values())
    max_colors = max(item["colors"] for item in metrics.values())
    if max_non_black < 0.005 or max_colors <= 2:
        return NO_VIDEO, "all captures are black or nearly blank", False, {
            "maxNonBlack": round(max_non_black, 4),
            "maxColors": max_colors,
        }

    title_frame = 600 if 600 in input_captures else frames[0]
    for frame in frames:
        if frame < 600:
            continue
        item = input_captures[frame]["metrics"]
        if item["nonBlack"] >= 0.02 and item["colors"] >= 8:
            title_frame = frame
            break
    title_hash = input_captures[title_frame]["hash"]
    title_image = input_captures[title_frame]["image"]
    late_frames = [frame for frame in frames if frame >= 3600] or [frame for frame in frames if frame > title_frame]
    if not late_frames:
        return TITLE_ONLY, "only boot/title captures", True, {}

    late_hashes = [input_captures[frame]["hash"] for frame in late_frames]
    distinct_from_title = max((hamming(title_hash, value) for value in late_hashes), default=0)
    title_similarity = max(
        (
            pixel_similarity(title_image, input_captures[frame]["image"])
            for frame in late_frames
        ),
        default=1.0,
    )
    unique_late = 1
    for index in range(1, len(late_hashes)):
        if hamming(late_hashes[index], late_hashes[index - 1]) > 8:
            unique_late += 1
    content_frames = [
        frame
        for frame in late_frames
        if content_like(metrics[frame])
    ]

    # Scripted-input effect: exact checkpoint hashes first, then capture hashes.
    input_hashes = checkpoint_hashes(input_payload)
    control_hashes = checkpoint_hashes(control_payload)
    common = sorted(set(input_hashes) & set(control_hashes))
    hash_effect = any(
        frame >= 600 and input_hashes[frame] != control_hashes[frame] for frame in common
    )
    visual_effect = False
    for frame in sorted(set(input_captures) & set(control_captures)):
        if frame < 600:
            continue
        distance = hamming(input_captures[frame]["hash"], control_captures[frame]["hash"])
        if distance > 10:
            visual_effect = True
            break
    input_effect = hash_effect or visual_effect

    signals = {
        "titleFrame": title_frame,
        "lateFrames": late_frames,
        "distinctFromTitle": distinct_from_title,
        "titlePixelSimilarity": round(title_similarity, 3),
        "uniqueLate": unique_late,
        "contentFrames": content_frames,
        "inputEffect": input_effect,
        "hashEffect": hash_effect,
        "visualEffect": visual_effect,
        "maxNonBlack": round(max_non_black, 4),
        "maxColors": max_colors,
    }

    if input_effect:
        first_content = next(
            (
                frame
                for frame in frames
                if frame >= 600
                and metrics[frame]["nonBlack"] >= 0.02
                and metrics[frame]["colors"] >= 8
            ),
            None,
        )
        if (
            first_content == title_frame
            and content_like(metrics[title_frame])
        ):
            return PLAYABLE, "the first rendered content screen accepts scripted input", False, signals
        if distinct_from_title > 10 or title_similarity < 0.90 or unique_late >= 2:
            return PLAYABLE, "scripted input changes the machine and a distinct beyond-title screen is captured", False, signals
        return TITLE_ONLY, "scripted input only changes the title screen; no distinct screen follows", True, signals

    if unique_late <= 2:
        if (distinct_from_title > 12 or title_similarity < 0.90) and content_frames:
            return PLAYABLE, "auto-advances from the title to a distinct content screen", True, signals
        return TITLE_ONLY, "no scripted-input effect; title/attract screen is static or looping", False, signals

    if content_frames and unique_late >= 3:
        return PLAYABLE, "content advances through several screens beyond the title without input", False, signals

    return TITLE_ONLY, "no distinct beyond-title screen detected", True, signals


# ---------------------------------------------------------------------------
# Selection and orchestration


def load_selection(
    comparison_path: Path, scope: str, only: set[str], limit: int
) -> list[dict]:
    data = json.loads(comparison_path.read_text(encoding="utf-8"))
    rows = [row for row in data.get("titles", []) if row.get("kind") in ("game", "homebrew")]
    if only:
        rows = [row for row in rows if row.get("base") in only or row.get("title") in only]
    elif scope == "peer-gap":
        rows = [
            row
            for row in rows
            if not row.get("playable")
            and (row.get("fourDo") in ("Good", "Playable") or row.get("phoenix") == "Good")
        ]
    elif scope == "unverified":
        rows = [row for row in rows if not row.get("playable")]
    elif scope != "all":
        raise SystemExit(f"unknown scope: {scope}")
    rows.sort(key=lambda row: row.get("base", ""))
    if limit:
        rows = rows[:limit]
    return rows


def title_id(row: dict) -> str:
    return slug(row.get("base") or row.get("title") or Path(row.get("canonical", "")).stem)


def load_overrides(path: Path | None) -> dict[str, dict]:
    if path is None or not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))


def ledger_files(path: Path | None) -> set[str]:
    if path is None or not path.exists():
        return set()
    done: set[str] = set()
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            name = (row.get("file") or "").strip().casefold()
            status = (row.get("manualStatus") or row.get("status") or "").strip()
            if name and status:
                done.add(name)
    return done


def process(
    row: dict,
    cli: list[str],
    out_root: Path,
    script: str,
    bios_auto: str,
    overrides: dict[str, dict],
    deep_control: bool = False,
) -> dict:
    title = row.get("title") or row.get("base") or row.get("canonical")
    identifier = title_id(row)
    override = overrides.get(row.get("base") or "", {})
    work = out_root / identifier
    work.mkdir(parents=True, exist_ok=True)
    disc = override.get("disc") or row.get("canonicalPath") or ""
    frames = int(override.get("frames", CAPTURE_FRAMES[-1]))
    captures = tuple(override.get("captureFrames") or [f for f in CAPTURE_FRAMES if f <= frames])
    checkpoints = tuple(override.get("checkpoints") or [f for f in CHECKPOINTS if f <= frames])
    title_script = override.get("inputScript") or script
    bios = override.get("bios")
    bios2 = override.get("bios2")
    bios_auto_value = override.get("biosAuto") or bios_auto
    input_device = override.get("inputDevice")
    fix_mode = override.get("fixMode")
    allow_unsigned = bool(override.get("allowUnsigned", False))
    audio = override.get("audio", "null")

    if not disc:
        return {
            "file": row.get("canonical", ""),
            "title": title,
            "status": NOT_LOADABLE,
            "failureReason": "no canonical disc path",
            "review": "yes",
            "signals": "{}",
        }

    input_payload, exit_code, stderr, elapsed = run_cli(
        cli,
        disc,
        work,
        frames,
        captures,
        checkpoints,
        str(Path(title_script).resolve()) if title_script else None,
        bios_auto_value,
        bios,
        bios2,
        input_device,
        fix_mode,
        allow_unsigned,
        audio,
        "input.json",
    )
    control_frames = min(frames, CONTROL_FRAMES) if not deep_control else frames
    control_captures = tuple(
        frame
        for frame in (captures if deep_control else CONTROL_CAPTURES)
        if frame <= control_frames
    )
    control_checkpoints = tuple(
        frame
        for frame in (checkpoints if deep_control else CHECKPOINTS)
        if frame <= control_frames
    )
    control_payload, control_exit, control_stderr, control_elapsed = run_cli(
        cli,
        disc,
        work,
        control_frames,
        control_captures,
        control_checkpoints,
        None,
        bios_auto_value,
        bios,
        bios2,
        input_device,
        fix_mode,
        allow_unsigned,
        audio,
        "control.json",
        control=True,
    )

    input_captures = analyze_captures(work / "captures", list(captures))
    control_captures = analyze_captures(work / "control-captures", list(control_captures))
    status, reason, review, signals = classify(
        input_payload, control_payload, input_captures, control_captures
    )

    screenshot_frame = max(input_captures) if input_captures else 0
    screenshot = (
        str(input_captures[screenshot_frame]["path"])
        if screenshot_frame in input_captures
        else ""
    )
    bios_path = input_payload.get("biosPath") or ""
    bios_name = input_payload.get("biosName") or (Path(bios_path).name if bios_path else "")
    reproduction = [
        "dotnet",
        "src/FiveDo.Cli/bin/Release/net11.0/FiveDo.Cli.dll",
        "run",
        f'"{disc}"',
    ]
    reproduction += (
        ["--bios", f'"{bios}"'] if bios else ["--bios-auto", bios_auto_value]
    )
    if bios2:
        reproduction += ["--bios2", f'"{bios2}"']
    reproduction += ["--frames", str(frames), "--audio", audio, "--json"]
    if title_script:
        reproduction += ["--input-script", f'"{title_script}"']
    if input_device:
        reproduction += ["--input-device", f'"{input_device}"']
    if fix_mode:
        reproduction += ["--fix-mode", str(fix_mode)]
    if allow_unsigned:
        reproduction += ["--allow-unsigned-discs"]
    record = {
        "file": row.get("canonical", ""),
        "title": title,
        "kind": row.get("kind", ""),
        "status": status,
        "date": time.strftime("%Y-%m-%d"),
        "bios": bios_name,
        "bios2": Path(input_payload.get("bios2Path", "")).name if input_payload.get("bios2Path") else "",
        "biosSelection": input_payload.get("biosSelection", ""),
        "discHash": input_payload.get("discHash", ""),
        "gameId": input_payload.get("gameId", ""),
        "frame": screenshot_frame,
        "frameCount": frames,
        "screenshot": screenshot,
        "reproduction": " ".join(reproduction),
        "failureReason": "" if status == PLAYABLE else reason,
        "review": "yes" if review else "no",
        "signals": json.dumps(signals, sort_keys=True),
        "inputScript": str(title_script or ""),
        "inputDevice": input_device or input_payload.get("inputDevices", ""),
        "elapsedSeconds": f"{elapsed:.2f}",
        "controlSeconds": f"{control_elapsed:.2f}",
        "exitCode": exit_code,
        "controlExitCode": control_exit,
        "path": disc,
        "notes": reason if status != PLAYABLE else "input and control passes agree",
    }
    (work / "summary.json").write_text(
        json.dumps({"record": record, "signals": signals}, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    return record


def safe_process(
    row: dict,
    cli: list[str],
    out_root: Path,
    script: str,
    bios_auto: str,
    overrides: dict[str, dict],
    deep_control: bool = False,
) -> dict:
    try:
        return process(row, cli, out_root, script, bios_auto, overrides, deep_control)
    except Exception as error:  # noqa: BLE001 - keep the sweep alive on one bad title
        title = row.get("title") or row.get("base") or ""
        return {
            "file": row.get("canonical", ""),
            "title": title,
            "kind": row.get("kind", ""),
            "status": NOT_LOADABLE,
            "date": time.strftime("%Y-%m-%d"),
            "failureReason": f"runner exception: {error}",
            "review": "yes",
            "signals": "{}",
            "path": row.get("canonicalPath", ""),
            "notes": f"runner exception: {error}",
            "elapsedSeconds": "0",
            "controlSeconds": "0",
        }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--comparison", default=DEFAULT_COMPARISON)
    parser.add_argument("--cli", default=DEFAULT_CLI)
    parser.add_argument("--script", default=DEFAULT_SCRIPT)
    parser.add_argument("--bios-auto", default="bios")
    parser.add_argument("--out", default=DEFAULT_OUT)
    parser.add_argument("--jobs", type=int, default=10)
    parser.add_argument(
        "--scope",
        default="peer-gap",
        choices=("peer-gap", "unverified", "all"),
        help="peer-gap: peer-playable 5DO-unverified; unverified: every non-playable; all: the 378-game set.",
    )
    parser.add_argument("--only", default="", help="Comma-separated title bases or display names.")
    parser.add_argument("--limit", type=int, default=0)
    parser.add_argument(
        "--deep-control",
        action="store_true",
        help="Run the no-input control for the full frame count so late input effects are detected.",
    )
    parser.add_argument(
        "--skip-existing",
        action="store_true",
        help="Skip canonical discs that already have a status in the ledger.",
    )
    parser.add_argument("--overrides", default="benchmarks/beyond-title/overrides.json")
    parser.add_argument(
        "--ledger",
        default="",
        help="Merge results into this committed CSV ledger (for example docs/data/beyond-title-results.csv).",
    )
    args = parser.parse_args()

    only = {item.strip() for item in args.only.split(",") if item.strip()}
    rows = load_selection(Path(args.comparison), args.scope, only, args.limit)
    if args.skip_existing:
        done = ledger_files(Path(args.ledger) if args.ledger else None)
        rows = [row for row in rows if (row.get("canonical") or "").casefold() not in done]
    if not rows:
        print("no titles selected", file=sys.stderr)
        return 2
    overrides_path = Path(args.overrides) if args.overrides else None
    overrides = load_overrides(overrides_path)
    cli = cli_command(args.cli)
    out_root = Path(args.out)
    out_root.mkdir(parents=True, exist_ok=True)
    print(f"testing {len(rows)} title(s) with {args.jobs} job(s)", file=sys.stderr)

    records = []
    with ThreadPoolExecutor(max_workers=max(1, args.jobs)) as pool:
        for index, record in enumerate(
            pool.map(
                lambda row: safe_process(
                    row,
                    cli,
                    out_root,
                    args.script,
                    args.bios_auto,
                    overrides,
                    args.deep_control,
                ),
                rows,
            ),
            start=1,
        ):
            records.append(record)
            print(
                f"  [{index}/{len(rows)}] {record['title']}: {record['status']}"
                f" ({record['elapsedSeconds']}s)"
                + (" REVIEW" if record.get("review") == "yes" else ""),
                file=sys.stderr,
            )

    fields = [
        "file",
        "title",
        "kind",
        "status",
        "date",
        "bios",
        "bios2",
        "biosSelection",
        "discHash",
        "gameId",
        "frame",
        "frameCount",
        "screenshot",
        "reproduction",
        "failureReason",
        "review",
        "signals",
        "manualStatus",
        "peerInvalid4do",
        "peerInvalidPhoenix",
        "peerEvidence",
        "inputScript",
        "inputDevice",
        "elapsedSeconds",
        "controlSeconds",
        "exitCode",
        "controlExitCode",
        "path",
        "notes",
    ]
    summary_path = out_root / "summary.csv"
    with summary_path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        for record in records:
            writer.writerow(record)

    counts = Counter(record["status"] for record in records)
    reviews = sum(1 for record in records if record.get("review") == "yes")
    print(
        f"wrote {summary_path}: "
        + ", ".join(f"{key} {value}" for key, value in sorted(counts.items()))
        + f", review {reviews}"
    )

    if args.ledger:
        ledger_path = Path(args.ledger)
        existing: dict[str, dict] = {}
        if ledger_path.exists():
            with ledger_path.open("r", encoding="utf-8-sig", newline="") as stream:
                for row in csv.DictReader(stream):
                    name = (row.get("file") or "").strip().casefold()
                    if name:
                        existing[name] = row
        for record in records:
            key = record["file"].casefold()
            previous = existing.get(key, {})
            for column in ("manualStatus", "peerInvalid4do", "peerInvalidPhoenix", "peerEvidence"):
                if previous.get(column) and not record.get(column):
                    record[column] = previous[column]
            if record.get("manualStatus"):
                record["status"] = record["manualStatus"]
            existing[key] = record
        ordered = sorted(existing.values(), key=lambda row: (row.get("file") or "").casefold())
        with ledger_path.open("w", encoding="utf-8-sig", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=fields, extrasaction="ignore")
            writer.writeheader()
            for row in ordered:
                writer.writerow(row)
        print(f"merged {len(records)} row(s) into {ledger_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
