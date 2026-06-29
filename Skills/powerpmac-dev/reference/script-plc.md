---
title: Script PLC 프로그램
aliases: [Script PLC, PLC 프로그램]
tags: [powerpmac/plc, type/reference]
domain: plc
status: stable
updated: 2026-06-29
---

# Power PMAC Script PLC 프로그램 (`plc`)

포그라운드(foreground)/백그라운드(background) 로직 프로그램. C 유사 구문이며, **모션에 의해 순서가 결정되지 않는다**.
사용 목적: 머신 로직, I/O 시퀀싱, 감시, 모션 시작/모니터링, 오류 대응.
인용 표기: (UM p686) = User's Manual PDF 페이지, (SWREF p72) = Software Reference, (TRN p283) = Training PDF 페이지.

## 정의 및 번호 부여

```
open plc 1        // 0..31 번호 지정
// 프로그램 내용
close
```
- **PLC는 최대 32개, 번호 0..31** (TRN p283).
- 번호 대신 이름을 사용할 수 있으며, IDE는 내부 번호를 1부터 자동 할당한다. `enable plc`, `list plc` 사용 시 이름을 쓸 수 있다 (TRN p282):
```
open plc Startup
// 내용
close
```
- `open`/`close` 쌍 하나가 PLC 버퍼 하나를 구성한다. `clear plc N`은 버퍼를 삭제하고, `list plc N`은 내용을 출력한다 (SWREF p64).
- 전원 투입, 리셋, 프로젝트 다운로드 시 **모든 Script PLC는 기본적으로 비활성화** 상태다 (UM p697). 자동 실행을 원하면 `pp_startup.txt`에 `enable plc` 줄을 추가한다.

## 실행 모델 — 반드시 먼저 읽을 것

PLC는 **한 번의 스캔(scan)** = 프로그램 맨 위에서 맨 아래까지 실행하거나, "점프 백(jump back)"(루프 `while` 끝, 또는 이전 줄로의 `goto`)이 발생할 때까지 실행한다 (UM p686).
- 스캔이 **프로그램 끝에 도달**하면 → 다음 스캔은 **맨 위에서 재시작**.
- 스캔이 **점프 백**(참인 `while` 블록의 끝)에서 멈추면 → 다음 스캔은 **점프 백 지점**(해당 루프의 맨 위)에서 재개 (UM p686, TRN p284).
- **PLC는 비활성화될 때까지 자동으로 반복** — "살아있는 상태 유지"를 위한 외부 루프 불필요 (TRN p283/284). 일회성 PLC를 만들려면 마지막 줄에 `disable plc N`을 넣는다 (TRN p284).

**포그라운드(foreground) vs 백그라운드(background)** (UM p686, TRN p283):
- `Sys.MaxRtPlc` (범위 0..3) = **RTI 포그라운드에서 실행되는 가장 높은 번호의 PLC**. 매 RTI마다 모션 프로그램 계산 이후, 활성 포그라운드 PLC 각각이 한 번씩 스캔된다 (PLC 0 먼저).
- `Sys.MaxRtPlc`**보다 번호가 큰 PLC는 백그라운드에서 실행**되며, 인터럽트 태스크가 없을 때 남는 시간에 처리된다. 각 백그라운드 사이클마다 활성 백그라운드 Script PLC 각각이 한 번씩 스캔되고, 그 사이에 활성 C PLC도 한 번씩 실행된다. 이후 CPU는 `Sys.BgSleep` 동안 OS에 양보(yield)된다 (UM p686).
- `Sys.MaxRtPlc` 변경은 해당 PLC가 비활성화된 상태에서만 적용된다 (TRN p283).
- RTI는 `Sys.RtIntPeriod + 1` 서보 인터럽트마다 발생한다 (UM p72).

**태스크 컨텍스트** (UM p72): 포그라운드 PLC는 모션 프로그램 계획, 키네마틱스, 룩어헤드, 모터 안전 점검과 함께 RTI를 공유한다. 백그라운드 PLC는 최하위 우선순위다. 포그라운드 PLC에서 무거운 작업을 수행하면 모션 계산 시간을 빼앗는다.

### 블로킹(blocking)을 하면 안 되는 이유

스캔은 동일 또는 낮은 우선순위의 다른 태스크가 CPU를 얻기 전에 완료까지 실행된다. 무한히 빡빡한 루프는 절대 양보하지 않으며, 다른 PLC와 (포그라운드의 경우) 모션 계산을 굶긴다. "기다리는" 올바른 방법은 **스캔마다 빠져나오는 빈 `while` 루프**를 사용하는 것이다:
```
while (Input1 == 0) {}   // 매 패스에서 여기서 스캔이 끝남; 다음 스캔에서 while 맨 위 재개
```
루프 조건이 참일 때마다 **스캔이 종료**되고, 다른 태스크가 실행되며, 다음 스캔에서 루프를 재검사한다 (UM p688). 이것이 양보(yield)다. 내부에 양보하는 대기나 빠르게 종료되는 본문이 있다면 `while(1){ ...큰 본문... }`도 괜찮지만, 프로그램이 끝나고 자동으로 재시작되도록 하는 방식을 선호한다.
- **`dwell` / `delay`는 PLC 지연으로 작동하지 않는다** — 이들은 모션 시퀀스 명령이다 (UM p688). 아래의 타이머(timer)를 사용한다.

## 흐름 제어 (C 유사) (SWREF p72, TRN p288)

```
if (cond) { ... } else { ... }      // 단일 문이면 중괄호 생략 가능
while (cond) { ... }                // 단일 문이면 중괄호 생략 가능
do { ... } while (cond)             // 반드시 한 번은 실행됨
switch (intExpr) { case 0: ... break; default: ... break; }  // 정수 상태만 가능
goto N    gosub N    callsub N    call SubName(args)   return
N1000:                              // 숫자 줄 레이블
```
연산자 (TRN p285): 산술 `+ - * / %`; 비트 `& | ^ ~ << >>`; 논리 `&& ||`; 비교 `== != > < >= <= !` 및 `~` (근사, 0.5 이내). 대입 `=  += -= *= /= %= &= |= ^= <<= >>=  ++ --`.

### 타이머(timer) / 블로킹 없는 지연

검증된 세 가지 방법:

1. **`Sys.Time`(초)을 사용하는 타이머 서브프로그램** — IDE 관용 패턴 (TRN p292):
```
open subprog Timer(duration)
local EndTime = Sys.Time + duration;   // duration 단위: 초
while (Sys.Time < EndTime) {}          // 매 스캔 양보
close
```
```
call Timer(0.25);   // 0.25초 대기 후 진행
```
2. **`Sys.CdTimer[i]`** 카운트다운 타이머(countdown timer), `i = 0..255`, 단위 **밀리초**, V2.2+ (UM p688):
```
Sys.CdTimer[5] = 750;                   // 750 ms
while (Sys.CdTimer[5] > 0) {}           // 자동으로 카운트다운됨
```
3. **`Sys.RunTime`** (리셋 후 경과 시간, 단위: 초) (UM p688):
```
MyEndTime = Sys.RunTime + MyDelayTime;
while (Sys.RunTime < MyEndTime) {}
```

### 상태 머신(state-machine) 패턴
```
open plc Sequencer
switch (MachineState) {
  case 0: if (StartBtn) { jog+1; MachineState = 1; } break;
  case 1: if (Motor[1].InPos) { MachineState = 2; } break;
  case 2: Output1 = 1; MachineState = 0; break;
}
close
```
외부 루프가 없음에 주목: PLC는 자동으로 재스캔되어 매 패스마다 상태를 전진시킨다.

## PLC에서 명령 발행

jog/home/kill, 축 이동, 변수 대입, 프로그램 제어 등의 버퍼드 Script 명령은 PLC 코드에서 **직접** 발행할 수 있다 (SWREF p70). 핵심 사항: **PLC 실행은 이동에 의해 시퀀스되지 않는다** — 이동 명령은 이동을 *시작*할 뿐이고 스캔은 계속된다 (UM p686). 완료 여부는 직접 감시해야 한다.

**PLC 내의 모터/좌표계(coordinate system) 명령** (TRN p294–298):
```
jog+1;  jog-1;  jog/1;              // jog +/- 무한 / 폐루프 정지
jog1=2000;  jog1:5000;  jog1^5000; // 절대 위치로 / 상대 명령 / 상대 실제값
home 1;  homez 1,2,3;  kill 1;     // home / zero-home / kill
```
**PLC에서 모션 프로그램 시작** — PLC의 모달 좌표계를 설정한 후 프로그램 형식 명령을 발행한다 (UM p690, TRN p241):
```
Ldata.Coord = 1;       // PLC의 모달 좌표계 (전원 투입 기본값 0)
                       // 또는 PLC[n].Ldata.Coord = m 으로 다른 PLC의 좌표계 설정
start:10               // 모션 프로그램 10을 처음부터 시작·실행
run1                   // 좌표계 1 실행; abort2 ; pause ; resume   (좌표계 번호 직접 지정 또는 Ldata.Coord 사용)
```
PLC는 **급속(rapid) 모드 축 이동과 모터 이동**(jog/home)만 명령할 수 있다. 다른 이동 유형(linear/circle/PVT)은 반드시 모션 프로그램에서 와야 한다 (UM p687). PLC는 `pmatch`를 자동으로 수행하지 않으므로, 축 이동 전에 필요하면 `pmatch`를 명시적으로 호출한다 (UM p687).

**`cmd "..."`로 온라인/크로스 스레드 명령 문자열 전송**, 이후 `sendallcmds`로 플러시하고 `Ldata.CmdStatus`를 폴링한다 (UM p778, TRN p581):
```
Ldata.CmdStatus = 1;                       // 설정; 명령 완료 시 클리어됨
cmd "&1 delete lookahead";
cmd "&1 #4->100C";
cmd "&1 define lookahead 10000";
sendallcmds;                               // 명령 실행 보장
do dwell 0; while (Ldata.CmdStatus == 1);  // 완전 실행까지 대기
```

**상태 조회** — 매 스캔마다 상태 요소를 직접 읽는다: `Motor[x].InPos`, `Motor[x].HomeComplete`, `Motor[x].DesVelZero`, `Coord[x].ErrorStatus`, `Plc[i].Active`, `Plc[i].Running`.

## 변수 & I/O

- PLC 간 전역(global): `global Name = 0;` 선언 후 `Name` 그대로 사용 (TRN p287). 레거시 `P`/`M`/`Q`도 사용 가능; `M`은 보통 I/O 레지스터에 매핑된다.
- `local Name;` 및 `local Name(8);` (배열)은 호출별 지역(local) 변수다 (TRN p299/301).
- 매핑된 변수나 데이터 구조 요소로 I/O 접근, 예: `GateIo[0].DataReg[3] = GateIo[0].DataReg[0];` (입력 워드를 출력 워드로 복사) (TRN p303). 예제에서 `Input1`/`Output1`은 사용자 정의 `#define`/M-var 별칭이다.
- **버퍼드 PLC 방식 I/O** (V2.1+): 입력은 스캔 시작 시 홀딩 레지스터로 복사되고, 출력은 스캔 종료 시 기록되며, 최대 4 스캔 디바운스 — UM "Using General-Purpose Digital I/O" 챕터 참조 (UM p687).

## 관용 패턴 (Idioms)

**에지 트리거(edge-trigger) 검출(래치)** — 매 스캔이 아니라 한 번만 동작하도록 모션 명령 앞에 반드시 사용 (TRN p293, **경고**: jog/home/모션에는 항상 에지 트리거 적용):
```
open plc edgetriggered
local Latch1 = Input1;
while (1) {
  if (Input1 == 1) {
    if (Latch1 == 0) { Output1 = 1; Latch1 = 1; }      // 상승 에지(rising edge)
  } else {
    if (Latch1 == 1) { Output1 = 0; Latch1 = 0; }      // 하강 에지(falling edge)
  }
}
close
```
**레벨 트리거(level-triggered)** (조합 논리; 출력에는 안전하지만 모션에는 사용 금지) (TRN p293):
```
if (Input1 == 1) { Output1 = 1; } else { Output1 = 0; }
```
**워치독(watchdog)/안전**: 모션 프로그램은 리미트/추종오차(following error)/amp fault를 트랩할 수 없다 — 그 대응은 반드시 PLC나 호스트에 있어야 한다 (UM p685). 매 스캔마다 `Coord[x].ErrorStatus`(16 = 런타임 오류, 2 = 버퍼 오버플로)를 폴링하고 반응한다(`kill`, `abort`, 출력 설정).

## 검증된 스니펫 (Verified snippets)

**1 — 카운터, 이름 있는 PLC** (TRN p287):
```
global Counter = 0;
open plc increment
Counter++;
close
// 터미널: enable plc increment
```

**2 — 홈 후 jog, 일회성** (TRN p298):
```
open plc jog_home
home 1;                                                   // home 시작
call Timer(0.01);                                         // 명령 적용 대기
while (Motor[1].InPos == 0 || Motor[1].HomeComplete == 0) {}  // 양보하며 대기
jog1=2000;                                                // 2000 카운트 절대 위치로 jog
call Timer(0.01);
while (Motor[1].InPos == 0) {}                            // 안정화 대기
disable plc jog_home                                      // 일회성: 자신 비활성화
close
```

**3 — 입력으로 에지 트리거 jog** (TRN p299):
```
open plc jog_io
Latch1 = Input1;
while (1) {
  if (Input1 == 1) { if (Latch1 == 0) { Latch1 = 1; jog+1; } }   // 상승 에지 → jog
  else            { if (Latch1 == 1) { Latch1 = 0; jog/1; } }    // 하강 에지 → 정지
}
close
```

## 활성화 / 비활성화 / 디버그 명령 (UM p697–699, SWREF p64)

| 명령 | 효과 | 실행 후 상태 |
|---|---|---|
| `enable plc {list}` | 프로그램 **처음**부터 스캔 시작; (재)시작하는 유일한 방법 | `Active=1 Running=1` |
| `disable plc {list}` | 현재 스캔 종료 시 중지; `enable`/`step`으로만 재시작 가능 | `Active=0 Running=0` |
| `pause plc {list}` | 현재 지점에서 중지; `resume`/`step`으로 일시정지 지점부터 재개 | `Active=1 Running=0` |
| `resume plc {list}` | 일시정지/스텝 상태의 PLC를 연속 모드로 재개 (비활성화된 PLC에는 불가) | `Active=1 Running=1` |
| `step plc {list}` | 프로그램 한 줄 실행 (디버그용) | `Active=1 Running=0` |

목록 형식: `enable plc 1..5, 7, 31`. PLC 내부에서 발행된 `disable/pause plc`는 현재 스캔을 끝낸 후 적용된다 (UM p698). 상태 확인은 `Plc[i].Active` / IDE Task Manager 이용 (TRN p283).

## Script PLC vs 모션 프로그램 vs C PLC — 용도별 선택

| | **Script PLC** | **Script 모션 프로그램 (`prog`)** | **C PLC** |
|---|---|---|---|
| 시퀀싱 | 이동 시퀀스 아님; 스캔이 끝/점프 백까지 실행 후 자동 반복 | 이동 시퀀스; 이동 경계에서 계산 일시정지 (UM p686) | Script PLC와 유사; bg 또는 rti |
| 실행 위치 | 포그라운드 (≤`Sys.MaxRtPlc`) 또는 백그라운드 | 좌표계별 RTI 계산 | `enable bgcplc`/`enable rticplc` (SWREF p73) |
| 이동 유형 | 급속 축 이동 + jog/home/kill만 (UM p687) | linear/circle/PVT/spline 및 전부 | API 경유 |
| 이동 시작 후 | 계속 실행, 오류 감시/판단/대응 가능 | 이동 완료까지 일시정지 | 계속 실행 |
| 지연 방법 | 타이머(`Sys.Time`/`CdTimer`/`RunTime`); `dwell` 사용 불가 | `dwell`/`delay` | C 타이밍 |
| 적합 용도 | 머신 로직, I/O, 감시, 오류 처리, 모션 시작/모니터링 | 사전 계획된 이동 시퀀스, 경로, G-code | 고속/고부하 연산 로직 |

**경험칙**: 사전 계획된 모션 → 모션 프로그램. 판단/모니터링/오류 대응/I/O → PLC. 이동을 *시작*하면서 로직이 계속 실행되며 반응해야 할 때 → PLC (UM p687).

## 더 깊은 내용

- PLC 실행 & 스케줄링, 지연, 이동 명령: **UM p686–689** → `reference/raw/user-manual/p0681-0700.txt`.
- PLC 시작/정지 명령 + 상태 비트: **UM p697–699** → 같은 청크.
- 명령 구문 요약 (Program Logic Control, Script PLC Execution Control, 버퍼드 Script 명령): **SWREF p70–73** → `reference/raw/software-ref/p0061-0080.txt`.
- 태스크 모델 (RTI vs 백그라운드, 서보/RTI 태스크 목록): **UM p70–74** → `reference/raw/user-manual/p0061-0080.txt`.
- PLC 예제 (구조, 흐름 제어, 타이머, 에지/레벨, jog/home, 연습): **TRN p281–300** → `reference/raw/training/p0281-0300.txt`; 배열/포인터 I/O 연습 **TRN p303** → `reference/raw/training/p0301-0320.txt`.
- `cmd "..."` / `sendallcmds` / `Ldata.CmdStatus` 패턴: **UM p778** → `reference/raw/user-manual/p0781-0800.txt`; **TRN p581** → `reference/raw/training/p0581-0600.txt`.
- (verify: UM p688) `Sys.CdTimer`는 V2.2+ 펌웨어 필요; 버퍼드 PLC I/O는 V2.1+ 필요.

---

## 관련 문서
- [[script-motion|Script 모션 프로그램]] — 모션 프로그램과의 차이·역할 분담
- [[syntax-rules|구문 규칙]] — 흐름 제어·변수 구문
- [[setup-workflow|셋업 워크플로우]] — 호밍/모터 명령을 PLC에서 발행
- [[gotchas|Gotchas]] — 태스크 계층·블로킹 함정
- [[🗺️ PowerPMAC 지식맵]] — 전체 지식맵(MOC)
