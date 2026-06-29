# 펌웨어 reference 소스

Power PMAC 데이터구조 + C API의 권위 있는 원본으로, 컨트롤러 펌웨어에서 복사했습니다
(시뮬레이터 `C:\DeltaTau\PowerPMACSim\opt\ppmac`, 펌웨어 2.3.1.82). 실물 컨트롤러는 펌웨어
2.8.3.0이므로, 미묘한 차이는 MCP `get_response`로 확인하세요.

커밋됨(정제 산출물): `ELEMENTS_INDEX.md` (+ `../c-api.md`).
미커밋(원본, 로컬에서 재생성): `headers/`, `pp_swtbl*.txt`, `ppstruct*.txt`.

## 재생성 방법
시뮬레이터/컨트롤러의 `opt/ppmac`에서 복사:
- `usrflash/Database/pp_swtbl0-3.txt` → 여기 (`Structure,Element` 인텔리센스 테이블).
- `ppstruct/ppstruct.txt`, `ppstruct_help.txt` → 여기.
- `libppmac/{gplib,rtpmacapi,rtpmaclib,libppmac,status,cmdprocessor}.h` → `headers/`.
- `rtpmac/{RtGpShm,pRtGpShm,gather,RtPlcThread}.h` → `headers/`.

그 다음 인덱스 재생성: `python tools/gen_element_index.py`.
