---
title: 프로젝트 구조 (.ppproj / pp_proj.ini)
aliases: [프로젝트 구조, Project Structure]
tags: [powerpmac/project, type/reference]
domain: project
status: stable
updated: 2026-06-29
---

# Power PMAC 프로젝트 구조 (OMRON Delta Tau)

Power PMAC IDE / PowerPmacSuite 프로젝트의 표준 레이아웃으로, 샘플 프로젝트 `PPMAC_Project`를 기준으로 정리한다. 솔루션(`.PowerPmacSuite_sln`, MSBuild 형식)은 하나의 `.ppproj` MSBuild manifest(매니페스트)를 참조하며, 프로젝트 트리는 컨트롤러의 `/var/ftp/usrflash/Project/` 아래 배포되는 구조와 동일하다.

샘플의 주요 사양: CPU = ARM `armLS1043A` (LS1043A, 쿼드코어, `4.1.18-ipipe`), 펌웨어 `2.8.3.0`, 크로스 툴체인 `arm-omron49-linux-gnueabihf-`. 빌드 구성(build config)은 `Debug`와 `Release` 두 가지.

---

## 1. 최상위 폴더(folder) 레이아웃

```
PPMAC_Project.PowerPmacSuite_sln        솔루션 파일 (MSBuild fmt 12.00); .ppproj 참조
PPMAC_Project/                          프로젝트 루트 (== 컨트롤러의 /var/ftp/usrflash/Project)
├─ PPMAC_Project.ppproj                 MSBuild 프로젝트 manifest (PropertyGroup + ItemGroup)
├─ PMAC Script Language/                Power PMAC Script 소스 (전처리기로 컴파일, 로드 순서대로)
│  ├─ Global Includes/                  .pmh 헤더: #define, 전역 선언, ECAT/모터 맵 — 가장 먼저 로드
│  ├─ Libraries/                        .pmc 재사용 서브프로그램/서브루틴 (예: 타이머, 호밍 파라미터)
│  ├─ Motion Programs/                  .pmc `open prog N` 모션 프로그램 + `open subprog`
│  ├─ PLC Programs/                     .plc `open plc N` 백그라운드/실시간 PLC
│  ├─ Kinematic Routines/               .pmc 순기구학/역기구학 (`open forward/inverse`) (이 샘플에선 비어 있음)
│  └─ Macro/                            MACRO 링 스테이션 스크립트 (이 샘플에선 없음)
├─ C Language/                          ARM 바이너리로 크로스컴파일되는 C 소스
│  ├─ Background Programs/<app>/        capp 사용자 백그라운드 앱 → <app>.out (Linux 실행 파일) 빌드
│  ├─ CPLCs/<name>/                     컴파일된 PLC: bgcplcNN (백그라운드), rticplc (실시간) → .o_so / libplcc*.so
│  ├─ Realtime Routines/                usrcode.c/.h — 실시간 사용자 서보/페이즈 알고리즘
│  ├─ Include/                          공유 헤더: pp_proj.h, ECATMap.h, PMAC_Interface_Map.h
│  └─ Libraries/                        CPLC/capp에 링크되는 공유 C 라이브러리 (이 샘플에선 비어 있음)
├─ Configuration/                       로드 manifest + 시작/저장 스크립트 + EtherCAT 설정
├─ System/                             하드웨어/모터/인코더/좌표계/EtherCAT용 IDE 설정 파일 (XML)
│  ├─ CPU/                              System.cpusetup
│  ├─ Motors/                           MotorN.msetup (모터당 하나)
│  ├─ Encoder/                          Encoder.encsetup
│  ├─ Coordinate Systems/               Coordinate Systems.cssetup
│  ├─ EtherCAT/                         Master0.ecatmaster, *.ecatslave, *.ecatmodule, EthercatConfig.ecc
│  └─ Hardware/                         CK3W-*.hwsetup (축 인터페이스 / I/O 유닛)
├─ Bin/                                빌드 출력물
│  ├─ Debug/                            capp1.out, libplcc*.so (DebugSymbols=true)
│  └─ Release/                          capp1.out, libplcc1.so (최적화)
├─ Database/                            IDE 심볼 DB: pp_*.sym, *.devdesc.xml, *.PmacDatabase (다운로드(download) 안 됨)
├─ Documentation/                       note.txt 및 사용자 문서
├─ Log/                                 pp_error.log, pp_proj.log, ecmaster0.0.log, error0.0.log
├─ Temp/                                임시 파일 (pp_debug.txt) — 다운로드 안 됨
└─ Tools/                              Tune.tunesetup 및 기타 IDE 도구 상태
```

> 공백이 포함된 폴더(folder) 이름(`PMAC Script Language`, `C Language`, `Global Includes`,
> `Coordinate Systems`, `Background Programs`, `Realtime Routines`)은 표준 명칭이다 —
> `pp_proj.ini` 경로를 포함해 정확히 그대로 유지해야 한다.

---

## 2. 파일 타입

| 확장자 | 설명 | 위치 | 편집 가능? |
|--------|------|------|-----------|
| `.PowerPmacSuite_sln` | 솔루션 파일 (.ppproj 참조) | 프로젝트 루트 | 거의 없음; IDE 관리 |
| `.ppproj` | MSBuild 프로젝트 manifest(매니페스트) (PropertyGroup + ItemGroup) | 프로젝트 루트 | 가능 (신중하게) — pp_proj.ini와 일관성 유지 필요 |
| `.pmh` | Script **헤더**: `#define`, `global`, 매핑 테이블 | `Global Includes/` | 가능 — 직접 작성 또는 IDE 생성 (매핑) |
| `.pmc` | Script 소스: 모션 프로그램, 서브프로그램, 라이브러리, 기구학 | `Motion Programs/`, `Libraries/`, `Kinematic Routines/` | 가능 |
| `.plc` | PLC 프로그램 (`open plc N … close`) | `PLC Programs/` | 가능 |
| `.c` / `.h` | capp, CPLC, 실시간 루틴용 C 소스/헤더 | `C Language/...` | 가능 |
| `.o` | 컴파일된 오브젝트 (중간 산출물) | 해당 `.c` 옆 | 불가 (생성됨) |
| `.out` | Linux ARM 실행 파일 (백그라운드 capp) | `Bin/<cfg>/`, `Background Programs/`에 복사 | 불가 (빌드됨) |
| `.o_so` | CPLC 공유 라이브러리용 위치독립 오브젝트 | CPLC 폴더 | 불가 (빌드됨) |
| `.so` / `libplcc*.so` | 컴파일된 PLC 공유 라이브러리 | `Bin/<cfg>/` | 불가 (빌드됨) |
| `.mak` | 타깃별 makefile (`*_debug.mak`, `*_release.mak`) | capp/CPLC 폴더 | 생성됨; 툴체인/플래그가 여기에 있음 |
| `.msetup` | 모터 설정 (XML) | `System/Motors/` | IDE를 통해; XML 직접 편집 가능 |
| `.encsetup` | 인코더 설정 (XML) | `System/Encoder/` | IDE를 통해 |
| `.cssetup` | 좌표계 설정 (XML) | `System/Coordinate Systems/` | IDE를 통해 |
| `.cpusetup` | CPU/시스템 설정 (XML) | `System/CPU/` | IDE를 통해 |
| `.hwsetup` | 하드웨어 유닛 설정 (XML), 예: CK3W-AX1515 | `System/Hardware/` | IDE를 통해 |
| `.ecatmaster` | EtherCAT 마스터 정의 | `System/EtherCAT/` | IDE를 통해 |
| `.ecatslave` | EtherCAT 슬레이브 노드 | `System/EtherCAT/` | IDE를 통해 (DependentUpon master) |
| `.ecatmodule` | 슬레이브 서브모듈 / PDO 그룹 | `System/EtherCAT/` | IDE를 통해 (DependentUpon slave) |
| `.ecc` | EtherCAT 설정 컨테이너 | `System/EtherCAT/` | IDE를 통해 |
| `.PmacDatabase` | 앰프/디바이스 데이터베이스 | `System/` | 불가 (IDE 데이터) |
| `.sym` | 심볼 데이터베이스 (pp_global/local/prog/subprog) | `Database/` | 불가 (생성됨) |
| `.ini` | `pp_proj.ini` 로드 순서(load order) manifest; `MotorTopology.ini` | `Configuration/`, `System/` | `pp_proj.ini`: 가능 — §4 참조 |
| `.cfg` | 저장/설정 스크립트: pp_save.cfg, pp_custom_save.cfg, ECATConfig.cfg, systemsetup.cfg | `Configuration/` | 생성됨; 편집 가능 |
| `.txt` | pp_startup.txt, pp_disable.txt, pp_inc_*.txt, rsync-filter.txt, Exclude.txt | `Configuration/`, 루트 | 가능 |

---

## 3. `.ppproj` manifest(매니페스트)

XML MSBuild 파일. `<DefaultTargets="Build">`; `Build`/`Rebuild` 타깃은 아래의 커스텀 `CompileTask`를 호출한다.

### 전역 `<PropertyGroup>` (샘플 값)

| 설정 | 샘플 | 의미 |
|------|------|------|
| `PPCPUType` | `armLS1043A;4.1.18-ipipe` | 대상 CPU + 커널; `.mak`의 툴체인 선택 |
| `CPUType` | `arm,LS1043A` | CPU 패밀리/파트 |
| `ProjectFrimwareVers` | `2.8.3.0` | 대상 펌웨어 (IDE의 오타 "Frimware" 주의) |
| `PVarStart` | `8192` | 컴파일러가 자동 할당을 시작할 첫 P 변수 번호 |
| `QVarStart` | `1024` | 자동 할당용 첫 Q 변수 번호 |
| `MVarStart` | `8192` | 자동 할당용 첫 M 변수 번호 |
| `UserBuffer` | `200` | 사용자 공유 메모리 버퍼 (MB 단위; ini = `UserBufSize` 209715200) |
| `ProgramBuffer` | `16` | 프로그램 버퍼 (ini `ProgramBufSize` 16777216) |
| `TableBuffer` | `1` | 테이블 버퍼 (ini `TableBufSize` 1048576) |
| `LookAheadBuffer` | `16` | Look-ahead 버퍼 (ini `LookAheadBufSize` 16777216) |
| `SymbolsBufSize` | `1` | 심볼 버퍼 (ini 1048576) |
| `CpuAffinityType` | `Unknown` | 어피니티 방식 선택자 |
| `CpuAffinityServoTask` | `1` | 서보 태스크용 코어 (ini `[CPU_AFFINITY]` 미러) |
| `CpuAffinityRtiTask` | `2` | RTI 태스크용 코어 |
| `CpuAffinityBackgroundTask` | `0` | 백그라운드 스레드용 코어 |
| `CpuAffinityEcatTask` | `3` | EtherCAT 태스크용 코어 |
| `CpuAffinityPhaseTask` / `…GateCaptureTask` | `1` | 페이즈 / 게이트 캡처 코어 |
| `CpuAffinityBgCplcTask` / `…EipTask` / `…GpAsciiTask` | `0` | Bg-CPLC / EIP / GPASCII 코어 |
| `TelnetPort` / `TelnetUser` | `22` / `root` | SSH/텔넷 전송 (포트 22 = SSH); 프로젝트 로드에 사용 |
| `FTPPort` / `FTPUser` / `FTPPassword` | `21` / `ftp` / `ftp` | 파일 업로드용 FTP 전송 |
| `DownloadCSoruce` | `No` | (sic) No이면 C **소스**를 다운로드(download)에서 제외 — `.out`/`.so`만 전송 |
| `EncryptionOption` | `Do Not Encrypt Any File` | 파일별 암호화 정책 |
| `RealtimeRoutinesBuildFlag` | `0` | `Realtime Routines/usrcode.c` 빌드 여부 |
| `EtherCatStackType` | `1` | EtherCAT 스택 선택자 (ini `[ETHERCAT] EcatType=1`) |
| `OutputType` | `Exe` | MSBuild 출력 타입 |
| `ProjectGuid` | `DC55630F-…` | `.sln`의 GUID와 일치 |

### 구성별 `<PropertyGroup Condition=...>`

```xml
'Debug|AnyCPU'   → <DebugSymbols>true</DebugSymbols>  <OutputPath>Bin\Debug\</OutputPath>
'Release|AnyCPU' → <DebugSymbols>false</DebugSymbols> <OutputPath>Bin\Release\</OutputPath>
```

### 컴파일 태스크

```xml
<UsingTask AssemblyFile="$(CompileTaskPath)\PPMAC460CompileTask.dll"
           TaskName="PPMAC460CompileTask.CompileTask" />
```
`Build`/`Rebuild`는 `<CompileTask Files="@(Compile)" OutputPath="$(OutputPath)"
PPCPUType="$(PPCPUType)" .../>` 를 호출한다. `$(CompileTaskPath)`는 IDE 설치 경로 내에서 해석된다. `Clean`은 `$(OutputPath)`를 삭제하고 재생성한다.

### ItemGroup 분류 (파일 태그 방식)

- `<Compile Include="…">` — **빌드할 C 파일** (이 항목만 빌드됨):
  `C Language\CPLCs\bgcplc01\bgcplc01.c`, `…\CPLCs\bgcplc00\bgcplc.c`,
  `C Language\Background Programs\capp1\capp1.c`. 서브타입 `Code`.
- `<Content Include="…">` — C 컴파일 없이 추적/다운로드되는 스크립트 + 설정 파일
  (`.pmh`, `.pmc`, `.plc`, `.h`, `.msetup`, `.ecatslave`, …). 여기의 `<DisplayOrder>`는
  IDE 트리 순서에만 영향 — **로드 순서(load order)가 아님**.
- `<None Include="…">` — 추적되지만 빌드/특수처리 안 됨 (설정 txt/cfg, 일부 설정 파일,
  None으로 등재된 라이브러리 `.pmc`, `.tunesetup`).
- `<Folder Include="…">` — 빈 폴더(folder)가 트리에 존재하도록 선언.
- EtherCAT 중첩은 `<DependentUpon>` 사용 (슬레이브→마스터, 모듈→슬레이브).

> `.ppproj` ItemGroup은 *어떤* 파일이 존재하는지, C 컴파일 대상인지를 정의한다.
> 스크립트 로드 순서(load order)는 정의하지 않는다 — 그것은 `pp_proj.ini`가 담당한다 (§4).

---

## 4. `pp_proj.ini` 로드 순서(load order) (`Configuration/pp_proj.ini`)

나열된 순서대로 모든 스크립트 파일을 전처리기 / CmdProcessor에 통과시킨다.
섹션 구성:

- `[PMAC_BUFFERS]` — `TableBufSize`, `UserBufSize`, `LookAheadBufSize`,
  `ProgramBufSize`, `SymbolsBufSize` (바이트 단위; `.ppproj` 버퍼 설정과 대응).
- `[CPU_AFFINITY]` — 태스크별 코어 고정 (`servotask=1`, `rtitask=2`,
  `backgroundthread=0`, `ecattask=3`, `phasetask=1`, `gatecapturetask=1`,
  `rticplcthread=2`, `eiptask=0`, `gpascii=0`, `bgcplcthread=0`).
- `[THREAD_PRIORITY]` — `BackgroundThread=10`, `EthercatThread=97`.
- `[PMACMODE]` — `UserMode=0`, `verbose=0`. `[ETHERCAT] EcatType=1`.
- `[CPLCC] LibraryDir=/var/ftp/usrflash/Project/Bin/Debug/` — CPLC `.so` 파일 위치.
- `[PMAC_PROGRAMS]` — **로드 목록(load order)**; 변수 시작 주소 이후 `file1..fileN` 나열:
  - `PVARSTART=8192`, `QVARSTART=1024`, `MVARSTART=8192` (`.ppproj`와 일치해야 함).
  - `last_file_number=N`으로 목록 종료.
- `[LINUX_PROGRAMS]` — capp 실행 파일 및 실행 플래그, `last_program_number`.

### 로드 순서가 중요한 이유

파일은 순서대로 연결/처리되므로, **정의(define)와 전역 선언은 사용되기 전에 파싱되어야 한다**. 샘플의 표준 로드 순서(load order)는 다음과 같다:

```
Global Includes (.pmh)   file1..file10   — ECATMap.pmh, global definitions.pmh,
                                            Home_Param_Set.pmh, MotorSetup.pmh, *_Mapping.pmh,
                                            PMAC_Interface.pmh  (define/전역 선언이 가장 먼저)
Libraries (.pmc)         file11..file13  — HmParaSet.pmc, timer.pmc, MtrHome.pmc
Motion Programs (.pmc)   file14..file20  — prog1.pmc, noclose.pmc, 100_Interpolation…,
                                            200_Multi…, 300_Path…, 400_Trigger…, 500_TDI…
PLC Programs (.plc)      file21..file27  — plc1_Param_Set.plc, Plc2_Homing_Manager.plc,
                                            plc3.plc, plc0.plc, PLC12_HomingManager.plc,
                                            plc31.plc, plc4.plc
```

모든 경로는 컨트롤러의 절대 경로이며, 예:
`file16=/var/ftp/usrflash/Project/PMAC Script Language/Motion Programs/100_Interpolation_move_prog.pmc`
(`PMAC Script Language`의 공백에 주의).

### `[LINUX_PROGRAMS]`

```
program1=/var/ftp/usrflash/Project/C Language/Background Programs/capp1.out
run1=0                     # 0 = 로드 시 자동 실행 안 함; 1 = 자동 실행
last_program_number=1
```

---

## 5. 컨트롤러 내 경로 대응

| PC 프로젝트 | 컨트롤러 |
|-------------|---------|
| `PPMAC_Project/` (루트) | `/var/ftp/usrflash/Project/` |
| `PMAC Script Language/…/X.pmc` | `/var/ftp/usrflash/Project/PMAC Script Language/…/X.pmc` |
| `C Language/Background Programs/capp1.out` | `/var/ftp/usrflash/Project/C Language/Background Programs/capp1.out` |
| `Bin/Debug/` | `/var/ftp/usrflash/Project/Bin/Debug/` |
| `Bin/Release/` | `/var/ftp/usrflash/Project/Bin/Release/` |
| CPLC 라이브러리 (`[CPLCC] LibraryDir`) | `/var/ftp/usrflash/Project/Bin/Debug/` (libplcc*.so) |

### 다운로드(download) 필터

IDE는 다운로드(download) 시 두 개의 필터 파일을 생성한다:

- **`rsync-filter.txt`** (rsync `P`/`-` 규칙): `Configuration/*.*`,
  `Log/*.log`, `C Language/Include/pp_proj.h`, `iec61131`을 보호;
  `Log/*.gpg`, `pp_error_hist.log`, 미사용 `CPLCs/user`, `CPLCs/rti`를 제외.
- **`Exclude.txt`** (경로 목록): 중간 산출물과 소스를 제외 — `*.c`, `*.h`,
  `*.o`, `*.mak`, `*.log`, `Temp`, `Database`, `Bin/*/usralgo.*`, 호스트 로그 파일,
  `Exclude.txt`/`rsync-filter.txt` 자신, 그리고 *소스* 트리의 `Debug/*.out`/`Release/*.out`
  (빌드된 `Bin/<cfg>` 내 `.out`은 전송됨).

`DownloadCSoruce=No`이면 C 소스(`.c`/`.h`)가 제외되고 컴파일된 `.out`/`.so` 바이너리만 배포된다.

---

## 6. 빌드 / 다운로드 핵심 사항

- **툴체인** (`*_debug.mak`, `PMAC_ARCH=armLS1043A` 기준):
  `ARCH=arm`, `CROSS_COMPILE=arm-omron49-linux-gnueabihf-`,
  `CC=arm-omron49-linux-gnueabihf-gcc`, sysroot
  `/opt/armv71-4.1.18-ipipe-quadcore`. 링크: `-lppmac -lpthread_rt -lxenomai -lmath`.
- **출력물**: capp → `../../../Bin/<cfg>/capp1.out`; CPLC → `.o_so` → `Bin/<cfg>/`의 `libplcc*.so`.
  Debug는 `-g3` 사용; Release는 최적화 적용.
- **`<Compile>` C 파일만 빌드된다.** Script `.pmc/.plc/.pmh`는 오브젝트로 컴파일되지 않으며,
  `pp_proj.ini`에 따라 컨트롤러에서 전처리/로드된다.
- **다운로드(download)는 해당 구성의 빌드 성공이 선행되어야 한다** — `Bin/<config>/`의 바이너리가
  존재해야 다운로드(download)로 전송된다. 빌드와 다운로드(download)는 *같은* 구성(Debug vs Release)으로 수행할 것.
- 전송 방식: 프로젝트 로드는 Telnet/SSH (포트 22, 사용자 `root`); 파일 업로드는 FTP (포트 21, 사용자/비밀번호 `ftp`).

---

## 7. "X는 어디에 두나?"

| 작업 | 추가 위치 | 등록 / 주의사항 |
|------|-----------|----------------|
| **새 모션 프로그램** | `PMAC Script Language/Motion Programs/NNN_Name.pmc` (`open prog N`) | `.ppproj`에 `<Content Include>` 추가; `[PMAC_PROGRAMS]`에 `fileK=` 추가 (includes/libraries **이후**); `last_file_number` 증가 |
| **새 PLC** | `PMAC Script Language/PLC Programs/plcN.plc` (`open plc N … close`) | `<Content Include>` 추가; 다른 PLC 근처에 `fileK=` 추가 (PLC는 마지막에 로드); `last_file_number` 증가. 로드 시 자동 실행이 필요하면 `pp_startup.txt`에 활성화 |
| **새 서브프로그램 / 라이브러리** | `PMAC Script Language/Libraries/Name.pmc` (`open subprog Name`) | `.ppproj`에 등록; 이 라이브러리를 호출하는 프로그램 **앞에** `fileK=` 배치 |
| **새 전역 define / 상수** | `PMAC Script Language/Global Includes/Name.pmh` (`#define`, `global`) | 등록; 사용 전에 파싱되도록 `[PMAC_PROGRAMS]`의 **맨 위**에 `fileK=` 배치 |
| **C 백그라운드 앱 (capp)** | `C Language/Background Programs/<app>/<app>.c` (+ `.h`) | `.ppproj`에 `<Compile Include>` 추가; 빌드 → `Bin/<cfg>/<app>.out`; `[LINUX_PROGRAMS]`에 등록 (`programK=…/<app>.out`, `runK=0|1`, `last_program_number` 증가) |
| **CPLC (백그라운드 / 실시간)** | `C Language/CPLCs/bgcplcNN/…c` (백그라운드) 또는 `rticplc/rticplc.c` (실시간) | `<Compile Include>` 추가; `Bin/<cfg>` 아래 `libplcc*.so`로 빌드됨 (`[CPLCC] LibraryDir`) |
| **실시간 사용자 알고리즘** | `C Language/Realtime Routines/usrcode.c/.h` | `.ppproj`의 `RealtimeRoutinesBuildFlag=1`로 설정해야 빌드됨 |
| **공유 C 헤더** | `C Language/Include/*.h` (예: pp_proj.h, ECATMap.h) | `<Content>`; C에서 `#include "../../Include/...h"` 사용; pp_proj.h는 보호/재생성됨 |
| **하드웨어 / 모터 / EtherCAT 변경** | `System/...` 설정 XML | IDE 편집기를 사용 권장; `MotorN.msetup`, `*.ecat*`, `*.hwsetup` 일관성 유지 |

**핵심 규칙:** 스크립트 파일을 추가할 때는 반드시 **두 곳**을 수정해야 한다 — `.ppproj` ItemGroup (IDE 추적용) **그리고** `pp_proj.ini`의 `[PMAC_PROGRAMS]` / `[LINUX_PROGRAMS]` 목록의 **올바른 로드 위치** (includes → libraries → motion programs → PLCs)에 추가하고, `last_file_number` / `last_program_number`를 증가시켜야 한다.

---

## Source

샘플 프로젝트: `C:\Cloude_Code\PowerPMAC_MCP\PPMAC_Project_Sample\` —
`PPMAC_Project.PowerPmacSuite_sln`, `PPMAC_Project\PPMAC_Project.ppproj`,
`PPMAC_Project\Configuration\pp_proj.ini`, 전체 `PPMAC_Project\` 트리.

## 관련 문서
- [[setup-workflow|셋업 워크플로우]] — IDE 브링업·다운로드 절차
- [[c-programming|C 프로그래밍 가이드]] — pp_proj 빌드 레이아웃
- [[NAVIGATION|Navigation Map]] — 매뉴얼 페이지 맵
- [[🗺️ PowerPMAC 지식맵]] — 전체 지식맵(MOC)
