# Power PMAC 개발 도구

OMRON Delta Tau **Power PMAC** 컨트롤러 개발을 Claude Code로 하기 위한 결과물 2종입니다.

> 📘 **처음 쓰시나요?** 설치부터 사용까지 한 번에 → **[사용자 매뉴얼: docs/manual.md](docs/manual.md)** (HTML·PDF 동봉: `docs/manual.html`, `docs/manual.pdf`)

| 폴더 | 내용 |
|---|---|
| `plugins/powerpmac-dev/` | **지식 Skill** (Claude Code 플러그인) — Script/PLC/C 문법, 데이터구조, 함정(gotcha), C-API, 프로젝트 구조, 검증된 스니펫. Claude가 Power PMAC 코드를 정확히 작성·리뷰하게 합니다. |
| `mcp-server/` | **MCP 서버** — 프로젝트 빌드, 컨트롤러로 다운로드, 라이브 gpascii·셸 명령 실행. |
| `cli/download-project.cmd` | 독립 실행용 rsync+projpp 다운로드 스크립트(MCP가 내부적으로 같은 일을 함). |

---

## 설치 (다른 PC / 다른 사용자)

### Skill만 — 플러그인 (권장 · 자동 업데이트, git pull 불필요)
```text
/plugin marketplace add minimin333/PowerPMAC_MCP
/plugin install powerpmac-dev@powerpmac
```
`/plugin` → **Marketplaces** 탭에서 `powerpmac`의 auto-update를 켜면 시작 시 **자동 최신화**됩니다.

### Skill + MCP — git clone + setup.ps1 (컨트롤러 제어 포함 / 개발자)
**`git clone` → `setup.ps1` 한 번**이면 Skill과 MCP가 설치됩니다. 경로는 자동 감지하므로 편집 불필요.

```powershell
git clone https://github.com/minimin333/PowerPMAC_MCP.git C:\Tools\PowerPMAC_MCP
cd C:\Tools\PowerPMAC_MCP
powershell -ExecutionPolicy Bypass -File .\setup.ps1
# Skill만 필요하면(PDK 없는 PC):  .\setup.ps1 -SkillOnly
```
`setup.ps1`이 ① Skill을 `~/.claude/skills/`에 junction, ② PDK·컴파일러 자동 감지(레지스트리/`DTBUILDPATH`),
③ MCP 빌드, ④ `claude mcp add --scope user` 등록을 수행합니다. 끝나면 **Claude Code 재시작**.

전제(MCP까지 쓰려면): Power PMAC IDE+PDK 라이선스, .NET SDK(빌드 1회), .NET 4.8 런타임.
**자세한 절차·문제 해결은 [INSTALL.md](INSTALL.md) 참고.**

### 제공 MCP 툴
`build_project`, `download_project`, `connect`/`disconnect`/`connection_status`,
`send_command`/`get_response`/`get_responses`, `exec_shell`. 기본값: SSH `root`/`deltatau`, 포트 22.

---

## 참고 및 한계
- MCP는 Windows 전용·x86입니다(PDK 런타임이 32비트).
- `download_project`는 `rsync`(전송) + `projpp`(로드)를 수행합니다. C는 다시 빌드하지 않으므로 C 코드가
  바뀌었으면 `build_project`를 먼저 실행하세요. 헤드리스 PTY·rsync 세부 사항은 `mcp-server/README.md` 참고.
- 저장소 미포함(로컬에 별도 보유): `Power PMAC Manual/`(PDF + PDK), `PPMAC_Project_Sample/`.
