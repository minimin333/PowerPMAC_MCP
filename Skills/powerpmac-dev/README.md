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

## 설치
저장소 루트의 **`setup.ps1`**이 이 Skill을 `~/.claude/skills/powerpmac-dev`로 junction 설치한다
(매뉴얼 없이도 동작 — 정제된 `reference/*.md`가 커밋돼 있음). 자세히는 루트
[INSTALL.md](../../INSTALL.md) 참고.
```powershell
# 저장소 루트에서:
powershell -ExecutionPolicy Bypass -File .\setup.ps1 -SkillOnly   # Skill만
```
**선택(심층 grep용):** 매뉴얼 전체 텍스트는 미커밋. PDF를 `../../Power PMAC Manual/`에 두고
`python tools/extract_pdfs.py`(요소 테이블은 `tools/gen_element_index.py`) 실행 시 `reference/raw/`·
`reference/firmware/` 코퍼스가 로컬 생성된다. 일상 코드 생성엔 불필요.

## raw 코퍼스를 커밋하지 않는 이유
매뉴얼 전체 텍스트(3,260p) 재배포는 라이선스상 부담 + repo 비대화. `extract_pdfs.py`로
재현 가능하므로 정제 산출물만 버전관리한다. (정책 변경 시 `.gitignore` 수정.)
