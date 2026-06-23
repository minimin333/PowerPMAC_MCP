// Power PMAC C-language routine skeletons
// Harvested verbatim from the Power PMAC User's Manual (UM).
// All files accessing shared memory must start with #include <RtGpShm.h>.
// For each routine, matching prototype + EXPORT_SYMBOL must appear in
// "usrcode.h" (see notes per example). Never embed user code in an
// indefinite loop -- the scheduler calls these routines repeatedly.

#include <RtGpShm.h>
#include <stdio.h>
#include <dlfcn.h>

// === Real-time interrupt C PLC skeleton (source: UM p858-859) ===
// Routine MUST be named "realtimeinterrupt_plcc" and declared void().
// Lives in CPLCs -> rticplc -> "rtiplcc.c".
// Enable at runtime with: UserAlgo.RtiCplc = 1  (disable with 0).
// This example writes alternating on/off patterns to banks of discrete outputs.
#define IoCard0Out0_7 *(piom + 0xA0000C/4)
#define IoCard0Out8_15 *(piom + 0xA00010/4)
#define IoCard0Out16_23 *(piom + 0xA00014/4)
#define OutputData(x) (x << 8)

void realtimeinterrupt_plcc()   // RTI C PLC function
{
  static int i = 0;
  if (i++>1000) {    // > 1 sec from cycle start
 IoCard0Out0_7=OutputData(0xAA); // Odd-numbered outputs on
 IoCard0Out8_15=OutputData(0xAA);
 IoCard0Out16_23=OutputData(0xAA);
 if (i>2000) i=0;   // Reset to start of cycle
  }
  else {     // < 1 sec from cycle start
 IoCard0Out0_7=OutputData(0x55); // Even-numbered outputs on
 IoCard0Out8_15=OutputData(0x55);
 IoCard0Out16_23=OutputData(0x55);
  }
}


// === Capture interrupt service routine (ISR) (source: UM p850-851) ===
// There can be only ONE routine of this type; it MUST be named "CaptCompISR"
// and declared exactly "void CaptCompISR (void)". Lives in Realtime Routines
// -> "usrcode.c". NO floating-point (P/Q vars) allowed inside a capture/
// compare ISR -- use integer arrays in the user shared-memory buffer.
//
// Script setup commands (run once):
//   Gate3[0].IntCtrl = $10000   // Unmask PosCapt[0] (not saved)
//   Sys.Idata[65535] = 0        // Initialize trigger counter
//   UserAlgo.CaptCompIntr = 1   // Enable capture/compare ISR
//
// Logs each captured position from channel 0 into the user shared-memory buffer.
void CaptCompISR (void)
{
  volatile GateArray3 *MyFirstGate3IC;  // ASIC structure pointer
  int *CaptCounter;     // Logs number of triggers
  int *CaptPosStore;     // Storage pointer

  MyFirstGate3IC = GetGate3MemPtr(0);   // Pointer to IC base
  MyFirstGate3IC->IntCtrl = 1;   // Clear interrupt source
  CaptCounter = (int *)pushm + 65535;   // Sys.Idata[65535]
  CaptPosStore = (int *)pushm + *CaptCounter + 65536;
  *CaptPosStore = MyFirstGate3IC->Chan[0].HomeCapt; // Store in array
  (*CaptCounter)++;     // Increment counter
}
// In "usrcode.h":
//   void CaptCompISR (void);
//   EXPORT_SYMBOL (CaptCompISR);


// === Compare interrupt service routine (ISR) (source: UM p852) ===
// Same single-routine "CaptCompISR" name applies (this is the compare variant).
// Loads each next compare position for channel 0 from the user buffer, then
// forces the channel's internal Equ state back to 0 (bit 7 clear, bit 6 set).
//
// Script setup commands (run once):
//   Gate3[0].IntCtrl = $100000             // Unmask PosComp[0] (not saved)
//   Sys.Idata[65535] = 0                   // Initialize compare counter
//   Gate3[0].Chan[0].CompA = Sys.Idata[65536]
//   Gate3[0].Chan[0].CompB = Sys.Idata[65536] + 40960 // + 10 counts
//   Gate3[0].Chan[0].CompAdd = 0           // Disable hardware increment
//   Gate3[0].Chan[0].EquWrite = 2          // Force internal state to 0
//   UserAlgo.CaptCompIntr = 1              // Enable capture/compare ISR
void CaptCompISR(void)
{
  volatile GateArray3 *MyGate3;  // DSPGATE3 IC structure variable
  int *CompCounter;    // Pointer to compare event index
  int *CompPosStore;    // Pointer to next compare position
  int Temp;

  MyGate3 = GetGate3MemPtr(0);  // Set to Gate3[0] structure
  MyGate3->IntCtrl = 0x10;   // Clear interrupt
  CompCounter = (int *)pushm + 65535;  // Set to Sys.Idata[65535]
  (*CompCounter)++;    // Increment event index
  CompPosStore = (int *)pushm + *CompCounter + 65536; // Point to next
  MyGate3->Chan[0].CompA = *CompPosStore;   // Next CompA pos
  MyGate3->Chan[0].CompB = *CompPosStore + 40960;  // Next CompB pos
  Temp = MyGate3->Chan[0].OutCtrl;  // Read present word
  Temp &= 0xFFFFFF7F;    // Clear bit 7 (EQU state to force)
  MyGate3->Chan[0].OutCtrl = Temp | 0x40; // Set bit 6 and write
}
// In "usrcode.h":
//   void CaptCompISR (void);
//   EXPORT_SYMBOL (CaptCompISR);
