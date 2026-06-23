# powerpmac-dev — Claude Code Skill

OMRON Delta Tau **Power PMAC** 개발용 Skill. Script Motion Program, Script PLC,
C(CPLC/capp) 프로그래밍의 문법·데이터구조·함정을 Claude가 이해하도록 정제한 지식 묶음.

## 구성
```
powerpmac-dev/
  SKILL.md              # 진입점: 멘탈모델 + 라우팅 + 핵심 함정
  reference/
    NAVIGATION.md       # 도메인 → 매뉴얼 페이지 맵
    data-structure.md   script-motion.md  script-plc.md
    c-programming.md    syntax-rules.md    gotchas.md
    raw/                # PDF 추출 원문(검색용, git 제외 — 로컬 생성)
  snippets/             # 검증된 예제 프로그램
  tools/extract_pdfs.py # PDF → raw 코퍼스 재생성 스크립트
```

## 팀원 설치
1. 이 저장소를 클론.
2. Power PMAC 매뉴얼 PDF 4종을 `../Power PMAC Manual/`에 둔다(별도 배포).
3. `python tools/extract_pdfs.py` 로 `reference/raw/` 코퍼스를 로컬 생성(grep 검색용).
4. 스킬을 Claude Code가 인식하도록 배치:
   - **프로젝트 한정**: 저장소 안 `Skills/`에 두고 해당 프로젝트에서 사용.
   - **전역**: `~/.claude/skills/powerpmac-dev` 로 심볼릭 링크 또는 클론.
     ```bash
     ln -s "$(pwd)/powerpmac-dev" ~/.claude/skills/powerpmac-dev   # macOS/Linux
     # Windows(관리자 PowerShell):
     # New-Item -ItemType SymbolicLink -Path "$HOME\.claude\skills\powerpmac-dev" -Target "<repo>\Skills\powerpmac-dev"
     ```

## raw 코퍼스를 커밋하지 않는 이유
매뉴얼 전체 텍스트(3,260p) 재배포는 라이선스상 부담 + repo 비대화. `extract_pdfs.py`로
재현 가능하므로 정제 산출물만 버전관리한다. (정책 변경 시 `.gitignore` 수정.)
