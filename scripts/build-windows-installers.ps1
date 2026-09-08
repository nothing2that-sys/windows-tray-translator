param(
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')]
    [string]$AppVersion = "1.1.1",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$installerScript = Join-Path $repositoryRoot "installer\windows-x64.iss"
$publishScript = Join-Path $PSScriptRoot "publish-windows-x64-all.ps1"
$publishRoot = Join-Path $repositoryRoot "artifacts\publish\release-packages"
$distributionRoot = Join-Path $repositoryRoot "dist\windows-x64"
$selfContainedSource = Join-Path $publishRoot "self-contained"
$frameworkDependentSource = Join-Path $publishRoot "framework-dependent"
$selfContainedSetup = Join-Path $distributionRoot "WindowsTrayTranslator-Setup-win-x64-self-contained.exe"
$frameworkDependentSetup = Join-Path $distributionRoot "WindowsTrayTranslator-Setup-win-x64-framework-dependent.exe"
$selfContainedSetupArchive = Join-Path $distributionRoot "WindowsTrayTranslator-Setup-win-x64-self-contained.zip"

function Assert-RepositoryChild([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    $prefix = $repositoryRoot.TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the repository: $resolved"
    }
}

function Find-InnoSetupCompiler {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $knownPaths = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
    )

    foreach ($knownPath in $knownPaths) {
        if (Test-Path -LiteralPath $knownPath -PathType Leaf) {
            return $knownPath
        }
    }

    throw "Inno Setup 6 compiler (ISCC.exe) was not found. Install it from https://jrsoftware.org/isdl.php and run this script again."
}

function Remove-ExistingFile([string]$Path) {
    Assert-RepositoryChild $Path
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        Remove-Item -LiteralPath $Path -Force
    }
}

function Build-Installer(
    [string]$Compiler,
    [string]$SourceDirectory,
    [string]$OutputFilename
) {
    $baseFilename = [IO.Path]::GetFileNameWithoutExtension($OutputFilename)
    & $Compiler `
        "/DSourceDir=$SourceDirectory" `
        "/DOutputDir=$distributionRoot" `
        "/DAppVersion=$AppVersion" `
        "/DOutputBaseFilename=$baseFilename" `
        $installerScript
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $OutputFilename -PathType Leaf)) {
        throw "Installer was not created: $OutputFilename"
    }
}

if (-not $SkipPublish) {
    & $publishScript
    if ($LASTEXITCODE -ne 0) {
        throw "Publishing application packages failed with exit code $LASTEXITCODE."
    }
}

foreach ($sourceDirectory in @($selfContainedSource, $frameworkDependentSource)) {
    $sourceExecutable = Join-Path $sourceDirectory "WindowsTrayTranslator.exe"
    if (-not (Test-Path -LiteralPath $sourceExecutable -PathType Leaf)) {
        throw "Published executable was not found: $sourceExecutable"
    }
}

New-Item -ItemType Directory -Path $distributionRoot -Force | Out-Null
Remove-ExistingFile $selfContainedSetup
Remove-ExistingFile $frameworkDependentSetup
Remove-ExistingFile $selfContainedSetupArchive

$compiler = Find-InnoSetupCompiler
Build-Installer $compiler $selfContainedSource $selfContainedSetup
Build-Installer $compiler $frameworkDependentSource $frameworkDependentSetup
Compress-Archive `
    -LiteralPath $selfContainedSetup `
    -DestinationPath $selfContainedSetupArchive `
    -CompressionLevel Optimal

$releaseFiles = @(
    Get-ChildItem -LiteralPath $distributionRoot -File |
        Where-Object { $_.Extension -in @(".zip", ".exe") } |
        Sort-Object Name
)
$checksumLines = foreach ($releaseFile in $releaseFiles) {
    $hash = (Get-FileHash -LiteralPath $releaseFile.FullName -Algorithm SHA256).Hash
    "$hash  $($releaseFile.Name)"
}
Set-Content -LiteralPath (Join-Path $distributionRoot "SHA256SUMS.txt") -Value $checksumLines -Encoding ascii

Write-Host ""
Write-Host "Created Windows x64 installers:"
Get-Item -LiteralPath $selfContainedSetup, $frameworkDependentSetup |
    Select-Object Name, Length |
    Format-Table -AutoSize
Get-FileHash -Algorithm SHA256 $selfContainedSetup, $frameworkDependentSetup |
    Select-Object Path, Hash |
    Format-Table -AutoSize
