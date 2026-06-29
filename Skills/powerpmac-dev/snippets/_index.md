---
title: 코드 스니펫 인덱스
aliases: [코드 스니펫, Snippets Index]
tags: [powerpmac/snippet, type/snippet]
domain: snippet
status: stable
updated: 2026-06-29
---

# 📑 코드 스니펫 인덱스

검증된 예제 코드 모음. Obsidian 그래프는 `.md`만 연결하므로, 실제 코드 파일은 아래 경로 링크로 연다.
(코드 파일은 본문에 임베드되지 않고 외부 파일로 열림.)

| 파일 | 언어 | 내용 |
|---|---|---|
| [motion-examples.pmc](motion-examples.pmc) | Script 모션 | linear/circle/pvt/spline, S-curve, 루프 등 검증된 모션 프로그램 (출처: UM p867–869) |
| [plc-examples.plc](plc-examples.plc) | Script PLC | 상태머신, 타이머, I/O, 에지 트리거 + **라이브 검증 limit→home 호밍 PLC**(plc1=로컬, plc2=EtherCAT) |
| [kinematics-example.pmc](kinematics-example.pmc) | Script 키네매틱스 | 좌표 변환·행렬 연산 서브프로그램 예제 |
| [c-cplc-examples.c](c-cplc-examples.c) | C | RTI CPLC 및 백그라운드 PLC 예제(인터럽트·실시간 루프) |

## 주요 스니펫 안내
- **plc-examples.plc 하단 호밍 PLC**: "음방향 jog → 마이너스 리미트 → 반전/+이동 → 홈 센서 → 완료"
  시퀀스를 PLC 실행만으로 구현(online 명령 불필요). 로컬(M1)은 Gate3 `CaptCtrl` 캡처 + native limit 정착,
  EtherCAT(M2)은 60FD 비트 직접판정 + `homez`. 상세 배경: [[setup-workflow|셋업 워크플로우]]의
  "호밍 — 리미트 탐색 후 홈 센서" 섹션과 [[경험_메모리/ppmac-ck3m-2motor-setup|CK3M 2모터 라이브 셋업]].

## 관련 문서
- [[script-motion|Script 모션 프로그램]] — 모션 예제의 이론
- [[script-plc|Script PLC 프로그램]] — PLC 예제의 이론
- [[c-programming|C 프로그래밍 가이드]] — C 예제의 이론
- [[🗺️ PowerPMAC 지식맵]] — 전체 지식맵(MOC)
