<#
.SYNOPSIS
    K2/K3 검증: 릴리즈 자산의 버전 일치와 SHA-256 체크섬을 재계산해 대조한다.
.DESCRIPTION
    scripts/package-release.ps1 실행 후 사용한다. 파일을 만들거나 수정하지 않는다.
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\Verify-ReleaseArtifacts.ps1
#>
param([string]$Version = "")

$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = & (Join-Path $PSScriptRoot "Get-ReleaseVersion.ps1")
}
$releaseDirectory = Join-Path $repositoryRoot "artifacts\release\$Version"

if (-not (Test-Path -LiteralPath $releaseDirectory)) {
    Write-Host "FAIL  릴리즈 폴더가 없습니다: $releaseDirectory" -ForegroundColor Red
    Write-Host "      먼저 scripts\package-release.ps1 을 실행하세요."
    exit 1
}

$failures = 0
function Report($ok, $id, $message) {
    if ($ok) {
        Write-Host ("PASS  {0,-4} {1}" -f $id, $message) -ForegroundColor Green
    } else {
        Write-Host ("FAIL  {0,-4} {1}" -f $id, $message) -ForegroundColor Red
        $script:failures++
    }
}

Write-Host "=== 릴리즈 자산 검증 (버전 $Version) ===`n"

# K1 - 자산 3종 존재
$installer = Join-Path $releaseDirectory "WindowsTrayTranslator-Setup-$Version-x64.exe"
$portable  = Join-Path $releaseDirectory "WindowsTrayTranslator-Portable-$Version-x64.zip"
$checksums = Join-Path $releaseDirectory "SHA256SUMS.txt"
Report (Test-Path -LiteralPath $installer) "K1" "installer 존재: $(Split-Path $installer -Leaf)"
Report (Test-Path -LiteralPath $portable)  "K1" "portable zip 존재: $(Split-Path $portable -Leaf)"
Report (Test-Path -LiteralPath $checksums) "K1" "SHA256SUMS.txt 존재"

if ($failures -gt 0) { exit 1 }

# K2 - 버전 일치
$installerVersion = (Get-Item -LiteralPath $installer).VersionInfo.ProductVersion
if ($installerVersion) { $installerVersion = $installerVersion.Trim() }
Report ($installerVersion -eq $Version) "K2" "installer ProductVersion '$installerVersion' = Get-ReleaseVersion '$Version'"

$publishedExe = Join-Path $repositoryRoot "artifacts\publish\windows-x64-self-contained\WindowsTrayTranslator.exe"
if (Test-Path -LiteralPath $publishedExe) {
    $exeVersion = (Get-Item -LiteralPath $publishedExe).VersionInfo.ProductVersion
    Report ($exeVersion -like "$Version*") "K2" "published exe ProductVersion '$exeVersion' 이 '$Version' 로 시작"
}

# K3 - 체크섬 재계산 대조
foreach ($line in (Get-Content -LiteralPath $checksums)) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $recorded, $name = $line -split '\s+', 2
    $name = $name.Trim()
    $target = Join-Path $releaseDirectory $name
    if (-not (Test-Path -LiteralPath $target)) {
        Report $false "K3" "$name : 파일 없음"
        continue
    }
    $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    Report ($actual -eq $recorded.ToUpperInvariant()) "K3" "$name 해시 일치"
}

Write-Host ""
if ($failures -eq 0) {
    Write-Host "모든 항목 PASS (K1/K2/K3)" -ForegroundColor Green
    exit 0
}

Write-Host "$failures 건 FAIL" -ForegroundColor Red
exit 1
