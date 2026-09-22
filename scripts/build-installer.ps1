param(
    [Parameter(Mandatory = $true)][string]$SourceDirectory,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$Version = (& (Join-Path $PSScriptRoot "Get-ReleaseVersion.ps1"))
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$installerScript = Join-Path $repositoryRoot "installer\windows-x64.iss"
$SourceDirectory = [IO.Path]::GetFullPath($SourceDirectory)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)

function Find-InnoSetupCompiler {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) { return $command.Source }
    foreach ($path in @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"))) {
        if (Test-Path -LiteralPath $path -PathType Leaf) { return $path }
    }
    throw "Inno Setup 6 compiler (ISCC.exe) was not found."
}

$sourceExecutable = Join-Path $SourceDirectory "WindowsTrayTranslator.exe"
if (-not (Test-Path -LiteralPath $sourceExecutable -PathType Leaf)) {
    throw "Published executable was not found: $sourceExecutable"
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$outputBaseName = "WindowsTrayTranslator-Setup-$Version-x64"
$compiler = Find-InnoSetupCompiler
& $compiler `
    "/DSourceDir=$SourceDirectory" `
    "/DOutputDir=$OutputDirectory" `
    "/DAppVersion=$Version" `
    "/DOutputBaseFilename=$outputBaseName" `
    $installerScript | Out-Host
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$installer = Join-Path $OutputDirectory "$outputBaseName.exe"
if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
    throw "Installer was not created: $installer"
}

$installer
