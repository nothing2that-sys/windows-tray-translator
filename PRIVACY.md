# Privacy

Windows Tray Translator는 개발자가 운영하는 별도 중계 서버를 사용하지 않습니다. 사용자가 번역을 실행하면 선택한 문자열과 번역 설정이 사용자가 지정한 API Key를 이용해 Google Gemini API로 직접 전송됩니다. Gemini API 사용에는 Google의 해당 서비스 약관과 개인정보처리방침이 적용됩니다.

## 로컬 저장 데이터

모든 앱 데이터는 기본적으로 `%LOCALAPPDATA%\WindowsTrayTranslator` 아래에 저장되며 설치 파일과 분리됩니다.

| 데이터 | 위치 | 내용과 보존 |
|---|---|---|
| 설정 | `config\appsettings.json` | 언어, 단축키, 모델, 기록 옵션 등. 사용자가 삭제할 때까지 유지 |
| API Key | `secrets.dat` | Windows DPAPI `CurrentUser` 범위로 암호화. 설정 화면에서 삭제 가능 |
| 진단 로그 | `logs\translator-YYYY-MM-DD.log` | 상태, 오류 종류, 응답 코드, 문자 수, 수행 시간. 번역 원문과 결과는 기록하지 않음. 기본 14일 |
| 번역 히스토리 | `history\history-YYYY-MM-DD.jsonl` | 기능을 명시적으로 켠 경우에만 원문과 번역 결과를 저장. 기본 OFF, 기본 보존 30일 |

진단 로그와 번역 히스토리는 서로 독립적으로 켜고 끌 수 있습니다. 보존 기간이 지난 일자별 파일은 앱이 시작되거나 관련 기능을 사용할 때 삭제됩니다.

1.1.x에서 `logs` 폴더에 저장된 기존 `history-*.jsonl`은 1.2.0을 처음 시작할 때 새 `history` 폴더로 이동합니다. 이동할 수 없는 경우 원본 파일을 삭제하지 않습니다.

## 데이터 삭제

- API Key: 설정의 `저장된 API 키 삭제`를 사용합니다.
- 진단 로그 또는 히스토리: 앱을 종료한 뒤 위 폴더의 해당 하위 폴더를 삭제합니다.
- 모든 로컬 데이터: 앱을 종료하고 `%LOCALAPPDATA%\WindowsTrayTranslator` 폴더를 삭제합니다.

제거 프로그램은 업그레이드 및 재설치 시 설정을 보존하기 위해 사용자 데이터 폴더를 자동 삭제하지 않습니다.

## 민감한 정보

개인정보, 회사 기밀, 인증 정보 또는 외부 API 전송이 금지된 문자열을 번역하지 마십시오. 선택 문자열은 번역을 위해 Google에 전송될 수 있으며, Google 측 데이터 처리 방식은 사용 중인 Gemini API 계정 및 정책에 따라 달라질 수 있습니다.

Privacy 관련 보안 문제는 [SECURITY.md](SECURITY.md)의 비공개 신고 절차를 사용해 주세요.
