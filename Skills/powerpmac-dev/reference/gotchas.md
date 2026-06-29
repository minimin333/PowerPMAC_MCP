---
title: Gotchas — 코드 안전 체크리스트
aliases: [Gotchas, 함정 모음, 안전 체크리스트]
tags: [powerpmac/safety, type/reference]
domain: safety
status: stable
updated: 2026-06-29
---

# Power PMAC Gotchas — 코드 안전 체크리스트

Power PMAC를 구분 짓는 직관에 어긋나는 특성과 흔한 실수 모음.
항목 형식: **규칙(rule)** → 이유(why) → 해결(fix). 인용 표기 (UM p73) = User's Manual PDF 페이지,
(SWREF p76) = Software Reference. 주장은 모두 출처 기반이며, 불확실한 항목은 "(verify: …)"로 표시.

---

## 1. 태스크 모델 (phase / servo / RTI / background)

- **고정된 4개 우선순위 계층, 높은 순서대로: Phase → Servo → RTI → Background.** 각 계층은 하위 전체를 선점하며,
  background는 남는 시간에만 실행. (UM p77, p548)
- **Phase 클럭(기본 ~9.04 kHz)이 커뮤테이션 + 전류 루프를 돌림.** Servo/MACRO IC가 있을 때만 존재 —
  CK3E/IPC(EtherCAT 전용)는 phase 태스크가 **전혀 없음**. (UM p70, p547)
- **Servo 클럭(기본 ~2.26 kHz = phase/4)이 ECT, 보간, 위치/속도 루프, 모터 안전 상태**(추종오차,
  des-vel-zero, in-position)를 돌림. (UM p70–71)
- **RTI는 (Sys.RtIntPeriod+1) servo 사이클마다 실행(기본 = 3번째마다).** 모션 프로그램 이동 계획, 포그라운드(RT)
  PLC, lookahead, **그리고** 모터 안전 점검(overtravel, amp fault, encoder loss, I2T, 브레이크 지연)을 수행. (UM p73, p84)
- **Overtravel/amp-fault/encoder-loss/I2T 점검은 servo 율이 아니라 RTI 율로 일어남.** 결함은 한 RTI 주기 안에
  잡히며 즉시는 아님. RTI 주파수를 높이면(Sys.RtIntPeriod 낮춤) 검출이 빨라지나 CPU를 더 씀. (UM p73)
- **Sys.MotorsPerRtInt > 0 이면 매 RTI마다 모든 모터를 점검하지 않음.** 초고속 블록율 응용에서 사용 — LimitLimit,
  EncLossLimit, AmpFaultLimit, 브레이크 지연의 실효 스케일/타이밍을 바꿈. 이해하지 못하면 0으로 둘 것. (UM p85, p434)
- **RTI는 초당 40회 이상 돌아야 watchdog가 안 걸림.** RTI가 WDT 카운터를 감소시키고 background가 리셋함.
  Sys.RtIntPeriod를 너무 크게(RTI 너무 느리게) 잡으면 소프트 watchdog 트립. (UM p84, p423)
- **Background = Script PLC(> Sys.MaxRtPlc) + background C PLC + GPOS/C 앱.** 한 BG 사이클은 BG Script PLC
  하나를 한 번 스캔하고, 모든 BG C PLC를 한 번 스캔하며, 모든 BG Script PLC가 한 번씩 돌 때까지 반복 후 하우스키핑. (UM p74–75)
- **PLC 스캔 의미: background Script PLC는 한 번의 전체 패스를 돌고 양보(yield)** — 다음 사이클에 맨 위에서 재진입.
  background PLC에 무한 `while(1)` 루프를 쓰지 말 것; 다른 BG PLC/하우스키핑을 굶기고 watchdog를 트립시킬 수 있음. (UM p74–75)
- **각 BG 사이클 후 스케줄러는 Sys.BgSleepTime(0.25–10 ms, 기본 1 ms)만큼 SLEEP**하여 GPOS/C 앱과 gpascii
  통신에 시간을 줌. 따라서 BG PLC 실효 스캔율 ≈ (BG 작업 + sleep)당 1회. background에서 정밀 타이밍을 가정하지 말 것. (UM p75)
- **Background는 고정 주기가 없음.** 정밀 타이밍을 BG PLC에 의존하지 말 것; servo/RTI 율 메커니즘이나 하드웨어 타이머를 쓸 것. (UM p89)
- **포그라운드(phase/servo/RTI)에서의 블로킹이나 긴 연산은 치명적.** phase 태스크가 오버런하면 Sys.PhaseErrorCtr 증가;
  servo 오버런 → Sys.ServoBusyCtr/ServoErrorCtr; 0이 아닌 ServoErrorCtr/PhaseErrorCtr는 심각한 결함. 포그라운드
  (RT PLC, CPLC) 코드는 짧고 non-blocking으로 유지. (UM p87–88)
- **포그라운드(RTI)에서 도는 Script PLC는 최대 1–4개, Sys.MaxRtPlc(0–3)로 결정.** PLC 0..MaxRtPlc는 RTI에서,
  나머지(최대 PLC 31)는 background에서 실행. PLC의 계층은 그 번호 vs MaxRtPlc로 정해짐. (UM p548)
- **RTI에서 도는 컴파일된 C PLC(rticplc)는 단 하나.** 그 실행 시간은 RTI 시간 통계에 집계되지 않음. (UM p548, p88)
- **멀티코어 CPU는 인터럽트 태스크를 한 코어, background를 다른 코어에서 실행**(기본). 멀티코어에서 BG vs 포그라운드의
  단일코어식 직렬화를 가정하지 말 것. (UM p74, p78)

## 2. SAVE / RESET 라이프사이클

- **RAM은 휘발성, flash는 전원 사이클을 견딤. 다운로드는 프로젝트를 RAM에만 올림.** `save` 없이는 리셋/전원
  사이클에서 모두 사라짐. (UM p61–62)
- **`save`는 활성 SAVED 셋업 요소 + 활성 프로젝트 파일을 flash에 복사.** 저장 요소를 바꾼 뒤 영속시키려면 반드시
  `save` 해야 함. (UM p62, SWREF p62)
- **프로젝트 항목만 저장됨.** 프로젝트 **밖**에서 전송된 프로그램/테이블(예: 임시 CNC 파트 프로그램)은 `save`로 flash에
  복사되지 않음. (UM p62)
- **`$$$` = 리셋: flash의 저장 셋업값 복원, 저장된 프로젝트 재로드, 그 명령들을 재실행.** 활성(미저장) 변경은 사라짐. (UM p62, SWREF p62)
- **리셋 시, 프로젝트 파일 안의 on-line 명령이 저장 요소를 설정하면 방금 flash에서 복원한 값을 덮어씀.** 시작
  PLC/명령이 요소를 설정하면 그 값이 flash 값을 이김. (UM p62)
- **`$$$***` = 재초기화: 모든 저장 요소를 공장 기본값으로 만들고 프로젝트를 로드하지 않음**(프로젝트는 flash에
  남되 로드만 안 됨). 깨끗한 초기 상태를 의도할 때만 사용. (UM p62)
- **마지막 `save` 이후 하드웨어 구성이 바뀌면 자동으로 재초기화가 트리거됨**(Sys.HWChangeErr=1 설정); 저장된
  프로젝트는 로드되지 않음. 카드 추가/제거 → 재구성·`save`·리셋 필요. (UM p62, p65, p69)
- **`reboot`은 하부 Linux도 재시작**한 뒤 `$$$`처럼 마지막 저장 프로젝트를 로드. `$$$`/`$$$***`는 Linux를 재시작하지 않음. (UM p62)
- **세 가지 요소 분류: SAVED 셋업, NON-SAVED 셋업, STATUS.** `save`로 flash에 쓰이는 건 SAVED뿐. STATUS 요소는
  읽기전용 출력 — 절대 쓰지 말 것. NON-SAVED 셋업은 매 리셋마다 재확립해야 함(예: 시작 PLC에서). (SWREF p79)
- **STATUS 요소는 저장·복원되지 않음** — 라이브 상태를 반영. 리셋을 견디리라 기대하지 말 것. (SWREF p79)
- **Gate3[i] 핵심 셋업 요소(PhaseFreq, MacroMode*, MacroEnable*, ServoClockDiv)는 쓰기 보호됨.**
  `Sys.WpKey=$AAAAAAAA`(Script) 또는 `Gate3[i].WpKey=$AAAAAAAA`(C, 매 쓰기 전)를 설정하지 않으면 쓰기가 조용히 무시됨. (UM p83, p94, p96)
- **클럭 소스 변경은 한 명령 줄에서 해야 함**(옛 소스 끄기 + 새 소스 켜기를 한 줄에) — 클럭 소스가 0이거나 2중이 되는
  순간이 없도록; 아니면 watchdog 트립. (UM p69)
- **`fsave`/`fload`는 제한된 요소 집합만 처리** — 전체 `save`의 대체가 아님. (SWREF p62)

## 3. 변수 & 단위

- **모터 단위 ≠ 축(공학) 단위.** 모터 단위는 피드백 분해능 × EncTable[i].ScaleFactor × Motor[x].PosSf에서 유도.
  축 단위는 축 정의 스케일(예: `#1->10000X`)에서 옴. FatalFeLimit, MaxPos/MinPos, JogSpeed 등은 **모터** 단위. (UM p427, p429)
- **모터 단위 스케일을 바꾸면 모든 모터 단위 리미트가 조용히 재스케일됨.** 큰 단위로 가면 FatalFeLimit가 너무 커져
  사실상 무력화될 수 있음. 스케일 변경 후 모든 모터 단위 리미트를 재확인. (UM p427)
- **P = 전역(모든 태스크). Q = 좌표계별. L/R/C/D = 한 프로그램/통신 스레드에 지역.** 접근되는 Q-변수는 주소 지정된
  C.S.에 달림: on-line에선 `&n`, 모션 프로그램에선 실행 중인 C.S., PLC에선 `Ldata.Coord`. Ldata.Coord 설정을
  잊은 PLC는 잘못된 Q-집합을 읽음. (UM p558–559)
- **L, R, C, D는 같은 지역 스택 공간의 다른 뷰** — 서로 alias됨. R/C/D는 "다르게 번호 매긴 L-변수". 독립이라 가정 말 것. (UM p554)
- **최상위 프로그램과 통신 스레드는 각자 고유한 L-변수 집합을 가짐.** L-값은 PLC 간, 또는 PLC와 모션 프로그램 간에
  넘어가지 않음. (UM p557)
- **`[]` 안의 index는 상수 또는 단일 지역변수여야 함 — 식(expression) 불가.** `Motor[L0+1].x`는 불법; 먼저
  L0에 계산: `L0=...; Motor[L0].x`. 대괄호 = 실제 배열 index; 괄호 `P(expr)` = 배열 함수(변수 번호를 계산). (UM p553, p555)
- **런타임 식의 범위 초과 index는 오류 없이 — 예측 불가 결과.** 상수 index만 다운로드 시 범위 검사됨. 계산된 index는
  스스로 검증할 것. (UM p554–555)
- **정수 아닌 index/변수번호 식은 가장 가까운 값이 아니라 내림(0 방향 floor)됨.** `P(27.9999)` → P27. 부동소수로
  계산했으면 사용 전 반올림을 강제. (UM p553, p555)
- **M(포인터) 변수는 raw 주소/레지스터를 alias — aliasing은 위험.** 자체 정의나 잘못 지정된 M-변수는 임의 메모리/I/O를
  덮어쓸 수 있음. 명명된 데이터구조 요소를 선호하고, M-var는 의도적으로 사용. (UM p559)
- **M-변수는 함수라 C에서 직접 접근 불가** — API 호출(또는 Script의 Sys.M[i]) 사용. P/Q는 C에서 `pshm->P[i]` /
  `pshm->Coord[x].Q[i]`로 접근 가능. (UM p559, p558)
- **IDE는 선언된 이름을 xVARSTART(기본 P8192, Q1024, M8192)부터 자동 할당.** 그 아래의 직접 번호 P/Q/M은 "안전";
  위는 선언된 이름과 충돌 위험. 수동 `#define`은 xVARSTART 아래로 유지. (UM p555–556)
- **사용자 배열은 범위 검사 없음.** `global Arr(512)` + 범위 초과 index는 인접 변수를 조용히 손상. (UM p556)
- **I-변수는 데이터구조 요소를 alias하는 Turbo-PMAC 호환 단축**(예: I123 = Motor[1].HomeVel). `I{n}->`로 매핑
  확인. 미할당 I-var(I8192–I16383)는 범용. (UM p559–560)
- **부동소수: 대부분 사용자 변수(P,Q,L,R,D)는 64비트 double; Script에 64비트 정수는 없음.** 할당한 소수가 약간
  어긋나게 보고될 수 있음(예: 1.1 → 1.10000000000000009). (UM p549, p551)
- **`nan`/`inf`는 전파되어 이후 연산을 손상.** 0으로 나눔 → ±inf; sqrt(-1) → nan. 없는 하드웨어 요소를 읽으면 `nan`
  반환. `isnan(x)`나 `!(x<inf)`로 검사; 더 낫게는 계산 전에 가드. (UM p550–551)

## 4. 모션 안전

- **kill vs abort vs disable은 서로 호환되지 않음.** `kill`/`k` = 개루프, 출력 0, amp 비활성, 즉시(감속 없음).
  `abort` = 제어된 폐루프 감속 정지(Motor[x].AbortTa/AbortTs)이며 모션 프로그램을 멈춤. `disable` = C.S.의 모든 모터 kill. (UM p427; SWREF p69, p74)
- **`dkill`/`ddisable`/`adisable` = 지연 kill** — 브레이크 체결(Motor[x].BrakeOnDelay)을 기다림. 일반
  `k`/`kill`/`disable`과 fault-kill은 브레이크를 기다리지 않음. 수직/중력축에선 지연형을 쓸 것. (UM p445–446; SWREF p74)
- **FatalFeLimit(Motor[x].FatalFeLimit) 초과 → 해당 모터 KILL; 다른 C.S. 모터는 abort(FaultMode bit0=1이면 kill).**
  Motor[x].FeFatal 설정. 이를 끄면(0이나 거대값) 런어웨이 보호가 사라짐 — 강력히 비권장. (UM p427–428)
- **WarnFeLimit는 자동 동작이 없음** — Motor[x].FeWarn / Coord[x].FeWarn만 설정; 앱이 직접 반응해야 함. (UM p428)
- **fatal FE, amp fault, encoder loss, I2T는 동일 결함 응답: 해당 모터 kill, 나머지 C.S. abort**
  (Motor[x].FaultMode bit0=1이면 전부 kill). **다른** 좌표계의 모터는 영향 없음. (UM p440, p444, p447)
- **소프트웨어 리미트는 기본 OFF(MaxPos=MinPos=0).** MaxPos > MinPos일 때만 활성, 모터 단위이며 모터 0(home)
  기준, 축 원점 오프셋의 영향 없음. (UM p430)
- **소프트 리미트는 계산 시점과 실행 시점 양쪽에서 검사;** Coord[x].SoftLimitStopDis가 프로그램 이동에 대해
  프로그램 정지(0) vs 리미트에서 포화(1)를 선택. 무한 jog(`j+`/`j-`)는 소프트 리미트까지의 유한 jog가 됨. (UM p429–431)
- **하드웨어 리미트 스위치는 반드시 normally-closed(failsafe); 표준 구성에선 극성을 사용자가 바꿀 수 없음** —
  케이블 단선이나 리미트 전원 상실은 "리미트로 진입"으로 읽힘. (UM p432)
- **하드웨어 리미트는 방향 민감; +limit과 −limit을 올바른 입력에 배선해야** 동작함. Motor[x].pLimits(0=비활성)와
  Motor[x].LimitBits(24=PMAC2 DSPGATE1, 9=PMAC3 DSPGATE3, 25=MACRO) 설정. (UM p432–433)
- **amp-enable/amp-fault 극성: enable은 소프트웨어로 못 바꿈**(0=disable, 1=enable; 결함 시 HW가 0 강제).
  fault 입력 극성은 Motor[x].AmpFaultLevel bit0으로 설정(기본 1=high-true fault). pAmpEnable/AmpEnableBit(22 PMAC2 /
  8 PMAC3), pAmpFault/AmpFaultBit(23 PMAC2 / 7 PMAC3) 설정. (UM p446–447)
- **encoder-loss와 보조-fault 검출은 기본 OFF**(pEncLoss=0, pAuxFault=0). 구성하지 않으면 자동 피드백 상실 보호
  없음. 헛트립 방지로 EncLossLimit ≥ ~3–4 설정. (UM p438–440, p443)
- **I2T(Motor[x].I2tTrip/I2tSet/MaxDac)는 시간 항에 올바른 Sys.ServoPeriod에 의존;** ServoPeriod가 틀리면 열
  보호가 부정확. 순수 위치/속도 출력 모드(전류 접근 불가)에선 I2T 불가. (UM p449, p451–452)
- **C.S.에서 널 정의 `#x->0`을 가진 모터도 그 C.S.의 결함 공유에 참여**(그룹과 함께 abort/kill됨). (UM p427, p447)
- **브레이크 출력: 0=체결, 1=해제, 소프트웨어 극성 없음; 리셋/watchdog에서 0으로 강제.** failsafe하게 배선. (UM p445)
- **전역 abort 입력(Sys.pAbortAll/AbortAllBit)은 Power Brick 외엔 기본 off.** "1"은 항상 결함 상태(failsafe), 극성
  제어 없음. C.S.별 반응은 Coord[x].AbortAllMode(0=abort, 1=kill, 2=abort 후 지연 kill, 3=무시). (UM p425–426)
- **모션 프로그램을 abort하면 그 프로그램 카운터가 시작으로 리셋됨** — abort 지점에서 단순 재개 불가(`list apc`로 위치 확인). (UM p427)
- **좌표계 명령(run/abort/hold/%)은 C.S. 전체에, 모터 명령(jog/home/k)은 한 모터에 작용.** C.S. 축에 할당된 모터는
  프로그램이 구동 — 프로그램이 소유 중일 때 jog하면 충돌; error 46 "COORD JOGGED OUT OF POSITION" / 재개 전 `pmatch` 사용. (SWREF p74, p77)
- **`jog/`는 jog를 멈추고 위치 제어를 복원.** 모터를 깨끗이 되돌려줄 때 사용. (SWREF p74)

## 5. Script vs C vs PLC — 선택과 혼합

- **Script 모션 프로그램 = 좌표계 내 조율/경로 모션(linear, circle, pvt, spline, G-code).** 머신 로직/IO 시퀀싱을
  모션 프로그램에 넣지 말 것; PLC에 넣을 것. (NAVIGATION; SWREF p70)
- **Script PLC = background/포그라운드 로직, I/O, 타이머, 시퀀싱 — 조율 경로 모션 아님.** PLC는 jog/프로그램 제어
  명령을 발행; 모션 프로그램 같은 move 블록을 담지 않음. (UM p686+; SWREF p72)
- **계층은 의도적으로 선택: 빠르고 결정적 로직엔 포그라운드(RTI) PLC, 느리고 비시간임계 로직엔 background PLC.**
  포그라운드 PLC 코드는 짧아야 함(background를 선점하고 RT 예산을 빼앗음). (UM p548, p84)
- **C PLC(CPLC): rticplc(1개만, 포그라운드, RTI) vs background C PLC(여럿).** 실시간 C는 non-blocking, sleep/할당하는
  OS 호출 없어야 함. background C / capp는 GPOS 기능 사용 가능. (UM p845+; c-programming 가이드)
- **M-변수는 C에서 직접 못 씀; P/Q는 pshm으로 가능.** Script M-var를 C에 섞으려면 API 호출 필요. (UM p559)
- **PLC에서 `command("…")` / cx 식 on-line 명령 실행은 오버헤드와 비동기 의미를 가짐** — 명령은 큐잉/파싱되며 즉시
  보장 안 됨. 매 스캔마다 명령을 마구 발행하지 말 것. (verify: UM PLC 챕터 p686–699)
- **같은 모터를 두 소유자가 구동하지 말 것.** 실행 중인 모션 프로그램이 제어하는 모터를 동시에 PLC/터미널에서 jog하면 안 됨(error 46). (SWREF p77)

## 6. 흔한 컴파일 / 런타임 에러 (SWREF p76–78)

보고되는 명령 에러 ID(쿼리/터미널 응답):
- **20 ILLEGAL CMD / 21 ILLEGAL PARAMETER** — 잘못된 명령 토큰이나 인자; 보통 오타, 잘못된 컨텍스트, 잘못된 구문. (SWREF p77)
- **23 OUT OF RANGE NUMBER / 24 OUT OF ORDER NUMBER / 25 INVALID NUMBER / 26 INVALID RANGE** —
  숫자/index 인자가 허용 범위 밖이거나 형식 오류. 상수 index가 여기서 범위 검사됨. (SWREF p77)
- **31 COMPILE ERR** — 다운로드 시 Script 프로그램 컴파일 실패(버퍼 프로그램의 구문 오류). (SWREF p77)
- **33 BUFFER IN USE / 34 BUFFER FULL / 40 BUFFER NOT DEFINED / 41 BUFFER ALREADY DEFINED** —
  프로그램/테이블 버퍼 관리 오류; `open`/`close`/`delete`를 올바르게. (SWREF p77)
- **35 INVALID LABEL / 36 INVALID LINE # / 22 PROGRAM NOT IN BUFFER** — goto/gosub 타깃 또는 프로그램 참조가 없음. (SWREF p77)
- **38 PROGRAM RUNNING / 39 NOT READY TO RUN** — 잘못된 실행 상태에서 프로그램 편집/시작 불가; 먼저 `abort`나 `stop`. (SWREF p77)
- **42 NO MOTORS DEFINED / 43 MOTOR NOT CLOSED LOOP / 44 MOTOR NOT PHASED / 45 MOTOR NOT ACTIVE** —
  모터가 활성/phase(`$`)/폐루프가 아니라 모션/`run` 거부. move 명령 전에 활성화/phase/enable. (SWREF p77)
- **46 COORD JOGGED OUT OF POSITION** — C.S. 모터가 jog로 벗어남; 프로그램 재개 전 `pmatch`(또는 jog 복귀). (SWREF p77)
- **47 SERVO REQUEST ACTIVE** — servo 작업 대기 중; 재시도. (SWREF p77)
- **70–77 Struct Write … Error** — 데이터구조 요소 쓰기 실패(잘못된 index, Gate/카드 없음, WpKey 미설정, 읽기전용/STATUS
  요소). Gate3[i] 쓰기엔 WpKey 확인. (SWREF p78)
- **50–59 MACRO …** — ring 미동기/불가, MACRO IC 없음, sync-master 구성(예: 57 SYNC MASTER MUST HAVE STN=0). (SWREF p77–78)

흔한 조용한(에러 없는) 함정: 런타임 범위 초과 index(§3), 리셋 시 미저장 변경 상실(§2), WpKey 없는 무효 Gate3 쓰기(§2), 기본 비활성 안전(§4).

---

## 더 깊은 내용 (raw 청크)
- 태스크 모델, 클럭, 통계, watchdog 타이밍: `raw/user-manual/p0061-0080.txt`, `p0081-0100.txt` (UM p61–91); 우선순위 `p0541-0560.txt` (UM p547–549).
- Save/reset/재초기화/HWChange, 클럭 소스: `raw/user-manual/p0061-0080.txt` (UM p61–69).
- 변수, 단위, index, 부동소수: `raw/user-manual/p0541-0560.txt` (UM p547–560); fatal-FE 단위 `p0421-0440.txt` (UM p427).
- 안전(watchdog, abort-all, 추종오차, soft/hard 리미트, encoder loss, amp fault, I2T, 브레이크): `raw/user-manual/p0421-0440.txt`, `p0441-0460.txt` (UM p422–461).
- 명령 분류, kill/abort/disable/dkill, 에러 표: `raw/software-ref/p0061-0080.txt` (SWREF p62–78).
- PLC/모션 프로그램 상세: UM p659–711, p686–699 (아직 청크 정독 안 함 — 깊은 주장 전 확인). C 프로그래밍: `raw/c-programming/`.

---

## 관련 문서
- [[script-plc|Script PLC 프로그램]] — 태스크 계층·스캔 의미의 PLC 측면
- [[script-motion|Script 모션 프로그램]] — kill/abort/소유권이 모션에 미치는 영향
- [[setup-workflow|셋업 워크플로우]] — 리미트·amp fault·home 실배선 절차
- [[data-structure|데이터 구조]] — SAVED/NON-SAVED/STATUS 분류, 요소 모델
- [[syntax-rules|구문 규칙]] — 변수(P/Q/M/L/I)·단위·index 규칙
- [[🗺️ PowerPMAC 지식맵]] — 전체 지식맵(MOC)
