# Windows Tray Translator

Windows에서 텍스트를 선택한 뒤 단축키만 누르면 Gemini로 즉시 번역하거나 선택한 문장을 번역문으로 교체할 수 있는 Windows Tray Utility입니다.

처음 사용하는 경우 UI 화면과 단계별 설명을 포함한 [사용자 매뉴얼](doc/WindowsTrayTranslator_UserManual.html)을 참고하세요.

트레이 실행, 번역과 안전한 자동 치환, 통합 설정 UI, 단축키 즉시 재등록, Windows 시작 프로그램 등록, self-contained 단일 파일 배포를 제공합니다.

## 요구 환경

- 개발: Windows 11 x64 또는 Microsoft가 .NET 10을 지원하는 Windows 환경, .NET 10 SDK (`global.json` 참조)
- 실행: Windows 11 x64. Windows 10은 Microsoft의 [.NET supported OS policy](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)에 포함된 edition만 지원 후보이며 RC 수동 검증이 필요합니다.
- 대상 런타임: .NET 10

## 기본 단축키

- 읽기 번역: `Alt+R`
- 작성 번역: `Alt+T`
- Quick Action: `Ctrl+Alt+A`

`Alt+R`은 선택 문자열을 Gemini로 번역하여 커서 근처 팝업에 표시합니다. `Alt+T`는 선택 문자열을 작성 대상 언어로 번역한 뒤 같은 선택 영역을 교체합니다. 세 단축키는 설정의 `번역` 탭에서 각각 변경할 수 있으며 저장 즉시 적용됩니다.

`Ctrl+Alt+A`는 선택 문자열을 먼저 가져온 뒤 커서 근처에 작은 Quick Action 창을 띄웁니다. 창에서 `자연스럽게 다듬기`, `요약`, `번역 후 요약` 중 하나를 마우스로 고르면 결과를 팝업으로 보여 주고 복사할 수 있습니다. Quick Action은 선택 영역을 교체하지 않고, 번역 히스토리에도 저장하지 않습니다. 창은 8초 후 자동으로 닫히며 마우스를 올려 두면 닫히지 않습니다. 이 단축키는 비워 두면 비활성화되고, 다른 프로그램과 충돌하면 Quick Action만 사용할 수 없게 되며 `Alt+R`과 `Alt+T`는 그대로 동작합니다.

`요약`은 원문과 같은 언어로 요약합니다. `번역 후 요약`은 선택 문장을 `읽기 대상 언어`로 먼저 번역한 다음 그 번역문을 요약합니다. 두 단계를 나눠 실행하므로 한 번의 호출로 번역과 요약을 동시에 시키는 것보다 내용이 덜 날아갑니다. 읽기 대상 언어가 `Auto`면 한국어 우세 문장은 영어로, 그 외는 한국어로 요약하고, 한국어 문장을 한국어로 요약할 때는 불필요한 번역 단계를 건너뜁니다. 읽기 대상 언어가 `Select`면 팔레트에서는 언어 선택 창을 다시 띄우지 않고 최근에 고른 읽기 언어를 씁니다. 세 작업은 설정의 `API` 탭에서 번역과 다른 모델로 돌릴 수 있습니다.

비밀번호 입력란처럼 보호된 입력 영역이 확인되면 클립보드에 접근하거나 Gemini로 전송하지 않고 안내만 표시합니다. 다만 모든 프로그램이 이 정보를 제공하지는 않으므로 완전한 차단을 보장하지는 않습니다.

작성 결과 형식은 설정의 `번역` 탭에서 다음 값 중 하나로 지정합니다.

- `TranslationOnly`: 번역문만
- `TranslationWithOriginal`: 번역문(원문)
- `OriginalWithTranslation`: 원문(번역문)
- `TranslationAndOriginalOnNewLine`: 번역문 다음 줄에 (원문)

번역 중 활성 창이나 포커스가 바뀌면 자동 치환하지 않습니다. 결과는 경고 팝업에 표시되며, 사용자가 `복사`를 누른 경우에만 클립보드에 기록됩니다.

기본적으로 자동 `Ctrl+C`로 먼저 가져오며, 지원하지 않는 앱에서는 Windows UI Automation으로 한 번 더 시도합니다. 설정에서 획득 우선순위를 바꿀 수 있습니다.

## 설정

트레이 아이콘을 더블클릭하거나 우클릭 후 `설정 열기`를 선택합니다.

- 일반: 번역 활성화, Windows 자동 시작, 읽기·작성별 팝업 자동 닫기, 진단 로그와 번역 히스토리 설정
- 번역: 대상 언어, 작성 결과 형식, 읽기·작성·Quick Action 단축키, 클립보드 설정
- API: Gemini 키와 모델, 모델 목록 조회, Quick Action 전용 모델과 제한 시간, 보조 모델, 요청 제한 시간, 재시도, 연결 테스트

요약은 번역보다 큰 모델을 요구합니다. 경량 모델은 문장을 단어로 줄이고 핵심을 빼는 경우가 있어, `API` 탭의 `Quick Action에 다른 모델 사용`을 켜면 요약·다듬기·번역 후 요약만 상위 모델로 실행합니다. `Alt+R`과 `Alt+T` 번역은 기존 모델을 그대로 쓰며, 이 모델은 보조 모델로 전환되지 않습니다.

상위 모델은 요약 한 번에 10초에서 45초까지 걸리고 혼잡할 때 503을 돌려줍니다. 그래서 Quick Action은 번역과 별도의 제한 시간(기본 90초, `API` 탭에서 변경)을 씁니다. 재시도는 시도마다 제한 시간을 각각 받으므로 첫 시도가 느려도 다음 시도가 바로 죽지 않습니다. 서버가 혼잡해 실패한 경우에는 시간 초과가 아니라 혼잡 사실을 그대로 알려 줍니다.

새 단축키가 다른 프로그램과 충돌하면 저장하지 않고 기존 단축키를 복구합니다. 시작할 때 충돌이 발생한 경우에는 성공한 단축키를 그대로 유지하고 실패한 단축키만 알림으로 안내합니다. `Windows 시작 시 자동 실행`은 현재 사용자 계정의 시작 프로그램에 등록되며 설정을 해제하면 제거됩니다.

트레이 메뉴의 `번역 히스토리`에서는 최근 읽기·작성 번역의 시간, 대상 언어, 원문과 번역문을 확인하고 번역문을 다시 복사할 수 있습니다. 히스토리는 기본적으로 꺼져 있으며, 사용자가 `번역 히스토리 저장`을 명시적으로 켠 경우에만 별도 `history` 폴더에 보존됩니다.

읽기 대상 언어는 `Select`, `Auto`, 한국어, 영어, 일본어, 중국어(간체), 베트남어를 제공하고, 작성 대상 언어는 `Select`와 각 개별 언어를 제공합니다. 읽기에서 `Auto`를 설정하면 앱이 기술 식별자를 제외한 자연어 단어의 비중으로 원문의 주 언어를 판정하여 한국어 우세 문장은 영어로, 그 외 문장은 한국어로 번역합니다. 일부 단어나 인용구만 다른 언어인 혼합 문장도 전체 문맥을 유지한 채 대상 언어로 번역합니다. `Select`를 설정하면 단축키를 누를 때 포커스를 빼앗지 않는 언어 선택 팝업이 나타나며, 고른 언어로 번역을 진행합니다. 작성 결과 형식은 설정 화면의 한글 설명과 예시를 보고 선택할 수 있습니다.

`Select` 팝업은 읽기·작성별 최근 언어를 맨 위에 표시합니다. 숫자키 `1~5`, 위·아래 방향키와 Enter로 선택할 수 있고 Esc로 취소할 수 있습니다.

번역 탭에서는 다음 품질 옵션을 제공합니다.

- Natural, Literal, Business 번역 방식
- 원문 말투 유지, 격식·존댓말, 친근한 말투
- 번역 말투·문체·용어·출력 형식을 자유롭게 적는 추가 자연어 지침
- 사용자 용어집 (`원문=번역문`, 한 줄에 하나)
- 번역하지 않고 그대로 유지할 단어

API 탭의 `모델 목록 새로고침`은 현재 API 키에서 `generateContent`를 지원하는 모델만 불러옵니다. 종료된 1.5·2.0 모델은 저장을 차단합니다. 보조 모델을 설정하면 읽기·작성 번역의 진행 팝업에서 기본 요청을 취소하고 보조 모델로 즉시 전환할 수 있으며, 기본 요청이 실패한 경우에도 보조 모델로 다시 시도할 수 있습니다. `번역 완료 후 다른 모델로 다시 번역할 수 있게 표시`를 켠 경우에만 완료 팝업에도 재번역 버튼이 나타납니다.

## API 키 설정

1. [Google AI Studio](https://aistudio.google.com/app/apikey)에서 Gemini API 키를 발급합니다.
2. 프로그램을 실행하고 트레이 메뉴에서 `설정 열기`를 선택합니다.
3. `API` 탭에 키와 모델명을 입력합니다. 번역용 기본 모델은 `gemini-3.5-flash-lite`입니다.
4. `API 연결 테스트`로 확인한 뒤 `저장`을 누릅니다.

API 키는 설정 JSON에 저장되지 않습니다. `%LOCALAPPDATA%\WindowsTrayTranslator\secrets.dat`에 Windows DPAPI CurrentUser 범위로 암호화하여 저장합니다. 진단 로그에는 선택 문자열과 번역 결과를 기록하지 않습니다. 번역 원문과 결과는 사용자가 히스토리를 켠 경우에만 `history\history-날짜.jsonl`에 저장됩니다. 자세한 내용은 [PRIVACY.md](PRIVACY.md)를 확인하세요.

선택 문자열은 Gemini API로 전송됩니다. 회사 기밀, 개인정보 또는 전송이 금지된 내용을 번역하지 마십시오.

## 개발 검증

```powershell
dotnet restore WindowsTrayTranslator.sln
dotnet build WindowsTrayTranslator.sln -c Release --no-restore
dotnet test WindowsTrayTranslator.sln -c Release --no-build
```

실제 Gemini 호출은 자동 테스트에서 수행하지 않습니다. 설정 화면의 연결 테스트에서 사용자가 직접 입력한 키로만 수행합니다.

## 배포

Inno Setup 6가 설치된 Windows에서 아래 canonical scripts를 실행합니다.

```powershell
./scripts/build.ps1
./scripts/test.ps1
./scripts/package-release.ps1 -NoRestore
```

`Directory.Build.props`의 Version을 기준으로 `artifacts\release\<version>` 아래에 공개용 파일 3개를 생성합니다.

```text
WindowsTrayTranslator-Setup-1.3.1-x64.exe
WindowsTrayTranslator-Portable-1.3.1-x64.zip
SHA256SUMS.txt
```

Portable ZIP과 Installer는 .NET 10 Windows x64 Runtime을 포함합니다. framework-dependent 산출물은 Public Release 기본 대상에서 제외합니다.

설치 마법사의 `추가 작업` 화면에서 사용자가 다음 항목을 각각 선택할 수 있습니다.

- 바탕 화면에 바로가기 만들기
- Windows에 로그인할 때 자동으로 실행하기

사용자별 고정 설치 위치는
`%LOCALAPPDATA%\Programs\WindowsTrayTranslator`이며 관리자 권한이 필요하지 않습니다.
설정, API 키, 로그 및 번역 히스토리는 설치 폴더와 분리된
`%LOCALAPPDATA%\WindowsTrayTranslator`에 유지됩니다.

현재 자동 생성되는 설치 파일에는 코드 서명 인증서가 적용되지 않습니다. 배포 전 실제 프로그램별 검증은 [수동 검증 체크리스트](doc/Phase7_ManualTestChecklist.md)를 사용합니다.

## 런타임 데이터

```text
%LOCALAPPDATA%\WindowsTrayTranslator\
├── config\appsettings.json
├── secrets.dat
├── logs\
└── history\
```

## 현재 제한사항

- 같은 최상위 창 안에서 자체 렌더링된 웹 입력 요소만 바뀌는 경우에는 포커스 변경을 완벽하게 식별하지 못할 수 있습니다.
- 일부 지연 렌더링 또는 앱 전용 클립보드 형식은 안전하게 백업할 수 없습니다. 이 경우 원본 보호를 위해 캡처를 취소합니다.
- 현재 프로그램보다 높은 권한으로 실행된 프로그램에는 `SendInput`이 차단될 수 있습니다.
- 관리자 권한 대상은 가능한 경우 번역 전에 감지해 클립보드와 선택 영역을 변경하지 않습니다. Windows가 권한 조회를 허용하지 않는 예외적인 경우에는 `SendInput` 실패 안내가 최종 보호 수단입니다.

## Public release

공개 전환과 독립 검증이 끝난 뒤 GitHub Releases에는 self-contained Installer, Portable ZIP, `SHA256SUMS.txt`만 제공합니다. 현재 설치 프로그램은 코드 서명되지 않아 Windows SmartScreen 경고가 표시될 수 있습니다.

Version `1.3.1`의 canonical packaging 명령은 다음과 같습니다.

```powershell
./scripts/build.ps1
./scripts/test.ps1
./scripts/package-release.ps1 -NoRestore
```

`v1.3.1` 형식의 tag workflow는 tag와 `Directory.Build.props` version의 일치를 확인하고 Draft Release를 생성합니다. Repository Public 전환과 Release 공개 게시는 별도 승인 작업입니다.

## Security and license

취약점은 공개 Issue 대신 [SECURITY.md](SECURITY.md)의 비공개 절차로 신고해 주세요. 이 프로젝트는 [MIT License](LICENSE)로 배포되며 외부 구성 요소는 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)를 참고하세요.
