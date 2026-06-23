# Power PMAC Script Language — Syntax Rules

Distilled from the Software Reference "COMMAND SYNTAX SUMMARY" (SWREF p57–76) and User's
Manual "Computational Features" (UM p547–566). For exhaustive per-token detail, see the grep
pointers in **Deeper detail** at the end.

## General rules (SWREF p57)
- **Not case sensitive** (`Motor[0].JogSpeed` == `motor[0].jogspeed`).
- **Spaces are not significant**, except where explicitly noted.
- Numeric constants: decimal or exponential (`3.456E5`, `-7.26e-7`); hex must be `$`-prefixed,
  unsigned, integer only (`$ff00` = 65280) (UM p552).
- Intermediate math is **64-bit IEEE-754 double**, regardless of operand storage format (UM p549).
  Special values: `inf`, `-inf`, `nan`; `nan` never compares equal to anything (use `isnan()`).
- Index values for arrays use **square brackets** `[ ]` with a non-negative integer constant or a
  single local variable (no expressions). Variable-number selection uses **parentheses** `( )` —
  e.g. `P(P1)`, `Q(L1)` — which is an array *function*, not a true array (UM p555–556).

## Operators (SWREF p58–59, full spec SWREF p1493–1511)

### Arithmetic
| Token | Meaning |
|---|---|
| `+` `-` `*` `/` | add, subtract/negate, multiply, divide |
| `%` | modulo |
| `<<` `>>` | bit shift left / right |

### Bitwise / logical (bit-by-bit)
| Token | Meaning |
|---|---|
| `&` | bitwise AND |
| `\|` | bitwise OR |
| `^` | bitwise XOR |
| `~` | bitwise invert (unary) |

### Standard assignment (assigns at evaluation time) (SWREF p1499)
`=` `+=` `-=` `*=` `/=` `%=` `&=` `\|=` `^=` `>>=` `<<=` `++` `--`

### Synchronous assignment — assignment is DELAYED until start of execution of the next
programmed move (SWREF p1503). Critical distinction for coordinating data changes with motion.
`==` `+==` `-==` `*==` `/==` `&==` `\|==` `^==` `++=` `--=`

```
Q10 = 5      // standard: Q10 becomes 5 now
Q10 == 5     // synchronous: Q10 becomes 5 when next move begins executing
```
NOTE: `==` is BOTH the synchronous-assign operator (in a statement) AND the equality comparator
(inside a condition). Context disambiguates.

### Conditional comparators (used inside `if`/`while`/`switch` conditions) (SWREF p1507)
| Token | Meaning | Alt token |
|---|---|---|
| `==` | equal | |
| `!=` | not equal | `<>` |
| `<` `>` | less / greater | |
| `<=` `>=` | less-or-equal / greater-or-equal | `!>` (≤), `!<` (≥) |
| `~` | approximately equal | |
| `!~` | approximately not equal | |

### Conditional combinatorial (SWREF p1511)
`&&` logical AND · `\|\|` logical OR · `!` logical NOT

## Functions (name + purpose; FULL list: grep SWREF p59–61)
Functions take argument(s) in **parentheses** and return a numeric value. Usable in both on-line
and buffered commands.

- **Scalar math** (SWREF p59–60): `abs acos acosd acosh asin asind asinh atan atand atan2 atan2d
  atanh cbrt ceil cos cosd cosh exp exp2 floor int isnan ln log log10 log2 madd pow qnrt qrrt
  randx rem rint rnd seed sin sincos sincosd sind sinh sgn sqrt tan tand tanh`. Trig `*d` variants
  use degrees; plain use radians.
- **Vector** (SWREF p60): `sum sumprod vcopy vmadd vscale` — operate on ranges of numbered
  variables (e.g. `sum(&Array(0),512,1)`).
- **Matrix** (SWREF p60): `mdet minv mmadd mminor mmul msolve mtrans`.
- **Transformation matrix** (SWREF p61): `tinit` (set identity), `tprop` (propagate by mult+add).
- **Byte buffer** (SWREF p61): `memcpy memset`.
- **Variable-set copy** (SWREF p61): `dcopy lrcopy rlcopy`.
- **String** (SWREF p61): `sprintf strcpy strncpy strtolower strtoupper strcat strncat strchr
  strrchr strcmp strncmp strspn strcspn strlen strpbrk strstr strtod`.
- **EtherCAT** (SWREF p61): `ecatsdo ecattypedsdo ecatcompletesdo ecatregreadwrite
  ecatslavestate ecatsetslavestatemachine`.

## Variable types (UM p554–566)
Fixed pools, no dynamic allocation. All general-purpose user vars are 64-bit doubles.

| Type | Decl keyword | Scope / lifetime | Count | Notes |
|---|---|---|---|---|
| `P` system global | `global` | Any task, whole controller | 65536 (P0–P65535) | also `Sys.P[i]` / C `pshm->P[i]` |
| `Q` C.S. global | `csglobal` | Per coordinate system; independent set each C.S. | 8192/C.S. | also `Coord[x].Q[i]`; in PLC selected by `Ldata.Coord` |
| `M` pointer | `ptr` | Global; maps to an address / element | 16384 (M0–M16383) | see pointer defs below; not directly usable in C |
| `I` legacy setup | `#define` | Global, saved | — | Turbo-PMAC compatibility alias to saved elements (UM p559) |
| `L` local | `local` | Per top-level program / comms thread | 8192 | stack-based; `Ln` in sub = `Rn` in caller |
| `R` return/stack | (auto) | Local; arg passing to sub | — | `Ri` (caller) = `Li` (callee) = `L(StackOffset+i)` |
| `C` C.S. axis | (auto) | Kinematic subs only | C0–C63 | renamed L-vars: C0=L(MAX_MOTORS); C0–C31 pos, C32–C63 vel |
| `D` non-stack local | `#define` | Per C.S./PLC/thread; NOT on stack | D0–D52+ | hold `read`-command G-code args & axis-query results |
| `Sys.Xdata` | (buffer) | Global user array buffer | user-sized | several formats |

### Direct vs named access
- Direct: letter + integer constant (`P100`, `L55`) or letter + `(expression)` (`Q(L1)`, `R(P5+P6-2)`).
- Named: declared via IDE (`global CycleCount;`, `csglobal LineSpeed;`, `local Temp;`,
  `ptr LaserOn->...`). IDE maps names to numbers starting at the `xVARSTART` offset
  (defaults `PVARSTART=8192 QVARSTART=1024 MVARSTART=8192`). Numbers below the start are safe
  for direct/manual use (UM p556–557).
- Manual define (Turbo-style): `#define TargetPressure P100` / `#define SolenoidOn M233`.

### Pointer (M) variable definitions (UM p560, SWREF M{data}-> p1254+)
```
ptr LaserOn  -> u.io.$A00000.8.1          // I/O address.startbit.numbits
ptr LaserMag -> Gate1[4].Chan[3].Dac[1]   // map onto a data-structure element
```
On-line forms: `M{data}->{address definition}`, `M{data}->{data structure element}`,
`M{data}->` reports current definition.

## Constants
Non-changeable numeric values. Decimal (signed, fractional, exponential) or hex (`$`-prefixed,
unsigned integer). `.001` (no leading 0) and leading zeros (`03`) are accepted (UM p552–553).

## Program structure & line rules
- **Comments**: `//` to end of line; `/* ... */` block (confirmed in source examples, e.g.
  `if (isnan(Var)) abort1; // ...`).
- **Statement separation**: statements may be terminated with `;`. Moves on **separate program
  lines execute sequentially**; multiple axes on the **same line** move coordinated/simultaneously
  (UM p665).
- **Line labels**: `N{constant}:` is a numeric jump-label destination (SWREF p72).
- Program text is entered between `open ...` and `close` (see on-line vs buffered, below).

## Flow control (buffered Script; SWREF p72–73)
```
if ({condition}) {command}                  // single-line
if ({condition}) { {command} ... }          // multi-line block
else {command}   |  else { ... }            // on preceding false IF

while ({condition}) {command}
while ({condition}) { ... }

do { ... } while ({condition})              // executes at least once

switch ({expression}) {
  case {constant}: {command}... [break]
  case {constant}: ...
  default: ...
}
```
Jumps & calls (SWREF p72):
- `goto{data}` — jump to numeric label, no return.
- `gosub{data}` — jump to label with return; no local-var passing.
- `callsub{data}` — jump to label with return; CAN pass local vars.
- `call{data}` — call a subprogram [and label] with return; CAN pass local vars.
- `return` — return to caller.
- `read({letter}[,{letter}...])` — read G-code-style args into D-variables.
- Conditional-flag flow: `cset cclr cexec cskip ccall cdef cundef` (SWREF p73).
- G-code dispatch: `G{data} M{data} T{data} D{data}` call subroutines in `Coord[x].Gprog`/`Mprog`/
  `Tprog`/`Dprog` (SWREF p72).

## On-line vs buffered commands (CRITICAL concept) (SWREF p62, p70)
- **On-line commands** are executed **immediately and discarded** (cannot be listed back). They
  are thread-specific for addressing. Three groups:
  - **Global** (not motor/C.S.-dependent): `$$$` `$$$***` `reboot` `save` `fsave` `fload`
    `backup` `cpu` `vers` `size` `free` `enable plc{list}` `disable plc`, variable get/set
    (`P10`, `P10=5`, `M20->...`), `{element}` / `{element}={expr}`, buffer mgmt
    (`open/close/clear/list prog|plc|subprog`).
  - **Coordinate-system** (act on addressed/`&n` or listed C.S.): `%{constant}` (time base),
    `r q s b stop start a h / \ < >`, `enable disable ddisable adisable`, `Q{data}[=...]`,
    `pmatch`, kinematic/rotary/lookahead buffer mgmt, reporting `&{list}p|d|v|f|t|g|?`.
  - **Motor** (act on addressed/`#n` or listed motor): reporting `p d v f`, `#{list}?`, jogging
    `j+ j- j/ j= j={c} j:{c} j^{c}` (long: `jog...`), `$ hm hmz k dkill out{c}`.
- **Addressing**: `#{constant}` sets modal motor; `&{constant}` sets modal C.S. (per thread).
  Non-modal one-shot: `#{list}`, `#*` (all motors), `&{list}`, `&*` (all C.S.).
- **Buffered Script program commands** are stored in a program/PLC/subprog buffer and executed
  later. Entered with `open prog N` / `open plc N` / `open subprog N` ... `close`. These include
  all move, move-mode, axis-attribute, move-attribute, variable-assignment, flow-control, PLC-
  control, port-comm (`send`), and direct motor/C.S. commands (SWREF p70–75).
- **In a PLC program the only move type is rapid**: commanding any move auto-sets rapid mode,
  even if another mode was just declared (UM p666).

### Buffered move / mode quick reference (SWREF p70–71)
- Move: `{axis}{data}...` (LINEAR/RAPID/SPLINE) · `{axis}{data}:{data}` (PVT) ·
  `{axis}{data}^{data}` (move-until-trigger, RAPID) · CIRCLE arc `X.. Y.. I.. J..` or `... R{data}`.
  Zero-distance: `dwell{data}` (fixed time base), `delay{data}` (variable time base).
- Modes: `linear rapid circle1..4 pvt{data} spline{data}`.
- Axis attrs: `abs inc frax frax2 normal pset pstore pload pclr pmatch`.
- Move attrs: `F{data} tm{data} ta{data} td{data} ts{data}` (and 3D cutter comp `nxyz txyz`).

## Deeper detail (exact raw chunks)
- Operators / functions full spec: `reference/raw/software-ref/p0041-0060.txt` (p49–61 TOC + summary),
  detailed pages SWREF p1493–1580 → `p1481-1500.txt` onward.
- Command categories on-line vs buffered: `reference/raw/software-ref/p0061-0080.txt` (p62–76).
- Variable types, scope, naming, pointers, R/C/D vars: `reference/raw/user-manual/p0541-0560.txt`
  and `p0561-0580.txt` (UM p554–566).
- Move command/mode semantics: `reference/raw/user-manual/p0661-0680.txt` (UM p665–668).
- To find any command's full page: grep `reference/raw/software-ref` for the token (e.g. `^while(`,
  `synchronous`, `callsub`).
