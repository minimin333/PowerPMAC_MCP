# Lecture Series Index (OMRON / Curt Wilson)

Deep-theory lecture series. Each topic has a raw transcript at `reference/raw/edu/lecture/`
(local-only, gitignored) — **grep it for detail**. Encoder & Commutation are already distilled in
`servo-internals.md`; the rest are pointers (grep raw when a question goes deep).

| Topic | Key content | Raw file (`raw/edu/lecture/…`) |
|---|---|---|
| **Encoder Processing** | enc types, 1/T & arctangent sub-count, ECT, error checking, absolute/resolver → **`servo-internals.md`** | `2022.06.21 - Encoder Processing.txt` (+ `_Korean.txt`) |
| **Trajectory Generation** | command-trajectory focus, interpolation, blending; the main motion-controller differentiator | `2022.08.09 - Trajectory Generation.txt` |
| **Clock Generation & Task Control** | Phase/Servo/RTI clock sources & dividers; hard real-time; task priority Phase>Servo>RTI>Background | `2022.09.08 - Clock Generation.txt` |
| **Safety Features** | runaway guards, FatalFeLimit, EncLoss, amp-fault, limits, watchdog, abort — **extends `gotchas.md`** | `2022.10.18 - Safety Features.txt` |
| **Compensation & Cam Tables** | comp tables (0/1/2/3-D, corrected every servo cycle, systematic-error); cam tables (motion gen) | `2022.11.15 - Compensation and Cam Tables.txt` |
| **Motor Commutation** | brushless/stepper, open/closed loop, sinusoidal, phase referencing, sine vs Direct-PWM → **`servo-internals.md`** | `2023.01.19 - Motor Commutation.txt` |
| **Motor Current Control** | T=KT·I; voltage→current; PMAC closes current loop digitally; PI loop in dq frame | `2023.02.23 - Power PMAC Motor Current Control.txt` |
| **Servo Control Part 1** | position/velocity loop placement; PID + feedforward; split between controller/drive/motor | `2023.03.30 - Power PMAC Servo Control (Part 1).txt` |
| **Servo Control Part 2** | advanced: notch/low-pass filters, feedforward, anti-vibration | `2023.05.23 - Power PMAC Servo Control (Part 2).txt` |
| **Script Language** | why a special motion script language vs standard languages — overlaps `syntax-rules.md`/`script-*.md` | `2023.07.25 - Power PMAC Script Language.txt` |
| **CNC Applications 1 & 2** | G-code, advanced move blending, look-ahead, path-error control — overlaps `script-motion.md` | `2023.09.28 …(Part 1).txt`, `2023.10.31 …(Part 2).txt` |
| **Robotic Applications** | non-Cartesian mechanisms, forward/inverse kinematics, path planning | `2023.12.14 - Robotic Applications.txt` |

A Korean translation exists for Encoder Processing (`…_Korean.txt`). Video (.mp4) lives next to each
PDF on the user's drive but is not ingested (no text).
