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
3. `#1hm` (or `HOME 1` in a program). Watch `HomeComplete` 0→1, `HomeInProgress` 1→0.
- Index example: `CaptCtrl=1`. Flag(home-low) example: `CaptCtrl=10`, `CaptFlagSel=0`.

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

## EtherCAT limits & homing (pointers into PDO)
- **Limits**: `Motor[n].pLimits = Slave_…_60FD_0_Digitalinputs.a`; `Motor[n].LimitBits`
  (PMAC order is fixed NOT-POT; 1S → `0`).
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
