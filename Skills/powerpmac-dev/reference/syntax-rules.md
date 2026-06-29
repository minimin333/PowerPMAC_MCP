---
title: 구문 규칙 (P/Q/M/L/I, 연산자, 흐름 제어)
aliases: [구문 규칙, Syntax Rules]
tags: [powerpmac/syntax, type/reference]
domain: syntax
status: stable
updated: 2026-06-29
---

# Power PMAC Script 언어 — 구문 규칙

Software Reference "COMMAND SYNTAX SUMMARY" (SWREF p57–76) 및 User's Manual "Computational Features"
(UM p547–566)에서 추출·정리. 토큰별 상세 내용은 하단 **더 깊은 내용**의 grep 포인터를 참조.

## 일반 규칙 (SWREF p57)
- **대소문자 구분 없음(not case sensitive)** (`Motor[0].JogSpeed` == `motor[0].jogspeed`).
- **공백은 의미 없음**, 명시적으로 표기된 경우를 제외.
- 숫자 상수(numeric constants): 십진수 또는 지수 표기 (`3.456E5`, `-7.26e-7`); 16진수는 반드시 `$` 접두사를 붙이며,
  부호 없는 정수만 허용 (`$ff00` = 65280) (UM p552).
- 중간 연산은 **64비트 IEEE-754 double**로 처리되며, 피연산자 저장 형식에 무관 (UM p549).
  특수값: `inf`, `-inf`, `nan`; `nan`은 어떤 것과도 같지 않으므로 (`isnan()` 사용).
- 배열의 index(인덱스) 값은 **대괄호** `[ ]`를 사용하며, 음이 아닌 정수 상수 또는 단일 지역변수(local variable)만 허용
  (식 불가). 변수 번호 선택은 **괄호** `( )` 사용 —
  예: `P(P1)`, `Q(L1)` — 이는 진짜 배열이 아닌 배열 *함수(array function)* (UM p555–556).

## 연산자 (SWREF p58–59, 전체 규격 SWREF p1493–1511)

### 산술(Arithmetic)
| 토큰 | 의미 |
|---|---|
| `+` `-` `*` `/` | 더하기, 빼기/부정, 곱하기, 나누기 |
| `%` | 나머지(modulo) |
| `<<` `>>` | 비트 왼쪽/오른쪽 시프트(bit shift left / right) |

### 비트/논리 (bit-by-bit)
| 토큰 | 의미 |
|---|---|
| `&` | 비트 AND(bitwise AND) |
| `\|` | 비트 OR(bitwise OR) |
| `^` | 비트 XOR(bitwise XOR) |
| `~` | 비트 반전(bitwise invert, 단항) |

### 표준 대입 (평가 시점에 즉시 대입) (SWREF p1499)
`=` `+=` `-=` `*=` `/=` `%=` `&=` `\|=` `^=` `>>=` `<<=` `++` `--`

### 동기 대입(synchronous assignment) — 다음 프로그램된 이동의 실행 시작까지 대입이 **지연됨** (SWREF p1503).
모션과 데이터 변경을 조율할 때 매우 중요한 차이.
`==` `+==` `-==` `*==` `/==` `&==` `\|==` `^==` `++=` `--=`

```
Q10 = 5      // 표준: Q10이 지금 즉시 5가 됨
Q10 == 5     // 동기: 다음 이동이 실행되기 시작할 때 Q10이 5가 됨
```
주의: `==`는 명령문에서는 동기 대입 연산자이면서, 조건식 안에서는 동등 비교 연산자이기도 함. 문맥으로 구분.

### 조건 비교 연산자 (`if`/`while`/`switch` 조건 안에서 사용) (SWREF p1507)
| 토큰 | 의미 | 대체 토큰 |
|---|---|---|
| `==` | 같음(equal) | |
| `!=` | 같지 않음(not equal) | `<>` |
| `<` `>` | 작음 / 큼(less / greater) | |
| `<=` `>=` | 작거나 같음 / 크거나 같음(less-or-equal / greater-or-equal) | `!>` (≤), `!<` (≥) |
| `~` | 근사적으로 같음(approximately equal) | |
| `!~` | 근사적으로 같지 않음(approximately not equal) | |

### 조건 결합 연산자 (SWREF p1511)
`&&` 논리 AND · `\|\|` 논리 OR · `!` 논리 NOT

## 함수 (이름 + 용도; 전체 목록: grep SWREF p59–61)
함수는 **괄호** 안에 인자를 받고 숫자값을 반환. 온라인 명령과 버퍼 명령 모두에서 사용 가능.

- **스칼라 수학(Scalar math)** (SWREF p59–60): `abs acos acosd acosh asin asind asinh atan atand atan2 atan2d
  atanh cbrt ceil cos cosd cosh exp exp2 floor int isnan ln log log10 log2 madd pow qnrt qrrt
  randx rem rint rnd seed sin sincos sincosd sind sinh sgn sqrt tan tand tanh`. `*d` 변형은 도(degree) 단위;
  접미사 없는 것은 라디안(radian) 사용.
- **벡터(Vector)** (SWREF p60): `sum sumprod vcopy vmadd vscale` — 번호가 매겨진 변수 범위에 연산
  (예: `sum(&Array(0),512,1)`).
- **행렬(Matrix)** (SWREF p60): `mdet minv mmadd mminor mmul msolve mtrans`.
- **변환 행렬(Transformation matrix)** (SWREF p61): `tinit` (단위행렬 설정), `tprop` (곱셈+덧셈으로 전파).
- **바이트 버퍼(Byte buffer)** (SWREF p61): `memcpy memset`.
- **변수 집합 복사(Variable-set copy)** (SWREF p61): `dcopy lrcopy rlcopy`.
- **문자열(String)** (SWREF p61): `sprintf strcpy strncpy strtolower strtoupper strcat strncat strchr
  strrchr strcmp strncmp strspn strcspn strlen strpbrk strstr strtod`.
- **EtherCAT** (SWREF p61): `ecatsdo ecattypedsdo ecatcompletesdo ecatregreadwrite
  ecatslavestate ecatsetslavestatemachine`.

## 변수 타입 (UM p554–566)
고정 풀(pool), 동적 할당 없음. 범용 사용자 변수는 모두 64비트 double.

| 타입 | 선언 키워드 | 범위 / 생존기간 | 개수 | 비고 |
|---|---|---|---|---|
| `P` 시스템 전역(system global) | `global` | 모든 태스크, 컨트롤러 전체 | 65536 (P0–P65535) | `Sys.P[i]` / C `pshm->P[i]`로도 접근 |
| `Q` 좌표계(C.S.) 전역 | `csglobal` | 좌표계별; 각 C.S.마다 독립된 집합 | 8192/C.S. | `Coord[x].Q[i]`로도 접근; PLC에서는 `Ldata.Coord`로 선택 |
| `M` 포인터(pointer) | `ptr` | 전역; 주소/요소에 매핑 | 16384 (M0–M16383) | 아래 포인터 정의 참조; C에서 직접 사용 불가 |
| `I` 레거시 셋업(legacy setup) | `#define` | 전역, 저장됨 | — | Turbo-PMAC 호환 별칭, 저장된 요소에 연결 (UM p559) |
| `L` 지역(local) | `local` | 최상위 프로그램 / 통신 스레드별 | 8192 | 스택 기반; 서브루틴에서의 `Ln`은 호출자의 `Rn` |
| `R` 반환/스택(return/stack) | (자동) | 지역; 서브루틴 인자 전달 | — | `Ri`(호출자) = `Li`(피호출자) = `L(StackOffset+i)` |
| `C` C.S. 축(C.S. axis) | (자동) | 키네마틱 서브루틴에서만 | C0–C63 | L-변수 재명명: C0=L(MAX_MOTORS); C0–C31 위치, C32–C63 속도 |
| `D` 비스택 지역(non-stack local) | `#define` | C.S./PLC/스레드별; 스택 위에 있지 않음 | D0–D52+ | `read` 명령의 G-code 인자 & 축 조회 결과 보관 |
| `Sys.Xdata` | (버퍼) | 전역 사용자 배열 버퍼 | 사용자 정의 크기 | 여러 형식 지원 |

### 직접 접근 vs 이름 접근
- 직접(direct): 문자 + 정수 상수 (`P100`, `L55`) 또는 문자 + `(식)` (`Q(L1)`, `R(P5+P6-2)`).
- 이름(named): IDE를 통해 선언 (`global CycleCount;`, `csglobal LineSpeed;`, `local Temp;`,
  `ptr LaserOn->...`). IDE는 `xVARSTART` 오프셋부터 번호를 할당
  (기본값 `PVARSTART=8192 QVARSTART=1024 MVARSTART=8192`). 그 아래 번호는 직접/수동 사용에 안전 (UM p556–557).
- 수동 정의(Turbo-스타일): `#define TargetPressure P100` / `#define SolenoidOn M233`.

### 포인터(M) 변수 정의 (UM p560, SWREF M{data}-> p1254+)
```
ptr LaserOn  -> u.io.$A00000.8.1          // I/O 주소.시작비트.비트수
ptr LaserMag -> Gate1[4].Chan[3].Dac[1]   // 데이터구조 요소에 매핑
```
온라인 형식: `M{data}->{주소 정의}`, `M{data}->{데이터구조 요소}`,
`M{data}->` 는 현재 정의를 보고.

## 상수(Constants)
변경 불가능한 숫자값. 십진수(부호 있음, 소수, 지수) 또는 16진수(`$` 접두사, 부호 없는 정수).
`.001` (앞자리 0 없음) 및 앞자리 0 (`03`)도 허용 (UM p552–553).

## 프로그램 구조 & 줄 규칙
- **주석(comments)**: `//`는 줄 끝까지; `/* ... */` 블록 주석 (소스 예시에서 확인,
  예: `if (isnan(Var)) abort1; // ...`).
- **명령 구분**: 명령은 `;`로 끝낼 수 있음. **별도 프로그램 줄**의 이동은 순차 실행;
  **같은 줄**에 여러 축이 있으면 조율(coordinated)/동시(simultaneous) 이동 (UM p665).
- **줄 레이블(line labels)**: `N{상수}:`는 숫자 점프 레이블 목적지 (SWREF p72).
- 프로그램 텍스트는 `open ...`과 `close` 사이에 입력 (아래 온라인 vs 버퍼 명령 참조).

## 흐름 제어(flow control) (버퍼 Script; SWREF p72–73)
```
if ({조건}) {명령}                          // 단일 행
if ({조건}) { {명령} ... }                  // 다중 행 블록
else {명령}   |  else { ... }              // 앞선 IF가 거짓일 때

while ({조건}) {명령}
while ({조건}) { ... }

do { ... } while ({조건})                  // 최소 한 번 실행

switch ({식}) {
  case {상수}: {명령}... [break]
  case {상수}: ...
  default: ...
}
```
점프 & 호출 (SWREF p72):
- `goto{data}` — 숫자 레이블로 점프, 반환 없음.
- `gosub{data}` — 레이블로 점프 후 반환; 지역변수 전달 불가.
- `callsub{data}` — 레이블로 점프 후 반환; 지역변수 전달 가능.
- `call{data}` — 서브프로그램 [과 레이블]을 호출 후 반환; 지역변수 전달 가능.
- `return` — 호출자로 반환.
- `read({문자}[,{문자}...])` — G-code 스타일 인자를 D-변수로 읽음.
- 조건 플래그 흐름: `cset cclr cexec cskip ccall cdef cundef` (SWREF p73).
- G-code 디스패치: `G{data} M{data} T{data} D{data}`는 `Coord[x].Gprog`/`Mprog`/
  `Tprog`/`Dprog` 안의 서브루틴을 호출 (SWREF p72).

## 온라인 vs 버퍼 명령 (매우 중요한 개념) (SWREF p62, p70)
- **온라인 명령(on-line commands)**은 **즉시 실행되고 버려짐** (이후 조회 불가). 주소 지정에서
  스레드별로 동작. 세 가지 그룹:
  - **전역(global)** (모터/C.S. 무관): `$$$` `$$$***` `reboot` `save` `fsave` `fload`
    `backup` `cpu` `vers` `size` `free` `enable plc{목록}` `disable plc`, 변수 get/set
    (`P10`, `P10=5`, `M20->...`), `{요소}` / `{요소}={식}`, 버퍼 관리
    (`open/close/clear/list prog|plc|subprog`).
  - **좌표계(coordinate-system)** (주소 지정된/`&n` 또는 나열된 C.S.에 작용): `%{상수}` (time base),
    `r q s b stop start a h / \ < >`, `enable disable ddisable adisable`, `Q{data}[=...]`,
    `pmatch`, 키네마틱/로터리/룩어헤드(lookahead) 버퍼 관리, 조회 `&{목록}p|d|v|f|t|g|?`.
  - **모터(motor)** (주소 지정된/`#n` 또는 나열된 모터에 작용): 조회 `p d v f`, `#{목록}?`, jog
    `j+ j- j/ j= j={c} j:{c} j^{c}` (긴 형식: `jog...`), `$ hm hmz k dkill out{c}`.
- **주소 지정(addressing)**: `#{상수}`는 모달 모터를 설정; `&{상수}`는 모달 C.S.를 설정 (스레드별).
  비모달 일회성: `#{목록}`, `#*` (모든 모터), `&{목록}`, `&*` (모든 C.S.).
- **버퍼 Script 프로그램 명령(buffered Script program commands)**은 프로그램/PLC/서브프로그램 버퍼에
  저장되어 나중에 실행. `open prog N` / `open plc N` / `open subprog N` ... `close`로 입력.
  이동, 이동 모드, 축 속성, 이동 속성, 변수 대입, 흐름 제어, PLC 제어, 포트 통신(`send`),
  직접 모터/C.S. 명령 등이 포함 (SWREF p70–75).
- **PLC 프로그램에서 유일한 이동 타입은 rapid**: 어떤 이동 명령이든 자동으로 rapid 모드를 설정하며,
  직전에 다른 모드를 선언했어도 마찬가지 (UM p666).

### 버퍼 이동/모드 빠른 참조 (SWREF p70–71)
- 이동(move): `{축}{data}...` (LINEAR/RAPID/SPLINE) · `{축}{data}:{data}` (PVT) ·
  `{축}{data}^{data}` (트리거까지 이동, RAPID) · CIRCLE 호(arc) `X.. Y.. I.. J..` 또는 `... R{data}`.
  영(zero) 거리: `dwell{data}` (고정 time base), `delay{data}` (가변 time base).
- 모드(modes): `linear rapid circle1..4 pvt{data} spline{data}`.
- 축 속성(axis attrs): `abs inc frax frax2 normal pset pstore pload pclr pmatch`.
- 이동 속성(move attrs): `F{data} tm{data} ta{data} td{data} ts{data}` (및 3D 커터 보정 `nxyz txyz`).

## 더 깊은 내용 (raw 청크)
- 연산자/함수 전체 규격: `reference/raw/software-ref/p0041-0060.txt` (p49–61 TOC + 요약),
  상세 페이지 SWREF p1493–1580 → `p1481-1500.txt` 이후.
- 온라인 vs 버퍼 명령 분류: `reference/raw/software-ref/p0061-0080.txt` (p62–76).
- 변수 타입, 범위, 명명, 포인터, R/C/D 변수: `reference/raw/user-manual/p0541-0560.txt`
  및 `p0561-0580.txt` (UM p554–566).
- 이동 명령/모드 의미: `reference/raw/user-manual/p0661-0680.txt` (UM p665–668).
- 명령의 전체 페이지를 찾으려면: `reference/raw/software-ref`에서 토큰을 grep (예: `^while(`,
  `synchronous`, `callsub`).

## 관련 문서
- [[data-structure|데이터 구조]] — 변수가 가리키는 요소 모델
- [[script-motion|Script 모션 프로그램]] — 모션 프로그램에서의 구문
- [[script-plc|Script PLC 프로그램]] — PLC에서의 구문
- [[gotchas|Gotchas]] — 변수·단위·index 함정
- [[🗺️ PowerPMAC 지식맵]] — 전체 지식맵(MOC)
