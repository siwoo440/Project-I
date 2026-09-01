# Project I 개발 일지

## Day 23 — 거리형 사무소 테스트맵·말 2마리 마차 및 영구 보관·판매·채무 시스템 구현

- 날짜: 2026-09-01
- 개발 단계: Phase 5 — 아이템·회수품·마차·경제
- 기준 커밋: `6be2d680c8129088f0a8ffdeb3532713b60002e4`
- 기준 커밋 메시지: `23`
- 기준 씬: `Assets/ProjectI/Scenes/ExplorationOffice.unity`
- 공통 마차 프리팹: `Assets/ProjectI/Prefabs/Wagon/Wagon.prefab`
- 공통 보관 단상 프리팹: `Assets/ProjectI/Prefabs/Office/StoragePedestal.prefab`

---

## 개발 목표

Day22에서 플레이어 사망, 래그돌, 원정 귀환·손실 판정까지 연결했다.

Day23에서는 귀환한 회수품을 실제 거점 경제와 연결하고,
기존 단순 기능 시험장이 아니라 이후 계속 확장할 수 있는
작은 거리형 테스트맵을 구성했다.

핵심 흐름은 다음과 같다.

```text
원정 귀환
↓
마차가 도로에 도착
↓
사무소로 회수품 운반
↓
가격 확인
├─ 일정 가격 미만 → 보관 단상
└─ 판매 → 공동 자금
              ↓
          채무 상환
```

감정 시스템은 사용하지 않는다.

회수품은 처음부터 가격을 가지며
사무소에서는 그 가격을 기준으로 보관 또는 판매한다.

---

# 1. 감정 시스템 제거

Day23의 경제 흐름에는 별도의 감정 단계가 없다.

사용하지 않는 개념:

```text
Appraisal
AppraisalArea
IsAppraised
FinalAppraisedValue
감정 완료
감정 가격 재추첨
```

최종 구조:

```text
WorldItem
└─ RecoverableValue
   ├─ Value
   └─ IsSold
```

회수품은 생성 이후 이미 확정된 `Value`를 가진다.

판매와 보관 모두 이 값을 직접 사용한다.

---

# 2. RecoverableValue

회수품의 확정 가격 상태:

```text
Assets/ProjectI/Scripts/Economy/RecoverableValue.cs
```

를 추가했다.

핵심 값:

```text
Value
IsSold
```

`Value`는 보관 가격 제한과 판매 가격 계산에 사용한다.

`IsSold`는 판매가 끝난 회수품이
다시 판매되거나 원정 손실 판정에 들어가는 것을 방지한다.

---

# 3. Day20 테스트 회수품 가격 연결

기존 Day20의 `WorldItem` 기반 테스트 회수품 구조는 유지한다.

테스트 가격:

```text
Day20_SilverCoin
은 동전
Value = 300

Day20_MetalOrnament
장인의 금속 장식
Value = 750

Day20_Crown
왕관
Value = 2250

Day20_GodsStatue
신들의 조각상
Value = 1500
```

가격 기능을 위해 새로운 회수품 인벤토리를 만들지 않는다.

기존 `WorldItem`에 `RecoverableValue`만 추가한다.

---

# 4. 공통 사무소 보관 단상 Prefab

새 공통 프리팹:

```text
Assets/ProjectI/Prefabs/Office/StoragePedestal.prefab
```

을 생성했다.

모든 단상은 같은 프리팹을 사용한다.

구조:

```text
StoragePedestal
├─ Base
├─ Pillar
├─ Top
├─ Trim
└─ DisplayPoint
```

`DisplayPoint`는 실제 회수품을 단상 위에 보여주는 위치다.

---

# 5. 단상 가격 제한

기본 보관 가격 상한:

```text
MaxStorageValue = 1000
```

이다.

판정 규칙:

```text
Value < 1000
→ 보관 가능

Value >= 1000
→ 보관 불가
```

따라서 현재 테스트에서는:

```text
은 동전 300
→ 가능

장인의 금속 장식 750
→ 가능

신들의 조각상 1500
→ 불가능

왕관 2250
→ 불가능
```

이다.

가격 제한은 Inspector의 `Max Storage Value`에서 조정할 수 있다.

---

# 6. 단상 F 보관

플레이어가 회수품을 빠른 슬롯에서 선택한 상태로
빈 단상을 바라보고 F를 누른다.

```text
회수품 선택
↓
단상 바라보기
↓
F
↓
가격 검사
↓
가격 조건 통과
↓
PlayerInventory에서 제거
↓
DisplayPoint로 이동
↓
단상 위에 실제 모델 표시
```

단상 하나에는 회수품 하나만 보관한다.

---

# 7. 단상 F 회수

단상에 이미 회수품이 있는 경우
F 입력은 보관이 아니라 회수로 동작한다.

```text
보관 중 회수품
↓
단상 바라보기
↓
F
↓
PlayerInventory 빈 슬롯 확인
↓
빠른 슬롯으로 회수
↓
단상 비움
```

플레이어의 빠른 슬롯이 가득 차 있으면 회수하지 않는다.

---

# 8. OfficeStoredItemState

단상에 보관된 회수품을 원정 아이템과 구분하기 위해:

```text
Assets/ProjectI/Scripts/Economy/OfficeStoredItemState.cs
```

를 추가했다.

핵심 상태:

```text
IsOfficeStored
Pedestal
```

단상 보관 성공:

```text
IsOfficeStored = true
```

플레이어가 다시 가져감:

```text
IsOfficeStored = false
```

---

# 9. 사무소 보관품 전멸 보호

Day22의:

```text
ExpeditionOutcomeController
```

를 확장했다.

기존 원정 실패에서는
현장 `WorldItem`이 손실 대상이 된다.

Day23 이후:

```text
OfficeStoredItemState.IsOfficeStored == true
```

이면 원정 결과 판정에서 제외한다.

따라서:

```text
전원 사망
↓
ExpeditionResult.Failed
↓
현장 회수품 손실

하지만

사무소 StoragePedestal
↓
그대로 유지
```

한다.

즉 단상은 원정과 분리된 안전 보관 영역이다.

---

# 10. 저장 범위

현재 Day23에서 구현한 영구 보관의 의미는:

```text
원정 실패
전원 사망
Returned / Lost 판정
```

으로부터 보호하는 것이다.

아직 게임 프로그램을 완전히 종료한 뒤 다시 실행해도
복원되는 Save/Load 파일 영구 저장까지 연결한 단계는 아니다.

향후 캠페인 Save 시스템을 구현할 때
`OfficeStoredItemState`와 단상 상태를 저장 대상으로 연결한다.

---

# 11. CampaignEconomy

사무소의 공동 자금을 관리하기 위해:

```text
Assets/ProjectI/Scripts/Economy/CampaignEconomy.cs
```

를 추가했다.

주요 값:

```text
SharedFunds
SaleMultiplier
```

개인별 돈은 만들지 않는다.

판매 수익과 채무 상환은
사무소 공동 자금 하나를 사용한다.

---

# 12. 판매 가격

판매 가격은 감정 값이 아니라
회수품의 확정 가격을 사용한다.

기본 공식:

```text
SalePrice
=
RecoverableValue.Value
×
SaleMultiplier
```

Day23 기본 테스트 배율:

```text
SaleMultiplier = 1.0
```

따라서 현재는 원래 가격과 판매 가격이 같다.

---

# 13. OfficeSaleCounter

회수품 판매 기능:

```text
Assets/ProjectI/Scripts/Economy/OfficeSaleCounter.cs
```

을 추가했다.

흐름:

```text
회수품 선택
↓
판매대 바라보기
↓
F
↓
판매 가격 계산
↓
PlayerInventory에서 제거
↓
IsSold = true
↓
공동 자금 증가
↓
판매된 회수품 비활성화
```

판매 완료 회수품은
이후 원정 귀환·손실 판정에서도 제외한다.

---

# 14. 채무 장부

채무 상환 기능:

```text
Assets/ProjectI/Scripts/Economy/DebtLedger.cs
```

을 추가했다.

6단계 목표:

```text
1단계  1000
2단계  1500
3단계  2250
4단계  3375
5단계  5062
6단계  7593
```

공동 자금으로 순서대로 상환한다.

---

# 15. 채무 상환 방식

장부를 바라보고 F를 누르면
현재 사용할 수 있는 최대 금액을 자동 상환한다.

```text
payment
=
min(
    SharedFunds,
    RemainingDebt
)
```

예:

```text
SharedFunds = 750
RemainingDebt = 1000

F

→ 750 상환
→ SharedFunds = 0
→ RemainingDebt = 250
```

현재 단계가 완료되면
다음 채무 단계로 넘어간다.

---

# 16. 기존 단순 경제 시험장 폐기

초기 Day23은 단순 평면 위에:

```text
단상
판매대
채무 장부
```

만 배치하는 기능 시험장으로 구성했다.

이후 방향을 수정하여:

```text
===Day23 Office Economy System===
```

기존 단순 구역을 제거하고
독립된 거리형 테스트 구역으로 확장했다.

---

# 17. 새로운 거리형 테스트맵

새 기준 구역:

```text
===Day23 Office Street District===
```

를 `ExplorationOffice` 안의 다른 시험장과 떨어진 위치에 생성했다.

기준 중심:

```text
StreetDistrictCenter
=
(44, 0, 5)
```

이다.

이 구역은 앞으로 테스트맵을 확장할 때
사무소·마차·경제 관련 개발의 기준 거점으로 사용한다.

---

# 18. 거리 기초 지형

새 거리에는 독립적인 큰 바닥을 생성했다.

```text
StreetFoundation

약
30m × 38m
```

범위다.

그 위에 중앙 도로와 보도를 배치했다.

---

# 19. MainRoad

거리 중심에는 남북 방향으로 긴:

```text
MainRoad
```

를 구성했다.

대략적인 크기:

```text
폭 8.5m
길이 34m
```

이다.

마차가 실제로 도로 위에 위치할 수 있을 정도의 폭으로 잡았다.

---

# 20. 도로 중앙 표식

`MainRoad` 중앙에는 짧은 표식을 반복 배치했다.

```text
RoadLine_01
RoadLine_02
...
```

기능적 의미보다는
현재 Primitive 기반 테스트 구역에서
도로 방향을 알아보기 쉽게 하는 시각 요소다.

---

# 21. 양쪽 보도

도로 좌우에는:

```text
Sidewalk_Left
Sidewalk_Right
```

를 구성했다.

건물과 도로 사이에
플레이어가 이동할 공간을 확보한다.

---

# 22. 일반 건물 거리

사무소 하나만 고립되어 보이지 않도록
주변에 일반 건물 5채를 배치했다.

```text
Building_Left_01
Building_Left_02
Building_Left_03

Building_Right_01
Building_Right_02
```

각 건물은 높이와 크기를 조금씩 다르게 했다.

---

# 23. 일반 건물 모델링

일반 건물은 Primitive 기반으로:

```text
Body
Roof
Door
Window_01
Window_02
```

구조를 가진다.

현재 기능이 없는 배경용 건물이지만
앞으로 상점, 숙소, 작업장 등으로 교체할 수 있는
거리 공간을 먼저 확보했다.

---

# 24. 거리 가로등

도로 주변에 최소 4개의 가로등을 배치했다.

예:

```text
StreetLamp_L_01
StreetLamp_L_02
StreetLamp_R_01
StreetLamp_R_02
```

각 가로등:

```text
Pole
Arm
Lamp
```

Primitive 구조를 가진다.

---

# 25. 사무소 건물

경제 기능을 실제 건물 안으로 이동했다.

구조:

```text
OfficeBuilding
├─ Office_Floor
├─ Office_EastWall
├─ Office_NorthWall
├─ Office_SouthWall
├─ Office_WestWall_North
├─ Office_WestWall_South
├─ Office_EntranceHeader
├─ Office_Roof
├─ Office_Sign
├─ Office_Window_North
├─ Office_Window_South
├─ Office_EntranceStep
└─ OfficeInterior
```

---

# 26. 사무소 출입구

도로를 향한 서쪽 벽 중앙을 열어
플레이어가 실제로 건물 안으로 들어갈 수 있게 했다.

```text
도로
↓
보도
↓
Office_EntranceStep
↓
중앙 출입구
↓
OfficeInterior
```

기존 단순 테스트처럼 기능 오브젝트를
외부 바닥에 바로 놓지 않는다.

---

# 27. 사무소 내부 기능 배치

`OfficeInterior` 안에 Day23 핵심 경제 기능을 모두 넣었다.

```text
OfficeInterior
├─ CampaignEconomy
├─ Day23_StoragePedestal_01
├─ Day23_StoragePedestal_02
├─ Day23_StoragePedestal_03
├─ Day23_StoragePedestal_04
├─ Day23_StoragePedestal_05
├─ Day23_StoragePedestal_06
├─ Day23_SaleCounter
└─ Day23_DebtLedger
```

즉 앞으로 경제 기능 테스트는
실제 사무소에 들어가서 수행한다.

---

# 28. 보관 단상 6개

사무소 내부에는
공통 `StoragePedestal.prefab` 인스턴스를 6개 배치했다.

```text
Day23_StoragePedestal_01
Day23_StoragePedestal_02
Day23_StoragePedestal_03
Day23_StoragePedestal_04
Day23_StoragePedestal_05
Day23_StoragePedestal_06
```

씬마다 각기 다른 단상을 다시 만드는 것이 아니라
모두 하나의 공통 프리팹을 참조한다.

---

# 29. 마차를 새 거리로 이동

Day21에서 만든:

```text
Wagon_Day21_Test
```

를 새로 복제하지 않았다.

기존 공통 `Wagon.prefab` 인스턴스를
새 거리의 중앙 도로로 이동했다.

기준 위치:

```text
StreetDistrictCenter
+
(0, 0.05, -7.2)

약
(44, 0.05, -2.2)
```

이다.

---

# 30. 이전 Wagon_TestFloor 제거

Day21에서 마차만 테스트하기 위해 사용한:

```text
Wagon_TestFloor
```

는 더 이상 필요하지 않다.

Day23 거리 구성 과정에서 이를 제거한다.

이후 마차는 독립 시험 바닥이 아니라
새 도시 테스트맵의 실제 도로 위에 존재한다.

---

# 31. 공통 Wagon.prefab 유지

마차를 거리로 옮겼다고 해서
씬 전용 마차를 새로 만들지 않았다.

여전히 원본:

```text
Assets/ProjectI/Prefabs/Wagon/Wagon.prefab
```

을 사용한다.

따라서 다른 맵에서도
동일한 규격의 마차를 계속 배치할 수 있다.

---

# 32. 마차 말 크기 수정

기존 마차의 말은
원본 크기:

```text
Scale = 1.0
```

이었다.

마차와 비교해 말이 크게 보이는 문제를 보정하여
각 말의 루트 스케일을:

```text
Scale = 0.8
```

로 변경했다.

즉 기존 대비 80% 크기다.

---

# 33. 말 1마리 → 2마리

기존:

```text
Visual
└─ Horse
```

한 마리 구성을 확장했다.

현재:

```text
Visual
├─ Horse
└─ Horse_Right
```

두 마리가 나란히 마차를 끄는 형태다.

기존 `Horse` 이름은 유지하여
선행 Day21 검증과 기존 구조 호환성을 보존한다.

---

# 34. 말 좌우 위치

두 말의 기본 X 위치:

```text
Horse
X = -0.95

Horse_Right
X = +0.95
```

이다.

마차 중심을 기준으로 좌우 대칭 배치한다.

두 말 모두:

```text
Local Scale
=
(0.8, 0.8, 0.8)
```

를 사용한다.

---

# 35. 말 전방 위치 보정

말을 80%로 축소하면
기존 루트 중심을 그대로 사용할 경우
마차와 연결된 시각적 거리가 어색해질 수 있다.

이를 보정하기 위해:

```text
HorseForwardOffset = 1.71
```

을 적용한다.

두 말은 같은 전후 위치에서 나란히 선다.

---

# 36. 두 번째 말 생성 방식

새 `Horse_Right`는
기존 왼쪽 말 전체를 복제한다.

따라서 오른쪽 말에도:

```text
Horse_Body
Horse_Chest
Horse_Neck
Horse_Head
Horse_Muzzle
Horse_Ear
Horse_Mane
Horse_Leg
Horse_Hoof
Horse_Tail
Harness
```

구조가 그대로 존재한다.

---

# 37. 말 2마리 자동 보정

에디터 보정 파일:

```text
Assets/ProjectI/Editor/Phase5Day21HorsePairUpgrade.cs
```

을 추가했다.

Unity 컴파일 완료 후
공통 `Wagon.prefab`을 확인한다.

필요한 경우:

```text
Wagon.prefab 열기
↓
기존 Horse 80% 축소
↓
왼쪽으로 이동
↓
Horse_Right 생성
↓
80% 크기로 통일
↓
오른쪽 배치
↓
Wagon.prefab 저장
```

처리한다.

---

# 38. 말 구성 수동 메뉴

필요 시:

```text
Tools
→ Project I
→ Day 21
→ Apply 80% Horse Pair
```

로 공통 마차 프리팹을 다시 보정할 수 있다.

검증:

```text
Tools
→ Project I
→ Day 21
→ Validate 80% Horse Pair
```

을 사용한다.

---

# 39. 기존 마차 기능 유지

말 외형 변경은
마차 기능을 변경하지 않는다.

그대로 유지:

```text
LargeCargoWarehouse
8개 바퀴
DriverSection
CargoArea
WagonCargoArea
SharedStorageChest
WagonSharedStorage
```

즉 말 2마리 변경은
공통 Wagon의 시각 구성만 확장한다.

---

# 40. Day23 거리형 Setup

자동 구성 파일:

```text
Assets/ProjectI/Editor/Phase5Day23Setup.cs
```

의 역할을 확장했다.

현재 흐름:

```text
StoragePedestal.prefab 확인
↓
ExplorationOffice 열기
↓
이전 단순 Day23 구역 제거
↓
거리 Foundation 생성
↓
MainRoad 생성
↓
Sidewalk 생성
↓
일반 건물 5채 생성
↓
가로등 생성
↓
OfficeBuilding 생성
↓
OfficeInterior 생성
↓
단상 6개 배치
↓
판매대 배치
↓
채무 장부 배치
↓
테스트 회수품 가격 연결
↓
기존 Wagon_Day21_Test 도로로 이동
↓
기존 Wagon_TestFloor 제거
↓
씬 저장
↓
Validator
```

---

# 41. 새로운 완료 마커

최신 Day23 거리형 상태는:

```text
===Day23 Office Street Ready v2===
```

로 표시한다.

기존:

```text
===Day23 Office Economy Ready v1===
```

은 이전 단순 시험장 상태이므로 제거한다.

---

# 42. Day23 Validator

최신 Validator는 다음을 확인한다.

```text
StoragePedestal.prefab 존재
Wagon.prefab 존재

거리형 테스트 구역 존재
MainRoad 존재
양쪽 Sidewalk 존재

일반 건물 5채
가로등 4개 이상

OfficeBuilding 존재
OfficeInterior 존재

사무소 내부 CampaignEconomy
단상 6개
판매대
채무 장부

단상 가격 제한 1000
공통 프리팹 인스턴스 사용

Wagon_Day21_Test 존재
기존 Wagon_TestFloor 제거
공통 Wagon.prefab 인스턴스 유지
도로 위치로 이동

테스트 회수품 가격 연결
단상 보관품 전멸 손실 보호
판매 완료품 원정 판정 제외
```

---

# 43. 현재 테스트맵 기준 상태

Day23 종료 시점의 테스트맵 상태를
이후 개발의 기준 상태로 사용한다.

즉 이후 Day24 이후 기능은
현재 `ExplorationOffice`의 기존 테스트 영역을 유지하면서
새 거리형 사무소 구역을 계속 확장하는 것을 기본으로 한다.

현재 거리형 기준 요소:

```text
중앙 도로
양쪽 보도
일반 건물 5채
사무소
사무소 내부 경제 기능
공통 보관 단상 6개
공통 Wagon.prefab
도로 위 마차
80% 크기 말 2마리
```

이 구성을 임의로 초기 상태로 되돌리지 않는다.

---

# 44. 최종 테스트 흐름

## 보관

```text
은 동전 F 획득
↓
거리의 사무소로 이동
↓
건물 내부 진입
↓
빈 단상 바라보기
↓
F
↓
Value 300 < 1000
↓
단상 보관
```

## 가격 제한

```text
왕관 F 획득
↓
단상 F
↓
Value 2250 >= 1000
↓
보관 거부
```

## 전멸 보호

```text
단상에 은 동전 보관
↓
원정 실패 / 전원 사망 판정
↓
은 동전은 LostItems에서 제외
↓
단상에 유지
```

## 판매

```text
왕관 선택
↓
사무소 판매대 F
↓
2250 판매
↓
SharedFunds +2250
```

## 채무

```text
SharedFunds 2250
↓
DebtLedger F
↓
1단계 1000 상환
↓
SharedFunds 1250
↓
2단계 진입
```

---

# 45. Day23 완료 결과

이번 일차 최종 완료 항목:

```text
감정 시스템 제거

RecoverableValue
확정 가격 기반 회수품 경제

공통 StoragePedestal.prefab
가격 1000 이상 보관 금지
단상 F 보관
단상 F 회수

OfficeStoredItemState
단상 보관품 전멸 손실 보호

CampaignEconomy
SharedFunds
SaleMultiplier

OfficeSaleCounter
판매 완료 중복 처리 방지

DebtLedger
6단계 채무 상환

독립 거리형 테스트 구역
중앙 MainRoad
양쪽 Sidewalk
일반 건물 5채
가로등
출입 가능한 OfficeBuilding

경제 기능 사무소 내부 이전

기존 공통 Wagon.prefab을 도로로 재배치
기존 Wagon_TestFloor 제거

말 크기 80% 축소
말 1마리에서 좌우 2마리로 변경
공통 Wagon.prefab 구조 유지

현재 테스트맵 상태를 이후 개발 기준으로 확정
```

---

# 다음 개발 방향

다음은 Phase 5:

```text
24일차
싱글 수직 슬라이스 통합
```

이다.

새 시스템을 크게 추가하기보다
지금까지 구현한 흐름을 하나의 실제 플레이 사이클로 연결하는 것이 핵심이다.

```text
사무소
↓
마차 / 원정 준비
↓
탐사
↓
전투
↓
회수품 획득
↓
마차 회수
↓
사망 또는 귀환
↓
거리형 사무소 복귀
↓
보관 또는 판매
↓
공동 자금
↓
채무 상환
↓
다음 원정
```

Day24 개발은 현재 Day23 종료 시점의 테스트맵 상태를
기준으로 이어서 진행한다.
