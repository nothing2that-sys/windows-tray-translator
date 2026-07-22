@echo off
setlocal

dotnet publish src\WindowsTrayTranslator\WindowsTrayTranslator.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:PublishTrimmed=false ^
  -o artifacts\publish\win-x64

if errorlevel 1 exit /b %errorlevel%

echo Published to artifacts\publish\win-x64
