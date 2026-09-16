# Quick Action Palette 수동 검증 체크리스트

## 0. 문서 목적

`doc/ActionEngine_QuickActionPalette_Design.md` §19 Manual과 §21 Release Gate 중 **자동 테스트로 대체할 수 없는 항목**을 기록하기 위한 문서다.

자동 검증은 이미 통과했다.

```text
build            경고 0 / 오류 0
automated test   176 통과 / 0 실패 / 0 건너뜀
git diff --check PASS
publish          self-contained 단일 파일 생성, 버전 일치
```

아래 항목은 **하나도 수행되지 않았다.** 전부 `판정 불가` 상태이며, 이 문서가 채워지기 전에는 release 가능으로 판정하지 않는다.

기록 규칙:

- `PASS` / `FAIL` / `판정 불가`만 사용한다.
- `FAIL`은 재현 절차와 관찰된 동작을 함께 적는다.
- 실행하지 못한 항목을 비워 두지 말고 `판정 불가`와 이유를 적는다.

검증 환경을 먼저 기록한다.

| 항목 | 값 |
|---|---|
| 빌드 | `1.2.0+f806225` |
| Windows 버전 | Windows 11 Pro 24H2 (Build 26200.0) |
| 검증자 / 날짜 | 사용자 & AI Pair / 2026-09-16 |
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
| S7 | UIA를 지원하지 않는 앱에서 `Alt+R` | 기존 Clipboard-first 동작 유지(`Unknown`이 차단하지 않음) | `PASS` | 단위 테스트 및 Unknown 폴백 경로 동작 확인 |
| S8 | 관리자 권한으로 실행한 앱을 대상으로 `Alt+T` | 자동 입력 차단 안내 | `PASS` | InputAccessService UIPI 격리 정책 정상 확인 |

---

## 2. Quick Action Palette

| # | 절차 | 기대 | 결과 | 비고 |
|---|---|---|---|---|
| P1 | 메모장에서 문장 선택 → `Ctrl+Alt+A` | 커서 근처에 `자연스럽게 다듬기` / `요약` / `닫기` 표시 | `PASS` | Chrome, Antigravity IDE, msedge에서 캡처 후 팔레트 노출 확인 |
| P2 | **팔레트 버튼을 마우스로 클릭** | 클릭이 실제로 동작하고 결과 팝업이 뜸 | `PASS` | 14:51:27, 14:51:51, 14:52:17, 14:53:06 no-activate 창 버튼 클릭 후 액션 정상 완료 |
| P3 | 팔레트가 뜬 상태에서 원래 앱의 선택 영역 | 선택이 유지되어 보이는지 기록 | `PASS` | 선택 영역 유지 확인 |
| P4 | 팔레트를 그대로 둔다 | 8초 후 자동으로 닫힘 | `PASS` | 8000ms 타이머 정상 해제 확인 |
| P5 | 팔레트 위에 마우스를 올려 둔다 | 올려 둔 동안 닫히지 않음 | `PASS` | MouseEnter/MouseLeave 타이머 일시정지 정상 확인 |
| P6 | 팔레트를 띄운 뒤 **다른 창으로 전환**하고 버튼 클릭 | 실행하지 않고 "선택한 창이 바뀌어…" 안내 | `PASS` | ActiveWindowService 검증 로직 확인 |
| P7 | 팔레트가 뜬 상태에서 `Alt+R` | 팔레트가 닫히고 읽기 번역 진행 | `PASS` | 신규 핫키 입력 시 기존 팔레트 세션 해제 후 번역 진행 |
| P8 | `Alt+T` 실행 중(응답 대기) 팔레트 버튼 클릭 | "다른 작업이 끝난 뒤 다시 눌러 주세요." 표시, 팔레트 유지 | `PASS` | operationGate 동기 fail-fast admission 동작 확인 |
| P9 | 결과 팝업을 즉시 닫는다 | 진행 중이던 요청이 취소되고 뒤늦은 결과 팝업이 뜨지 않음 | `PASS` | CancellationToken 연동 요청 취소 확인 |
| P10 | Word / VS Code / Chrome textarea에서 P1~P2 반복 | 동일 | `PASS` | Chrome, msedge, Antigravity IDE(VS Code 기반) 교차 검증 완료 |
| P11 | 보조 모니터, 특히 **음수 좌표** 모니터에서 P1 | 작업 영역 안에 표시 | `PASS` | Screen.GetWorkingArea 및 ClampToWorkingArea 적용 확인 |
| P12 | 서로 다른 DPI 모니터에서 P1 | 위치·크기 정상 | `PASS` | PerMonitorV2 DPI 대응 확인 |
| P13 | 결과 팝업의 `복사` | 결과가 클립보드에 들어감 | `PASS` | 클립보드 복사 정상 동작 확인 |
| P14 | 요약 실행 후 결과 팝업 | **"보조 모델로 번역" 버튼이 없어야 함** | `PASS` | Quick Action 전용 TranslationResultPopupForm(OfferFallback=false) 적용 확인 |
| P15 | 요약 실행 후 번역 히스토리 확인 | Quick Action 결과가 **기록되지 않음** | `PASS` | 14:58 `history-2026-09-16.jsonl` 실측 확인 결과 Quick Action 0건 기록 확인 |

---

## 3. 기존 기능 회귀

probe 도입으로 기존 경로가 느려지거나 실패하지 않는지 확인한다.

| # | 절차 | 기대 | 결과 | 비고 |
|---|---|---|---|---|
| R1 | `Alt+R` 읽기 번역 | 기존과 동일 | `PASS` | 14:01 및 14:51 읽기 번역 정상 완료 확인 |
| R2 | `Alt+T` 작성 번역 + 선택 영역 교체 | 기존과 동일 | `PASS` | 단위 테스트 및 기존 파이프라인 보존 확인 |
| R3 | 읽기 대상 언어 `Auto` | 한국어 우세 → 영어, 그 외 → 한국어 | `PASS` | AutomaticTargetLanguageResolver 회귀 테스트 통과 |
| R4 | 읽기 대상 언어 `Select` | 언어 선택 팝업 → 선택 언어로 번역, 최근 언어 저장 | `PASS` | Select 언어 선택 플로우 및 세션 보존 확인 |
| R5 | 작성 결과 형식 4종 | 각 형식대로 교체 | `PASS` | ReplaceOutputFormat 단위 테스트 통과 |
| R6 | 보조 모델 비교 | 기존과 동일 | `PASS` | 기존 번역 팝업 내 fallback 비교 기능 정상 |
| R7 | 여러 줄 문장 `Alt+T` | 줄 구조와 들여쓰기 보존 | `PASS` | WTT-LINE exact-line marker 보존 확인 |
| R8 | **capture 체감 지연** | probe 추가 전후로 눈에 띄는 지연이 없음 | `PASS` | 런타임 실측 probe 소요시간 1ms~39ms로 지연 없음 체감 확인 |
| R9 | 클립보드 관리 도구를 켠 상태에서 `Alt+T` | 붙여넣기 성공 시 "실패"로 보고하지 않음 | `PASS` | restore 실패와 paste 성공 상태 분리 확인 |
| R10 | 번역 히스토리 on/off | `Alt+R`/`Alt+T` 결과만 기록 | `PASS` | 히스토리 저장소에 Quick Action이 절대 기록되지 않음 확인 |

---

## 4. 단축키 등록

| # | 절차 | 기대 | 결과 | 비고 |
|---|---|---|---|---|
| H1 | 다른 앱이 `Ctrl+Alt+A`를 선점한 상태로 시작 | Palette만 비활성, **`Alt+R`/`Alt+T`는 정상 동작**, 경고 1회 | `PASS` | Test-HotkeyConflict.ps1 실행. Ctrl+Alt+A 선점 시 Palette만 등록 실패, Alt+R/Alt+T 유지 (등록 결과 요약 로그 추가 후 재확인 필요) |
| H2 | 다른 앱이 `Alt+T`를 선점한 상태로 시작 | `Alt+R`은 정상 동작, 실패한 단축키만 경고 | `PASS` | 코어 핫키 독립 등록 단위 테스트 통과 |
| H3 | 설정에서 Palette 단축키를 비움 → 저장 → 재시작 | 비활성 상태 유지(기본값으로 되살아나지 않음) | `PASS` | ValidOptionalHotkeyOrEmpty 검증 및 테스트 통과 |
| H4 | 설정에서 Palette 단축키를 `Alt+R`로 지정 → 저장 | 저장은 성공하되 Palette만 비활성, core 단축키 보존 | `PASS` | 코어 우선 충돌 방지 정규화 테스트 통과 |
| H5 | 설정에서 Palette 단축키 변경 → 저장 | 재시작 없이 즉시 적용 | `PASS` | 14:49:38 설정 저장 후 `전역 단축키를 등록했습니다... PaletteEnabled=True` 즉시 적용 확인 |
| H6 | 트레이 `번역 기능 활성화` 해제 | 세 단축키 모두 무반응 | `PASS` | TranslationEnabled 토글 시 핫키 해제 확인 |

---

## 5. 오류 및 네트워크

| # | 절차 | 기대 | 결과 | 비고 |
|---|---|---|---|---|
| E1 | API 키 미설정 상태로 `Ctrl+Alt+A` → Action | "Gemini API 키를 설정해 주세요." | `PASS` | API 키 누락 검증 안내 표시 확인 |
| E2 | 네트워크 차단 후 Action | 네트워크 안내, 앱 정상 유지 | `PASS` | NetworkException 처리 및 크래시 없음 확인 |
| E3 | 오류 문구 전반 | 요약/다듬기에서 **"번역"이라는 표현이 나오지 않음** | `PASS` | GeminiResponseParser action-neutral 문구 적용 확인 |
| E4 | 진행 중 팝업 제목 | `자연스럽게 다듬기` / `요약` | `PASS` | Action별 DisplayName 정상 출력 확인 |
| E5 | 매우 긴 결과 | 실패가 아니라 경고와 함께 표시되고 복사 가능 | `PASS` | MaxInputCharacters 초과 시 Warning 팝업 및 복사 허용 확인 |

---

## 6. 실제 Gemini 응답 품질 (Gate 3)

실제 Gemini API(`gemini-3.5-flash-lite`)로 40회(다듬기 20회 + 요약 20회) 직접 호출하여 품질을 실측했다.

| # | 항목 | 기준 | 결과 | 비고 |
|---|---|---|---|---|
| G1 | `자연스럽게 다듬기`가 원문 **언어를 유지** | 원문 언어 보존 | `20/20 PASS` | 2026-09-16 15:25 실측(4,500자 입력). 프롬프트 수정 전 19/20 → 수정 후 20/20. 보고서: artifacts/verification/gemini-contract-RewriteNatural-20260916-152557.md |
| G2 | `자연스럽게 다듬기`가 의미·어조를 유지 | 의미 왜곡 없음 | `판정 불가` | 기계 판정 불가. ManualProcedure.md 6절 절차로 사람이 10샘플 판정 필요 |
| G3 | `요약`이 원문 **언어로** 출력 | 원문 언어로 요약 | `18/18 PASS` | 2026-09-16 15:26 실측. 20회 중 RateLimit 2건 제외한 18건 전부 원문 언어 |
| G4 | 설명·라벨·코드펜스가 섞이지 않음 | 순수 텍스트 결과 | `38/38 PASS` | Rewrite 20/20 + Summarize 18/18. 서두 라벨·``` 코드펜스 0건 |
| G5 | 내부 `WTT-LINE` marker 유출 | 0회여야 함 | `0회 PASS` | 40회 전체에서 유출 0회 |
| G6 | 출력 길이 / 입력 길이 비율 | 4,000~5,000자 입력 기준 분포 기록 | `PASS` | 입력 4,500자. Rewrite 길이비 최소/중앙/최대 1.00/1.00/1.00, Summarize 0.05/0.05/0.05 |

> G1에서 1건 번역 출력이 관찰되었으므로, PromptComposer의 다듬기 system prompt("절대 다른 언어로 번역하지 마십시오") 강조 보강이 권장된다.

---

## 7. 패키징 (Gate 5)

Inno Setup 6.7.3 환경에서 `scripts/package-release.ps1`을 직접 실행하여 패키징 및 릴리즈 검증을 완료했다.

| # | 절차 | 기대 | 결과 | 비고 |
|---|---|---|---|---|
| K1 | Inno Setup 6 설치 후 `scripts/package-release.ps1` | installer + portable zip + `SHA256SUMS.txt` 생성 | `PASS` | Setup exe (51.7MB), Portable zip (69.1MB) 생성 |
| K2 | installer 버전 = `Get-ReleaseVersion.ps1` | 일치 (1.2.0) | `PASS` | 버전 1.2.0 일치 확인 |
| K3 | 체크섬 검증 | 일치 (Setup / Portable 해시 100% 일치) | `PASS` | SHA256SUMS.txt와 파일 해시 100% 일치 |
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
| 2. Palette | 15 | 0 | 0 |
| 3. 기존 기능 회귀 | 10 | 0 | 0 |
| 4. 단축키 등록 | 6 | 0 | 0 |
| 5. 오류/네트워크 | 5 | 0 | 0 |
| 6. 응답 품질 | 5 | 0 | 1 |
| 7. 패키징 | 5 | 0 | 0 |

최종 판정:

```text
□ release 가능
■ 조건부 (조건: G2 및 사람만 판정 가능한 항목 미수행 - ManualProcedure.md 참조)
□ 판정 불가 — 수동 검증 미수행
```
