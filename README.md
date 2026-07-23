# Windows Tray Translator

.NET 8 WinForms 기반의 Windows 트레이 상주형 Gemini 번역 애플리케이션입니다.

현재 구현 범위는 Phase 1~4입니다. 트레이 실행, 중복 실행 방지, 전역 단축키, 선택 문자열 캡처, 클립보드 복원, DPAPI API 키 저장, Gemini 번역, 읽기 결과 팝업까지 구현되어 있습니다. 선택 영역 자동 치환은 Phase 5에서 연결합니다.

## 요구 환경

- 개발: Windows 10/11 x64, .NET SDK 9 이상
- 실행: Windows 10/11 x64
- 대상 런타임: .NET 8

## 기본 단축키

- 읽기 번역: `Alt+R`
- 작성 번역용 캡처: `Alt+T`

`Alt+R`은 선택 문자열을 Gemini로 번역하여 커서 근처 팝업에 표시합니다. `Alt+T`는 Phase 5 전까지 선택 문자열 캡처만 확인합니다. 단축키 설정 UI는 Phase 6에서 추가되며, 현재는 `%LOCALAPPDATA%\WindowsTrayTranslator\config\appsettings.json`에서 두 값을 각각 변경할 수 있습니다.

선택 문자열은 자동 `Ctrl+C`로 먼저 가져오며, 지원하지 않는 앱에서는 Windows UI Automation으로 한 번 더 시도합니다.

## API 키 설정

1. 프로그램을 실행합니다.
2. 트레이 아이콘을 더블클릭하거나 우클릭 후 `설정 열기`를 선택합니다.
3. Gemini API 키와 모델명을 입력하고 `저장`을 누릅니다.
4. `API 연결 테스트`를 눌러 확인합니다.

API 키는 설정 JSON에 저장되지 않습니다. `%LOCALAPPDATA%\WindowsTrayTranslator\secrets.dat`에 Windows DPAPI CurrentUser 범위로 암호화하여 저장합니다. 선택 문자열과 번역 결과는 로그에 기록하지 않습니다.

선택 문자열은 Gemini API로 전송됩니다. 회사 기밀, 개인정보 또는 전송이 금지된 내용을 번역하지 마십시오.

## 개발 검증

```powershell
dotnet restore WindowsTrayTranslator.sln
dotnet build WindowsTrayTranslator.sln -c Release --no-restore
dotnet test WindowsTrayTranslator.sln -c Release --no-build
```

실제 Gemini 호출은 자동 테스트에서 수행하지 않습니다. 설정 화면의 연결 테스트에서 사용자가 직접 입력한 키로만 수행합니다.

## 배포

```powershell
dotnet publish src/WindowsTrayTranslator/WindowsTrayTranslator.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishTrimmed=false `
  -o artifacts/publish/win-x64
```

또는 `scripts\publish-windows-x64.bat`을 실행합니다.

## 런타임 데이터

```text
%LOCALAPPDATA%\WindowsTrayTranslator\
├── config\appsettings.json
├── secrets.dat
└── logs\
```

## 현재 제한사항

- 작성 번역 자동 치환은 아직 연결되지 않았습니다.
- 일부 지연 렌더링 또는 앱 전용 클립보드 형식은 안전하게 백업할 수 없습니다. 이 경우 원본 보호를 위해 캡처를 취소합니다.
- 현재 프로그램보다 높은 권한으로 실행된 프로그램에는 `SendInput`이 차단될 수 있습니다.
