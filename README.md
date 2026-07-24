# Windows Tray Translator

.NET 8 WinForms 기반의 Windows 트레이 상주형 Gemini 번역 애플리케이션입니다.

처음 사용하는 경우 UI 화면과 단계별 설명을 포함한 [사용자 매뉴얼](doc/WindowsTrayTranslator_UserManual.html)을 참고하세요.

Phase 1~7 구현을 완료했습니다. 트레이 실행, 번역과 안전한 자동 치환, 통합 설정 UI, 단축키 즉시 재등록, Windows 시작 프로그램 등록, self-contained 단일 파일 배포를 제공합니다.

## 요구 환경

- 개발: Windows 10/11 x64, .NET SDK 9 이상
- 실행: Windows 10/11 x64
- 대상 런타임: .NET 8

## 기본 단축키

- 읽기 번역: `Alt+R`
- 작성 번역: `Alt+T`

`Alt+R`은 선택 문자열을 Gemini로 번역하여 커서 근처 팝업에 표시합니다. `Alt+T`는 선택 문자열을 작성 대상 언어로 번역한 뒤 같은 선택 영역을 교체합니다. 두 단축키는 설정의 `번역` 탭에서 각각 변경할 수 있으며 저장 즉시 적용됩니다.

작성 결과 형식은 설정의 `번역` 탭에서 다음 값 중 하나로 지정합니다.

- `TranslationOnly`: 번역문만
- `TranslationWithOriginal`: 번역문(원문)
- `OriginalWithTranslation`: 원문(번역문)
- `TranslationAndOriginalOnNewLine`: 번역문 다음 줄에 (원문)

번역 중 활성 창이나 포커스가 바뀌면 자동 치환하지 않습니다. 결과는 경고 팝업에 표시되며, 사용자가 `복사`를 누른 경우에만 클립보드에 기록됩니다.

기본적으로 자동 `Ctrl+C`로 먼저 가져오며, 지원하지 않는 앱에서는 Windows UI Automation으로 한 번 더 시도합니다. 설정에서 획득 우선순위를 바꿀 수 있습니다.

## 설정

트레이 아이콘을 더블클릭하거나 우클릭 후 `설정 열기`를 선택합니다.

- 일반: 번역 활성화, Windows 자동 시작, 읽기·작성별 팝업 자동 닫기, 로그 설정
- 번역: 대상 언어, 작성 결과 형식, 읽기·작성 단축키, 클립보드 설정
- API: Gemini 키와 모델, 모델 목록 조회, 보조 모델, 요청 제한 시간, 재시도, 연결 테스트

새 단축키가 다른 프로그램과 충돌하면 저장하지 않고 기존 두 단축키를 복구합니다. `Windows 시작 시 자동 실행`은 현재 사용자 계정의 시작 프로그램에 등록되며 설정을 해제하면 제거됩니다.

트레이 메뉴의 `번역 히스토리`에서는 최근 읽기·작성 번역의 시간, 대상 언어, 원문과 번역문을 확인하고 번역문을 다시 복사할 수 있습니다. 히스토리는 로그 기록이 활성화된 동안 로그 보존 기간에 맞춰 일자별로 저장됩니다.

읽기 대상 언어는 `Select`, `Auto`, 한국어, 영어, 일본어, 중국어(간체), 베트남어를 제공하고, 작성 대상 언어는 `Select`와 각 개별 언어를 제공합니다. 읽기에서 `Auto`를 설정하면 한국어 원문은 영어로, 그 외 언어는 한국어로 번역합니다. `Select`를 설정하면 단축키를 누를 때 포커스를 빼앗지 않는 언어 선택 팝업이 나타나며, 고른 언어로 번역을 진행합니다. 작성 결과 형식은 설정 화면의 한글 설명과 예시를 보고 선택할 수 있습니다.

`Select` 팝업은 읽기·작성별 최근 언어를 맨 위에 표시합니다. 숫자키 `1~5`, 위·아래 방향키와 Enter로 선택할 수 있고 Esc로 취소할 수 있습니다.

번역 탭에서는 다음 품질 옵션을 제공합니다.

- Natural, Literal, Business 번역 방식
- 원문 말투 유지, 격식·존댓말, 친근한 말투
- 번역 말투·문체·용어·출력 형식을 자유롭게 적는 추가 자연어 지침
- 사용자 용어집 (`원문=번역문`, 한 줄에 하나)
- 번역하지 않고 그대로 유지할 단어

API 탭의 `모델 목록 새로고침`은 현재 API 키에서 `generateContent`를 지원하는 모델만 불러옵니다. 종료된 1.5·2.0 모델은 저장을 차단하며, 선택적으로 기본 모델의 429·서버 오류·사용 불가 상황에서 보조 모델을 호출할 수 있습니다.

네트워크, 시간 초과, 서버 오류 또는 잘못된 응답이 번역 작업 사이에서 3회 연속 발생하면 Gemini 통신 객체를 내부적으로 다시 만들고 현재 번역을 한 번 자동 재시도합니다. 인증, 사용량 제한, 모델 및 설정 오류는 재초기화해도 해결되지 않으므로 이 복구 대상에서 제외합니다.

## API 키 설정

1. [Google AI Studio](https://aistudio.google.com/app/apikey)에서 Gemini API 키를 발급합니다.
2. 프로그램을 실행하고 트레이 메뉴에서 `설정 열기`를 선택합니다.
3. `API` 탭에 키와 모델명을 입력합니다. 번역용 기본 모델은 `gemini-3.5-flash-lite`입니다.
4. `API 연결 테스트`로 확인한 뒤 `저장`을 누릅니다.

API 키는 설정 JSON에 저장되지 않습니다. `%LOCALAPPDATA%\WindowsTrayTranslator\secrets.dat`에 Windows DPAPI CurrentUser 범위로 암호화하여 저장합니다. 일반 운영 로그에는 선택 문자열과 번역 결과를 기록하지 않지만, 로그 기록이 활성화된 경우 번역 히스토리 기능을 위해 원문과 번역문을 `logs\history-날짜.jsonl`에 저장합니다.

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
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o artifacts/publish/win-x64
```

또는 `scripts\publish-windows-x64.bat`을 실행합니다. 스크립트는 기존 출력 폴더를 정리한 다음 self-contained 단일 EXE인지 검사하고 SHA-256 해시를 출력합니다.

런타임 포함·미포함 패키지를 한 번에 만들려면 다음 스크립트를 실행합니다.

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish-windows-x64-all.ps1
```

생성 위치:

```text
dist\windows-x64\
├── WindowsTrayTranslator-win-x64-self-contained.zip
├── WindowsTrayTranslator-win-x64-framework-dependent.zip
├── README.md
└── SHA256SUMS.txt
```

- `self-contained`: .NET 런타임 포함. 별도 런타임 설치 없이 실행할 수 있습니다.
- `framework-dependent`: .NET 8 Desktop Runtime 미포함. 파일은 작지만 대상 PC에 해당 런타임이 필요합니다.

최종 산출물:

```text
artifacts\publish\win-x64\WindowsTrayTranslator.exe
```

이 EXE에는 .NET 8 Windows x64 런타임이 포함됩니다. 배포 전 실제 프로그램별 검증은 [Phase 7 수동 검증 체크리스트](doc/Phase7_ManualTestChecklist.md)를 사용합니다.

## 런타임 데이터

```text
%LOCALAPPDATA%\WindowsTrayTranslator\
├── config\appsettings.json
├── secrets.dat
└── logs\
```

## 현재 제한사항

- 같은 최상위 창 안에서 자체 렌더링된 웹 입력 요소만 바뀌는 경우에는 포커스 변경을 완벽하게 식별하지 못할 수 있습니다.
- 일부 지연 렌더링 또는 앱 전용 클립보드 형식은 안전하게 백업할 수 없습니다. 이 경우 원본 보호를 위해 캡처를 취소합니다.
- 현재 프로그램보다 높은 권한으로 실행된 프로그램에는 `SendInput`이 차단될 수 있습니다.
- 관리자 권한 대상은 가능한 경우 번역 전에 감지해 클립보드와 선택 영역을 변경하지 않습니다. Windows가 권한 조회를 허용하지 않는 예외적인 경우에는 `SendInput` 실패 안내가 최종 보호 수단입니다.
