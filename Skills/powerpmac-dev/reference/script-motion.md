# Power PMAC — Script Motion Programs (`prog`)

Reference for writing/reviewing Power PMAC **Script motion programs**: fixed/rotary motion
programs, move modes, coordinate-system setup, kinematics, lookahead, execution control, G-codes.
Sources: UM = User's Manual, SWREF = Software Reference (PDF page numbers).

> **Cardinal rule:** A motion program does **not** belong to a coordinate system (C.S.). Any active
> C.S. can run any program; the C.S. supplies the motor↔axis mapping. Motion programs are
> *sequenced by the move* — calculation auto-suspends after enough moves are queued and resumes as
> execution advances (UM p681). PLC programs are NOT move-sequenced and can command **only**
> rapid-mode moves (UM p686, UM p712).

---

## 1. Program definition & download

```
open prog 1        // open + ERASE buffer N (1..4294967295); no clear needed (UM p670)
linear; abs;       // modal setup
ta500 ts0 f5;      // modal params
X10; dwell500;     // moves / dwells
X0;  dwell500;
close              // close buffer; program now runnable
```

- `open prog N` … `close` — N is just a numeric label, no priority, up to 1023 programs (UM p658, p670).
- Buffer cannot be opened while that program (or a called subprog) is **executing or suspended**;
  issue `abort` (`a`) first to free it (UM p670).
- `open prog N` immediately followed by `close` (no body) → program ceases to exist (UM p670).
- Optional args: `open prog N[,stackoffset][,labeltablesize]` (SWREF p64).
- IDE source layout: Motion Programs / Libraries (subprograms) / Kinematics Routines / Global
  Includes / PLC Programs (UM p673).

### Subprograms & subroutines
- `open subprog N` … `close` — called via `call`, never run directly (UM p671). IDE form
  `open subprog Name(Arg1,Arg2,&Arg3)` auto-maps args to `L0,L1,...`; `&`-prefixed = return-by-ref
  (UM p675–676). Up to 16 args, 255-level nesting (UM p662, p676).
- In-program subroutines: jump label `N10000:` reached by `gosub`/`callsub` (UM p662). `callsub`
  passes locals; `gosub` does not.
- `call123.456` → `subprog 123` at label `N456000:` (frac × 1e6) (UM p663).

### Rotary motion programs (program 0 per C.S.)
- Streamed/appendable while running (parts too big for memory) (UM p658).
- `define rotary {bytes}[,{linebufbytes}]` (min 2048 B) → `open rotary` (appends, does NOT erase) →
  `close rotary`; `clear rotary` empties, `delete rotary` removes (UM p670, SWREF p67).
- Restrictions in rotary progs: no `goto`/jump labels, no `gosub`/`callsub`, no switch/case, only
  single-line `if`/`while`, no `else` (UM p659–662). `call` to subprograms IS allowed.

---

## 2. Coordinate-system addressing & running

- Address modally with `&x` (e.g. `&1`); the program-start command then acts on that C.S.
  (`&1r`, `&2a`) (UM p689). List form `&1..3r`, `&2,4,6a` acts on all listed without changing the
  modal C.S. (UM p689).
- **Select then run** (program counter = "pointer"):
  - `b{N}` / `begin:N` — point C.S. to program N (frac part × 1e6 = jump label; `b75.00135` → `N1350:`) (UM p691).
  - `r` / `run` — run continuously from current point (UM p691).
  - `s` / `step` — single-step (one move / `bstart`→`bstop` block) (UM p691).
  - `start{N}` / `start:N` — `begin`+`run` combined (point + run from start) (UM p691).
- Buffered (in-program) start/stop must list the C.S. (`run1`, `abort2`) or set `Ldata.Coord`
  (UM p689). Arg after colon: `start:500`; C.S. before colon: `start3:500` (UM p689).

**Preconditions to start a program** (UM p690): all axis motors active (`Motor[x].ServoCtrl>0`),
enabled, closed-loop; `Coord[x].Csolve=1` (axis-def solvable) OR a forward-kinematic routine
present; no motor with both overtravel limits tripped; program valid. A C.S. with **no** motors
assigned can still run a program ("dry run").

---

## 3. Move modes (modal — persist until changed) (UM p665, p711, SWREF p70)

| Keyword | Path / profile | Notes |
|---|---|---|
| `rapid` | min-time point-to-point, trapezoid/S-curve | only mode allowed in PLC progs; = G00 |
| `linear` | straight line in Cartesian, trapezoid | default on reset; = G01; blends |
| `circle1` / `circle2` | CW / CCW arc on **X/Y/Z** | = G02 / G03; needs segmentation |
| `circle3` / `circle4` | CW / CCW arc on **XX/YY/ZZ** | secondary Cartesian set |
| `spline{t}` | cubic **B-spline**, parabolic-vel | smooth multi-point; `spline1/2` are NOT keywords |
| `pvt{t}` | **Hermite** spline, parabolic-vel | per-axis end-velocity control |

Switch modes by issuing the keyword; all later moves use that mode. In a PLC program any move
command forces rapid mode regardless of declared mode (UM p665).

### 3a. Move commands (UM p667, p711, SWREF p70)
- Basic (rapid/linear/spline): `{axis}{data}…` — `X10 Y20 Z30`. Constant w/o parens, expression in
  parens: `YY(Target+50)`.
- Same line = simultaneous coordinated; separate lines = sequential.
- Uncommanded axes in the C.S. hold position.
- Repeating an axis letter on one line starts a new implicit block: `X5 Y10 X7 Y13` (UM p711).

### 3b. Rapid (UM p712)
```
rapid; inc; X30 Y10;       // each motor jogs; speed=Motor[x].MaxSpeed or JogSpeed
```
- Speed: `Motor[x].MaxSpeed` if `Motor[x].RapidSpeedSel=1` (default) else `Motor[x].JogSpeed`.
- Accel/jerk: `Motor[x].JogTa`, `Motor[x].JogTs` (>0 = time ms; <0 = inverse rate).
- `Coord[x].RapidVelCtrl=1` → slower axes stretched to match longest (≈straight line).
- Not blended; CAN be broken into from a PLC (`cx`) move (UM p720).

### 3c. Linear (UM p722)
```
linear; abs; ta100 td200 ts50; F50;   // F=feedrate, tm=time (mutually exclusive)
X100 Y50;
```
- `F{v}` sets vector speed (mag, **positive**!); `tm{t}` sets move time (ms). Both write
  `Coord[x].Tm` (F → negative value). Negative F = time mode → danger (UM p724).
- `ta`/`td`/`ts`/`tsd` → `Coord[x].Ta/.Td/.Ts/.Tsd`. If `Ts≥Ta`, total accel = `2*Ts` (UM p727).
- F units = (axis units)/(`Coord[x].FeedTime` ms). FeedTime 1000→/sec, 60000→/min (UM p724).
- Feedrate axes = `frax(...)` (default X,Y,Z); `nofrax` = none; non-feedrate axes timed by
  `Coord[x].AltFeedRate` (UM p666, p724).
- Acc/vel/jerk limits: `Motor[x].MaxSpeed`, `.InvAmax`, `.InvDmax`, `.InvJmax` (UM p727–729).

### 3d. Circle (UM p742)
Center by **vector** (I/J/K = X/Y/Z; II/JJ/KK = XX/YY/ZZ) — vector points start→center:
```
normal K-1;            // plane = XY (vector normal to plane); J-1=ZX, I-1=YZ (UM p708)
F10; abs; circle1;
X20 Y20 I20 J0;        // arc; I,J = start→center components
```
Or by **radius** (X/Y/Z only, not RR): `X20 Y20 R20` (+R = arc <180°, −R = arc >180°; no full
circle in one R command) (UM p743). A full circle needs `I…J…` with start=end (e.g. `I10` alone).
Requires segmentation mode (`Coord[x].SegMoveTime>0`) (UM p722). `normal` also sets the plane for
corner-blend decisions and 2D cutter comp.

### 3e. PVT (UM p778)
```
pvt200;                // move time 200 ms (re-issue pvt to change time)
X100:-25 Y50:0;        // {axis}{pos}:{end-velocity}  (signed velocity!)
```
- End velocity is signed (negative if ending in − direction). Time only from the `pvt{t}` command;
  `ta`/`tm` ignored (UM p778). Builds arbitrary profiles by stitching segments (UM p779).

### 3f. Spline (UM p785)
```
spline50;                          // uniform B-spline, all sections 50 ms
X1000; X1500; X2000;
spline50 spline100 spline150;      // non-uniform: T0Spline/T1Spline/T2Spline
```
- Continuous in pos/vel/accel even at boundaries. Programmed splines are never segmented (UM p785).
  Path does NOT pass exactly through programmed points (unlike PVT).

### 3g. Axis mode, dwell/delay (UM p666, p706)
- `abs` / `inc` — all axes, or `abs(x,y)` for listed axes. Reset default = abs.
- `dwell{ms}` — fixed time base, ignores feedrate override, **stops** blending/lookahead
  pre-calc (even `dwell 0`) (UM p706).
- `delay{ms}` — obeys time-base override, does NOT disable pre-calc.
- Move-until-trigger (rapid only): `X50^-5` (2nd val = signed dist from trigger) (UM p667, p718).
  Not supported through kinematics (UM p719).

---

## 4. Coordinate systems: axis definitions (UM p501)

Axis letters: `X Y Z A B C U V W`, plus `AA..HH`, `LL..ZZ` (up to 32/C.S.) (UM p501).
X/Y/Z and XX/YY/ZZ are the two Cartesian sets (only these do circles / 2D cutter comp) (UM p505).

```
&1                     // address C.S. 1 (axis-def cannot take a C.S. list)
#1->X                  // motor 1 = X, 1 motor unit per axis unit
#1->10000X             // scaling: 10000 counts per X unit
#1->10000X+20000       // + fixed offset (axis zero vs motor home)
#1->8660.25X-5000Y     // motor = linear combination (rotation / squareness)
#1->X #2->X            // gantry: two motors → same axis
#4->0                  // NULL definition (no axis, but shares time-base & fault)
#4->S / S0 / S1        // spindle (CS time-base / CS0 time-base / fixed 100%)
#1->I                  // motor is an inverse-kinematic axis (see §5)
```
- Scale factors stored in `Motor[i].CoordSf[j]` (j: A=0…ZZ=31, 32=offset) (UM p505).
- To move a motor between C.S.: null it first (`&1 #4->0` then `&2 #4->C`) (UM p503).
- Rotary rollover: `Coord[x].PosRollover[i]` (i=A,B,C,AA,BB,CC), usually 360 (UM p506).
- Multiple parallel coordinate systems: each `&x` is independent; the same program/subprogram can
  run in several C.S. simultaneously. `Ldata.Coord` tells a shared subprogram which C.S. called it
  (UM p701).

**pmatch** — converts present **motor** positions → starting **axis** positions (forward direction).
Auto-run internally on `r`/`s` (UM p509). Must be called **explicitly** before a move in a PLC
program, or after changing motor/axis relationship inside a motion program (UM p510, p688).

---

## 5. Kinematic subroutines (forward / inverse) (UM p511)

Use when motor↔axis relationship is **non-linear** (robots, 4/5-axis machine tools). Axis-def
statements handle only linear relationships.

```
&1 open forward        // (IDE: open forward(1))   — opens + clears buffer
  // INPUT  : KinPosMotorX = Lx  (motor cmd pos, motor units)
  // OUTPUT : KinPosAxisα = C0..C31; set bits of KinAxisUsed (D0) per axis used
  if (KinVelEna > 0) callsub 100;   // 2nd pass for &xv/&xf velocity reporting
  KinAxisUsed = $C0;                // here X($40)+Y($80)
N100:
  KinPosAxisX = ...(KinPosMotor1)...;
  KinPosAxisY = ...;
return
close

&1
#1->I #2->I            // declare inverse-kinematic motors
open inverse
  // INPUT  : KinPosAxisα = C0..C31 (target pos); PVT vel in C32..C63
  // OUTPUT : KinPosMotorX = Lx  (motor units)
  local X2Y2;
  ...
  KinPosMotor1 = ...; KinPosMotor2 = ...;
close
```

- **forward** (motor→axis): auto-called at program start (compute start axis pos) and by `pmatch`,
  `&xp/xd/xv/xf/xg`, `pread/dread/vread/fread/dtogread`. Must set the proper `D0` mask bits or the
  result is discarded (UM p659, p513).
- **inverse** (axis→motor): auto-called once per move (non-segmented) or once per segment
  (segmented), to produce motor targets for `#x->I` motors (UM p659, p518).
- Axis name → C-var / D0 bit table at UM p513 (A=C0/$1 … X=C6/$40, Y=C7/$80, Z=C8/$100 … ZZ=C31).
  Velocity vars C32..C63 (UM p519).
- Kinematic routines must contain **no** move commands (UM p688). Distinguish caller:
  `Ldata.Status & $40` = called from motion program (else query) (UM p514).
- To halt on bad solution: set `Coord[x].ErrorStatus = 255` (reserved for user errors); on query
  return `sqrt(-1)` (NaN) (UM p514). Iteration cap via `Ldata.GoBack` (default 10) (UM p517).
- Cannot open `forward`/`inverse` while a motion or PLC program in that C.S. is running (UM p672).

---

## 6. Lookahead (UM p790)

Scans ahead over buffered segments and slows the path (never changes it) so no motor violates its
position / velocity / acceleration limits; works **backwards** through the buffer to decelerate in
time (UM p790–794). Operates on **linear, circle, and PVT** moves; requires **segmentation mode**.
Do NOT use lookahead with external time base or where quick external reaction is needed (UM p790).

Setup (UM p791):
1. Axis-define all motors into the C.S.
2. Set `Motor[x].MaxPos/.MinPos`, `Motor[x].MaxSpeed`, `Motor[x].InvAmax` per motor; optionally
   `Coord[x].MaxFeedrate`.
3. `Coord[x].SegMoveTime` = 10–20 servo cycles (ms).
4. Size: stopping-time = `MaxSpeed*InvAmax`; segments = stopping-time/(2*SegMoveTime); set
   `Coord[x].LHDistance` = segments × 4/3 (round up).
5. After each reset: `define lookahead {#segments}` for the C.S. (≥ LHDistance + backup segments).
   `delete lookahead` to remove (SWREF p67).

Interactions:
- **PVT + lookahead** supported (UM p781). Centripetal limit for circles: `Coord[x].MaxCirAccel`.
- Stop / reverse / resume (act on lookahead buffer):
  - `lh\` (on-line `\`) — **quick stop**: fastest decel within accel limits; suspends (not abort).
  - `lh<` (`<`) — start **reverse** (retrace) through buffered moves (no new calc).
  - `lh>` (`>`) — resume **forward**.
  (UM p669, p692–694, SWREF p75).
- Soft-limit hit in lookahead → suspend & stop at limit (≈`\`), can retrace or resume; needs
  `abort` before a new program (UM p793).
- Feedrate / time-base override: `%{val}` on-line or `Coord[x].DesTimeBase`; `%0` freezes motion
  (still "running") (UM p695). Segmentation feedrate override is separate from time base.

---

## 7. Execution control (start / stop / hold) (UM p689)

**Start / resume**
- `r`/`run`, `s`/`step`, `start{N}`/`start:N`, `b{N}`/`begin`, `resume`, `lh>`/`>` (UM p691–692).

**Stop — resumable** (`Coord[x].ProgActive=1`, buffer NOT clearable until `abort`/`begin`):
- `q`/`pause` — finish calculated moves, stop at programmed point; resume with `r`/`s` (UM p693).
- `h`/`hold` — feed-hold: ramp time-base→0 at `Coord[x].FeedHoldSlew`; stops **off** a programmed
  point; resume `r`/`s`/`>`. `Coord[x].FeedHold`=3 decel,1 stopped,2 accel (UM p693).
- `\`/`lh\` — quick stop in lookahead (see §6) (UM p694).
- `%0` / `DesTimeBase=0` — freeze interpolation, program still "running" (UM p695).

**Stop — NOT resumable** (`Coord[x].ProgActive=0`, counter reset to start):
- `a`/`abort` — controlled decel of all C.S. motors at `Motor[x].AbortTa/.AbortTs`; off-path
  (UM p695). Used for faults / to free the buffer.
- `disable` — immediate kill (open loop, amps off); `ddisable` = delayed (brake) (UM p696).
- `adisable` — abort-then-delayed-kill (UM p697).
- `#*k` / `#*dkill` — kill all motors all C.S. (note: `#3k` will NOT kill motors of a C.S. running a
  program) (UM p697).

**Normal end / `return` / `stop`** — counter resets to program start, calculated moves finish,
motors hold closed-loop at last point; buffer reusable (UM p692). `stop` in a subprogram also resets
to top-level start.

**Single-step / program counter:** `s`/`step` advances one move (or `bstart`→`bstop` block); halts
running programs too (UM p691, p693). `list pc` = code from counter; `list apc` = from abort point
(SWREF p67).

**Relevant status flags** (Coord[x].): `Csolve` (axis-solve OK), `ProgActive` (resumable),
`ProgRunning`, `ProgProceeding` (motion advancing), `HomeComplete`, `FeedHold`, `LookAheadStop`,
`ErrorStatus` (16=run-time error, 2=buffer overflow, 255=user), `RunTimeError`, `BufferWarn`
(UM p683–694). Run-time error (equations late) → auto-abort all C.S. motors (UM p683).

**Conditional/flow:** `if/else`, `switch/case/break/default`, `while`, `do…while`, `continue`,
`break`, `goto`, `gosub`, `callsub`, `call`, `return` (SWREF p72). CNC single-line conditionals:
`cexecN`/`cskipN`/`csetN`/`cclrN` test/set bit N of `Coord[x].Cflags`; `ccallN`/`cdefN`/`cundefN`
for canned-cycle modal calls (UM p660–664).

---

## 8. G-codes (RS-274) — implemented as subprogram calls (UM p663, p703)

Power PMAC's native move syntax IS RS-274-compatible; the letter codes are dispatched to
integrator-written subprograms (UM p700):

| Code | Subprog element (default) | Jump label |
|---|---|---|
| `G{n}` | `Coord[x].Gprog` (1000) | `n*1000` (G17→N17000:) |
| `M{n}` | `Coord[x].Mprog` (1001) | `n*1000` |
| `T{n}` | `Coord[x].Tprog` (1002) | `n*1000` |
| `D{n}` | `Coord[x].Dprog` (1003) | `n*1000` |

Also S/H/F-codes via `Sprog`/`Hprog`/`Fprog` (UM p702–703). Call to a non-existent label in an
existing subprog jumps to the **top** of the subprog (use for unimplemented-code handling)
(UM p703). Standard mappings (UM p703–711): G00→`rapid`, G01→`linear`, G02→`circle1`,
G03→`circle2`, G04→`dwell`/`delay`, G09→`Coord[x].OnceNoBlend=1`, G17/18/19→`normal K-1/J-1/I-1`,
G40/41/42→`ccmode0/1/2`, G61/64→`Coord[x].NoBlend=1/0`, G90/91→`abs`/`inc`, G93/94→`InvTimeMode`.
Full G-code subroutine examples at **UM p703**.

---

## 9. Common patterns (verified snippets)

**A. Minimal linear program** (UM p867, Example 1 — verbatim):
```
open prog 1    // Open buffer for entry
linear;        // Linear interpolation move mode
abs;           // Absolute move mode
ta500;         // 1/2-second accel/decel time
ts0;           // No S-curve accel/decel time
f5;            // Speed of 5 axis units per time unit
X10;           // Move X-axis to position of 10 units
dwell500;      // Sit here for 1/2-second
X0;            // Move X-axis to position of 0 units
dwell500;      // Sit here for 1/2-second
close
// run with:  &1 b1r        (point C.S.1 to prog 1, then run)
```

**B. Blended linear + circle contour** (UM p868, Example 4 — verbatim excerpt):
```
f50; ta100; ts50;          // Params for linear & circle moves
linear y13;                // Straight-line move to (1,13)
circle1 x2 y14 i1 j0;      // CW arc to (2,14) about (2,13)
linear x3;                 // Straight-line move to (3,14)
circle1 x4 y13 i0 j-1;     // CW arc to (4,13) about (3,13)
dwell 0;                   // Stop blending and lookahead
```

**C. PVT segment building block** (UM p779 — verbatim):
```
inc
pvt200
Y133.333:1000
pvt100
Y100:1000
Y96.667:900
pvt500
Y83.333:0
```

---

## Deeper detail (raw chunks for harvesting)

- **Writing/executing Script programs, classes, flow control, downloading, start/stop:**
  `reference/raw/user-manual/p0641-0660.txt` (p658–660), `p0661-0680.txt` (p661–681),
  `p0681-0700.txt` (p681–700 — execution rules, start/stop commands).
- **G-codes & RS-274:** `p0701-0720.txt` (p700–711). ★ full G00–G95 subroutine examples.
- **Coordinate systems / axis defs / kinematics:** `p0501-0520.txt` (p501–520). ★ shoulder-elbow
  robot forward+inverse kinematic snippets (p515–519) — good harvest source.
- **Move-mode trajectories:** rapid `p0701-0720.txt` (p712–720); linear `p0721-0740.txt` (p722–730);
  circle `p0741-0760.txt` (p742–745 examples); PVT `p0761-0780.txt` (p778–781);
  spline `p0781-0800.txt` (p785–788).
- **Lookahead:** `p0781-0800.txt` (p790–795 — quick+detailed setup).
- **Command summary (keywords, buffered move/mode/attribute commands):**
  `reference/raw/software-ref/p0061-0080.txt` (p64–75). ★ most compact keyword list.
- **★ Example motion programs (snippet harvest):** `reference/raw/user-manual/p0861-0880.txt`
  (p867–869 Examples 1–4: linear, scaled/looping, circle contour).

### Gaps / cautions
- `spline1`/`spline2` are **not** Power PMAC keywords (only `spline{data}`; circle has `circle1..4`).
- 3D cutter comp (`ccmode3`, `nxyz`, `txyz`) and tool-radius-comp corner geometry only summarized
  (UM p760–765) — out of scope here; see those pages if needed.
- Exact `Coord[x]`/`Motor[x]` element ranges/defaults: confirm in SWREF saved/non-saved element
  chapters (SWREF p79+) before asserting specific values.
