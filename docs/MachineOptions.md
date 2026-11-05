# Machine options

This file is generated from `MachineOptionRegistry` by `5do settings options --format markdown`.
Do not edit the table by hand; update the registry and regenerate the file.

Each option is validated by the machine, reachable from the CLI, desktop settings, and per-game profiles, and recorded in save states.

| Key | CLI | Settings | Profile | Type | Default | Range | State | Description |
|---|---|---|---|---|---|---|---|---|
| cpuClock | --cpu-clock | CpuClockMultiplier | CpuClockMultiplier | number | 1x (12.5 MHz) | 0.5x-4x (6.25-50 MHz) | yes | ARM60 clock multiplier. 1x is the stock 12.5 MHz; 0.5x-4x is supported. |
| region | --region | Region | Region | enum (VideoRegion) | Ntsc | Ntsc, Pal1, Pal2 | yes | NTSC 320x240 at 60 Hz, PAL1 320x288 at 50 Hz, or PAL2 384x288 at 50 Hz. |
| armTiming | --arm-timing | ArmTiming | ArmTiming | enum (ArmTimingMode) | FreeDoCompatible | FreeDoCompatible, Arm60Datasheet | yes | ARM60 N-cycle class: FreeDO-compatible N=4 or the ARM60 datasheet N=2. |
| clioTimers | --clio-timers | ClioTimers | ClioTimers | enum (ClioTimerMode) | FreeDoCompatible | FreeDoCompatible, OperaScheduler | yes | CLIO timer scan: FreeDO's all-timer scan or Opera's per-slot scheduler. |
| celTiming | --cel-timing | CelTiming | CelTiming | enum (CelTimingMode) | FreeDoCompatible | FreeDoCompatible, OperaBusy | yes | MADAM CEL timing: synchronous FreeDO draws or Opera's pixel/texel busy deadline. |
| cdromTiming | --cd-timing | CdromTiming | CdromTiming | enum (CdromTimingMode) | FreeDoCompatible | FreeDoCompatible, OperaCycleBased | yes | CD-ROM data-sector timing: immediate FreeDO transfers or Opera's cycle-based readiness. |
| cdSpeed | --cd-speed | CdSpeed | CdSpeed | enum (CdSpeed) | X2 | Unbounded, X1, X2, X4, X8, X16, X24, X32 | yes | Effective read speed for the cycle-based CD-ROM model. The stock 3DO drive is 2x. |
| memory | --memory | MemoryConfiguration | MemoryConfiguration | enum (MemoryConfiguration) | 2 MB DRAM / 1 MB VRAM (21) | 21, 22, 41, 42, 81, 82, E2, F1 (DRAM/VRAM in MB) | yes | DRAM/VRAM capacity. The stock machine has 2 MB DRAM and 1 MB VRAM; expanded layouts change the RAM boundary and VRAM base. |
| randomSeed | --random-seed | RandomSeed | RandomSeed | seed | default (0xa5a5a5a5) | default or a 32-bit value | yes | Initial DSP PRNG seed. Blank uses FreeDO's default 0xa5a5a5a5. |
| fixMode | --fix-mode | FixModeOverrides | FixMode | flags | None | 0x00000000-0xFFFFFFFF | yes | Per-game 4DO compatibility fix mask. |
| cpuBackend | --cpu-backend | CpuBackend | CpuBackend | enum (CpuBackendKind) | Interpreter | Interpreter | yes | Guest ARM60 backend. The interpreter is the reference and the only implemented backend. |
| highResolution | --high-res | HighResolution | HighResolution | boolean | disabled | enabled, disabled | yes | Opt-in 640x480 CEL/VDLP rendering. |
| operaDipir | --opera-dipir | OperaDipir | OperaDipir | boolean | disabled | enabled, disabled | yes | CLIO DIPIR latches and XBUS access aborts. |
| operaSoftReset | --opera-soft-reset | OperaSoftReset | OperaSoftReset | boolean | disabled | enabled, disabled | yes | CLIO CSTAT soft resets use Opera's ARM/MADAM/DSP reset path. |
