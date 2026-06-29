---
name: powerpmac-dev
description: >-
  OMRON Delta Tau Power PMAC 컨트롤러 코드 — Script 모션 프로그램(prog), Script PLC(plc),
  C 프로그램(CPLC/capp) — 를 작성·리뷰·디버그한다. Use whenever the task involves Power PMAC /
  PMAC / Delta Tau / Sysmac motion control: motion program move modes
  (linear/circle/pvt/spline), coordinate systems & kinematics, data-structure
  elements (Sys./Motor[]/Coord[]/Gate3[]), P/Q/M/I variables, save/$$$/reset
  behavior, CfromScript, real-time vs background tasks, or motion safety
  (following error, limits, kill/abort).
tags: [powerpmac/index, type/moc]
aliases: [Power PMAC Development, PowerPMAC Skill]
updated: 2026-06-29
---

# Power PMAC 개발

OMRON Delta Tau **Power PMAC** 지식 스킬. 공식 매뉴얼(User Manual, Software Reference,
5-Day Training, C Programming)에서 정제함. 이 파일은 **지도 + 안전 요약**이며, 깊이가 필요하면
해당 `reference/` 파일을 열 것.

## 이 스킬 사용법
1. 도메인 식별(Script 모션? Script PLC? C? 데이터 구조? gotcha?).
2. 해당하는 아래 `reference/*.md`만 읽을 것 — 밀도 높은 정제본임.
3. 정제본에 없는 정확한 요소/명령은 `grep reference/raw/`(전체 매뉴얼 텍스트, ~3260쪽).
   `reference/NAVIGATION.md`가 도메인 → 매뉴얼 페이지 범위를 매핑; `reference/raw/software-ref/_toc.txt`가 요소 인덱스.
4. 모션/안전 코드를 내기 전에 아래 **Top gotchas**를 확인할 것.

## Reference 라우팅
| 필요한 것 | 파일 |
|---|---|
| 연산자, 변수(P/Q/M/L/I), 흐름 제어, on-line vs buffered 명령 | `reference/syntax-rules.md` |
| `Structure[index].Element` 모델, Sys./Motor[]/Coord[]/Gate3[], SAVED/NON-SAVED/STATUS, I-var 매핑 | `reference/data-structure.md` |
| 모션 프로그램: `open prog`, move 모드, 축 정의, 좌표계, 키네매틱스, lookahead, G-code | `reference/script-motion.md` |
| PLC 프로그램: `open plc`, 스캔 모델, 타이머, `cmd`, 시퀀싱, 관용구 | `reference/script-plc.md` |
| C: CPLC(실시간) vs capp(백그라운드), C API / pshm 접근, CfromScript, 빌드/pp_proj | `reference/c-programming.md` |
| **C API 실제 시그니처**(gplib.h: GetResponse/Command/GetPmacVar; RtGpShm.h pshm 구조체) | `reference/c-api.md` |
| **정식 요소 목록**(모든 `Structure.Element`, 펌웨어 intellisense 테이블) | `reference/firmware/ELEMENTS_INDEX.md` + grep `reference/firmware/pp_swtbl*.txt` |
| 함정: 태스크 모델, save/reset, 단위, 모션 안전, 에러 ID | `reference/gotchas.md` |
| **프로젝트 구조**: 폴더 레이아웃, 파일 종류, `.ppproj` manifest, `pp_proj.ini` 로드 순서, 컨트롤러 내 `/var/ftp/usrflash/Project` 매핑 | `reference/project-structure.md` |
| **IDE & 모터 브링업**: IDE 창, 시스템 클럭, **로컬·EtherCAT 모터 셋업**, PID 튜닝, jog 파라미터, 호밍(Gate3 캡처 / EtherCAT touch-probe / **limit-find→home-sensor PLC**, `CaptCtrl` 에지 선택, **실제위치 = `ActPos−HomePos`**), EtherCAT enable/reset; + MCP 명령 매핑 | `reference/setup-workflow.md` |
| **서보 내부**(심화): 엔코더 종류/sub-count(1/T,arctangent)/ECT/EncLoss, 커뮤테이션 모드, phase referencing, sine vs Direct-PWM 출력 | `reference/servo-internals.md` |
| **벤더 교육 자료**(심화 이론 강의 + ODT 트레이닝): 주제→raw 인덱스; 상세는 grep `reference/raw/edu/` | `reference/lecture-series.md`, `reference/training-course.md` |
| 도메인 → 매뉴얼 페이지 맵; raw 코퍼스 재생성 방법 | `reference/NAVIGATION.md` |
| 적용할 검증된 예제 프로그램 | `snippets/` |

## 멘탈 모델 (Power PMAC가 다른 점)
- **모든 것은 명명된 데이터구조 요소**, raw 레지스터나 I-변수가 아님.
  `Structure[index].Element` — 예: `Motor[1].JogSpeed`, `Coord[1].Tm`, `Gate3[0].Chan[0].ServoCapt`.
  index는 0부터, 상수 또는 단일 지역변수(`[]` 안에 연산 불가). 레거시 Turbo `I`-변수는 여전히 SAVED 요소를
  alias함(`I123 ≡ Motor[1].HomeVel`; `I{n}->`로 조회).
- **세 가지 프로그램 종류, 세 가지 역할:**
  - **모션 프로그램**(`open prog N…close`, `&x`로 주소 지정) = 좌표계 내 조율된 **경로** 모션
    (linear/circle/pvt/spline, F/TA/TS, G-code). 머신 로직용 아님.
  - **Script PLC**(`open plc N…close`) = 비동기 **로직 / I/O / 시퀀싱**. 스캔당 한 번의 전체 패스를
    돌고 양보 — 절대 `while(1)`로 블로킹하지 말 것. `cmd "…"`로 `jog`/프로그램 제어 발행. 경로 이동용 아님.
  - **C** = **CPLC**(RTI에서 도는 실시간 PLC 하나, 짧고 non-blocking이어야 함) 또는 **capp**
    (백그라운드 C 앱, OS 기능 사용 가능). 추가로 Script에서 호출 가능한 **CfromScript** 함수.
- **고정된 4개 우선순위 계층(높→낮): Phase → Servo → RTI → Background.** 각각 하위를 선점.
  모션 계획, 포그라운드 PLC, 모터 *안전 점검*은 **RTI** 율로 실행. Background는 고정 주기가 없음 — 타이밍을 의존하지 말 것.
- **좌표계 vs 모터.** `&x` C.S. 명령(`run`/`abort`/`hold`/`%`)은 그룹 전체에, `#x` 모터 명령
  (`jog`/`home`/`kill`)은 한 모터에 작용. 축 정의(`#1->10000X`)는 모터를 C.S. 축에 연결하고 **축 단위 ≠ 모터 단위**로 설정.
- **RAM vs flash.** 프로젝트 다운로드는 RAM에만 존재. `save`가 SAVED 셋업 + 프로젝트를 flash에 영속;
  `$$$` 리셋은 flash에서 복원; `$$$***` = 공장 기본값. 미저장 변경은 리셋에서 사라짐.

## Top gotchas (전체 목록: `reference/gotchas.md`)
1. **미저장 변경은 리셋/전원 사이클에서 사라짐.** SAVED 요소를 바꾼 뒤 `save`. 비저장 셋업은 매 부팅마다
   재적용해야 함(시작 PLC).
2. **`kill` ≠ `abort` ≠ `disable`.** `kill/k` = 개루프, amp off, *감속 없음*(즉시). `abort` =
   제어된 폐루프 감속 + 프로그램 정지. 수직/중력축에선 **지연** 형(`dkill`/`ddisable`)을 써서 브레이크가 먼저 체결되게.
3. **대부분의 안전은 기본 OFF.** 소프트웨어 리미트(MaxPos=MinPos=0), encoder-loss(pEncLoss=0),
   amp-fault, abort-all — 구성 전까지 모두 비활성. FatalFeLimit가 주된 런어웨이 가드; 0으로 만들지 말 것.
4. **모터 단위 vs 축 단위.** Jog/home/limit/FatalFeLimit은 **모터** 단위; 축(공학) 단위는 축 정의 스케일에서 옴.
   모터 단위 재스케일은 모든 모터 단위 리미트를 조용히 재스케일함.
5. **실시간에서 블로킹 금지.** Phase/Servo/RTI/CPLC 코드는 짧고 non-blocking이어야 함; 오버런은
   ServoErrorCtr/PhaseErrorCtr를 증가시키거나 watchdog를 트립. RTI는 초당 40회 이상 돌아야 함.
6. **PLC 스캔 의미.** background Script PLC는 한 패스를 돌고 양보; 무한 내부 루프는 다른 PLC를 굶기고
   watchdog를 트립시킬 수 있음. 블로킹 대기 대신 타이머/상태머신을 쓸 것.
7. **`[]` 안의 index = 상수 또는 단일 지역변수, 식 불가.** `Motor[L0+1]`는 불법 — 먼저 `L0`에 계산.
   런타임 범위 초과 index는 **오류 없이** 손상만 일으킴.
8. **Q는 좌표계별; PLC는 `Ldata.Coord`를 설정해야** 안 그러면 잘못된 Q-집합을 읽음. P는 전역;
   L/R/C/D는 지역이고 서로 alias되며 프로그램 간 넘어가지 않음.
9. **한 모터를 두 소유자가 구동하지 말 것**(실행 중 프로그램 + jog) → error 46
   `COORD JOGGED OUT OF POSITION`; 재개 전 `pmatch`.
10. **Gate3[i] 핵심 셋업 쓰기엔 먼저 `Sys.WpKey=$AAAAAAAA`**, 아니면 쓰기가 조용히 무시됨.
    없는 하드웨어 요소를 읽으면 `nan` 반환, 이후 연산으로 전파됨.

## 정확성 규율 (어시스턴트용)
- Power PMAC 요소/명령 이름은 정확하고 버전 민감. 요소나 키워드 존재가 불확실하면 **사용 전 `grep
  reference/raw/`로 확인** — 이름을 지어내지 말 것.
- "(verify: …)"로 표시된 정제 노트는 미확인; 의존 전 raw로 검증.
- 비자명한 규칙을 말할 땐 출처 페이지를 인용해 사용자가 매뉴얼을 확인할 수 있게.
- 참고: reference 문서들은 Obsidian 볼트에서 읽히도록 YAML frontmatter(title/tags 등)와 문서 하단
  `## 관련 문서` wikilink를 가짐. 이는 사람이 읽기 위한 메타데이터이며 스킬 동작에는 영향 없음.
