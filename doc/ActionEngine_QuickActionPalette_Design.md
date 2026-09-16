# Windows Tray Translator

# Minimal Action Engine & Quick Action Palette 설계

## 1. 문서 상태

- 상태: 최종 적대적 검증 반영 구현 기준안
- 작성 기준: 2026-09-16 working tree
- 기준 commit: `1c5c87e758bef62e258d3b5606cdf5804e473148`
- 제품 방향: 가볍고 직관적인 Windows tray utility
- 구현 전제: 기존 `Alt+R`, `Alt+T` 호환성 우선

관련 문서:

- 초기 상세 설계: `doc/ActionEngine_SelectionActionBar_Design.md`
- 초기 Lite Scope: `doc/ActionEngine_SelectionActionBar_LiteScope.md`
- 적대적 검증 결과: `doc/ActionEngine_AdversarialReview_Result.md`
- Quick Action Palette 1차 검증: `doc/ActionEngine_QuickActionPalette_AdversarialReview_Result.md`
- Quick Action Palette 최종 검증: `doc/ActionEngine_QuickActionPalette_FinalReview_Result.md`

위 두 초기 설계 문서는 검토 이력으로만 보존한다. 현재 구현 범위, 우선순위와 완료 기준은 이 문서를 authoritative source로 사용한다.

---

## 2. 최종 결정

자동 mouse selection 감지와 Global Mouse Hook을 사용하지 않는다.

대신 사용자가 별도 단축키를 눌렀을 때만 Quick Action Palette를 표시한다.

```text
텍스트 선택
→ Ctrl+Alt+A
→ modifier 해제 및 target 확인
→ Sensitive probe #1
→ clipboard snapshot/clear
→ attempt 1..2: Sensitive probe #2 → Ctrl+C → read
→ clipboard 복원
→ Quick Action Palette 표시
→ Action 선택
→ Gemini 실행
→ Popup + 복사
```

핵심 결정:

1. 기존 `Alt+R`, `Alt+T`는 그대로 유지한다.
2. `Ctrl+Alt+A` 하나만 optional Quick Action Palette 단축키로 추가한다. 등록 실패가 기존 hotkey에 영향을 주지 않는다.
3. Palette를 표시하기 전에 선택 text를 capture한다.
4. 신규 Action은 첫 release에서 Popup/복사만 지원한다.
5. 자동 Replace는 기존 `Alt+T`만 유지한다.
6. Global Mouse Hook, Drag 감지, Action Bar 자동 노출은 제거한다.
7. 범용 Action 관리 system을 만들지 않는다.
8. Gemini translation 결합을 푸는 최소 seam만 추가한다.
9. Palette는 기존 번역과 중복되지 않는 Rewrite/Summary 두 Action만 제공한다.
10. Palette Action은 fallback, history, Replace를 사용하지 않는다.

첫 완료 목표:

> 기존 Alt+R/Alt+T가 그대로 동작하고, 사용자가 Ctrl+Alt+A를 누르면 선택 text를 먼저 capture한 뒤 작은 Palette에서 자연스럽게 다듬기 또는 요약을 선택하여 Popup으로 확인하고 복사할 수 있다.

---

## 3. 적대적 검증 결과 반영

### 3.1 채택

다음 finding을 설계 전제로 채택한다.

- 기본값은 Clipboard-first이므로 UIA provider 내부의 password fallback 차단만으로 보호되지 않는다.
- Clipboard recapture 기반 `SelectionLease`는 clipboard write/restore 순서와 충돌한다.
- `GeminiApiClient` 내부가 translation system prompt와 line marker에 결합되어 있다.
- 미지 hotkey ID가 현재 Replace로 분기될 수 있다.
- `operationGate`가 fallback interaction 전체를 직렬화하지 않는다.
- `ActiveWindowService.Capture()`는 hook callback에서 사용하기에 무겁다.
- 언어 선택 popup의 modifier 없는 global key 등록 방식을 Palette에 재사용하면 안 된다.
- 설정 parsing 실패는 현재 파일 전체를 기본값으로 되돌린다.
- 기존 codebase에 소비되지 않는 추상화가 있어 새 interface/class 도입을 최소화해야 한다.
- optional Palette hotkey 등록 실패는 core hotkey 등록과 격리해야 한다.
- Palette 표시 중 `operationGate`를 계속 점유하면 기존 hotkey가 장시간 무응답이 된다.
- Clipboard restore 실패는 capture와 paste에서 서로 반대 방향으로 잘못 보고되고 있다.

### 3.2 보정

Hotkey numeric ID는 서로 다른 HWND에 등록되는 경우 숫자가 같다는 사실만으로 직접 충돌한다고 단정하지 않는다. 현재 설계는 dynamic ID allocator를 만들지 않고 고정 ID 세 개만 사용한다.

Sensitive probe는 UIA가 명확히 `IsPassword=true`를 반환한 영역만 확실히 차단한다. UIA timeout/unsupported 상태까지 안전하다고 주장하지 않는다.

`operationGate`는 Palette 표시 중 점유하지 않는다. Action click 시 `Wait(0)`으로 동기 fail-fast admission에 성공한 뒤 같은 callback에서 session을 claim한다. 사용자 operation ownership과 clipboard mutation 직렬화를 분리한다.

---

## 4. 사용자 Workflow

### 4.1 기존 읽기 번역

```text
선택
→ Alt+R
→ 기존 Read target/Auto/Select 정책
→ 번역 Popup
```

### 4.2 기존 작성 번역

```text
선택
→ Alt+T
→ 기존 Replace target/format 정책
→ 현재 window/focus 검증
→ Replace 또는 Copy fallback
```

현재 `Alt+T`는 selection range identity까지 보증하지 않는다. 동일 HWND 안에서 같은 focus control의 caret나 selection이 변경된 경우를 완전히 판별할 수 없다는 제한을 문서화한다.

### 4.3 Quick Action Palette

```text
선택
→ Ctrl+Alt+A
→ capture 성공
→ [자연스럽게 다듬기] [요약]
→ Action click
→ 결과 Popup
```

Capture 실패 시 Palette를 표시하지 않는다.

```text
선택 없음 → 기존 capture 오류
Sensitive → 보호된 입력 안내
Target 변경 → 취소 안내
Too large → 문자 수 안내
```

---

## 5. 포함 범위

### 5.1 Hotkey

```text
Alt+R       읽기 번역
Alt+T       작성 번역
Ctrl+Alt+A  Quick Action Palette
```

Palette hotkey는 설정에서 변경하거나 비활성화할 수 있지만 Action별 hotkey는 제공하지 않는다.

core hotkey 두 개는 서로 달라야 한다. Palette hotkey가 core와 충돌하면 Palette만 비활성화한다. 시작 시 core 두 개를 먼저 등록하고 Palette는 별도 best-effort 등록한다. Palette 등록 실패가 core 등록을 해제하거나 rollback하지 않는다.

### 5.2 Action

| Action | Input | Output | Layout |
|---|---|---|---|
| 자연스럽게 다듬기 | captured selection | Popup/Copy | line marker 미사용 |
| 요약 | captured selection | Popup/Copy | free form, line marker 미사용 |

번역과 `영어로 작성`은 Palette에 중복 노출하지 않는다. 기존 `Alt+R`과 `Alt+T`가 담당한다.

### 5.3 Palette UI

```text
┌─────────────────────────┐
│ 자연스럽게 다듬기       │
│ 요약                    │
│                    닫기 │
└─────────────────────────┘
```

- fixed Action 두 개
- mouse click 선택
- cursor 근처 표시
- working area clamp
- `ShowWithoutActivation`
- `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`
- `TopMost`
- timeout 8초, pointer hover 동안 pause
- close button

지원하지 않음:

- 전역 Esc/Enter/숫자키 등록
- keyboard navigation
- 검색
- Recent/Favorite
- Action 관리
- App Profile
- 외부 click 감지를 위한 mouse hook

Palette는 timeout, Action click, Close click, shutdown에서 닫는다. 외부 click dismiss를 위해 추가 hook이나 polling을 만들지 않는다.

---

## 6. 명시적 제외 범위

```text
Global Mouse Hook
Drag/MouseUp selection 감지
자동 Selection Action Bar
Selection Action Bar 앱 제외 목록
Action Launcher 검색 UI
Dynamic Action Hotkey
Custom Action
Action Catalog override/merge
OutputRouter class
TextActionCoordinator class
ITextGenerationProvider
ISelectionLease
Clipboard selection lease
UIA range mutation
Preview 후 자동 Replace
신규 Action Replace
Undo/Regenerate
Insert Before/After
Local Action
Preset Import/Export
Usage statistics/telemetry
OCR/Voice/Plugin/Script
Schema v2 migration
```

후속 기능은 실제 사용에서 반복되는 불편이 확인될 때 별도 설계와 승인을 거친다.

---

## 7. 최소 Domain Model

범용 hierarchy를 만들지 않는다.

```csharp
internal enum BuiltInActionKind
{
    RewriteNatural,
    Summarize
}

internal sealed record CapturedSelection(
    string Text,
    WindowIdentity SourceWindow);
```

고정 Action metadata는 `static readonly` collection 하나로 충분하다.

```csharp
Display name은 `BuiltInActionKind`의 작은 `switch` 식으로 반환한다.
```

첫 release에서 model에 넣지 않는 값:

```text
Id string
Enabled override
Favorite
SortOrder
Hotkey per Action
Model override
Fallback override
InputType
ExecutionType
ApplicationProfile
Capability collection
Usage metadata
```

신규 Action은 모두 Popup/Copy이므로 output capability abstraction도 필요하지 않다.

---

## 8. Hotkey Dispatch

현재의 “Read ID인가, 아니면 Replace” 분기를 제거한다.

```csharp
private enum HotkeyCommand
{
    ReadTranslation,
    ReplaceTranslation,
    ShowActionPalette
}
```

Native ID mapping:

```text
1 → ReadTranslation
2 → ReplaceTranslation
3 → ShowActionPalette
그 외 → log 후 무시
```

예외 처리도 동일 command 기준으로 분기한다. 미지 ID는 selection capture, clipboard, Gemini, notification replace path에 진입하지 않는다.

등록 정책은 core와 optional을 분리한다.

```text
Startup:
1. Alt+R, Alt+T, Palette를 각각 독립적으로 등록
2. 한 hotkey 실패가 이미 성공한 다른 hotkey를 해제하지 않음
3. 실패한 key 이름을 모아 하나의 startup warning으로 표시

Settings apply:
1. candidate를 deep clone/normalize
2. 이전 등록 set이 있으므로 core pair와 enabled Palette를 transaction으로 등록
3. 이후 설정 적용이 실패하면 core와 Palette 모두 previous 값으로 복구
4. CopySettings/section 교체는 실패 가능한 작업이 모두 끝난 뒤 마지막에 수행
```

`SettingsValidator.Normalize` 충돌 우선순위도 고정한다.

```text
Read == Replace
→ core를 Alt+R/Alt+T로 복구

Palette == Read 또는 Palette == Replace
→ core를 보존하고 Palette만 빈 문자열로 비활성화
```

Palette 빈 문자열은 유효한 disabled 상태다. default를 강제하여 다시 활성화하지 않는다.

- `ValidOptionalHotkeyOrEmpty`: whitespace 또는 parse 실패는 `string.Empty`
- `ApplySettings`: Palette가 비어 있으면 parse, 충돌 검사, 등록을 모두 건너뜀
- Palette가 core와 충돌하면 예외 대신 Palette만 빈 문자열로 강등
- Palette 변경/disable/dispose 전 `UnregisterPalette()`로 native ID 3을 명시적으로 해제

Dynamic native ID allocator는 만들지 않는다.

---

## 9. Sensitive Probe

### 9.1 목적

Clipboard-first 설정에서도 명확한 password field에 `Ctrl+C`를 보내기 전에 차단한다.

### 9.2 결과

```csharp
internal enum SensitiveProbeResult
{
    Safe,
    Sensitive,
    Unknown
}
```

정책:

| 결과 | 동작 |
|---|---|
| `Sensitive` | `Ctrl+C`/API 호출 금지, 보호된 입력 메시지; probe #1이면 clipboard 접근도 금지 |
| `Safe` | 기존 provider pipeline 진행 |
| `Unknown` | 기존 호환을 위해 진행하되 안전 확인으로 기록하지 않음 |

`Unknown`에는 UIA 미지원, timeout, element unavailable이 포함된다. 이를 `Safe`와 합치지 않는다.

### 9.3 위치와 두 번의 probe

```text
Hotkey command 확인
→ source window capture
→ InputAccess 확인
→ modifier 해제 대기
→ source window 재확인
→ Sensitive probe #1
→ clipboard snapshot
→ clipboard clear
→ for attempt 1..2
    → source window 재확인
    → Sensitive probe #2
    → clipboard sequence 기준값
    → Ctrl+C
    → bounded read polling
→ restore
```

probe #1은 알려진 password field에서 clipboard 접근 자체를 막는다. probe #2는 느린 snapshot/clear와 이전 attempt의 polling/retry delay 동안 동일 HWND 내부 focus가 password field로 이동하는 TOCTOU 창을 줄인다. probe #2는 loop 밖에서 한 번 실행하지 않고 매 `SendCopy()` 직전에 실행한다. 어느 attempt에서든 `Sensitive`이면 해당 `Ctrl+C`를 보내지 않고 snapshot을 복원한다.

probe #2 자체는 최대 300ms의 UIA await와 UI-thread continuation post를 포함한다. probe 완료 후 해당 `Ctrl+C` 사이에는 추가 network, popup, timer wait, retry loop를 넣지 않는다. `IsStillActive`는 동일 Chromium/Electron HWND 안의 focus 이동을 보증하지 않으므로 sensitive probe를 대체하지 않는다.

기존 `Alt+R`의 `Select` 언어 popup은 capture 전에 무기한 대기할 수 있으므로, 기존 translation flow도 Clipboard provider가 실제로 `Ctrl+C`를 보내기 직전에 위 두 probe를 수행한다. ApplicationController의 provider 선택 이전 1회 probe만으로 구현하지 않는다.

### 9.4 실행 제약

- bounded timeout
- probe timeout은 `ClipboardCopyTimeoutMs`와 분리된 내부 상수 `SensitiveProbeTimeoutMs = 300` 사용; release 전 runtime 측정으로 유지/조정
- raw text를 읽지 않고 `IsPassword`만 확인
- probe worker는 single-flight 가드 사용: 기존 worker가 아직 실행 중이면 새 worker를 만들지 않고 즉시 `Unknown`
- in-flight flag는 `WaitAsync` timeout 시점이 아니라 실제 UIA worker의 `finally`에서 해제하여 포기된 worker를 최대 1개로 제한
- failure는 `Unknown`
- log에는 result와 elapsed만 기록

probe #1이 `Sensitive`이면 `ClipboardService`와 Gemini client 호출 0회를 보장한다. probe #2가 `Sensitive`이면 snapshot/clear/restore는 발생할 수 있지만 그 attempt의 `Ctrl+C`와 Gemini 호출은 0회여야 한다. attempt 1 실패 후 focus를 password field로 바꾼 fixture로 attempt 2의 차단도 별도 검증한다.

---

## 10. Selection Capture

기존 provider 우선순위를 유지한다.

```text
PreferUiAutomation=true
→ UIA → Clipboard fallback

PreferUiAutomation=false
→ Clipboard → UIA fallback
```

첫 release에서는 `SelectionFailureKind` 신규 enum을 만들지 않는다. `SelectedTextResult`의 성공 여부, 메시지, `CanFallback`, `ClipboardRestored`로 처리한다. `ClipboardRestored` 기본값은 true다. 단 `Sensitive` 판정 결과는 반드시 `CanFallback=false`로 반환하여 일반 capture 실패 문구로 덮이지 않게 한다. UIA-first provider도 `IsPassword=true`를 일반 null selection으로 합치지 않고 보호 메시지와 `CanFallback=false`를 직접 반환한다. failure 의미가 실제 UI나 정책에서 더 필요할 때만 enum을 도입한다.

Clipboard provider는 여러 위치에서 직접 반환하지 않는다. 정상/실패 결과를 지역변수에 축적해 단일 exit로 통일한다. 기존 `finally`는 유지하고 restore 결과만 `restoreOk` 지역변수에 기록한다. try/finally를 완전히 빠져나온 뒤 `SelectedTextResult`에 `ClipboardRestored=restoreOk`를 채워 한 번 반환한다. 취소 예외는 `finally` 복원 후 다시 전파한다. 명시적 restore와 기존 finally를 함께 두어 이중 restore하지 않는다. 이 플래그를 `CapturedSelection`에 중복 저장하지 않는다.

Palette flow에서는 capture 결과를 `CapturedSelection`으로 메모리에 보관한다.

- Palette가 닫히면 해제
- Action을 선택하면 request가 끝날 때까지 보관
- disk/history/log 저장 금지
- raw text log 금지
- Palette Action은 Translation History에 기록하지 않음

---

## 11. Clipboard와 Operation 직렬화

### 11.1 문제

현재 `operationGate`는 hotkey primary flow만 감싸고, popup에서 시작한 fallback은 gate 밖에서 실행될 수 있다. 이전 fallback paste와 새 selection capture가 겹치면 clipboard sequence가 교차할 수 있다.

### 11.2 분리된 책임

```text
operation currency
→ 어떤 API 결과와 UI 결과가 현재 interaction에 속하는지 판단

clipboardMutationGate
→ Clipboard capture/clear/restore와 SetText/paste/restore를 직렬화
```

lock order는 항상 `operationGate → clipboardMutationGate`다. clipboard gate를 보유한 code는 operation gate를 획득하지 않는다. 대기 가능한 획득은 UI thread에서 `.Wait()`, `.Result`를 쓰지 않고 `await WaitAsync()`만 사용한다. Palette Action click의 `operationGate.Wait(0)`은 대기하지 않는 fail-fast admission으로만 허용한다. 양수 timeout 또는 무기한 동기 대기는 금지한다. `SemaphoreSlim` 재진입도 금지한다.

### 11.3 Clipboard critical section

다음 전체를 공통 async clipboard helper가 가장 바깥에서 감싸고 하나의 gate 안에서 수행한다. `finally`의 restore까지 gate 안에 포함한다.

```text
Selection capture:
modifier release → target/probe #1 → snapshot → clear
→ attempt마다 target/probe #2 → Ctrl+C → read → restore

Replace:
snapshot → set result → target/current 확인 → Ctrl+V → guarded restore
```

Network 요청 동안 clipboard gate를 잡지 않는다.

capture는 gate 획득 직후와 `Ctrl+C` 직전에 target context를 확인한다. replace는 gate 획득 직후와 clipboard write 직전에 target context와 operation currency를 모두 확인한다.

stale/cancelled operation이면 clipboard를 변경하지 않고 종료한다.

Capture restore 실패와 paste restore 실패를 삼키거나 예외로 뒤집지 않는다.

```text
Capture success + restore success       → Palette 표시
Capture success + restore failure       → Palette 표시 + clipboard 복원 실패 경고
Paste success + restore success         → 정상 완료
Paste success + restore failure         → 교체 성공 + clipboard 복원 실패 경고
Paste failure                           → 자동 입력 실패
```

paste가 이미 성공한 뒤 restore 예외가 발생해도 자동 입력 실패로 보고하지 않는다. 사용자가 중복 붙여넣기를 하지 않도록 성공과 복원 실패를 분리해 전달한다.

Replace helper 경계는 `ApplyReplacementAsync`의 `CaptureSnapshot()` 이전부터 `PasteTextAsync`의 restore 완료까지다. `PasteTextAsync`만 gate로 감싸서 snapshot read가 gate 밖에 남지 않게 한다. 가능하면 snapshot 획득을 paste helper 내부로 옮겨 clipboard 접근 지점을 한곳으로 모은다.

### 11.4 Gate를 보유한 fire-and-forget task

fallback과 Palette generation task를 단순 `_ =`로 버리지 않는다.

- task body 최상위에서 `OperationCanceledException`과 예상 예외 처리
- current interaction 검증
- 종료 시 cancellation source 소유권 정리
- unobserved exception 없음
- `operationGate`를 획득한 async task는 method 첫 문장부터 `try` 경계 안에 들어감
- 모든 성공/실패/취소에서 같은 `finally` 단일 지점이 `operationGate.Release()` 수행
- UI click handler나 caller는 gate를 해제하지 않음

정확한 구현 방식은 별도 class보다 현재 `ApplicationController` 내부의 작은 helper로 시작한다.

---

## 12. Replace Safety 범위

Clipboard `SelectionLease`는 구현하지 않는다.

기존 `Alt+T`에 유지할 검증:

```text
동일 foreground HWND
동일 process ID
동일 focused HWND
current operation
clipboard sequence guard
```

제한:

- 동일 edit HWND 내부의 caret 이동을 항상 검출하지 못한다.
- 같은 text를 다른 위치에 선택한 상황을 검출하지 못한다.
- `FocusedWindow=0`은 focus 없음과 조회 실패를 구분하지 못한다.

따라서 표현은 “target window/focus 변경 방지”로 제한한다. “정확히 같은 selection 보장”이라고 표현하지 않는다.

신규 Rewrite/Summary에는 Replace를 제공하지 않는다.

UIA range identity와 `ISelectionLease`는 첫 release Spike와 구현 범위에서 제거한다.

---

## 13. Gemini 최소 Seam

### 13.1 목표

현재 번역 동작을 유지하면서 Rewrite와 Summary가 translation-only prompt 및 line marker를 사용하지 않게 한다.

### 13.2 API 구조

새 provider interface를 만들지 않는다.

`GeminiApiClient` 내부에 generic core를 추가하고, `GeminiTranslationProvider`에 API key/model/timeout/retry 정책을 재사용하는 진입점을 추가한다. Palette가 client를 직접 호출하여 provider 정책을 우회하지 않는다.

Palette generation은 `Settings.Api.Model`, `Settings.Api.RequestTimeoutSeconds`, `Settings.Api.TransientRetryCount`를 사용한다. `FallbackModel`과 fallback 자동 전환은 사용하지 않는다. `GeminiResponseParser`의 사용자 메시지는 번역 전용 접두어를 제거해 translation과 Palette 양쪽에서 자연스럽게 표시한다.

```csharp
Task<TranslationResult> GenerateAsync(
    string apiKey,
    string model,
    string systemPrompt,
    string userPrompt,
    int timeoutSeconds,
    int transientRetryCount,
    CancellationToken cancellationToken);
```

`GenerateAsync`는 항상 marker-free다. `TranslationLayoutPreserver.Prepare`, `Restore`, `SanitizeOutput`을 모두 우회하고, 정상 output을 그대로 보존하며, 내부 `WTT-LINE` marker가 발견되면 조용히 삭제하지 않고 `InvalidResponse`로 실패 처리한다. 이를 통해 요약 목록의 선행 공백과 들여쓰기를 보존한다.

`TranslateAsync`와 `GenerateAsync`는 HTTP, retry, status mapping을 담당하는 **private core 하나를 공유**한다. `TranslateAsync`는 기존 translation system prompt와 target language instruction을 조합하고 exact-line marker를 적용하며, public signature와 payload를 그대로 유지한다.

marker 적용 여부를 `GenerateAsync`의 parameter로 두지 않는다. marker는 **선택 문자열에만** 적용해야 하는데 `GenerateAsync`는 지시문과 본문이 합쳐진 `userPrompt` 하나만 받으므로, 그 경로에서는 지시문 줄까지 marker가 붙어 payload가 깨진다. 따라서 marker는 translation 전용이며 private core를 통해서만 적용한다.

### 13.3 PromptComposer

신규 상위 component는 `PromptComposer` 한 개만 허용한다.

```csharp
internal sealed record PromptPlan(
    string SystemPrompt,
    string UserPrompt);
```

Palette Action은 모두 marker-free이므로 plan에 marker flag를 두지 않는다.

Action별 정책:

| Action | System 역할 | Output | Markers |
|---|---|---|---:|
| RewriteNatural | 같은 언어와 의미를 유지해 자연스럽게 수정 | 결과 text만 | No |
| Summarize | source language로 핵심 요약 | 요약 text만 | No |

Selected text는 prompt instruction과 구분된 data block으로 전달한다.

### 13.4 기존 Translation 호환

다음은 변경하지 않는다.

- `AutomaticTargetLanguageResolver`
- `Auto`가 Gemini request 전에 해소되어야 한다는 계약
- `Select` target flow
- `TranslationPreferences`
- glossary/excluded terms
- 기존 exact-line marker와 sanitize 동작
- fallback model 정책

Rewrite/Summary에 기존 translation preference를 자동 적용하지 않는다.

Palette Action은 `TranslationInteraction`을 재사용하지 않는 one-shot generation으로 실행한다.

- fallback button/action 없음
- fallback model 자동 전환 없음
- `TranslationHistoryStore` 저장 없음
- Replace 없음
- action별 title과 번역에 종속되지 않은 timeout/network/error 문구
- Popup/Copy만 제공

---

## 14. Action Result Validation

성공 조건:

```text
HTTP success
AND parse success
AND output not null/empty/whitespace
AND internal marker leak 없음
```

Rewrite/Summary는 line marker를 사용하지 않으므로 marker가 반환되면 오류로 처리한다.

empty output과 marker leak은 실패 처리한다. 비정상적으로 긴 결과는 Replace하지 않으므로 실패시키지 않고 `Warning` Popup에 결과를 표시하며 Copy를 허용한다. 경고 기준은 `Settings.Translation.MaxInputCharacters`를 재사용하되 결과를 잘라내지 않는다. 첫 release에서 별도 output setting이나 Action별 ratio engine을 만들지 않는다.

Gemini 실제 응답 품질은 network test 없이 PASS로 판정하지 않는다.

---

## 15. Palette Lifetime

Palette session은 다음 값만 보관한다.

```text
CapturedSelection
Completed
```

규칙:

- 한 번에 Palette 하나만 표시
- 새 hotkey는 기존 Palette session을 취소하고 닫은 뒤 새 command를 처리
- timeout/Close는 session cancel 및 captured text 해제
- Action click handler는 첫 `await` 전에 `operationGate.Wait(0)`으로 fail-fast admission을 동기 수행
- admission 실패 시 Palette session을 닫고 captured text 참조를 해제한 뒤 "다른 작업이 진행 중입니다. 완료 후 다시 선택해 주세요" 안내
- admission 성공 시 같은 UI callback 안에서 `Completed` 확인/설정, timer stop, Palette hide를 동기적으로 완료
- Palette 표시 중에는 `operationGate`를 점유하지 않음
- Action click 시 source window가 여전히 active인지 확인; 달라졌으면 실행하지 않고 다시 선택하도록 안내
- gate가 이미 사용 중일 때 대기/자동 재시도/timeout 연장 상태를 만들지 않음

> busy 경로에 대한 보정(2026-09-16 실측): 모든 hotkey가 gate 획득 이전에 기존 Palette를 닫으므로,
> 팔레트가 열린 채 gate가 점유되는 상태는 사용자 조작으로 만들어지지 않는다. 즉 위 busy 규정은
> **도달 불가한 방어 규정**이다. `PaletteAdmission.Busy` 분기는 "새 hotkey가 팔레트를 닫는다"는
> 규칙이 바뀔 때의 안전망으로 유지하되, 수동 검증 항목으로 취급하지 않는다.
- Action generation 시작 시에만 captured text를 request로 넘기고 Palette session 참조 해제
- admission을 획득한 `RunPaletteActionAsync`는 method 전체를 `try/catch/finally`로 감싸고 `finally`에서 `operationGate.Release()`
- Palette generation은 `paletteOperationId`와 `paletteCancellation` 필드로 소유
- Popup close/cancel은 operation ID가 Palette ID와 일치하면 `paletteCancellation.Cancel()` 후 반환
- Palette는 `TranslationInteraction`을 만들지 않으며, cancellation 필드는 종료 `finally`에서 current ID 확인 후 정리
- 취소된/stale Palette 결과는 Popup을 다시 표시하지 않음
- timeout은 8초이며 pointer hover 동안 pause

전역 keyboard hook이나 임시 `RegisterHotKey`로 Palette navigation을 구현하지 않는다.

---

## 16. Settings

첫 release에서 추가할 값은 하나다.

```csharp
public string ActionPaletteHotkey { get; set; } = "Ctrl+Alt+A"; // empty = disabled
```

기존 `TranslationSettings`에 두어 schema v2 migration을 만들지 않는다.

설정 전용 수정 지점:

- `AppSettings`
- `SettingsValidator`
- `SettingsForm` load/build
- `ApplicationController.ApplySettings`
- hotkey register/rollback
- settings clone/copy
- default config와 user documentation

### 16.1 Settings 복사

현재 수동 field-by-field `CopySettings`는 새 field 누락 위험이 있다.

단순 JSON round-trip 결과를 반환하는 것만으로 끝내지 않고, 기존 `Settings` root instance를 유지하면서 clone한 section을 교체한다.

```text
clone candidate
→ target.General = clone.General
→ target.Translation = clone.Translation
→ target.Api = clone.Api
```

실제 reference consumer를 조사한 뒤 root instance 교체 가능 여부를 확인한다.

충돌 normalization과 rollback 규칙:

- core pair 충돌은 기존 기본값으로 복구
- Palette와 core 충돌은 Palette만 disabled
- optional Palette는 `ValidOptionalHotkeyOrEmpty`로 normalize하고 빈 값은 parse/register하지 않음
- `TryRollbackSettings`는 core와 Palette 등록을 모두 previous 값으로 복구
- `configuration.Save(candidate)`가 normalize한 최종 candidate를 기준으로 이후 작업 수행
- clipboard/logger/history `Configure` 인자는 아직 갱신되지 않은 `Settings`가 아니라 `candidate`에서 읽음
- 설정 section clone은 Save/Normalize 이후 candidate에서 생성
- 설정 값 반영은 clipboard/logger/history 등 실패 가능한 configure 호출이 모두 성공한 뒤 마지막에 수행
- 실패 시 native hotkey, startup flag, API key, disk config, service configure 상태, memory Settings를 previous 값으로 복구

### 16.2 손상 Recovery

가벼운 범위에서 다음만 수행한다.

- load 실패 시 반환 defaults도 `SettingsValidator.Normalize` 적용
- parse 실패 원본은 overwrite 전에 `.bak` 보존
- 사용자에게 설정 복구 알림

JSON property별 부분 복구 engine은 만들지 않는다.

---

## 17. 기존 별도 결함 처리

Action 작업 전에 다음 기존 결함을 작은 regression slice로 분리한다.

1. 미지 hotkey ID가 Replace로 분기되는 문제
2. fallback task와 새 clipboard operation 경쟁
3. fire-and-forget fallback 예외 관찰 누락
4. `TranslationHistoryChanged`가 `EnableLogging`을 확인하는 문제
5. 설정 parse 실패 defaults 미정규화
6. 수동 `CopySettings` 필드 누락 위험
7. capture/paste clipboard restore 결과가 반대로 보고되는 문제
8. optional Palette hotkey 실패가 core registration을 해칠 수 있는 문제

이 수정은 Action 기능과 섞지 않고 기존 동작을 보존하는 test와 함께 먼저 완료한다.

---

## 18. 구현 Slice

### Slice A. 기존 파괴적 위험 차단

- explicit hotkey command mapping
- unknown ID ignore test
- startup의 세 hotkey 개별 등록과 settings apply transaction 분리
- Palette/core 충돌 시 Palette만 disable하는 normalization
- rollback에서 core/Palette 등록과 Settings 모두 복구
- service configure는 `candidate` 값을 사용하고 실패 시 previous 값으로 복구
- clipboard mutation 공통 async helper와 lock order 확정
- Replace helper는 snapshot 획득부터 restore까지 포괄
- capture/paste restore 결과의 성공/경고/실패 분리
- fallback task exception/cancellation ownership
- history notification flag 수정
- settings clone/copy 누락 방지
- config load failure normalize/backup

신규 Action이나 Palette를 추가하지 않는다.

### Slice B. Sensitive Probe

- `SensitiveProbeResult`
- single-flight probe worker guard
- Clipboard provider 내부 probe #1과 retry loop 매 attempt의 probe #2
- modifier release와 snapshot/clear/`Ctrl+C` 순서 재배치
- bounded execution
- Clipboard copy timeout과 분리된 probe budget
- probe #1 Sensitive에서 clipboard/API 호출 0회
- 각 attempt의 probe #2 Sensitive에서 해당 `Ctrl+C`/API 호출 0회와 snapshot 복원
- Sensitive는 `CanFallback=false`
- UIA provider password 결과도 `CanFallback=false`
- Safe/Unknown 호환 동작

### Slice C. Gemini Generic Core

- `GenerateAsync` core
- `GeminiTranslationProvider` generic entry point
- 기존 `TranslateAsync` wrapper
- `PromptComposer`
- 기존 번역 payload/test 호환
- Rewrite/Summary payload에 translation rule/marker 없음
- `GenerateAsync`에서 Prepare/Restore/Sanitize 완전 우회
- action-neutral error/title

### Slice D. Quick Action Palette

- third fixed hotkey
- capture-before-show
- fixed two buttons
- synchronous fail-fast gate admission and session claim before first await
- Palette generation task의 `try/catch/finally` 단일 gate release
- Palette operation ID/cancellation과 Popup close 연결
- Palette 표시 중 operation gate 해제, Action click 시 재획득
- source window 재검증과 busy retry UX
- 8초 timeout/hover pause/close/session cleanup
- Rewrite/Summary Popup/Copy
- fallback/Replace/history 없음
- 기존 Popup을 action별 title로 재사용

### Slice E. Release Validation

- 기존 hotkey regression
- sensitive probe manual/component evidence
- clipboard concurrency tests
- actual Gemini Action contract test
- Palette UX manual matrix
- build/test/package

Slice별 승인 없이 다음 Slice로 넘어가지 않는다.

### 18.1 Slice별 주요 파일

| Slice | 주요 파일 |
|---|---|
| A | `App/ApplicationController.cs`, `Hotkeys/GlobalHotkeyService.cs`, `Clipboard/ClipboardService.cs`, `Selection/ClipboardSelectedTextProvider.cs`, `Selection/SelectedTextResult.cs`, `Configuration/SettingsValidator.cs`, `Configuration/ConfigurationService.cs`, `Configuration/AppSettings.cs`, `UI/SettingsForm.cs`, `UI/TrayApplicationContext.cs` |
| B | `Selection/ClipboardSelectedTextProvider.cs`, `Selection/UiAutomationSelectedTextProvider.cs`, `Selection/SelectedTextResult.cs` |
| C | `Translation/Gemini/GeminiApiClient.cs`, `Translation/Gemini/GeminiTranslationProvider.cs`, `Translation/Gemini/GeminiResponseParser.cs`, 신규 `PromptComposer.cs` |
| D | `App/ApplicationController.cs`, 신규 `UI/QuickActionPaletteForm.cs`, `UI/TrayApplicationContext.cs`, `UI/TranslationResultPopupForm.cs`, settings/default config/user documentation |
| E | `tests/WindowsTrayTranslator.Tests/`, build/package scripts와 release artifacts |

실제 namespace root는 `src/WindowsTrayTranslator`다. 테스트 파일은 각 slice에서 함께 추가하며 Slice E까지 미루지 않는다.

---

## 19. 필수 Test

### Unit/Component

- unknown hotkey ID → clipboard/API 0회
- core hotkey 중복 validation
- Palette/core 충돌 → core 보존, Palette disabled
- 빈/잘못된 Palette hotkey → disabled 상태 load/save 유지
- startup Alt+T 충돌 → 성공한 Alt+R 유지 및 실패 warning 집계
- Palette hotkey registration 실패 → Alt+R/Alt+T 유지
- settings apply 후 실패 → core/Palette 등록과 Settings previous 값 복구
- settings configure가 normalized candidate 값을 즉시 사용
- probe #1 Sensitive → Clipboard/API 0회
- attempt 1 capture timeout 후 focus를 password로 전환 → attempt 2 `Ctrl+C`/API 0회 + snapshot restore
- Unknown → 기존 provider policy 유지
- fallback paste 대기 중 새 capture → clipboard critical section 교차 없음
- stale operation → clipboard write 0회
- cancellation → unobserved exception 없음
- Translate payload 기존 prompt/marker 유지
- Rewrite payload에 translation target/marker 없음
- Summary payload에 translation target/marker 없음
- empty/marker-leak output 차단
- output warning 기준 초과 → Warning Popup + 전체 결과 Copy 가능
- marker 미사용 output의 leading indentation 보존
- Palette Action → fallback/history/Replace 0회
- Action click/timeout 경합 → 단일 완료
- Palette generation 진입부 예외/취소 → operation gate 반드시 회수
- Popup close → Palette Gemini request 취소, stale Popup 0회
- Palette source window 변경 → generation 0회
- busy gate → session 종료, captured text 해제, 재선택 안내
- Palette timeout/Close → captured text reference 해제
- capture/paste 성공 후 restore 실패 → 성공과 경고를 구분
- config parse failure → normalized defaults + backup
- settings save 후 Palette hotkey 즉시 적용

### Manual

- Notepad, Chrome/Edge textarea, Word, VS Code에서 Palette hotkey capture
- no-activate Palette button click 동작
- Palette 표시 중 원래 app focus 유지 여부 기록
- Palette timeout/Close 후 hotkey 정상 복구
- Palette 표시 후 다른 창으로 전환하고 Action click → 재선택 안내
- 8초 timeout과 hover pause UX
- known password field에서 차단 문구와 clipboard 불변
- UIA unsupported app에서 기존 Clipboard-first 호환
- 관리자 권한 target 차단
- multi-monitor/negative coordinate 위치
- API key 없음/network failure UX

Runtime evidence가 없는 항목은 PASS로 기록하지 않는다.

---

## 20. 성능과 Privacy

- mouse/keyboard low-level hook 없음
- 기존 `RegisterHotKey` mechanism만 사용
- background polling 없음
- Palette가 닫힌 동안 timer 없음
- API 호출은 Action 선택 후에만 수행
- capture된 raw text는 memory에만 보관하고 session 종료 시 해제
- Palette Action 원문과 결과는 Translation History에 기록하지 않음
- raw text/custom result는 운영 log에 기록하지 않음
- Translation History가 활성화된 경우에도 기존 Alt+R/Alt+T 저장 계약만 유지하며 Palette Action은 저장하지 않음
- Gemini direct transmission 사실을 사용자 문서에서 유지

Sensitive probe latency와 UIA timeout worker 누적은 구현 전/중 실측한다.

---

## 21. Release Gate

### Gate 1. 기존 동작

- `Alt+R`, `Alt+T`, Auto, Select, ReplaceFormat, fallback regression PASS
- Palette registration 실패 시 core hotkey PASS
- 한 core hotkey 충돌 시 성공한 다른 core hotkey 유지
- probe `Unknown`/timeout에서 기존 Alt+R/Alt+T capture 성공률과 지연 회귀 허용 범위 충족
- unknown hotkey destructive path 제거
- clipboard capture/paste concurrency PASS

### Gate 2. Sensitive

- probe #1 known password field에서 clipboard, `Ctrl+C`, Gemini 호출 0회
- probe #2 Sensitive 전환에서 `Ctrl+C`, Gemini 호출 0회와 clipboard 복원
- attempt 1 timeout 후 password 전환 시 attempt 2 `Ctrl+C`, Gemini 호출 0회
- probe timeout/unsupported가 app hang을 만들지 않음
- `Unknown` 한계를 문서화

### Gate 3. Generation

- 기존 translation payload 의미 보존
- Rewrite/Summary에서 translation-only prompt와 marker 제거
- empty/marker leak 실패 및 긴 output 경고 표시

### Gate 4. Palette

- capture 성공 전 UI 미표시
- fixed Action 두 개만 표시
- 전역 mouse/keyboard hook 없음
- no-activate click/timeout/hover pause/cleanup manual PASS
- timeout과 Action click 동시 경합에서 단일 완료
- generation 예외/Popup cancel 뒤 operation gate 회수
- source window 변경 후 Action 실행 차단
- Palette 실패 후 기존 hotkey 정상

### Gate 5. Release

- build warning/error 0
- 전체 automated test PASS
- `git diff --check` PASS
- installer/package/version/hash 검증
- 기존 사용자 설정 upgrade 검증

---

## 22. 남은 판정 불가 항목

- 모든 application의 password field가 UIA `IsPassword`를 제공하는지
- Sensitive probe의 앱별 latency와 timeout 빈도
- 현재 UIA `Task.Run + WaitAsync` timeout 후 worker 누적 영향
- no-activate Palette click의 앱별 focus/selection 표시 차이
- Gemini Rewrite/Summary 실제 응답 품질
- clipboard mutation gate 설계의 실제 cancellation timing
- clipboard snapshot이 큰 multi-format data에서 probe #1/#2 사이에 드는 실측 시간
- Chromium/Electron의 동일 HWND 내부 password focus 전환에서 probe #2 신뢰성
- probe 추가 전후 Alt+R/Alt+T 성공률과 latency 변화
- attempt 1 timeout 후 attempt 2 직전 focus 전환 재현과 차단
- input 4,000~5,000자 구간 RewriteNatural 출력/입력 길이 분포
- capture당 최대 3회 probe 기준 누적 latency와 포기된 UIA worker 생존 시간

이 항목은 구현 주장이나 green unit test만으로 PASS 처리하지 않는다.

---

## 23. 최종 Scope 문장

```text
기존 번역 단축키 두 개를 안전하게 유지한다.
별도 단축키 하나로 선택 text를 먼저 capture한다.
고정된 두 Action 중 하나를 mouse로 선택한다.
신규 Action 결과는 Popup으로 보여주고 복사할 수 있다.
자동 mouse 감지, 범용 Action 관리, 신규 Replace는 만들지 않는다.
```
