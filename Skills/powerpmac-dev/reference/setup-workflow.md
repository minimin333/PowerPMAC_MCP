# IDE Setup & Operation Workflow

Practical bring-up workflow: IDE → system clocks → motor setup (local & EtherCAT) →
tuning → jog → homing → EtherCAT enable/reset. Distilled from OMRON Power PMAC LEVEL1/2
training. Element names match the firmware tables; this file is **procedures only** —
product-specific numbers are examples. Full raw text: `reference/raw/edu/` (local-only, gitignored).

## IDE essentials
- **Connect**: default IP `192.168.0.200`, user `root`, pw `deltatau` (Test / No Device options).
- **Key windows**: Terminal (online cmds — *execute immediately*, be careful), Watch (live elements;
  click cell → "C" to switch read→command), Position, Status (motor/CS/global flags; red=fault,
  green=ok), Output (build/download), PowerPMAC Messages (all error reports), Plot/Scope (gather),
  Jog Ribbon, Task Manager (CPU / PLC / prog running state).
- **Build & download**: right-click solution → *Build and Download All Programs*. `F1` = command help.

## System setup (do first)
1. **Factory reset**: `$$$***` (factory defaults) → `save` → `$$$` (reset).
2. **Persistence**: download → Active (RAM). `save` → flash (`/opt/ppmac/usrflash`). `$$$` reloads
   saved. SAVED setup survives reset; NON-SAVED setup is re-applied each boot by a startup PLC.
3. **New project**: template = PMAC-only or EtherCAT. Set project property
   *"download systemsetup.cfg" = No* — otherwise a later `save` reverts your motor setup to defaults.
4. **Clocks** (System → CPU → System): set Phase / Servo / RealTime(RTI) freq per #motors & load.
   Phase = commutation + current loop (highest prio); Servo = commanded-position update rate;
   RTI = motion planning + foreground PLC.
   - Script/MCP: set `Gate3[i].PhaseFreq` (CK3M AX unit: `CK3WAX[i].PhaseFreq`; firmware snaps to nearest)
     + `ServoClockDiv` (ServoFreq = PhaseFreq/(ServoClockDiv+1)), **and separately** `Sys.ServoPeriod`
     (= 1000/ServoFreqHz, ms) + `Sys.PhaseOverServoPeriod` — these two are independent SAVED params, **not**
     auto-derived from the Gate clock. `Sys.WpKey=$AAAAAAAA` to unlock, then `save`+`$$$`. After reset, give
     the controller a moment before reading — clock-derived values (`Sys.ServoPeriod`…) read **stale**
     immediately post-reset. Verify true rate from two `Sys.ServoCount` samples (ΔCount/Δwall-time).

## Local motor setup (analog ±10V or Direct-PWM; e.g. CK3M + AX card + servo drive)
Wizard: right-click **Motors → Add a Motor → topology "Single Feedback"**, then:
1. **Amplifier** — pick or define (manufacturer + part #; via Delta Tau → Part Managers).
2. **Motor** — pick/define from datasheet (wrong data → motor/amp damage).
3. **Encoder** — pick/define (e.g. 2500 line = 10000 count/rev typical).
4. **Hardware interface** — amp / feedback / flag channels; set Amp Fault Level (Low/High True),
   enable HW limits if used → Accept.
5. **Interactive feedback** — hand-turn motor, set Encoder Direction → Accept.
6. **Limits**, then **I2T** — torque-mode general servos have their own overload cutoff → "Turn
   Protection Off"; otherwise it limits to the smaller of motor/amp continuous current.
7. **Test & Set** (DC bias offset etc.; may need retries) → motor ready for tuning.
- **Pointer model**: outputs/feedback are set by address, e.g. `Motor[1].pDac = Gate3[0].Chan[0].Pwm[0].a`
  (`.a` = address-of an element; leading `p` = pointer-to). 
- **CK3M AX unit**: the axis-interface unit (e.g. CK3W-AX1515N) is structure **`CK3WAX[i]`** with `.Chan[0..3]`,
  *not* `Gate3[i]` — clocks and channel registers live there (`CK3WAX[0].Chan[0].Pwm[0].a`/`.Dac[0].a` alias
  the same analog out; `.ServoCapt.a`, `.OutCtrl.a`, `.Status.a`). Factory default already wires `Motor[1]`
  to `CK3WAX[0].Chan[0]` as a single-feedback analog motor (`PhaseCtrl=0`, drive commutates).
- **Amp-fault status latches**: once tripped it clears only on an enable (`#xj/`) — *not* by flipping
  `AmpFaultLevel`, toggling `ServoCtrl`, or `#xk`. Set `AmpFaultLevel` to the drive's **healthy** fault-line
  level: an OMRON G5 analog drive's ALM is fail-safe (line low when healthy) → `AmpFaultLevel=1`.
- **Changing scaling (`Motor[].PosSf`) live, then enabling, makes the motor jump/runaway** — set scaling
  first, then `save`+`$$$` so it applies cleanly from boot, *then* enable.

## Tuning (position loop)
Auto/Basic = one-touch "Start Tuning" → Accept → Servo On + jog to verify.
Manual interactive — find each gain in order, zero the others first:
- **Kp** (P, stiffness): raise until noise/oscillation onset, back off (step move, e.g. 1000ct / 300ms).
- **Kvfb** (D, damping): cut Kp overshoot & settling time.
- **Kvff** (velocity FF, parabolic move): cut velocity-dependent following error; start ≈ Kvfb value.
- **Ki + IntegralMode** (1 = at-stop only, 0 = whole move): cut steady-state following error.
- **Kaff** (accel FF, trapezoidal move): cut following error in accel/decel segments.
- **Fine tune**: gather during real machining; adjust gains to spec (canned profiles aren't enough).

## Jog (simplest closed-loop move; single motor; async to CS/program)
Online `#nJ…`, program `Jog…`:
- `#1J/` servo-on hold · `#1J+`/`#1J-` infinite · `#1J=val` absolute · `#1J:val` relative ·
  `#1J=` pre-jog pos · `#1J=*` go to `Motor[x].ProgJogPos` · `#1J==val` move & set pre-jog.
- Program forms: `Jog/1`, `Jog+1`, `Jog1=`, `Jog1:`, `Jogret1`, `Jog1=*`.
- Profile vars: `Motor[].JogSpeed` (motor units/ms, always +), `Motor[].JogTa`, `Motor[].JogTs`
  (>0 = time ms; <0 = inverse rate — if JogTa<0, JogTs must also be <0). New values apply on the
  *next* jog command.
- `Motor[x].InPos` = 1 when settled: closed-loop + `DesVelZero` + no move/dwell + |FE| ≤
  `InPosBand` for `InPosTime`+1 cycles. **`InPosBand` default 0 → must set.**
- Abort decel: `Motor[].AbortTa/AbortTs` (auto on HW/SW limit, CS abort, runtime error).

## Homing — Local (Gate3 hardware capture)
Vars: `Motor[].HomeVel` (±), `JogTa`, `JogTs`, `HomeOffset`. Status: `HomeInProgress`, `HomeComplete`.
1. Unlock Gate3 writes: `Sys.WpKey=$AAAAAAAA` (else writes silently ignored).
2. `Gate3[0].Chan[0].CaptCtrl` (0–15 = flag/index, hi/lo combo) + `.CaptFlagSel`
   (0 home, 1 +limit, 2 −limit, 3 user).
3. `#1hm` (online) — or `home 1;` **inside a PLC buffer** (`home 1` as an online cmd = ILLEGAL CMD).
   Watch `HomeComplete` 0→1, `HomeInProgress` 1→0.
- **`CaptCtrl` (verified live)**: `1`=index(Z); `2`=selected flag **high / rising (0→1)**;
  `10`=flag **low / falling (1→0)** (=2 with the +8 invert bit). Pick the edge to match the sensor.
- **★ Real position = `Motor[x].ActPos − Motor[x].HomePos`, NOT raw `ActPos`.** Homing writes the captured
  position into `HomePos`, so a good home reads `ActPos−HomePos ≈ 0` (or `HomeOffset`). raw ActPos is the
  encoder's (possibly multi-turn) accumulated count — judging home success by it mistakes a correct home for
  a fault. → [[ppmac-actpos-homepos]].
- **Home flag can be a half-plane, not a point**: a real rig may drive the home input 0 across one half of
  travel and 1 across the other (boundary = the home edge). Approach so the flag is in its *inactive* state at
  the start and capture the transition (e.g. come from the − side where flag=0, move +, capture rising →
  `CaptCtrl=2`). If the flag is already in the capture-active level at the start (e.g. flag-low `CaptCtrl=10`
  while already on the low side), `#xhm` triggers immediately = false home at the start position.

## Homing — limit-find then home-sensor (multi-stage PLC, verified live)
Pattern the user asked for: jog −, confirm minus limit, reverse +, complete on the home sensor. Braces
state-machine PLC (one per motor, independent; trigger all together with one var). Per motor:
`jog-x` → minus-limit detected → stop → (settle) → `home x` / `jog+x` → home sensor → `homez x` or capture.
- **Local motor with native limits set (`pLimits`≠0)**: hitting a HW limit **auto-aborts** the motor; a
  `jog+`/`home` issued in the *very next* PLC scan gets **absorbed** (motor stays put). Insert a settle —
  `Sys.CdTimer[i]=200` (ms) then wait `<=0` — before issuing the off-limit move. (A motor with native limits
  *cleared* has no abort, so its off-limit `jog+` takes immediately — see EtherCAT case below.)
- Read flags through the interpreted elements when `pLimits` is set: `Motor[x].MinusLimit`/`.PlusLimit`,
  `CK3WAX[0].Chan[0].HomeFlag`. **Both limits reading 1 at once = un-wired/floating limit inputs** (open =
  fail-safe active), not a real both-limit condition — characterize the sensor before trusting it.
- `home x` (not `homez`) re-runs the configured capture (CaptCtrl/CaptFlagSel) while moving at `HomeVel`
  (sign = direction); use it when you want hardware-captured precision. `homez x` just sets the present
  position as home (HomeComplete=1, no move) — fine for a software-detected sensor edge.

## EtherCAT motor setup (e.g. OMRON 1S servo drive, NX-I/O)
EtherCAT = real-time master/slave fieldbus. **DC** (Distributed Clock) syncs slaves to the servo
RTI. **PDO** = cyclic data, starts when `ECAT[i].Enable=1` (DC mode for motion, FreeRun for plain
I/O). **SDO** = acyclic register R/W:
`EcatTypedSdo(master, slave, dir(0=wr/1=rd), index, subindex, data, length)`.
Steps:
1. Set clocks first. Add EtherCAT master (or use the EtherCAT project template).
2. EtherCAT cycle = multiple of **62.5 µs** (dual-core max 250 µs); auto-matches servo rate.
3. Right-click master → **Scan EtherCAT Network** (if it fails, terminal `ecat reset`). Slaves appear.
4. Open each slave: remove unused Safety module; **PDO Mapping** tab → choose the right PDO set;
   **DC** tab → Shift Time = 25–50 % of cycle (check *Overwrite Mode*). Plain I/O slave → FreeRun.
5. Right-click master → **Load Mapping to PMAC**.
6. **Add a Motor → topology "EtherCAT"** → pick slave + control type + user-unit conversion → save
   (box orange→green). Hardware interface: 1S auto-fills → Accept.
7. **Build & Download**, then enable the net: `ECAT[0].Enable=1` (or right-click master → Active EtherCAT).
8. *Watch EtherCAT Mapped Variables* to verify cyclic updates.
- PDO-mapped elements have long generated names, e.g. `Slave_1001_…_60FD_0_Digitalinputs.a`.
- The scan generates `Project/PMAC Script Language/Global Includes/ECATMap.pmh` with
  `#define Slave_<pos>_<model>_<idx>_<sub>_<name>  ECAT[0].IO[n].Data` — **outputs `IO[0..]`, inputs `IO[4096..]`**.
  The scan + PDO map + *Load Mapping to PMAC* is an **IDE-only** step (firmware master = acontis; a pure
  terminal/script session cannot generate the named map). Once mapped, motor assignment IS scriptable:

### EtherCAT CiA402 (CSP) motor — element-level assignment (script/MCP, after mapping)
A motor can be bound to a CiA402 drive **without the Add-Motor wizard**, pointing at the `ECAT[0].IO[n].Data`
entries above (verified live on an OMRON 1S):
- **Feedback**: `EncTable[k].type=1`, `.pEnc=ECAT[0].IO[<6064 actual>].Data.a`, `.pEnc1=Sys.pushm`,
  `.index1..6=0`, `.ScaleFactor=1` → reads the 32-bit Position-actual register. **type 1, not 11**
  (type 1 reads a plain 32-bit register when the indices are 0). Then `Motor[x].pEnc=pEnc2=EncTable[k].a`.
- **Command (CSP)**: `Motor[x].pDac=ECAT[0].IO[<607A target>].Data.a` (the servo output IS the target position).
- **Enable/fault**: `Motor[x].pAmpEnable=ECAT[0].IO[<6040 controlword>].Data.a` (PMAC auto-runs the CiA402
  state machine: Switch-On-Disabled→Ready→…→Operation-Enabled), `Motor[x].pAmpFault=ECAT[0].IO[<6041
  statusword>].Data.a`, `Motor[x].AmpFaultBit=3` (statusword Fault bit), set `AmpFaultLevel`.
- `Motor[x].PhaseCtrl=0` (drive commutates); scaling `Motor[x].PosSf=Pos2Sf` (e.g. user-units / 2^encbits).
- **★ `Motor[x].Ctrl=Sys.PosCtrl` — REQUIRED for CSP.** Default `Ctrl` runs a torque/PID loop, so the target
  register (607A) is never written (stays 0); the drive then trips on position deviation (OMRON 1S error
  `$FF24`, statusword Fault bit) on every enable. `Sys.PosCtrl` passes the commanded position straight to
  `pDac`, so the **drive** closes the loop. Easy to miss when copying a motor — `Motor[].Ctrl` must be copied
  too, not just pointers/scale/gains.
- Enable with `#xj/`; the target syncs to actual at enable (no jump) and the CiA402 fault latch clears.

## EtherCAT limits & homing (pointers into PDO)
- **Limits**: `Motor[n].pLimits = Slave_…_60FD_0_Digitalinputs.a`; `Motor[n].LimitBits`
  (positive limit = bit `LimitBits`, negative limit = bit `LimitBits+1`).
  **★ Verify the actual 60FD bit map per drive — don't assume.** On a live OMRON 1S the negative limit (NOT)
  read as **bit 1** and the home/EXT1 input as **bit 17**, both active-high — *not* the nominal CiA bit0/bit2.
  6M counts of − jog never lit the assumed bit2; the bit that actually toggled at the limit was the truth.
- **Detect-in-PLC alternative**: leaving `pLimits=0` and bit-testing the DI in the PLC
  (`if (ECAT[0].IO[<60FD>].Data & (1<<bit)) {...}`) avoids native limit-abort, so an off-limit `jog+x` takes
  effect immediately (no settle needed, unlike a native-limit local motor). Complete with `homez x` on the
  home-bit rising edge.
- **online `jog/x` on an EtherCAT motor can return `MOTOR NOT ACTIVE`** — use the addressed form `#xj/`
  instead. Inside a PLC buffer `jog/x` / `jog-x` / `home x;` work normally.
- **Homing via drive touch-probe** (CiA402: `0x60B8` func / `0x60B9` status / `0x60BA` pos):
  `Motor[2].pCaptPos = Slave_…_60BA_…Touchprobepos1posvalue.a`,
  `Motor[2].pCaptFlag = Slave_…_60B9_…Touchprobestatus.a`, `CaptFlagBit=1`,
  `CaptPosLeftShift=0`, `CaptPosRightShift=0`. Touchprobefunction: `$15`=index latch, `$11`=flag(EXT1).
  Sequence: func=0 → wait status 0 → func=`$15` → wait status 1 → `#1hm` → wait status 3 →
  check `Motor[1].HomeComplete`.

## EtherCAT init / reset patterns (startup PLC)
- **Init** (because cyclic I/O is NON-SAVED): wait `ECAT[0].SlaveCount == Σ ECAT[0].Slave[k].Online`,
  set `ECAT[0].Enable=1`, then wait `ECAT[0].MasterState==2 && ECAT[0].MasterReady==1`
  (timeout via `Sys.Time`).
- **Reset**: `cmd "ecatreset"`, then wait `Sys.EcatMasterReady` / `ECAT[0].MasterReady` back to 1.

## Local vs EtherCAT motor — setup contrast
| | Local | EtherCAT |
|---|---|---|
| Output/feedback | `Motor[x].pDac/pEnc → Gate3[i].Chan[j]` | pointers → PDO-mapped slave vars |
| Servo loop | PMAC closes (commutation via Phase clock) | drive closes (CiA402 CSP/CSV/CST); CK3E has no Phase task |
| Limits / home | Gate3 `CaptCtrl`/`CaptFlagSel` | `pLimits`/`LimitBits` + drive touch-probe |
| Bring-up | Add Motor "Single Feedback" + HW interface | Scan → PDO map → Load → Add Motor "EtherCAT" → Enable |
| Persistence | servo setup SAVED | cyclic I/O NON-SAVED → re-init each boot |

## MCP application notes (the `powerpmac` MCP tools)
This workflow maps straight onto the live MCP — use it to generate correct command sequences:
- **Verify setup** → `get_responses`: `Motor[1].AmpEna`, `.InPos`, `.HomeComplete`,
  `Gate3[0].Chan[0].CaptCtrl`, `ECAT[0].Enable`, `ECAT[0].MasterReady`, `ECAT[0].SlaveCount`.
- **Jog** → `send_command "Motor[1].JogSpeed=10"` then `#1j+` / `#1j/` / `#1j=10000`.
- **Home (local)** → `send_command "Sys.WpKey=$AAAAAAAA"` → set `CaptCtrl`/`CaptFlagSel` → `#1hm`.
- **EtherCAT bring-up** → after `download_project`: `send_command "ECAT[0].Enable=1"`;
  recover with `send_command "ecatreset"`; read mapped slave vars via `get_response`.
- **SDO** → drive params at runtime via `EcatTypedSdo(...)` inside a Script PLC (not a bare online cmd).
- **Build/download** → `build_project` (compiles Script + ARM C, incl. loaded EtherCAT map) →
  `download_project`. Always persist with `send_command "save"`; the EtherCAT map itself is
  NON-SAVED, so rely on a startup PLC to re-enable the net.

---
Source: OMRON Power PMAC LEVEL1/2 training. Raw text kept local-only at `reference/raw/edu/`
(gitignored). General procedures and firmware elements only; vendor part numbers are illustrative.
