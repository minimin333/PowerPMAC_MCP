# Power PMAC Gotchas — Code Safety Checklist

Non-obvious characteristics and common mistakes that distinguish Power PMAC.
Format per item: **rule** → why → fix. Cite as (UM p73) = User's Manual PDF page, (SWREF p76) = Software Reference. Assertions are source-backed; uncertain items marked "(verify: …)".

---

## 1. TASK MODEL (phase / servo / RTI / background)

- **Four fixed priority tiers, highest first: Phase → Servo → RTI → Background.** Each tier preempts all lower ones; background runs only in leftover time. (UM p77, p548)
- **Phase clock (default ~9.04 kHz) runs commutation + current loop.** Only present if Servo/MACRO ICs exist; CK3E/IPC (EtherCAT-only) do NO phase tasks. (UM p70, p547)
- **Servo clock (default ~2.26 kHz = phase/4) runs ECT, interpolation, position/velocity loop, and motor safety status** (following error, des-vel-zero, in-position). (UM p70–71)
- **RTI runs every (Sys.RtIntPeriod+1) servo cycles (default = every 3rd).** It does motion-program move planning, foreground (RT) PLCs, lookahead, AND the motor safety checks: overtravel, amp fault, encoder loss, I2T, brake delays. (UM p73, p84)
- **Overtravel/amp-fault/encoder-loss/I2T checks happen at RTI rate, not servo rate.** A fault is caught within an RTI period, not instantly. Raising RTI frequency (low Sys.RtIntPeriod) speeds detection but costs CPU. (UM p73)
- **Sys.MotorsPerRtInt > 0 means NOT every motor is checked each RTI.** Used for very high block-rate apps; it changes the effective scaling/timing of LimitLimit, EncLossLimit, AmpFaultLimit, brake delays. Leave at 0 unless you understand this. (UM p85, p434)
- **RTI must run >40×/sec or the watchdog trips.** The RTI decrements the WDT counter; background resets it. Setting Sys.RtIntPeriod too large (RTI too slow) causes a soft watchdog trip. (UM p84, p423)
- **Background = Script PLCs (> Sys.MaxRtPlc) + background C PLCs + GPOS/C apps.** One BG cycle runs one scan of one BG Script PLC, then one scan of all BG C PLCs, looped until all BG Script PLCs ran once, then housekeeping. (UM p74–75)
- **PLC scan semantics: a background Script PLC runs ONE full pass then yields** — it is re-entered from the top next cycle. Do NOT write infinite `while(1)` loops in a background PLC; that starves other BG PLCs/housekeeping and can trip the watchdog. (UM p74–75)
- **After each BG cycle the scheduler SLEEPS for Sys.BgSleepTime (0.25–10 ms, default 1 ms)** to give GPOS/C apps and gpascii comms time. So BG PLC effective scan rate ≈ once per (BG work + sleep). Don't assume tight timing in background. (UM p75)
- **Background has no fixed period.** Never rely on a BG PLC for precise timing; use servo/RTI-rate mechanisms or hardware timers instead. (UM p89)
- **Blocking or long compute in foreground (phase/servo/RTI) is fatal.** If phase tasks overrun, Sys.PhaseErrorCtr increments; servo overrun → Sys.ServoBusyCtr/ServoErrorCtr; any nonzero ServoErrorCtr/PhaseErrorCtr is a serious fault. Keep foreground (RT PLC, CPLC) code short and non-blocking. (UM p87–88)
- **At most 1–4 Script PLCs run in foreground (RTI), set by Sys.MaxRtPlc (0–3).** PLCs 0..MaxRtPlc run at RTI; the rest (up to PLC 31) run in background. A PLC's tier depends on its number vs MaxRtPlc. (UM p548)
- **Only ONE compiled C PLC (rticplc) can run in the RTI.** Its exec time is NOT counted in the RTI time statistics. (UM p548, p88)
- **Multi-core CPUs run interrupt tasks on one core, background on another** (default). Don't assume single-core serialization of BG vs foreground on multi-core. (UM p74, p78)

## 2. SAVE / RESET LIFECYCLE

- **RAM is volatile; flash survives power-cycle. A download puts the project in RAM only.** Without `save`, everything is lost on reset/power-cycle. (UM p61–62)
- **`save` copies active SAVED setup elements + active project files into flash.** You MUST `save` after changing any saved setup element if you want it to persist. (UM p62, SWREF p62)
- **Only project items are saved.** Programs/tables transferred OUTSIDE the project (e.g. a temporary CNC part program) are NOT copied to flash by `save`. (UM p62)
- **`$$$` = reset: restores saved setup values from flash, reloads saved project, re-issues its commands.** Active (unsaved) changes are lost. (UM p62, SWREF p62)
- **On reset, on-line commands inside project files that set a saved element OVERWRITE the value just restored from flash.** If a startup PLC/command sets an element, that wins over the flash value. (UM p62)
- **`$$$***` = re-initialize: sets ALL saved elements to factory defaults and loads NO project** (project stays in flash, just not loaded). Use only when you intend a clean slate. (UM p62)
- **A hardware-config change since the last `save` auto-triggers re-initialization** (sets Sys.HWChangeErr=1); the saved project is NOT loaded. Add/remove a card → you must reconfigure, `save`, reset. (UM p62, p65, p69)
- **`reboot` restarts the underlying Linux too**, then loads the last saved project like `$$$`. `$$$`/`$$$***` do NOT restart Linux. (UM p62)
- **Three element classes: SAVED setup, NON-SAVED setup, STATUS.** Only SAVED are written to flash by `save`. STATUS elements are read-only outputs — never write them. NON-SAVED setup must be re-established every reset (e.g. in a startup PLC). (SWREF p79)
- **Status elements are NOT saved and NOT restored** — they reflect live state. Don't expect a status value to survive a reset. (SWREF p79)
- **Gate3[i] key setup elements (PhaseFreq, MacroMode*, MacroEnable*, ServoClockDiv) are write-protected.** Set `Sys.WpKey=$AAAAAAAA` (Script) or `Gate3[i].WpKey=$AAAAAAAA` (C, before each write) or the write is silently ineffective. (UM p83, p94, p96)
- **Clock-source change must be done on a single command line** (turn off old source + turn on new in one line) so there is never zero or double clock source — otherwise a watchdog trip. (UM p69)
- **`fsave`/`fload` handle only a limited element set** — not a substitute for full `save`. (SWREF p62)

## 3. VARIABLES & UNITS

- **Motor units ≠ axis (engineering) units.** Motor units derive from feedback resolution × EncTable[i].ScaleFactor × Motor[x].PosSf. Axis units come from the axis-definition scale (e.g. `#1->10000X`). FatalFeLimit, MaxPos/MinPos, JogSpeed etc. are in MOTOR units. (UM p427, p429)
- **Changing motor-unit scaling silently rescales all motor-unit limits.** Going to larger units can make FatalFeLimit so large it is effectively disabled. Re-check every motor-unit limit after a scale change. (UM p427)
- **P = global (all tasks). Q = per-coordinate-system. L/R/C/D = local to one program/comms thread.** Q-variable accessed depends on the addressed C.S.: `&n` in on-line, the running C.S. in a motion program, `Ldata.Coord` in a PLC. A PLC that forgets to set Ldata.Coord reads the wrong Q-set. (UM p558–559)
- **L, R, C, D are different views of the same local stack space** — they alias. R/C/D are "differently numbered L-variables." Don't assume independence. (UM p554)
- **Each top-level program and each comms thread has its OWN L-variable set.** L-values do NOT carry between PLCs or between a PLC and a motion program. (UM p557)
- **Index in `[]` must be a constant or a single local variable — no expressions.** `Motor[L0+1].x` is illegal; compute into L0 first: `L0=...; Motor[L0].x`. Square brackets = real array index; parentheses `P(expr)` = array-function (computes a variable number). (UM p553, p555)
- **Out-of-range index from a runtime expression gives NO error — unpredictable results.** Only constant indices are range-checked at download. Validate computed indices yourself. (UM p554–555)
- **Non-integer index/variable-number expressions round DOWN (toward zero floor), not nearest.** `P(27.9999)` → P27. Force rounding before use if computed via floating point. (UM p553, p555)
- **M (pointer) variables alias raw addresses/registers — aliasing is dangerous.** A self-defined or mis-pointed M-variable can clobber arbitrary memory/I/O. Prefer named data-structure elements; use M-vars deliberately. (UM p559)
- **M-variables are functions, not directly accessible in C** — use API calls (or Sys.M[i] in Script). P/Q ARE reachable in C via `pshm->P[i]` / `pshm->Coord[x].Q[i]`. (UM p559, p558)
- **IDE auto-assigns declared names starting at xVARSTART (default P8192, Q1024, M8192).** Direct-numbered P/Q/M below that are "safe"; above it you risk colliding with declared names. Keep manual `#define`s below xVARSTART. (UM p555–556)
- **No range checking on user arrays.** `global Arr(512)` + out-of-range index silently corrupts adjacent variables. (UM p556)
- **I-variables are a Turbo-PMAC compatibility shortcut aliasing data-structure elements** (e.g. I123 = Motor[1].HomeVel). Use `I{n}->` to see the mapping. Unassigned I-vars (I8192–I16383) are general-purpose. (UM p559–560)
- **Floating-point: most user vars (P,Q,L,R,D) are 64-bit double; there is NO 64-bit integer in Script.** Assigned decimals may report back slightly off (e.g. 1.1 → 1.10000000000000009). (UM p549, p551)
- **`nan`/`inf` propagate and corrupt later math.** Division by 0 → ±inf; sqrt(-1) → nan. Reading a non-present hardware element returns `nan`. Test with `isnan(x)` or `!(x<inf)`; better, guard before computing. (UM p550–551)

## 4. MOTION SAFETY

- **kill vs abort vs disable are NOT interchangeable.** `kill`/`k` = open loop, zero output, amp disabled, immediate (no deceleration). `abort` = controlled closed-loop decel to stop (Motor[x].AbortTa/AbortTs) and stops the motion program. `disable` = kill all motors in a C.S. (UM p427; SWREF p69, p74)
- **`dkill`/`ddisable`/`adisable` = DELAYED kill** — they wait for brake engagement (Motor[x].BrakeOnDelay). Plain `k`/`kill`/`disable` and fault-kills do NOT wait for the brake. On a vertical/gravity axis use the delayed forms. (UM p445–446; SWREF p74)
- **FatalFeLimit (Motor[x].FatalFeLimit) exceeded → motor is KILLED; other C.S. motors aborted (or killed if FaultMode bit0=1).** Sets Motor[x].FeFatal. Disabling it (0 or huge) removes runaway protection — strongly discouraged. (UM p427–428)
- **WarnFeLimit takes NO automatic action** — it only sets transparent Motor[x].FeWarn / Coord[x].FeWarn; your app must react. (UM p428)
- **Same fault response for fatal FE, amp fault, encoder loss, I2T: kill the motor, abort the rest of the C.S.** (or kill all if Motor[x].FaultMode bit0=1). Motors in OTHER coordinate systems are unaffected. (UM p440, p444, p447)
- **Software limits are OFF by default (MaxPos=MinPos=0).** They activate only when MaxPos > MinPos, in motor units, referenced to motor zero (home), unaffected by axis-origin offset. (UM p430)
- **Software limits checked at BOTH calc time and execution time;** Coord[x].SoftLimitStopDis chooses stop-program (0) vs saturate-at-limit (1) for program moves. Indefinite jog (`j+`/`j-`) becomes a definite jog to the soft limit. (UM p429–431)
- **Hardware limit switches MUST be normally-closed (failsafe); polarity is not user-changeable** for the standard config — a disconnected cable or lost limit-supply reads as "into limit." (UM p432)
- **Hardware limits are direction-sensitive; you MUST wire +limit and −limit to the correct inputs** or they do nothing. Set Motor[x].pLimits (0 = disabled) and Motor[x].LimitBits (24=PMAC2 DSPGATE1, 9=PMAC3 DSPGATE3, 25=MACRO). (UM p432–433)
- **Amp-enable/amp-fault polarity is NOT software-changeable for enable** (0=disable, 1=enable; HW forces 0 on fault). Fault-input polarity is set by Motor[x].AmpFaultLevel bit0 (default 1=high-true fault). Set pAmpEnable/AmpEnableBit (22 PMAC2 / 8 PMAC3) and pAmpFault/AmpFaultBit (23 PMAC2 / 7 PMAC3). (UM p446–447)
- **Encoder-loss and auxiliary-fault detection are OFF by default** (pEncLoss=0, pAuxFault=0). No automatic feedback-loss protection unless you configure it. Set EncLossLimit ≥ ~3–4 to avoid nuisance trips. (UM p438–440, p443)
- **I2T (Motor[x].I2tTrip/I2tSet/MaxDac) depends on a correct Sys.ServoPeriod** for the time term; a wrong ServoPeriod makes thermal protection inaccurate. I2T is impossible in pure position/velocity output mode (no current access). (UM p449, p451–452)
- **A motor with the null definition `#x->0` in a C.S. still participates in that C.S.'s fault sharing** (gets aborted/killed with the group). (UM p427, p447)
- **Brake output: 0=engage, 1=release, no software polarity; forced to 0 on reset/watchdog.** Wire failsafe accordingly. (UM p445)
- **Global abort input (Sys.pAbortAll/AbortAllBit) is off by default** except on Power Brick. A "1" is ALWAYS the fault state (failsafe), no polarity control. Per-C.S. reaction set by Coord[x].AbortAllMode (0=abort, 1=kill, 2=abort-then-delayed-kill, 3=ignore). (UM p425–426)
- **Aborting a motion program resets its program counter to the start** — you cannot simply resume from the aborted point (use `list apc` to find where it was). (UM p427)
- **Coordinate-system commands (run/abort/hold/%) act on the whole C.S.; motor commands (jog/home/k) act on one motor.** A motor assigned to a C.S. axis is driven by the program; jogging it while the program owns it conflicts — error 46 "COORD JOGGED OUT OF POSITION" / use `pmatch` before resuming. (SWREF p74, p77)
- **`jog/` stops jogging AND restores position control.** Use it to hand a motor back cleanly. (SWREF p74)

## 5. SCRIPT vs C vs PLC — choosing & mixing

- **Script motion program = coordinated/path motion (linear, circle, pvt, spline, G-code) in a coordinate system.** Don't put machine logic/IO sequencing in a motion program; put it in a PLC. (NAVIGATION; SWREF p70)
- **Script PLC = background/foreground logic, I/O, timers, sequencing — NOT coordinated path motion.** A PLC issues jog/program-control commands; it does not contain move blocks the way a motion program does. (UM p686+; SWREF p72)
- **Choose tier deliberately: foreground (RTI) PLC for fast/deterministic logic, background PLC for slow/non-time-critical logic.** Foreground PLC code must be short (it preempts background and steals RT budget). (UM p548, p84)
- **C PLC (CPLC): rticplc (1 only, foreground, RTI) vs background C PLCs (multiple).** Real-time C must be non-blocking, no OS calls that sleep/allocate. Background C / capp may use GPOS facilities. (UM p845+; c-programming guide)
- **M-variables can't be used directly in C; P/Q can via pshm.** Mixing Script M-vars into C requires API calls. (UM p559)
- **`command("…")` / cx-style execution of on-line commands from a PLC has overhead and async semantics** — the command is queued/parsed, not guaranteed instantaneous. Don't busy-issue commands every scan. (verify: UM PLC chapter p686–699)
- **Don't drive the same motor from two owners.** A motor controlled by a running motion program should not simultaneously be jogged from a PLC/terminal (error 46). (SWREF p77)

## 6. COMMON COMPILE / RUNTIME ERRORS (SWREF p76–78)

Reported command error IDs (query/terminal responses):
- **20 ILLEGAL CMD / 21 ILLEGAL PARAMETER** — bad command token or argument; usually a typo, wrong context, or wrong syntax form. (SWREF p77)
- **23 OUT OF RANGE NUMBER / 24 OUT OF ORDER NUMBER / 25 INVALID NUMBER / 26 INVALID RANGE** — numeric/index argument out of allowed range or malformed. Constant indices are range-checked here. (SWREF p77)
- **31 COMPILE ERR** — Script program failed to compile on download (syntax error in a buffered program). (SWREF p77)
- **33 BUFFER IN USE / 34 BUFFER FULL / 40 BUFFER NOT DEFINED / 41 BUFFER ALREADY DEFINED** — program/table buffer management errors; `open`/`close`/`delete` the buffer correctly. (SWREF p77)
- **35 INVALID LABEL / 36 INVALID LINE # / 22 PROGRAM NOT IN BUFFER** — goto/gosub target or program reference does not exist. (SWREF p77)
- **38 PROGRAM RUNNING / 39 NOT READY TO RUN** — cannot edit/start a program in the wrong run state; `abort` or `stop` first. (SWREF p77)
- **42 NO MOTORS DEFINED / 43 MOTOR NOT CLOSED LOOP / 44 MOTOR NOT PHASED / 45 MOTOR NOT ACTIVE** — motion/`run` rejected because motor isn't activated, phased (`$`), or closed-loop. Activate/phase/enable before commanding moves. (SWREF p77)
- **46 COORD JOGGED OUT OF POSITION** — a C.S. motor was jogged away; run `pmatch` (or jog back) before resuming the program. (SWREF p77)
- **47 SERVO REQUEST ACTIVE** — a servo operation is pending; retry. (SWREF p77)
- **70–77 Struct Write … Error** — writing a data-structure element failed (bad index, missing Gate/card, WpKey not set, read-only/status element). Check WpKey for Gate3[i] writes. (SWREF p78)
- **50–59 MACRO …** — ring not synced/available, no MACRO ICs, sync-master config (e.g. 57 SYNC MASTER MUST HAVE STN=0). (SWREF p77–78)

Common silent (no-error) traps: runtime out-of-range index (§3), unsaved changes lost on reset (§2), ineffective Gate3 writes without WpKey (§2), disabled-by-default safety (§4).

---

## Deeper detail (raw chunks)
- Task model, clocks, statistics, watchdog timing: `raw/user-manual/p0061-0080.txt`, `p0081-0100.txt` (UM p61–91); priorities `p0541-0560.txt` (UM p547–549).
- Save/reset/re-init/HWChange, clock source: `raw/user-manual/p0061-0080.txt` (UM p61–69).
- Variables, units, indices, floating point: `raw/user-manual/p0541-0560.txt` (UM p547–560); fatal-FE units `p0421-0440.txt` (UM p427).
- Safety (watchdog, abort-all, following error, soft/hard limits, encoder loss, amp fault, I2T, brake): `raw/user-manual/p0421-0440.txt`, `p0441-0460.txt` (UM p422–461).
- Command categories, kill/abort/disable/dkill, error table: `raw/software-ref/p0061-0080.txt` (SWREF p62–78).
- PLC/motion program detail: UM p659–711, p686–699 (not yet chunked-read here — verify before deep claims). C programming: `raw/c-programming/`.
