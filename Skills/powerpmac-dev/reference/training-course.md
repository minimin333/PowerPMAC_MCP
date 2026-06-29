---
title: 트레이닝 코스 인덱스 (ODT 5-Day)
aliases: [트레이닝 코스, Training Course]
tags: [powerpmac/project, type/reference]
domain: project
status: stable
updated: 2026-06-29
---

# 트레이닝 코스 인덱스 (ODT 초급자 과정)

실습 중심 셋업/프로그래밍 과정. 원본 파일은 `reference/raw/edu/training/` (로컬 전용, gitignored).
대부분의 주제는 기존 레퍼런스 문서와 겹치므로 "Maps to" 열을 참조. 브링업(bring-up) 절차는
`setup-workflow.md`에 정리되어 있으며, 과정별 실습 내용은 raw 파일에서 grep으로 검색.

| # | 주제(Topic) | Maps to | Raw 파일 (`raw/edu/training/…`) |
|---|---|---|---|
| 00 | 트레이닝 소개 (ODT 개요) | — | `00- PPMAC Training Introduction.txt` |
| 01 | ACC24E3 하드웨어 개요(Hardware Overview) | hardware | `01- Power UMAC ACC24E3 Hardware Overview.txt` |
| 02 | IDE 개요(IDE Overview) | `setup-workflow.md` | `02- Power PMAC IDE Overview.txt` |
| 03 | 구조체 & 변수(Structures & Variables) | `data-structure.md`, `syntax-rules.md` | `03- …Structures & Variables.txt` |
| 04 | 트레이닝 머신 (XYZC 검사 장비) | — | `04- UMAC Training Machine.txt` |
| 05 | 조깅(Jogging) / 플롯(Plot) / 중단(Abort) | `setup-workflow.md` (Jog) | `05- Jogging Plot Abort.txt` |
| 06 | 홈잉(Homing) & 트리거 이동(Triggered Moves) | `setup-workflow.md` (Homing) | `06- Homing & triggered Moves.txt` |
| 07 | 좌표계(Coordinate Systems) & 모션 프로그램(Motion Programs) | `script-motion.md` | `07- Coordinate Systems & Motion Programs.txt` |
| 08 | 멀티태스킹(Multitasking) & PLC | `script-plc.md`, `gotchas.md` | `08- Multitasking & PLCs.txt` |
| 09 | 서브프로그램(Subprograms) & 서브루틴(Subroutines) | `script-plc.md` | `09- Subprograms and Subroutines.txt` |
| 10 | 초급자 최종 실습 (Estop/Reset/Pendant PLC + 사행(serpentine) 스캔) | — | `10- Beginners Final Exercise.txt` |
| 11 | 시스템 구성(`$$$***`→`save`→`$$$`) | `setup-workflow.md` | `11- …System Configuration.txt` |
| 12 | 엔코더(Encoder) 구성 | `servo-internals.md` (ECT) | `12- ACC24E3 Encoder Configuration.txt` |
| 13 | 모터 & 앰프(Motor & Amp) 구성 | `setup-workflow.md` (local setup) | `13- ACC24E3 Motor Amp Config.txt` |
| 14a | 전류 루프(Current Loop) 튜닝 | `servo-internals.md` (Direct-PWM) | `14a- Current Loop Tuning.txt` |
| 14b | 모터 페이징(Motor Phasing) | `servo-internals.md` (phase referencing) | `14b- Motor Phasing.txt` |
| 14c | 위치 루프(Position Loop) 튜닝 | `setup-workflow.md` (tuning) | `14c- Position Loop Tuning.txt` |

**코스 셋업 순서 체인**(11–14에서 반복): `$$$***`/`save`/`$$$` → 기본 시스템 & 지배 클럭(dominant clock) 설정
→ 엔코더 구성(ECT) → 모터 & 앰프 구성 → 모터 커미셔닝(phasing) → 조깅(jog)/홈잉(home) → 모션 / PLC / HMI.

---

## 관련 문서
- [[lecture-series|강의 시리즈 인덱스]] — 심화 이론 강의
- [[script-plc|Script PLC 프로그램]] — 트레이닝의 PLC 주제 정제본
- [[script-motion|Script 모션 프로그램]] — 트레이닝의 모션 주제 정제본
- [[NAVIGATION|Navigation Map]] — 매뉴얼 페이지 맵
- [[🗺️ PowerPMAC 지식맵]] — 전체 지식맵(MOC)
