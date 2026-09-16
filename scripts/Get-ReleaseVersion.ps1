$ErrorActionPreference = "Stop"

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$versionFile = Join-Path $repositoryRoot "Directory.Build.props"
[xml]$properties = Get-Content -LiteralPath $versionFile
$version = [string]$properties.Project.PropertyGroup.Version

if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Directory.Build.props does not contain a valid semantic Version: '$version'"
}

$version
