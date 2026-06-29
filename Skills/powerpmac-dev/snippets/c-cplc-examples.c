// Power PMAC C 언어 루틴 스켈레톤 모음
// Power PMAC User's Manual(UM)에서 그대로 수집한 코드.
// 공유 메모리에 접근하는 모든 파일은 #include <RtGpShm.h>로 시작해야 한다.
// 각 루틴마다 "usrcode.h"에 대응하는 프로토타입과 EXPORT_SYMBOL이 있어야 한다
// (각 예제의 주석 참고). 사용자 코드를 무한 루프 안에 삽입하면 안 된다 --
// 스케줄러가 이 루틴들을 반복적으로 호출한다.

#include <RtGpShm.h>
#include <stdio.h>
#include <dlfcn.h>

// === 실시간 인터럽트 C PLC 스켈레톤 (source: UM p858-859) ===
// 루틴 이름은 반드시 "realtimeinterrupt_plcc"이어야 하고 void()로 선언한다.
// CPLCs -> rticplc -> "rtiplcc.c"에 위치한다.
// 런타임 활성화: UserAlgo.RtiCplc = 1  (비활성화는 0).
// 이 예제는 이산 출력 뱅크에 교대로 on/off 패턴을 기록한다.
#define IoCard0Out0_7 *(piom + 0xA0000C/4)
#define IoCard0Out8_15 *(piom + 0xA00010/4)
#define IoCard0Out16_23 *(piom + 0xA00014/4)
#define OutputData(x) (x << 8)

void realtimeinterrupt_plcc()   // 실시간 인터럽트 C PLC 함수
{
  static int i = 0;
  if (i++>1000) {    // 사이클 시작 후 1초 초과
 IoCard0Out0_7=OutputData(0xAA); // 홀수 번호 출력 켜기
 IoCard0Out8_15=OutputData(0xAA);
 IoCard0Out16_23=OutputData(0xAA);
 if (i>2000) i=0;   // 사이클 시작으로 리셋
  }
  else {     // 사이클 시작 후 1초 미만
 IoCard0Out0_7=OutputData(0x55); // 짝수 번호 출력 켜기
 IoCard0Out8_15=OutputData(0x55);
 IoCard0Out16_23=OutputData(0x55);
  }
}


// === 캡처 인터럽트 서비스 루틴(ISR) (source: UM p850-851) ===
// 이 타입의 루틴은 오직 하나만 존재할 수 있다. 이름은 반드시 "CaptCompISR"이어야
// 하며 "void CaptCompISR (void)"로 정확히 선언한다. Realtime Routines
// -> "usrcode.c"에 위치한다. 캡처/비교 ISR 내부에서는 부동소수점(P/Q 변수)
// 사용 불가 -- 사용자 공유 메모리 버퍼의 정수 배열을 사용할 것.
//
// Script 설정 명령(1회 실행):
//   Gate3[0].IntCtrl = $10000   // PosCapt[0] 언마스크 (저장 안됨)
//   Sys.Idata[65535] = 0        // 트리거 카운터 초기화
//   UserAlgo.CaptCompIntr = 1   // 캡처/비교 ISR 활성화
//
// 채널 0에서 캡처된 각 위치를 사용자 공유 메모리 버퍼에 기록한다.
void CaptCompISR (void)
{
  volatile GateArray3 *MyFirstGate3IC;  // ASIC 구조체 포인터
  int *CaptCounter;     // 트리거 횟수 기록
  int *CaptPosStore;     // 저장 포인터

  MyFirstGate3IC = GetGate3MemPtr(0);   // IC 베이스 포인터
  MyFirstGate3IC->IntCtrl = 1;   // 인터럽트 소스 클리어
  CaptCounter = (int *)pushm + 65535;   // Sys.Idata[65535]
  CaptPosStore = (int *)pushm + *CaptCounter + 65536;
  *CaptPosStore = MyFirstGate3IC->Chan[0].HomeCapt; // 배열에 저장
  (*CaptCounter)++;     // 카운터 증가
}
// "usrcode.h"에 추가:
//   void CaptCompISR (void);
//   EXPORT_SYMBOL (CaptCompISR);


// === 비교 인터럽트 서비스 루틴(ISR) (source: UM p852) ===
// 동일한 "CaptCompISR" 이름을 사용한다(이것은 비교 변형 버전).
// 사용자 버퍼에서 채널 0의 다음 비교 위치를 불러온 후,
// 채널 내부의 Equ 상태를 0으로 강제 초기화한다(bit 7 클리어, bit 6 세트).
//
// Script 설정 명령(1회 실행):
//   Gate3[0].IntCtrl = $100000             // PosComp[0] 언마스크 (저장 안됨)
//   Sys.Idata[65535] = 0                   // 비교 카운터 초기화
//   Gate3[0].Chan[0].CompA = Sys.Idata[65536]
//   Gate3[0].Chan[0].CompB = Sys.Idata[65536] + 40960 // + 10 counts
//   Gate3[0].Chan[0].CompAdd = 0           // 하드웨어 자동 증가 비활성화
//   Gate3[0].Chan[0].EquWrite = 2          // 내부 상태를 0으로 강제 설정
//   UserAlgo.CaptCompIntr = 1              // 캡처/비교 ISR 활성화
void CaptCompISR(void)
{
  volatile GateArray3 *MyGate3;  // DSPGATE3 IC 구조체 변수
  int *CompCounter;    // 비교 이벤트 인덱스 포인터
  int *CompPosStore;    // 다음 비교 위치 포인터
  int Temp;

  MyGate3 = GetGate3MemPtr(0);  // Gate3[0] 구조체로 설정
  MyGate3->IntCtrl = 0x10;   // 인터럽트 클리어
  CompCounter = (int *)pushm + 65535;  // Sys.Idata[65535]로 설정
  (*CompCounter)++;    // 이벤트 인덱스 증가
  CompPosStore = (int *)pushm + *CompCounter + 65536; // 다음 위치 지정
  MyGate3->Chan[0].CompA = *CompPosStore;   // 다음 CompA 위치
  MyGate3->Chan[0].CompB = *CompPosStore + 40960;  // 다음 CompB 위치
  Temp = MyGate3->Chan[0].OutCtrl;  // 현재 워드 읽기
  Temp &= 0xFFFFFF7F;    // bit 7 클리어 (강제할 EQU 상태)
  MyGate3->Chan[0].OutCtrl = Temp | 0x40; // bit 6 세트 후 쓰기
}
// "usrcode.h"에 추가:
//   void CaptCompISR (void);
//   EXPORT_SYMBOL (CaptCompISR);
