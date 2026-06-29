---
title: C 프로그래밍 가이드 (CPLC/capp)
aliases: [C 프로그래밍, C Programming]
tags: [powerpmac/c, type/reference]
domain: c
status: stable
updated: 2026-06-29
---

# Power PMAC — C 프로그래밍

> **C API의 정확한 시그니처**(gplib.h의 `GetResponse`/`Command`/`GetPmacVar`/`SetPmacVar`,
> RtGpShm.h의 `pshm` 공유 메모리(shared memory) 구조체, 실시간(real-time) API)는 펌웨어 헤더
> `reference/firmware/headers/`에서 추출한 **`reference/c-api.md`**를 참조.

Power PMAC의 C 코드는 Linux 기반 모션 컨트롤러에서 실행된다. C는 Script PLC보다 약 10–20배
빠르고 CPU 부하를 낮추지만, 복잡도가 높고 안전망이 없다 (CPROG p3). 모든 C 코드는 IDE에서
**Build and Download All Programs**(프로젝트 이름 우클릭)으로 로드한다 (UM p846).
경고: 존재하지 않는 변수/레지스터에 접근하면 에러가 발생하며, 이 에러는
**Power PMAC를 재부팅할 때까지 지워지지 않는다** (CPROG p3, p30).

## C 프로그램 유형 (우선순위 순, 높은 순서대로)

Power PMAC는 여섯 가지 우선순위 레벨에서 사용자 C를 호출한다 (UM p845). 백그라운드(background)
C 애플리케이션(독립 Linux 프로세스)을 제외한 모든 유형은 내장 스케줄러가 호출하는 *함수*다.

| 유형 | 실행 위치 / 시점 | 진입 시그니처 | 활성화 방법 | IDE 위치 |
|---|---|---|---|---|
| **Capture/Compare ISR** | DSPGATE3 캡처/비교 이벤트 시 최고 우선순위 IRQ; ≥60 kHz | `void CaptCompISR(void)` | `UserAlgo.CaptCompIntr=1` | C Lang → Realtime Routines → `usrcode.c` |
| **사용자 정의 phase** | 매 phase 인터럽트, 모터별 | `void MyPhaseAlg(MotorData *Mptr)` | `Motor[x].PhaseCtrl>0` + 루틴 선택 | Realtime Routines → `usrcode.c` |
| **사용자 정의 servo** | 매 servo 인터럽트, 모터별; 명령값 반환 | `double MyServoAlg(MotorData *Mptr)` | `Motor[x].ServoCtrl>0` + 루틴 선택 | Realtime Routines → `usrcode.c` |
| **RTI C PLC** | 매 실시간 인터럽트(포그라운드(foreground)), RTI 모션 계산 + Script PLC 0 이후 | `void realtimeinterrupt_plcc()` | `UserAlgo.RtiCplc=1` | C Lang → CPLCs → rticplc → `rtiplcc.c` |
| **백그라운드 C PLC** | 백그라운드 Script PLC 스캔 사이; 최대 32개 | `void user_plcc()` | `UserAlgo.BgCplc[n]=1` | C Lang → CPLCs → `bgcplcnn/bgplcnn.c` |
| **백그라운드 C 앱** | 독립 Linux 프로세스, 최저 우선순위 | `int main()` (표준 C 프로그램) | Linux 실행 파일로 실행/종료 | C Lang → Background Programs |
| **CfromScript** | Script에서 동기 호출; 자동 스케줄링 아님 | `double CfromScript(double a1..a7, LocalData *Ldata)` | 기본적으로 RT Script에서 호출 가능 | Realtime Routines → `usrcode.c` |

주요 스케줄링 특성:
- **RTI C PLC**는 매 `(Sys.RtIntPeriod + 1)` servo 인터럽트마다 실행되며, 이전 RTI 계산이 오버런한
  경우 해당 사이클은 **건너뜀**. Script PLC 0(중단된 지점에서 재개)과 달리 RTI CPLC는 매 사이클
  **처음부터 재시작**한다 (UM p858, CPROG p18).
- **백그라운드 C PLC**는 활성화된 각 백그라운드 Script PLC 스캔 이후 실행된다. 활성화된 모든 BG
  CPLC가 스캔을 완료하거나 **100 µs**가 경과할 때까지(둘 중 짧은 쪽), 다음 백그라운드 Script PLC는
  실행되지 않는다 (UM p860). BG CPLC는 각 Script PLC 사이에 교차 실행된다 (CPROG p15).
- **Capture/Compare ISR / phase / servo**는 servo 루프 자체보다 높은 우선순위를 가지며, ISR은
  단일 최고 우선순위 인터럽트다 (UM p845, p849).
- **백그라운드 C 앱**은 인터럽트와 전용 백그라운드 태스크가 CPU를 해제했을 때만 실행된다 (UM
  p845). TCP/IP 소켓, USB 파일 I/O, 로깅은 Linux 레벨 유형에서만 가능하다 (CPROG p10).

유형별 수량 제한 (CPROG p15, p18, p27): RTI CPLC는 정확히 **하나**, CaptCompISR은 **하나**,
CfromScript는 컨트롤러당 **하나**; BG CPLC는 최대 **32**개; 백그라운드 C 앱은 여러 개 가능.

## 빌드(build) / 프로젝트 레이아웃 (`pp_proj`)

C 소스는 IDE Solution Explorer의 **C Language** 브랜치 아래에 위치한다 (UM p846):
- **Realtime Routines** → `usrcode.c` / `usrcode.h` — phase, servo, CaptCompISR, CfromScript.
  여러 루틴을 담을 수 있다.
- **CPLCs** → `rticplc/rtiplcc.c` (RTI), `bgcplcNN/bgplcNN.c` (백그라운드, NN = 두 자리 00–31).
- **Background Programs** — 독립 Linux C 앱, `.out` 실행 파일로 컴파일.

IDE에는 Power PMAC 프로세서를 타깃으로 하는 내장 **GNU C/C++ 크로스 컴파일러**가 포함된다. 빌드
및 로드는 프로젝트 우클릭 → **Build and Download All Programs** (UM p846). 타깃에서 백그라운드
앱 실행 파일의 경로 예시:
`/var/ftp/usrflash/Project/C Language/Background Programs/<name>.out` (CPROG p10).

자동 호출되는 모든 RT 루틴(phase/servo/ISR/CfromScript)에는 `usrcode.h`에 프로토타입과
`EXPORT_SYMBOL(Name);` **양쪽 모두** 필요하다 (UM p851, p855, p859, p861):
```c
// usrcode.h
void  CaptCompISR(void);                 EXPORT_SYMBOL(CaptCompISR);
void  MyPhaseAlg(struct MotorData *Mptr);EXPORT_SYMBOL(MyPhaseAlg);
double MyServoAlg(struct MotorData *Mptr);EXPORT_SYMBOL(MyServoAlg);
double CfromScript(double,double,double,double,double,double,double,LocalData*);
EXPORT_SYMBOL(CfromScript);
```
RTI CPLC와 BG CPLC는 고정된 이름(`realtimeinterrupt_plcc`, `user_plcc`)을 사용하며, 매뉴얼
예제에서는 EXPORT_SYMBOL이 **불필요**하다 (UM p858–860).

## C에서 데이터 구조 접근

공유 메모리(shared memory)를 사용하는 모든 파일은 반드시 다음으로 시작해야 한다 (UM p846):
```c
#include <RtGpShm.h>
```
이 헤더는 실시간(Rt) 및 범용/백그라운드(Gp) 코드 모두에서 공유 메모리(Shm) 안의 접근 가능한
모든 변수/구조체/버퍼를 정의한다.

자동 호출 루틴(phase, servo, C PLC, ISR)은 미리 정의된 세 포인터를 **상속**한다 (UM p846):
- `pshm` — 전체 공유 메모리 데이터 구조의 포인터
- `piom` — I/O(ASIC 레지스터) 공간의 포인터
- `pushm` — 사용자 정의 버퍼 메모리의 포인터

독립 백그라운드 C 앱은 이를 **명시적으로 선언**해야 한다 (UM p846, p866):
```c
volatile struct SHM *pshm;
```

요소 이름은 Script 환경과 동일하지만 C에서는 **대소문자를 구분**하며 `pshm->`를 앞에 붙인다
(UM p846). Script에서 쓰기 보호된 요소 중 상당수는 C 헤더에 없으며, `RtGpShm.h`에 존재하는
요소는 모두 쓰기 가능하다 (UM p846).

표준 접근 패턴 (CPROG p31–37):
```c
pshm->MaxRtPlc = 3;            // Script: Sys.MaxRtPlc = 3
pshm->Status   = 3;           // Script: Sys.Status = 3
pshm->Motor[x].ActPos;        // 모터 요소
pshm->Coord[x].InPos;         // 좌표계 요소
pshm->ECAT[0].Sync0CycleTime; // EtherCAT  (또는 #include "../../Include/ECATMap.h")
```

**Script 방식 P/전역 변수**를 C에서 사용: `_PPScriptMode_`를 define하면 이름 또는 인덱스로
직접 접근 가능 (CPROG p35–36):
```c
#define _PPScriptMode_
pshm->P[i] = pshm->P[j];
SetPtrVar(MvarNumOrName, value);          // 포인터/M 변수 쓰기
x = GetPtrVar(MvarNumOrName);             // 포인터/M 변수 읽기
```

**사용자 공유 메모리 버퍼** — `pushm` + 오프셋으로 타입 지정 접근 (CPROG p34). 오프셋 단위는
요소 타입 크기; `pushm`을 대상 타입으로 캐스팅:
```c
char         *c = (char*)        pushm + 1000;  // Sys.Cdata[1000]
unsigned int *u = (unsigned int*)pushm + 2000;  // Sys.Udata[2000]
int          *i = (int*)         pushm + 3000;  // Sys.Idata[3000]
float        *f = (float*)       pushm + 4000;  // Sys.Fdata[4000]
double       *d = (double*)      pushm + 5000;  // Sys.Ddata[5000]
```

**Gate/ASIC IC** — IC당 구조체 포인터를 매핑한 뒤 명시적 시프트/마스크로 32비트 워드 단위
접근. `volatile`로 선언; 각 접근이 전체 32비트 버스 읽기(~100 명령 사이클)이므로 소프트웨어
변수에 캐시해서 재사용 (UM p847–848, CPROG p33):
```c
volatile GateArray3 *Gate3_0 = GetGate3MemPtr(0);   // IC 미검출 시 NULL/0
Gate3_0->GpioData[0];
Gate3_0->Chan[0].HomeCapt;
Gate3_0->Chan[1].CompA = MyCompPos << 8;
MyTriggerFlag = (Gate3_0->Chan[3].Status & 0x80000) >> 19;
```
매핑 함수: `GetGate1MemPtr(n)`, `GetGate2MemPtr(n)`, `GetGate3MemPtr(n)`,
`GetGateIoMemPtr(n)` (UM p847). `pshm->OffsetGate3[n]` + `piom` + 레지스터 오프셋을 통한
직접 포인터 주소 접근도 가능하지만 더 복잡하며, `>> 2`로 바이트 오프셋을 워드 주소로 변환한다
(UM p848–849).

**키네매틱스 변수**를 C에서 사용: 모터는 `KinPosMotor[x]`; 축은 `KinPosAxisX/Y/Z/...` (CPROG p32).

## C API / 시스템 명령 함수 (CPROG p37)

```c
int  JogPosition(int n, double x);            // 모터 n을 위치 x로 jog
int  JogSpeed(int n, double x);               // 모터 n을 속도 x로 jog
int  JogTrigger(int n, double x, double dx);  // 트리거 오프셋 dx와 함께 x까지 jog
void KillAllMotors(void);
void KillCoord(int n);                        // 좌표계 n의 모든 모터 kill
void AbortMotor(int n);
void AbortCoord(int n);
int  Command(char *pinstr);                   // 응답 없이 온라인 명령 문자열 전송
int  GetResponse(char *pinstr, char *poutstr, size_t outlen, unsigned char EchoMode);
```
`Command()`는 터미널에서 입력하는 것과 동일하게 문자열을 PMAC 명령 프로세서에 전송한다(응답
없음). `GetResponse()`는 명령을 전송하고 반환된 문자열을 `poutstr`에 캡처한다(최대 `outlen`);
`EchoMode`는 응답 포맷을 제어하는 PMAC "echo" 파라미터다 (CPROG p37).

## 실시간 안전 규칙

실시간 C는 **Script 레벨의 안전 검사 없이** 실행된다 — 존재하지 않는 메모리/하드웨어에 대한
접근을 컴파일러가 잡아내지 못하며, 그러한 에러는 재부팅 전까지 지속된다 (CPROG p30). 인터럽트
우선순위 루틴(ISR, phase, servo, RTI/BG CPLC)에서는:
- **무한/무기한 루프 금지.** 사이클 내에 완료되지 않는 루프는 PMAC를 "정지"시키고, 다른 태스크를
  굶기며, watchdog를 트립시킬 수 있다. 루틴은 스케줄러가 매 사이클 재호출한다 — 직접 루프를
  돌리지 말 것 (UM p853, p857, p858, p860).
- 모든 루프는 RTI/사이클 예산 내에 반드시 완료되도록 보장해야 한다 (CPROG p5, p15, p18).
- **CaptCompISR 추가 제약:** 부동소수점 변수나 연산 금지, P/Q-변수 금지(부동소수점이므로),
  수학 라이브러리 함수 금지. 정수 연산, 비트 연산, 조건문, 대입만 사용. 최소화하지 않으면 CPU가
  멈춘다 (UM p850, CPROG p28). 여기서 모터 위치 읽기는 무의미하다 — phase 율의 `PhaseCapt`가
  최대치이므로, 대신 User Phase를 사용 (CPROG p28).
- 하드웨어 레지스터 접근(각 ~100 사이클)을 최소화; 소프트웨어 변수에 복사해서 재사용하고,
  출력 "이미지" 변수를 빌드한 뒤 한 번에 씀 (UM p847).
- 잘못된 servo/phase 코드는 모터를 폭주시킬 수 있다 — 신중히 검증할 것 (CPROG p21, p24).

**백그라운드 C 앱**(Linux 프로세스)은 블로킹(blocking) 작업, 소켓, USB/파일 I/O, 대형 루프에
유일하게 적합한 곳이다 (CPROG p10):
- `printf`는 허용되지만 CPU 부하를 추가한다 — **디버그 전용**; 운영 시 비활성화 (CPROG p11).
- 지연에는 항상 **nanosleep**을 사용; 바쁜 대기(busy-wait) 금지 (CPROG p11).
- 여기서도 CPU 집약적 호출을 최소화해서 제어 태스크가 굶지 않도록 한다 (CPROG p10).
- Script/PLC에서 시작: `system "/var/ftp/.../Background Programs/<name>.out"`; 정지:
  `system "killall -9 <name>.out"` (CPROG p10).

## CfromScript

Script에서 C 함수를 동기적으로 호출 — 주로 연산이 많은 키네매틱스에 사용한다 (UM p860).
컨트롤러당 함수 하나; 상태 인자로 다중화. 8번째 인자(`LocalData *Ldata`)는 Power PMAC가
자동으로 추가한다 — Script 호출에서 **직접 전달하지 말 것** (UM p861).

`usrcode.c`에 선언 (`usrcode.h`에 프로토타입/EXPORT_SYMBOL 추가) (UM p861):
```c
double CfromScript(double arg1, double arg2, double arg3, double arg4,
                   double arg5, double arg6, double arg7, LocalData *Ldata)
{
    double *R = GetRVarPtr(Ldata);   // R[0]==R0 in caller
    double *L = GetLVarPtr(Ldata);   // L[n] = 모터 n의 위치 (키네매틱스)
    double *C = GetCVarPtr(Ldata);   // C[n] = 축 n의 위치 (키네매틱스)
    double *D = GetDVarPtr(Ldata);   // D[0]==D0
    // ... 계산 ...
    return 0.0;                      // 반환값은 반드시 호출자가 저장해야 함
}
```
Script에서 호출(사용하지 않는 인자도 0으로 채워 7개 double을 모두 전달해야 하며, 결과를 저장하지
않으면 구문 오류). 함수가 반환될 때까지 호출자 실행이 중지된다 (UM p862):
```c
open plc 0
P1000 = CfromScript(0,0,0,0,0,0,0);
close
```
**백그라운드** Script 루틴에서 호출하려면 `UserAlgo.CFunc = 1` 설정이 필요하다
(`global definitions.pmh` 또는 `pp.startup.txt`에 설정); RT 호출자(포그라운드 PLC ≤ `Sys.MaxRtPlc`,
키네매틱스, 모션 프로그램)는 플래그가 불필요하다 (UM p861, CPROG p5). 범용 사용자 변수
(P, Q, L, R, C, D)는 모두 `double`; 다른 타입 요소는 double 변수에 복사해서 변환 (UM p861).

## 검증된 코드 예제

**RTI C PLC** — 약 1 s 패턴으로 디지털 출력 토글 (UM p858–859):
```c
#include <RtGpShm.h>
#include <stdio.h>
#include <dlfcn.h>

#define IoCard0Out0_7   *(piom + 0xA0000C/4)
#define IoCard0Out8_15  *(piom + 0xA00010/4)
#define IoCard0Out16_23 *(piom + 0xA00014/4)
#define OutputData(x)   (x << 8)

void realtimeinterrupt_plcc()            // 고정 이름; 활성화: UserAlgo.RtiCplc=1
{
    static int i = 0;
    if (i++ > 1000) {                    // 사이클 시작 후 ~1 s 초과
        IoCard0Out0_7  = OutputData(0xAA);
        IoCard0Out8_15 = OutputData(0xAA);
        IoCard0Out16_23= OutputData(0xAA);
        if (i > 2000) i = 0;             // 사이클 재시작 (무한 루프 아님)
    } else {
        IoCard0Out0_7  = OutputData(0x55);
        IoCard0Out8_15 = OutputData(0x55);
        IoCard0Out16_23= OutputData(0x55);
    }
}
```

**Capture ISR** — 정수 전용; 채널 0의 캡처 위치를 사용자 버퍼에 기록 (UM p851):
```c
// Script 설정: Gate3[0].IntCtrl=$10000; Sys.Idata[65535]=0; UserAlgo.CaptCompIntr=1
void CaptCompISR(void)                   // 고정 이름; 정수 전용
{
    volatile GateArray3 *MyFirstGate3IC;
    int *CaptCounter, *CaptPosStore;

    MyFirstGate3IC = GetGate3MemPtr(0);
    MyFirstGate3IC->IntCtrl = 1;                       // IRQ 클리어/재암 (조기)
    CaptCounter  = (int *)pushm + 65535;               // Sys.Idata[65535]
    CaptPosStore = (int *)pushm + *CaptCounter + 65536;
    *CaptPosStore = MyFirstGate3IC->Chan[0].HomeCapt;  // 32비트, 1/256 카운트
    (*CaptCounter)++;
}
```
`IntCtrl` 레이아웃: 상위 바이트(16–23) = 어떤 채널 캡처/비교가 IRQ를 발생시킬지 활성화/언마스크;
중간 바이트(8–15) = 읽기 전용 소스(어떤 이벤트가 발생했는지); 하위 바이트(0–7) = 상태/클리어 —
트리거된 비트에 1을 써서 클리어하고 재암 (UM p849–850, CPROG p27). 캡처 비트 0–3 =
`Chan[0..3].PosCapt`; 비교 비트 4–7 = `Chan[0..3].Equ`. `HomeCapt` 단위는 1/256 카운트;
`CompA/CompB` 단위는 1/4096 카운트 (UM p850–852).

## 더 깊은 내용
- **CPROG (c-programming, 주 참고):** `reference/raw/c-programming/p0001-0020.txt` — 개요/p3,
  CfromScript/p5–8, BG C 앱/p10–13, BG CPLC/p15–16, RTI CPLC/p18, User Servo/p21.
  `p0021-0037.txt` — User Phase/p24, Capture/Compare ISR + IntCtrl 바이트/p27–28, C 구문: `pshm->`
  사용법/p31, Motor/Coord/ECAT/키네매틱스/p32, Gate 포인터/p33, 사용자 버퍼 Cdata/Udata/Idata/Fdata/
  Ddata/p34, SetPtrVar/GetPtrVar/p35, `_PPScriptMode_` 전역/p36, C 시스템 명령 함수/p37.
- **UM (user-manual):** `reference/raw/user-manual/p0841-0860.txt` — 우선순위/p845, IDE 빌드/
  p846, 공유 메모리 + pshm/piom/pushm/p846, ASIC 구조체 + GetGateNMemPtr/p847–849, CaptComp ISR +
  예제/p849–852, phase/p853–855, servo + 멀티모터/p855–858, RTI CPLC/p858, BG CPLC/p860,
  CfromScript 선언/p860–861. `p0861-0880.txt` — CfromScript 호출/GetR/L/C/DVarPtr/키네매틱스
  핸들러/p861–865, BG C 앱/p866, Script 모션/PLC 프로그램 예제/p867–880.

---

## 관련 문서
- [[c-api|C API 실제 시그니처]] — gplib.h/RtGpShm.h 함수·구조체
- [[script-plc|Script PLC 프로그램]] — Script PLC와의 대비
- [[project-structure|프로젝트 구조]] — pp_proj/빌드 레이아웃
- [[🗺️ PowerPMAC 지식맵]] — 전체 지식맵(MOC)
