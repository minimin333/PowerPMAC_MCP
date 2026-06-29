---
title: 서보 내부 동작 (엔코더·커뮤테이션)
aliases: [서보 내부, Servo Internals]
tags: [powerpmac/servo, type/reference]
domain: servo
status: stable
updated: 2026-06-29
---

# 서보 내부 동작: 엔코더(Encoder) 처리 & 커뮤테이션(Commutation)

`setup-workflow.md`의 브링업/튜닝 아래에 있는 피드백(feedback) → 커뮤테이션 체인에 대한 심층 분석.
OMRON 강의 시리즈(Curt Wilson)에서 정리. 원본 트랜스크립트: `reference/raw/edu/lecture/`
(로컬 전용, gitignored). 전류 루프 / 서보 루프 / 클럭 / 안전 상세 내용은
`lecture-series.md`의 인덱스를 사용한 뒤 raw에서 grep할 것.

## 엔코더(Encoder) 종류 및 처리

- **디지털 구상 증분형(Digital quadrature incremental)**: A/B, ×4 디코드(4 counts/line); 방향은 위상(phase)으로 판별; SCLK에서 샘플링(사이클당 한 엣지); 전원 투입 기준점에 index (C/I/Z) 필요; 전원 투입 시 절대 위치 없음.
- **3상 홀(Hall)**: 1 cycle/pole-pair (저분해능). 두 가지 용도 — 전원 투입 시 거친 phase referencing(상 정렬)(UVW 입력을 통해 ±30°e, index 호밍 후 보정), 또는 3상 구상 1차 피드백(속도 응용).
- **아날로그 사인파(Analog sinewave)**: sin/cos 아날로그; 보간 필요(라인 카운트 + 라인 내 아크탄젠트(arctangent)).
- **절대 직렬(Absolute serial)**: 직렬 프로토콜, ≥1 rev에 걸쳐 절대 위치; DSPGATE3 = 9가지 프로토콜(SW로 1개 선택); 시간 지연 존재; 1/T sub-count 정보 없음.
- **리졸버(Resolver)**: 이중 회전 변압기, sin/cos에서 atan2, 1 사이클 내 절대 위치, 견고하나 저분해능(low-res); 여기(excitation) + 샘플링이 **Phase 클럭**에 동기; ECT 저역 통과 필터 강력 권장.

## Sub-count 확장 (저속 부드러움 향상, 정지 시 분해능(resolution) 향상 아님)

- **1/T**: HW 타이머 2개(마지막 2카운트 간 시간 / 마지막 카운트 이후 시간); `Pos = Counter ± T2/T1`, servo/phase 인터럽트 및 외부 캡처 시점에 평가. DSPGATE3는 HW에서 처리.
- **아크탄젠트(Arctangent)** (사인 엔코더): 라인 카운터 + sin/cos ADC로 라인 내 위치 계산; servo 율로 합산 (예: 4096 states/line). DSPGATE3 ASIC에서 HW 처리 (PMAC2에서는 ECT 소프트웨어).

## 사인파 엔코더(Sinusoidal encoder) 오류 모델 (위치 오차 E vs 신호 결함)

Sine 오프셋 `E=x·cosθ` · Cosine 오프셋 `E=-x·sinθ` · 진폭 불일치 `E=(x/2)·sin2θ` ·
위상 오차 `E=-φ·cos2θ`. **ACI** (자동 보정 보간기, FPGA @20 MHz)는 오차에 대해 푸리에 변환을 수행하고 고조파를 빼냄. 진폭 검사 = 제곱합(Lissajous radius²).

## 오류 검사 (모터를 auto-disable → EncLoss로 설정)

- **구상(Quadrature)**: 카운트 오류(한 SCLK에서 A&B 모두 변화 → 회복 불가, 재호밍 필요; 신호가 너무 빠르면 SCLK 높이고, 노이즈면 낮춤) + 신호 상실(차동 쌍 XOR).
- **사인파(Sinusoidal)**: XOR 상실 감지 없음; 제곱합 진폭 사용(주의: 진폭은 속도에 따라 감소하므로 ASIC 기본 임계값이 너무 높은 경우가 많음).
- **직렬(Serial)**: 타임아웃(상실) + CRC/패리티(손상); `SerialEncDataB`의 상위 비트에 오류 비트 존재; `EncTable[n].type=12`로 외삽 데이터 대체 가능; N회 오류 스캔 후 트립.

## 엔코더 변환 테이블(ECT, Encoder Conversion Table) — 존재 이유

서보는 하나의 부동소수점 위치를 원하지만, HW는 카운터+타이머 / ADC / 다중 레지스터를 제공함. ECT가 결합 및 전처리:
- **변화량 제한(Change limiting)** (디지털 비트 오류 통과): `.index3=0`이면 1차 미분(속도)을 1 사이클 제한한 뒤 `.MaxDelta`로 슬루잉; `.index3>0`이면 N 사이클 동안 2차 미분(가속도) 제한; `.MaxDelta=0`=비활성.
- **저역 통과/추적 필터(Low-pass/tracking filter)** (잡음 있는 아날로그/리졸버): `.index2>31`이면 활성; `.index2`=LP 이득 (Tf = 256/(256−.index2) −1 서보 사이클), `.index1`=적분 이득 (`.index4`=지수).
- 롤오버를 위한 적분/미분, 시프트/스케일/차분/클리핑도 포함.

## 위치 비교 (HW EQU 출력)

`CompA`/`CompB`/`CompAdd` 설정; HW는 엔코더 위치(1/T 포함)가 비교값에 도달하면 EQU를 토글하고, 이후 소프트웨어 없이 자동 증분 — 정확하고 균등 간격의 펄스 출력.

## 절대(Absolute) vs 증분(Incremental) 기준점 설정

증분형 → 전원 투입마다 재기준 설정: **phasing-search** (커뮤테이션 각도) + **homing-search** (전체 위치). 절대형 → 조립 시 한 번만 기준 설정; PMAC이 센서 영점과 모터 영점 사이의 오프셋을 저장. 전원 투입 시 읽기는 전 범위 커버 (흔히 멀티턴; `SerialEncDataA`+`B`); 이후 읽기 = 단일 32비트, 변화량 계산 (롤오버 시 짧은 방향 가정). 멀티턴 기술: 기어링 / 배터리 카운터 / Wiegand(전원 없이 턴 수 유지).

## 모터 커뮤테이션(Motor Commutation)

- **이유**: 브러시리스(brushless) = 전자식 커뮤테이션 동기 AC; 토크 방향 유지를 위해 상간 전류 방향 전환; 사인파 커뮤테이션은 정밀한 토크를 위해 크기를 변화시킴.
- **위치**: 서보 루프가 토크(전류 크기) 명령을 출력 → 커뮤테이션이 회전자 자계 각도를 더함 → 상전류(phase-current) 명령 → 전류 루프. **컨트롤러 또는 드라이브**에서 실행 가능.
- **PMAC에서 커뮤테이션하는 이유**: 드라이브로의 피드백 배선 불필요; 저렴하고 범용적이며 단순한 드라이브; 고품질 사인파; 증분형 엔코더만으로 브러시리스 커뮤테이션 가능(+ phasing-search(상 정렬)); AC 유도형 자계 지향(벡터) 제어. **PMAC에서 커뮤테이션 + 서보 통합 ⇒ 피드백 상실/반전 = 토크 상실, 런어웨이 없음** (드라이브 폐루프 모터 대비 핵심 안전 특성).
- **개루프 vs 폐루프**: 개루프(스텝 모터; 센서 없음; 자기력에 의존; silent-stall 위험); 폐루프(서보; 센서 있음; 더 많은 토크 사용; 특성화 가능).
- **알고리즘**: 6단계(Hall, 6 states/elec cycle, 토크 리플 — PMAC은 상시 운전에 사용 안 함); 사인파(전류를 최대 토크 지점에 유지, 효율적, 진동 없음). 스텝 모터: 풀/하프/마이크로(64–512 µsteps; 진동 감소, 정밀도가 반드시 높아지지는 않음).
- **3단계**: (1) 회전자 자계 각도 (동기형: 엔코더 vs 기준; 유도형: + 슬립 어드밴스); (2) 고정자 전류 정렬 — 토크 전류 ⊥ 회전자 자계(직교, quadrature) + 자화 전류 ∥ (직류, direct, 유도형용); (3) 상에 투영(2개 계산, 3번째는 균형 루프로).
- **Phase referencing(상 정렬)** (동기형 모터는 회전자 절대 각도 필요; 잘못된 기준 → 런어웨이, 토크 ∝ cos(angleError)): 절대 센서가 없으면 phasing-search 이동(stepper-search / four-guess); 또는 절대 읽기(절대 엔코더 / 리졸버 / Hall).

## 출력 모드 (커뮤테이션 → 증폭기)

- **사인파 출력(Sine-wave output)**: PMAC이 상전류 명령을 DAC 전압으로 계산(16/18-bit); 전류 루프를 닫지 **않음** (아날로그 앰프가 처리). 초고정밀(낮은 지연/노이즈, 높은 명령 분해능).
- **직접 PWM(Direct-PWM)**: PMAC이 디지털로 전류 루프를 닫음; 트랜지스터에 PWM 듀티 출력; 앰프 ADC가 2개의 상전류 반환; 루프는 **DC (dq) 프레임**에서 닫힘(속도 증가 시 고주파 문제 최소화); 앰프는 "덤" 파워 블록, 모든 설정은 PMAC에서. (DC 브러시 via Direct-PWM: `PhasePos=0` 고정, `PhaseMode` bit2=1로 Id 루프 비활성.)
- **직접 마이크로스텝핑(Direct microstepping)** (Direct-PWM, 개루프, 엔코더 없음 — 방사선 경화 응용): 명령 속도에서의 슬립이 커뮤테이션 각도를 전진; `IdCmd`로 전류 크기 설정.

(전류 루프 튜닝, 서보 루프 알고리즘/필터, 클럭/태스크 구조, 안전, comp/cam → `lecture-series.md`의 인덱스 참고 후 `reference/raw/edu/lecture/` grep.)

---
출처: OMRON Power PMAC 강의 시리즈 (Encoder Processing 2022.06, Motor Commutation 2023.01).
로컬 전용 `reference/raw/edu/lecture/` (gitignored). 개념/요소 전용.

---

## 관련 문서
- [[setup-workflow|셋업 워크플로우]] — 모터 브링업·피드백 설정
- [[data-structure|데이터 구조]] — EncTable[]/Gate3[] 요소 모델
- [[lecture-series|강의 시리즈 인덱스]] — 심화 이론 강의 raw 인덱스
- [[🗺️ PowerPMAC 지식맵]] — 전체 지식맵(MOC)
