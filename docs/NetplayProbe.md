# Netplay Loopback Probe (NET-01)

**Status:** probe complete (2026-11-26); implementation remains open for host/join,
initial state sync, reconnect, and the desktop/CLI surface. This page records the design,
measurements, and the decision to use delay-based lockstep for the first direct-IP
increment.

## Scope

The probe answers four questions before committing to a netplay implementation:

1. Do two independently constructed machines produce identical frame and audio hashes
   from the same disc, BIOS, tuning, and scripted input?
2. Can a versioned input/hash protocol keep two machine instances in sync over UDP
   loopback for a 10-minute emulated session?
3. How much input delay is needed for loopback, simulated 50 ms one-way latency, and
   packet loss?
4. Is rollback viable on the existing state snapshot, and how large is the state used for
   join/reconnect sync?

The probe is intentionally developer-only. It uses scripted input, has no lobby, no NAT
traversal, no authentication, and no desktop UI.

## Implementation

- `NetplayCodec` (`src/FiveDo.Host/Netplay/NetplayProtocol.cs`) defines a versioned
  little-endian datagram format: hello/hello-ack, redundant input history, frame/audio
  hash checkpoints, input requests, and close.
- `UdpNetplayTransport` sends direct UDP datagrams. The host learns the join endpoint from
  the first hello; the join starts with the host endpoint.
- `LockstepSession` samples local input `InputDelayFrames` ahead, sends the newest eight
  samples in every input packet, waits for the peer's input for the current frame, and
  publishes a frame hash plus cumulative PCM hash after each frame.
- Missing input frames are recovered in two ways: the eight-entry redundancy window heals
  short loss, and an explicit input request triggers a resend when a frame is still
  missing. The sender also keeps pumping during a two-second graceful close, so the last
  frame can be recovered after one peer has already finished.
- `NetplayContentHash` covers both BIOS images, the disc identity (`GameId`), and every
  behavior-affecting tuning value. A mismatch is rejected during the handshake.
- `NetplayMachinePeer` binds a session to a `FiveDoMachine`, combines the two controller
  states into one PBUS packet, and hashes the assembled frame with a reusable
  `FrameBuffer`.
- `tools/FiveDo.NetplayProbe` runs two machines and two UDP sockets in one process, plus a
  single-machine reference run with both scripted players.

## Determinism baseline

Two CLI runs of Wolfenstein 3D (USA) with the same BIOS and no input matched at every
checkpoint:

| Frame | Frame hash | PC |
|---|---|---|
| 60 | `0x110E7DC5` | `0x00005954` |
| 600 | `0x110E7DC5` | `0x00059960` |
| 1,800 | `0x66068F5C` | `0x000B2104` |
| 3,600 | `0x2460BF33` | `0x000118DC` |

Two runs with `benchmarks/diff/scripts/tierb-10min.txt` also matched frame, PC, and audio
hashes at 60/600/1,200/1,800/3,600. The script changes the trajectory (`f=3,600` hash
`0xF5D013D6`), so the probe is not measuring a static screen.

## Probe results

**36,000-frame loopback session (10 minutes emulated), delay 4, no simulated loss:**

- Host and join: **36,000 frames each**, `Completed`, **0 desyncs**, **0 host/join
  checkpoint mismatches**.
- Throughput: **240.7 fps** per peer (149.53 s), 254.4 fps reference; wall 149.56 s.
- Traffic: 72,003 input/hash/request packets per peer, **10,080,066 bytes** sent and
  received per peer over 10 minutes, about **1.17 KB/s**.
- Waits: max 27.73 ms, total 2.77 s (host); max 16.51 ms, total 0.48 s (join).
- Redundancy: 208,479/167,026 historical entries and 43,515/84,968 duplicates ignored;
  0 explicit resends needed.
- Reference final frame at 35,999: `0xBA80C0E9`; every sampled checkpoint matched the
  two netplay peers exactly.

**600 frames with 50 ms simulated one-way latency, delay 8:** both peers completed at
128.3 fps with 0 mismatches and 0 desyncs. The final reference hash was `0x110E7DC5`, and
the peers spent 5.8 ms per frame waiting.

**600 frames with every third input datagram dropped, delay 4:** both peers completed at
389 fps with 0 mismatches and 0 desyncs; the eight-entry window recovered the loss without
an explicit request.

**State and reconnect cost (60 Hz budget is 16.67 ms):**

| Metric | Value |
|---|---|
| Uncompressed snapshot | 9,348,653 bytes |
| Snapshot capture | 5.06 ms |
| Snapshot restore | 2.31 ms |
| Compressed state | 649,639 bytes |
| Compressed save | 38 ms |
| Compressed load | 24 ms |

## Peer reference comparison

The mupen64plus netplay source in `References/RMG-master` was reviewed as the closest
available peer implementation:

- It uses **TCP for registration, settings, and save-state sync** and **UDP for per-frame
  input and sync checks**. The probe uses UDP for everything to keep the bounded test
  small; the next increment should move the handshake and the 650 KB initial state to a
  reliable stream instead of fragmenting it over UDP.
- It relays input through a server and identifies events by a monotonic input count. The
  first 5DO increment is two-player direct IP, so the probe exchanges input peer-to-peer
  by frame number; a relay or full mesh would be needed for more than two players.
- It reports a server-assigned buffer target and per-player lag, and temporarily removes
  the speed limiter when the local buffer exceeds the target to catch up. 5DO's frame is
  far below the 16.67 ms budget (the probe runs at roughly 250 fps with two peers), so the
  first increment can keep a fixed input delay and reserve dynamic catch-up for a later
  profile.
- It checks sync with CP0 register snapshots. The probe checks the assembled frame hash
  and cumulative PCM hash after every frame, which is stricter for 5DO's rendering and
  audio paths.
- RetroArch's libretro netplay can provide frontend rollback, but 5DO is a standalone
  emulator; the core would still need a state-transfer and hash policy. That is why the
  probe measures the existing snapshot cost rather than adopting the libretro interface.
- `References/CPP_SimpleGui` is the same RMG-style UI shell and delegates to the mupen
  core; `opera-libretro` only exposes libretro's frontend netplay hooks. Neither adds a
  5DO-specific protocol or state-sync design.

## Decision

**Use delay-based lockstep for the first direct-IP increment.** It preserves the existing
deterministic machine, needs no prediction or rollback, and the 36,000-frame probe passes
with zero divergence at roughly 252 fps. Input delay 2-4 is sufficient on loopback/LAN;
50 ms one-way latency still runs at about 128 fps with delay 8, well above the 60 Hz
target.

Rollback is technically viable (capture + restore is about 4.1 ms), but it would add
prediction, state history, and resimulation complexity for no measured benefit on the
target LAN scope. Keep it as a later option if a wide-area mode is ever approved.

The compressed state (650 KB, 38 ms save / 24 ms load) is small enough for initial join
sync and reconnect. The next increment should send it during the handshake, then resume
lockstep from the confirmed frame.

## Remaining work

- Direct-IP **host/join** CLI and desktop surface, with port, input-delay, and reconnect
  settings.
- Initial state transfer and validation at join; reconnect from the last confirmed state.
- Joypad input only in this probe; pointer devices and more than two players are out of
  scope for the first increment.
- Audio/video integration and latency display in the desktop shell.
- Desync recovery policy beyond reporting and stopping.
- User documentation for direct IP, ports, firewall rules, and the no-lobby/no-NAT scope.
- Security note: the probe protocol has no authentication or encryption. Do not expose the
  port to an untrusted network.

## How to run

```powershell
dotnet run --project tools/FiveDo.NetplayProbe -c Release -- `
  --disc "J:\Panasonic 3DO\Wolfenstein 3D (1994)(Interplay)(US)[!].chd" `
  --bios bios/panafz1.bin `
  --frames 36000 --delay 4 `
  --p1-script benchmarks/diff/scripts/tierb-10min.txt `
  --state-cost --json artifacts/netplay-probe/probe-36000.json
```

Add `--latency-ms 50 --delay 8` for a high-latency run, `--drop-input-every 3` for loss,
or `--no-reference` to skip the single-machine comparison. The JSON report contains every
sampled checkpoint, per-peer counters, the desync details, and the state measurements.
The committed 36,000-frame report from this run is
[`docs/data/netplay-probe-36000.json`](data/netplay-probe-36000.json).
