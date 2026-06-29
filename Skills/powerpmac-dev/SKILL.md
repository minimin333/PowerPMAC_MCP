---
name: powerpmac-dev
description: >-
  Write, review, and debug OMRON Delta Tau Power PMAC controller code — Script
  motion programs (prog), Script PLCs (plc), and C programs (CPLC/capp). Use
  whenever the task involves Power PMAC / PMAC / Delta Tau / Sysmac motion
  control: motion program move modes (linear/circle/pvt/spline), coordinate
  systems & kinematics, data-structure elements (Sys./Motor[]/Coord[]/Gate3[]),
  P/Q/M/I variables, save/$$$/reset behavior, CfromScript, real-time vs
  background tasks, or motion safety (following error, limits, kill/abort).
---

# Power PMAC Development

Knowledge skill for OMRON Delta Tau **Power PMAC**. Distilled from the official
manuals (User Manual, Software Reference, 5-Day Training, C Programming).
This file is the map + the safety summary; open the `reference/` file for depth.

## How to use this skill
1. Identify the domain (Script motion? Script PLC? C? data structure? a gotcha?).
2. Read **only** the matching `reference/*.md` below — they are dense distillations.
3. For an exact element/command not in the distillation, `grep reference/raw/`
   (the full manual text, ~3260 pages). `reference/NAVIGATION.md` maps domains →
   manual page ranges; `reference/raw/software-ref/_toc.txt` is the element index.
4. Before emitting motion/safety code, check the **Top gotchas** below.

## Reference routing
| Need | File |
|---|---|
| Operators, variables (P/Q/M/L/I), flow control, on-line vs buffered commands | `reference/syntax-rules.md` |
| `Structure[index].Element` model, Sys./Motor[]/Coord[]/Gate3[], SAVED/NON-SAVED/STATUS, I-var mapping | `reference/data-structure.md` |
| Motion programs: `open prog`, move modes, axis def, coord systems, kinematics, lookahead, G-code | `reference/script-motion.md` |
| PLC programs: `open plc`, scan model, timers, `cmd`, sequencing, idioms | `reference/script-plc.md` |
| C: CPLC (real-time) vs capp (background), C API / pshm access, CfromScript, build/pp_proj | `reference/c-programming.md` |
| **C API real signatures** (gplib.h: GetResponse/Command/GetPmacVar; RtGpShm.h pshm structs) | `reference/c-api.md` |
| **Authoritative element list** (every `Structure.Element`, from firmware intellisense tables) | `reference/firmware/ELEMENTS_INDEX.md` + grep `reference/firmware/pp_swtbl*.txt` |
| Pitfalls: task model, save/reset, units, motion safety, error IDs | `reference/gotchas.md` |
| **Project structure**: folder layout, file types, `.ppproj` manifest, `pp_proj.ini` load order, on-controller `/var/ftp/usrflash/Project` mapping | `reference/project-structure.md` |
| **IDE & motor bring-up**: IDE windows, system clocks, **local & EtherCAT motor setup**, PID tuning, jog params, homing (Gate3 capture / EtherCAT touch-probe), EtherCAT enable/reset; + MCP command mapping | `reference/setup-workflow.md` |
| **Servo internals** (deep): encoder types/sub-count(1/T,arctangent)/ECT/EncLoss, commutation modes, phase referencing, sine vs Direct-PWM output | `reference/servo-internals.md` |
| **Vendor course material** (deep-theory lectures + ODT training): topic→raw index; grep `reference/raw/edu/` for detail | `reference/lecture-series.md`, `reference/training-course.md` |
| Domain → manual page map; how to regenerate the raw corpus | `reference/NAVIGATION.md` |
| Verified example programs to adapt | `snippets/` |

## Mental model (what makes Power PMAC different)
- **Everything is a named data-structure element**, not a raw register or I-variable.
  `Structure[index].Element` — e.g. `Motor[1].JogSpeed`, `Coord[1].Tm`, `Gate3[0].Chan[0].ServoCapt`.
  Indices start at 0, are constants or a single local var (no math in `[]`). Legacy Turbo
  `I`-variables still alias SAVED elements (`I123 ≡ Motor[1].HomeVel`; query with `I{n}->`).
- **Three program kinds, three jobs:**
  - **Motion program** (`open prog N…close`, addressed `&x`) = coordinated **path** motion
    in a coordinate system (linear/circle/pvt/spline, F/TA/TS, G-code). Not for machine logic.
  - **Script PLC** (`open plc N…close`) = asynchronous **logic / I/O / sequencing**. Runs one
    full pass per scan then yields — never `while(1)` block it. Issues `jog`/program-control
    via `cmd "…"`. Not for path moves.
  - **C** = **CPLC** (one real-time PLC in the RTI, must be short & non-blocking) or **capp**
    (background C app, may use OS facilities). Plus **CfromScript** functions callable from Script.
- **Four fixed priority tiers (high→low): Phase → Servo → RTI → Background.** Each preempts
  lower ones. Motion planning, foreground PLCs, and motor *safety checks* run at **RTI** rate.
  Background has no fixed period — never rely on it for timing.
- **Coordinate system vs motor.** `&x` C.S. commands (`run`/`abort`/`hold`/`%`) act on the whole
  group; `#x` motor commands (`jog`/`home`/`kill`) act on one motor. An axis-definition
  (`#1->10000X`) links a motor to a C.S. axis and sets **axis units ≠ motor units**.
- **RAM vs flash.** A project download lives in RAM only. `save` persists SAVED setup + project
  to flash; `$$$` reset restores from flash; `$$$***` = factory defaults. Unsaved changes die on reset.

## Top gotchas (full list: `reference/gotchas.md`)
1. **Unsaved changes are lost on reset/power-cycle.** `save` after changing any SAVED element.
   Non-saved setup must be re-applied each boot (startup PLC).
2. **`kill` ≠ `abort` ≠ `disable`.** `kill/k` = open loop, amp off, *no decel* (instant). `abort` =
   controlled closed-loop decel + stops the program. On vertical/gravity axes use the **delayed**
   forms (`dkill`/`ddisable`) so the brake engages first.
3. **Most safety is OFF by default.** Software limits (MaxPos=MinPos=0), encoder-loss (pEncLoss=0),
   amp-fault, abort-all — all disabled until configured. FatalFeLimit is the main runaway guard;
   don't zero it.
4. **Motor units vs axis units.** Jog/home/limits/FatalFeLimit are in **motor** units; axis
   (engineering) units come from the axis-definition scale. Rescaling motor units silently rescales
   every motor-unit limit.
5. **No blocking in real-time.** Phase/Servo/RTI/CPLC code must be short & non-blocking; overruns
   increment ServoErrorCtr/PhaseErrorCtr or trip the watchdog. RTI must run >40×/s.
6. **PLC scan semantics.** A background Script PLC runs one pass then yields; an infinite inner loop
   starves other PLCs and can trip the watchdog. Use timers/state machines, not blocking waits.
7. **Index in `[]` = constant or one local var, no expressions.** `Motor[L0+1]` is illegal — compute
   into `L0` first. Runtime out-of-range indices give **no error**, just corruption.
8. **Q is per-coordinate-system; a PLC must set `Ldata.Coord`** or it reads the wrong Q-set. P is
   global; L/R/C/D are local and alias each other and don't carry between programs.
9. **Don't drive one motor from two owners** (running program + jog) → error 46
   `COORD JOGGED OUT OF POSITION`; run `pmatch` before resuming.
10. **Gate3[i] key setup writes need `Sys.WpKey=$AAAAAAAA`** first, or the write is silently ignored.
    Reading a non-present hardware element returns `nan`, which then propagates through math.

## Accuracy discipline (for the assistant)
- Power PMAC element/command names are exact and version-sensitive. If unsure an element or keyword
  exists, **grep `reference/raw/` to confirm before using it** — do not invent names.
- Distillation notes flagged "(verify: …)" are unconfirmed; verify against raw before relying on them.
- Cite the source page when stating a non-obvious rule, so the user can check the manual.
