---
title: IDE & 모터 브링업 워크플로우
aliases: [셋업 워크플로우, Setup Workflow]
tags: [powerpmac/setup, type/reference]
domain: setup
status: stable
updated: 2026-06-29
---

# IDE 셋업 & 운용 워크플로우

실전 브링업(bring-up) 워크플로우: IDE → 시스템 클럭 → 모터 셋업(로컬 & EtherCAT) →
튜닝 → jog → 호밍(homing) → EtherCAT 활성화/리셋. OMRON Power PMAC LEVEL1/2
교육에서 증류. 요소 이름은 펌웨어 테이블과 일치하며, 이 파일은 **절차만** 담음 —
제품별 숫자는 예시. 전체 원문: `reference/raw/edu/` (로컬 전용, gitignored).

## IDE 필수 사항
- **접속**: 기본 IP `192.168.0.200`, 사용자 `root`, 비밀번호 `deltatau` (Test / No Device 옵션).
- **주요 창**: Terminal(온라인 명령 — *즉시 실행되므로 주의*), Watch(실시간 요소;
  셀 클릭 → "C" 로 읽기→명령 전환), Position, Status(모터/CS/전역 플래그; 빨강=결함,
  초록=정상), Output(빌드/다운로드), PowerPMAC Messages(모든 오류 보고), Plot/Scope(수집),
  Jog Ribbon, Task Manager(CPU / PLC / 프로그램 실행 상태).
- **빌드 & 다운로드**: 솔루션 우클릭 → *Build and Download All Programs*. `F1` = 명령 도움말.

## 시스템 셋업 (먼저 수행)
1. **공장 초기화**: `$$$***` (공장 기본값) → `save` → `$$$` (리셋).
2. **영속성**: 다운로드 → Active (RAM). `save` → flash (`/opt/ppmac/usrflash`). `$$$`로 저장된 내용 재로드.
   SAVED 셋업은 리셋을 견디며; NON-SAVED 셋업은 시작 PLC에서 매 부팅마다 재적용됨.
3. **새 프로젝트**: 템플릿 = PMAC-only 또는 EtherCAT. 프로젝트 속성
   *"download systemsetup.cfg" = No* 로 설정 — 그렇지 않으면 이후 `save` 시 모터 셋업이 기본값으로 되돌아감.
4. **클럭** (System → CPU → System): 모터 수 & 부하에 따라 Phase / Servo / RealTime(RTI) 주파수 설정.
   Phase = 커뮤테이션(commutation) + 전류 루프(최고 우선순위); Servo = 명령 위치 갱신 주기;
   RTI = 모션 계획 + 포그라운드 PLC.
   - Script/MCP: `Gate3[i].PhaseFreq` 설정 (CK3M AX 유닛: `CK3WAX[i].PhaseFreq`; 펌웨어가 가장 가까운 값으로 스냅)
     + `ServoClockDiv` (ServoFreq = PhaseFreq/(ServoClockDiv+1)), **그리고 별도로** `Sys.ServoPeriod`
     (= 1000/ServoFreqHz, ms) + `Sys.PhaseOverServoPeriod` — 이 두 값은 독립된 SAVED 파라미터로, Gate 클럭에서
     **자동 유도되지 않음**. `Sys.WpKey=$AAAAAAAA`로 잠금 해제 후 `save`+`$$$`. 리셋 직후에는
     컨트롤러가 안정화될 때까지 잠시 대기 — 클럭 파생값(`Sys.ServoPeriod` 등)이 리셋 직후에는 **stale** 값을 읽음.
     두 번의 `Sys.ServoCount` 샘플(ΔCount/Δwall-time)로 실제 주기를 교차검증.

## 로컬 모터 셋업 (아날로그 ±10V 또는 Direct-PWM; 예: CK3M + AX 카드 + 서보 드라이브)
마법사: **Motors → Add a Motor → 토폴로지 "Single Feedback"** 우클릭, 그 다음:
1. **Amplifier** — 선택 또는 정의 (제조사 + 부품 번호; Delta Tau → Part Managers 경유).
2. **Motor** — 데이터시트로 선택/정의 (잘못된 데이터 → 모터/앰프 손상).
3. **Encoder** — 선택/정의 (예: 2500 line = 10000 count/rev 일반적).
4. **Hardware interface** — 앰프 / 피드백 / 플래그 채널; Amp Fault Level(Low/High True) 설정,
   HW 리미트(limit) 사용 시 활성화 → Accept.
5. **Interactive feedback** — 모터 손으로 돌리기, Encoder Direction 설정 → Accept.
6. **Limits**, 이어서 **I2T** — 토크 모드 범용 서보는 자체 과부하 차단 보유 → "Turn
   Protection Off"; 그렇지 않으면 모터/앰프 연속 전류 중 작은 값으로 제한됨.
7. **Test & Set** (DC 바이어스 오프셋 등; 재시도 필요할 수 있음) → 모터 튜닝 준비 완료.
- **포인터 모델**: 출력/피드백은 주소로 설정, 예: `Motor[1].pDac = Gate3[0].Chan[0].Pwm[0].a`
  (`.a` = 요소의 address-of; 선행 `p` = pointer-to).
- **CK3M AX 유닛**: 축 인터페이스 유닛(예: CK3W-AX1515N)은 구조체 **`CK3WAX[i]`** 에 `.Chan[0..3]`을 가지며,
  `Gate3[i]` 가 **아님** — 클럭과 채널 레지스터가 여기 있음 (`CK3WAX[0].Chan[0].Pwm[0].a`/`.Dac[0].a`는
  같은 아날로그 출력을 alias; `.ServoCapt.a`, `.OutCtrl.a`, `.Status.a`). 공장 기본값은 이미 `Motor[1]`을
  `CK3WAX[0].Chan[0]` 에 단일 피드백 아날로그 모터로 배선(`PhaseCtrl=0`, 드라이브가 커뮤테이션).
- **Amp-fault 상태는 래치됨**: 한번 트립되면 enable(`#xj/`)로만 해제 — `AmpFaultLevel` 변경, `ServoCtrl` 토글,
  `#xk` 로는 **해제되지 않음**. `AmpFaultLevel`은 드라이브의 **정상 상태** 결함 라인 레벨로 설정:
  OMRON G5 아날로그 드라이브의 ALM은 페일세이프(정상 시 라인 low) → `AmpFaultLevel=1`.
- **스케일링(`Motor[].PosSf`) 변경 후 즉시 활성화하면 모터가 점프/런어웨이** — 스케일을 먼저 설정한 뒤
  `save`+`$$$` 로 부팅 시 깨끗이 적용, *그 다음* 활성화.

## 튜닝 (위치 루프)
Auto/Basic = 원터치 "Start Tuning" → Accept → Servo On + jog으로 검증.
수동 인터랙티브 — 다른 게인을 먼저 0으로 만들고 순서대로 각 게인 탐색:
- **Kp** (P, 강성): 노이즈/발진 발생 직전까지 올리고 낮춤 (스텝 이동, 예: 1000ct / 300ms).
- **Kvfb** (D, 제동): Kp 오버슈트 & 정착 시간 단축.
- **Kvff** (속도 FF, 포물선 이동): 속도 의존 추종오차(following error) 감소; 초기값 ≈ Kvfb.
- **Ki + IntegralMode** (1 = 정지 시만, 0 = 전 구간): 정상상태 추종오차 제거.
- **Kaff** (가속 FF, 사다리꼴 이동): 가속/감속 구간 추종오차 감소.
- **미세 조정**: 실제 가공 중 수집; 사양에 맞게 게인 조정 (캔드(canned) 프로파일만으론 부족).

## Jog (가장 단순한 폐루프 이동; 단일 모터; CS/프로그램과 비동기)
온라인 `#nJ…`, 프로그램 `Jog…`:
- `#1J/` 서보 온 홀드 · `#1J+`/`#1J-` 무한 · `#1J=val` 절대 · `#1J:val` 상대 ·
  `#1J=` 사전 jog 위치 · `#1J=*` → `Motor[x].ProgJogPos` 이동 · `#1J==val` 이동 & 사전 jog 설정.
- 프로그램 형식: `Jog/1`, `Jog+1`, `Jog1=`, `Jog1:`, `Jogret1`, `Jog1=*`.
- 프로파일 변수: `Motor[].JogSpeed` (모터 단위/ms, 항상 +), `Motor[].JogTa`, `Motor[].JogTs`
  (>0 = 시간 ms; <0 = 역수 비율 — JogTa<0이면 JogTs도 반드시 <0). 새 값은 *다음*
  jog 명령부터 적용.
- `Motor[x].InPos` = 1 (정착(settle) 완료): 폐루프 + `DesVelZero` + 이동/드웰 없음 + |FE| ≤
  `InPosBand` 상태가 `InPosTime`+1 사이클 지속. **`InPosBand` 기본값 0 → 반드시 설정.**
- Abort 감속: `Motor[].AbortTa/AbortTs` (HW/SW 리미트, CS abort, 런타임 오류 시 자동 적용).

## 호밍 — 로컬 (Gate3 하드웨어 캡처(capture))
변수: `Motor[].HomeVel` (±), `JogTa`, `JogTs`, `HomeOffset`. 상태: `HomeInProgress`, `HomeComplete`.
1. Gate3 쓰기 잠금 해제: `Sys.WpKey=$AAAAAAAA` (없으면 쓰기가 조용히 무시됨).
2. `Gate3[0].Chan[0].CaptCtrl` (0–15 = 플래그/인덱스, hi/lo 조합) + `.CaptFlagSel`
   (0 home, 1 +limit, 2 −limit, 3 user).
3. `#1hm` (온라인) — 또는 **PLC 버퍼 내부**에서 `home 1;` (`home 1`을 온라인 명령으로 쓰면 = ILLEGAL CMD).
   `HomeComplete` 0→1, `HomeInProgress` 1→0 감시.
- **`CaptCtrl` (실기 검증)**: `1`=index(Z); `2`=선택 플래그 **high / 상승엣지(0→1)**;
  `10`=플래그 **low / 하강엣지(1→0)** (=2에 +8 반전 비트). 센서에 맞는 엣지 선택.
- **★ 실제 위치 = `Motor[x].ActPos − Motor[x].HomePos`, raw `ActPos` 아님.** 호밍이 캡처된
  위치를 `HomePos`에 기록하므로 정상 홈은 `ActPos−HomePos ≈ 0` (또는 `HomeOffset`)을 읽음. raw ActPos는
  엔코더의 (멀티턴일 수 있는) 누적 카운트 — 이걸로 홈 성공 여부를 판단하면 정상 홈을 결함으로 오해. → [[ppmac-actpos-homepos]].
- **홈 플래그는 점이 아닌 반평면일 수 있음**: 실제 장치에서 홈 입력이 이동 범위 한쪽 절반에서 0, 나머지에서 1일 수 있음
  (경계 = 홈 엣지). 플래그가 *비활성* 상태인 방향에서 접근하여 전환을 캡처(예: flag=0인 − 쪽에서 진입, + 방향 이동,
  상승 캡처 → `CaptCtrl=2`). 플래그가 이미 캡처 활성 레벨에 있을 때(예: flag-low `CaptCtrl=10`인데 이미 low 쪽에 있음)
  `#xhm` 이 즉시 트리거 = 시작 위치에서 거짓 홈.

## 호밍 — 리미트 탐색 후 홈 센서 (다단계 PLC, 실기 검증)
사용자가 요청한 패턴: jog −, 마이너스 리미트 확인, 역방향 +, 홈 센서에서 완료. 중괄호(braces)
상태머신 PLC(모터당 하나, 독립; 변수 하나로 동시 트리거). 모터당:
`jog-x` → 마이너스 리미트 감지 → 정지 → (정착) → `home x` / `jog+x` → 홈 센서 → `homez x` 또는 캡처.
- **네이티브 리미트가 설정된(`pLimits`≠0) 로컬 모터**: HW 리미트에 닿으면 **자동 abort**; 바로 다음 PLC 스캔에서
  발행한 `jog+`/`home`은 **흡수됨**(모터 그대로). 리미트 탈출 이동 발행 전 정착 삽입 —
  `Sys.CdTimer[i]=200` (ms) 후 `<=0` 대기. (네이티브 리미트가 *미설정*인 모터는 abort가 없으므로
  `jog+`가 즉시 적용 — 아래 EtherCAT 케이스 참고.)
- `pLimits` 설정 시 해석 요소로 플래그 읽기: `Motor[x].MinusLimit`/`.PlusLimit`,
  `CK3WAX[0].Chan[0].HomeFlag`. **양쪽 리미트가 동시에 1 = 배선 없음/플로팅 리미트 입력** (오픈 = 페일세이프 활성),
  실제 양방향 리미트 조건이 아님 — 신뢰 전에 센서 특성 확인.
- `home x` (`homez` 아님)는 `HomeVel` 속도(부호 = 방향)로 이동하면서 설정된 캡처(CaptCtrl/CaptFlagSel)를 재실행;
  하드웨어 캡처 정밀도가 필요할 때 사용. `homez x`는 현재 위치를 홈으로 설정(HomeComplete=1, 이동 없음) —
  소프트웨어로 감지한 센서 엣지에 적합.

## EtherCAT 모터 셋업 (예: OMRON 1S 서보 드라이브, NX-I/O)
EtherCAT = 실시간 마스터/슬레이브 필드버스. **DC**(Distributed Clock)는 슬레이브를 서보
RTI에 동기화. **PDO** = 주기적 데이터, `ECAT[i].Enable=1` 시 시작 (모션에는 DC 모드, 일반 I/O에는 FreeRun).
**SDO** = 비주기적 레지스터 R/W:
`EcatTypedSdo(master, slave, dir(0=wr/1=rd), index, subindex, data, length)`.
단계:
1. 클럭 먼저 설정. EtherCAT 마스터 추가 (또는 EtherCAT 프로젝트 템플릿 사용).
2. EtherCAT 사이클 = **62.5 µs** 의 배수 (이중코어 최대 250 µs); 서보 주기에 자동 매칭.
3. 마스터 우클릭 → **Scan EtherCAT Network** (실패 시 터미널에서 `ecat reset`). 슬레이브 표시.
4. 각 슬레이브 열기: 미사용 Safety 모듈 제거; **PDO Mapping** 탭 → 올바른 PDO 세트 선택;
   **DC** 탭 → Shift Time = 사이클의 25–50% (*Overwrite Mode* 확인). 일반 I/O 슬레이브 → FreeRun.
5. 마스터 우클릭 → **Load Mapping to PMAC**.
6. **Add a Motor → 토폴로지 "EtherCAT"** → 슬레이브 + 제어 타입 + 사용자 단위 변환 선택 → 저장
   (박스 주황→초록). Hardware interface: 1S 자동 채움 → Accept.
7. **Build & Download**, 이어서 네트 활성화: `ECAT[0].Enable=1` (또는 마스터 우클릭 → Active EtherCAT).
8. *Watch EtherCAT Mapped Variables*로 주기적 갱신 확인.
- PDO 매핑된 요소는 긴 생성 이름을 가짐, 예: `Slave_1001_…_60FD_0_Digitalinputs.a`.
- 스캔 시 `Project/PMAC Script Language/Global Includes/ECATMap.pmh` 생성 —
  `#define Slave_<pos>_<model>_<idx>_<sub>_<name>  ECAT[0].IO[n].Data` — **출력 `IO[0..]`, 입력 `IO[4096..]`**.
  스캔 + PDO 맵 + *Load Mapping to PMAC*는 **IDE 전용** 단계 (펌웨어 마스터 = acontis; 순수
  터미널/스크립트 세션은 명명된 맵을 생성할 수 없음). 매핑 후 모터 할당은 스크립트로 가능:

### EtherCAT CiA402 (CSP) 모터 — 요소 수준 할당 (script/MCP, 매핑 후)
모터를 CiA402 드라이브에 **Add-Motor 마법사 없이** 바인딩 가능 — 위의 `ECAT[0].IO[n].Data`
엔트리를 직접 가리킴 (OMRON 1S 실기 검증):
- **피드백**: `EncTable[k].type=1`, `.pEnc=ECAT[0].IO[<6064 actual>].Data.a`, `.pEnc1=Sys.pushm`,
  `.index1..6=0`, `.ScaleFactor=1` → 32비트 Position-actual 레지스터 읽기. **type 1, type 11 아님**
  (인덱스가 0일 때 type 1은 일반 32비트 레지스터를 읽음). 이어서 `Motor[x].pEnc=pEnc2=EncTable[k].a`.
- **명령 (CSP)**: `Motor[x].pDac=ECAT[0].IO[<607A target>].Data.a` (서보 출력 = 목표 위치).
- **Enable/fault**: `Motor[x].pAmpEnable=ECAT[0].IO[<6040 controlword>].Data.a` (PMAC이 CiA402
  상태머신 자동 구동: Switch-On-Disabled→Ready→…→Operation-Enabled), `Motor[x].pAmpFault=ECAT[0].IO[<6041
  statusword>].Data.a`, `Motor[x].AmpFaultBit=3` (statusword Fault 비트), `AmpFaultLevel` 설정.
- `Motor[x].PhaseCtrl=0` (드라이브가 커뮤테이션); 스케일링 `Motor[x].PosSf=Pos2Sf` (예: user-units / 2^encbits).
- **★ `Motor[x].Ctrl=Sys.PosCtrl` — CSP에 필수.** 기본 `Ctrl`은 토크/PID 루프를 실행하므로 목표
  레지스터(607A)에 절대 쓰지 않음(0 유지); 드라이브가 위치 편차로 트립(OMRON 1S 오류 `$FF24`,
  statusword Fault 비트)됨. `Sys.PosCtrl`은 명령 위치를 `pDac`에 직접 전달하여 **드라이브**가 루프 폐쇄.
  모터를 복사할 때 놓치기 쉬움 — `Motor[].Ctrl`도 포인터/스케일/게인과 함께 복사 필수.
- `#xj/`로 활성화; 활성화 시 목표가 실제에 동기화(점프 없음)되고 CiA402 결함 래치 해제.

## EtherCAT 리미트 & 호밍 (PDO 포인터)
- **리미트**: `Motor[n].pLimits = Slave_…_60FD_0_Digitalinputs.a`; `Motor[n].LimitBits`
  (양방향 리미트 = 비트 `LimitBits`, 음방향 리미트 = 비트 `LimitBits+1`).
  **★ 드라이브별 실제 60FD 비트 맵 검증 필수 — 가정 금지.** 실기 OMRON 1S에서 음방향 리미트(NOT)는
  **비트 1**, 홈/EXT1 입력은 **비트 17**, 둘 다 active-high — 명목상 CiA bit0/bit2와 다름.
  − 방향으로 6M 카운트 jog해도 가정했던 bit2가 켜지지 않았음; 리미트에서 실제로 토글된 비트가 진실.
- **PLC 내 감지 대안**: `pLimits=0`으로 두고 PLC에서 DI 비트 테스트
  (`if (ECAT[0].IO[<60FD>].Data & (1<<bit)) {...}`)하면 네이티브 리미트 abort를 피하므로,
  `jog+x`가 즉시 효과 (로컬 네이티브 리미트와 달리 정착 불필요). 홈 비트 상승엣지에서 `homez x`로 완료.
- **EtherCAT 모터에서 온라인 `jog/x`는 `MOTOR NOT ACTIVE` 반환 가능** — 주소 지정 형식 `#xj/` 사용.
  PLC 버퍼 내부에서는 `jog/x` / `jog-x` / `home x;` 정상 동작.
- **드라이브 터치프로브(touch-probe)를 통한 호밍** (CiA402: `0x60B8` 함수 / `0x60B9` 상태 / `0x60BA` 위치):
  `Motor[2].pCaptPos = Slave_…_60BA_…Touchprobepos1posvalue.a`,
  `Motor[2].pCaptFlag = Slave_…_60B9_…Touchprobestatus.a`, `CaptFlagBit=1`,
  `CaptPosLeftShift=0`, `CaptPosRightShift=0`. Touchprobefunction: `$15`=index 래치, `$11`=플래그(EXT1).
  순서: func=0 → status 0 대기 → func=`$15` → status 1 대기 → `#1hm` → status 3 대기 →
  `Motor[1].HomeComplete` 확인.

## EtherCAT 초기화 / 리셋 패턴 (시작 PLC)
- **초기화** (주기적 I/O는 NON-SAVED이므로): `ECAT[0].SlaveCount == Σ ECAT[0].Slave[k].Online` 대기,
  `ECAT[0].Enable=1` 설정, 이어서 `ECAT[0].MasterState==2 && ECAT[0].MasterReady==1` 대기
  (`Sys.Time`으로 타임아웃 처리).
- **리셋**: `cmd "ecatreset"` 후 `Sys.EcatMasterReady` / `ECAT[0].MasterReady` 가 1로 돌아오길 대기.

## 로컬 vs EtherCAT 모터 — 셋업 비교
| | 로컬 | EtherCAT |
|---|---|---|
| 출력/피드백 | `Motor[x].pDac/pEnc → Gate3[i].Chan[j]` | 포인터 → PDO 매핑된 슬레이브 변수 |
| 서보 루프 | PMAC 폐쇄 (Phase 클럭으로 커뮤테이션) | 드라이브 폐쇄 (CiA402 CSP/CSV/CST); CK3E는 Phase 태스크 없음 |
| 리미트 / 홈 | Gate3 `CaptCtrl`/`CaptFlagSel` | `pLimits`/`LimitBits` + 드라이브 터치프로브 |
| 브링업 | Add Motor "Single Feedback" + HW interface | 스캔 → PDO 맵 → Load → Add Motor "EtherCAT" → Enable |
| 영속성 | 서보 셋업 SAVED | 주기적 I/O NON-SAVED → 매 부팅마다 재초기화 |

## MCP 응용 노트 (`powerpmac` MCP 도구)
이 워크플로우는 라이브 MCP에 직접 매핑됨 — 올바른 명령 시퀀스 생성에 활용:
- **셋업 검증** → `get_responses`: `Motor[1].AmpEna`, `.InPos`, `.HomeComplete`,
  `Gate3[0].Chan[0].CaptCtrl`, `ECAT[0].Enable`, `ECAT[0].MasterReady`, `ECAT[0].SlaveCount`.
- **Jog** → `send_command "Motor[1].JogSpeed=10"` 후 `#1j+` / `#1j/` / `#1j=10000`.
- **홈 (로컬)** → `send_command "Sys.WpKey=$AAAAAAAA"` → `CaptCtrl`/`CaptFlagSel` 설정 → `#1hm`.
- **EtherCAT 브링업** → `download_project` 후: `send_command "ECAT[0].Enable=1"`;
  `send_command "ecatreset"` 으로 복구; `get_response`로 매핑된 슬레이브 변수 읽기.
- **SDO** → Script PLC 내부에서 `EcatTypedSdo(...)` 로 런타임 드라이브 파라미터 설정 (베어 온라인 명령 아님).
- **빌드/다운로드** → `build_project` (Script + ARM C 컴파일, 로드된 EtherCAT 맵 포함) →
  `download_project`. 반드시 `send_command "save"`로 영속화; EtherCAT 맵 자체는
  NON-SAVED이므로 시작 PLC로 네트 재활성화에 의존.

---
출처: OMRON Power PMAC LEVEL1/2 교육. 원문은 `reference/raw/edu/` 로컬 전용 보관
(gitignored). 범용 절차 및 펌웨어 요소만; 벤더 부품 번호는 예시.

## 관련 문서
- [[servo-internals|서보 내부 동작]] — 엔코더·커뮤테이션·피드백 내부
- [[script-plc|Script PLC 프로그램]] — 호밍/모터 명령을 PLC에서 발행
- [[data-structure|데이터 구조]] — Motor[]/Gate3[]/ECAT[] 요소 모델
- [[gotchas|Gotchas]] — 리미트·amp fault·안전 함정
- [[ppmac-actpos-homepos|실제위치 = ActPos−HomePos]] — 현장 검증 메모
- [[🗺️ PowerPMAC 지식맵]] — 전체 지식맵(MOC)
