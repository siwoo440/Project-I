# Project I 개발 일지

## Day 21 — 공통 마차 프리팹 및 대형 적재창고·공동 보관·확보 판정 시스템 구현

- 날짜: 2026-09-01
- 개발 단계: Phase 5 — 아이템·회수품·마차·경제
- 기준 커밋: `ecac74aece9271ad75ea49ba225ecd2241bc3214`
- 기준 커밋 메시지: `a`
- 기준 씬: `Assets/ProjectI/Scenes/ExplorationOffice.unity`
- 공통 마차 프리팹: `Assets/ProjectI/Prefabs/Wagon/Wagon.prefab`

---

## 개발 목표

Day20에서 기존 `WorldItem`, `PlayerInventory`, 빠른 슬롯, 운반 시스템을 이용해
회수품을 실제 플레이어 아이템 흐름으로 통합했다.

Day21에서는 그 다음 단계로 회수품을 원정 중 실제로 보호할 수 있는
공통 회수 거점인 마차를 구현했다.

핵심 목표는 다음과 같다.

```text
모든 맵에서 동일한 Wagon.prefab 사용
↓
회수품을 마차까지 직접 운반
↓
대형 후방 적재창고 안에 내려놓기
↓
CargoArea가 회수품을 감지
↓
Secured 상태 부여
```

일반 장비는 물리 적재창고와 구분하여
마차의 공동 보관함으로 이동할 수 있도록 구성했다.

---

# 1. 모든 맵이 공유하는 Wagon.prefab

마차는 각 씬에서 별도로 제작하지 않는다.

공통 원본:

```text
Assets/ProjectI/Prefabs/Wagon/Wagon.prefab
```

하나를 사용한다.

각 맵에서는 이 프리팹을 인스턴스로 배치하는 방식으로 통일한다.

```text
Wagon.prefab
├─ Map A 인스턴스
├─ Map B 인스턴스
├─ Map C 인스턴스
└─ ExplorationOffice 테스트 인스턴스
```

따라서 이후 마차 외형이나 기능을 수정할 때도
공통 프리팹을 수정하면 모든 맵에 동일한 규격을 적용할 수 있다.

---

# 2. 세로로 긴 마차 규격

초기 구상보다 가로 폭을 넓히지 않고
말의 진행 방향을 기준으로 앞뒤 길이를 크게 늘렸다.

대형 후방 창고의 핵심 바닥 크기는 다음과 같다.

```text
폭   : 약 3.8 m
길이 : 약 9.8 m
```

즉 창고 길이가 폭의 두 배를 훨씬 넘는
세로형 구조다.

후방 창고는 마차 전체 기능 중 가장 큰 비중을 차지하며,
큰 회수품을 실제로 넣고 배치할 수 있는 공간을 목표로 한다.

---

# 3. 마차 기본 계층

공통 프리팹은 개념적으로 다음 구조를 가진다.

```text
Wagon
├─ Visual
│  ├─ Main_Chassis
│  ├─ LargeCargoWarehouse
│  ├─ Wheels
│  ├─ DriverSection
│  └─ Horse
│
├─ CargoArea
│
└─ SharedStorageChest
   └─ StoredItems
```

시각 모델링과 기능 오브젝트를 분리하여
후속 애니메이션이나 모델 교체 작업이 쉬운 방향으로 구성했다.

---

# 4. 대형 후방 적재창고

마차 뒤쪽에는 `LargeCargoWarehouse`를 구성했다.

주요 구성:

```text
Warehouse_Floor
Warehouse_LeftWall
Warehouse_RightWall
Warehouse_Roof
Warehouse_FrontWall

RearDoor_LeftPost
RearDoor_RightPost
RearDoor_Header

Rear_LoadingRamp
Cargo_ZoneFloor

WallBrace
RoofRib
```

창고는 단순한 상자가 아니라
바닥, 양쪽 벽, 지붕, 보강 프레임, 후방 입구와 경사판으로 구성된다.

---

# 5. 후방 개방구와 Loading Ramp

큰 회수품을 마차에 넣기 쉽게
창고 뒤쪽을 완전히 막지 않고 적재용 개방구를 만들었다.

```text
후방 개방구
↓
Rear_LoadingRamp
↓
대형 후방 창고
↓
CargoArea
```

후방 경사판은 실제 이동 가능한 물리 Collider를 유지한다.

향후 양손 대형 회수품을 직접 들고
마차 내부까지 이동하는 테스트에 사용할 수 있다.

---

# 6. 긴 차체용 8개 바퀴

차체 길이가 늘어났기 때문에
기존 4바퀴 구조가 아니라 총 4축으로 확장했다.

축 위치:

```text
Axle 01
Axle 02
Axle 03
Axle 04
```

각 축에는 좌우 바퀴가 하나씩 존재한다.

```text
왼쪽 바퀴 4개
오른쪽 바퀴 4개

총 8개
```

프리팹 계층에서는 다음 형태로 구성된다.

```text
Wheels
├─ Wheel_Left_01
├─ Wheel_Right_01
├─ Axle_01
├─ Wheel_Left_02
├─ Wheel_Right_02
├─ Axle_02
├─ Wheel_Left_03
├─ Wheel_Right_03
├─ Axle_03
├─ Wheel_Left_04
├─ Wheel_Right_04
└─ Axle_04
```

긴 후방 창고와 차체 무게를 시각적으로 받쳐주는 형태다.

---

# 7. 운전석

창고 앞쪽에는 별도의 운전 구역을 구성했다.

```text
DriverSection
├─ DriverDeck
├─ DriverBench
├─ DriverBackrest
└─ FrontRail
```

현재 Day21에서는 운전 기능이나 이동 기능을 구현하지 않는다.

운전석은 이후 마차 이동,
운전자 애니메이션,
말 이동과 연결할 수 있는 시각적 기반만 제공한다.

---

# 8. 말 형태 모델링

마차 앞에는 외부 모델 없이
Unity Primitive를 조합하여 말 실루엣을 구성했다.

주요 구성:

```text
Horse
├─ Horse_Body
├─ Horse_Chest
├─ Horse_Neck
├─ Horse_Head
├─ Horse_Muzzle
├─ Horse_Ear_Left
├─ Horse_Ear_Right
├─ Horse_Mane
├─ Horse_Leg_01
├─ Horse_Leg_02
├─ Horse_Leg_03
├─ Horse_Leg_04
├─ Horse_Hoof_01~04
└─ Horse_Tail
```

몸통은 진행 방향으로 길게 배치하고,
가슴 → 목 → 머리 → 주둥이 순서로 실루엣을 연결했다.

말의 실제 애니메이션은 Day21 범위에 포함하지 않는다.

---

# 9. 마차 연결봉과 마구

말과 마차가 분리되어 보이지 않도록
연결 구조도 함께 모델링했다.

```text
Harness_Shaft_Left
Harness_Shaft_Right
Harness_Yoke
Harness_BreastStrap
```

좌우 연결봉은 마차 앞부분에서 말 쪽으로 길게 뻗는다.

현재는 시각 및 Collider 기반이며
물리 Joint나 실제 견인 시뮬레이션은 사용하지 않는다.

---

# 10. CargoArea

후방 창고 내부에는 회수품 확보 판정을 담당하는:

```text
CargoArea
└─ WagonCargoArea
```

를 배치했다.

`CargoArea`는 `BoxCollider.isTrigger = true` 형태로 동작한다.

적재 범위는 창고 벽 안쪽보다 조금 작게 설정하여
물건이 단순히 마차 근처에 있는 것이 아니라
실제로 창고 내부에 들어왔을 때만 확보 판정을 받도록 했다.

---

# 11. 회수품 확보 흐름

Day20 회수품은 그대로 기존 `WorldItem`을 사용한다.

마차 적재 흐름:

```text
WorldItem 획득
↓
PlayerInventory 빠른 슬롯
↓
플레이어가 직접 운반
↓
마차 뒤쪽으로 이동
↓
CargoArea 내부에 G로 내려놓기
↓
WorldItem 월드 물리 활성화
↓
WagonCargoArea 감지
↓
Secured
```

따라서 회수품을 마차 메뉴에서 버튼으로 순간 이동시키지 않는다.

실제 물리 오브젝트를 직접 운반하고 적재하는 구조를 유지한다.

---

# 12. WagonCargoItemState

회수품이 마차에 확보되었는지 기록하기 위해:

```text
WagonCargoItemState
```

를 추가했다.

핵심 상태:

```text
IsSecured
SecuredArea
```

`WagonCargoArea`가 기존 `WorldItem`을 감지하면
필요한 경우 상태 컴포넌트를 붙여 확보 상태를 기록한다.

기존 Day20 아이템 구조 자체를 새로운 회수품 시스템으로 교체하지 않는다.

---

# 13. 확보 조건

아이템은 다음 조건을 만족해야 확보된다.

```text
WorldItem 존재
+
현재 손에 들고 있지 않음
+
PlayerInventory 내부 보관 상태가 아님
+
아이템 중심점이 CargoArea 내부
```

즉 손에 든 채 Trigger를 통과하는 것만으로는
확보되지 않는다.

실제 창고 내부에 내려놓아 월드 상태가 되어야 한다.

---

# 14. 확보 해제

회수품을 다시 가져갈 수도 있어야 한다.

다음 상황에서는 확보 상태가 해제된다.

```text
CargoArea 밖으로 이동
플레이어가 다시 집음
다른 저장 루트에 들어감
마차가 비활성화됨
```

따라서:

```text
Secured
→ 다시 F로 획득
→ Unsecured
```

흐름이 가능하다.

이 상태는 다음 Day22 사망·원정 손실 시스템에서
핵심 판정값으로 사용할 수 있다.

---

# 15. 다른 마차로 옮기는 경우

회수품이 이미 다른 마차에 확보되어 있는 상태에서
새로운 `WagonCargoArea`에 들어가면
기존 마차의 확보 상태를 먼저 해제하고
현재 마차로 확보 소유권을 이동한다.

개념:

```text
Wagon A
Secured
↓
회수품 이동
↓
Wagon B CargoArea
↓
Wagon A Unsecured
Wagon B Secured
```

멀티맵 또는 복수 마차 구조에도 대응할 수 있는 기반이다.

---

# 16. 공동 보관함

회수품의 물리 적재창고와 별도로
운전석 근처에는:

```text
SharedStorageChest
└─ WagonSharedStorage
```

를 배치했다.

이 보관함은 검, 도끼, 랜턴 등
일반 빠른 슬롯 장비를 보관하는 용도로 사용할 수 있다.

기본 용량:

```text
12개
```

---

# 17. 공동 보관함 상호작용

기존 `IInteractable`과 `PlayerInteractor` 흐름을 재사용한다.

따라서 상자를 바라보고:

```text
F
```

입력으로 사용한다.

현재 선택 슬롯에 아이템이 있으면:

```text
선택 아이템
↓
F
↓
PlayerInventory에서 제거
↓
SharedStorageChest 저장
```

처리한다.

---

# 18. 공동 보관함에서 꺼내기

현재 선택 슬롯이 비어 있고
공동 보관함에 저장된 아이템이 있으면
최근에 넣은 아이템부터 꺼낸다.

```text
빈 슬롯 선택
↓
SharedStorageChest 바라보기
↓
F
↓
공동 보관함 아이템 회수
↓
PlayerInventory 첫 빈 슬롯
↓
즉시 선택
↓
손에 표시
```

Day21에서는 별도의 보관함 UI를 추가하지 않고
기존 F 상호작용과 빠른 슬롯만으로 기능을 검증한다.

---

# 19. PlayerInventory 최소 확장

새로운 마차 전용 인벤토리를 만들지 않았다.

기존:

```text
Assets/ProjectI/Scripts/Items/PlayerInventory.cs
```

에 외부 저장소 연결을 위한 기능만 추가했다.

```text
TryStoreSelectedItem()
TryReceiveStoredItem()
```

이를 통해 기존 빠른 슬롯 데이터와
공동 보관함 사이에서 `WorldItem`을 이동한다.

기존 `WorldItem`, `QuickSlot`, `PlayerCarryController`, `QuickSlotHud`
구조는 계속 유지한다.

---

# 20. 마차 자동 생성 Setup

Day21 에디터 자동 구성 파일:

```text
Assets/ProjectI/Editor/Phase5Day21Setup.cs
```

을 추가했다.

자동 구성 흐름:

```text
Unity 컴파일 완료
↓
Day21 Setup 실행
↓
Prefabs/Wagon 폴더 확인
↓
Day21 재질 생성
↓
Primitive 기반 마차 모델 생성
↓
Wagon.prefab 저장
↓
ExplorationOffice 열기
↓
Day21 테스트 구역 생성
↓
Wagon.prefab 인스턴스 배치
↓
씬 저장
↓
Day21 Validator 실행
```

---

# 21. 수동 Setup 메뉴

자동 적용이 필요한 경우 다음 메뉴를 사용한다.

```text
Tools
→ Project I
→ Day 21
→ Apply Wagon System
```

마차 프리팹만 다시 만들려면:

```text
Tools
→ Project I
→ Day 21
→ Rebuild Wagon Prefab Only
```

를 사용한다.

---

# 22. ExplorationOffice 테스트 구역

Day21 테스트용 씬 루트:

```text
===Day21 Wagon System===
```

을 생성했다.

내부에는:

```text
Wagon_TestFloor
Wagon_Day21_Test
```

가 배치된다.

`Wagon_Day21_Test`는
씬에서 직접 복제한 독립 오브젝트가 아니라
공통 `Wagon.prefab`의 인스턴스다.

---

# 23. Day21 완료 마커

자동 Setup 완료 여부는 다음 씬 루트로 표시한다.

```text
===Day21 Wagon Ready v1===
```

Setup은 이 마커를 이용하여
Unity를 다시 열 때 같은 시험장을 반복 생성하지 않는다.

---

# 24. Day21 Validator

검증 파일:

```text
Assets/ProjectI/Editor/Phase5Day21Validator.cs
```

을 추가했다.

검증 항목은 다음과 같다.

```text
공통 Wagon.prefab 존재

세로형 대형 후방 창고 존재
창고 길이 비율 확인

긴 차체용 총 8개 바퀴 존재

마차 앞 Horse_Body 존재

CargoArea Trigger 존재

WagonSharedStorage 존재
공동 보관 기본 용량 12개 이상

PlayerInventory 외부 저장 기능 존재
PlayerInventory 공동 보관 회수 기능 존재

ExplorationOffice Day21 테스트 구역 존재
Day21 완료 마커 존재

씬의 테스트 마차가
공통 Wagon.prefab 인스턴스인지 확인
```

수동 검증 메뉴:

```text
Tools
→ Project I
→ Day 21
→ Validate Wagon System
```

---

# 25. 생성 재질

마차 모델링에는 Day21 전용 생성 재질을 사용한다.

주요 유형:

```text
목재
밝은 목재
금속
창고 지붕
CargoArea 표시
말 몸통
말 갈기·발굽
마구
테스트 바닥
```

외부 모델이 준비되기 전에도
프리미티브와 재질만으로 기능과 전체 실루엣을 확인할 수 있다.

---

# 26. Day21 테스트 흐름

## 회수품 적재

```text
Day20 회수품 F 획득
↓
마차 후방으로 이동
↓
Loading Ramp를 통해 창고 진입
↓
CargoArea 안에서 G
↓
아이템 월드 상태 복귀
↓
Secured 판정
```

---

## 회수품 다시 꺼내기

```text
마차 안 Secured 회수품
↓
F
↓
PlayerInventory 획득
↓
Secured 해제
↓
Unsecured
```

---

## 일반 아이템 공동 보관

```text
검 또는 일반 아이템 선택
↓
SharedStorageChest 바라보기
↓
F
↓
플레이어 빠른 슬롯 제거
↓
공동 보관함 저장
```

---

## 공동 보관 아이템 회수

```text
빈 빠른 슬롯 선택
↓
SharedStorageChest 바라보기
↓
F
↓
최근 보관 아이템 회수
↓
플레이어 빠른 슬롯 등록
↓
손에 표시
```

---

# 27. Day21 최종 구조

```text
                     Horse
                       │
                Harness / Shaft
                       │
                 DriverSection
                       │
        ┌────────── Wagon ──────────┐
        │                           │
        │   LargeCargoWarehouse     │
        │                           │
        │       CargoArea           │
        │                           │
        └────── Rear Loading ───────┘
                 │
          WagonCargoArea
                 │
       WagonCargoItemState
                 │
             IsSecured
```

일반 장비는 별도 경로를 사용한다.

```text
PlayerInventory
       │
SharedStorageChest
       │
WagonSharedStorage
       │
  StoredItems
```

---

# 28. Day21 완료 결과

이번 일차의 핵심 완료 항목:

```text
공통 Wagon.prefab 생성
모든 맵에서 같은 프리팹 사용 가능

세로형 대형 후방 창고 구현
후방 적재 개방구 구현
Loading Ramp 구현

긴 차체용 총 8개 바퀴 구현

운전석 구현

Primitive 기반 말 형태 구현
말과 마차 연결봉·마구 구현

CargoArea 구현
회수품 Secured / Unsecured 판정 구현

WagonCargoItemState 구현

SharedStorageChest 구현
공동 보관 기본 용량 12개 구현

PlayerInventory 외부 저장·회수 기능 연결

ExplorationOffice 테스트 구역 구현

Day21 Setup 구현
Day21 Validator 구현
```

---

# 다음 개발 방향

다음은 Phase 5의:

```text
22일차
사망·시체·원정 손실 시스템
```

이다.

Day21에서 확보 상태인:

```text
WagonCargoItemState.IsSecured == true
```

회수품은 원정 종료 또는 플레이어 사망 이후에도
보호 대상이 될 수 있다.

반대로:

```text
던전 바닥
플레이어 손
PlayerInventory
마차 밖
```

등에 남아 있는 미확보 아이템은
원정 실패 시 손실 대상이 되도록 연결할 수 있다.

따라서 Day22의 핵심 흐름은 다음과 같다.

```text
플레이어 사망
↓
시체 생성
↓
현재 장비·아이템 처리
↓
마차 Secured 물품 보존
↓
미확보 물품 손실 후보
↓
정상 귀환 / 부분 귀환 / 원정 실패 판정
```
