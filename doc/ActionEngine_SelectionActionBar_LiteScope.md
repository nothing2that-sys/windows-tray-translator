# Windows Tray Translator

# Action Engine & Selection Action Bar Lite Scope

> **대체된 문서:** 적대적 검증과 후속 결정에 따라 자동 Selection Action Bar를 제거하고 단축키 기반 Quick Action Palette로 변경했다. 현재 구현 범위와 순서는 `doc/ActionEngine_QuickActionPalette_Design.md`를 따른다.

## 1. 문서 목적과 우선순위

이 문서는 Action Engine과 Selection Action Bar를 실제로 구현할 때 적용할 최소 범위를 고정한다.

관련 상세 문서:

- `doc/ActionEngine_SelectionActionBar_Design.md`

두 문서가 충돌하면 다음 우선순위를 적용한다.

```text
구현 범위와 출시 순서: Lite Scope 우선
Safety Contract와 기술 검토 항목: 상세 설계 참고
실제 동작 판단: 현재 코드와 실측 결과 우선
```

상세 설계에 존재한다는 이유만으로 기능을 구현하지 않는다. Lite Scope에 포함되지 않은 항목은 별도 승인 전까지 범위 밖이다.

---

## 2. 제품 원칙

이 확장의 목표는 많은 기능을 가진 AI client가 아니다.

> Windows에서 선택한 text를 한두 번의 입력으로 원하는 형태로 바꾸고, 현재 작업 흐름에 안전하게 돌려주는 가벼운 tray utility

기능 판단 기준:

1. 기존 `Alt+R`, `Alt+T`보다 사용 흐름을 실제로 개선하는가?
2. 평상시 CPU, memory, background activity를 의미 있게 늘리지 않는가?
3. 설정과 UI를 설명 없이 이해할 수 있는가?
4. 잘못된 위치에 text를 입력할 위험을 높이지 않는가?
5. Windows 또는 대상 application이 이미 잘 제공하는 기능을 중복 구현하지 않는가?

하나라도 만족하지 못하면 첫 release에서 제외한다.

---

## 3. Lite Release 포함 범위

### 3.1 기존 사용자 계약 유지

```text
Alt+R
→ 선택 text 읽기 번역
→ Popup

Alt+T
→ 선택 text 영어 번역
→ 안전 검증
→ Replace 또는 Copy fallback
```

- 단축키 기본값과 외부 동작을 유지한다.
- 기존 target language, Auto routing, ReplaceFormat, model/fallback 설정을 유지한다.
- Action Engine 도입 때문에 기존 사용자가 설정을 다시 만들게 하지 않는다.

### 3.2 최소 Action Engine

첫 release에서 필요한 Action은 네 개다.

| ID | 표시명 | Output | 비고 |
|---|---|---|---|
| `builtin.translate.read` | 번역 | Popup | 기존 `Alt+R` |
| `builtin.translate.english` | 영어로 작성 | Replace | 기존 `Alt+T` |
| `builtin.rewrite.natural` | 자연스럽게 다듬기 | Preview/Replace | 신규 |
| `builtin.summarize` | 요약 | Popup | 신규 |

`비즈니스 영어`, `설명`, `문법 교정`은 첫 release에 넣지 않는다. 네 Action의 사용성과 prompt/output 안정성을 확인한 뒤 추가 여부를 판단한다.

### 3.3 Output

지원 범위:

```text
Popup
ReplaceSelection
Clipboard
```

원칙:

- 기존 `Alt+T`만 호환성을 위해 `Immediate`를 유지할 수 있다.
- 신규 Replace Action은 `PreviewFirst`가 기본이다.
- selection 또는 target을 확실히 확인할 수 없으면 paste하지 않고 Popup/Copy로 전환한다.

### 3.4 Mini Selection Action Bar

형태는 하나만 지원한다.

```text
[번역] [⋯]
```

`⋯` 메뉴:

```text
번역
영어로 작성
자연스럽게 다듬기
요약
----------------
이 앱에서 표시하지 않기
```

제외:

- Favorite
- Recent Action
- 검색
- 동적 정렬
- icon-only mode
- Bubble/Full mode
- animation customization

Action Bar는 opt-in experimental 기능으로 시작한다.

```text
기존 사용자 upgrade: OFF
신규 사용자: 명시적 선택 후 ON
기능 OFF: global mouse hook 미등록
```

---

## 4. 반드시 유지할 Safety

Safety는 무거운 부가 기능이 아니라 제품 핵심이다.

### 4.1 SensitiveInput

```text
UI Automation IsPassword=true
→ SensitiveInput
→ Clipboard fallback 금지
→ API 호출 금지
→ "보호된 입력 영역에서는 사용할 수 없습니다" 표시
```

Password 감지와 “선택 없음”을 같은 실패로 처리하지 않는다.

### 4.2 Replace 검증

Replace 직전 최소 조건:

```text
동일 foreground window
AND 동일 process
AND 동일 focused window
AND 현재 operation
AND selection 재검증 성공
AND clipboard 외부 변경 없음
```

UI Automation selection identity가 실제 앱에서 안정적으로 검증되지 않으면 강한 보증으로 표시하지 않는다.

Clipboard 방식은 동일 selection text를 재capture해 비교하되, 같은 text가 다른 위치에 선택된 경우까지 구분하지 못한다는 제한을 인정한다.

판정 결과:

```text
확실함 → Replace 허용
변경됨 → Replace 금지
판정 불가 → Replace 금지
```

### 4.3 Result validation

API HTTP 성공만으로 Action 성공으로 판단하지 않는다.

최소 검증:

- null/empty/whitespace-only 결과 차단
- 내부 line marker 노출 차단
- 비정상적으로 큰 결과의 Immediate Replace 금지
- Action별 허용 output 위반 차단

위험 결과는 가능한 경우 Preview/Copy로 전환한다.

---

## 5. 최소 Architecture

첫 구현에서 필요한 상위 component는 다음으로 제한한다.

```text
ActionCatalog
TextActionCoordinator
PromptComposer
OutputRouter
SelectionSnapshot / SelectionValidator
```

책임:

| Component | 책임 |
|---|---|
| `ActionCatalog` | 네 built-in Action 제공 |
| `TextActionCoordinator` | capture → generation → output 순서와 single-operation 관리 |
| `PromptComposer` | Action별 prompt와 layout policy 생성 |
| `OutputRouter` | Popup/Clipboard/Replace 분기 |
| `SelectionValidator` | SensitiveInput과 Replace 직전 context 확인 |

초기 model에 넣지 않는 field:

```text
InputType
ExecutionType
ApplicationProfile
Recent/UseCount
Action별 Model/FallbackModel
Import/Export metadata
Undo metadata
Insert mutation mode
```

실제 기능이 승인된 시점에 schema를 확장한다.

---

## 6. 설정 원칙

첫 내부 migration에서는 기존 설정을 authoritative source로 유지한다.

```text
Translation.ReadHotkey
Translation.ReplaceHotkey
Translation.ReadTargetLanguage
Translation.ReplaceTargetLanguage
Translation.ReplaceFormat
```

Action Engine adapter가 이 값을 읽도록 한다. 첫 단계부터 대규모 schema v2 migration을 강제하지 않는다.

Selection Action Bar를 구현할 때 필요한 최소 설정만 추가한다.

```json
{
  "SelectionActionBar": {
    "Enabled": false,
    "TimeoutMs": 4000,
    "ExcludedProcesses": []
  }
}
```

Custom Action을 실제로 승인하기 전에는 `Actions.Custom`, dynamic hotkey collection, usage metadata를 저장하지 않는다.

설정 항목 하나가 잘못됐다고 전체 설정을 초기화하지 않는다. 손상된 entry만 기본값으로 복구하고 원본 raw text를 log하지 않는다.

---

## 7. 첫 Release에서 제외

다음 기능은 상세 설계에 언급되어 있어도 구현하지 않는다.

```text
Custom Action Editor
Action별 Hotkey
Action Launcher
Application Profile
Replace Undo
Regenerate
Insert Before / Insert After
Local Action
Recent/Favorite 자동 UI
Action Template
Preset Import / Export
사용 통계
외부 Telemetry
Action별 Model/Fallback 설정
OCR/Screenshot Capture
Voice/Speech
Plugin Runtime
PowerShell/Script/Shell 실행
Cloud Sync
```

후속 기능은 실제 사용 중 반복되는 불편과 측정 근거가 있을 때만 추가한다.

### Custom Action 후속 검토 조건

네 built-in Action과 Action Bar가 안정된 뒤에도 사용자가 반복적으로 새로운 prompt를 요구할 때만 검토한다.

승인 시에도 첫 Custom Action UI는 다음으로 제한한다.

```text
이름
Instruction
Output: Popup / Replace / Clipboard
Action Bar 표시 여부
```

개별 model, import/export, template, script 실행은 포함하지 않는다.

---

## 8. Background/성능 예산

Tray utility이므로 기능이 꺼져 있을 때 비용이 없어야 한다.

- Action Bar OFF이면 mouse hook을 등록하지 않는다.
- polling timer를 상시 운용하지 않는다.
- hook callback에서 Clipboard, UI Automation, file I/O, process path 조회, network, blocking wait를 수행하지 않는다.
- Action 실행 전에는 Gemini API를 호출하지 않는다.
- 사용 통계 database나 background indexing을 만들지 않는다.
- timeout된 UI Automation 작업을 gesture마다 무제한 생성하지 않는다.
- Action Bar 등록 실패가 `Alt+R`, `Alt+T`를 중단시키지 않는다.

CPU/memory 수치는 prototype 실측 후 release gate로 확정하며 측정 전 PASS로 표기하지 않는다.

---

## 9. 구현 순서

### Slice 0-A. SensitiveInput 구분

- typed selection failure 도입
- password failure는 fallback 금지
- 사용자 메시지 분리
- Clipboard/API 호출 0회 test

완료 전 다음 단계로 넘어가지 않는다.

### Slice 0-B. Replace 재검증

- 현재 window/focus 검증 유지
- selection 재검증 seam 추가
- 같은 HWND에서 caret/selection 변경 test
- 판정 불가 시 Copy fallback

UI Automation range identity는 먼저 기술 Spike로 검증한다. 불안정하면 보증 수준을 낮추고 자동 Replace를 확대하지 않는다.

### Slice 1-A. 기존 Read Action adapter

- `Alt+R`을 최소 Action pipeline으로 이동
- Auto target, language Select, popup, fallback model 동작 보존
- 외부 UX 변화 없음

### Slice 1-B. 기존 Replace Action adapter

- `Alt+T`을 같은 pipeline으로 이동
- ReplaceFormat과 clipboard restore 동작 보존
- Slice 0-B safety 적용

### Slice 2. Built-in 두 개 추가

- 자연스럽게 다듬기
- 요약
- Action별 prompt/layout/result validation test

### Slice 3. Experimental Mini Action Bar

- `[번역][⋯]`
- four-action fixed menu
- own-click suppression
- multi-monitor/DPI
- `이 앱에서 표시하지 않기`
- feature OFF 시 hook 미등록

### Slice 4. 유지 또는 제거 결정

Action Bar는 구현했다는 이유로 기본 ON으로 바꾸지 않는다.

앱별 selection 유지, false positive, hook 비용을 반복 측정한다. 기준을 통과하지 못하면 experimental OFF를 유지하거나 기능을 제거한다.

---

## 10. Release 판정

### 필수 PASS

- 기존 `Alt+R`, `Alt+T` regression
- Password field에서 Clipboard/API 접근 없음
- focus/window/selection 변경 시 잘못된 paste 없음
- clipboard snapshot/restore 기존 계약 보존
- 네 Action의 prompt/output contract
- feature OFF 시 hook과 background 작업 없음
- Action Bar 실패 후 기존 hotkey 정상 동작

### 실측 전 판정 불가

- Word/Chrome/Teams/Visual Studio에서 no-activate bar click 후 selection 유지
- UI Automation range identity의 앱별 안정성
- 일반 mouse 사용 중 false-positive 빈도
- mixed-DPI monitor 위치 정확성
- Gemini 실제 응답 품질과 expansion guard 기준

판정 불가 항목을 PASS로 간주하지 않는다.

---

## 11. 최종 완료 상태

첫 완료 목표는 다음 한 문장으로 제한한다.

> 기존 Alt+R/Alt+T가 그대로 안정적으로 동작하고, 선택적으로 켤 수 있는 작은 `[번역][⋯]` UI에서 번역·영어 작성·자연스럽게 다듬기·요약을 실행하며, Replace 대상이 확실하지 않으면 자동 입력하지 않고 결과를 보여주거나 복사할 수 있다.

이 상태에서 사용자 불편이 확인되지 않으면 기능을 더 추가하지 않는다.
