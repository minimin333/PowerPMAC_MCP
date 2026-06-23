# Source Navigation Map (domain → manual page ranges)

Raw corpus lives in `reference/raw/<slug>/pXXXX-YYYY.txt` (20 pages/chunk, UTF-8, grep-able).
Page numbers below are **PDF page numbers** (match the `===== PAGE N =====` markers in chunks).
Use this map to read ONLY the relevant ranges when distilling or answering — do not read whole manuals.

Slugs: `user-manual` (882p), `software-ref` (1711p), `training` (630p), `c-programming` (37p).

---

## DATA STRUCTURE  (Sys. / Motor[] / Coord[] / Gate3[] / legacy P,Q,M,I,L)
- **software-ref p57–76** — COMMAND SYNTAX SUMMARY (operators, math/vector/matrix funcs,
  on-line vs buffered command categories). ★ most compact syntax source.
- software-ref p79–632 — SAVED data structure elements (Sys., Motor[], Coord[], Gate3[]…).
- software-ref p633–776 — NON-SAVED setup elements (incl. EtherCAT cyclic I/O).
- software-ref p777–918 — STATUS elements.
- user-manual p548+ — Computational Features: P, Q, M, L, user-defined variables, pointers.

## SCRIPT MOTION PROGRAMS  (prog: linear/circle/PVT/spline, coord sys, kinematics, G-code)
- user-manual p659–711 — Writing/Executing Script Programs (motion, rotary, subprograms,
  kinematic subroutines), coordinate-system addressing, start/stop execution.
- user-manual p703 — Standard G-Codes.
- user-manual p469–500 — Executing Individual Motor Moves (jog, home, etc.).
- user-manual p501–547 — Setting Up Coordinate Systems; Kinematic program buffers (p513).
- user-manual p712–803 — Move Mode Trajectories; Lookahead (p779–803).
- user-manual p868+ — EXAMPLE SCRIPT PROGRAMS ★ harvest snippets here.
- software-ref p70–72 — Move / Move-mode / Axis-attribute / Move-attribute commands.
- training — worked motion examples (grep for `linear`, `pvt`, `circle`, `dwell`).

## SCRIPT PLC PROGRAMS  (plc: background logic, timers, command(), I/O sequencing)
- user-manual p686–699 — PLC Programs; Starting/Stopping Script PLC Execution.
- software-ref p72–73 — Program Logic Control; Script PLC Execution Control.
- training — worked PLC examples (grep for `plc`, `while`, `command`).

## C PROGRAMMING  (CPLC real-time, capp background, C API, gplib, pp_proj)
- **c-programming (full 37p)** — focused C programming guide. ★ primary source.
- user-manual p845–867 — Writing C Functions and Programs in Power PMAC.
- user-manual p862 — CfromScript (calling C from Script).

## TASK MODEL & GOTCHAS  (real-time vs background, clocks, save/reset, safety)
- user-manual p61–91 — System Configuration; Real-Time Interrupt tasks (p73), Background (p75).
- user-manual p423–468 — Making Your Power PMAC Application Safe (limits, following error).
- user-manual p548–549 — RTI vs Background computational contexts.

---
NOTE: PDF originals are NOT committed (.gitignore). To regenerate the raw corpus from the
PDFs, run `tools/extract_pdfs.py` with the manuals present in `../Power PMAC Manual/`.
