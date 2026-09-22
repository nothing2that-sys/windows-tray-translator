param(
    [string]$OutputDirectory = "",
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$project = Join-Path $repositoryRoot "src\WindowsTrayTranslator\WindowsTrayTranslator.csproj"
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot "artifacts\publish\windows-x64-self-contained"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)

$prefix = $repositoryRoot.TrimEnd('\') + '\'
if (-not $OutputDirectory.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must be inside the repository: $OutputDirectory"
}

if (-not $NoRestore) {
    & dotnet restore $project -r win-x64
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if (Test-Path -LiteralPath $OutputDirectory) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

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
    -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$entries = @(Get-ChildItem -LiteralPath $OutputDirectory -Force)
if ($entries.Count -ne 1 -or $entries[0].Name -ne "WindowsTrayTranslator.exe") {
    throw "Single-file publish verification failed. Found: $($entries.Name -join ', ')"
}
