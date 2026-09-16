$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solution = Join-Path $repositoryRoot "WindowsTrayTranslator.sln"

& dotnet test $solution -c Release --no-build --logger "console;verbosity=normal"
exit $LASTEXITCODE
