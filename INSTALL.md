# 설치 가이드 (다른 PC / 다른 사용자)

`git clone` → `setup.ps1` **한 번**이면 Skill과 MCP가 설치됩니다.
경로는 자동 감지하므로 수동 편집이 필요 없습니다.

---

## 전제 조건 (PC마다)
| 항목 | 용도 | 없으면 |
|---|---|---|
| **Windows** + **.NET Framework 4.8** | MCP 실행(Win10/11 기본 포함) | — |
| **Power PMAC IDE + PDK 라이선스** | MCP **빌드·다운로드** (컴파일러·rsync·`CLLLicFile.lic` 제공) | MCP 불가 → `-SkillOnly`로 Skill만 |
| **.NET SDK** (1회) | MCP **빌드** | MCP 빌드 불가 → Skill만 |

> Skill만 쓸 사람(코드 작성·리뷰)은 IDE/PDK·SDK 없이도 됩니다.

---

## 설치 (Skill + MCP)
```powershell
git clone https://github.com/minimin333/PowerPMAC_MCP.git C:\Tools\PowerPMAC_MCP
cd C:\Tools\PowerPMAC_MCP
powershell -ExecutionPolicy Bypass -File .\setup.ps1
```
`setup.ps1`이 자동으로 수행:
1. **Skill 설치** — `~/.claude/skills/powerpmac-dev`를 저장소로 junction(매뉴얼 없이 동작).
2. **PDK·컴파일러 자동 감지** — 환경변수 → `DTBUILDPATH` → 레지스트리(`PowerPMAC Development Kit`)
   → 기본 설치경로(`C:\DeltaTau\PowerPMAC\<버전>\PDK`).
3. **MCP 빌드** — 감지한 PDK로 `mcp-server`를 x86/net48 빌드.
4. **등록** — `claude mcp add powerpmac --scope user`(모든 프로젝트에서 사용).

마지막에 **Claude Code를 재시작**하면 Skill 자동 동작 + MCP(`powerpmac`) 연결.

## 설치 (Skill만 — PDK 없는 PC)
```powershell
git clone https://github.com/minimin333/PowerPMAC_MCP.git C:\Tools\PowerPMAC_MCP
cd C:\Tools\PowerPMAC_MCP
powershell -ExecutionPolicy Bypass -File .\setup.ps1 -SkillOnly
```

---

## 사용 확인
- Claude Code 재시작 후 "Power PMAC의 Motor[] 데이터구조 알려줘" → Skill이 답변.
- `claude mcp list` 에 `powerpmac` 표시 → "192.168.0.200에 프로젝트 빌드/다운로드해줘" 가능.

## 업데이트
```powershell
cd C:\Tools\PowerPMAC_MCP
git pull
powershell -ExecutionPolicy Bypass -File .\setup.ps1   # 재빌드·재등록(멱등)
```

---

## 문제 해결
| 증상 | 조치 |
|---|---|
| **PDK를 찾지 못함** | `setup.ps1 -PdkHome "C:\...\PDK"` (CLLLicFile.lic 있는 폴더 지정). IDE/PDK 설치 확인. |
| **dotnet 없음** | https://dotnet.microsoft.com/download 에서 .NET SDK 설치 후 재실행. |
| **컴파일러 미발견** | Power PMAC IDE 설치(머신 환경변수 `DTBUILDPATH` 제공). C 빌드·다운로드에 필요. |
| **빌드가 파일 잠금 오류** | Claude Code를 종료(실행 중 MCP가 exe를 잠금) 후 `setup.ps1` 재실행. |
| **claude CLI 미발견** | setup.ps1이 출력하는 `claude mcp add ...` 명령을 Claude Code 터미널에서 수동 실행. |
| **다운로드 시 컨트롤러 접속 실패** | IP/비밀번호 확인(기본 `root`/`deltatau`, 포트 22). 같은 네트워크인지 확인. |

## 참고
- MCP는 **각 PC에서 빌드**합니다(PDK DLL이 라이선스라 prebuilt 재배포 불가).
- 경로 하드코딩이 없어 어디에 clone하든 동작합니다. 자세한 내부 동작은 `mcp-server/README.md` 참고.
