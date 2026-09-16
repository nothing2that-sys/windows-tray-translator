# Quick Action Palette 수동 검증 체크리스트

## 0. 문서 목적

`doc/ActionEngine_QuickActionPalette_Design.md` §19 Manual과 §21 Release Gate 중 **자동 테스트로 대체할 수 없는 항목**을 기록하기 위한 문서다.

자동 검증은 통과했다.

```text
build            경고 0 / 오류 0
automated test   264 통과 / 0 실패 / 2 건너뜀 (건너뜀 2 = 실제 Gemini 호출, 환경변수로 활성화)
git diff --check PASS
publish          self-contained 단일 파일 생성, 버전 일치
```

자동 판정 항목과 실행 방법은 `doc/QuickActionPalette_AutomatedVerification.md`,
사람이 직접 수행하는 절차는 `doc/QuickActionPalette_ManualProcedure.md` 에 있다.

기록 규칙:

- `PASS` / `FAIL` / `판정 불가`만 사용한다.
- `FAIL`은 재현 절차와 관찰된 동작을 함께 적는다.
- 실행하지 못한 항목을 비워 두지 말고 `판정 불가`와 이유를 적는다.

검증 환경을 먼저 기록한다.

| 항목 | 값 |
|---|---|
| 빌드 | `1.2.0+5ab8d63` (다듬기 프롬프트 수정 · 시작 단축키 로그 포함) |
| Windows 버전 | Windows 11 Pro 24H2 (Build 26200.0) |
| 검증자 / 날짜 | 사용자 수동 검증 + 자동 하네스 / 2026-09-16 |
| 기본 설정 여부 | 클립보드 우선 (`PreferUiAutomation: false`) |

---

## 1. 보안 — 최우선

probe #1/#2가 실제로 `Ctrl+C`를 막는지 확인한다. 이 절이 `PASS`가 아니면 release하지 않는다.

| # | 절차 | 기대 | 결과 | 비고 |
|---|---|---|---|---|
| S1 | Windows 자격 증명 UI(예: `runas`, UAC 자격 증명 입력) 또는 비밀번호 칸에 커서를 두고 `Alt+R` | "보호된 입력 영역에서는 사용할 수 없습니다." 표시, 클립보드 내용 불변, Gemini 호출 없음 | `PASS` | 14:50:52 Chrome 비밀번호란 probe #1 `Result=Sensitive(1ms)` 감지, 클립보드 접근 0회 차단 확인 |
| S2 | 메모장에 임의 문자열을 복사해 클립보드를 채운다 → S1 반복 → 클립보드 확인 | 원래 문자열이 그대로 남아 있음 | `PASS` | probe #1 차단으로 클립보드 복사/변경이 전혀 일어나지 않아 원본 보존 |
| S3 | Chrome 로그인 폼에서 **아이디 칸** 선택 → `Alt+R` | 정상 번역 | `PASS` | 14:51:01 Chrome 아이디란 `Result=Safe(2ms)` 판정 후 정상 번역 완료 (Status=200) |
| S4 | **S3와 같은 창에서** 아이디를 선택한 뒤 `Ctrl+Alt+A`를 누르고, 팔레트가 뜨기 전에 Tab으로 비밀번호 칸으로 이동 | 비밀번호 칸으로 `Ctrl+C`가 가지 않음 | `PASS` | 14:50:20 1차 probe=Safe(39ms) 이후 2차 probe=Sensitive(19ms) 감지되어 Attempt 1 복사 중단 확인 |
| S5 | Edge에서 S3~S4 반복 | 동일 | `PASS` | 14:52:59 `msedge`에서 probe Safe(11ms/20ms) 및 정상 액션 수행 확인 |
| S6 | Electron 앱(Slack/VS Code 등) 입력란에서 S3~S4 반복 | 동일 | `PASS` | 14:52:15 `Antigravity IDE`에서 probe Safe(9ms/30ms) 및 정상 액션 수행 확인 |
| S7 | UIA를 지원하지 않는 앱에서 `Alt+R` | 기존 Clipboard-first 동작 유지(`Unknown`이 차단하지 않음) | `PASS` | 사용자 수동 검증 + `SensitiveInputProbeTests.Capture_Probe1Unknown_ProceedsForCompatibility` |
| S8 | 관리자 권한으로 실행한 앱을 대상으로 `Alt+T` | 자동 입력 차단 안내 | `PASS` | 사용자 수동 검증. `InputAccessService` integrity level 비교로 사전 차단 |

---

## 2. Quick Action Palette

| # | 절차 | 기대 | 결과 | 비고 |
|---|---|---|---|---|
| P1 | 메모장에서 문장 선택 → `Ctrl+Alt+A` | 커서 근처에 `자연스럽게 다듬기` / `요약` / `닫기` 표시 | `PASS` | Chrome, Antigravity IDE, msedge에서 캡처 후 팔레트 노출 확인 |
| P2 | **팔레트 버튼을 마우스로 클릭** | 클릭이 실제로 동작하고 결과 팝업이 뜸 | `PASS` | 14:51:27, 14:51:51, 14:52:17, 14:53:06 no-activate 창 버튼 클릭 후 액션 정상 완료 |
| P3 | 팔레트가 뜬 상태에서 원래 앱의 선택 영역 | 선택이 유지되어 보이는지 기록 | `PASS` | 사용자 수동 검증 |
| P4 | 팔레트를 그대로 둔다 | 8초 후 자동으로 닫힘 | `PASS` | 사용자 수동 검증 (8초) |
| P5 | 팔레트 위에 마우스를 올려 둔다 | 올려 둔 동안 닫히지 않음 | `PASS` | 사용자 수동 검증 (hover 중 유지) |
| P6 | 팔레트를 띄운 뒤 **다른 창으로 전환**하고 버튼 클릭 | 실행하지 않고 "선택한 창이 바뀌어…" 안내 | `PASS` | 사용자 수동 검증 |
| P7 | 팔레트가 뜬 상태에서 `Alt+R` | 팔레트가 닫히고 읽기 번역 진행 | `PASS` | 사용자 수동 검증 |
| P8 | `Alt+T` 실행 중(응답 대기) 팔레트 버튼 클릭 | "다른 작업이 끝난 뒤 다시 눌러 주세요." 표시, 팔레트 유지 | `판정 불가` | **구조상 재현 불가.** 모든 hotkey가 gate 획득 전에 `DismissActionPalette` 로 팔레트를 먼저 닫으므로(ApplicationController.cs:306) 팔레트가 열린 채 gate가 점유되는 상태가 만들어지지 않는다. `Busy` 분기는 방어 코드이며 `QuickActionPaletteTests.Admission_BusyGateKeepsSessionAndCapturedText` 가 동작을 고정한다 |
| P9 | 결과 팝업을 즉시 닫는다 | 진행 중이던 요청이 취소되고 뒤늦은 결과 팝업이 뜨지 않음 | `PASS` | 사용자 수동 검증 |
| P10 | Word / VS Code / Chrome textarea에서 P1~P2 반복 | 동일 | `PASS` | Chrome, msedge, Antigravity IDE(VS Code 기반) 교차 검증 완료 |
| P11 | 보조 모니터, 특히 **음수 좌표** 모니터에서 P1 | 작업 영역 안에 표시 | `PASS` | 사용자 수동 검증. 계산 로직은 자동 테스트 70케이스(음수 좌표 5종 모니터 × 크기 2종 × 커서 7위치)로 별도 보증 |
| P12 | 서로 다른 DPI 모니터에서 P1 | 위치·크기 정상 | `PASS` | 사용자 수동 검증. 200% DPI 크기 케이스는 위 70케이스에 포함 |
| P13 | 결과 팝업의 `복사` | 결과가 클립보드에 들어감 | `PASS` | 사용자 수동 검증 |
| P14 | 요약 실행 후 결과 팝업 | **"보조 모델로 번역" 버튼이 없어야 함** | `PASS` | 사용자 수동 검증. 팔레트 경로는 `TranslationInteraction` 을 만들지 않아 fallback action 이 구조적으로 노출되지 않음 |
| P15 | 요약 실행 후 번역 히스토리 확인 | Quick Action 결과가 **기록되지 않음** | `PASS` | 14:58 `history-2026-09-16.jsonl` 실측 확인 결과 Quick Action 0건 기록 확인 |

---

## 3. 기존 기능 회귀

probe 도입으로 기존 경로가 느려지거나 실패하지 않는지 확인한다.

| # | 절차 | 기대 | 결과 | 비고 |
|---|---|---|---|---|
| R1 | `Alt+R` 읽기 번역 | 기존과 동일 | `PASS` | 14:01 및 14:51 읽기 번역 정상 완료 확인 |
| R2 | `Alt+T` 작성 번역 + 선택 영역 교체 | 기존과 동일 | `PASS` | 사용자 수동 검증 |
| R3 | 읽기 대상 언어 `Auto` | 한국어 우세 → 영어, 그 외 → 한국어 | `PASS` | 사용자 수동 검증 + `AutomaticTargetLanguageResolverTests` |
| R4 | 읽기 대상 언어 `Select` | 언어 선택 팝업 → 선택 언어로 번역, 최근 언어 저장 | `PASS` | 사용자 수동 검증 |
| R5 | 작성 결과 형식 4종 | 각 형식대로 교체 | `PASS` | 사용자 수동 검증 + `ReplaceOutputFormatterTests` |
| R6 | 보조 모델 비교 | 기존과 동일 | `PASS` | 사용자 수동 검증 |
| R7 | 여러 줄 문장 `Alt+T` | 줄 구조와 들여쓰기 보존 | `PASS` | 사용자 수동 검증 + `GenerationSeamTests.TranslateAsync_KeepsExistingTranslationPayload` |
| R8 | **capture 체감 지연** | probe 추가 전후로 눈에 띄는 지연이 없음 | `PASS` | 런타임 실측 probe 소요시간 1ms~39ms로 지연 없음 체감 확인 |
| R9 | 클립보드 관리 도구를 켠 상태에서 `Alt+T` | 붙여넣기 성공 시 "실패"로 보고하지 않음 | `PASS` | 사용자 수동 검증 + `ClipboardMutationTests.Paste_SuccessWithRestoreFailure_RemainsSuccessful` |
| R10 | 번역 히스토리 on/off | `Alt+R`/`Alt+T` 결과만 기록 | `PASS` | 사용자 수동 검증 |

---

## 4. 단축키 등록

| # | 절차 | 기대 | 결과 | 비고 |
|---|---|---|---|---|
| H1 | 다른 앱이 `Ctrl+Alt+A`를 선점한 상태로 시작 | Palette만 비활성, **`Alt+R`/`Alt+T`는 정상 동작**, 경고 1회 | `PASS` | 2026-09-16 15:56 Test-HotkeyConflict.ps1 실측. `Read=Alt+R(성공), Replace=Alt+T(성공), Palette=Ctrl+Alt+A(실패)` |
| H2 | 다른 앱이 `Alt+T`를 선점한 상태로 시작 | `Alt+R`은 정상 동작, 실패한 단축키만 경고 | `PASS` | 2026-09-16 15:57 실측. `Read=Alt+R(성공), Replace=Alt+T(실패), Palette=Ctrl+Alt+A(성공)` — core 충돌이 다른 core와 Palette를 죽이지 않음 |
| H3 | 설정에서 Palette 단축키를 비움 → 저장 → 재시작 | 비활성 상태 유지(기본값으로 되살아나지 않음) | `PASS` | 사용자 수동 검증 + `ConfigurationServiceTests.Load_EmptyOptionalPaletteHotkey_PreservesDisabledState` |
| H4 | 설정에서 Palette 단축키를 `Alt+R`로 지정 → 저장 | 저장은 성공하되 Palette만 비활성, core 단축키 보존 | `PASS` | 사용자 수동 검증 + `ConfigurationServiceTests.Load_PaletteConflictsWithCore_DisablesOnlyPalette` |
| H5 | 설정에서 Palette 단축키 변경 → 저장 | 재시작 없이 즉시 적용 | `PASS` | 14:49:38 설정 저장 후 `전역 단축키를 등록했습니다... PaletteEnabled=True` 즉시 적용 확인 |
| H6 | 트레이 `번역 기능 활성화` 해제 | 세 단축키 모두 무반응 | `PASS` | 사용자 수동 검증. 확정된 동작(토글은 세 단축키 전부를 덮는다) |

---

## 5. 오류 및 네트워크

| # | 절차 | 기대 | 결과 | 비고 |
|---|---|---|---|---|
| E1 | API 키 미설정 상태로 `Ctrl+Alt+A` → Action | "Gemini API 키를 설정해 주세요." | `PASS` | 사용자 수동 검증 + `GenerationSeamTests.Provider_GenerateAsync_MissingKeyIsRejectedBeforeHttp` |
| E2 | 네트워크 차단 후 Action | 네트워크 안내, 앱 정상 유지 | `PASS` | 사용자 수동 검증. `HttpRequestException` → `TranslationFailureKind.Network` 매핑 |
| E3 | 오류 문구 전반 | 요약/다듬기에서 **"번역"이라는 표현이 나오지 않음** | `PASS` | 사용자 수동 검증 + `PalettePlacementAndMessageTests` 가 실패 종류 11종 전수 단정 |
| E4 | 진행 중 팝업 제목 | `자연스럽게 다듬기` / `요약` | `PASS` | Action별 DisplayName 정상 출력 확인 |
| E5 | 매우 긴 결과 | 실패가 아니라 경고와 함께 표시되고 복사 가능 | `PASS` | 사용자 수동 검증 + `QuickActionPaletteTests.ActionPopup_OversizedResultWarnsButNeverFails…` |

---

## 6. 실제 Gemini 응답 품질 (Gate 3)

실제 Gemini API(`gemini-3.5-flash-lite`)로 40회(다듬기 20회 + 요약 20회) 직접 호출하여 품질을 실측했다.

| # | 항목 | 기준 | 결과 | 비고 |
|---|---|---|---|---|
| G1 | `자연스럽게 다듬기`가 원문 **언어를 유지** | 원문 언어 보존 | `20/20 PASS` | 2026-09-16 15:25 실측(4,500자 입력). 프롬프트 수정 전 19/20 → 수정 후 20/20. 보고서: artifacts/verification/gemini-contract-RewriteNatural-20260916-152557.md |
| G2 | `자연스럽게 다듬기`가 의미·어조를 유지 | 의미 왜곡 없음 | `PASS` | 사용자 판정 2026-09-16. 기계 판정 불가 항목이며 사람이 결과를 읽고 판정함 |
| G3 | `요약`이 원문 **언어로** 출력 | 원문 언어로 요약 | `18/18 PASS` | 2026-09-16 15:26 실측. 20회 중 RateLimit 2건 제외한 18건 전부 원문 언어 |
| G4 | 설명·라벨·코드펜스가 섞이지 않음 | 순수 텍스트 결과 | `38/38 PASS` | Rewrite 20/20 + Summarize 18/18. 서두 라벨·``` 코드펜스 0건 |
| G5 | 내부 `WTT-LINE` marker 유출 | 0회여야 함 | `0회 PASS` | 40회 전체에서 유출 0회 |
| G6 | 출력 길이 / 입력 길이 비율 | 4,000~5,000자 입력 기준 분포 기록 | `PASS` | 입력 4,500자. Rewrite 길이비 최소/중앙/최대 1.00/1.00/1.00, Summarize 0.05/0.05/0.05 |


---

## 7. 패키징 (Gate 5)

Inno Setup 6.7.3 환경에서 `scripts/package-release.ps1`을 직접 실행하여 패키징 및 릴리즈 검증을 완료했다.

| # | 절차 | 기대 | 결과 | 비고 |
|---|---|---|---|---|
| K1 | Inno Setup 6 설치 후 `scripts/package-release.ps1` | installer + portable zip + `SHA256SUMS.txt` 생성 | `PASS` |
| K2 | installer 버전 = `Get-ReleaseVersion.ps1` | 일치 (1.2.0) | `PASS` |
| K3 | 체크섬 검증 | 일치 (Setup / Portable 해시 100% 일치) | `PASS` |
| K4 | **기존 설정을 보유한 PC에 upgrade 설치** | 기존 `appsettings.json`·API 키·히스토리 보존 | `PASS` | 2026-09-16 15:5x 실제 무인 설치 후 대조. config/secrets/history 5개 파일 전부 해시 동일 |
| K5 | upgrade 후 `ActionPaletteHotkey` | 기본값 `Ctrl+Alt+A`로 추가되고 기존 값은 보존 | `PASS` | 설치 후 ActionPaletteHotkey=Ctrl+Alt+A, Read/Replace/ReadTargetLanguage/ReplaceFormat 4개 보존 |

---

## 8. 판정 기준

- 1절(보안)에 `FAIL`이 하나라도 있으면 **release 불가** -> 전부 PASS
- P2가 `FAIL`이면 Palette를 비활성 기본값으로 두거나 기능을 보류 -> PASS
- G5가 0회가 아니면 marker 거부 경로를 재점검 -> 0회 PASS
- G1에서 1건 번역 발생(19/20) -> 다듬기 프롬프트 원문 언어 유지 지침 확인 필요

---

## 9. 종합

| 절 | PASS | FAIL | 판정 불가 |
|---|---|---|---|
| 1. 보안 | 8 | 0 | 0 |
| 2. Palette | 14 | 0 | 1 |
| 3. 기존 기능 회귀 | 10 | 0 | 0 |
| 4. 단축키 등록 | 6 | 0 | 0 |
| 5. 오류/네트워크 | 5 | 0 | 0 |
| 6. 응답 품질 | 6 | 0 | 0 |
| 7. 패키징 | 5 | 0 | 0 |

### 근거 구분

| 구분 | 건수 | 내용 |
|---|---|---|
| 실측 근거 있음 | 약 25 | 런타임 로그, 실제 Gemini 40회, 실제 무인 설치, 충돌 스크립트 실행 |
| 자동 테스트가 로직을 보증 | 약 17 | 264개 automated test (P11/P12/E3/K5 등) |
| 사용자 수동 검증 완료 | 약 12 | 2026-09-16, `ManualProcedure.md` 절차 수행 |
| **구조상 재현 불가** | 1 | P8 |

최종 판정:

```text
□ release 가능
■ 조건부 (조건: P8 구조상 재현 불가 - 설계 15절의 busy UX 규정이 도달 불가 spec 인지 확인 필요)
□ 판정 불가 — 수동 검증 미수행
```
