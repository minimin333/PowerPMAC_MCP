# Power PMAC Data-Structure Model

Power PMAC exposes nearly all memory and I/O registers through **pre-defined data structures** —
the same structures used by the embedded firmware. The user accesses control and status registers
by name, without defining addresses (UM p552). Distilled from SWREF p79+ (element dictionary)
and UM p552–554. Do not enumerate every element; use the grep pointers below.

## Addressing model (UM p553, SWREF p1184)
```
Structure[index].Element
Structure[index].SubStructure[index].Element     // nested
```
- **Index** is in **square brackets** `[ ]`, a non-negative integer **constant** or a **single local
  variable** (no math expressions in the index). Fractional → rounded DOWN to integer. Indices
  **start at 0** for every indexable structure (UM p553).
- Read value: send `{element}` (e.g. `Motor[1].JogSpeed`). Read its **address**: `{element}.a`
  (e.g. `Coord[3].PathDistance.a`, used to set pointer sources). Write: `{element}={expression}`.
- Nested hardware example: `Gate3[i].Chan[j].ServoCapt`. A missing hardware element reads `nan`.
- Indexing a true array uses `[ ]`; selecting a *numbered variable* uses `( )` (array function) —
  e.g. `Sys.P[i]` (element array, brackets) vs `P(i)` (variable function, parens).

### Index ranges of common structures (UM p554)
| Structure | Index range | Meaning |
|---|---|---|
| `Motor[x]` | 0–255 | `#x` motor |
| `Coord[x]` | 0–127 | `&x` coordinate system (C.S. 0 = "park" for unassigned motors) |
| `EncTable[n]` | 0–767 | encoder conversion table entry |
| `CompTable[m]` | 0–255 | compensation table |
| `CamTable[m]` | 0–255 | cam table |
| `Gate1[i].Chan[j]` | i 0–19, j 0–3 | DSPGATE1 servo IC, channel j+1 |
| `Gate2[i].Chan[j]` | i 0–15, j 0–3 | DSPGATE2 MACRO IC, channel j+1 |
| `Gate3[i].Chan[j]` | i 0–15, j 0–3 | DSPGATE3 general IC, channel j+1 |
| `GateIo[i]` | 0–15 | I/O ASIC board |

When a local variable supplies an index, only `L0`..`L(1022 − MaxConstantIndex)` are valid
(e.g. motors: `L0`..`L767`) (UM p554).

## Top-level structures (governance + persistence class)
Persistence class comes from the SWREF chapter a structure's elements appear in: **SAVED**
(p79–632), **NON-SAVED setup** (p633–776), **STATUS** (p777–918). Most major structures have
elements in more than one class.

| Structure | Indexed | Governs | Has SAVED | Has NON-SAVED | Has STATUS |
|---|---|---|---|---|---|
| `Sys.` | no | System globals: clocks (`ServoPeriod`,`RtIntPeriod`), `MaxRtPlc`, `MaxMotors`, status, `P[]`/`M[]` arrays | yes | — | yes |
| `Motor[x].` | 0–255 | Per-motor setup & motion: jog/home/limits/servo gains; `Motor[x].Servo.` sub for loop algorithm | yes | yes | yes |
| `Coord[x].` | 0–127 | Per-C.S.: axis defs, move params (`Ta`,`Ts`,`Tm`), `Q[]`, `Ldata.`, G/M/T/D prog dispatch, path calc | yes | yes | yes |
| `Gate1[i].` | 0–19 | DSPGATE1 (PMAC2-style) servo ASIC: PWM, clocks, channels | yes | — | yes (ASIC) |
| `Gate2[i].` | 0–15 | DSPGATE2 (PMAC2-style) MACRO/IO ASIC | yes | — | yes |
| `Gate3[i].` | 0–15 | DSPGATE3 (PMAC3-style) general ASIC: `Chan[j]` servo/encoder/DAC | yes | — | yes |
| `GateIo[i].` | 0–15 | I/O ASIC boards (ACC-11/14/etc.) | yes | yes | yes |
| `EncTable[n].` | 0–767 | Encoder conversion table: feedback/master processing | yes | — | (results in status) |
| `CompTable[m].` | 0–255 | Compensation tables (`.Data[i]`/`[j][i]`/`[k][j][i]`; usable as 2D/3D arrays) | yes | yes | yes |
| `CamTable[m].` | 0–255 | Cam tables | yes | yes | yes |
| `BufIo[i].` | — | Buffered (forced) I/O scan | yes | yes | yes |
| `AdcDemux.` | no | ADC de-multiplexing | yes | — | yes |
| `BrickAC.` / `BrickLV.` | (`.Chan[j]`) | Power Brick AC / LV amplifier (multi- & single-channel) | yes | yes | yes |
| `Plc[i].` | — | Script PLC program runtime (`Ldata.`) | — | yes | (status via Ldata) |
| `Gather.` | no | Data-gathering function | — | yes | — |

Accessory names like `Acc24E3[i]`, `Acc5E3[i]`, `Acc11E[i]`, `Acc84E[i]` are **Script aliases**
onto an underlying `Gate*`/`GateIo` structure (SWREF p79–80). `PowerBrick` controllers expose the
same `Motor[]`/`Coord[]`/`Gate3[]`/`BrickAC`/`BrickLV` model (verify exact element: grep SWREF
for `BrickAC.` / `BrickLV.`). `Cam[]` is not a top-level structure name — cam functionality is
`CamTable[m]` (verify: SWREF p113).

## SAVED vs NON-SAVED vs STATUS (SWREF p79; UM p552)
| Class | `save` copies to flash? | Restored on power-up/reset? | Writable by app? | Purpose |
|---|---|---|---|---|
| **SAVED setup** | YES | YES (active memory reloaded) | yes | Fundamental configuration (gains, limits, IDs) |
| **NON-SAVED setup** | NO | NO (default each boot) | yes | Runtime/volatile setup, incl. EtherCAT cyclic I/O |
| **STATUS** | NO | NO | NO — read only | Live state/feedback; writing them is meaningless |

Why it matters:
- `save` persists only SAVED elements. NON-SAVED setup must be re-applied by startup PLC / project
  load each boot.
- `$$$` resets and **restores SAVED values from flash**; `$$$***` resets to **factory defaults**
  (SWREF p62). `backup` reports present SAVED values.
- Never write STATUS elements from application code.

## Legacy Turbo-PMAC I-/M-variable mapping vs structure model (UM p559–560)
- The structure model **supersedes** Turbo PMAC `I`-variables and user `M`-variable assignments,
  but legacy aliases remain for convenience.
- A SAVED element that matches a Turbo I-variable is also reachable by that **same I-number**.
  Example: `Motor[1].HomeVel` ≡ `I123`. Query the mapping with `I{n}->` (e.g. `I123->` returns
  `Motor[1].HomeVel`; `I5198->` → `Coord[1].MaxFeedRate`; `I8010->` → `Sys.ServoPeriod`).
- Numbering: motor `Mxxyy` style → `I0`–`I99` reserved Motor 0, `I5000`–`I5099` C.S. 0, global
  setup moved to `I8000`–`I8099`. `I8192`–`I16383` are free general-purpose doubles.
- New elements (extra motors/C.S., new features) have **no** I-variable alias but behave identically.
- Prefer the structure name in new code; treat `I`-numbers as read-only legacy shortcuts.

## Authoritative element list (firmware tables)
The controller's own intellisense tables are the ground truth for *which elements exist*:
- `reference/firmware/ELEMENTS_INDEX.md` — every top structure + element count (75 structures,
  1828 entries; Motor[] 307, Coord[] 231, Sys 151, …).
- `reference/firmware/pp_swtbl1.txt` / `pp_swtbl2.txt` — the full `Structure,Element` lists;
  grep them (e.g. `^Motor,` , `^Coord,`) to confirm an exact element name before using it.
- `reference/firmware/headers/RtGpShm.h` — the C struct view of the same data.
These come from simulator fw 2.3.1.82; the live controller is 2.8.3.0, so verify edge cases
with the MCP `get_response`.

## How to find any element
- Grep the SWREF raw chunks for the structure prefix, e.g. `Motor[`, `Coord[`, `Sys.`,
  `Gate3[`, `EncTable[`, `CompTable[`, in `reference/raw/software-ref/`.
- Persistence class = which chapter the hit lands in: SAVED p79–632, NON-SAVED p633–776,
  STATUS p777–918 (use the page markers in each chunk).
- The alphabetical element index spans `reference/raw/software-ref/p0001-0040.txt` (TOC) — grep
  there for an element name to get its page, then open the matching `pXXXX-YYYY.txt`.

## Deeper detail (exact raw chunks)
- Addressing model & index rules, top-structure list: `reference/raw/user-manual/p0541-0560.txt`
  (UM p552–554).
- SAVED chapter intro + accessory aliases: `reference/raw/software-ref/p0061-0080.txt` (SWREF p79–80).
- Structure-by-structure TOC (page numbers per structure/class):
  `reference/raw/software-ref/_toc.txt` (search "Saved/Non-Saved/Status Data Structure Elements").
- `Sys.` saved elements SWREF p605 → `p0581-0600.txt`/`p0601-0620.txt`; `Motor[x]` saved p417;
  `Coord[x]` saved p138; `Gate3[i]` (grep `Gate3[`).
