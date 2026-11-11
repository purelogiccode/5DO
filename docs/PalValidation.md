# PAL library validation

**PLAT-09, 2026-11-10.** PAL1/PAL2 output, 50 Hz frame pacing, and per-game PAL BIOS
selection were already implemented. This pass re-ran the Europe-only/PAL-sensitive
matrix titles with the documented European Anvil BIOS and explicit `--region pal1`
instead of relying on automatic selection or an NTSC release.

## Scope

The 378-title matrix uses the canonical US release where a title has one; EU-US titles
are therefore covered by their NTSC rows. The PAL-sensitive set is the Europe-only
matrix rows plus Battle Chess (Europe), whose PAL release is a separate dump:

| Title | Disc | Region |
|---|---|---|
| Battle Chess (Europe) | `Battle Chess (Europe).chd` | PAL-only |
| Defcon 5 | `Defcon 5 (1995)(GoldStar)(EU)[!].chd` | Europe |
| Panasonic PAL Sampler | `Panasonic REAL 3DO Interactive Multiplayer - Sampler CD for PAL System (1994)(Panasonic)(EU)[!].chd` | Europe |
| Powers Kingdom | `Powers Kingdom (1994)(Panasonic)(EU)[!].chd` | Europe |
| Remute 3D | `Remute 3D (Europe) (En,De) (Unl).chd` | Europe (unsigned homebrew) |
| Striker - World Cup Special | `Striker - World Cup Special (1995)(Panasonic)(EU)[!].chd` | Europe |
| True Golf Classics - Waialae Country Club | `True Golf Classics - Waialae Country Club (Germany).chd` | Europe |
| Zhadnost - The People's Party | `Zhadnost - The Peoples Party (1995)(Studio 3DO)(EU)[!].chd` | Europe |

## Method

The reproducible runner is [`tools/pal-validation.ps1`](https://github.com/purelogiccode/5DO/blob/master/tools/pal-validation.ps1):

```powershell
pwsh -NoProfile -File tools/pal-validation.ps1 `
  -Cli src/FiveDo.Cli/bin/Release/net11.0/FiveDo.Cli.dll `
  -OutputDirectory artifacts/pal-validation
```

Every run used:

- `--bios bios/panafz10e-anvil.bin` (MD5 `a48e6746bd7edec0f40cff078f0bb19f`)
- `--region pal1`
- `--audio null`
- `--nvram <title>/nvram.ram` with a fresh image on the first pass
- `--input-script benchmarks/diff/scripts/tierb-10min.txt`
- `--capture-dir <title>/captures --capture-frames 3000,6000,9000,12000`
- Battle Chess: 36,000 frames with captures at 600/3,000/9,000/18,000/30,000/36,000
- Remute 3D: `--allow-unsigned-discs`

## Results

All eight titles completed on 2026-11-10 with `region=Pal1`, `fieldRate=50`, the
European Anvil BIOS, and a rendered capture. The committed summary is
[`docs/data/pal-validation.csv`](data/pal-validation.csv); the per-run JSON and PNGs are
under `artifacts/pal-validation/<title>/`.

| Title | Frames | Output | Frame hash | Disc hash | Capture | Result |
|---|---:|---|---|---|---|---|
| Battle Chess (Europe) | 36,000 | 320x288 | `0xFED6B558` | `9bb853e8f611d60efdcd17969dec2730` | `captures/f036000.png` | Pass |
| Defcon 5 | 12,000 | 320x288 | `0xD104CFA8` | `c17409b3544768ce6bcaa77fe9a70928` | `captures/f012000.png` | Pass |
| Panasonic PAL Sampler | 12,000 | 384x288 | `0x5ADC825B` | `7c1353092dbd024c7ecc050d6f57d571` | `captures/f012000.png` | Pass |
| Powers Kingdom | 12,000 | 384x288 | `0x64D95FAA` | `14f7638d69ec532c2104e55c02ddc3d7` | `captures/f012000.png` | Pass |
| Remute 3D | 12,000 | 320x288 | `0x0CDEA189` | `3331db41068b10ed0d2c29b78717f7cb` | `captures/f012000.png` | Pass |
| Striker - World Cup Special | 12,000 | 320x288 | `0x26BB8706` | `a48456f4a1bdde641631709320814227` | `captures/f012000.png` | Pass |
| True Golf Classics - Waialae Country Club | 12,000 | 320x288 | `0x92A0F10D` | `b985cc21cf9e6abdcff8417648fbcf16` | `captures/f012000.png` | Pass |
| Zhadnost - The People's Party | 12,000 | 320x288 | `0x449D3BBD` | `d7801a0f20ac545249efbecde31b90cf` | `captures/f012000.png` | Pass |

The Battle Chess run reached the interactive board at 36,000 frames (the documented
30,000-frame checkpoint is included in the capture set). The PAL Sampler and Powers
Kingdom outputs are 384x288, confirming the PAL2-width path is exercised by real
content even under `--region pal1`. No failures required BIOS/dump/core triage.

A separate PAL2 probe ran Battle Chess with `--region pal2` for 600 frames: `ok: true`,
`fieldRate: 50`, **384x288**, frame hash `0x466A198C`, captured in
`artifacts/pal-validation/pal2-probe/`. It is a presentation check only, not a second
beyond-title pass.

## Triage rule

A title that does not render or does not advance is triaged in this order:

1. **BIOS**: rerun with the documented `panafz10e-anvil.bin` and explicit `--region pal1`.
2. **Dump**: compare the disc hash and try the alternate Europe dump.
3. **Core**: only after 1 and 2, compare the frame hash and input path with the NTSC
   reference or the PAL test ROM/timing tests.
