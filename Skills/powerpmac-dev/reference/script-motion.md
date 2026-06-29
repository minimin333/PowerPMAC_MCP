---
title: Script 모션 프로그램
aliases: [Script 모션, 모션 프로그램]
tags: [powerpmac/motion, type/reference]
domain: motion
status: stable
updated: 2026-06-29
---

# Power PMAC — Script 모션 프로그램 (`prog`)

Power PMAC **Script 모션 프로그램** 작성·검토를 위한 레퍼런스: 고정/회전(rotary) 모션 프로그램, 이동(move) 모드, 좌표계(coordinate system) 설정, 키네매틱스(kinematics), 룩어헤드(lookahead), 실행 제어, G-code.
출처: UM = User's Manual, SWREF = Software Reference (PDF 페이지 번호).

> **핵심 원칙:** 모션 프로그램은 특정 좌표계(C.S.)에 귀속되지 **않는다**. 활성 상태의 어떤 C.S.든 어떤 프로그램이든 실행할 수 있으며, C.S.가 모터↔축(axis) 매핑을 제공한다. 모션 프로그램은 *이동(move) 순서로 시퀀싱*된다 — 충분한 이동이 큐에 쌓이면 계산이 자동으로 일시 중단되고, 실행이 진행됨에 따라 재개된다 (UM p681). PLC 프로그램은 이동 시퀀싱이 **아니며** rapid 모드 이동만 명령할 수 있다 (UM p686, UM p712).

---

## 1. 프로그램 정의 & 다운로드

```
open prog 1        // 버퍼 N(1..4294967295)을 열고 + 지움; 별도 clear 불필요 (UM p670)
linear; abs;       // 모달(modal) 설정
ta500 ts0 f5;      // 모달 파라미터
X10; dwell500;     // 이동 / dwell
X0;  dwell500;
close              // 버퍼 닫기; 프로그램 이후 실행 가능
```

- `open prog N` … `close` — N은 단순 숫자 레이블, 우선순위 없음, 최대 1023개 프로그램 (UM p658, p670).
- 해당 프로그램(또는 호출된 서브프로그램)이 **실행 중이거나 일시 중단** 상태이면 버퍼를 열 수 없다; 먼저 `abort`(`a`)를 발행해 해제할 것 (UM p670).
- `open prog N` 바로 다음 `close`(본문 없음) → 프로그램 소멸 (UM p670).
- 선택 인자: `open prog N[,stackoffset][,labeltablesize]` (SWREF p64).
- IDE 소스 레이아웃: Motion Programs / Libraries(서브프로그램) / Kinematics Routines / Global Includes / PLC Programs (UM p673).

### 서브프로그램 & 서브루틴
- `open subprog N` … `close` — `call`로 호출, 직접 run 불가 (UM p671). IDE 형식 `open subprog Name(Arg1,Arg2,&Arg3)`은 인자를 자동으로 `L0,L1,...`에 매핑; `&`-접두어 = 참조 반환(return-by-ref) (UM p675–676). 최대 16개 인자, 255단계 중첩 (UM p662, p676).
- 프로그램 내 서브루틴: 점프 레이블 `N10000:`을 `gosub`/`callsub`으로 도달 (UM p662). `callsub`은 지역 변수를 전달하나 `gosub`은 전달하지 않는다.
- `call123.456` → `subprog 123`의 레이블 `N456000:`으로 이동 (frac × 1e6) (UM p663).

### 회전(Rotary) 모션 프로그램 (각 C.S.의 program 0)
- 실행 중에도 스트리밍/추가(append) 가능 (메모리보다 큰 파트 프로그램용) (UM p658).
- `define rotary {bytes}[,{linebufbytes}]` (최소 2048 B) → `open rotary` (추가 방식, 지우지 않음) → `close rotary`; `clear rotary`는 비움, `delete rotary`는 삭제 (UM p670, SWREF p67).
- 회전 프로그램의 제한: `goto`/점프 레이블 없음, `gosub`/`callsub` 없음, switch/case 없음, 한 줄짜리 `if`/`while`만 가능, `else` 없음 (UM p659–662). 서브프로그램으로의 `call`은 허용된다.

---

## 2. 좌표계(Coordinate-system) 주소 지정 & 실행

- 모달 방식으로 `&x`(예: `&1`)를 지정; 그 후 프로그램 시작 명령이 해당 C.S.에 작용 (`&1r`, `&2a`) (UM p689). 리스트 형식 `&1..3r`, `&2,4,6a`는 모달 C.S.를 바꾸지 않고 나열된 전체에 작용 (UM p689).
- **선택 후 실행** (프로그램 카운터 = "포인터"):
  - `b{N}` / `begin:N` — C.S.를 프로그램 N으로 지정 (소수 부분 × 1e6 = 점프 레이블; `b75.00135` → `N1350:`) (UM p691).
  - `r` / `run` — 현재 위치에서 연속 실행 (UM p691).
  - `s` / `step` — 단일 스텝 (이동 한 번 / `bstart`→`bstop` 블록) (UM p691).
  - `start{N}` / `start:N` — `begin`+`run` 결합 (시작점 지정 + 실행) (UM p691).
- 버퍼드(프로그램 내) 시작/중지는 C.S.를 나열하거나 (`run1`, `abort2`) `Ldata.Coord`를 설정해야 함 (UM p689). 콜론 뒤 인자: `start:500`; 콜론 앞 C.S.: `start3:500` (UM p689).

**프로그램 시작 전제 조건** (UM p690): 모든 축(axis) 모터 활성(`Motor[x].ServoCtrl>0`), 활성화됨, 폐루프; `Coord[x].Csolve=1`(축 정의 풀 수 있음) 또는 순방향 키네매틱(forward-kinematic) 루틴 존재; 양쪽 오버트래블 리미트가 모두 트립된 모터 없음; 프로그램 유효. 모터가 **하나도 할당되지 않은** C.S.도 프로그램 실행 가능("드라이 런").

---

## 3. 이동 모드 (모달 — 변경 전까지 지속) (UM p665, p711, SWREF p70)

| 키워드 | 경로 / 프로파일 | 비고 |
|---|---|---|
| `rapid` | 최단 시간 포인트-투-포인트, 사다리꼴/S-커브 | PLC 프로그램에서 허용되는 유일한 모드; = G00 |
| `linear` | 카르테시안 직선, 사다리꼴 | 리셋 시 기본값; = G01; 블렌드(blend) 됨 |
| `circle1` / `circle2` | **X/Y/Z** 기준 CW / CCW 호 | = G02 / G03; 세그멘테이션(segmentation) 필요 |
| `circle3` / `circle4` | **XX/YY/ZZ** 기준 CW / CCW 호 | 보조 카르테시안 세트 |
| `spline{t}` | 3차 **B-스플라인**, 포물선-속도 | 부드러운 다점 이동; `spline1/2`는 키워드가 아님 |
| `pvt{t}` | **Hermite** 스플라인, 포물선-속도 | 축별 종단 속도 제어 |

키워드를 발행하면 모드 전환; 이후 모든 이동에 해당 모드가 적용됨. PLC 프로그램에서는 어떤 이동 명령이든 선언 모드에 관계없이 rapid 모드로 강제된다 (UM p665).

### 3a. 이동 명령 (UM p667, p711, SWREF p70)
- 기본 (rapid/linear/spline): `{axis}{data}…` — `X10 Y20 Z30`. 괄호 없는 상수, 괄호 안 식(expression): `YY(Target+50)`.
- 같은 줄 = 동시 조율 이동; 별도 줄 = 순차 이동.
- C.S. 내 명령받지 않은 축(axis)은 위치를 유지.
- 한 줄에 축(axis) 문자가 반복되면 새 암묵적 블록이 시작됨: `X5 Y10 X7 Y13` (UM p711).

### 3b. Rapid (UM p712)
```
rapid; inc; X30 Y10;       // 각 모터가 jog; 속도=Motor[x].MaxSpeed 또는 JogSpeed
```
- 속도: `Motor[x].RapidSpeedSel=1`(기본)이면 `Motor[x].MaxSpeed`, 아니면 `Motor[x].JogSpeed`.
- 가속/저크: `Motor[x].JogTa`, `Motor[x].JogTs` (>0 = 시간 ms; <0 = 역수율).
- `Coord[x].RapidVelCtrl=1` → 느린 축이 가장 긴 축(≈직선)에 맞춰 늘어남.
- 블렌드 없음; PLC(`cx`) 이동에서 진입 가능 (UM p720).

### 3c. Linear (UM p722)
```
linear; abs; ta100 td200 ts50; F50;   // F=이송속도(feedrate), tm=이동 시간(ms) (둘 중 하나만)
X100 Y50;
```
- `F{v}`는 벡터 속도(크기, **양수!**)를 설정; `tm{t}`는 이동 시간(ms)을 설정. 둘 다 `Coord[x].Tm`을 씀 (F → 음수값). 음수 F = 시간 모드 → 위험 (UM p724).
- `ta`/`td`/`ts`/`tsd` → `Coord[x].Ta/.Td/.Ts/.Tsd`. `Ts≥Ta`이면 총 가속 = `2*Ts` (UM p727).
- F 단위 = (축 단위)/(`Coord[x].FeedTime` ms). FeedTime 1000→/초, 60000→/분 (UM p724).
- 이송속도 축(feedrate axis) = `frax(...)` (기본 X,Y,Z); `nofrax` = 없음; 이송속도 비대상 축은 `Coord[x].AltFeedRate`로 타이밍 (UM p666, p724).
- 속도/가속/저크 리미트: `Motor[x].MaxSpeed`, `.InvAmax`, `.InvDmax`, `.InvJmax` (UM p727–729).

### 3d. Circle (UM p742)
**벡터**로 중심 지정 (I/J/K = X/Y/Z; II/JJ/KK = XX/YY/ZZ) — 벡터는 시작점→중심 방향:
```
normal K-1;            // 평면 = XY (평면 법선 벡터); J-1=ZX, I-1=YZ (UM p708)
F10; abs; circle1;
X20 Y20 I20 J0;        // 호; I,J = 시작점→중심 성분
```
또는 **반경**으로 지정 (X/Y/Z만 해당, RR 불가): `X20 Y20 R20` (+R = 180° 미만 호, −R = 180° 초과 호; R 명령 하나로 완전한 원 불가) (UM p743). 완전한 원은 `I…J…`에 시작=끝 (예: `I10`만 지정). 세그멘테이션(segmentation) 모드(`Coord[x].SegMoveTime>0`) 필요 (UM p722). `normal`은 코너 블렌드 결정과 2D 커터 보정에서도 평면을 설정한다.

### 3e. PVT (UM p778)
```
pvt200;                // 이동 시간 200 ms (시간 변경 시 pvt 재발행)
X100:-25 Y50:0;        // {axis}{위치}:{종단 속도}  (부호 있는 속도!)
```
- 종단 속도는 부호 있음 (− 방향으로 끝날 경우 음수). 시간은 `pvt{t}` 명령에서만; `ta`/`tm` 무시 (UM p778). 세그먼트를 이어 붙여 임의 프로파일 구성 (UM p779).

### 3f. Spline (UM p785)
```
spline50;                          // 균일 B-스플라인, 전 구간 50 ms
X1000; X1500; X2000;
spline50 spline100 spline150;      // 비균일: T0Spline/T1Spline/T2Spline
```
- 경계에서도 위치/속도/가속이 연속. 프로그래밍된 스플라인은 세그멘테이션되지 않음 (UM p785). 경로가 프로그래밍된 점을 정확히 통과하지 않음 (PVT와 차이).

### 3g. 축(Axis) 모드, dwell/delay (UM p666, p706)
- `abs` / `inc` — 전체 축(axis), 또는 `abs(x,y)`로 나열된 축에만 적용. 리셋 기본값 = abs.
- `dwell{ms}` — 고정 시간 기준, 이송속도 오버라이드 무시, 블렌드/룩어헤드 사전 계산 **중단** (`dwell 0`도 마찬가지) (UM p706).
- `delay{ms}` — 타임베이스 오버라이드를 준수, 사전 계산을 중단하지 않음.
- 트리거까지 이동 (rapid만): `X50^-5` (두 번째 값 = 트리거로부터의 부호 있는 거리) (UM p667, p718). 키네매틱스를 통해서는 지원하지 않음 (UM p719).

---

## 4. 좌표계: 축(Axis) 정의 (UM p501)

축(Axis) 문자: `X Y Z A B C U V W`, 추가로 `AA..HH`, `LL..ZZ` (C.S.당 최대 32개) (UM p501).
X/Y/Z와 XX/YY/ZZ는 두 카르테시안 세트 (원(circle) / 2D 커터 보정은 이것만 가능) (UM p505).

```
&1                     // C.S. 1 주소 지정 (축 정의에 C.S. 리스트 불가)
#1->X                  // 모터 1 = X, 1 모터 단위 당 1 축 단위
#1->10000X             // 스케일링: X 단위당 10000 카운트
#1->10000X+20000       // + 고정 오프셋 (축 원점 vs 모터 홈)
#1->8660.25X-5000Y     // 모터 = 선형 조합 (회전 / 직각도)
#1->X #2->X            // 갠트리: 두 모터 → 같은 축
#4->0                  // NULL 정의 (축 없음, 하지만 타임베이스 & 결함은 공유)
#4->S / S0 / S1        // 스핀들 (CS 타임베이스 / CS0 타임베이스 / 고정 100%)
#1->I                  // 모터가 역 키네매틱(inverse-kinematic) 축
```
- 스케일 팩터는 `Motor[i].CoordSf[j]`에 저장 (j: A=0…ZZ=31, 32=오프셋) (UM p505).
- 모터를 C.S. 간 이동: 먼저 null 처리 (`&1 #4->0` 후 `&2 #4->C`) (UM p503).
- 회전 롤오버: `Coord[x].PosRollover[i]` (i=A,B,C,AA,BB,CC), 보통 360 (UM p506).
- 복수 병렬 좌표계: 각 `&x`는 독립; 같은 프로그램/서브프로그램이 여러 C.S.에서 동시에 실행 가능. `Ldata.Coord`는 공유 서브프로그램에 어떤 C.S.가 호출했는지 알려줌 (UM p701).

**pmatch** — 현재 **모터** 위치를 순방향으로 **축(axis)** 시작 위치로 변환. `r`/`s` 실행 시 내부적으로 자동 호출 (UM p509). PLC 프로그램에서 이동 전, 또는 모션 프로그램 내에서 모터/축 관계를 변경한 후에는 **명시적으로** 호출해야 함 (UM p510, p688).

---

## 5. 키네매틱(Kinematic) 서브루틴 (순방향 / 역방향) (UM p511)

모터↔축 관계가 **비선형**일 때 사용 (로봇, 4/5축 공작기계). 축 정의 구문은 선형 관계만 처리한다.

```
&1 open forward        // (IDE: open forward(1))   — 열고 + 버퍼 지움
  // 입력  : KinPosMotorX = Lx  (모터 명령 위치, 모터 단위)
  // 출력 : KinPosAxisα = C0..C31; 사용된 축의 KinAxisUsed (D0) 비트 설정
  if (KinVelEna > 0) callsub 100;   // 2번째 패스(속도 보고용 &xv/&xf)
  KinAxisUsed = $C0;                // 여기서 X($40)+Y($80)
N100:
  KinPosAxisX = ...(KinPosMotor1)...;
  KinPosAxisY = ...;
return
close

&1
#1->I #2->I            // 역 키네매틱 모터 선언
open inverse
  // 입력  : KinPosAxisα = C0..C31 (목표 위치); PVT 속도는 C32..C63에
  // 출력 : KinPosMotorX = Lx  (모터 단위)
  local X2Y2;
  ...
  KinPosMotor1 = ...; KinPosMotor2 = ...;
close
```

- **순방향(forward)** (모터→축): 프로그램 시작 시(시작 축 위치 계산)와 `pmatch`, `&xp/xd/xv/xf/xg`, `pread/dread/vread/fread/dtogread`에서 자동 호출. 올바른 `D0` 마스크 비트를 설정해야 결과가 유지됨 (UM p659, p513).
- **역방향(inverse)** (축→모터): 이동당 한 번(비세그멘트) 또는 세그먼트당 한 번(세그멘트) 자동 호출, `#x->I` 모터용 모터 목표값 생성 (UM p659, p518).
- 축 이름 → C-변수 / D0 비트 대응표: UM p513 (A=C0/$1 … X=C6/$40, Y=C7/$80, Z=C8/$100 … ZZ=C31). 속도 변수 C32..C63 (UM p519).
- 키네매틱 루틴에는 이동 명령이 **없어야** 함 (UM p688). 호출자 구분: `Ldata.Status & $40` = 모션 프로그램에서 호출됨 (아니면 쿼리) (UM p514).
- 잘못된 해에서 정지: `Coord[x].ErrorStatus = 255` 설정 (사용자 오류용 예약값); 쿼리 반환 시 `sqrt(-1)` (NaN) (UM p514). 반복 상한은 `Ldata.GoBack`으로 (기본 10) (UM p517).
- 해당 C.S.에서 모션 또는 PLC 프로그램이 실행 중이면 `forward`/`inverse`를 열 수 없음 (UM p672).

---

## 6. 룩어헤드(Lookahead) (UM p790)

버퍼링된 세그먼트를 앞으로 스캔하여 어떤 모터도 위치/속도/가속 리미트를 위반하지 않도록 경로는 바꾸지 않고 속도만 낮춤; 버퍼를 **역방향**으로 훑어 제때 감속 (UM p790–794). **linear, circle, PVT** 이동에 동작; **세그멘테이션(segmentation) 모드** 필요. 외부 타임베이스가 있거나 빠른 외부 반응이 필요한 경우 룩어헤드 사용 금지 (UM p790).

설정 (UM p791):
1. 모든 모터를 C.S.에 축 정의.
2. 모터별 `Motor[x].MaxPos/.MinPos`, `Motor[x].MaxSpeed`, `Motor[x].InvAmax` 설정; 선택적으로 `Coord[x].MaxFeedrate`.
3. `Coord[x].SegMoveTime` = 10–20 서보 사이클 (ms).
4. 크기 산정: 정지 시간 = `MaxSpeed*InvAmax`; 세그먼트 수 = 정지시간/(2*SegMoveTime); `Coord[x].LHDistance` = 세그먼트 수 × 4/3 (올림).
5. 각 리셋 후: 해당 C.S.에 `define lookahead {#segments}` (≥ LHDistance + 백업 세그먼트). 제거 시 `delete lookahead` (SWREF p67).

연동 사항:
- **PVT + 룩어헤드** 지원 (UM p781). 원(circle)의 구심 리미트: `Coord[x].MaxCirAccel`.
- 정지 / 역방향 / 재개 (룩어헤드 버퍼에 작용):
  - `lh\` (온라인 `\`) — **빠른 정지**: 가속 리미트 내 최대 감속; 일시 중단(abort 아님).
  - `lh<` (`<`) — 버퍼 이동을 통해 **역방향(retrace)** 시작 (새 계산 없음).
  - `lh>` (`>`) — **순방향** 재개.
  (UM p669, p692–694, SWREF p75).
- 룩어헤드에서 소프트 리미트 도달 → 일시 중단 & 리미트에서 정지 (≈`\`), retrace 또는 재개 가능; 새 프로그램 전에 `abort` 필요 (UM p793).
- 이송속도 / 타임베이스 오버라이드: `%{val}` 온라인 또는 `Coord[x].DesTimeBase`; `%0`은 모션을 동결 (여전히 "실행 중") (UM p695). 세그멘테이션 이송속도 오버라이드는 타임베이스와 별도.

---

## 7. 실행 제어 (시작 / 정지 / 홀드) (UM p689)

**시작 / 재개**
- `r`/`run`, `s`/`step`, `start{N}`/`start:N`, `b{N}`/`begin`, `resume`, `lh>`/`>` (UM p691–692).

**정지 — 재개 가능** (`Coord[x].ProgActive=1`, `abort`/`begin` 전까지 버퍼 clear 불가):
- `q`/`pause` — 계산된 이동 완료 후, 프로그래밍된 지점에서 정지; `r`/`s`로 재개 (UM p693).
- `h`/`hold` — 피드 홀드(feed-hold): `Coord[x].FeedHoldSlew`에서 타임베이스→0으로 램프 다운; 프로그래밍된 지점 **이외**에서 정지; `r`/`s`/`>`로 재개. `Coord[x].FeedHold`=3 감속, 1 정지, 2 가속 (UM p693).
- `\`/`lh\` — 룩어헤드에서 빠른 정지 (§6 참조) (UM p694).
- `%0` / `DesTimeBase=0` — 보간 동결, 프로그램은 여전히 "실행 중" (UM p695).

**정지 — 재개 불가** (`Coord[x].ProgActive=0`, 카운터가 시작으로 리셋):
- `a`/`abort` — 모든 C.S. 모터를 `Motor[x].AbortTa/.AbortTs`로 제어 감속; 경로 이탈 (UM p695). 결함/버퍼 해제 시 사용.
- `disable` — 즉시 kill (개루프, 앰프 꺼짐); `ddisable` = 지연(브레이크) (UM p696).
- `adisable` — abort 후 지연 kill (UM p697).
- `#*k` / `#*dkill` — 모든 C.S.의 모든 모터 kill (주의: `#3k`는 프로그램이 실행 중인 C.S.의 모터는 kill하지 않음) (UM p697).

**정상 종료 / `return` / `stop`** — 카운터가 프로그램 시작으로 리셋, 계산된 이동 완료, 모터는 마지막 지점에서 폐루프 유지; 버퍼 재사용 가능 (UM p692). 서브프로그램에서 `stop`하면 최상위 시작으로도 리셋됨.

**단일 스텝 / 프로그램 카운터:** `s`/`step`은 이동 한 번(또는 `bstart`→`bstop` 블록)씩 진행; 실행 중인 프로그램도 정지 (UM p691, p693). `list pc` = 카운터 위치부터 코드; `list apc` = abort 지점부터 (SWREF p67).

**관련 상태 플래그** (Coord[x].): `Csolve` (축 풀기 OK), `ProgActive` (재개 가능), `ProgRunning`, `ProgProceeding` (모션 진행 중), `HomeComplete`, `FeedHold`, `LookAheadStop`, `ErrorStatus` (16=런타임 오류, 2=버퍼 오버플로, 255=사용자), `RunTimeError`, `BufferWarn` (UM p683–694). 런타임 오류(방정식 지연) → 모든 C.S. 모터 자동 abort (UM p683).

**조건/흐름:** `if/else`, `switch/case/break/default`, `while`, `do…while`, `continue`, `break`, `goto`, `gosub`, `callsub`, `call`, `return` (SWREF p72). CNC 한 줄 조건: `cexecN`/`cskipN`/`csetN`/`cclrN`은 `Coord[x].Cflags`의 비트 N을 검사/설정; `ccallN`/`cdefN`/`cundefN`은 캔드 사이클(canned-cycle) 모달 호출 (UM p660–664).

---

## 8. G-code (RS-274) — 서브프로그램 호출로 구현 (UM p663, p703)

Power PMAC의 네이티브 이동 구문은 RS-274 호환이며, 문자 코드는 인티그레이터가 작성한 서브프로그램으로 디스패치된다 (UM p700):

| 코드 | 서브프로그램 요소 (기본값) | 점프 레이블 |
|---|---|---|
| `G{n}` | `Coord[x].Gprog` (1000) | `n*1000` (G17→N17000:) |
| `M{n}` | `Coord[x].Mprog` (1001) | `n*1000` |
| `T{n}` | `Coord[x].Tprog` (1002) | `n*1000` |
| `D{n}` | `Coord[x].Dprog` (1003) | `n*1000` |

S/H/F-코드도 `Sprog`/`Hprog`/`Fprog`로 (UM p702–703). 기존 서브프로그램에서 존재하지 않는 레이블로 call하면 서브프로그램의 **맨 위**로 점프 (미구현 코드 처리에 활용) (UM p703). 표준 매핑 (UM p703–711): G00→`rapid`, G01→`linear`, G02→`circle1`, G03→`circle2`, G04→`dwell`/`delay`, G09→`Coord[x].OnceNoBlend=1`, G17/18/19→`normal K-1/J-1/I-1`, G40/41/42→`ccmode0/1/2`, G61/64→`Coord[x].NoBlend=1/0`, G90/91→`abs`/`inc`, G93/94→`InvTimeMode`. 전체 G-code 서브루틴 예시: **UM p703**.

---

## 9. 자주 쓰는 패턴 (검증된 스니펫)

**A. 최소 linear 프로그램** (UM p867, 예시 1 — 원문 그대로):
```
open prog 1    // Open buffer for entry
linear;        // Linear interpolation move mode
abs;           // Absolute move mode
ta500;         // 1/2-second accel/decel time
ts0;           // No S-curve accel/decel time
f5;            // Speed of 5 axis units per time unit
X10;           // Move X-axis to position of 10 units
dwell500;      // Sit here for 1/2-second
X0;            // Move X-axis to position of 0 units
dwell500;      // Sit here for 1/2-second
close
// 실행:  &1 b1r        (C.S.1을 prog 1로 지정 후 실행)
```

**B. 블렌드된 linear + circle 윤곽** (UM p868, 예시 4 — 원문 발췌):
```
f50; ta100; ts50;          // Params for linear & circle moves
linear y13;                // Straight-line move to (1,13)
circle1 x2 y14 i1 j0;      // CW arc to (2,14) about (2,13)
linear x3;                 // Straight-line move to (3,14)
circle1 x4 y13 i0 j-1;     // CW arc to (4,13) about (3,13)
dwell 0;                   // Stop blending and lookahead
```

**C. PVT 세그먼트 구성 블록** (UM p779 — 원문 그대로):
```
inc
pvt200
Y133.333:1000
pvt100
Y100:1000
Y96.667:900
pvt500
Y83.333:0
```

---

## 심화 내용 (수집용 raw 청크)

- **Script 프로그램 작성/실행, 클래스, 흐름 제어, 다운로드, 시작/정지:**
  `reference/raw/user-manual/p0641-0660.txt` (p658–660), `p0661-0680.txt` (p661–681),
  `p0681-0700.txt` (p681–700 — 실행 규칙, 시작/정지 명령).
- **G-code & RS-274:** `p0701-0720.txt` (p700–711). ★ 전체 G00–G95 서브루틴 예시.
- **좌표계 / 축 정의 / 키네매틱스:** `p0501-0520.txt` (p501–520). ★ 숄더-엘보 로봇 순방향+역방향 키네매틱 스니펫 (p515–519) — 수집 유용 소스.
- **이동 모드 궤적:** rapid `p0701-0720.txt` (p712–720); linear `p0721-0740.txt` (p722–730); circle `p0741-0760.txt` (p742–745 예시); PVT `p0761-0780.txt` (p778–781); spline `p0781-0800.txt` (p785–788).
- **룩어헤드(Lookahead):** `p0781-0800.txt` (p790–795 — 간단+상세 설정).
- **명령 요약 (키워드, 버퍼드 이동/모드/속성 명령):**
  `reference/raw/software-ref/p0061-0080.txt` (p64–75). ★ 가장 압축된 키워드 목록.
- **★ 예시 모션 프로그램 (스니펫 수집):** `reference/raw/user-manual/p0861-0880.txt`
  (p867–869 예시 1–4: linear, 스케일/반복, circle 윤곽).

### 갭 / 주의 사항
- `spline1`/`spline2`는 Power PMAC 키워드가 **아님** (`spline{data}`만 사용; circle은 `circle1..4` 있음).
- 3D 커터 보정 (`ccmode3`, `nxyz`, `txyz`) 및 공구반경 보정 코너 지오메트리는 간략 요약만 됨 (UM p760–765) — 여기서는 범위 외; 필요하면 해당 페이지 참조.
- 정확한 `Coord[x]`/`Motor[x]` 요소 범위/기본값: 특정 값을 단언하기 전에 SWREF saved/non-saved 요소 챕터 (SWREF p79+)에서 확인.

---

## 관련 문서
- [[script-plc|Script PLC 프로그램]] — 로직/시퀀싱과의 역할 분담
- [[syntax-rules|구문 규칙]] — 좌표계 변수(Q)·흐름 제어
- [[setup-workflow|셋업 워크플로우]] — 축 정의·모터 브링업
- [[gotchas|Gotchas]] — 모션 안전·소유권·abort 함정
- [[🗺️ PowerPMAC 지식맵]] — 전체 지식맵(MOC)
