# Power PMAC 개발 도구

OMRON Delta Tau **Power PMAC** 컨트롤러 개발을 Claude Code로 하기 위한 결과물 2종입니다.

| 폴더 | 내용 |
|---|---|
| `Skills/powerpmac-dev/` | **지식 Skill** — Script/PLC/C 문법, 데이터구조, 함정(gotcha), C-API, 프로젝트 구조, 검증된 스니펫. Claude가 Power PMAC 코드를 정확히 작성·리뷰하게 합니다. |
| `mcp-server/` | **MCP 서버** — 프로젝트 빌드, 컨트롤러로 다운로드, 라이브 gpascii·셸 명령 실행. |
| `cli/download-project.cmd` | 독립 실행용 rsync+projpp 다운로드 스크립트(MCP가 내부적으로 같은 일을 함). |

---

## **Skill**을 다른 사용자에게 배포 (쉬움)

정제된 reference가 저장소에 포함돼 있어, **매뉴얼 없이도** Skill이 바로 동작합니다. 설치만 하면 됩니다.

1. 이 저장소를 **클론**합니다.
2. Claude Code가 인식하도록 Skill 폴더를 **배치** — 둘 중 택일:
   - **전역**(모든 프로젝트): `Skills/powerpmac-dev`를 `~/.claude/skills/`에 연결.
     ```powershell
     # Windows (junction은 관리자 권한 불필요):
     New-Item -ItemType Junction -Path "$HOME\.claude\skills\powerpmac-dev" `
       -Target "<repo>\Skills\powerpmac-dev"
     ```
     ```bash
     # macOS/Linux:
     ln -s "<repo>/Skills/powerpmac-dev" ~/.claude/skills/powerpmac-dev
     ```
   - **프로젝트 한정**: 프로젝트의 `Skills/` 폴더 아래에 둡니다.
3. 끝 — Power PMAC 관련 질문을 하면 Skill이 자동으로 트리거됩니다.

**선택(심층 검색용):** 매뉴얼 전체 텍스트·펌웨어 테이블은 저장소에 포함하지 않습니다(라이선스·용량).
매뉴얼을 직접 `grep` 하려면 PDF를 `Power PMAC Manual/`에 넣고
`python Skills/powerpmac-dev/tools/extract_pdfs.py`를 실행하세요(요소 테이블은 `tools/gen_element_index.py`).
일상적인 코드 생성에는 필요 없습니다.

---

## **MCP 서버**를 다른 사용자에게 배포

MCP는 라이선스가 걸린 PDK를 인프로세스로 호스팅하므로, 각 사용자가 **자신의 PDK 설치 기준으로 빌드**합니다.

### 사전 조건 (PC마다)
- **Windows** + **.NET Framework 4.8** (런타임 — Win10/11 기본 포함).
- **Power PMAC IDE 설치** → `C:\DeltaTau\PowerPMAC\Compilers`(ARM 크로스컴파일러 + `rsync`/`ssh`/`sshpass`)와
  머신 환경변수 `DTBUILDPATH`를 제공. **빌드와 다운로드 모두**에 필요.
- **PDK 파일** — ODT DLL·네이티브 DLL·**유효한 라이선스**(`CLLLicFile.lic`)가 든 `PDK_Reference` 폴더.
  라이선스는 설치별이라 각 사용자가 자기 것을 가져야 합니다.
- **.NET SDK** (빌드용, 1회) — `dotnet build`. (Visual Studio도 가능)

### 설치 절차
1. PDK 위치를 빌드에 알려줍니다(기본 경로 `…\Power PMAC Manual\PDK_Reference`면 생략):
   ```powershell
   $env:PdkHome = "C:\path\to\PDK_Reference"
   ```
2. 빌드:
   ```powershell
   cd mcp-server
   dotnet build -c Release          # -> bin\Release\powerpmac-mcp.exe (x86)
   ```
   PDK 매니지드 DLL이 exe 옆으로 **복사**됩니다(네이티브 런타임·라이선스는 제자리 사용).
3. Claude Code에 등록 — 프로젝트 `.mcp.json`(이 저장소에 예시 있음) 또는:
   ```powershell
   claude mcp add powerpmac "C:\...\mcp-server\bin\Release\powerpmac-mcp.exe" `
     -e POWERPMAC_PDK_HOME="C:\path\to\PDK_Reference"
   ```
   선택 환경변수: IDE가 `C:\DeltaTau\PowerPMAC\Compilers`가 아니면 `POWERPMAC_COMPILERS_HOME`.

### 제공 툴
`build_project`, `download_project`, `connect`/`disconnect`/`connection_status`,
`send_command`/`get_response`/`get_responses`, `exec_shell`. 기본값: SSH `root`/`deltatau`, 포트 22.

### 패키징 팁
.NET SDK가 없는 동료를 위해선, 한 번 빌드한 뒤 `bin\Release\`와 **그 사용자의** `PDK_Reference`를
함께 압축하세요 — exe + (각자 라이선스된) DLL + 라이선스는 같이 다녀야 합니다. 가장 간단한 건 역시
"IDE/PDK 설치 후 `dotnet build`"입니다.

---

## 참고 및 한계
- MCP는 Windows 전용·x86입니다(PDK 런타임이 32비트).
- `download_project`는 `rsync`(전송) + `projpp`(로드)를 수행합니다. C는 다시 빌드하지 않으므로 C 코드가
  바뀌었으면 `build_project`를 먼저 실행하세요. 헤드리스 PTY·rsync 세부 사항은 `mcp-server/README.md` 참고.
- 저장소 미포함(로컬에 별도 보유): `Power PMAC Manual/`(PDF + PDK), `PPMAC_Project_Sample/`.
