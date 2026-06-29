---
title: Navigation — 도메인→매뉴얼 페이지 맵
aliases: [Navigation, 네비게이션]
tags: [powerpmac/meta, type/reference]
domain: meta
status: stable
updated: 2026-06-29
---

# Source Navigation Map (도메인 → 매뉴얼 페이지 범위 맵)

원본 코퍼스(raw corpus)는 `reference/raw/<slug>/pXXXX-YYYY.txt`에 위치(20페이지/청크, UTF-8, grep 가능).
아래 페이지 번호는 **PDF 페이지 번호**(청크 내 `===== PAGE N =====` 마커와 일치).
정제(distill)하거나 답변을 작성할 때 관련 범위만 읽을 것 — 전체 매뉴얼을 읽지 말 것.

슬러그(slug): `user-manual` (882p), `software-ref` (1711p), `training` (630p), `c-programming` (37p).

---

## DATA STRUCTURE  (Sys. / Motor[] / Coord[] / Gate3[] / legacy P,Q,M,I,L)
- **software-ref p57–76** — COMMAND SYNTAX SUMMARY (연산자, 수학/벡터/행렬 함수,
  on-line vs buffered 명령 분류). ★ 가장 간결한 구문 소스.
- software-ref p79–632 — SAVED 데이터 구조 요소 (Sys., Motor[], Coord[], Gate3[]…).
- software-ref p633–776 — NON-SAVED 셋업 요소 (EtherCAT 사이클릭 I/O 포함).
- software-ref p777–918 — STATUS 요소.
- user-manual p548+ — 연산 기능(Computational Features): P, Q, M, L, 사용자 정의 변수, 포인터.

## SCRIPT MOTION PROGRAMS  (prog: linear/circle/PVT/spline, coord sys, kinematics, G-code)
- user-manual p659–711 — Script 프로그램 작성/실행 (모션, 로터리, 서브프로그램,
  키네마틱 서브루틴), 좌표계 주소 지정, 실행 시작/정지.
- user-manual p703 — 표준 G-코드(Standard G-Codes).
- user-manual p469–500 — 개별 모터 이동 실행 (jog, home 등).
- user-manual p501–547 — 좌표계 셋업; 키네마틱 프로그램 버퍼 (p513).
- user-manual p712–803 — 이동 모드 궤적(Move Mode Trajectories); Lookahead (p779–803).
- user-manual p868+ — 예제 Script 프로그램(EXAMPLE SCRIPT PROGRAMS) ★ 스니펫 수확 지점.
- software-ref p70–72 — Move / Move-mode / Axis-attribute / Move-attribute 명령.
- training — 실제 모션 예제 (grep: `linear`, `pvt`, `circle`, `dwell`).

## SCRIPT PLC PROGRAMS  (plc: background logic, timers, command(), I/O sequencing)
- user-manual p686–699 — PLC 프로그램; Script PLC 실행 시작/정지.
- software-ref p72–73 — Program Logic Control; Script PLC 실행 제어.
- training — 실제 PLC 예제 (grep: `plc`, `while`, `command`).

## C PROGRAMMING  (CPLC real-time, capp background, C API, gplib, pp_proj)
- **c-programming (full 37p)** — C 프로그래밍 집중 가이드. ★ 1차 소스.
- user-manual p845–867 — Power PMAC에서 C 함수 및 프로그램 작성.
- user-manual p862 — CfromScript (Script에서 C 호출).

## TASK MODEL & GOTCHAS  (real-time vs background, clocks, save/reset, safety)
- user-manual p61–91 — 시스템 구성; 실시간 인터럽트 태스크 (p73), Background (p75).
- user-manual p423–468 — Power PMAC 애플리케이션을 안전하게 만들기 (리미트, 추종오차).
- user-manual p548–549 — RTI vs Background 연산 컨텍스트.

---
NOTE: PDF originals are NOT committed (.gitignore). To regenerate the raw corpus from the
PDFs, run `tools/extract_pdfs.py` with the manuals present in `../Power PMAC Manual/`.

## 관련 문서
- [[lecture-series|강의 시리즈 인덱스]]
- [[training-course|트레이닝 코스 인덱스]]
- [[🗺️ PowerPMAC 지식맵]] — 전체 지식맵(MOC)
