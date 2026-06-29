---
title: C API 실제 시그니처 (gplib.h / RtGpShm.h)
aliases: [C API]
tags: [powerpmac/c, type/reference]
domain: c
status: stable
updated: 2026-06-29
---

# Power PMAC C-API 레퍼런스

Power PMAC C 코드(CPLC, capp/백그라운드 C 앱, CfromScript)를 작성할 때 사용하는
실제 C API. `reference/firmware/headers/` 안의 컨트롤러 펌웨어 헤더에서 **원문 그대로**
추출함. 시그니처(signature)는 있는 그대로 복사 — doc 주석과 프로토타입이 다를 경우
프로토타입이 우선함.

**두 가지 실행 컨텍스트**(헤더는 공유하지만 사용 가능한 함수가 다름):
- **백그라운드 C 앱(`capp`)** — Linux 프로세스. 전체 `gplib.h` 통신(comms) API
  (`GetResponse`, `Command`, `InitLibrary`…)를 사용할 수 있음. `InitLibrary()`를 먼저,
  `CloseLibrary()`를 마지막에 반드시 호출해야 함.
- **실시간(real-time) / 백그라운드 CPLC** — 펌웨어 내부에서 실행됨. `gplib.h` 통신 API는
  **사용 불가** (`gplib.h` 10-11행: "사용자 작성 실시간 C PLC에는 제공되지 않음").
  공유 메모리(shared memory) 접근(`pshm->…`)과 `rtpmacapi.h` / `rtpmaclib.h` 함수를 사용할 것.

`Motor[]`/`Coord[]`/`Sys` 스크립트 이름의 권위 있는 요소 목록:
`reference/firmware/ELEMENTS_INDEX.md` 및 `pp_swtbl0-3.txt` 테이블 참조. 아래의 C 구조체(struct)
필드 이름은 스크립트 요소 이름과 대부분 일치하지만 동일하다고 보장되지는 않음 —
`RtGpShm.h`를 grep하여 필드 이름을 확인할 것.

---

## 1. 통신 / 명령 API (`gplib.h`)

백그라운드 C 앱 전용. `<gplib.h>`를 include. 프로토타입 원문(괄호 안은 파일 줄 번호 참조):

```c
// 라이브러리 라이프사이클 (gplib.h:104,110,115)
int  InitLibrary (void);                 // 가장 먼저 호출; ==0 일 때만 계속 진행
void CloseLibrary (void);                // 종료 전 호출
int  ServoRunning(void);                 // 0 = 서보 카운트 미실행, !=0 = 실행 중

// 명령 프로세서: 응답(response) 필요 (gplib.h:144,157)
int GetResponse  (char *pinstr, char *poutstr, size_t outlen, unsigned char EchoMode);
int GetResponseTS(char *pinstr, char *poutstr, size_t outlen, unsigned char EchoMode);
//   반환값: 0=OK, 음수=에러 번호, 1=ESC 전송됨. "$$$"/"reboot"는 처리하지 않음.
//   GetResponse는 모달(modal) 명령을 유지; GetResponseTS는 스레드 안전(thread-safe, 모달 미유지).

// 명령 프로세서: 응답 없음 (gplib.h:167,176)
int Command  (char *pinstr);             // 심볼릭→PMAC 변환; 모달 상태 유지
int CommandTS(char *pinstr);             // 스레드 안전 변형, 모달 유지 없음

// 변수 읽기/쓰기 (gplib.h:186,195,204,213)
int GetPmacVar      (char *pinstr, double *pdata);  // 심볼릭 이름 → 값
int SetPmacVar      (char *pinstr, double  data);   // 심볼릭 이름  = 값
int GetPmacNativeVar(char *pinstr, double *pdata);  // 네이티브 I/M/P/Q/Motor[x].Servo.Kp (심볼 변환 없음)
int SetPmacNativeVar(char *pinstr, double  data);
//   Q/CsGlobal 변수의 경우 문자열 앞에 "&n" (n = CS 번호)을 붙일 것.

// 상태 접근자(accessor) (gplib.h:275,285) — int >=0 반환, 또는 <0 에러 번호
int GetPmacStatus      (char *pinstr);   // 예: "Coord[2].ProgRunning"; 심볼 변환 수행
int GetPmacNativeStatus(char *pinstr);   // 심볼 변환 없음

// 파일 기반 응답(response) (gplib.h:130) — "$$$"/"reboot" 처리; CmdProcData* 필요
int GetResponseFile(char *filepath, FILE *inpfile, FILE *outfile, FILE *errfile,
                    int sendACK, int gpascii2, CmdProcData *pCPD);

// 좌표계 위치/속도 헬퍼 (gplib.h:229-265)
typedef struct coorddata { char axis[4]; double data; } coorddata;  // gplib.h:216
int GetCoordPos    (unsigned cs, struct coorddata *csdata);  // csdata: 크기 32 배열
int GetCoordVel    (unsigned cs, struct coorddata *csdata);
int GetCoordFError (unsigned cs, struct coorddata *csdata);
int GetCoordDesPos (unsigned cs, struct coorddata *csdata);  // CS 내 축 수 반환, 없으면 0
int GetCoordAxisData(int maxaxis, char *axis, struct coorddata *csdata, double *pdata);

// 정보 문자열 (gplib.h:72-96) — 내부 문자열 포인터(pointer) 반환
unsigned char *GetErrorStr(unsigned error);  // 에러 번호의 양수 값을 넘길 것
unsigned char *GetVersStr (void);            // == "VERS"
unsigned char *GetDateStr (void);            // == "DATE"
unsigned char *GetCIDStr  (void);            // == "CID"
unsigned char *GetTYPEStr (void);            // == "TYPE"

// 기타 (gplib.h:290,547,553,564-566)
void WaitNServoCounts(int n);
int  FastSave(void);   int FastLoad(void);              // pp_custom_save.cfg
int  LockSHM(void); int TryLockSHM(void); int UnlockSHM(void);   // SHM 보호
void GetArrayLimits(int *maxmotors,int *maxcoords,int *maxmsgs,int *maxsendfiles,
        int *maxsendfilesize, unsigned *maxgatbufsize,int *maxgatitems,
        unsigned *maxphasegatbufsize,int *maxphasegatitems,int *maxrings);  // gplib.h:358
```

`EchoMode`는 PMAC `echo` 파라미터(비트 플래그). `CmdProcData`는 `cmdprocessor.h`에 정의됨.

**최소 capp 패턴:**
```c
#include <gplib.h>
int main(void) {
    char resp[256];
    if (InitLibrary()) return 1;            // 반드시 성공해야 함
    GetResponse("Motor[1].ActPos", resp, sizeof(resp), 0);
    Command("#1j+");                        // 모터 1 jog
    CloseLibrary();
    return 0;
}
```

---

## 2. 공유 메모리 데이터 접근 (`RtGpShm.h` / `pRtGpShm.h`)

**CPLC**의 주 데이터 경로(capp에서도 사용 가능). 두 헤더 모두 include할 것.
전역 포인터(pointer)는 `pRtGpShm.h`에 선언되고 `RtGpShm.h:3319-3330`에서 `extern`으로 재선언됨:

```c
extern struct SHM     *pshm;     // <-- 주 진입점: Motor/Coord/전역 데이터 전체
extern volatile unsigned *piom;  // IO 메모리 베이스 (게이트 레지스터, Offset* 필드 경유)
extern void           *pushm;    // 사용자 공유 메모리(User shared memory)
extern unsigned char  *pPrgShm;  // 프로그램 메모리
extern void           *pTblShm;  // 테이블 메모리
extern void           *pLHShm;   // 룩어헤드(Lookahead) 메모리
```

**최상위 컨테이너** `typedef struct SHM { … } SHM, *PSHM;` (RtGpShm.h:3111-3305). 주요 멤버:

```c
MotorData Motor[MAX_MOTORS];   // RtGpShm.h:3117   (MAX_MOTORS = 256, 239행)
CoordData Coord[MAX_COORDS];   // RtGpShm.h:3118   (MAX_COORDS = 128, 241행)
double    P[MAX_P];            // RtGpShm.h:3122   전역 P-변수 (MAX_P=65536)
IMData    Idef[MAX_I], Mdef[MAX_M];   // I/M 변수 정의 구조체
PlcData   Plc[MAX_PLC];               // 스크립트 PLC 테이블
ProgData  Prog[MAX_PROG], SubProg[MAX_PROG];
CompData  CompTable[MAX_COMP_TABLES]; CamData CamTable[MAX_CAM_TABLES];
EncData   EncTable[MAX_ENCODERS];     // (구조체 내 첫 번째 선언, 3113행)
GATHER    Gather;                     // 데이터 수집(data-gather) 제어
double    ServoPeriod, PhaseOverServoPeriod, ZeroVelSetPoint;
unsigned  ServoCount, PhaseCount, RtIntPeriod, WdTimer, ...;   // 다수의 Sys.* 필드가 여기 인라인으로 존재
float     CpuTemp;                    // == Sys.CpuTemp   (3172행)
USER_C_CODE UserAlgo;                 // CPLC 등록/타이밍 (3187행, 구조체는 2859행)
unsigned  Status;                     // 전역 상태 비트필드 (union, 3197-3230행)
```

**중요 — `Sys.` 매핑:** 일반적으로 `pshm->Sys` 멤버는 **존재하지 않음**. 스크립트의
`Sys.<x>` 요소 대부분은 `SHM`의 *최상위* 필드로 매핑됨. 예: `Sys.ServoCount` → `pshm->ServoCount`,
`Sys.CpuTemp` → `pshm->CpuTemp`, `Sys.WdTimer` → `pshm->WdTimer`. (`struct GlobalStatus Sys`
멤버는 `#ifdef __G3MACRO__` 아래에만 존재, 3258행.) `Sys.x` 요소의 매핑이 불확실할 경우
`RtGpShm.h`에서 필드 이름을 grep하거나 `rtpmacapi.h`의 `Get/SetGlobalVar()`를 사용할 것.

**표준 접근 예시 (CPLC):**
```c
#include <RtGpShm.h>
#include <pRtGpShm.h>
#include <rtpmacapi.h>
// 모터 3 실제 위치 읽기, jog 속도 설정, 전역 P100 증가
double pos = pshm->Motor[3].ActPos;          // MotorData 필드 (RtGpShm.h:2360)
pshm->Motor[3].JogSpeed = 25.0;              // MotorData 필드 (RtGpShm.h:2408)
pshm->P[100] += 1.0;                          // 전역 P-변수
unsigned sc = pshm->ServoCount;              // == Sys.ServoCount
```

**주요 구조체 typedef (RtGpShm.h)** — 본문은 `grep '^typedef struct <Name>'`으로 확인:
`MotorData` (2162), `CoordData` (1894), `ServoData` (1763), `LocalData` (1803),
`PlcData` (1823), `ProgData` (1830), `KinData` (1836), `IMData` (1623), `DataBlock` (1563),
`EncData` (1500), `CompData`/`CamData`/`BufIoData` (2477/2485/2493), `GATHER` (2822),
`USER_C_CODE` (2859), `GateArray0/1/2/3` (1301/1327/1357/1394), `GateIOStruct` (1450),
`OpenStruct` (2540), `PPmacVarStruct` (2571), `SendCmd`/`SendFile` (2961/2935), `SHM` (3111).

**주요 `MotorData` 필드** (RtGpShm.h:2359-2466) — 스크립트 `Motor[].` 요소 이름과 일치:
`ActPos, ActVel, DesPos, DesVel, PosError, Pos, Pos2, MasterPos, ServoOut, HomePos`,
`JogSpeed, MaxSpeed, HomeVel, HomeOffset`, 상태 비트필드 `AmpEna`(bit19), `HomeComplete`(bit16),
`FeFatal`(bit5), 포인터(pointer) 필드 `*pLimits,*pAmpEnable,*pAmpFault,*pBrakeOut`, 그리고
`MotorMode, MotorStatus, CapturePos, EcatAmpEnable`.
**주요 `CoordData` 필드** (RtGpShm.h:1894-2160): `Program` (ProgData), `Inverse,Forward` (KinData),
`Ldata` (LocalData), `Q[MAX_Q]`, `DesTimeBase, TimeBase, FeedTime, MaxFeedRate`,
`CdPos[32],CdVel[32], TcPos[32]`, 그리고 `Motors[MAX_MOTORS]` 멤버십 배열.

빌드 한계값 (`RtGpShm.h:238-281`): `MAX_MOTORS 256`, `MAX_COORDS 128`, `MAX_P 65536`,
`MAX_I 16384`, `MAX_M 16384`, `MAX_Q 8192`, `MAX_PLC 32`, `MAX_PROG 1023`, `MAX_ENCODERS 768`.

---

## 3. 실시간 API (`rtpmacapi.h`)

**CPLC** 및 capp 모두 사용 가능 (헤더 9행: "PP 펌웨어, 사용자 작성 실시간 C PLC 및
linux C APP에서 사용"). `<rtpmacapi.h>` include. 다수의 함수가 `libppmac_API` 링키지 매크로 사용
(비-Windows에서는 빈 값으로 확장; `rtpmaclib.h:29` 참조). 프로토타입 원문:

```c
// 메모리 포인터(pointer) 게터 (rtpmacapi.h:36-70)
struct SHM*        GetSharedMemPtr(void);     // == pshm
volatile unsigned* GetIOMemPtr(void);         // == piom
void*              GetUserBufferPtr(void);    // == pushm
void*              GetTableBufferPtr(void);
struct GateArray1* GetGate1MemPtr(int index);
struct GateArray2* GetGate2MemPtr(int index);
struct GateArray3* GetGate3MemPtr(int index);
struct GateIOStruct* GetGateIOMemPtr(int index);
void libppmac_API  SetSharedMemPtr(PSHM pShm);

// 프리프로세서 할당 전역 / CS-전역 P, Q 변수 (rtpmacapi.h:78-176)
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

// LocalData*를 통한 지역 (L/R/C/D) 변수 접근 (rtpmacapi.h:200-303)
double* GetLVarPtr(LocalData *Ldata);  double* GetRVarPtr(LocalData *Ldata);
double* GetCVarPtr(LocalData *Ldata);  double* GetDVarPtr(LocalData *Ldata);
double GetLVar(LocalData*,unsigned); double GetRVar(LocalData*,unsigned);
double GetCVar(LocalData*,unsigned); double GetDVar(LocalData*,unsigned);
void   SetLVar(LocalData*,unsigned,double);  void SetRVar(LocalData*,unsigned,double);
void   SetCVar(LocalData*,unsigned,double);  void SetDVar(LocalData*,unsigned,double);

// 모터 읽기 (rtpmacapi.h:311-335)
double GetMotorPos(unsigned mtr);   double GetMotorDesPos(unsigned mtr);
double GetMotorFError(unsigned mtr);double GetMotorVel(unsigned mtr);

// 사용자 버퍼(buffer) 원시 메모리 (rtpmacapi.h:336-339)
void UserGetMem(unsigned char *data,unsigned offset,unsigned bytelength);
void UserSetMem(unsigned char *data,unsigned offset,unsigned bytelength);
void UserGet32bitMem(unsigned *data,unsigned offset,unsigned length32);
void UserSet32bitMem(unsigned *data,unsigned offset,unsigned length32);

// 모터 / 좌표계 액션 호출 (rtpmacapi.h:655-944)
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

// 스레드 간 "Send" 메시지 포트 (rtpmacapi.h:577-633)
void ClrSendPort(unsigned SendPort);  void ClrAllSendPorts(void);
int  Send(unsigned SendPort, char *pinstr);
int  GetSendUnformatted(unsigned number, char *poutstr);
int  GetSend(unsigned number, char *poutstr);
char *GetNextSend(char *pbuf, int *blen, char *next_send, int *nlen);

// 시간 (rtpmacapi.h:638-646)
double GetPmacTimeOfDay(void);  double GetPmacRunTime(void);  double GetCPUClock(void);
int    SizeOfLookAhead(void);   int Verbose(void);  // (rtpmacapi.h:849-850)
```

**캡처/비교(Capture/Compare):** 이 헤더들에는 전용 `CaptCompare(...)` 호출이 없음.
캡처/비교 인터럽트는 SHM 내 `USER_C_CODE` 구조체(`RtGpShm.h:2859`)를 통해 등록:
`pCaptCompIntrFunc CaptCompFuncAddr; int CaptCompIntr; char CaptureIntrName[64];` — 모터별
캡처는 `pshm->Motor[n].MotorCaptureStatus` / `CapturePos`로 읽고,
`MacroReadPosCapture(int mtr,int *mdata)` 및 `ClearTrigger(int n)` (rtpmacapi.h:670,869) 사용.
(정확한 캡처/비교 등록 확인: USER_C_CODE, RtGpShm.h 2859-2886행.)

**RT PLC 스레드** (`RtPlcThread.h`): 펌웨어 내부 스케줄러 진입점으로, 사용자 CPLC 코드에서
일반적으로 직접 호출하지 않음:
```c
int  StartPLCThread(void);   void StopPLCThread(void);
void StartRtPLCThread(void); void StopRtPLCThread(void);
extern sem_t *pPlcThread_sem, *pPlcRtiThread_sem;
```
CPLC 등록 및 PLC별 타이밍은 `pshm->UserAlgo` (`USER_C_CODE`)에 있음:
`BgCplc[MAX_PLC]`, `BgCplcTime/MinTime/MaxTime`, `RtiCplc*`, `CFuncRTAddr/CFuncBGAddr` (RtGpShm.h:2859-2886).

---

## 4. 수학 / 문자열 헬퍼 (`rtpmaclib.h`)

커널 모드 부동소수점 + 문자열 라이브러리. CPLC **및** capp 모두 사용 가능
(헤더 21행). `<rtpmaclib.h>` include. 주로 RT 커널을 위한 libc 대체재:

```c
long long libppmac_API fclock(void);   // CPU 클럭 틱 카운터 (rtpmaclib.h:586)
double sqrt,cbrt,qrrt,qnrt, atan,atan2,asin,acos,tan,sin,cos,...,pow,exp,log,log2,log10
double libppmac_API round(double); double libppmac_API rint(double); floor; ceil;
void   libppmac_API sincos(double x,double *s,double *c);
// 문자열/메모리: itostr,utostr,dtostr,itohexstr,itoHEXstr, strcopy,strncopy,strcomp,
//               strsize,strtoupper,strtolower, mem_set,move_mem,memcopy  (rtpmaclib.h:42-165)
```
RT 컨텍스트 내에서는 glibc 대신 이 함수들을 사용하여 커널 FP 환경을 유지할 것.

---

## 5. 상태 & 수집 (`status.h`, `gather.h`)

**`status.h`** — 상태 문자열 테이블 및 일괄 상태 게터:
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
// 문자열 테이블: GLOBAL/COORD/MOTOR/MACRO_STATUS_MSGS[][MAX_STATUS_MSG_LNGTH]
// 항목 수: GLOBAL 32, COORD 32, MOTOR 35, MACRO 32; 메시지 길이 64 (status.h:17-30)
```

**`gather.h`** — SHM 수집 버퍼(buffer) 드레인 실행 (제어는 `pshm->Gather`, 구조체 `GATHER`):
```c
void servoGather(void);   // 서보 레이트 수집 (gather.h:25)
void phaseGather(void);   // 페이즈 레이트 수집 (gather.h:26)
```

**`cmdprocessor.h`** — `GetResponseFile`에 전달하는 명령 프로세서 컨텍스트 구조체
`CmdProcData`와 `int PPfputs(char *outstr, CmdProcData *CPD);`를 정의. 주요 필드:
`outfile`, `errfile`, `EchoMode`, `OutputMode`, `Ldata` (LocalData), `respstr`/`respsize` (cmdprocessor.h:24-55).

---

## 헤더 사용 방법

| 필요 상황 | Include | 비고 |
|---|---|---|
| **capp**에서 명령 프로세서와 통신 | `gplib.h` | `InitLibrary()` 먼저; CPLC에서는 사용 불가 |
| `Motor[]/Coord[]/Sys/P` 데이터 읽기/쓰기 | `RtGpShm.h` + `pRtGpShm.h` | `pshm->…` 사용; CPLC의 주 경로 |
| RT 헬퍼 함수 (변수 읽기/쓰기, jog, amp, send) | `rtpmacapi.h` | CPLC와 capp 모두 동작 |
| RT 커널 컨텍스트에서 수학/문자열 | `rtpmaclib.h` | glibc 대신 사용; `fclock()` |
| 상태 디코드 / 일괄 상태 | `status.h` | 문자열 테이블 + `GetAll*Status` |
| 데이터 수집(Data gathering) | `gather.h` (+ `pshm->Gather`) | `GATHER` 구조체: RtGpShm.h:2822 |
| 명령 프로세서 컨텍스트 구조체 | `cmdprocessor.h` | `CmdProcData`, `PPfputs` |

**Grep 포인터(pointer)** (모든 경로는 `reference/firmware/headers/` 하위):
- 통신 API:        `grep -n "GetResponse\|Command\|GetPmacVar\|InitLibrary" gplib.h`
- SHM 포인터:     `grep -n "pshm\|piom\|extern struct SHM" pRtGpShm.h RtGpShm.h`
- 구조체 본문:    `grep -n "^typedef struct" RtGpShm.h`  (이후 해당 줄 범위를 읽을 것)
- 모터 필드:     `grep -n "ActPos\|JogSpeed\|AmpEna\|MotorStatus" RtGpShm.h`
- RT 함수:     `grep -n "(.*);" rtpmacapi.h`
- 빌드 한계값:     `grep -n "#define MAX_" RtGpShm.h`

**권위 있는 스크립트 요소 이름** (`Motor[].x`, `Coord[].x`, `Sys.x`):
`reference/firmware/ELEMENTS_INDEX.md` 및 `reference/firmware/pp_swtbl0-3.txt`.
IDE 생성 테이블(fw 2.3.1.82)이 *이름*의 기준; `RtGpShm.h`의 C 구조체 필드 이름이
*C 접근*의 기준. 대부분 일치하지만 동일하다고 보장되지 않음 —
필드에 접근하는 C 코드를 생성할 경우 `RtGpShm.h`에서 철자를 반드시 확인할 것.

---

## 관련 문서
- [[c-programming|C 프로그래밍 가이드]] — CPLC/capp 작성·빌드
- [[data-structure|데이터 구조]] — pshm으로 접근하는 요소 모델
- [[🗺️ PowerPMAC 지식맵]] — 전체 지식맵(MOC)
