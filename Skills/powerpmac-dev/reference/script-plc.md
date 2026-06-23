# Power PMAC Script PLC Programs (`plc`)

Background/foreground logic programs. C-like syntax, **not** sequenced by motion.
Use for: machine logic, I/O sequencing, supervision, starting/monitoring motion, error response.
Cites: (UM p686), (SWREF p72), (TRN p283) = User's Manual / Software Reference / Training PDF page.

## Definition & numbering

```
open plc 1        // numbered 0..31
// program contents
close
```
- **Up to 32 PLCs, numbered 0..31** (TRN p283).
- May name instead of number; IDE auto-assigns an internal number starting at 1. Use the name with `enable plc`, `list plc` (TRN p282):
```
open plc Startup
// contents
close
```
- One PLC buffer per `open`/`close`. `clear plc N` erases a buffer; `list plc N` reports it (SWREF p64).
- On power-on, reset, or project download, **all Script PLCs are disabled by default** (UM p697). Put `enable plc` lines in `pp_startup.txt` to auto-run.

## Execution model — read this first

A PLC runs **one scan** = top of program to end of program, OR to a "jump back" (end of a `while` loop, or `goto` to an earlier line) (UM p686).
- If a scan reaches **end of program** → next scan restarts at the **top**.
- If a scan stops on a **jump back** (bottom of a true `while`) → next scan resumes at the **point jumped back to** (top of that loop) (UM p686, TRN p284).
- **PLCs repeat automatically until disabled** — no outer loop needed to "keep alive" (TRN p283/284). For a one-shot PLC, make the last line `disable plc N` (TRN p284).

**Foreground vs background** (UM p686, TRN p283):
- `Sys.MaxRtPlc` (range 0..3) = highest-numbered PLC that runs in **foreground under the RTI**. Each RTI, after motion-program calcs, one scan of each active foreground PLC runs (PLC 0 first).
- PLCs numbered **above `Sys.MaxRtPlc` run in background**, in time left when no interrupt task runs. Each background cycle, **one scan of each active background Script PLC** runs; one scan of every active C PLC runs between them. Then the CPU releases to the OS for `Sys.BgSleep` (UM p686).
- Changes to `Sys.MaxRtPlc` take effect only while the affected PLCs are disabled (TRN p283).
- RTI fires every `Sys.RtIntPeriod + 1` servo interrupts (UM p72).

**Task context** (UM p72): foreground PLCs share the RTI with motion-program planning, kinematics, lookahead, and motor safety checks. Background PLCs are lowest priority. Heavy work in a foreground PLC steals time from motion calculation.

### Why you must NOT block

A scan runs to completion before any other same-or-lower-priority task gets the CPU. An indefinite tight loop never yields → starves other PLCs and (in foreground) motion calcs. The **correct** way to "wait" is an **empty `while` that the scan exits each pass**:
```
while (Input1 == 0) {}   // scan ENDS here each pass; resumes at top of while next scan
```
Each time the loop condition is true, **the scan ends**; other tasks run; the next scan re-tests the loop (UM p688). This yields. A `while(1){ ...big body... }` that never hits a jump-back-with-yield within reason is fine *as long as* it contains an inner yielding wait or finishes its body quickly — but prefer letting the program end and auto-restart.
- **`dwell` / `delay` do NOT work as PLC delays** — those are motion-sequenced commands (UM p688). Use timers (below).

## Control flow (C-like) (SWREF p72, TRN p288)

```
if (cond) { ... } else { ... }      // braces optional if single statement
while (cond) { ... }                // omit braces for single statement
do { ... } while (cond)             // always executes once
switch (intExpr) { case 0: ... break; default: ... break; }  // integer states only
goto N    gosub N    callsub N    call SubName(args)   return
N1000:                              // numeric line label
```
Operators (TRN p285): arithmetic `+ - * / %`; bitwise `& | ^ ~ << >>`; logical `&& ||`; comparators `== != > < >= <= !` and `~` (approx, within 0.5). Assignment `=  += -= *= /= %= &= |= ^= <<= >>=  ++ --`.

### Timers / delay without blocking

Three verified mechanisms:

1. **Timer subprogram** using `Sys.Time` (seconds) — the IDE-idiomatic pattern (TRN p292):
```
open subprog Timer(duration)
local EndTime = Sys.Time + duration;   // duration in seconds
while (Sys.Time < EndTime) {}          // yields each scan
close
```
```
call Timer(0.25);   // wait 0.25 s before proceeding
```
2. **`Sys.CdTimer[i]`** countdown timers, `i = 0..255`, scaled in **milliseconds**, V2.2+ (UM p688):
```
Sys.CdTimer[5] = 750;                   // 750 ms
while (Sys.CdTimer[5] > 0) {}           // counts down automatically
```
3. **`Sys.RunTime`** (seconds since reset) (UM p688):
```
MyEndTime = Sys.RunTime + MyDelayTime;
while (Sys.RunTime < MyEndTime) {}
```

### State-machine pattern
```
open plc Sequencer
switch (MachineState) {
  case 0: if (StartBtn) { jog+1; MachineState = 1; } break;
  case 1: if (Motor[1].InPos) { MachineState = 2; } break;
  case 2: Output1 = 1; MachineState = 0; break;
}
close
```
Note no outer loop: the PLC re-scans automatically, advancing the state each pass.

## Commanding from a PLC

Buffered Script commands (jog/home/kill, axis moves, variable assigns, program control) can be issued **directly** in PLC code (SWREF p70). Key fact: **PLC execution is NOT sequenced by moves** — a move command just *starts* the move and the scan continues (UM p686). You must monitor for completion yourself.

**Motor / coordinate-system commands inside a PLC** (TRN p294–298):
```
jog+1;  jog-1;  jog/1;              // jog +/- indefinitely / closed-loop stop
jog1=2000;  jog1:5000;  jog1^5000; // to abs pos / relative cmd / relative actual
home 1;  homez 1,2,3;  kill 1;     // home / zero-home / kill
```
**Starting a motion program from a PLC** — set the PLC's modal C.S. then issue program-form commands (UM p690, TRN p241):
```
Ldata.Coord = 1;       // PLC's modal coordinate system (power-on default 0)
                       // or PLC[n].Ldata.Coord = m to set another PLC's C.S.
start:10               // begin+run motion program 10 from start
run1                   // run C.S. 1; abort2 ; pause ; resume   (list C.S. or use Ldata.Coord)
```
PLCs can only command **rapid-mode axis moves and motor moves** (jog/home); other move types (linear/circle/PVT) must come from a motion program (UM p687). A PLC does **not** auto-`pmatch`; call `pmatch` explicitly before an axis move if needed (UM p687).

**Sending an on-line / cross-thread command string** with `cmd "..."`, then flush with `sendallcmds` and poll `Ldata.CmdStatus` (UM p778, TRN p581):
```
Ldata.CmdStatus = 1;                       // set, clears when commands done
cmd "&1 delete lookahead";
cmd "&1 #4->100C";
cmd "&1 define lookahead 10000";
sendallcmds;                               // ensure commands execute
do dwell 0; while (Ldata.CmdStatus == 1);  // wait until fully executed
```

**Querying status** — read status elements directly each scan: `Motor[x].InPos`, `Motor[x].HomeComplete`, `Motor[x].DesVelZero`, `Coord[x].ErrorStatus`, `Plc[i].Active`, `Plc[i].Running`.

## Variables & I/O

- Globals across PLCs: `global Name = 0;` then plain `Name` (TRN p287). Legacy `P`/`M`/`Q` also available; `M` typically maps to I/O registers.
- `local Name;` and `local Name(8);` (array) are per-call locals (TRN p299/301).
- Talk to I/O via mapped variables or data-structure elements, e.g. `GateIo[0].DataReg[3] = GateIo[0].DataReg[0];` (copy input word to output word) (TRN p303). Named `Input1`/`Output1` in examples are user-defined `#define`/M-var aliases.
- **Buffered PLC-style I/O** (V2.1+): inputs copied to holding registers at scan start, outputs written at scan end, with up to 4-scan debounce — see UM chapter "Using General-Purpose Digital I/O" (UM p687).

## Idioms

**Edge detection (latch)** — required before any motion command so it fires once, not every scan (TRN p293, **WARNING**: always edge-trigger jog/home/motion):
```
open plc edgetriggered
local Latch1 = Input1;
while (1) {
  if (Input1 == 1) {
    if (Latch1 == 0) { Output1 = 1; Latch1 = 1; }      // rising edge
  } else {
    if (Latch1 == 1) { Output1 = 0; Latch1 = 0; }      // falling edge
  }
}
close
```
**Level-triggered** (combinational, safe for outputs but NOT for motion) (TRN p293):
```
if (Input1 == 1) { Output1 = 1; } else { Output1 = 0; }
```
**Watchdog/safety**: a motion program cannot trap limit/following-error/amp faults — that response must be in a PLC or host (UM p685). Poll `Coord[x].ErrorStatus` (16 = run-time error, 2 = buffer overflow) each scan and react (`kill`, `abort`, set outputs).

## Verified snippets

**1 — Counter, named PLC** (TRN p287):
```
global Counter = 0;
open plc increment
Counter++;
close
// terminal: enable plc increment
```

**2 — Home then jog, one-shot** (TRN p298):
```
open plc jog_home
home 1;                                                   // start home
call Timer(0.01);                                         // let command take effect
while (Motor[1].InPos == 0 || Motor[1].HomeComplete == 0) {}  // wait, yielding
jog1=2000;                                                // jog to 2000 cts absolute
call Timer(0.01);
while (Motor[1].InPos == 0) {}                            // wait for settle
disable plc jog_home                                      // one-shot: stop self
close
```

**3 — Edge-triggered jog from input** (TRN p299):
```
open plc jog_io
Latch1 = Input1;
while (1) {
  if (Input1 == 1) { if (Latch1 == 0) { Latch1 = 1; jog+1; } }   // rising → jog
  else            { if (Latch1 == 1) { Latch1 = 0; jog/1; } }    // falling → stop
}
close
```

## Enable / disable / debug commands (UM p697–699, SWREF p64)

| Command | Effect | Status after |
|---|---|---|
| `enable plc {list}` | Start scans at **beginning** of program; only way to (re)start | `Active=1 Running=1` |
| `disable plc {list}` | Stop at end of current scan; restart only via `enable`/`step` | `Active=0 Running=0` |
| `pause plc {list}` | Stop at current point; resume at paused point via `resume`/`step` | `Active=1 Running=0` |
| `resume plc {list}` | Resume paused/stepped PLC in continuous mode (not a disabled one) | `Active=1 Running=1` |
| `step plc {list}` | Execute one program line (debug) | `Active=1 Running=0` |

Lists: `enable plc 1..5, 7, 31`. Issued from within a PLC, `disable/pause plc` lets the current scan finish first (UM p698). Check status with `Plc[i].Active` / IDE Task Manager (TRN p283).

## Script PLC vs Motion Program vs C PLC — when to use which

| | **Script PLC** | **Script Motion Program (`prog`)** | **C PLC** |
|---|---|---|---|
| Sequencing | NOT move-sequenced; scan runs to end/jump-back, auto-repeats | Move-sequenced; calc suspends at move boundaries (UM p686) | Like Script PLC; bg or rti |
| Runs in | Foreground (≤`Sys.MaxRtPlc`) or background | RTI calc per coordinate system | `enable bgcplc`/`enable rticplc` (SWREF p73) |
| Move types | rapid axis + jog/home/kill only (UM p687) | linear/circle/PVT/spline + all | via API |
| After a move starts | continues, can monitor/decide/react to errors | suspends until move done | continues |
| Delays | timers (`Sys.Time`/`CdTimer`/`RunTime`); NOT `dwell` | `dwell`/`delay` | C timing |
| Best for | machine logic, I/O, supervision, error handling, starting/monitoring motion | pre-determined move sequences, paths, G-code | high-rate/heavy compute logic |

**Rule of thumb**: pre-planned motion → motion program. Decisions/monitoring/error response/I/O → PLC. Need the move to *start* but logic to keep running and react → PLC (UM p687).

## Deeper detail

- PLC execution & scheduling, delays, commanding moves: **UM p686–689** → `reference/raw/user-manual/p0681-0700.txt`.
- Start/stop PLC commands + status bits: **UM p697–699** → same chunk.
- Command syntax summary (Program Logic Control, Script PLC Execution Control, buffered Script commands): **SWREF p70–73** → `reference/raw/software-ref/p0061-0080.txt`.
- Task model (RTI vs background, servo/RTI task lists): **UM p70–74** → `reference/raw/user-manual/p0061-0080.txt`.
- Worked PLC examples (structure, control flow, timer, edge/level, jog/home, exercises): **TRN p281–300** → `reference/raw/training/p0281-0300.txt`; array/ptr I/O exercises **TRN p303** → `reference/raw/training/p0301-0320.txt`.
- `cmd "..."` / `sendallcmds` / `Ldata.CmdStatus` idiom: **UM p778** → `reference/raw/user-manual/p0781-0800.txt`; **TRN p581** → `reference/raw/training/p0581-0600.txt`.
- (verify: UM p688) `Sys.CdTimer` requires V2.2+ firmware; buffered PLC I/O requires V2.1+.
