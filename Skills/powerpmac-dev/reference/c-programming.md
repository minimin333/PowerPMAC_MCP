# Power PMAC — C Programming

> For **exact C API signatures** (gplib.h `GetResponse`/`Command`/`GetPmacVar`/`SetPmacVar`, the
> `pshm` shared-memory structs from RtGpShm.h, real-time API) see **`reference/c-api.md`**,
> distilled from the firmware headers in `reference/firmware/headers/`.

C code on Power PMAC runs on a Linux-based motion controller. C executes ~10–20× faster than
Script PLCs and lowers CPU load, at the cost of complexity and no safety net (CPROG p3). All C
code is loaded via **Build and Download All Programs** in the IDE (right-click project name)
(UM p846). WARNING: accessing a nonexistent variable/register raises an error that is **not
cleared until the Power PMAC is rebooted** (CPROG p3, p30).

## C program types (priority order, highest first)

Power PMAC calls user C at six priority levels (UM p845). All are *functions* called by the
built-in scheduler **except** background C applications, which are independent Linux processes.

| Type | Where / when it runs | Entry signature | Enable | IDE location |
|---|---|---|---|---|
| **Capture/Compare ISR** | Highest-prio IRQ on DSPGATE3 capture/compare event; ≥60 kHz | `void CaptCompISR(void)` | `UserAlgo.CaptCompIntr=1` | C Lang → Realtime Routines → `usrcode.c` |
| **User-written phase** | Each phase interrupt, per motor | `void MyPhaseAlg(MotorData *Mptr)` | `Motor[x].PhaseCtrl>0` + select routine | Realtime Routines → `usrcode.c` |
| **User-written servo** | Each servo interrupt, per motor; returns command | `double MyServoAlg(MotorData *Mptr)` | `Motor[x].ServoCtrl>0` + select routine | Realtime Routines → `usrcode.c` |
| **RTI C PLC** | Each real-time interrupt (foreground), after RTI motion calc + Script PLC 0 | `void realtimeinterrupt_plcc()` | `UserAlgo.RtiCplc=1` | C Lang → CPLCs → rticplc → `rtiplcc.c` |
| **Background C PLC** | Between scans of background Script PLCs; up to 32 | `void user_plcc()` | `UserAlgo.BgCplc[n]=1` | C Lang → CPLCs → `bgcplcnn/bgplcnn.c` |
| **Background C app** | Independent Linux process, lowest priority | `int main()` (standard C program) | run/kill as a Linux executable | C Lang → Background Programs |
| **CfromScript** | Called synchronously from Script; not auto-scheduled | `double CfromScript(double a1..a7, LocalData *Ldata)` | callable from RT Script by default | Realtime Routines → `usrcode.c` |

Key scheduling facts:
- **RTI C PLC** runs every `(Sys.RtIntPeriod + 1)` servo interrupts; a cycle is **skipped** if the
  previous RTI's calculations overran. Unlike Script PLC 0 (which resumes where it left off), the
  RTI CPLC **restarts from the top** each cycle (UM p858, CPROG p18).
- **Background C PLCs** run after each enabled background Script PLC scan. The next background
  Script PLC will not run until all enabled BG CPLCs finish a scan **or 100 µs** elapses,
  whichever is less (UM p860). BG CPLCs run interleaved between each Script PLC (CPROG p15).
- **Capture/Compare ISR / phase / servo** are higher priority than the servo loop itself; the ISR
  is the single highest-priority interrupt (UM p845, p849).
- **Background C app** runs only when interrupts and dedicated background tasks have released the
  CPU (UM p845). Only the Linux-level types can do TCP/IP sockets, USB file I/O, logging (CPROG p10).

Per-type quantity limits (CPROG p15, p18, p27): exactly **one** RTI CPLC, **one** CaptCompISR,
**one** CfromScript per controller; up to **32** BG CPLCs; multiple background C apps.

## Build / project layout (`pp_proj`)

C source lives under the project's **C Language** branch in the IDE Solution Explorer (UM p846):
- **Realtime Routines** → `usrcode.c` / `usrcode.h` — phase, servo, CaptCompISR, CfromScript.
  May hold multiple routines.
- **CPLCs** → `rticplc/rtiplcc.c` (RTI), `bgcplcNN/bgplcNN.c` (background, NN = two-digit 00–31).
- **Background Programs** — independent Linux C apps, compiled to `.out` executables.

The IDE ships a built-in **GNU C/C++ cross-compiler** targeting the Power PMAC processor. Build +
load with right-click project → **Build and Download All Programs** (UM p846). On the target,
background app executables land at e.g.
`/var/ftp/usrflash/Project/C Language/Background Programs/<name>.out` (CPROG p10).

Every auto-called RT routine (phase/servo/ISR/CfromScript) needs a matching prototype **and**
`EXPORT_SYMBOL(Name);` in `usrcode.h` (UM p851, p855, p859, p861):
```c
// usrcode.h
void  CaptCompISR(void);                 EXPORT_SYMBOL(CaptCompISR);
void  MyPhaseAlg(struct MotorData *Mptr);EXPORT_SYMBOL(MyPhaseAlg);
double MyServoAlg(struct MotorData *Mptr);EXPORT_SYMBOL(MyServoAlg);
double CfromScript(double,double,double,double,double,double,double,LocalData*);
EXPORT_SYMBOL(CfromScript);
```
RTI CPLC and BG CPLC use fixed names (`realtimeinterrupt_plcc`, `user_plcc`) and do **not** need
EXPORT_SYMBOL in the manual examples (UM p858–860).

## Accessing data structures from C

Every file touching shared memory must start with (UM p846):
```c
#include <RtGpShm.h>
```
This header defines all accessible variables/structures/buffers in shared memory (Shm) for both
real-time (Rt) and general-purpose/background (Gp) code.

Auto-called routines (phase, servo, C PLCs, ISR) **inherit** three predefined pointers (UM p846):
- `pshm` — pointer to the full shared-memory data structure
- `piom` — pointer to I/O (ASIC register) space
- `pushm` — pointer to user-defined buffer memory

Independent background C apps must **declare them explicitly** (UM p846, p866):
```c
volatile struct SHM *pshm;
```

Element names match the Script environment but are **case-sensitive in C** and prefixed `pshm->`
(UM p846). Many Script write-protected elements are simply absent from the C header; any element
present in `RtGpShm.h` is writable (UM p846).

Canonical access patterns (CPROG p31–37):
```c
pshm->MaxRtPlc = 3;            // Script: Sys.MaxRtPlc = 3
pshm->Status   = 3;           // Script: Sys.Status = 3
pshm->Motor[x].ActPos;        // motor element
pshm->Coord[x].InPos;         // coordinate-system element
pshm->ECAT[0].Sync0CycleTime; // EtherCAT  (or #include "../../Include/ECATMap.h")
```

**Script-style P/global variables** in C: define `_PPScriptMode_`, then use names directly or
indexed (CPROG p35–36):
```c
#define _PPScriptMode_
pshm->P[i] = pshm->P[j];
SetPtrVar(MvarNumOrName, value);          // write a pointer/M variable
x = GetPtrVar(MvarNumOrName);             // read a pointer/M variable
```

**User shared-memory buffer** typed access via `pushm` + offset (CPROG p34). Offsets are in units
of the element type; cast `pushm` to the target type:
```c
char         *c = (char*)        pushm + 1000;  // Sys.Cdata[1000]
unsigned int *u = (unsigned int*)pushm + 2000;  // Sys.Udata[2000]
int          *i = (int*)         pushm + 3000;  // Sys.Idata[3000]
float        *f = (float*)       pushm + 4000;  // Sys.Fdata[4000]
double       *d = (double*)      pushm + 5000;  // Sys.Ddata[5000]
```

**Gate/ASIC ICs** — map a structure pointer per IC, then use whole-32-bit-word access with
explicit shift/mask. Declare `volatile`; each access is a full 32-bit bus read (~100 instruction
cycles, so cache reads into a software var) (UM p847–848, CPROG p33):
```c
volatile GateArray3 *Gate3_0 = GetGate3MemPtr(0);   // NULL/0 if IC not detected
Gate3_0->GpioData[0];
Gate3_0->Chan[0].HomeCapt;
Gate3_0->Chan[1].CompA = MyCompPos << 8;
MyTriggerFlag = (Gate3_0->Chan[3].Status & 0x80000) >> 19;
```
Map functions: `GetGate1MemPtr(n)`, `GetGate2MemPtr(n)`, `GetGate3MemPtr(n)`,
`GetGateIoMemPtr(n)` (UM p847). Direct pointer-by-address access via `pshm->OffsetGate3[n]` +
`piom` + register offset is available but harder; the `>> 2` converts byte offsets to word
addressing (UM p848–849).

**Kinematics variables** in C: motors `KinPosMotor[x]`; axes `KinPosAxisX/Y/Z/...` (CPROG p32).

## C API / system command functions (CPROG p37)

```c
int  JogPosition(int n, double x);            // jog motor n to position x
int  JogSpeed(int n, double x);               // jog motor n at speed x
int  JogTrigger(int n, double x, double dx);  // jog to x with trigger offset dx
void KillAllMotors(void);
void KillCoord(int n);                        // kill all motors in C.S. n
void AbortMotor(int n);
void AbortCoord(int n);
int  Command(char *pinstr);                   // send on-line cmd string, no response expected
int  GetResponse(char *pinstr, char *poutstr, size_t outlen, unsigned char EchoMode);
```
`Command()` sends a string to the PMAC command processor exactly as if typed in the terminal
(no response). `GetResponse()` sends a command and captures the returned string into `poutstr`
(max `outlen`); `EchoMode` is the PMAC "echo" parameter controlling response format (CPROG p37).

## Real-time safety rules

Real-time C runs with **no Script-level safety checks** — the compiler will not catch access to
nonexistent memory/hardware, and such errors persist until reboot (CPROG p30). In any
interrupt-priority routine (ISR, phase, servo, RTI/BG CPLC):
- **No infinite/indefinite loops.** Looping that doesn't complete within the cycle gets the PMAC
  "stuck", starves other tasks, and can trip the watchdog. The routine is re-called every cycle by
  the scheduler — do not loop it yourself (UM p853, p857, p858, p860).
- Any loop must be guaranteed to finish inside the RTI/cycle budget (CPROG p5, p15, p18).
- **CaptCompISR additionally:** NO floating-point variables or math, NO P/Q-variables (those are
  floating-point), NO math-library functions. Only integer arithmetic, bit ops, conditionals, and
  assignment. Keep it minimal or the CPU stalls (UM p850, CPROG p28). Reading motor position here
  is pointless — phase-rate `PhaseCapt` is the max; use User Phase instead (CPROG p28).
- Limit hardware-register accesses (~100 cycles each); copy to a software var to reuse, and build
  an output "image" var then write once (UM p847).
- Bad servo/phase code can run a motor away — verify carefully (CPROG p21, p24).

**Background C app** (Linux process) is the *only* place for blocking work, sockets, USB/file I/O,
and large loops (CPROG p10):
- `printf` is allowed but adds CPU load — **debug only**; disable in production (CPROG p11).
- For delays always use **nanosleep**, never busy-wait (CPROG p11).
- Even here, minimize CPU-heavy calls to avoid starving control tasks (CPROG p10).
- Start from Script/PLC: `system "/var/ftp/.../Background Programs/<name>.out"`; stop:
  `system "killall -9 <name>.out"` (CPROG p10).

## CfromScript

Call a C function synchronously from Script — primarily for compute-heavy kinematics (UM p860).
Single function per controller; multiplex via a state argument. The 8th arg (`LocalData *Ldata`)
is appended automatically by Power PMAC — do **not** pass it in the Script call (UM p861).

Declare in `usrcode.c` (+ prototype/EXPORT_SYMBOL in `usrcode.h`) (UM p861):
```c
double CfromScript(double arg1, double arg2, double arg3, double arg4,
                   double arg5, double arg6, double arg7, LocalData *Ldata)
{
    double *R = GetRVarPtr(Ldata);   // R[0]==R0 in caller
    double *L = GetLVarPtr(Ldata);   // L[n] = Motor n pos (kinematics)
    double *C = GetCVarPtr(Ldata);   // C[n] = Axis n pos (kinematics)
    double *D = GetDVarPtr(Ldata);   // D[0]==D0
    // ... calculations ...
    return 0.0;                      // return value MUST be stored by caller
}
```
Call from Script (must pass all 7 doubles even if unused — pass 0; must store the result or it's a
syntax error). Execution of the caller halts until the function returns (UM p862):
```c
open plc 0
P1000 = CfromScript(0,0,0,0,0,0,0);
close
```
Calling from a **background** Script routine requires `UserAlgo.CFunc = 1` (set it in
`global definitions.pmh` or `pp.startup.txt`); RT callers (foreground PLC ≤ `Sys.MaxRtPlc`,
kinematics, motion programs) need no flag (UM p861, CPROG p5). General-purpose user vars
(P, Q, L, R, C, D) are all `double`; convert other-typed elements by copying into one (UM p861).

## Verified snippets

**RTI C PLC** — toggle discrete outputs on a ~1 s pattern (UM p858–859):
```c
#include <RtGpShm.h>
#include <stdio.h>
#include <dlfcn.h>

#define IoCard0Out0_7   *(piom + 0xA0000C/4)
#define IoCard0Out8_15  *(piom + 0xA00010/4)
#define IoCard0Out16_23 *(piom + 0xA00014/4)
#define OutputData(x)   (x << 8)

void realtimeinterrupt_plcc()            // fixed name; enable: UserAlgo.RtiCplc=1
{
    static int i = 0;
    if (i++ > 1000) {                    // > ~1 s from cycle start
        IoCard0Out0_7  = OutputData(0xAA);
        IoCard0Out8_15 = OutputData(0xAA);
        IoCard0Out16_23= OutputData(0xAA);
        if (i > 2000) i = 0;             // restart cycle (NO infinite loop)
    } else {
        IoCard0Out0_7  = OutputData(0x55);
        IoCard0Out8_15 = OutputData(0x55);
        IoCard0Out16_23= OutputData(0x55);
    }
}
```

**Capture ISR** — integer-only; log channel-0 captured position into the user buffer (UM p851):
```c
// Script setup: Gate3[0].IntCtrl=$10000; Sys.Idata[65535]=0; UserAlgo.CaptCompIntr=1
void CaptCompISR(void)                   // fixed name; integers only
{
    volatile GateArray3 *MyFirstGate3IC;
    int *CaptCounter, *CaptPosStore;

    MyFirstGate3IC = GetGate3MemPtr(0);
    MyFirstGate3IC->IntCtrl = 1;                       // clear/re-arm IRQ early
    CaptCounter  = (int *)pushm + 65535;               // Sys.Idata[65535]
    CaptPosStore = (int *)pushm + *CaptCounter + 65536;
    *CaptPosStore = MyFirstGate3IC->Chan[0].HomeCapt;  // 32-bit, 1/256 count
    (*CaptCounter)++;
}
```
`IntCtrl` layout: high byte (16–23) = enable/unmask which channel capture/compare raises the IRQ;
middle byte (8–15) = read-only source (which event fired); low byte (0–7) = status/clear — write 1
to the triggered bit to clear and re-arm (UM p849–850, CPROG p27). Capture bits 0–3 =
`Chan[0..3].PosCapt`; compare bits 4–7 = `Chan[0..3].Equ`. `HomeCapt` units are 1/256 count;
`CompA/CompB` units are 1/4096 count (UM p850–852).

## Deeper detail
- **CPROG (c-programming, primary):** `reference/raw/c-programming/p0001-0020.txt` — overview/p3,
  CfromScript/p5–8, BG C app/p10–13, BG CPLC/p15–16, RTI CPLC/p18, User Servo/p21.
  `p0021-0037.txt` — User Phase/p24, Capture/Compare ISR + IntCtrl bytes/p27–28, C syntax: `pshm->`
  usage/p31, Motor/Coord/ECAT/Kinematics/p32, Gate ptr/p33, user buffer Cdata/Udata/Idata/Fdata/
  Ddata/p34, SetPtrVar/GetPtrVar/p35, `_PPScriptMode_` globals/p36, C system-command funcs/p37.
- **UM (user-manual):** `reference/raw/user-manual/p0841-0860.txt` — priorities/p845, IDE build/
  p846, shared-mem + pshm/piom/pushm/p846, ASIC structs + GetGateNMemPtr/p847–849, CaptComp ISR +
  examples/p849–852, phase/p853–855, servo + multi-motor/p855–858, RTI CPLC/p858, BG CPLC/p860,
  CfromScript decl/p860–861. `p0861-0880.txt` — CfromScript calling/GetR/L/C/DVarPtr/kinematics
  handler/p861–865, BG C app/p866, plus example Script motion/PLC programs/p867–880.
