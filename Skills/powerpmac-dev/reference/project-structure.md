# Power PMAC Project Structure (OMRON Delta Tau)

Canonical layout of a Power PMAC IDE / PowerPmacSuite project, grounded in the
sample project `PPMAC_Project`. A solution (`.PowerPmacSuite_sln`, MSBuild-style)
references one `.ppproj` MSBuild manifest; the project tree mirrors what is
deployed to the controller under `/var/ftp/usrflash/Project/`.

Key facts for this sample: CPU = ARM `armLS1043A` (LS1043A, quad-core,
`4.1.18-ipipe`), firmware `2.8.3.0`, cross toolchain
`arm-omron49-linux-gnueabihf-`. Two build configs: `Debug`, `Release`.

---

## 1. Top-level folder layout

```
PPMAC_Project.PowerPmacSuite_sln        Solution file (MSBuild fmt 12.00); references the .ppproj
PPMAC_Project/                          Project root (== /var/ftp/usrflash/Project on controller)
├─ PPMAC_Project.ppproj                 MSBuild project manifest (PropertyGroup + ItemGroup)
├─ PMAC Script Language/                Power PMAC Script source (compiled by preprocessor, load-ordered)
│  ├─ Global Includes/                  .pmh headers: #define, global decls, ECAT/motor maps — loaded FIRST
│  ├─ Libraries/                        .pmc reusable subprogs/subroutines (e.g. timer, homing params)
│  ├─ Motion Programs/                  .pmc `open prog N` motion programs + `open subprog`
│  ├─ PLC Programs/                     .plc `open plc N` background/realtime PLCs
│  ├─ Kinematic Routines/               .pmc forward/inverse kinematics (`open forward/inverse`) (empty here)
│  └─ Macro/                            MACRO-ring station scripts (not present in this sample)
├─ C Language/                          C source cross-compiled to ARM binaries
│  ├─ Background Programs/<app>/        capp user background app → builds <app>.out (Linux executable)
│  ├─ CPLCs/<name>/                     Compiled PLCs: bgcplcNN (background), rticplc (realtime) → .o_so / libplcc*.so
│  ├─ Realtime Routines/                usrcode.c/.h — realtime user servo/phase algorithms
│  ├─ Include/                          Shared headers: pp_proj.h, ECATMap.h, PMAC_Interface_Map.h
│  └─ Libraries/                        Shared C libs linked into CPLCs/capps (empty here)
├─ Configuration/                       Load manifest + startup/save scripts + EtherCAT config
├─ System/                             IDE setup files (XML) for hardware/motors/encoders/CS/EtherCAT
│  ├─ CPU/                              System.cpusetup
│  ├─ Motors/                           MotorN.msetup (one per motor)
│  ├─ Encoder/                          Encoder.encsetup
│  ├─ Coordinate Systems/               Coordinate Systems.cssetup
│  ├─ EtherCAT/                         Master0.ecatmaster, *.ecatslave, *.ecatmodule, EthercatConfig.ecc
│  └─ Hardware/                         CK3W-*.hwsetup (axis interface / I/O units)
├─ Bin/                                Build outputs
│  ├─ Debug/                            capp1.out, libplcc*.so (DebugSymbols=true)
│  └─ Release/                          capp1.out, libplcc1.so (optimized)
├─ Database/                            IDE symbol DBs: pp_*.sym, *.devdesc.xml, *.PmacDatabase (not downloaded)
├─ Documentation/                       note.txt and user docs
├─ Log/                                 pp_error.log, pp_proj.log, ecmaster0.0.log, error0.0.log
├─ Temp/                                Scratch (pp_debug.txt) — not downloaded
└─ Tools/                              Tune.tunesetup and other IDE tool state
```

> Folder names with spaces (`PMAC Script Language`, `C Language`, `Global Includes`,
> `Coordinate Systems`, `Background Programs`, `Realtime Routines`) are canonical —
> preserve them exactly, including in `pp_proj.ini` paths.

---

## 2. File types

| Ext | What it is | Lives in | Editable? |
|-----|-----------|----------|-----------|
| `.PowerPmacSuite_sln` | Solution file (references the .ppproj) | project root | Rarely; IDE-managed |
| `.ppproj` | MSBuild project manifest (PropertyGroup + ItemGroup) | project root | Yes (carefully) — must stay consistent with pp_proj.ini |
| `.pmh` | Script **header**: `#define`, `global`, mapping tables | `Global Includes/` | Yes — hand-written or IDE-generated (mappings) |
| `.pmc` | Script source: motion programs, subprograms, libraries, kinematics | `Motion Programs/`, `Libraries/`, `Kinematic Routines/` | Yes |
| `.plc` | PLC program (`open plc N … close`) | `PLC Programs/` | Yes |
| `.c` / `.h` | C source / header for capps, CPLCs, realtime routines | `C Language/...` | Yes |
| `.o` | Compiled object (intermediate) | next to its `.c` | No (generated) |
| `.out` | Linux ARM executable (background capp) | `Bin/<cfg>/`, copied to `Background Programs/` | No (built) |
| `.o_so` | Position-independent object for a CPLC shared lib | CPLC folder | No (built) |
| `.so` / `libplcc*.so` | Compiled-PLC shared library | `Bin/<cfg>/` | No (built) |
| `.mak` | Per-target makefile (`*_debug.mak`, `*_release.mak`) | capp/CPLC folder | Generated; toolchain/flags live here |
| `.msetup` | Motor setup (XML) | `System/Motors/` | Via IDE; XML editable |
| `.encsetup` | Encoder setup (XML) | `System/Encoder/` | Via IDE |
| `.cssetup` | Coordinate-system setup (XML) | `System/Coordinate Systems/` | Via IDE |
| `.cpusetup` | CPU/system setup (XML) | `System/CPU/` | Via IDE |
| `.hwsetup` | Hardware unit setup (XML), e.g. CK3W-AX1515 | `System/Hardware/` | Via IDE |
| `.ecatmaster` | EtherCAT master definition | `System/EtherCAT/` | Via IDE |
| `.ecatslave` | EtherCAT slave node | `System/EtherCAT/` | Via IDE (DependentUpon master) |
| `.ecatmodule` | Slave sub-module / PDO group | `System/EtherCAT/` | Via IDE (DependentUpon slave) |
| `.ecc` | EtherCAT configuration container | `System/EtherCAT/` | Via IDE |
| `.PmacDatabase` | Amplifier/device database | `System/` | No (IDE data) |
| `.sym` | Symbol database (pp_global/local/prog/subprog) | `Database/` | No (generated) |
| `.ini` | `pp_proj.ini` load manifest; `MotorTopology.ini` | `Configuration/`, `System/` | `pp_proj.ini`: yes — see §4 |
| `.cfg` | Save/config scripts: pp_save.cfg, pp_custom_save.cfg, ECATConfig.cfg, systemsetup.cfg | `Configuration/` | Generated; editable |
| `.txt` | pp_startup.txt, pp_disable.txt, pp_inc_*.txt, rsync-filter.txt, Exclude.txt | `Configuration/`, root | Yes |

---

## 3. The `.ppproj` manifest

XML MSBuild file. `<DefaultTargets="Build">`; targets `Build`/`Rebuild` invoke the
custom `CompileTask` (see UsingTask below).

### Global `<PropertyGroup>` (sample values)

| Setting | Sample | Meaning |
|---------|--------|---------|
| `PPCPUType` | `armLS1043A;4.1.18-ipipe` | Target CPU + kernel; selects toolchain in the `.mak` |
| `CPUType` | `arm,LS1043A` | CPU family/part |
| `ProjectFrimwareVers` | `2.8.3.0` | Target firmware (note the IDE's misspelling "Frimware") |
| `PVarStart` | `8192` | First P-variable the compiler may auto-allocate |
| `QVarStart` | `1024` | First Q-variable for auto-allocation |
| `MVarStart` | `8192` | First M-variable for auto-allocation |
| `UserBuffer` | `200` | User shared-mem buffer (MB-scale; ini = `UserBufSize` 209715200) |
| `ProgramBuffer` | `16` | Program buffer (ini `ProgramBufSize` 16777216) |
| `TableBuffer` | `1` | Table buffer (ini `TableBufSize` 1048576) |
| `LookAheadBuffer` | `16` | Look-ahead buffer (ini `LookAheadBufSize` 16777216) |
| `SymbolsBufSize` | `1` | Symbols buffer (ini 1048576) |
| `CpuAffinityType` | `Unknown` | Affinity scheme selector |
| `CpuAffinityServoTask` | `1` | Core for servo task (mirrors ini `[CPU_AFFINITY]`) |
| `CpuAffinityRtiTask` | `2` | Core for RTI task |
| `CpuAffinityBackgroundTask` | `0` | Core for background thread |
| `CpuAffinityEcatTask` | `3` | Core for EtherCAT task |
| `CpuAffinityPhaseTask` / `…GateCaptureTask` | `1` | Phase / gate-capture cores |
| `CpuAffinityBgCplcTask` / `…EipTask` / `…GpAsciiTask` | `0` | Bg-CPLC / EIP / GPASCII cores |
| `TelnetPort` / `TelnetUser` | `22` / `root` | SSH/telnet transport (port 22 = SSH) for project load |
| `FTPPort` / `FTPUser` / `FTPPassword` | `21` / `ftp` / `ftp` | FTP transport for file upload |
| `DownloadCSoruce` | `No` | (sic) If No, C **source** is excluded from download — only `.out`/`.so` ship |
| `EncryptionOption` | `Do Not Encrypt Any File` | Per-file encryption policy |
| `RealtimeRoutinesBuildFlag` | `0` | Whether to build `Realtime Routines/usrcode.c` |
| `EtherCatStackType` | `1` | EtherCAT stack selector (ini `[ETHERCAT] EcatType=1`) |
| `OutputType` | `Exe` | MSBuild output type |
| `ProjectGuid` | `DC55630F-…` | Matches the GUID in the `.sln` |

### Per-config `<PropertyGroup Condition=...>`

```xml
'Debug|AnyCPU'   → <DebugSymbols>true</DebugSymbols>  <OutputPath>Bin\Debug\</OutputPath>
'Release|AnyCPU' → <DebugSymbols>false</DebugSymbols> <OutputPath>Bin\Release\</OutputPath>
```

### The compile task

```xml
<UsingTask AssemblyFile="$(CompileTaskPath)\PPMAC460CompileTask.dll"
           TaskName="PPMAC460CompileTask.CompileTask" />
```
`Build`/`Rebuild` call `<CompileTask Files="@(Compile)" OutputPath="$(OutputPath)"
PPCPUType="$(PPCPUType)" .../>`. `$(CompileTaskPath)` resolves inside the IDE
install. `Clean` just removes/recreates `$(OutputPath)`.

### ItemGroup classification (how files are tagged)

- `<Compile Include="…">` — **C files to compile** (only these are built):
  `C Language\CPLCs\bgcplc01\bgcplc01.c`, `…\CPLCs\bgcplc00\bgcplc.c`,
  `C Language\Background Programs\capp1\capp1.c`. Subtype `Code`.
- `<Content Include="…">` — script + setup files tracked/downloaded but not C-compiled
  (`.pmh`, `.pmc`, `.plc`, `.h`, `.msetup`, `.ecatslave`, …). `<DisplayOrder>` here is
  IDE tree ordering only — **not** the load order.
- `<None Include="…">` — tracked, not built/special-cased (config txt/cfg, some setup
  files, library `.pmc` listed as None, `.tunesetup`).
- `<Folder Include="…">` — declares an (often empty) folder so it exists in the tree.
- EtherCAT nesting uses `<DependentUpon>` (slave→master, module→slave).

> The `.ppproj` ItemGroup says *which* files exist and whether they C-compile.
> It does **not** define script load order — `pp_proj.ini` does (§4).

---

## 4. `pp_proj.ini` load order (`Configuration/pp_proj.ini`)

Runs every script file through the preprocessor / CmdProcessor **in the listed order**.
Sections:

- `[PMAC_BUFFERS]` — `TableBufSize`, `UserBufSize`, `LookAheadBufSize`,
  `ProgramBufSize`, `SymbolsBufSize` (bytes; mirror the `.ppproj` buffer settings).
- `[CPU_AFFINITY]` — per-task core pinning (`servotask=1`, `rtitask=2`,
  `backgroundthread=0`, `ecattask=3`, `phasetask=1`, `gatecapturetask=1`,
  `rticplcthread=2`, `eiptask=0`, `gpascii=0`, `bgcplcthread=0`).
- `[THREAD_PRIORITY]` — `BackgroundThread=10`, `EthercatThread=97`.
- `[PMACMODE]` — `UserMode=0`, `verbose=0`. `[ETHERCAT] EcatType=1`.
- `[CPLCC] LibraryDir=/var/ftp/usrflash/Project/Bin/Debug/` — where CPLC `.so` live.
- `[PMAC_PROGRAMS]` — **the load list**, with var-start addresses then `file1..fileN`:
  - `PVARSTART=8192`, `QVARSTART=1024`, `MVARSTART=8192` (must match `.ppproj`).
  - `last_file_number=N` terminates the list.
- `[LINUX_PROGRAMS]` — capp executables with a run flag and `last_program_number`.

### Why order matters

Files are concatenated/processed sequentially, so **defines and globals must be
parsed before they are used**. The sample's canonical order is:

```
Global Includes (.pmh)   file1..file10   — ECATMap.pmh, global definitions.pmh,
                                            Home_Param_Set.pmh, MotorSetup.pmh, *_Mapping.pmh,
                                            PMAC_Interface.pmh  (defines/globals first)
Libraries (.pmc)         file11..file13  — HmParaSet.pmc, timer.pmc, MtrHome.pmc
Motion Programs (.pmc)   file14..file20  — prog1.pmc, noclose.pmc, 100_Interpolation…,
                                            200_Multi…, 300_Path…, 400_Trigger…, 500_TDI…
PLC Programs (.plc)      file21..file27  — plc1_Param_Set.plc, Plc2_Homing_Manager.plc,
                                            plc3.plc, plc0.plc, PLC12_HomingManager.plc,
                                            plc31.plc, plc4.plc
```

All paths are absolute controller paths, e.g.
`file16=/var/ftp/usrflash/Project/PMAC Script Language/Motion Programs/100_Interpolation_move_prog.pmc`
(note the literal space in `PMAC Script Language`).

### `[LINUX_PROGRAMS]`

```
program1=/var/ftp/usrflash/Project/C Language/Background Programs/capp1.out
run1=0                     # 0 = present but not auto-run on load; 1 = auto-launch
last_program_number=1
```

---

## 5. On-controller mapping

| PC project | Controller |
|------------|-----------|
| `PPMAC_Project/` (root) | `/var/ftp/usrflash/Project/` |
| `PMAC Script Language/…/X.pmc` | `/var/ftp/usrflash/Project/PMAC Script Language/…/X.pmc` |
| `C Language/Background Programs/capp1.out` | `/var/ftp/usrflash/Project/C Language/Background Programs/capp1.out` |
| `Bin/Debug/` | `/var/ftp/usrflash/Project/Bin/Debug/` |
| `Bin/Release/` | `/var/ftp/usrflash/Project/Bin/Release/` |
| CPLC libs (`[CPLCC] LibraryDir`) | `/var/ftp/usrflash/Project/Bin/Debug/` (libplcc*.so) |

### Download filters

The IDE writes two filter files at download time:

- **`rsync-filter.txt`** (rsync `P`/`-` rules): protects `Configuration/*.*`,
  `Log/*.log`, `C Language/Include/pp_proj.h`, `iec61131`; excludes `Log/*.gpg`,
  `pp_error_hist.log`, and unused `CPLCs/user`, `CPLCs/rti`.
- **`Exclude.txt`** (path list): excludes intermediates and source — `*.c`, `*.h`,
  `*.o`, `*.mak`, `*.log`, `Temp`, `Database`, `Bin/*/usralgo.*`, host log files,
  `Exclude.txt`/`rsync-filter.txt` themselves, and `Debug/*.out`/`Release/*.out`
  from the *source* trees (the built `.out` in `Bin/<cfg>` still ships).

When `DownloadCSoruce=No`, C source (`.c`/`.h`) is excluded — only the compiled
`.out`/`.so` binaries are deployed.

---

## 6. Build / download essentials

- **Toolchain** (from `*_debug.mak`, `PMAC_ARCH=armLS1043A`):
  `ARCH=arm`, `CROSS_COMPILE=arm-omron49-linux-gnueabihf-`,
  `CC=arm-omron49-linux-gnueabihf-gcc`, sysroot
  `/opt/armv71-4.1.18-ipipe-quadcore`. Links `-lppmac -lpthread_rt -lxenomai -lmath`.
- **Outputs**: capp → `../../../Bin/<cfg>/capp1.out`; CPLC → `.o_so` → `libplcc*.so`
  in `Bin/<cfg>/`. Debug uses `-g3`; Release uses optimization.
- **Only `<Compile>` C files build.** Script `.pmc/.plc/.pmh` are not compiled to
  objects; they are preprocessed/loaded on the controller per `pp_proj.ini`.
- **Download requires a prior successful build of that config** — the binaries
  in `Bin/<config>/` must exist before download ships them. Build then download the
  *same* config (Debug vs Release).
- Transports: project load over Telnet/SSH (port 22, user `root`); file upload over
  FTP (port 21, user/pass `ftp`).

---

## 7. "Where do I put X?"

| Task | Add file under | Then register / note |
|------|----------------|----------------------|
| **New motion program** | `PMAC Script Language/Motion Programs/NNN_Name.pmc` (`open prog N`) | Add `<Content Include>` in `.ppproj`; add `fileK=` in `[PMAC_PROGRAMS]` **after** includes/libraries; bump `last_file_number` |
| **New PLC** | `PMAC Script Language/PLC Programs/plcN.plc` (`open plc N … close`) | Add `<Content Include>`; add `fileK=` near the other PLCs (PLCs load last); bump `last_file_number`. Enable in `pp_startup.txt` if it must run on load |
| **New subprogram / library** | `PMAC Script Language/Libraries/Name.pmc` (`open subprog Name`) | Register in `.ppproj`; place its `fileK=` **before** any program that calls it |
| **New global define / constant** | `PMAC Script Language/Global Includes/Name.pmh` (`#define`, `global`) | Register; place its `fileK=` **at the top** of `[PMAC_PROGRAMS]` so it parses before use |
| **C background app (capp)** | `C Language/Background Programs/<app>/<app>.c` (+ `.h`) | Add `<Compile Include>` in `.ppproj`; build → `Bin/<cfg>/<app>.out`; register in `[LINUX_PROGRAMS]` (`programK=…/<app>.out`, `runK=0|1`, bump `last_program_number`) |
| **CPLC (background / realtime)** | `C Language/CPLCs/bgcplcNN/…c` (background) or `rticplc/rticplc.c` (realtime) | Add `<Compile Include>`; builds into `libplcc*.so` under `Bin/<cfg>` (`[CPLCC] LibraryDir`) |
| **Realtime user algorithm** | `C Language/Realtime Routines/usrcode.c/.h` | Set `RealtimeRoutinesBuildFlag=1` in `.ppproj` to build it |
| **Shared C header** | `C Language/Include/*.h` (e.g. pp_proj.h, ECATMap.h) | `<Content>`; `#include "../../Include/...h"` from C; pp_proj.h is protected/regenerated |
| **Hardware / motor / EtherCAT change** | `System/...` setup XML | Prefer the IDE editors; keep `MotorN.msetup`, `*.ecat*`, `*.hwsetup` consistent |

**Golden rule:** adding a script file means editing **two** places — the `.ppproj`
ItemGroup (so the IDE tracks it) **and** `pp_proj.ini`'s `[PMAC_PROGRAMS]` /
`[LINUX_PROGRAMS]` list **in the correct load position** (includes → libraries →
motion programs → PLCs), then bumping `last_file_number` / `last_program_number`.

---

## Source

Sample project: `C:\Cloude_Code\PowerPMAC_MCP\PPMAC_Project_Sample\` —
`PPMAC_Project.PowerPmacSuite_sln`, `PPMAC_Project\PPMAC_Project.ppproj`,
`PPMAC_Project\Configuration\pp_proj.ini`, and the full `PPMAC_Project\` tree.
