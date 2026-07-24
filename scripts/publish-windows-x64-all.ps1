$ErrorActionPreference = "Stop"

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$project = Join-Path $repositoryRoot "src\WindowsTrayTranslator\WindowsTrayTranslator.csproj"
$workingRoot = Join-Path $repositoryRoot "artifacts\publish\release-packages"
$distributionRoot = Join-Path $repositoryRoot "dist\windows-x64"
$selfContainedOutput = Join-Path $workingRoot "self-contained"
$frameworkDependentOutput = Join-Path $workingRoot "framework-dependent"
$selfContainedArchive = Join-Path $distributionRoot "WindowsTrayTranslator-win-x64-self-contained.zip"
$frameworkDependentArchive = Join-Path $distributionRoot "WindowsTrayTranslator-win-x64-framework-dependent.zip"

function Assert-RepositoryChild([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    $prefix = $repositoryRoot.TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the repository: $resolved"
    }
}

function Remove-ExistingDirectory([string]$Path) {
    Assert-RepositoryChild $Path
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Assert-SingleExecutable([string]$OutputDirectory) {
    $executable = Join-Path $OutputDirectory "WindowsTrayTranslator.exe"
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "Published executable was not found: $executable"
    }

    $entries = @(Get-ChildItem -LiteralPath $OutputDirectory -Force)
    if ($entries.Count -ne 1) {
        throw "Single-file verification failed for $OutputDirectory. Found $($entries.Count) entries."
    }
}

Remove-ExistingDirectory $workingRoot
Remove-ExistingDirectory $distributionRoot
New-Item -ItemType Directory -Path $selfContainedOutput -Force | Out-Null
New-Item -ItemType Directory -Path $frameworkDependentOutput -Force | Out-Null
New-Item -ItemType Directory -Path $distributionRoot -Force | Out-Null

& dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    --no-restore `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $selfContainedOutput
if ($LASTEXITCODE -ne 0) {
    throw "Self-contained publish failed with exit code $LASTEXITCODE."
}

& dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained false `
    --no-restore `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $frameworkDependentOutput
if ($LASTEXITCODE -ne 0) {
    throw "Framework-dependent publish failed with exit code $LASTEXITCODE."
}

Assert-SingleExecutable $selfContainedOutput
Assert-SingleExecutable $frameworkDependentOutput

Compress-Archive `
    -LiteralPath (Join-Path $selfContainedOutput "WindowsTrayTranslator.exe") `
    -DestinationPath $selfContainedArchive `
    -CompressionLevel Optimal
Compress-Archive `
    -LiteralPath (Join-Path $frameworkDependentOutput "WindowsTrayTranslator.exe") `
    -DestinationPath $frameworkDependentArchive `
    -CompressionLevel Optimal

Write-Host ""
Write-Host "Created Windows x64 release packages:"
Get-FileHash -Algorithm SHA256 $selfContainedArchive, $frameworkDependentArchive |
    Select-Object Path, Hash |
    Format-Table -AutoSize
