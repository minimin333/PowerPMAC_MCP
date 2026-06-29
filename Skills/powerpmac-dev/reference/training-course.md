# Training Course Index (ODT Beginners)

Hands-on setup/programming course. Raw at `reference/raw/edu/training/` (local-only, gitignored).
Most topics overlap existing reference — see "maps to". Bring-up procedures are distilled in
`setup-workflow.md`; grep the raw for course-specific exercises.

| # | Topic | Maps to | Raw file (`raw/edu/training/…`) |
|---|---|---|---|
| 00 | Training Intro (about ODT) | — | `00- PPMAC Training Introduction.txt` |
| 01 | ACC24E3 Hardware Overview | hardware | `01- Power UMAC ACC24E3 Hardware Overview.txt` |
| 02 | IDE Overview | `setup-workflow.md` | `02- Power PMAC IDE Overview.txt` |
| 03 | Structures & Variables | `data-structure.md`, `syntax-rules.md` | `03- …Structures & Variables.txt` |
| 04 | Training Machine (XYZC inspection rig) | — | `04- UMAC Training Machine.txt` |
| 05 | Jogging / Plot / Abort | `setup-workflow.md` (Jog) | `05- Jogging Plot Abort.txt` |
| 06 | Homing & Triggered Moves | `setup-workflow.md` (Homing) | `06- Homing & triggered Moves.txt` |
| 07 | Coordinate Systems & Motion Programs | `script-motion.md` | `07- Coordinate Systems & Motion Programs.txt` |
| 08 | Multitasking & PLCs | `script-plc.md`, `gotchas.md` | `08- Multitasking & PLCs.txt` |
| 09 | Subprograms & Subroutines | `script-plc.md` | `09- Subprograms and Subroutines.txt` |
| 10 | Beginners Final Exercise (Estop/Reset/Pendant PLC + serpentine scan) | — | `10- Beginners Final Exercise.txt` |
| 11 | System Configuration (`$$$***`→`save`→`$$$`) | `setup-workflow.md` | `11- …System Configuration.txt` |
| 12 | Encoder Configuration | `servo-internals.md` (ECT) | `12- ACC24E3 Encoder Configuration.txt` |
| 13 | Motor & Amp Config | `setup-workflow.md` (local setup) | `13- ACC24E3 Motor Amp Config.txt` |
| 14a | Current Loop Tuning | `servo-internals.md` (Direct-PWM) | `14a- Current Loop Tuning.txt` |
| 14b | Motor Phasing | `servo-internals.md` (phase referencing) | `14b- Motor Phasing.txt` |
| 14c | Position Loop Tuning | `setup-workflow.md` (tuning) | `14c- Position Loop Tuning.txt` |

**Course setup-order chain** (recurring in 11–14): `$$$***`/`save`/`$$$` → basic system & dominant
clock settings → encoder config (ECT) → motor & amp config → motor commissioning (phasing) →
jog/home → motion / PLCs / HMI.
