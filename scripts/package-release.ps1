param([switch]$NoRestore)

$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$version = & (Join-Path $PSScriptRoot "Get-ReleaseVersion.ps1")
$publishDirectory = Join-Path $repositoryRoot "artifacts\publish\windows-x64-self-contained"
$releaseDirectory = Join-Path $repositoryRoot "artifacts\release\$version"

if (Test-Path -LiteralPath $releaseDirectory) {
    Remove-Item -LiteralPath $releaseDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null

& (Join-Path $PSScriptRoot "publish.ps1") -OutputDirectory $publishDirectory -NoRestore:$NoRestore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$portable = Join-Path $releaseDirectory "WindowsTrayTranslator-Portable-$version-x64.zip"
Compress-Archive `
    -LiteralPath (Join-Path $publishDirectory "WindowsTrayTranslator.exe") `
    -DestinationPath $portable `
    -CompressionLevel Optimal

$installer = & (Join-Path $PSScriptRoot "build-installer.ps1") `
    -SourceDirectory $publishDirectory `
    -OutputDirectory $releaseDirectory `
    -Version $version

$releaseAssets = @($installer, $portable) | ForEach-Object { Get-Item -LiteralPath $_ }
$checksumFile = Join-Path $releaseDirectory "SHA256SUMS.txt"
$checksumLines = foreach ($asset in $releaseAssets | Sort-Object Name) {
    $hash = (Get-FileHash -LiteralPath $asset.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($asset.Name)"
}
Set-Content -LiteralPath $checksumFile -Value $checksumLines -Encoding ascii

Write-Host "Release assets created from Version ${version}:"
Get-Item -LiteralPath $installer, $portable, $checksumFile |
    Select-Object Name, Length |
    Format-Table -AutoSize
