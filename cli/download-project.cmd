@echo off
setlocal
rem  Usage: download-project.cmd <projectDir> <ip> <password>
rem  Transfers a built Power PMAC project to the controller (rsync) and loads it (projpp).
rem  Uses the proven sshpass+rsync path; run with a real console.

set "PROJDIR=%~1"
set "IP=%~2"
set "PW=%~3"
if "%PW%"=="" set "PW=deltatau"

rem  Locate the IDE compilers (rsync/ssh/sshpass): env override -> DTBUILDPATH -> default install.
if defined POWERPMAC_COMPILERS_HOME (
  set "PATH=%POWERPMAC_COMPILERS_HOME%\bin;%POWERPMAC_COMPILERS_HOME%\usr\local\bin;%PATH%"
) else if defined DTBUILDPATH (
  set "PATH=%DTBUILDPATH%;%PATH%"
) else (
  set "PATH=C:\DeltaTau\PowerPMAC\Compilers\bin;C:\DeltaTau\PowerPMAC\Compilers\usr\local\bin;%PATH%"
)
set "SSH=ssh -F /dev/null -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o ConnectTimeout=15"

echo === RSYNC %PROJDIR% -^> %IP% ===
rem  cd into the project dir and use a RELATIVE source: a Windows "C:\..." path makes rsync
rem  treat the drive letter "C:" as a remote host ("source and destination both remote").
cd /d "%PROJDIR%"
sshpass -p %PW% rsync -rtvz -s --rsh="%SSH%" ^
  --exclude=".vs" --exclude=".vscode" --exclude="Temp" --exclude="Log" ^
  --exclude="*.log" --exclude="*_pp_debug.txt" --exclude="DownloadProgress.txt" ^
  --exclude="errors.txt" --exclude="warnings.txt" --exclude="Exclude.txt" --exclude="rsync-filter.txt" ^
  --exclude="*.o" ^
  "./" "root@%IP%:/var/ftp/usrflash/Project/"
echo RSYNC_EXIT=%ERRORLEVEL%

echo === PROJPP (load) ===
sshpass -p %PW% %SSH% root@%IP% "cd /var/ftp/usrflash/Project && projpp -l 2>&1"
echo PROJPP_EXIT=%ERRORLEVEL%
endlocal
