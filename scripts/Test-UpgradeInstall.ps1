<#
.SYNOPSIS
    K4 검증: 기존 설정을 가진 상태에서 installer로 upgrade 설치한 뒤 사용자 데이터가 보존되는지 확인한다.
.DESCRIPTION
    설치 전 %LOCALAPPDATA%\WindowsTrayTranslator 의 config / secrets.dat / history 를 해시로 기록하고,
    installer 를 무인 모드로 실행한 뒤 다시 해시를 계산해 대조한다.

    -Install 을 주지 않으면 스냅샷과 사전 점검만 수행하고 설치하지 않는다(기본값).
    실제 설치는 시스템을 변경하므로 -Install 을 명시해야 한다.
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\Test-UpgradeInstall.ps1
    powershell -ExecutionPolicy Bypass -File scripts\Test-UpgradeInstall.ps1 -Install
#>
param(
    [switch]$Install,
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = & (Join-Path $PSScriptRoot "Get-ReleaseVersion.ps1")
}
$installer = Join-Path $repositoryRoot "artifacts\release\$Version\WindowsTrayTranslator-Setup-$Version-x64.exe"
$dataRoot = Join-Path $env:LOCALAPPDATA "WindowsTrayTranslator"

function Snapshot {
    $items = @{}
    foreach ($relative in @("config\appsettings.json", "secrets.dat")) {
        $path = Join-Path $dataRoot $relative
        if (Test-Path -LiteralPath $path) {
            $items[$relative] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        }
    }
    $historyDirectory = Join-Path $dataRoot "history"
    if (Test-Path -LiteralPath $historyDirectory) {
        foreach ($file in Get-ChildItem -LiteralPath $historyDirectory -Filter "*.jsonl" -File) {
            $items["history\$($file.Name)"] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        }
    }
    return $items
}

if (-not (Test-Path -LiteralPath $dataRoot)) {
    Write-Host "SKIP  기존 사용자 데이터가 없습니다: $dataRoot" -ForegroundColor Yellow
    Write-Host "      앱을 한 번 실행해 설정과 API 키를 만든 뒤 다시 시도하세요."
    exit 2
}

$before = Snapshot
Write-Host "=== 설치 전 스냅샷 ==="
if ($before.Count -eq 0) {
    Write-Host "  (보존 대상 파일 없음)"
} else {
    $before.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Host ("  {0,-40} {1}" -f $_.Key, $_.Value.Substring(0, 16)) }
}

$settingsPath = Join-Path $dataRoot "config\appsettings.json"
$beforeSettings = $null
if (Test-Path -LiteralPath $settingsPath) {
    $beforeSettings = Get-Content -LiteralPath $settingsPath -Raw -Encoding utf8 | ConvertFrom-Json
    Write-Host "`n설치 전 단축키: Read=$($beforeSettings.Translation.ReadHotkey), Replace=$($beforeSettings.Translation.ReplaceHotkey), Palette=$($beforeSettings.Translation.ActionPaletteHotkey)"
}

if (-not $Install) {
    Write-Host "`n-Install 이 없어 설치를 건너뜁니다. 실제 검증은 다음으로 실행하세요:" -ForegroundColor Yellow
    Write-Host "  powershell -ExecutionPolicy Bypass -File scripts\Test-UpgradeInstall.ps1 -Install"
    exit 0
}

if (-not (Test-Path -LiteralPath $installer)) {
    Write-Host "FAIL  installer 가 없습니다: $installer" -ForegroundColor Red
    exit 1
}

if (Get-Process -Name WindowsTrayTranslator -ErrorAction SilentlyContinue) {
    Write-Host "`n실행 중인 앱을 종료합니다."
    Stop-Process -Name WindowsTrayTranslator -Force
    Start-Sleep -Seconds 2
}

Write-Host "`n=== installer 실행 (무인 모드) ==="
$process = Start-Process -FilePath $installer -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" -Wait -PassThru
Write-Host "installer 종료 코드: $($process.ExitCode)"
if ($process.ExitCode -ne 0) {
    Write-Host "FAIL  installer 가 0이 아닌 코드로 종료했습니다." -ForegroundColor Red
    exit 1
}

$after = Snapshot
$failures = 0
Write-Host "`n=== K4 대조 ==="
foreach ($key in $before.Keys) {
    if (-not $after.ContainsKey($key)) {
        Write-Host ("FAIL  {0} : 삭제됨" -f $key) -ForegroundColor Red
        $failures++
    } elseif ($after[$key] -ne $before[$key]) {
        Write-Host ("FAIL  {0} : 변경됨" -f $key) -ForegroundColor Red
        $failures++
    } else {
        Write-Host ("PASS  {0} : 보존" -f $key) -ForegroundColor Green
    }
}

Write-Host "`n=== K5 대조 ==="
if (Test-Path -LiteralPath $settingsPath) {
    $afterSettings = Get-Content -LiteralPath $settingsPath -Raw -Encoding utf8 | ConvertFrom-Json
    $paletteOk = $null -ne $afterSettings.Translation.ActionPaletteHotkey
    Write-Host ("{0}  ActionPaletteHotkey 존재: '{1}'" -f $(if ($paletteOk) { "PASS" } else { "FAIL" }), $afterSettings.Translation.ActionPaletteHotkey)
    if (-not $paletteOk) { $failures++ }

    if ($beforeSettings) {
        foreach ($name in @("ReadHotkey", "ReplaceHotkey", "ReadTargetLanguage", "ReplaceFormat")) {
            $same = $beforeSettings.Translation.$name -eq $afterSettings.Translation.$name
            Write-Host ("{0}  {1} 보존: '{2}'" -f $(if ($same) { "PASS" } else { "FAIL" }), $name, $afterSettings.Translation.$name)
            if (-not $same) { $failures++ }
        }
    }
} else {
    Write-Host "FAIL  설치 후 설정 파일이 없습니다." -ForegroundColor Red
    $failures++
}

$installedExe = Join-Path $env:LOCALAPPDATA "Programs\WindowsTrayTranslator\WindowsTrayTranslator.exe"
if (Test-Path -LiteralPath $installedExe) {
    Write-Host "`n설치된 버전: $((Get-Item -LiteralPath $installedExe).VersionInfo.ProductVersion)"
}

Write-Host ""
if ($failures -eq 0) {
    Write-Host "K4/K5 전부 PASS" -ForegroundColor Green
    exit 0
}
Write-Host "$failures 건 FAIL" -ForegroundColor Red
exit 1
