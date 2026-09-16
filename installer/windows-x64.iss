#ifndef SourceDir
  #error SourceDir must be supplied by the build script.
#endif

#ifndef OutputDir
  #error OutputDir must be supplied by the build script.
#endif

#ifndef AppVersion
  #error AppVersion must be supplied by the build script.
#endif

#ifndef OutputBaseFilename
  #define OutputBaseFilename "WindowsTrayTranslator-Setup"
#endif

#define AppName "Windows Tray Translator"
#define AppExeName "WindowsTrayTranslator.exe"

[Setup]
AppId={{2B8D2DAF-ED3D-4453-8A79-8F72048483E4}
AppName={#AppName}
AppVersion={#AppVersion}
UninstallDisplayName={#AppName}
DefaultDirName={localappdata}\Programs\WindowsTrayTranslator
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile=..\src\WindowsTrayTranslator\Assets\app-icon.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousTasks=yes

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕 화면에 바로가기 만들기"; GroupDescription: "추가 작업을 선택하세요:"; Flags: unchecked
Name: "startup"; Description: "Windows에 로그인할 때 자동으로 실행하기"; GroupDescription: "추가 작업을 선택하세요:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WindowsTrayTranslator"; ValueData: """{app}\{#AppExeName}"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; Description: "설치가 끝나면 {#AppName} 실행"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    { An upgrade must also remove a previously selected startup entry when the
      user clears the task on this run. }
    if WizardIsTaskSelected('startup') then
      RegWriteStringValue(
        HKCU,
        'Software\Microsoft\Windows\CurrentVersion\Run',
        'WindowsTrayTranslator',
        '"' + ExpandConstant('{app}\{#AppExeName}') + '"')
    else
      RegDeleteValue(
        HKCU,
        'Software\Microsoft\Windows\CurrentVersion\Run',
        'WindowsTrayTranslator');
  end;
end;
