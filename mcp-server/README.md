# powerpmac-mcp

PC에서 OMRON Delta Tau **Power PMAC** 컨트롤러를 다루는 MCP 서버:
프로젝트 빌드, 컨트롤러로 다운로드, 라이브 gpascii·셸 명령 실행.

**.NET Framework 4.8 · x86** stdio 서버로, PDK DLL을 인프로세스로 호스팅합니다.

## 왜 net48 + x86인가
PDK 런타임이 32비트(`cygwin1.dll`, `ODT.*`, `PPMAC460CompileTask`, `Renci.SshNet`)이고
매니지드 DLL이 .NET Framework + WinForms 대상이라, 호스트 프로세스가 일치해야 네이티브 로드가 됩니다.

## 툴
| 툴 | 용도 |
|---|---|
| `build_project` | `.ppproj`를 로컬 빌드(PDK), 에러/경고 반환. |
| `download_project` | 빌드된 프로젝트를 rsync로 전송 + `projpp`로 컨트롤러에 로드. |
| `connect` / `disconnect` / `connection_status` | 지속 gpascii + 리눅스 셸 세션. |
| `send_command` / `get_response` / `get_responses` | gpascii 명령 / 조회 / 배치 조회. |
| `exec_shell` | 컨트롤러에서 리눅스 셸 명령 실행. |

기본값: SSH 포트 22, 사용자 `root`, 비밀번호 `deltatau`.

## 다운로드 동작 원리 (PDK의 RSYNC 호출을 쓰지 않는 이유)
PDK의 매니지드 `Download.DownloadAllProgramsRSYNC`는 cygwin `rsync`/`ssh`를 띄우는데, 이들은
대화형 **PTY**가 필요합니다. 헤드리스 MCP(stdio) 프로세스엔 PTY가 없어 **영원히 멈춥니다(행)**.
그래서 `download_project`는:
1. `sshpass + rsync`(전송) 후 `projpp`(로드)를 실행하는 작은 배치를 생성합니다.
2. 이를 **`UseShellExecute=true`**로 띄웁니다 — 헤드리스 MCP인데도 Windows가 콘솔 앱에 **새 콘솔**을
   할당하고, 그 콘솔이 cygwin `sshpass`/`ssh`에 PTY를 줍니다(일반 `Process.Start`는 못 줘서 행).
3. rsync 소스는 **상대경로**입니다(프로젝트로 `cd` 후 `./` 동기화). Windows `C:\…` 소스는 rsync가
   드라이브 콜론 `:`을 "원격 호스트"로 오해해 "source and destination both remote" 에러가 납니다.
   이 두 가지가 핵심입니다.

`build_project`로 먼저 빌드해 `Bin/<config>/*.out`이 있어야 합니다. `download_project`는 현재 파일을
전송하고 `projpp`를 실행하며, `projpp`가 Script를 컴파일해 PMAC 버퍼로 로드합니다.
같은 로직의 독립 스크립트도 있습니다: `../cli/download-project.cmd`.

## 경로 자동 감지 (이식성)
PDK와 컴파일러 위치를 **자동 감지**하므로 환경변수 설정이 없어도 동작한다(`PdkRuntime.cs`):
- **PDK**(ODT DLL + 네이티브 DLL + 라이선스 `CLLLicFile.lic`): `POWERPMAC_PDK_HOME` →
  레지스트리 `PowerPMAC Development Kit` → `…\PowerPMAC\<버전>\PDK`(IDE 동봉) 순. `CLLLicFile.lic`
  존재로 검증. 매니지드 DLL은 csproj가 출력 폴더로 복사하고, 네이티브 런타임은 제자리에서 사용
  (`SetDllDirectory`는 C 빌드를 깨뜨리므로 `connect` 동안만 일시 적용).
- **컴파일러**(ARM 크로스컴파일러 + `rsync`/`ssh`/`sshpass`): `POWERPMAC_COMPILERS_HOME` →
  머신 환경변수 `DTBUILDPATH` → 레지스트리 → `C:\DeltaTau\PowerPMAC\Compilers`.

## 빌드 및 등록
보통은 저장소 루트의 **`setup.ps1`**이 감지·빌드·등록을 모두 해준다([INSTALL.md](../INSTALL.md) 참고).
수동 빌드:
```
cd mcp-server
dotnet build -c Release          # PdkHome 자동 감지. -> bin\Release\powerpmac-mcp.exe (x86)
# 자동 감지가 안 되면:  dotnet build -c Release /p:PdkHome="C:\...\PDK"
```
등록은 `claude mcp add powerpmac "<exe>" --scope user`(setup.ps1이 수행).

## 검증됨 (실물 192.168.0.200, 펌웨어 2.8.3.0)
- `build_project`(ARM C 크로스컴파일 포함), `connect`, `get_response`/`get_responses`, `exec_shell`.
- **`download_project`**: 전체 프로젝트를 헤드리스로 ~8초에 전송·로드. 이후 `list plc 0` /
  `list prog 2`가 방금 다운로드한 코드를 그대로 반환.

## 메모
- gpascii 세션은 한 번에 하나; `connect`가 기존 세션을 대체합니다.
- `save`/`reset`(`PowerPMACSave`/`PowerPMACReset`)은 쉬운 후속 확장이며 아직 미노출입니다.
