<#
.SYNOPSIS
    H1/H2 검증: 다른 프로그램이 단축키를 선점한 상태에서 앱을 시작해, 나머지 단축키가 살아남는지 확인한다.
.DESCRIPTION
    이 스크립트가 RegisterHotKey로 지정한 조합을 실제로 점유한 뒤 앱을 실행하고, 로그를 읽어
    등록 결과를 판정한다. 끝나면 앱을 종료하고 점유를 해제한다. 설치나 설정 변경은 하지 않는다.

    앱이 이미 실행 중이면 단일 인스턴스 정책 때문에 판정할 수 없으므로 먼저 종료해야 한다.
.PARAMETER Chord
    점유할 조합. Ctrl+Alt+A (H1) 또는 Alt+T (H2).
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\Test-HotkeyConflict.ps1 -Chord "Ctrl+Alt+A"
    powershell -ExecutionPolicy Bypass -File scripts\Test-HotkeyConflict.ps1 -Chord "Alt+T"
#>
param(
    [Parameter(Mandatory = $true)][string]$Chord,
    [int]$WaitSeconds = 6
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$exe = Join-Path $repositoryRoot "artifacts\publish\windows-x64-self-contained\WindowsTrayTranslator.exe"
$logDirectory = Join-Path $env:LOCALAPPDATA "WindowsTrayTranslator\logs"

if (-not (Test-Path -LiteralPath $exe)) {
    Write-Host "FAIL  게시된 실행 파일이 없습니다: $exe" -ForegroundColor Red
    Write-Host "      먼저 scripts\publish.ps1 을 실행하세요."
    exit 1
}

if (Get-Process -Name WindowsTrayTranslator -ErrorAction SilentlyContinue) {
    Write-Host "FAIL  WindowsTrayTranslator 가 이미 실행 중입니다. 트레이에서 종료한 뒤 다시 실행하세요." -ForegroundColor Red
    exit 1
}

Add-Type -Namespace WttVerify -Name Native -MemberDefinition @"
[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
public static extern bool RegisterHotKey(System.IntPtr hWnd, int id, uint fsModifiers, uint vk);
[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
public static extern bool UnregisterHotKey(System.IntPtr hWnd, int id);
"@

# MOD_ALT 1, MOD_CONTROL 2, MOD_SHIFT 4, MOD_WIN 8
$modifiers = 0
$key = $null
foreach ($part in ($Chord -split '\+' | ForEach-Object { $_.Trim() })) {
    switch ($part.ToUpperInvariant()) {
        "CTRL"    { $modifiers = $modifiers -bor 2 }
        "CONTROL" { $modifiers = $modifiers -bor 2 }
        "ALT"     { $modifiers = $modifiers -bor 1 }
        "SHIFT"   { $modifiers = $modifiers -bor 4 }
        "WIN"     { $modifiers = $modifiers -bor 8 }
        default   { $key = $part.ToUpperInvariant() }
    }
}
if ($modifiers -eq 0 -or -not $key -or $key.Length -ne 1) {
    Write-Host "FAIL  조합을 해석하지 못했습니다: $Chord" -ForegroundColor Red
    exit 1
}
$virtualKey = [byte][char]$key

$marker = Get-Date
$hotkeyId = 0x5754
if (-not [WttVerify.Native]::RegisterHotKey([IntPtr]::Zero, $hotkeyId, $modifiers, $virtualKey)) {
    Write-Host "FAIL  '$Chord' 를 점유하지 못했습니다. 다른 프로그램이 이미 쓰고 있을 수 있습니다." -ForegroundColor Red
    exit 1
}
Write-Host "'$Chord' 를 점유했습니다. 앱을 시작합니다...`n"

$process = $null
try {
    $process = Start-Process -FilePath $exe -PassThru
    Start-Sleep -Seconds $WaitSeconds

    $log = Get-ChildItem -LiteralPath $logDirectory -Filter "translator-*.log" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $log) {
        Write-Host "FAIL  로그 파일을 찾지 못했습니다: $logDirectory" -ForegroundColor Red
        exit 1
    }

    $lines = Get-Content -LiteralPath $log.FullName -Encoding utf8 |
        Where-Object { $_ -match '^\d{4}-\d{2}-\d{2}T' } |
        Where-Object { [datetime]::Parse(($_ -split ' ')[0]) -ge $marker }

    Write-Host "=== 시작 이후 단축키 관련 로그 ==="
    $relevant = $lines | Where-Object { $_ -match '단축키' }
    if ($relevant) { $relevant | ForEach-Object { Write-Host "  $_" } } else { Write-Host "  (없음)" }
    Write-Host ""

    # 시작 경로는 실패만 로그에 남기던 문제가 있어, 이제 키별 결과 요약 한 줄을 남긴다.
    $summary = $relevant | Where-Object { $_ -match '시작 시 전역 단축키 등록 결과' } | Select-Object -Last 1
    $failed  = $relevant | Where-Object { $_ -match '등록하지 못했습니다|사용 중' }

    if (-not $summary) {
        Write-Host "판정 불가  등록 결과 요약 로그가 없습니다. 이 빌드가 최신인지 확인하세요." -ForegroundColor Yellow
        Write-Host "           (scripts\publish.ps1 재실행 필요)"
        exit 2
    }

    $readAlive    = $summary -match 'Read=\S+\(성공\)'
    $replaceAlive = $summary -match 'Replace=\S+\(성공\)'
    $paletteAlive = $summary -match 'Palette=\S+\(성공\)'

    if ($Chord -match 'Ctrl.*Alt.*A') {
        Write-Host "--- H1 판정 ---"
        Write-Host ("  Palette 등록 실패 경고 존재 : {0}" -f ($(if ($failed) { 'PASS' } else { 'FAIL' })))
        Write-Host ("  Palette 비활성              : {0}" -f ($(if (-not $paletteAlive) { 'PASS' } else { 'FAIL' })))
        Write-Host ("  Alt+R / Alt+T 유지          : {0}" -f ($(if ($readAlive -and $replaceAlive) { 'PASS' } else { 'FAIL' })))
    } else {
        Write-Host "--- H2 판정 ---"
        Write-Host ("  충돌 단축키만 실패 경고     : {0}" -f ($(if ($failed) { 'PASS' } else { 'FAIL' })))
        Write-Host ("  나머지 core 단축키 유지     : {0}" -f ($(if ($readAlive -or $replaceAlive) { 'PASS' } else { 'FAIL' })))
    }
    Write-Host "`n위 로그 원문을 수동 검증 매트릭스의 비고에 붙여 넣으세요."
}
finally {
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        Write-Host "`n앱을 종료했습니다."
    }
    [void][WttVerify.Native]::UnregisterHotKey([IntPtr]::Zero, $hotkeyId)
    Write-Host "'$Chord' 점유를 해제했습니다."
}
