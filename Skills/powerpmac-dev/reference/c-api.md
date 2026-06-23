# Power PMAC C-API Reference

Real C API for writing Power PMAC C code (CPLC, capp/background C App, CfromScript),
extracted **verbatim** from controller firmware headers in `reference/firmware/headers/`.
Signatures are copied as-is — if a doc comment and a prototype disagree, the prototype wins.

**Two execution contexts** (they share headers but not all functions):
- **Background C App (`capp`)** — a Linux process. May use the full `gplib.h` comms API
  (`GetResponse`, `Command`, `InitLibrary`…). MUST call `InitLibrary()` first, `CloseLibrary()` last.
- **Real-time / background CPLC** — runs inside firmware. `gplib.h` comms API is
  **NOT available** (`gplib.h` line 10-11: "NOT available to user written real time C PLCs").
  Use shared-memory access (`pshm->…`) and the `rtpmacapi.h` / `rtpmaclib.h` functions instead.

Authoritative element list for `Motor[]`/`Coord[]`/`Sys` script names:
`reference/firmware/ELEMENTS_INDEX.md` and the `pp_swtbl0-3.txt` tables. The C struct field
names below mostly match those script element names but are not guaranteed identical — confirm
field names by grepping `RtGpShm.h`.

---

## 1. Communication / command API (`gplib.h`)

Background C Apps only. Include `<gplib.h>`. Prototypes verbatim (file line refs in parens):

```c
// Library lifecycle (gplib.h:104,110,115)
int  InitLibrary (void);                 // call FIRST; proceed only if ==0
void CloseLibrary (void);                // call before exit
int  ServoRunning(void);                 // 0 = servo count not running, !=0 = running

// Command processor: response expected (gplib.h:144,157)
int GetResponse  (char *pinstr, char *poutstr, size_t outlen, unsigned char EchoMode);
int GetResponseTS(char *pinstr, char *poutstr, size_t outlen, unsigned char EchoMode);
//   returns 0=OK, negative=error number, 1=ESC sent. Does NOT handle "$$$"/"reboot".
//   GetResponse retains modal commands; GetResponseTS is thread-safe (no modal retention).

// Command processor: no response (gplib.h:167,176)
int Command  (char *pinstr);             // symbolic->PMAC conversion; retains modal state
int CommandTS(char *pinstr);             // thread-safe variant, no modal retention

// Variable get/set (gplib.h:186,195,204,213)
int GetPmacVar      (char *pinstr, double *pdata);  // symbolic name -> value
int SetPmacVar      (char *pinstr, double  data);   // symbolic name  = value
int GetPmacNativeVar(char *pinstr, double *pdata);  // native I/M/P/Q/Motor[x].Servo.Kp (no symbol xlate)
int SetPmacNativeVar(char *pinstr, double  data);
//   For Q/CsGlobal vars precede string with "&n" (n = CS number).

// Status accessors (gplib.h:275,285) — return int >=0, or <0 error number
int GetPmacStatus      (char *pinstr);   // e.g. "Coord[2].ProgRunning"; does symbol xlate
int GetPmacNativeStatus(char *pinstr);   // no symbol xlate

// File-based response (gplib.h:130) — handles "$$$"/"reboot"; needs a CmdProcData*
int GetResponseFile(char *filepath, FILE *inpfile, FILE *outfile, FILE *errfile,
                    int sendACK, int gpascii2, CmdProcData *pCPD);

// Coordinate-system position/velocity helpers (gplib.h:229-265)
typedef struct coorddata { char axis[4]; double data; } coorddata;  // gplib.h:216
int GetCoordPos    (unsigned cs, struct coorddata *csdata);  // csdata array of size 32
int GetCoordVel    (unsigned cs, struct coorddata *csdata);
int GetCoordFError (unsigned cs, struct coorddata *csdata);
int GetCoordDesPos (unsigned cs, struct coorddata *csdata);  // returns # axes in CS, 0 if none
int GetCoordAxisData(int maxaxis, char *axis, struct coorddata *csdata, double *pdata);

// Info strings (gplib.h:72-96) — return ptr to internal string
unsigned char *GetErrorStr(unsigned error);  // pass POSITIVE value of the error #
unsigned char *GetVersStr (void);            // == "VERS"
unsigned char *GetDateStr (void);            // == "DATE"
unsigned char *GetCIDStr  (void);            // == "CID"
unsigned char *GetTYPEStr (void);            // == "TYPE"

// Misc (gplib.h:290,547,553,564-566)
void WaitNServoCounts(int n);
int  FastSave(void);   int FastLoad(void);              // pp_custom_save.cfg
int  LockSHM(void); int TryLockSHM(void); int UnlockSHM(void);   // SHM protection
void GetArrayLimits(int *maxmotors,int *maxcoords,int *maxmsgs,int *maxsendfiles,
        int *maxsendfilesize, unsigned *maxgatbufsize,int *maxgatitems,
        unsigned *maxphasegatbufsize,int *maxphasegatitems,int *maxrings);  // gplib.h:358
```

`EchoMode` is the PMAC `echo` parameter (bit flags). `CmdProcData` is defined in `cmdprocessor.h`.

**Minimal capp pattern:**
```c
#include <gplib.h>
int main(void) {
    char resp[256];
    if (InitLibrary()) return 1;            // MUST succeed
    GetResponse("Motor[1].ActPos", resp, sizeof(resp), 0);
    Command("#1j+");                        // jog motor 1
    CloseLibrary();
    return 0;
}
```

---

## 2. Shared-memory data access (`RtGpShm.h` / `pRtGpShm.h`)

This is the primary data path for **CPLCs** (and also usable in capps). Include both headers.
Global pointers are declared in `pRtGpShm.h` and re-`extern`ed at `RtGpShm.h:3319-3330`:

```c
extern struct SHM     *pshm;     // <-- main entry point: all Motor/Coord/global data
extern volatile unsigned *piom;  // IO memory base (gate registers, via Offset* fields)
extern void           *pushm;    // User shared memory
extern unsigned char  *pPrgShm;  // Program memory
extern void           *pTblShm;  // Table memory
extern void           *pLHShm;   // Lookahead memory
```

**Top-level container** `typedef struct SHM { … } SHM, *PSHM;` (RtGpShm.h:3111-3305). Key members:

```c
MotorData Motor[MAX_MOTORS];   // RtGpShm.h:3117   (MAX_MOTORS = 256, line 239)
CoordData Coord[MAX_COORDS];   // RtGpShm.h:3118   (MAX_COORDS = 128, line 241)
double    P[MAX_P];            // RtGpShm.h:3122   global P-variables (MAX_P=65536)
IMData    Idef[MAX_I], Mdef[MAX_M];   // I/M variable definition structs
PlcData   Plc[MAX_PLC];               // script PLC table
ProgData  Prog[MAX_PROG], SubProg[MAX_PROG];
CompData  CompTable[MAX_COMP_TABLES]; CamData CamTable[MAX_CAM_TABLES];
EncData   EncTable[MAX_ENCODERS];     // (declared first in struct, line 3113)
GATHER    Gather;                     // data-gather control
double    ServoPeriod, PhaseOverServoPeriod, ZeroVelSetPoint;
unsigned  ServoCount, PhaseCount, RtIntPeriod, WdTimer, ...;   // many Sys.* fields are inline here
float     CpuTemp;                    // == Sys.CpuTemp   (line 3172)
USER_C_CODE UserAlgo;                 // CPLC registration/timing (line 3187, struct at 2859)
unsigned  Status;                     // global status bitfield (union, line 3197-3230)
```

**IMPORTANT — `Sys.` mapping:** there is generally **no `pshm->Sys` member**. Most script
`Sys.<x>` elements map to *top-level* `SHM` fields, e.g. `Sys.ServoCount` -> `pshm->ServoCount`,
`Sys.CpuTemp` -> `pshm->CpuTemp`, `Sys.WdTimer` -> `pshm->WdTimer`. (A `struct GlobalStatus Sys`
member exists only under `#ifdef __G3MACRO__`, line 3258.) When unsure how a `Sys.x` element maps,
grep the field name in `RtGpShm.h` or use `Get/SetGlobalVar()` from `rtpmacapi.h`.

**Canonical access snippet (CPLC):**
```c
#include <RtGpShm.h>
#include <pRtGpShm.h>
#include <rtpmacapi.h>
// read motor 3 actual position, set its jog speed, bump global P100
double pos = pshm->Motor[3].ActPos;          // MotorData field (RtGpShm.h:2360)
pshm->Motor[3].JogSpeed = 25.0;              // MotorData field (RtGpShm.h:2408)
pshm->P[100] += 1.0;                          // global P-variable
unsigned sc = pshm->ServoCount;              // == Sys.ServoCount
```

**Main struct typedefs (RtGpShm.h)** — grep `^typedef struct <Name>` for the body:
`MotorData` (2162), `CoordData` (1894), `ServoData` (1763), `LocalData` (1803),
`PlcData` (1823), `ProgData` (1830), `KinData` (1836), `IMData` (1623), `DataBlock` (1563),
`EncData` (1500), `CompData`/`CamData`/`BufIoData` (2477/2485/2493), `GATHER` (2822),
`USER_C_CODE` (2859), `GateArray0/1/2/3` (1301/1327/1357/1394), `GateIOStruct` (1450),
`OpenStruct` (2540), `PPmacVarStruct` (2571), `SendCmd`/`SendFile` (2961/2935), and `SHM` (3111).

**Selected `MotorData` fields** (RtGpShm.h:2359-2466) — names match script `Motor[].` elements:
`ActPos, ActVel, DesPos, DesVel, PosError, Pos, Pos2, MasterPos, ServoOut, HomePos`,
`JogSpeed, MaxSpeed, HomeVel, HomeOffset`, status bitfields `AmpEna`(bit19), `HomeComplete`(bit16),
`FeFatal`(bit5), pointer fields `*pLimits,*pAmpEnable,*pAmpFault,*pBrakeOut`, plus
`MotorMode, MotorStatus, CapturePos, EcatAmpEnable`.
**Selected `CoordData` fields** (RtGpShm.h:1894-2160): `Program` (ProgData), `Inverse,Forward` (KinData),
`Ldata` (LocalData), `Q[MAX_Q]`, `DesTimeBase, TimeBase, FeedTime, MaxFeedRate`,
`CdPos[32],CdVel[32], TcPos[32]`, and `Motors[MAX_MOTORS]` membership array.

Build limits (`RtGpShm.h:238-281`): `MAX_MOTORS 256`, `MAX_COORDS 128`, `MAX_P 65536`,
`MAX_I 16384`, `MAX_M 16384`, `MAX_Q 8192`, `MAX_PLC 32`, `MAX_PROG 1023`, `MAX_ENCODERS 768`.

---

## 3. Real-time API (`rtpmacapi.h`)

For **CPLCs** and capps (header line 9: "for use in PP F/W, user written real time C PLCs and
linux C APPs"). Include `<rtpmacapi.h>`. Many use the `libppmac_API` linkage macro (expands empty
on non-Windows; see `rtpmaclib.h:29`). Prototypes verbatim:

```c
// Memory pointer getters (rtpmacapi.h:36-70)
struct SHM*        GetSharedMemPtr(void);     // == pshm
volatile unsigned* GetIOMemPtr(void);         // == piom
void*              GetUserBufferPtr(void);    // == pushm
void*              GetTableBufferPtr(void);
struct GateArray1* GetGate1MemPtr(int index);
struct GateArray2* GetGate2MemPtr(int index);
struct GateArray3* GetGate3MemPtr(int index);
struct GateIOStruct* GetGateIOMemPtr(int index);
void libppmac_API  SetSharedMemPtr(PSHM pShm);

// Preprocessor-assigned global / CS-global P,Q vars (rtpmacapi.h:78-176)
double GetGlobalVar( unsigned varname);            void SetGlobalVar( unsigned varname, double value);
double GetGlobalArrayVar( unsigned arrayname, unsigned index);
void   SetGlobalArrayVar( unsigned arrayname, unsigned index, double value);
double GetCSGlobalVar( unsigned varname, unsigned cs);
void   SetCSGlobalVar( unsigned varname, unsigned cs, double value);
double GetCSGlobalArrayVar( unsigned arrayname, unsigned index, unsigned cs);
void   SetCSGlobalArrayVar( unsigned arrayname, unsigned index, unsigned cs, double value);
double GetPtrVar( unsigned varname);               void SetPtrVar( unsigned varname, double value);
double GetPtrArrayVar( unsigned arrayname, unsigned index);
void   SetPtrArrayVar( unsigned arrayname, unsigned index, double value);
double GetIVar( unsigned num);                     void SetIVar( unsigned num, double value);

// Local (L/R/C/D) variable access via a LocalData* (rtpmacapi.h:200-303)
double* GetLVarPtr(LocalData *Ldata);  double* GetRVarPtr(LocalData *Ldata);
double* GetCVarPtr(LocalData *Ldata);  double* GetDVarPtr(LocalData *Ldata);
double GetLVar(LocalData*,unsigned); double GetRVar(LocalData*,unsigned);
double GetCVar(LocalData*,unsigned); double GetDVar(LocalData*,unsigned);
void   SetLVar(LocalData*,unsigned,double);  void SetRVar(LocalData*,unsigned,double);
void   SetCVar(LocalData*,unsigned,double);  void SetDVar(LocalData*,unsigned,double);

// Motor reads (rtpmacapi.h:311-335)
double GetMotorPos(unsigned mtr);   double GetMotorDesPos(unsigned mtr);
double GetMotorFError(unsigned mtr);double GetMotorVel(unsigned mtr);

// User buffer raw mem (rtpmacapi.h:336-339)
void UserGetMem(unsigned char *data,unsigned offset,unsigned bytelength);
void UserSetMem(unsigned char *data,unsigned offset,unsigned bytelength);
void UserGet32bitMem(unsigned *data,unsigned offset,unsigned length32);
void UserSet32bitMem(unsigned *data,unsigned offset,unsigned length32);

// Motor / coord action calls (rtpmacapi.h:655-944)
int  MotorProgRunning(int n);                  enum progstatus PlcStatus(int num);
int  ClearHomeFlag(int n);  void ClearTrigger(int n);
int  BrakeEnable(int n);    int  AmpEnable(int n);   int AmpDisable(int n);
int  CommandOut(int n, double x);              void AbortMotor(int n);
int  CloseLoopEnable(int n);
void EnableCoord(int n);    void AbortCoord(int n);  void KillCoord(int n);
void DkillCoord(int n);     void AbortDkillCoord(int n);
void KillAllMotors(void);   void DkillAllMotors(void);
int  MotorHomez(int n);     int  MotorHome(int n);
void AbortCoordAll(int n);  void AbortDkillCoordAll(int n);
int  JogSpeed(int n, double x);                int JogPosition(int n, double x);
int  JogTrigger(int n, double x, double dx);
enum progstatus CoordStatus(int num);
int  MotorModeAmpEnable(int n);  int MotorModeAmpDisable(int n);
int  MotorModeJogPosition(int n,double x);  int MotorModeJogSpeed(int n,double x);

// Inter-thread "Send" message ports (rtpmacapi.h:577-633)
void ClrSendPort(unsigned SendPort);  void ClrAllSendPorts(void);
int  Send(unsigned SendPort, char *pinstr);
int  GetSendUnformatted(unsigned number, char *poutstr);
int  GetSend(unsigned number, char *poutstr);
char *GetNextSend(char *pbuf, int *blen, char *next_send, int *nlen);

// Time (rtpmacapi.h:638-646)
double GetPmacTimeOfDay(void);  double GetPmacRunTime(void);  double GetCPUClock(void);
int    SizeOfLookAhead(void);   int Verbose(void);  // (rtpmacapi.h:849-850)
```

**Capture/Compare:** there is no dedicated `CaptCompare(...)` call in these headers. The capture/compare
interrupt is registered via the `USER_C_CODE` struct in SHM (`RtGpShm.h:2859`):
`pCaptCompIntrFunc CaptCompFuncAddr; int CaptCompIntr; char CaptureIntrName[64];` — and per-motor
capture is read with `pshm->Motor[n].MotorCaptureStatus` / `CapturePos`, plus
`MacroReadPosCapture(int mtr,int *mdata)` and `ClearTrigger(int n)` (rtpmacapi.h:670,869).
(verify exact capture/compare registration: USER_C_CODE, RtGpShm.h line 2859-2886.)

**RT PLC threads** (`RtPlcThread.h`): firmware-internal scheduler entry points, not typically called
by user CPLC code:
```c
int  StartPLCThread(void);   void StopPLCThread(void);
void StartRtPLCThread(void); void StopRtPLCThread(void);
extern sem_t *pPlcThread_sem, *pPlcRtiThread_sem;
```
CPLC registration & per-PLC timing live in `pshm->UserAlgo` (`USER_C_CODE`):
`BgCplc[MAX_PLC]`, `BgCplcTime/MinTime/MaxTime`, `RtiCplc*`, `CFuncRTAddr/CFuncBGAddr` (RtGpShm.h:2859-2886).

---

## 4. Math / string helpers (`rtpmaclib.h`)

Kernel-mode floating-point + string library, usable in CPLCs **and** capps
(header line 21). Include `<rtpmaclib.h>`. This is mostly a libc-substitute for the RT kernel:

```c
long long libppmac_API fclock(void);   // CPU clock tick counter (rtpmaclib.h:586)
double sqrt,cbrt,qrrt,qnrt, atan,atan2,asin,acos,tan,sin,cos,...,pow,exp,log,log2,log10
double libppmac_API round(double); double libppmac_API rint(double); floor; ceil;
void   libppmac_API sincos(double x,double *s,double *c);
// string/mem: itostr,utostr,dtostr,itohexstr,itoHEXstr, strcopy,strncopy,strcomp,
//             strsize,strtoupper,strtolower, mem_set,move_mem,memcopy  (rtpmaclib.h:42-165)
```
Use these (not glibc) inside RT context to stay within the kernel FP environment.

---

## 5. Status & gather (`status.h`, `gather.h`)

**`status.h`** — status-string tables and bulk status getters:
```c
enum StatusMsgType { MSG_GLOBAL, MSG_COORD, MSG_MOTOR, MSG_MACRO };   // status.h:15
int  GetStatusString(enum StatusMsgType type, unsigned number, unsigned bitnumber, char *msg);
void GetGlobalStatus(unsigned *status);        void GetGlobalStatusErrors(unsigned *status);
void GetAllMotorStatus(unsigned *count, unsigned number[], unsigned status[]);
void GetAllMotorStatusErrors(unsigned *count, unsigned number[], unsigned status[]);
void GetAllCoordStatus(unsigned *count, unsigned status[]);
void GetAllCoordStatusErrors(unsigned *count, unsigned status[]);
void GetAllMacroStatus(unsigned *count, unsigned number[], unsigned status[]);
void GetAllMacroStatusErrors(unsigned *count, unsigned number[], unsigned status[]);
// String tables: GLOBAL/COORD/MOTOR/MACRO_STATUS_MSGS[][MAX_STATUS_MSG_LNGTH]
// counts: GLOBAL 32, COORD 32, MOTOR 35, MACRO 32; msg length 64 (status.h:17-30)
```

**`gather.h`** — runs the SHM gather buffer drain (control via `pshm->Gather`, struct `GATHER`):
```c
void servoGather(void);   // servo-rate gather (gather.h:25)
void phaseGather(void);   // phase-rate gather (gather.h:26)
```

**`cmdprocessor.h`** — defines `CmdProcData` (the command-processor context struct passed to
`GetResponseFile`) and `int PPfputs(char *outstr, CmdProcData *CPD);`. Key fields: `outfile`,
`errfile`, `EchoMode`, `OutputMode`, `Ldata` (LocalData), `respstr`/`respsize` (cmdprocessor.h:24-55).

---

## How to use these headers

| Need | Include | Notes |
|---|---|---|
| Talk to command processor from a **capp** | `gplib.h` | `InitLibrary()` first; not for CPLCs |
| Read/write `Motor[]/Coord[]/Sys/P` data | `RtGpShm.h` + `pRtGpShm.h` | use `pshm->…`; primary CPLC path |
| RT helper fns (var get/set, jog, amp, send) | `rtpmacapi.h` | works in CPLC and capp |
| Math/string in RT kernel context | `rtpmaclib.h` | use instead of glibc; `fclock()` |
| Status decode / bulk status | `status.h` | string tables + `GetAll*Status` |
| Data gathering | `gather.h` (+ `pshm->Gather`) | `GATHER` struct in RtGpShm.h:2822 |
| Command-processor context struct | `cmdprocessor.h` | `CmdProcData`, `PPfputs` |

**Grep pointers** (all paths under `reference/firmware/headers/`):
- Comms API:        `grep -n "GetResponse\|Command\|GetPmacVar\|InitLibrary" gplib.h`
- SHM pointers:     `grep -n "pshm\|piom\|extern struct SHM" pRtGpShm.h RtGpShm.h`
- Struct bodies:    `grep -n "^typedef struct" RtGpShm.h`  (then read the line range)
- Motor fields:     `grep -n "ActPos\|JogSpeed\|AmpEna\|MotorStatus" RtGpShm.h`
- RT functions:     `grep -n "(.*);" rtpmacapi.h`
- Build limits:     `grep -n "#define MAX_" RtGpShm.h`

**Authoritative script-element names** (`Motor[].x`, `Coord[].x`, `Sys.x`):
`reference/firmware/ELEMENTS_INDEX.md` and `reference/firmware/pp_swtbl0-3.txt`.
The IDE-generated tables (fw 2.3.1.82) are canonical for *names*; the C struct field names in
`RtGpShm.h` are canonical for *C access*. They usually match but are not guaranteed identical —
when generating C that touches a field, confirm the spelling against `RtGpShm.h`.
