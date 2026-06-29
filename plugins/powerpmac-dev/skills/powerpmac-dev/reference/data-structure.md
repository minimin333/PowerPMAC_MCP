---
title: 데이터 구조 모델 (Structure[index].Element)
aliases: [데이터 구조, Data Structure]
tags: [powerpmac/data, type/reference]
domain: data
status: stable
updated: 2026-06-29
---

# Power PMAC 데이터 구조(Data Structure) 모델

Power PMAC는 임베디드 펌웨어가 직접 사용하는 것과 동일한 **사전 정의 데이터 구조(pre-defined data structures)**를 통해
거의 모든 메모리와 I/O 레지스터를 노출한다. 사용자는 주소를 직접 정의하지 않고 이름만으로 제어·상태(status) 레지스터에
접근한다 (UM p552). SWREF p79+ (요소(element) 사전)와 UM p552–554를 요약한 문서. 모든 요소(element)를 나열하지 않으며,
아래 grep 포인터를 활용할 것.

## 주소 지정 모델 (UM p553, SWREF p1184)
```
Structure[index].Element
Structure[index].SubStructure[index].Element     // nested
```
- **인덱스(Index)**는 **대괄호** `[ ]` 안에 쓰며, 음이 아닌 정수 **상수** 또는 **단일 지역 변수** 하나만 허용
  (인덱스 안에 수식 불가). 소수점 이하는 내림(floor, 0 방향)으로 정수화됨. 인덱스는 모든 인덱서블 구조에서
  **0부터 시작** (UM p553).
- 값 읽기: `{element}` 전송 (예: `Motor[1].JogSpeed`). **주소** 읽기: `{element}.a`
  (예: `Coord[3].PathDistance.a`, 포인터 소스 설정에 사용). 쓰기: `{element}={expression}`.
- 중첩 하드웨어 예: `Gate3[i].Chan[j].ServoCapt`. 존재하지 않는 하드웨어 요소(element)를 읽으면 `nan` 반환.
- 진짜 배열 인덱싱은 `[ ]`; *번호 변수* 선택은 `( )` (배열 함수) —
  예: `Sys.P[i]` (요소 배열, 대괄호) vs `P(i)` (변수 함수, 소괄호).

### 주요 구조(Structure) 인덱스 범위 (UM p554)
| 구조(Structure) | 인덱스 범위 | 의미 |
|---|---|---|
| `Motor[x]` | 0–255 | `#x` 모터 |
| `Coord[x]` | 0–127 | `&x` 좌표계(coordinate system, C.S. 0 = 미할당 모터의 "파킹" 좌표계) |
| `EncTable[n]` | 0–767 | 엔코더 변환 테이블 엔트리 |
| `CompTable[m]` | 0–255 | 보상 테이블 |
| `CamTable[m]` | 0–255 | 캠 테이블 |
| `Gate1[i].Chan[j]` | i 0–19, j 0–3 | DSPGATE1 서보 IC, 채널 j+1 |
| `Gate2[i].Chan[j]` | i 0–15, j 0–3 | DSPGATE2 MACRO IC, 채널 j+1 |
| `Gate3[i].Chan[j]` | i 0–15, j 0–3 | DSPGATE3 범용 IC, 채널 j+1 |
| `GateIo[i]` | 0–15 | I/O ASIC 보드 |

지역 변수가 인덱스를 공급하는 경우, `L0`..`L(1022 − MaxConstantIndex)` 범위만 유효
(예: 모터: `L0`..`L767`) (UM p554).

## 최상위 구조(top-level structures)와 영속성(persistence) 분류
영속성 분류는 해당 구조의 요소(element)가 SWREF 어느 챕터에 등장하는지로 결정된다: **SAVED**
(p79–632), **NON-SAVED 셋업** (p633–776), **STATUS** (p777–918). 대부분의 주요 구조는
두 개 이상의 분류에 걸쳐 요소를 가진다.

| 구조(Structure) | 인덱스 여부 | 관장 범위 | SAVED 있음 | NON-SAVED 있음 | STATUS 있음 |
|---|---|---|---|---|---|
| `Sys.` | 없음 | 시스템 전역: 클럭(`ServoPeriod`,`RtIntPeriod`), `MaxRtPlc`, `MaxMotors`, 상태(status), `P[]`/`M[]` 배열 | 있음 | — | 있음 |
| `Motor[x].` | 0–255 | 모터별 셋업 & 모션: jog/home/리미트(limit)/서보 게인; 루프 알고리즘 서브구조 `Motor[x].Servo.` | 있음 | 있음 | 있음 |
| `Coord[x].` | 0–127 | 좌표계(C.S.)별: 축 정의, 이동 파라미터(`Ta`,`Ts`,`Tm`), `Q[]`, `Ldata.`, G/M/T/D 프로그램 디스패치, 경로 계산 | 있음 | 있음 | 있음 |
| `Gate1[i].` | 0–19 | DSPGATE1 (PMAC2-style) 서보 ASIC: PWM, 클럭, 채널 | 있음 | — | 있음 (ASIC) |
| `Gate2[i].` | 0–15 | DSPGATE2 (PMAC2-style) MACRO/IO ASIC | 있음 | — | 있음 |
| `Gate3[i].` | 0–15 | DSPGATE3 (PMAC3-style) 범용 ASIC: `Chan[j]` 서보/엔코더/DAC | 있음 | — | 있음 |
| `GateIo[i].` | 0–15 | I/O ASIC 보드 (ACC-11/14/등) | 있음 | 있음 | 있음 |
| `EncTable[n].` | 0–767 | 엔코더 변환 테이블: 피드백/마스터 처리 | 있음 | — | (결과는 status에) |
| `CompTable[m].` | 0–255 | 보상 테이블 (`.Data[i]`/`[j][i]`/`[k][j][i]`; 2D/3D 배열로 활용 가능) | 있음 | 있음 | 있음 |
| `CamTable[m].` | 0–255 | 캠 테이블 | 있음 | 있음 | 있음 |
| `BufIo[i].` | — | 버퍼드(강제) I/O 스캔(scan) | 있음 | 있음 | 있음 |
| `AdcDemux.` | 없음 | ADC 역다중화(de-multiplexing) | 있음 | — | 있음 |
| `BrickAC.` / `BrickLV.` | (`.Chan[j]`) | Power Brick AC / LV 앰프 (다채널 & 단채널) | 있음 | 있음 | 있음 |
| `Plc[i].` | — | Script PLC 프로그램 런타임 (`Ldata.`) | — | 있음 | (Ldata를 통한 status) |
| `Gather.` | 없음 | 데이터 수집(data-gathering) 기능 | — | 있음 | — |

`Acc24E3[i]`, `Acc5E3[i]`, `Acc11E[i]`, `Acc84E[i]` 같은 액세서리 이름은 내부 `Gate*`/`GateIo` 구조에
대한 **Script 별칭(alias)**이다 (SWREF p79–80). `PowerBrick` 컨트롤러는 동일한 `Motor[]`/`Coord[]`/`Gate3[]`/`BrickAC`/`BrickLV` 모델을 노출한다 (정확한 요소는 SWREF에서 `BrickAC.` / `BrickLV.`로 grep하여 확인).
`Cam[]`은 최상위 구조 이름이 아니며 — 캠 기능은 `CamTable[m]`이다 (verify: SWREF p113).

## SAVED vs NON-SAVED vs STATUS (SWREF p79; UM p552)
| 분류 | `save`로 flash에 복사? | 전원 투입/리셋 시 복원? | 앱에서 쓰기 가능? | 용도 |
|---|---|---|---|---|
| **SAVED 셋업** | YES | YES (활성 메모리 재로드) | 예 | 기본 설정(게인, 리미트(limit), ID) |
| **NON-SAVED 셋업** | NO | NO (매 부팅마다 기본값) | 예 | 런타임/휘발성 셋업, EtherCAT 사이클릭 I/O 포함 |
| **STATUS** | NO | NO | NO — 읽기 전용 | 라이브 상태(status)/피드백; 쓰기 무의미 |

왜 중요한가:
- `save`는 SAVED 요소(element)만 영속시킨다. NON-SAVED 셋업은 매 부팅마다 시작 PLC / 프로젝트
  로드로 재적용해야 한다.
- `$$$`는 리셋 후 **flash에서 SAVED 값을 복원**하고, `$$$***`는 **공장 기본값으로 초기화**
  한다 (SWREF p62). `backup`은 현재 SAVED 값을 보고한다.
- 애플리케이션 코드에서 STATUS 요소에는 절대 쓰지 말 것.

## 레거시 Turbo-PMAC I/M-변수 매핑 vs 구조 모델 (UM p559–560)
- 구조 모델은 Turbo PMAC의 `I`-변수와 사용자 `M`-변수 할당을 **대체**하지만, 편의를 위해 레거시 별칭은 유지된다.
- Turbo I-변수와 매핑되는 SAVED 요소는 **동일한 I-번호**로도 접근 가능하다.
  예: `Motor[1].HomeVel` ≡ `I123`. 매핑은 `I{n}->` 쿼리로 확인 (예: `I123->` → `Motor[1].HomeVel`;
  `I5198->` → `Coord[1].MaxFeedRate`; `I8010->` → `Sys.ServoPeriod`).
- 번호 체계: 모터 `Mxxyy` 방식 → `I0`–`I99` 모터 0 예약, `I5000`–`I5099` C.S. 0, 전역 셋업은
  `I8000`–`I8099`로 이동. `I8192`–`I16383`은 범용 더블형 자유 변수.
- 추가 모터/C.S., 신기능 등 신규 요소에는 I-변수 별칭이 **없으나** 동작은 동일하다.
- 신규 코드에서는 구조 이름을 우선 사용하고, `I`-번호는 읽기 전용 레거시 단축어로만 취급할 것.

## 공식 요소(element) 목록 (펌웨어 테이블)
컨트롤러 자체의 인텔리센스 테이블이 *실제 존재하는 요소(element)*에 대한 신뢰 원천이다:
- `reference/firmware/ELEMENTS_INDEX.md` — 모든 최상위 구조 + 요소 수 (구조 75개,
  총 1828개 엔트리; Motor[] 307, Coord[] 231, Sys 151, …).
- `reference/firmware/pp_swtbl1.txt` / `pp_swtbl2.txt` — 전체 `Structure,Element` 목록;
  grep으로 검색 (예: `^Motor,`, `^Coord,`)하여 사용 전 정확한 요소 이름을 확인.
- `reference/firmware/headers/RtGpShm.h` — 동일 데이터의 C 구조체 뷰.
이 파일들은 시뮬레이터 fw 2.3.1.82 기준; 실제 컨트롤러는 2.8.3.0이므로 엣지 케이스는
MCP `get_response`로 검증할 것.

## 요소(element) 검색 방법
- `reference/raw/software-ref/`에서 구조 접두사(예: `Motor[`, `Coord[`, `Sys.`,
  `Gate3[`, `EncTable[`, `CompTable[`)로 SWREF 원본 청크를 grep.
- 영속성 분류 = 해당 히트가 속한 챕터: SAVED p79–632, NON-SAVED p633–776,
  STATUS p777–918 (각 청크의 페이지 마커 활용).
- 알파벳순 요소 인덱스: `reference/raw/software-ref/p0001-0040.txt` (목차) — 요소 이름으로
  grep하여 페이지를 찾은 후 해당 `pXXXX-YYYY.txt` 파일을 열 것.

## 더 깊은 내용 (raw 청크)
- 주소 지정 모델 & 인덱스 규칙, 최상위 구조 목록: `reference/raw/user-manual/p0541-0560.txt`
  (UM p552–554).
- SAVED 챕터 소개 + 액세서리 별칭: `reference/raw/software-ref/p0061-0080.txt` (SWREF p79–80).
- 구조별 목차 (구조/분류별 페이지 번호):
  `reference/raw/software-ref/_toc.txt` ("Saved/Non-Saved/Status Data Structure Elements" 검색).
- `Sys.` 저장 요소 SWREF p605 → `p0581-0600.txt`/`p0601-0620.txt`; `Motor[x]` saved p417;
  `Coord[x]` saved p138; `Gate3[i]` (`Gate3[`로 grep).

---

## 관련 문서
- [[syntax-rules|구문 규칙]] — 요소를 가리키는 변수·연산자
- [[setup-workflow|셋업 워크플로우]] — 요소 단위 모터/클럭 설정
- [[firmware/ELEMENTS_INDEX|Elements Index]] — 정식 요소 전체 목록
- [[gotchas|Gotchas]] — SAVED/NON-SAVED/STATUS 분류 함정
- [[🗺️ PowerPMAC 지식맵]] — 전체 지식맵(MOC)
