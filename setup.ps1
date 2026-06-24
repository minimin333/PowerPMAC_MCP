<#
.SYNOPSIS
  Power PMAC 도구 설치 스크립트 — Skill 설치 + MCP 서버 빌드·등록.

.DESCRIPTION
  이 저장소를 clone한 PC에서 한 번 실행하면:
    1) Skill을 ~/.claude/skills/ 에 junction 으로 설치(매뉴얼 없이 동작),
    2) (PDK/IDE가 있으면) MCP 서버를 사용자 PDK로 빌드하고 'claude mcp add --scope user'로 등록.
  PDK·컴파일러 경로는 환경변수→DTBUILDPATH→레지스트리→기본 설치경로 순으로 자동 감지한다.
  멱등(재실행 안전). 관리자 권한 불필요.

.PARAMETER PdkHome
  PDK 폴더를 직접 지정(CLLLicFile.lic 가 있는 폴더). 자동 감지가 실패할 때만 사용.

.PARAMETER SkillOnly
  MCP 빌드·등록을 건너뛰고 Skill만 설치(컨트롤러·PDK 없는 PC용).

.PARAMETER Verify
  빌드한 exe에 initialize/tools.list 를 보내 응답을 확인.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\setup.ps1
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\setup.ps1 -SkillOnly
#>
[CmdletBinding()]
param(
  [string]$PdkHome,
  [switch]$SkillOnly,
  [switch]$Quiet,
  [switch]$Verify
)

$ErrorActionPreference = 'Stop'
$Repo     = $PSScriptRoot
$SkillSrc = Join-Path $Repo 'Skills\powerpmac-dev'
$Csproj   = Join-Path $Repo 'mcp-server\PowerPmacMcp.csproj'
$Exe      = Join-Path $Repo 'mcp-server\bin\Release\powerpmac-mcp.exe'
$script:Compilers = $null

function Info($m){ if(-not $Quiet){ Write-Host "[setup] $m" -ForegroundColor Cyan } }
function Ok($m){   if(-not $Quiet){ Write-Host "[setup] $m" -ForegroundColor Green } }
function Warn($m){ Write-Host "[setup] $m" -ForegroundColor Yellow }

# ---- 감지 헬퍼 ----------------------------------------------------------
function Test-IsPdk($p){
  if([string]::IsNullOrWhiteSpace($p)){ return $false }
  $p = $p.Trim().TrimEnd('\','"')
  return (Test-Path (Join-Path $p 'CLLLicFile.lic')) -and
         (Test-Path (Join-Path $p 'ODT.PowerPmacBuildAndDownload.dll')) -and
         (Test-Path (Join-Path $p 'PPMAC460CompileTask.dll')) -and
         (Test-Path (Join-Path $p 'cygwin1.dll'))
}
function Test-HasRsync($p){
  if([string]::IsNullOrWhiteSpace($p)){ return $false }
  return (Test-Path (Join-Path ($p.Trim().TrimEnd('\','"')) 'bin\rsync.exe'))
}
function Get-RegInstall([scriptblock]$nameMatch, [scriptblock]$validate){
  foreach($root in @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
                     'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall')){
    if(-not (Test-Path $root)){ continue }
    foreach($k in (Get-ChildItem $root -ErrorAction SilentlyContinue)){
      $pp = Get-ItemProperty $k.PSPath -ErrorAction SilentlyContinue
      if($null -eq $pp -or -not $pp.DisplayName -or -not $pp.InstallLocation){ continue }
      if((& $nameMatch $pp.DisplayName)){
        $loc = $pp.InstallLocation.Trim().TrimEnd('\','"')
        if((& $validate $loc)){ return $loc }
      }
    }
  }
  return $null
}
function Find-Compilers {
  if((Test-HasRsync $env:POWERPMAC_COMPILERS_HOME)){ return $env:POWERPMAC_COMPILERS_HOME.Trim().TrimEnd('\','"') }
  if($env:DTBUILDPATH){
    $root = Split-Path (($env:DTBUILDPATH -split ';')[0].Trim())   # ...\Compilers\bin -> ...\Compilers
    if((Test-HasRsync $root)){ return $root }
  }
  $reg = Get-RegInstall { param($n) ($n -match 'PowerPMAC') -and ($n -match 'Compiler') } { param($p) Test-HasRsync $p }
  if($reg){ return $reg }
  if((Test-HasRsync 'C:\DeltaTau\PowerPMAC\Compilers')){ return 'C:\DeltaTau\PowerPMAC\Compilers' }
  return $null
}
function Find-Pdk($override){
  if((Test-IsPdk $override)){ return $override.Trim().TrimEnd('\','"') }
  if((Test-IsPdk $env:POWERPMAC_PDK_HOME)){ return $env:POWERPMAC_PDK_HOME.Trim().TrimEnd('\','"') }
  $reg = Get-RegInstall { param($n) $n -eq 'PowerPMAC Development Kit' } { param($p) Test-IsPdk $p }
  if($reg){ return $reg }
  $roots = @()
  if($script:Compilers){ $roots += (Split-Path $script:Compilers) }   # ...\PowerPMAC
  $roots += @('C:\DeltaTau\PowerPMAC','C:\Program Files (x86)\Delta Tau\Power PMAC')
  foreach($r in ($roots | Select-Object -Unique)){
    if(-not (Test-Path $r)){ continue }
    if((Test-IsPdk (Join-Path $r 'PDK'))){ return (Join-Path $r 'PDK') }
    foreach($d in (Get-ChildItem $r -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending)){
      $cand = Join-Path $d.FullName 'PDK'
      if((Test-IsPdk $cand)){ return $cand }
    }
  }
  return $null
}

# ---- 1. Skill 설치 (junction) ------------------------------------------
function Install-Skill {
  if(-not (Test-Path $SkillSrc)){ throw "Skill 폴더가 없습니다: $SkillSrc (저장소 clone 확인)" }
  $dest = Join-Path $HOME '.claude\skills\powerpmac-dev'
  $parent = Split-Path $dest
  if(-not (Test-Path $parent)){ New-Item -ItemType Directory -Path $parent -Force | Out-Null }
  if(Test-Path $dest){
    $it = Get-Item $dest -Force
    if($it.Attributes -band [IO.FileAttributes]::ReparsePoint){
      cmd /c rmdir "$dest" | Out-Null      # junction 링크만 제거(대상 보존)
    } else {
      Remove-Item $dest -Recurse -Force
    }
  }
  New-Item -ItemType Junction -Path $dest -Target $SkillSrc | Out-Null
  Ok "Skill 설치: $dest  ->  $SkillSrc"
}

# ======================= main =======================
Info "저장소: $Repo"
Install-Skill

if($SkillOnly){ Ok "SkillOnly 모드 — 완료. Claude Code 재시작 후 Power PMAC 질문이 동작합니다."; return }

if(-not (Get-Command dotnet -ErrorAction SilentlyContinue)){
  Warn ".NET SDK(dotnet)가 없어 MCP를 빌드할 수 없습니다. Skill만 설치됨."
  Warn "  -> https://dotnet.microsoft.com/download 설치 후 다시 실행하세요."
  return
}

$script:Compilers = Find-Compilers
$pdk = Find-Pdk $PdkHome
if(-not $pdk){
  Warn "Power PMAC PDK를 찾지 못했습니다 — Skill만 설치됨."
  Warn "  -> Power PMAC IDE/PDK를 설치하거나, -PdkHome `"<CLLLicFile.lic 폴더>`" 로 지정해 다시 실행하세요."
  return
}
Ok "PDK = $pdk"
if($script:Compilers){ Ok "Compilers = $script:Compilers" } else { Warn "컴파일러 미발견 — C 빌드/다운로드가 제한될 수 있습니다(Power PMAC IDE 설치 권장)." }

# ---- 2. MCP 빌드 -------------------------------------------------------
Info "MCP 서버 빌드 중 (x86/net48)..."
& dotnet build $Csproj -c Release "/p:PdkHome=$pdk" | Out-Host
if($LASTEXITCODE -ne 0 -or -not (Test-Path $Exe)){ throw "MCP 빌드 실패 (위 로그 확인)." }
Ok "빌드 완료: $Exe"

# ---- 3. 등록 (claude mcp add, user scope) ------------------------------
if(Get-Command claude -ErrorAction SilentlyContinue){
  try { & claude mcp remove powerpmac --scope user 2>$null | Out-Null } catch {}
  $envArgs = @('-e',"POWERPMAC_PDK_HOME=$pdk")
  if($script:Compilers){ $envArgs += @('-e',"POWERPMAC_COMPILERS_HOME=$script:Compilers") }
  & claude mcp add powerpmac $Exe --scope user @envArgs
  if($LASTEXITCODE -eq 0){ Ok "MCP 등록 완료 (powerpmac, user scope)." }
  else { Warn "claude mcp add 실패 — 아래 명령을 수동 실행하세요:"; Write-Host "  claude mcp add powerpmac `"$Exe`" --scope user -e POWERPMAC_PDK_HOME=`"$pdk`"" }
} else {
  Warn "claude CLI를 찾지 못했습니다. 아래 명령으로 수동 등록하세요:"
  Write-Host "  claude mcp add powerpmac `"$Exe`" --scope user -e POWERPMAC_PDK_HOME=`"$pdk`"" -ForegroundColor White
}

# ---- 4. (선택) 스모크 ---------------------------------------------------
if($Verify){
  Info "스모크 테스트..."
  $tin  = Join-Path $env:TEMP 'ppmac_verify.jsonl'
  $tout = Join-Path $env:TEMP 'ppmac_verify.out'
  '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}' | Set-Content $tin
  '{"jsonrpc":"2.0","id":2,"method":"tools/list"}' | Add-Content $tin
  cmd /c "`"$Exe`" < `"$tin`" > `"$tout`" 2>nul" | Out-Null
  if((Get-Content $tout -Raw -ErrorAction SilentlyContinue) -match '"tools"'){ Ok "스모크 OK — exe가 tools 목록을 응답함." }
  else { Warn "스모크 실패 — exe 응답을 확인하세요." }
  Remove-Item $tin,$tout -ErrorAction SilentlyContinue
}

Ok "설치 완료. Claude Code를 재시작하면 Skill과 MCP(powerpmac)가 활성화됩니다."
