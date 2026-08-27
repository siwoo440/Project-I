# Project I 개발 일지

## Day 6 — 빠른 슬롯·재바인딩 입력 및 Canvas 인벤토리 UI 구현

- 날짜: 2026-08-27
- 개발 단계: Phase 2 — 플레이어 조작과 상호작용
- 기준 커밋: `86eafbc47405aa07487a06e428eb7565fedf8b61`
- 기준 커밋 메시지: `:6:`
- 이전 기준: `23bd67be548bef3198ea3cec7ce8695b0c8ba10c`

---

## 개발 목표

5일차에서 구현한 월드 아이템 운반 시스템을
빠른 슬롯 6칸 기반 인벤토리 구조로 확장한다.

한손 아이템은 자유롭게 슬롯을 전환할 수 있도록 하고,
양손 아이템은 실제로 들고 있는 동안 현재 슬롯을 잠그도록 구성한다.

또한 모든 게임플레이 입력을 Input Action 기반으로 통일하여
향후 설정 화면에서 키를 재지정할 수 있는 구조를 준비하고,
빠른 슬롯 HUD를 IMGUI가 아닌 실제 Canvas UI로 구성한다.

---

## 구현 내용

### 1. 빠른 슬롯 6칸

플레이어 인벤토리에 고정 6칸 빠른 슬롯을 추가했다.

구조:

- Slot 1
- Slot 2
- Slot 3
- Slot 4
- Slot 5
- Slot 6

월드 아이템을 F로 획득하면
첫 번째 빈 슬롯에 저장되고 해당 슬롯이 즉시 선택된다.

선택되지 않은 아이템은 `InventoryStorage` 아래에 숨겨 보관한다.

---

### 2. 월드 아이템 ↔ 인벤토리 연결

5일차의 직접 운반 구조를 다음 흐름으로 변경했다.

`WorldItem → PlayerInventory → 선택 슬롯 → PlayerCarryController`

아이템 획득 시:

1. 빈 슬롯 확인
2. 아이템을 인벤토리 보관 상태로 전환
3. 첫 빈 슬롯에 저장
4. 새 슬롯 선택
5. 선택 아이템을 CarryPoint에 표시

현재 슬롯의 아이템을 버리면
해당 슬롯이 비워지고 아이템은 다시 월드 물리 상태로 돌아간다.

---

### 3. OneHand 슬롯 전환

`CarryType.OneHand` 아이템은
들고 있는 상태에서도 자유롭게 다른 슬롯으로 전환할 수 있다.

슬롯을 변경하면 기존 한손 아이템은 삭제되지 않고
자신의 빠른 슬롯에 그대로 보관된다.

화면에서는 선택된 슬롯의 아이템만 표시된다.

---

### 4. TwoHand 슬롯 잠금

`CarryType.TwoHand`는 별도의 장비 슬롯이 아니라
운반 중 슬롯 변경을 제한하는 규칙으로 유지했다.

양손 아이템을 실제로 들고 있으면:

- 숫자키 슬롯 전환 차단
- 마우스 휠 슬롯 전환 차단
- 현재 슬롯 LOCK 상태

Q로 아이템을 버리면:

- 아이템 월드 복귀
- 현재 슬롯 Empty
- 슬롯 LOCK 해제

---

### 5. 숫자키 1~6 직접 슬롯 선택

빠른 슬롯에 전용 Input Action을 추가했다.

- `Slot1`
- `Slot2`
- `Slot3`
- `Slot4`
- `Slot5`
- `Slot6`

숫자키를 누르면 해당 슬롯으로 즉시 변경된다.

기본 입력:

- `1` → Slot 1
- `2` → Slot 2
- `3` → Slot 3
- `4` → Slot 4
- `5` → Slot 5
- `6` → Slot 6

양손 아이템 운반 중에는 직접 선택도 차단된다.

---

### 6. 마우스 휠 슬롯 전환

`SlotScroll` Input Action을 추가했다.

마우스 휠로 빠른 슬롯을 원형 순환 방식으로 변경할 수 있다.

예:

`1 → 2 → 3 → 4 → 5 → 6 → 1`

반대 방향으로 돌리면 역순으로 이동한다.

---

### 7. 버리기 키 Q

기존 G 직접 입력을 제거하고
`Drop` Input Action으로 통일했다.

기본 키:

`Q`

동작:

`Q → 현재 선택 아이템 버리기 → 슬롯 Empty`

아이템은 기존 5일차 규칙대로
플레이어 바로 앞의 낮은 위치에 떨어진다.

---

### 8. 아이템 사용 입력

좌클릭은 `Use` Input Action으로 구성했다.

선택된 아이템이 `IUsableItem`을 구현하면
좌클릭 입력으로 해당 아이템의 `Use()`가 실행된다.

이번 단계에서는 실제 전투·도구 기능 대신
`TestUsableItem`을 통해 기본 사용 흐름을 검증한다.

---

### 9. 모든 게임플레이 입력 Input Action 기반 통일

직접 `Keyboard.current`, `Mouse.current`를 읽던 입력을
Input Action 이름 기반 구조로 변경했다.

주요 액션:

- `Move`
- `Look`
- `Sprint`
- `Jump`
- `Crouch`
- `Interact`
- `Use`
- `Drop`
- `Slot1`
- `Slot2`
- `Slot3`
- `Slot4`
- `Slot5`
- `Slot6`
- `SlotScroll`
- `Pause`

이 구조를 통해 게임플레이 코드는
실제 키가 Q인지 R인지 알 필요 없이 액션만 사용한다.

---

### 10. 키 재바인딩 기반

`InputRebindService`를 추가했다.

지원 구조:

- 현재 Binding 표시
- 특정 Binding 대화형 재지정
- Binding Override 저장
- 실행 시 저장된 Override 로드
- 특정 키 기본값 복구
- 전체 키 설정 초기화

재바인딩 데이터는 PlayerPrefs에 JSON으로 저장한다.

따라서 이후 설정 UI에서는
`PerformInteractiveRebinding()`을 이용해 실제 키 설정 화면을 연결할 수 있다.

---

### 11. Input Action 자동 구성

Day 6 Editor Fix에서
Unity Input System API를 사용하여 필요한 액션과 기본 Binding을 구성한다.

기존처럼 JSON 문자열을 정규식으로 직접 수정하지 않고:

`InputActionAsset.FromJson() → Action/Binding 구성 → ToJson()`

방식으로 처리한다.

주요 기본 키:

- Interact → F
- Crouch → Left Ctrl
- Jump → Space
- Sprint → Left Shift
- Use → Left Mouse
- Drop → Q
- Pause → Escape
- SlotScroll → Mouse Wheel
- Slot1~6 → 숫자키 1~6

---

### 12. Canvas 기반 빠른 슬롯 UI

기존 `OnGUI()` / `GUI.Box()` 방식의 임시 HUD를 제거했다.

새 UI 구조:

`PlayerHUDCanvas`
- `QuickSlotPanel`
  - `Slot_1`
  - `Slot_2`
  - `Slot_3`
  - `Slot_4`
  - `Slot_5`
  - `Slot_6`

각 슬롯은 실제 Unity UI의:

- Canvas
- CanvasScaler
- GraphicRaycaster
- Image
- Text

를 사용한다.

Canvas는 `Screen Space - Overlay` 방식이며
기준 해상도는 1920 × 1080으로 구성했다.

---

### 13. 슬롯 번호 왼쪽 위 표시

각 빠른 슬롯에는 별도의 `Number` Text를 배치했다.

슬롯 번호는 각 칸의 왼쪽 위에 표시된다.

예:

```text
┌─────────┐
│1        │
│         │
│   검    │
│         │
└─────────┘
```

선택된 슬롯은 배경색으로 구분하고,
양손 아이템으로 잠긴 슬롯에는 `LOCK` 문구를 표시한다.

---

### 14. Rigidbody Kinematic 경고 수정

아이템을 인벤토리에 보관한 뒤 다시 CarryPoint에 표시할 때 발생했던:

`Setting linear velocity of a kinematic body is not supported.`

경고를 수정했다.

`ClearMotionIfDynamic()`을 추가하여
Rigidbody가 Dynamic 상태일 때만:

- `linearVelocity`
- `angularVelocity`

를 초기화한다.

이미 Kinematic 상태라면 velocity 값을 쓰지 않는다.

---

### 15. 09_InventoryTest

빠른 슬롯 통합 검증을 위한
`09_InventoryTest` 구역을 구성했다.

시험 아이템:

- 검
- 손전등
- 열쇠
- 작은 도구
- 회복 아이템
- 작은 회수품
- 곡괭이

앞의 6개 OneHand 아이템으로 슬롯을 모두 채우는 시험과
TwoHand 곡괭이의 슬롯 잠금 규칙을 시험할 수 있다.

---

## 주요 생성 파일

- `Assets/ProjectI/Scripts/Items/QuickSlot.cs`
- `Assets/ProjectI/Scripts/Items/QuickSlotRules.cs`
- `Assets/ProjectI/Scripts/Items/PlayerInventory.cs`
- `Assets/ProjectI/Scripts/Items/IUsableItem.cs`
- `Assets/ProjectI/Scripts/Items/TestUsableItem.cs`
- `Assets/ProjectI/Scripts/Items/QuickSlotHud.cs`
- `Assets/ProjectI/Scripts/Player/GameplayInputActions.cs`
- `Assets/ProjectI/Scripts/Player/InputRebindService.cs`
- `Assets/ProjectI/Editor/Phase2Day6Setup.cs`
- `Assets/ProjectI/Editor/Phase2Day6Validator.cs`
- `Assets/ProjectI/Editor/Phase2Day6InputUiFix.cs`
- `Assets/ProjectI/Editor/Phase2Day6InputUiValidator.cs`

## 주요 수정 파일

- `Assets/InputSystem_Actions.inputactions`
- `Assets/ProjectI/Scenes/ExplorationOffice.unity`
- `Assets/ProjectI/Scripts/Player/PlayerInputReader.cs`
- `Assets/ProjectI/Scripts/Player/PlayerLook.cs`
- `Assets/ProjectI/Scripts/Items/WorldItem.cs`
- `Assets/ProjectI/Scripts/Items/PlayerCarryController.cs`

---

## 저장소 점검 결과

최신 커밋 `86eafbc4`를 기준으로 6일차 구현을 확인했다.

확인된 사항:

- 빠른 슬롯 6칸
- F 획득 후 첫 빈 슬롯 저장
- OneHand 자유 슬롯 전환
- TwoHand 운반 중 슬롯 잠금
- Q Drop Input Action
- 숫자키 1~6 직접 슬롯 선택
- 마우스 휠 슬롯 이동
- 좌클릭 Use Input Action
- 게임플레이 입력 Input Action 기반 통일
- 향후 설정 화면용 InputRebindService
- Canvas 기반 빠른 슬롯 HUD
- 각 슬롯 번호 왼쪽 위 배치
- 선택 / LOCK 시각 표시
- Kinematic Rigidbody velocity 경고 방지

GitHub Commit Status는 등록된 CI 검사가 없는 상태다.

따라서 저장소 코드 검토와 별개로
Unity Editor의 실제 Compile 및 Play Mode 결과가 최종 검증 기준이다.

### 비차단 정리 항목

현재 `InputSystem_Actions.inputactions`의 Slot1~Slot6에는
기존 `<Keyboard>/digit1~6` Binding과
추가된 `<Keyboard>/1~6` Binding이 함께 존재한다.

현재 슬롯 선택 기능을 막는 문제는 아니지만
향후 입력 설정 정리 시 하나의 기본 Binding 체계로 통일하는 것이 좋다.

또한 `PlayerInventory` 일부 주석에는
이전 기본 키였던 `G` 표현이 남아 있으나
실제 동작은 `Drop` Input Action의 기본 Q를 사용한다.

---

## 로컬 최종 확인 항목

- Console Error 0개
- Kinematic Rigidbody velocity 경고 재발 없음
- F 아이템 획득
- 빈 슬롯 자동 저장
- 1~6 숫자키 직접 선택
- 마우스 휠 슬롯 순환
- OneHand 슬롯 자유 변경
- TwoHand 슬롯 변경 차단
- TwoHand 선택 슬롯 LOCK 표시
- Q 아이템 버리기
- 버린 슬롯 Empty 처리
- 좌클릭 Use 동작
- Canvas 슬롯 6칸 표시
- 슬롯 번호 왼쪽 위 표시
- 선택 슬롯 배경 표시
- 아이템 이름 표시
- ESC Pause / 커서 전환
- 기존 이동 / 점프 / 웅크리기 / 달리기 정상
- 기존 상호작용 / CarryPoint 운반 정상

---

## Phase 2 완료 상태

Day 6까지 완료되면
Phase 2의 플레이어 조작·상호작용·기본 인벤토리 프로토타입이 연결된다.

현재 플레이어는:

`이동 → 상호작용 → 아이템 획득 → 빠른 슬롯 보관 → 아이템 선택 → 사용 → 버리기`

까지 하나의 흐름으로 처리할 수 있다.

다음 개발 단계부터는
조명·화염·전력 등 Project I의 핵심 환경 시스템을 구현한다.
