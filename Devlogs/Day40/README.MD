# 40일차 개발 일지

## 작업 주제

Canvas 기반 인벤토리와 핫바 구현 및 Village 공통 HUD 연결

## 작업 목표

- 기존 OnGUI 인벤토리를 Canvas UI로 교체
- 전체 인벤토리 슬롯 자동 생성
- 아이템의 슬롯 점유 상태 표시
- 현재 선택 아이템 슬롯 강조
- 선택 아이템 상세 정보 표시
- 현재 무게와 제한 무게 표시
- 가방 보너스 슬롯 자동 반영
- 두손 운반 상태 표시
- 아이템 획득, 선택, 사용, 버리기 실시간 갱신
- PlayerHUD를 Dungeon과 Village에서 공통으로 사용
- Village에서 던전 전용 정보 자동 숨김
- 기존 성능 검사 패널 시작 상태 숨김

## 구현 내용

### InventorySystem 공개 상태 추가

Canvas UI가 기존 InventorySystem의 상태를 안전하게 읽을 수 있도록 다음 읽기 전용 속성을 추가했다.

- Items
- SelectedIndex
- TwoHandItem
- WeightLimit

기존 아이템 획득, 선택, 사용, 버리기, 무게 계산, 이동속도 페널티 로직은 변경하지 않았다.

### InventorySlotUI

개별 인벤토리 슬롯의 화면 표시를 담당하는 InventorySlotUI를 추가했다.

각 슬롯은 다음 정보를 표시한다.

- 슬롯 번호
- 아이템 이름
- 빈 슬롯 여부
- 아이템 점유 여부
- 현재 선택 여부
- 다중 슬롯 연결 상태

현재 선택한 아이템이 사용하는 모든 슬롯에는 노란색 외곽선을 표시한다.

빈 슬롯과 점유 슬롯 및 선택 슬롯은 서로 다른 배경색을 사용한다.

### InventoryHUD

InventorySystem의 현재 상태를 Canvas에 표시하는 InventoryHUD를 추가했다.

InventoryHUD는 Scene에서 InventorySystem을 자동으로 검색한다.

Village에서 플레이어가 10초 후 생성되는 구조에 대응하기 위해 InventorySystem을 찾지 못한 경우에도 주기적으로 다시 검색한다.

InventorySystem이 연결되면 현재 인벤토리 상태를 자동으로 화면에 반영한다.

### 동적 슬롯 생성

InventorySystem.TotalSlots 값에 맞춰 InventorySlot 프리팹을 자동으로 생성한다.

기본 슬롯 수는 5칸이며 가방 아이템을 획득하면 BonusSlots 값에 따라 추가 슬롯이 자동으로 생성된다.

가방을 제거하거나 인벤토리가 초기화되어 전체 슬롯 수가 감소하면 불필요한 슬롯 UI도 자동으로 제거된다.

### 다중 슬롯 아이템

한 아이템이 여러 슬롯을 사용하는 경우 실제 Slots 값만큼 슬롯을 점유하도록 표시한다.

첫 번째 점유 슬롯에는 아이템 이름을 표시하고 이어지는 슬롯에는 연결 기호를 표시한다.

현재 선택된 다중 슬롯 아이템은 점유한 모든 슬롯이 함께 강조된다.

### 아이템 이름 표시

현재 ICarryable에는 아이콘 정보가 없으므로 이번 단계에서는 아이템 이름 기반 핫바로 구현했다.

슬롯에 표시되는 이름이 너무 길 경우 앞 여섯 글자까지만 표시한다.

정식 아이템 아이콘은 ItemData와 TreasureData에 Sprite 정보를 추가하는 이후 작업에서 구현할 예정이다.

### 선택 아이템 상세 정보

현재 선택한 아이템에 대해 다음 정보를 표시한다.

- 아이템 이름
- 아이템 무게
- 사용 슬롯 수
- 보물의 골드 가치

마우스 휠로 선택 아이템이 변경되면 상세 정보와 슬롯 강조가 즉시 변경된다.

### 무게 UI

현재 인벤토리 무게를 글자와 가로 게이지로 표시한다.

표시 정보는 다음과 같다.

- 현재 무게
- 제한 무게
- 무게 비율

무게 상태에 따라 게이지 색상을 변경한다.

- 제한의 50% 이하: 녹색
- 제한의 50~100%: 녹색에서 노란색으로 변경
- 제한의 100% 초과: 붉은색

기존 InventorySystem의 이동속도와 스태미너 회복 페널티는 그대로 유지된다.

### 가방 보너스 슬롯

가방 아이템의 BonusSlots 값을 Canvas에 표시한다.

가방을 획득하면 전체 슬롯 수가 증가하고 새로운 슬롯 UI가 자동으로 생성된다.

표시 형식은 다음과 같다.

- 인벤토리 0/5칸
- 가방 +0칸

### 두손 운반 상태

대형 보물은 일반 인벤토리 슬롯을 사용하지 않고 별도의 두손 운반 상태로 표시한다.

두손 운반 중에는 다음 안내 문구를 표시한다.

- 두손 운반 아이템 이름
- Q 내려놓기 안내

두손 보물을 들고 있는 동안 일반 슬롯의 선택 강조는 일시적으로 숨긴다.

Q를 눌러 두손 보물을 내려놓으면 안내 문구가 자동으로 사라진다.

### 아이템 상태 실시간 갱신

다음 상황에서 Canvas 인벤토리가 즉시 갱신되도록 구현했다.

- E로 아이템 획득
- 마우스 휠로 선택 아이템 변경
- Q로 선택 아이템 버리기
- R로 회복 아이템 사용
- 가방 획득으로 슬롯 증가
- 두손 보물 획득과 내려놓기
- 플레이어 사망으로 모든 소지품 드랍
- 제한시간 유기로 모든 소지품 드랍
- 마차 탈출 시 모든 소지품 확보

### InventorySlot 프리팹

동적으로 생성할 InventorySlot 프리팹을 제작했다.

프리팹은 다음 컴포넌트를 사용한다.

- Image
- Outline
- LayoutElement
- InventorySlotUI
- SlotNumberText
- ItemNameText

HotbarContainer에는 HorizontalLayoutGroup을 적용해 슬롯 수가 변경되어도 화면 아래쪽 중앙에 정렬되도록 구성했다.

### PlayerHUDCanvas 구조 확장

기존 PlayerHUDCanvas에 InventoryHUD를 추가했다.

최종 UI 구조는 다음과 같다.

```text
PlayerHUDCanvas
├─ CrosshairText
├─ DungeonInfoPanel
│  ├─ TimeText
│  └─ BrightnessText
├─ PlayerStatusPanel
│  ├─ HealthLabel
│  ├─ HealthValueText
│  ├─ HealthBarBackground
│  │  └─ HealthBarFill
│  ├─ StaminaLabel
│  ├─ StaminaValueText
│  └─ StaminaBarBackground
│     └─ StaminaBarFill
├─ InteractionText
├─ InventoryHUD
│  ├─ InventorySummaryPanel
│  │  ├─ InventorySummaryText
│  │  ├─ WeightText
│  │  ├─ WeightBarBackground
│  │  │  └─ WeightBarFill
│  │  ├─ SelectedItemText
│  │  └─ TwoHandText
│  └─ HotbarContainer
└─ ControlGuideText

InventoryHUD와 기존 조작 안내 문구가 겹치지 않도록 ControlGuideText의 세로 위치를 조정했다.

PlayerHUD 프리팹 제작

Dungeon Scene에서 구성한 PlayerHUDCanvas를 공통 PlayerHUD 프리팹으로 저장했다.

프리팹 위치는 다음과 같다.

Assets/Prefabs/UI/PlayerHUD.prefab

PlayerHUD 프리팹에는 39일차 플레이어 HUD와 40일차 InventoryHUD가 모두 포함된다.

Village Scene HUD 연결

기존에는 PlayerHUDCanvas가 Dungeon Scene에만 배치되어 Village Scene에서는 새 HUD가 보이지 않았다.

기존 OnGUI 디버그 UI도 기본적으로 숨겨져 있었기 때문에 Village 화면에 아무런 UI가 없는 것처럼 보이는 문제가 발생했다.

이 문제를 해결하기 위해 Village Scene에도 PlayerHUD 프리팹을 추가했다.

Village Scene의 UI 구조는 다음과 같다.

Village
├─ Main Camera
├─ VillageEnvironment
├─ VillageWagon
├─ VillageSystems
├─ Player 또는 Player(Clone)
└─ UI
   └─ PlayerHUDCanvas

PlayerHUDCanvas는 Dungeon과 Village Scene에 각각 하나씩 배치한다.

Scene 전환 시 중복될 수 있으므로 PlayerHUDCanvas에는 DontDestroyOnLoad를 사용하지 않는다.

Village 플레이어 지연 생성 대응

Village에서는 Scene 진입 후 10초가 지나면 플레이어가 마차 바닥에 생성된다.

PlayerHUD와 InventoryHUD는 플레이어 생성 이전에도 활성 상태를 유지하며 PlayerController, PlayerInteractor, LightSystem, InventorySystem을 주기적으로 다시 검색한다.

플레이어가 생성되면 별도의 Inspector 재연결 없이 HUD가 자동으로 플레이어 상태를 표시한다.

InventoryHUD 오브젝트 자체를 비활성화하면 Update가 중단되어 플레이어를 다시 검색할 수 없으므로 항상 활성 상태로 유지한다.

DungeonInfoPanel Scene 구분

Village에는 DungeonTimeSystem이 없지만 PlayerHUD 프리팹을 공통으로 사용하면서 던전 시간 패널이 함께 표시될 수 있는 문제를 수정했다.

PlayerHUD에 DungeonInfoPanel 참조를 추가했다.

현재 Scene에서 DungeonTimeSystem을 찾았는지 확인해 던전 정보 패널을 자동으로 켜고 끈다.

Scene별 표시 상태는 다음과 같다.

Scene	DungeonInfoPanel
Dungeon	표시
Village	숨김

Dungeon Scene에서는 다음 정보가 정상적으로 표시된다.

던전 게임시간
남은 제한시간
현재 밝기
밝기 단계

Village Scene에서는 던전 전용 시간과 밝기 패널을 숨기고 다음 공통 HUD만 표시한다.

체력
스태미너
조준점
상호작용 문구
인벤토리
핫바
무게
선택 아이템 정보
기존 OnGUI 인벤토리 유지

기존 InventorySystem.OnGUI는 F2 디버그 비교용으로 유지했다.

일반 플레이에서는 Canvas InventoryHUD만 표시된다.

F2를 누르면 기존 OnGUI 인벤토리와 핫바가 나타나 Canvas 수치와 비교할 수 있다.

성능 검사 패널 시작 상태 수정

RuntimePerformanceMonitor의 26일차 성능·재생성 검사 패널이 Scene 시작 직후 표시되는 문제를 수정했다.

코드의 showPanel 기본값을 false로 변경했다.

기존 Scene에 직렬화된 true 값이 남아 있어도 확실하게 숨길 수 있도록 Awake에서 showPanel을 false로 초기화했다.

RuntimePerformanceMonitor Inspector의 Show Panel도 해제했다.

성능 검사 패널의 동작은 다음과 같다.

Scene 시작: 숨김
F10 입력: 표시
F10 다시 입력: 숨김
F9 입력: 던전 1회 재생성
F11 입력: 던전 자동 10회 반복 검사
수직 슬라이스 검증 패널 확인

VerticalSliceValidator의 Show Validation Panel을 해제해 F3 검증 패널도 Scene 시작 시 숨김 상태가 되도록 확인했다.

수직 슬라이스 검증 패널의 동작은 다음과 같다.

Scene 시작: 숨김
F3 입력: 표시
F3 다시 입력: 숨김
F8 입력: 즉시 재검증
Canvas 해상도 대응

PlayerHUDCanvas는 다음 Canvas Scaler 설정을 사용한다.

UI Scale Mode: Scale With Screen Size
Reference Resolution: 1920 × 1080
Screen Match Mode: Match Width Or Height
Match: 0.5

HotbarContainer는 화면 아래쪽 중앙 Anchor를 사용한다.

1920×1080, 1600×900, 1280×720 해상도에서 슬롯과 인벤토리 정보가 화면 밖으로 벗어나지 않도록 구성했다.

Collider 및 Rigidbody

PlayerHUDCanvas, InventoryHUD, InventorySummaryPanel, HotbarContainer, InventorySlot에는 Collider와 Rigidbody를 사용하지 않는다.

모든 UI는 Canvas와 RectTransform을 사용한다.

테스트 결과
Dungeon Scene
기본 5개 슬롯 생성 정상
일반 아이템 획득 표시 정상
다중 슬롯 아이템 표시 정상
마우스 휠 선택 강조 정상
선택 아이템 상세 정보 표시 정상
보물 골드 가치 표시 정상
현재 무게와 제한 무게 표시 정상
무게 상태별 게이지 색상 변경 정상
가방 보너스 슬롯 생성 정상
두손 운반 안내 표시 정상
Q 아이템 버리기 갱신 정상
R 회복 아이템 사용 갱신 정상
플레이어 사망 후 전체 슬롯 초기화 정상
던전 시간과 밝기 패널 표시 정상
기존 F2 OnGUI 수치와 Canvas 수치 일치
Village Scene
PlayerHUD 프리팹 표시 정상
플레이어 생성 전 HUD 참조 재검색 정상
10초 후 플레이어 생성 정상
플레이어 생성 후 체력과 스태미너 HUD 연결 정상
플레이어 생성 후 InventorySystem 자동 연결 정상
InventoryHUD와 핫바 표시 정상
조준점과 상호작용 안내 표시 정상
DungeonInfoPanel 숨김 정상
던전 시간과 밝기 정보 미표시 정상
디버그 패널
Scene 시작 시 성능 검사 패널 숨김 정상
F10 성능 검사 패널 표시 및 숨김 정상
Scene 시작 시 수직 슬라이스 검증 패널 숨김 정상
F3 수직 슬라이스 패널 표시 및 숨김 정상
기존 디버그 UI 단축키 충돌 없음
해상도
1920×1080 HUD 배치 정상
1600×900 HUD 배치 정상
1280×720 HUD 배치 정상
다음 작업

41일차에는 마을과 캠페인 관련 기존 OnGUI 화면을 Canvas UI로 교체한다.

다음 기능을 구현할 예정이다.

캠페인 날짜와 마감일 표시
보유 골드와 남은 빚 표시
남은 탐험 기회 표시
빚 납부 Canvas 화면 정리
마을 상점 Canvas 화면 정리
던전 선택 Canvas 화면 정리
캠페인 성공 및 실패 화면 정리
기존 캠페인 OnGUI 단계적 제거
