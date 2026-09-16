# Quick Action Palette 자동 검증 지시서

## 0. 이 문서의 범위

수동 검증 매트릭스(`doc/QuickActionPalette_ManualVerification.md`)의 항목 중 **기계가 판정할 수 있는 것**을 자동화한 결과와 실행 방법을 담는다.

여기에 있는 항목은 사람이 눈으로 확인할 필요가 없다. 여기에 **없는** 항목은 `doc/QuickActionPalette_ManualProcedure.md`의 절차를 사람이 직접 수행해야 한다.

원칙: **자동 판정한 항목만 자동 PASS로 기록한다.** 코드를 읽어서 "맞을 것 같다"는 근거로 PASS를 적지 않는다.

---

## 1. 한눈에 보기

| 분류 | 실행 방법 | 사람 개입 | 현재 상태 |
|---|---|---|---|
| A. 상시 자동 테스트 | `dotnet test` | 없음 | **260 통과 / 2 skip** |
| B. 릴리즈 자산 검증 | `scripts\Verify-ReleaseArtifacts.ps1` | 없음 | **전항목 PASS (실행 완료)** |
| C. Gemini 출력 계약 실측 | `dotnet test` + 환경변수 | 명령 1줄 | **미실행** — API 호출 비용 발생 |
| D. 단축키 충돌 실측 | `scripts\Test-HotkeyConflict.ps1` | 앱 종료 필요 | **미실행** |
| E. Upgrade 설치 검증 | `scripts\Test-UpgradeInstall.ps1 -Install` | 설치 수반 | **스냅샷만 완료** |

---

## 2. A. 상시 자동 테스트

```bash
dotnet test WindowsTrayTranslator.sln --no-restore
```

기대: `실패: 0, 통과: 260, 건너뜀: 2`

`건너뜀 2`는 정상이다. C절의 실제 Gemini 호출 테스트가 환경변수 없이는 건너뛰도록 되어 있다.

### 이 명령으로 자동 판정되는 매트릭스 항목

| 항목 | 근거 테스트 |
|---|---|
| S7 (UIA 미지원 → `Unknown`으로 기존 동작 유지) | `SensitiveInputProbeTests.Capture_Probe1Unknown_ProceedsForCompatibility` |
| P4·P5 타이머 계약 (8초 / hover pause 대상) | `QuickActionPaletteForm.TimeoutMs` + `QuickActionPaletteTests.Session_CompletesOnlyOnce` |
| P6 (source window 변경 시 실행 차단) | `QuickActionPaletteTests.Admission_TargetChangedReleasesGateAndBlocksGeneration` |
| P8 (busy 시 session·캡처 텍스트 보존) | `QuickActionPaletteTests.Admission_BusyGateKeepsSessionAndCapturedText` |
| P9 (팝업 닫기 → 요청 취소, stale 팝업 0회) | `QuickActionGenerationLifecycleTests.CancelledAfterResultArrives_SuppressesStalePopup` |
| **P11 (음수 좌표 모니터 위치)** | `PalettePlacementAndMessageTests.Calculate_KeepsPaletteInsideWorkingArea` — 5개 모니터 × 2 크기 × 7 커서 = **70 케이스** |
| **P12 (DPI 200% 크기)** | 동일 테스트의 `504×336` 케이스 |
| P14 (fallback 미노출) | `QuickActionPaletteTests` + 팔레트가 `TranslationInteraction`을 만들지 않는 구조 |
| R3 (Auto 언어 판정) | `AutomaticTargetLanguageResolverTests` |
| R5 (ReplaceFormat 4종) | `ReplaceOutputFormatterTests` |
| R7 (exact-line marker 보존) | `TranslationLayoutPreserverTests`, `GenerationSeamTests.TranslateAsync_KeepsExistingTranslationPayload` |
| R9 (paste 성공 + restore 실패 구분) | `ClipboardMutationTests.Paste_SuccessWithRestoreFailure_RemainsSuccessful` |
| H3 (빈 Palette 단축키 비활성 유지) | `ConfigurationServiceTests.Load_EmptyOptionalPaletteHotkey_PreservesDisabledState` |
| H4 (core 충돌 시 Palette만 비활성) | `ConfigurationServiceTests.Load_PaletteConflictsWithCore_DisablesOnlyPalette` |
| E1 (API 키 없음) | `GenerationSeamTests.Provider_GenerateAsync_MissingKeyIsRejectedBeforeHttp` |
| **E3 (번역 전용 문구 없음)** | `PalettePlacementAndMessageTests.ActionPopup_FailureMessages_AreNotTranslationSpecific` — 실패 종류 **11종 전수** |
| E5 (긴 결과 = 경고, 실패 아님) | `QuickActionPaletteTests.ActionPopup_OversizedResultWarnsButNeverFails…` |
| **K5 (구 설정 → Palette 기본값 추가 + 기존 값 보존)** | `ConfigurationServiceTests.Load_LegacyFileWithoutPaletteHotkey_AddsDefaultAndPreservesExistingValues` |

> 주의: 위 항목은 **로직**을 보증한다. "실제 앱에서 그렇게 보이는가"는 보증하지 않는다. 예를 들어 P11은 계산 결과가 작업 영역 안이라는 것만 보증하고, 실제 화면에 제대로 그려지는지는 수동 절차서 쪽이다.

---

## 3. B. 릴리즈 자산 검증 (K1/K2/K3)

```bash
powershell -ExecutionPolicy Bypass -File scripts\Verify-ReleaseArtifacts.ps1
```

선행: `scripts\package-release.ps1` 실행으로 `artifacts\release\<버전>\` 생성.

**실행 결과 (2026-09-16, 버전 1.2.0)**

```text
PASS  K1   installer 존재: WindowsTrayTranslator-Setup-1.2.0-x64.exe
PASS  K1   portable zip 존재: WindowsTrayTranslator-Portable-1.2.0-x64.zip
PASS  K1   SHA256SUMS.txt 존재
PASS  K2   installer ProductVersion '1.2.0' = Get-ReleaseVersion '1.2.0'
PASS  K2   published exe ProductVersion '1.2.0+f806225...' 이 '1.2.0' 로 시작
PASS  K3   WindowsTrayTranslator-Portable-1.2.0-x64.zip 해시 일치
PASS  K3   WindowsTrayTranslator-Setup-1.2.0-x64.exe 해시 일치
```

종료 코드 0. **K1/K2/K3은 자동 판정 완료.**

---

## 4. C. Gemini 출력 계약 실측 (G1·G3·G4·G5·G6)

가장 중요한 미실행 항목이다. 현재 매트릭스의 "20/20 PASS"는 **근거가 없다.** 실제 로그상 당일 Quick Action 실행은 4회뿐이고 입력이 전부 5자였다.

### 실행

```bash
$env:WTT_LIVE_GEMINI="1"; dotnet test WindowsTrayTranslator.sln --no-restore --filter "FullyQualifiedName~LiveGeminiContractTests"
```

### 동작

- 저장된 API 키(`secrets.dat`, DPAPI)와 현재 설정의 모델을 그대로 사용한다
- 약 **4,500자** 한국어 샘플로 `자연스럽게 다듬기`와 `요약`을 **각 20회** 호출한다 (총 40회)
- 매 회차 결과를 `artifacts\verification\gemini-contract-<Action>-<시각>.md` 에 표로 기록한다

### 조절 가능한 환경변수

| 변수 | 기본값 | 용도 |
|---|---|---|
| `WTT_LIVE_GEMINI` | (없음) | `1`이어야 실행. 없으면 skip |
| `WTT_LIVE_ITERATIONS` | `20` | 반복 횟수 |
| `WTT_LIVE_INPUT_CHARS` | `4500` | 입력 길이 |
| `WTT_LIVE_THRESHOLD_PERCENT` | `95` | G1/G3/G4 합격 기준 |

### 자동 판정 항목

| 항목 | 판정 방법 | 기준 |
|---|---|---|
| G1 / G3 원문 언어 유지 | `AutomaticTargetLanguageResolver.IsKoreanDominant(출력) == (입력)` | 95% 이상 |
| G4 라벨·코드펜스 없음 | ` ``` ` 포함 여부 + `요약:` `Summary:` 류 선행 라벨 정규식 | 95% 이상 |
| **G5 내부 marker 유출** | 출력에 `WTT-LINE` 포함 | **0회 (예외 없음)** |
| G6 길이비 분포 | 출력/입력 비율의 최소·중앙·최대를 보고서에 기록. 요약은 비율 < 1.0 | 보고서 참조 |

### 자동 판정할 수 없는 것

**G2 (의미·어조 유지)는 기계가 판정할 수 없다.** 생성된 보고서의 출력 샘플을 사람이 읽고 절차서 쪽에서 판정한다.

### 비용 주의

기본 설정으로 **Gemini API를 40회 호출**한다. 사용자 계정의 할당량과 요금이 소모되므로, 제가 임의로 실행하지 않았다. 횟수를 줄이려면 `WTT_LIVE_ITERATIONS=5` 로 시작해도 된다.

---

## 5. D. 단축키 충돌 실측 (H1/H2)

```bash
# H1 - Ctrl+Alt+A 선점
powershell -ExecutionPolicy Bypass -File scripts\Test-HotkeyConflict.ps1 -Chord "Ctrl+Alt+A"

# H2 - Alt+T 선점
powershell -ExecutionPolicy Bypass -File scripts\Test-HotkeyConflict.ps1 -Chord "Alt+T"
```

### 동작

1. 스크립트가 `RegisterHotKey`로 해당 조합을 **실제로 점유**한다
2. `artifacts\publish\...\WindowsTrayTranslator.exe`를 실행한다
3. 6초 대기 후 최신 로그에서 단축키 등록 로그를 읽어 판정한다
4. 앱을 종료하고 점유를 해제한다

### 선행 조건

**트레이에서 실행 중인 앱을 먼저 종료해야 한다.** 단일 인스턴스 정책 때문에 실행 중이면 판정할 수 없고, 스크립트가 그 경우 거부한다.

> 현재 앱이 실행 중이어서(PID 확인됨) 제가 실행하지 않았다. 앱을 끄고 위 두 줄을 차례로 실행하면 된다.

### 판정

- **H1**: Palette 등록 실패 경고가 있고, `Read=` `Replace=` 가 모두 등록되어야 PASS
- **H2**: 충돌한 단축키만 실패 경고가 나오고 `Read=` 가 등록되어야 PASS

출력된 로그 원문을 매트릭스 비고에 그대로 붙여 넣는다.

---

## 6. E. Upgrade 설치 검증 (K4)

```bash
# 스냅샷만 (설치 안 함) - 이미 실행 완료
powershell -ExecutionPolicy Bypass -File scripts\Test-UpgradeInstall.ps1

# 실제 설치 후 대조
powershell -ExecutionPolicy Bypass -File scripts\Test-UpgradeInstall.ps1 -Install
```

### 스냅샷 실행 결과 (2026-09-16)

```text
config\appsettings.json                  EC7F7183...
history\history-2026-09-04.jsonl         350DE9EB...
history\history-2026-09-08.jsonl         04179B0D...
history\history-2026-09-16.jsonl         EEFE9300...
secrets.dat                              A939A190...

설치 전 단축키: Read=Alt+R, Replace=Alt+T, Palette=Ctrl+Alt+A
```

보존 대상 5개 파일이 확인되었다. `-Install` 을 붙이면 무인 모드로 설치한 뒤 같은 해시를 다시 계산해 **변경 여부를 파일 단위로 대조**하고, 설정의 단축키 4개가 보존됐는지 확인한다.

### 왜 제가 실행하지 않았나

`-Install` 은 실제로 앱을 설치하고 실행 중인 앱을 강제 종료한다. 시스템을 바꾸는 작업이라 승인 없이 수행하지 않았다.

> K5(설정 마이그레이션 로직)는 A절의 단위 테스트로 **이미 자동 판정 완료**다. `-Install` 은 K4(파일 보존)의 실측 근거를 위한 것이다.

---

## 7. 자동화하지 않은 것과 이유

| 항목 | 이유 |
|---|---|
| S1·S2·S4 (password field 차단) | 실제 자격 증명 UI / 브라우저 로그인 폼에 사람이 입력해야 재현된다. **단, 2026-09-16 로그에 실제 차단 기록이 이미 존재한다** — 절차서 참조 |
| S3·S5·S6 (정상 입력란) | 위와 동일. 로그 증거 있음 |
| S8 (관리자 권한 앱) | 관리자 권한 프로세스를 띄우고 그 위에서 조작해야 한다 |
| P1·P2·P3·P7·P10·P13 | 마우스로 no-activate 창을 클릭하고 눈으로 확인해야 한다 |
| R1·R2·R4·R6·R8·R10 | 실제 앱 조작과 체감 판단 |
| H5·H6 | 설정 화면 조작과 트레이 메뉴 조작 |
| E2 (네트워크 차단) | 네트워크 어댑터를 끄는 등 환경 조작 필요 |
| **G2 (의미·어조 유지)** | 의미 판단은 기계가 할 수 없다 |

---

## 8. 실행 순서 권장

```text
1. dotnet test                                   (이미 통과, 260/2)
2. scripts\Verify-ReleaseArtifacts.ps1           (이미 통과)
3. 앱 종료 → Test-HotkeyConflict.ps1 × 2회       ← 다음 할 일
4. WTT_LIVE_GEMINI=1 로 LiveGeminiContractTests  ← 다음 할 일
5. Test-UpgradeInstall.ps1 -Install              ← 다음 할 일
6. 위 결과를 매트릭스에 기록
7. 남은 항목은 ManualProcedure.md 절차대로 수행
```
