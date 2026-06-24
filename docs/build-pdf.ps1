<#
  build-pdf.ps1 — manual.html 을 헤드리스 Edge/Chrome 로 manual.pdf 로 렌더한다.
  추가 설치 불필요(Windows 에 기본 포함된 Edge, 또는 Chrome 사용). 멱등·재실행 가능.

  사용:  powershell -ExecutionPolicy Bypass -File .\build-pdf.ps1
#>
$ErrorActionPreference = 'Stop'

$here = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$Html = Join-Path $here 'manual.html'
$Pdf  = Join-Path $here 'manual.pdf'
if (-not (Test-Path $Html)) { throw "HTML 을 찾을 수 없습니다: $Html" }

# 1) 브라우저 탐지: Edge 우선, 없으면 Chrome
$candidates = @(
  "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
  "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
  "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
  "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
  "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe"
)
$browser = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $browser) {
  $browser = (Get-Command msedge, chrome -ErrorAction SilentlyContinue | Select-Object -First 1).Source
}
if (-not $browser) {
  throw "Edge/Chrome 를 찾지 못했습니다. 둘 중 하나를 설치하거나, 브라우저에서 manual.html 을 열고 Ctrl+P 로 PDF 저장하세요."
}

# 2) 헤드리스 렌더 (전용 임시 프로필 → 실행 중인 브라우저와 충돌 방지)
$uri        = ([System.Uri]$Html).AbsoluteUri
$profileDir = Join-Path $env:TEMP 'ppmac-pdf-profile'
if (Test-Path $Pdf) { Remove-Item $Pdf -Force }

function Invoke-Render([string]$headlessFlag) {
  $argList = @(
    $headlessFlag, '--disable-gpu', '--no-first-run', '--disable-extensions',
    "--user-data-dir=$profileDir", '--no-pdf-header-footer',
    "--print-to-pdf=$Pdf", $uri
  )
  # msedge/chrome 는 GUI 서브시스템 앱이라 `&` 로는 대기하지 않는다 → Start-Process -Wait 필수
  Start-Process -FilePath $browser -ArgumentList $argList -Wait -PassThru -WindowStyle Hidden | Out-Null
}

Write-Host "[build-pdf] $([System.IO.Path]::GetFileName($browser)) 로 PDF 생성 중..."
Invoke-Render '--headless'
if (-not (Test-Path $Pdf)) { Invoke-Render '--headless=new' }   # 신버전 폴백

if (Test-Path $Pdf) {
  $kb = [math]::Round((Get-Item $Pdf).Length / 1KB, 1)
  Write-Host "[build-pdf] 완료: $Pdf ($kb KB)"
} else {
  throw "PDF 생성 실패. 브라우저에서 manual.html 을 열고 Ctrl+P 로 저장해 주세요."
}
