# Servo Internals: Encoder Processing & Commutation

Deep-dive on the feedback → commutation chain underneath the bring-up/tuning in `setup-workflow.md`.
Distilled from the OMRON lecture series (Curt Wilson). Raw transcripts: `reference/raw/edu/lecture/`
(local-only, gitignored). For current-loop / servo-loop / clock / safety detail, use the index in
`lecture-series.md` and grep the raw.

## Encoder types & processing
- **Digital quadrature incremental**: A/B, ×4 decode (4 counts/line); direction from phase; sampled at
  SCLK (one edge per cycle); needs index (C/I/Z) for power-on reference; no power-on absolute.
- **3-phase Hall**: 1 cycle/pole-pair (low res). Two uses — rough power-on phasing (±30°e via UVW
  inputs, corrected after index homing), OR 3-phase quadrature primary feedback (velocity apps).
- **Analog sinewave**: sin/cos analog; needs interpolation (line count + within-line arctangent).
- **Absolute serial**: serial protocol, absolute over ≥1 rev; DSPGATE3 = 9 protocols (1 SW-selected);
  time delays; no 1/T sub-cycle info.
- **Resolver**: dual rotary transformer, atan2 from sin/cos, absolute within 1 cycle, rugged, low-res;
  excitation + sampling sync to **Phase clock**; ECT low-pass filter strongly recommended.

## Sub-count extension (low-speed smoothness, NOT resolution at stop)
- **1/T**: two HW timers (time between last 2 counts / time since last count); `Pos = Counter ± T2/T1`,
  evaluated at servo/phase interrupt and at external capture. DSPGATE3 does it in HW.
- **Arctangent** (sine encoders): line counter + sin/cos ADC for within-line; combined at servo rate
  (e.g. 4096 states/line). DSPGATE3 ASIC in HW (was ECT software in PMAC2).

## Sinusoidal encoder error model (position error E vs signal defect)
Sine offset `E=x·cosθ` · Cosine offset `E=-x·sinθ` · Magnitude mismatch `E=(x/2)·sin2θ` ·
Phase error `E=-φ·cos2θ`. **ACI** (auto-correcting interpolator, FPGA @20 MHz) runs Fourier on the
error and subtracts harmonics. Magnitude check = sum-of-squares (Lissajous radius²).

## Error checking (set motor to auto-disable → EncLoss)
- **Quadrature**: count error (both A&B change in one SCLK → unrecoverable, re-home; raise SCLK if
  signal-too-fast, lower if noise) + signal-loss (differential-pair XOR).
- **Sinusoidal**: no XOR loss detect; use sum-of-squares magnitude (note: magnitude falls with speed,
  so ASIC default threshold is often too high).
- **Serial**: timeout (loss) + CRC/parity (corruption); error bits at high end of `SerialEncDataB`;
  `EncTable[n].type=12` can substitute extrapolated data; trip after N error scans.

## Encoder Conversion Table (ECT) — why it exists
Servo wants one floating-point position; HW gives counters+timers / ADCs / multiple registers. ECT
combines + pre-processes:
- **Change limiting** (digital bit-error ride-through): `.index3=0` limits 1st-deriv (velocity) 1 cycle
  then slews at `.MaxDelta`; `.index3>0` limits 2nd-deriv (accel) for N cycles; `.MaxDelta=0`=off.
- **Low-pass/tracking filter** (noisy analog/resolver): enabled if `.index2>31`; `.index2`=LP gain
  (Tf = 256/(256−.index2) −1 servo cycles), `.index1`=integral gain (`.index4`=exponent).
- Also integration/differentiation, shift/scale/difference/clip for rollover.

## Position compare (HW EQU output)
Set `CompA`/`CompB`/`CompAdd`; HW toggles EQU when encoder pos (incl. 1/T) reaches compare value, then
auto-increments with no further software — accurate, evenly-spaced pulses.

## Absolute vs incremental referencing
Incremental → re-reference each power-up: **phasing-search** (commutation angle) + **homing-search**
(overall position). Absolute → reference once at assembly; PMAC saves offset between sensor-zero and
motor-zero. Power-on read covers full range (often multi-turn; `SerialEncDataA`+`B`); ongoing read =
single 32-bit, computes change (assumes short direction for rollover). Multi-turn tech: gearing /
battery counter / Wiegand (keeps turns powerless).

## Motor commutation
- **Why**: brushless = electronically-commutated synchronous AC; switch current direction across phases
  to keep torque direction; sinusoidal commutation varies magnitude for precise torque.
- **Where it fits**: servo loop outputs torque (current-magnitude) command → commutation adds rotor
  field angle → phase-current commands → current loop. Can run in **controller OR drive**.
- **Why commutate in PMAC**: no feedback wiring to drive; cheaper/universal/simpler drives; high-quality
  sinusoidal; can commutate brushless from just incremental encoder (+ phasing search); AC-induction
  field-oriented (vector) control. **Commutation + servo in one device ⇒ lost/reversed feedback = loss
  of torque, not runaway** (key safety property of local motors vs drive-closed).
- **Open vs closed loop**: open (steppers; no sensor; relies on magnetics; silent-stall risk);
  closed (servos; sensor; uses more torque; characterizable).
- **Algorithms**: six-step (Hall, 6 states/elec cycle, torque ripple — PMAC doesn't use for ongoing);
  sinusoidal (current held at max-torque point, efficient, no oscillation). Stepper: full/half/micro
  (64–512 µsteps; cuts oscillation, not necessarily accuracy).
- **3 steps**: (1) rotor field angle (sync: enc vs reference; induction: + slip advance); (2) align
  stator current — torque current ⊥ rotor field (quadrature) + magnetization current ∥ (direct, for
  induction); (3) project onto phases (compute 2, 3rd from balance loop).
- **Phase referencing** (sync motors need absolute rotor angle; bad ref → runaway, torque ∝
  cos(angleError)): phasing-search move (stepper-search / four-guess) if no absolute sensor; OR
  absolute read (abs encoder / resolver / Hall).

## Output modes (commutation → amplifier)
- **Sine-wave output**: PMAC computes phase-current commands as DAC volts (16/18-bit); does NOT close
  the current loop (analog amp does). Super-high-precision (low delay/noise, high command resolution).
- **Direct-PWM**: PMAC closes current loops digitally; outputs PWM duty to transistors; amp ADCs return
  2 phase currents; loops closed in **DC (dq) frame** (minimizes hi-freq problems at speed); amp is a
  "dumb" power block, all setup in PMAC. (DC brush via Direct-PWM: freeze `PhasePos=0`, `PhaseMode`
  bit2=1 disables Id loop.)
- **Direct microstepping** (Direct-PWM, open-loop, no encoder — radiation-hard apps): slip from command
  velocity advances commutation angle; `IdCmd` sets current magnitude.

(Current-loop tuning, servo-loop algorithm/filters, clock/task structure, safety, comp/cam → index in
`lecture-series.md`, then grep `reference/raw/edu/lecture/`.)

---
Source: OMRON Power PMAC lecture series (Encoder Processing 2022.06, Motor Commutation 2023.01).
Raw local-only at `reference/raw/edu/lecture/` (gitignored). Concepts/elements only.
