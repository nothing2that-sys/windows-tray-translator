param([switch]$NoRestore)

$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solution = Join-Path $repositoryRoot "WindowsTrayTranslator.sln"

if (-not $NoRestore) {
    & dotnet restore $solution
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

& dotnet build $solution -c Release --no-restore
exit $LASTEXITCODE
