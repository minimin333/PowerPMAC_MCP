---
title: 강의 시리즈 인덱스 (심화 이론)
aliases: [강의 시리즈, Lecture Series]
tags: [powerpmac/servo, type/reference]
domain: servo
status: stable
updated: 2026-06-29
---

# 강의 시리즈 인덱스 (OMRON / Curt Wilson)

심화 이론 강의 시리즈(Lecture Series). 각 주제별 원문 텍스트(transcript)는 `reference/raw/edu/lecture/`
(로컬 전용, gitignored)에 있으니, **세부 내용은 grep으로 조회**할 것. Encoder & Commutation(인코더 & 정류)은 이미
`servo-internals.md`에 정제되었으며, 나머지 항목은 포인터 역할만 함(질문이 깊어지면 raw 파일을 grep할 것).

| 주제(Topic) | 핵심 내용(Key content) | Raw 파일 (`raw/edu/lecture/…`) |
|---|---|---|
| **Encoder Processing** | 인코더 종류, 1/T & arctangent 세분화 카운트, ECT, 오류 검사, absolute/resolver → **`servo-internals.md`** | `2022.06.21 - Encoder Processing.txt` (+ `_Korean.txt`) |
| **Trajectory Generation** | 지령 궤적(command-trajectory) 중심, 보간(interpolation), 블렌딩(blending); 모션 컨트롤러의 핵심 차별점 | `2022.08.09 - Trajectory Generation.txt` |
| **Clock Generation & Task Control** | Phase/Servo/RTI 클럭 소스 및 분주(divider); 경성 실시간(hard real-time); 태스크 우선순위 Phase>Servo>RTI>Background | `2022.09.08 - Clock Generation.txt` |
| **Safety Features** | 런어웨이 방지, `FatalFeLimit`, `EncLoss`, amp-fault, 리미트, watchdog, abort — **`gotchas.md`의 확장** | `2022.10.18 - Safety Features.txt` |
| **Compensation & Cam Tables** | 보정 테이블(compensation table, 0/1/2/3-D, 매 서보 주기 적용, 계통 오차 보정); 캠 테이블(cam table, 모션 생성) | `2022.11.15 - Compensation and Cam Tables.txt` |
| **Motor Commutation** | 브러시리스/스테퍼, 개루프/폐루프, 정현파(sinusoidal), phase referencing, sine vs Direct-PWM → **`servo-internals.md`** | `2023.01.19 - Motor Commutation.txt` |
| **Motor Current Control** | T=KT·I; 전압→전류; PMAC가 디지털로 전류 루프를 폐루프 구성; dq 프레임 PI 루프 | `2023.02.23 - Power PMAC Motor Current Control.txt` |
| **Servo Control Part 1** | 위치/속도 루프 배치; PID + 피드포워드(feedforward); 컨트롤러/드라이브/모터 간 역할 분담 | `2023.03.30 - Power PMAC Servo Control (Part 1).txt` |
| **Servo Control Part 2** | 고급 주제: 노치/로우패스 필터(notch/low-pass filter), 피드포워드, 안티바이브레이션(anti-vibration) | `2023.05.23 - Power PMAC Servo Control (Part 2).txt` |
| **Script Language** | 범용 언어 대신 전용 모션 Script 언어를 쓰는 이유 — `syntax-rules.md`/`script-*.md`와 중복 | `2023.07.25 - Power PMAC Script Language.txt` |
| **CNC Applications 1 & 2** | G-code, 고급 이동 블렌딩(move blending), look-ahead, 경로 오차 제어 — `script-motion.md`와 중복 | `2023.09.28 …(Part 1).txt`, `2023.10.31 …(Part 2).txt` |
| **Robotic Applications** | 비직교(non-Cartesian) 기구부, 순기구학/역기구학(forward/inverse kinematics), 경로 계획(path planning) | `2023.12.14 - Robotic Applications.txt` |

Encoder Processing 주제는 한국어 번역본(`…_Korean.txt`)이 존재한다. 영상(.mp4)은 사용자 드라이브의 각 PDF 옆에 있으나
텍스트로 변환되지 않아 수집(ingest)되지 않음.

---

## 관련 문서
- [[servo-internals|서보 내부 동작]] — 강의 이론의 정제본
- [[training-course|트레이닝 코스 인덱스]] — ODT 트레이닝 자료
- [[NAVIGATION|Navigation Map]] — 매뉴얼 페이지 맵
- [[🗺️ PowerPMAC 지식맵]] — 전체 지식맵(MOC)
