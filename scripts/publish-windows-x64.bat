@echo off
setlocal

pushd "%~dp0.."
if not exist "WindowsTrayTranslator.sln" (
  echo ERROR: Repository root could not be found.
  popd
  exit /b 1
)

set "OUTPUT=%CD%\artifacts\publish\win-x64"
if exist "%OUTPUT%" rmdir /s /q "%OUTPUT%"

dotnet publish src\WindowsTrayTranslator\WindowsTrayTranslator.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:PublishTrimmed=false ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "%OUTPUT%"

if errorlevel 1 (
  popd
  exit /b 1
)

if not exist "%OUTPUT%\WindowsTrayTranslator.exe" (
  echo ERROR: Published executable was not found.
  popd
  exit /b 1
)

set "FILE_COUNT=0"
for /f %%C in ('dir /b /a "%OUTPUT%" ^| find /c /v ""') do set "FILE_COUNT=%%C"
if not "%FILE_COUNT%"=="1" (
  echo ERROR: Single-file verification failed. Found %FILE_COUNT% entries:
  dir /b /a "%OUTPUT%"
  popd
  exit /b 1
)

echo Published and verified single-file executable:
echo %OUTPUT%\WindowsTrayTranslator.exe
certutil -hashfile "%OUTPUT%\WindowsTrayTranslator.exe" SHA256

popd
exit /b 0
