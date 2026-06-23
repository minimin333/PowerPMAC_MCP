# Power PMAC Data-Structure Element Index (authoritative)

Generated from the controller firmware intellisense tables `pp_swtbl0-3.txt`
(simulator opt/ppmac, fw 2.3.1.82) — the canonical `Structure,Element` lists the
IDE/C intellisense uses. For the full element set under a structure, grep the tables
in `reference/firmware/` (e.g. `Motor`, `Coord`, `Sys` in `pp_swtbl1.txt`).
Live controller is fw 2.8.3.0 — a few elements may differ; confirm with `get_response`.

Source tables: pp_swtbl0.txt (40 rows), pp_swtbl1.txt (1246 rows), pp_swtbl2.txt (563 rows), pp_swtbl3.txt (19 rows)
Total entries: 1828  |  distinct top structures: 75

## Top-level structures by element count
| Structure | # elements |
|---|---|
| `Motor[]` | 307 |
| `Coord[]` | 231 |
| `Sys` | 151 |
| `Servo` | 98 |
| `Chan[]` | 94 |
| `Gate3[]` | 63 |
| `ECAT[]` | 51 |
| `Gate2[]` | 38 |
| `Gather` | 32 |
| `Acc72E[]` | 31 |
| `Ldata` | 30 |
| `CCData[]` | 29 |
| `CCExec` | 29 |
| `CamTable[]` | 28 |
| `EncTable[]` | 26 |
| `BrickAC` | 23 |
| `Slave[]` | 23 |
| `CompTable[]` | 18 |
| `Gate1[]` | 17 |
| `Status[]` | 17 |
| `Desired` | 16 |
| `New[]` | 16 |
| `BufIo[]` | 15 |
| `Acc36E[]` | 12 |
| `UserAlgo` | 11 |
| `Acc59E[]` | 10 |
| `Acc72EX[]` | 10 |
| `Config` | 10 |
| `IO[]` | 10 |
| `LHData[]` | 10 |
| `LHExec` | 10 |
| `LPIO[]` | 10 |
| `Macro` | 10 |
| `MuxIo` | 9 |
| `Plc[]` | 9 |
| `Acc84C[]` | 8 |
| `BrickLV` | 8 |
| `GateIo[]` | 8 |
| `Init` | 8 |
| `LPDomainOutputRegs[]` | 8 |
| `LPDomainRegs[]` | 8 |
| `Modbus[]` | 8 |
| `RTDomainOutputRegs[]` | 8 |
| `RTDomainRegs[]` | 8 |
| `SyncTable[]` | 8 |
| `Bp[]` | 7 |
| `Cid[]` | 7 |
| `ModbusServer[]` | 7 |
| `Stack[]` | 7 |
| `TraceData[]` | 7 |
| `TraceExec` | 7 |
| `Acc28E[]` | 6 |
| `Acc84B[]` | 6 |
| `Acc84E[]` | 6 |
| `Acc84S[]` | 6 |
| `AdcDemux` | 6 |
| `PortA[]` | 6 |
| `PortB[]` | 6 |
| `Tdata[]` | 6 |
| `Acc53E[]` | 5 |
| `AuxChan[]` | 5 |
| `CtrlPanel[]` | 5 |
| `SendFiles[]` | 5 |
| `SyncManager[]` | 5 |
| `CC3Data[]` | 4 |
| `PDO[]` | 4 |
| `Prog[]` | 4 |
| `Program` | 4 |
| `SubProg[]` | 4 |
| `Forward` | 3 |
| `Inverse` | 3 |
| `PDOMapping[]` | 3 |
| `TPData[]` | 3 |
| `TPExec` | 3 |
| `RingTest[]` | 2 |
