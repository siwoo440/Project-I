# Project I 개발 일지

## Day 20 — 기존 아이템·빠른 슬롯 기반 회수품 운반 시스템 통합 및 오류 정리

- 날짜: 2026-09-01
- 개발 단계: Phase 5 — 아이템·회수품·마차·경제
- 기준 최신 커밋: `84426fe336e3749c73e961fc96a75e19ea153b8a`
- 기준 커밋 메시지: `a`
- 기준 씬: `Assets/ProjectI/Scenes/ExplorationOffice.unity`

---

## 개발 목표

Phase 5의 첫 작업으로 아이템·회수품 공통 시스템을 정리했다.

처음에는 회수품을 일반 아이템과 분리한 별도 운반 시스템으로 구성했으나,
프로젝트에는 이미 Day5~6에서 다음 기능이 구현되어 있었다.

```text
WorldItem
PlayerInventory
PlayerCarryController
QuickSlot
QuickSlotHud
CarryType
```

새로운 회수품 시스템이 이 구조를 우회하면서
동일한 기능이 중복되고 한손 아이템이 빠른 슬롯을 사용하지 않는 문제가 발생했다.

따라서 Day20의 최종 방향을 다음과 같이 수정했다.

```text
별도 회수품 운반 시스템 제거

↓

기존 WorldItem 시스템 재사용

↓

회수품도 일반 월드 아이템과 동일하게
F 획득 → 빠른 슬롯 저장 → 손에 표시 → G 내려놓기
```

---

# 1. 기존 아이템 시스템 재사용

Day20에서는 새로운 인벤토리나 새로운 운반 컨트롤러를 만들지 않는다.

기존 구조:

```text
WorldItem
↓
PlayerInventory
↓
QuickSlot 1~6
↓
PlayerCarryController
↓
OneHandCarryPoint / TwoHandCarryPoint
↓
QuickSlotHud
```

를 그대로 사용한다.

이 구조를 유지함으로써 기존 장비와 회수품이
동일한 입력과 슬롯 규칙을 공유한다.

---

# 2. 회수품 획득 흐름

회수품은 별도 수집 목록으로 들어가지 않고
기존 `WorldItem`으로 동작한다.

최종 획득 흐름:

```text
회수품을 바라봄
↓
F
↓
PlayerInventory.TryPickup()
↓
첫 번째 빈 QuickSlot 탐색
↓
해당 슬롯에 WorldItem 저장
↓
새 슬롯 자동 선택
↓
PlayerCarryController가 손에 표시
↓
QuickSlotHud에 아이템 이름 표시
```

따라서 회수품도 기존 아이템과 동일하게
화면 아래 1~6번 슬롯을 실제로 차지한다.

---

# 3. 한손 아이템 오른손 배치

기존 Day5의 한손 운반 지점 구조를 복구했다.

최종 한손 CarryPoint:

```text
OneHandCarryPoint

Local Position
X = +0.42
Y = -0.38
Z = 0.90
```

X가 양수이므로
1인칭 화면 기준 오른쪽 아래에 한손 아이템이 표시된다.

기존에 잘못 추가했던:

```text
RecoverableOneHandCarryPoint
```

는 제거한다.

---

# 4. 양손 아이템 배치

양손 아이템은 기존:

```text
TwoHandCarryPoint
```

를 사용한다.

최종 기본 위치:

```text
X = 0
Y = -0.48
Z = 1.05
```

화면 중앙 아래에서 양손으로 운반하는 형태다.

기존에 별도로 추가했던:

```text
RecoverableTwoHandCarryPoint
```

는 제거한다.

---

# 5. 빠른 슬롯 표시

한손과 양손 회수품 모두
기존 `PlayerInventory`의 빠른 슬롯을 사용한다.

예:

```text
[1] 은 동전
[2] 장인의 금속 장식
[3] 왕관
[4] 신들의 조각상
[5]
[6]
```

실제 슬롯은 플레이어가 획득한 순서와
현재 빈 슬롯 상태에 따라 결정된다.

`QuickSlotHud`는 각 슬롯의 `WorldItem.DisplayName`을 읽어
화면 아래 HUD에 표시한다.

---

# 6. 한손 아이템 규칙

Day20 테스트용 한손 회수품:

```text
은 동전
장인의 금속 장식
```

두 아이템은:

```text
CarryType.OneHand
```

을 사용한다.

동작:

```text
F 획득
↓
빈 슬롯 저장
↓
해당 슬롯 선택
↓
오른쪽 OneHandCarryPoint에 표시
↓
다른 슬롯으로 전환 가능
↓
선택 해제 시 InventoryStorage에 보관
↓
다시 슬롯 선택 시 오른손에 표시
↓
G 입력
↓
월드에 내려놓기 + 슬롯 비우기
```

---

# 7. 양손 아이템 규칙

Day20 테스트용 양손 회수품:

```text
왕관
신들의 조각상
```

두 아이템은:

```text
CarryType.TwoHand
```

을 사용한다.

기존 `QuickSlotRules`와 `PlayerInventory`의
양손 슬롯 잠금 규칙을 그대로 사용한다.

즉 별도 회수품 전용 잠금 시스템을 만들지 않는다.

---

# 8. 테스트 회수품 4종

Day20 테스트 구역에는
기존 `WorldItem`으로 구성된 회수품 4종을 배치했다.

| 오브젝트 | 표시 이름 | 운반 방식 |
| --- | --- | --- |
| `Day20_SilverCoin` | 은 동전 | OneHand |
| `Day20_MetalOrnament` | 장인의 금속 장식 | OneHand |
| `Day20_Crown` | 왕관 | TwoHand |
| `Day20_GodsStatue` | 신들의 조각상 | TwoHand |

모두 다음 공통 구조를 가진다.

```text
GameObject
├─ Rigidbody
├─ BoxCollider
└─ WorldItem
```

즉 테스트 회수품에도 별도의
`RecoverableInstance` 컴포넌트를 사용하지 않는다.

---

# 9. Day20 테스트 구역

새 Day20 시험장 루트:

```text
===Day20 Existing Item Test===
```

를 `ExplorationOffice`에 생성했다.

중심 위치:

```text
(20, 0, 20)
```

부근에 두어 기존 전투·몬스터·함정 시험장과 분리한다.

시험장에는:

```text
ItemTest_Floor

Day20_SilverCoin
Day20_MetalOrnament
Day20_Crown
Day20_GodsStatue
```

가 배치된다.

---

# 10. 기존 별도 회수품 시스템 폐기

초기 Day20 구현에서 새로 만들었던 다음 구조는
현재 최종 설계에서 사용하지 않는다.

```text
ItemCategory.cs
ItemData.cs
RecoverableData.cs
RecoverableInstance.cs
RecoverableSpawnPoint.cs
PlayerRecoverableCarrier.cs
RecoverableDebugPage.cs
```

해당 구조는 기존 Day5~6 아이템 시스템과 역할이 중복되었다.

특히:

```text
PlayerRecoverableCarrier
```

가 `PlayerInventory`를 거치지 않고 회수품을 직접 운반하면서

```text
빠른 슬롯 미점유
QuickSlotHud 미표시
별도 왼손 CarryPoint 사용
```

문제가 발생했다.

따라서 모두 제거하고 기존 아이템 경로로 통합했다.

---

# 11. 이전 Day20 시험장 제거

초기 별도 회수품 구조에서 사용한:

```text
===Day20 Item Recoverable System===
===Day20 Item Recoverable Ready===
```

루트와 완료 마커를 제거한다.

최종 Day20에서는:

```text
===Day20 Existing Item Test===
===Day20 Existing Item Ready v2===
```

를 사용한다.

---

# 12. PlayerRecoverableCarrier Missing Script 문제

별도 회수품 코드를 삭제한 뒤
씬의 Player 오브젝트에 삭제된 스크립트 참조가 남아:

```text
The referenced script (Unknown) on this Behaviour is missing!
```

경고가 발생했다.

원인은:

```text
Player
↓
PlayerRecoverableCarrier 참조 유지
↓
PlayerRecoverableCarrier.cs 삭제
↓
Unity가 해당 GUID를 찾지 못함
↓
Missing Script
```

구조였다.

---

# 13. Missing Script 정리

Player 계층에서
삭제된 `PlayerRecoverableCarrier` 참조를 제거했다.

최종 최신 `ExplorationOffice.unity`에서는
이전에 `PlayerRecoverableCarrier`가 사용하던 스크립트 GUID가
더 이상 남아 있지 않도록 정리했다.

기존 Player 구성:

```text
PlayerInventory
PlayerCarryController
QuickSlotHud
기존 전투/이동 컴포넌트
```

는 유지한다.

---

# 14. Phase5Day20Setup

현재 Day20 자동 구성 파일:

```text
Assets/ProjectI/Editor/Phase5Day20Setup.cs
```

은 별도의 회수품 운반 시스템을 생성하지 않고
기존 아이템 시스템을 이용해 Day20 시험장을 구성한다.

주요 처리:

```text
ExplorationOffice 열기
↓
기존 Player 조회
↓
PlayerInventory 확인
↓
PlayerCarryController 확인
↓
Player Camera 확인
↓
이전 Day20 시험장 제거
↓
이전 전용 CarryPoint 제거
↓
Missing Script 정리
↓
OneHandCarryPoint 오른쪽 위치 복구
↓
TwoHandCarryPoint 중앙 위치 복구
↓
기존 WorldItem 기반 보물 4종 생성
↓
씬 저장
↓
Validator 실행
```

수동 메뉴:

```text
Tools
→ Project I
→ Day 20
→ Apply Existing Item Carry Fix
```

---

# 15. Phase5Day20Validator

검증 파일:

```text
Assets/ProjectI/Editor/Phase5Day20Validator.cs
```

을 사용한다.

검증 내용:

```text
Day20 기존 아이템 시험장 존재
Day20 완료 마커 존재

이전 Day20 시험장 제거
이전 Day20 완료 마커 제거

PlayerInventory 유지
PlayerCarryController 유지
QuickSlotHud 유지

PlayerRecoverableCarrier 제거

OneHandCarryPoint 오른쪽 배치
TwoHandCarryPoint 중앙 배치

RecoverableOneHandCarryPoint 제거
RecoverableTwoHandCarryPoint 제거

WorldItem 기반 보물 4개 존재
한손 보물 2개
양손 보물 2개
```

수동 검증 메뉴:

```text
Tools
→ Project I
→ Day 20
→ Validate Existing Item Carry Fix
```

---

# 16. 최종 테스트 흐름

## 한손 아이템

```text
은 동전 접근
↓
F
↓
하단 빈 슬롯에 은 동전 표시
↓
오른손에 아이템 표시
↓
숫자키 또는 휠로 다른 슬롯 선택
↓
아이템 숨김 보관
↓
해당 슬롯 재선택
↓
오른손에 다시 표시
↓
G
↓
월드에 내려놓기
↓
해당 슬롯 비움
```

장인의 금속 장식도 같은 구조를 사용한다.

---

## 양손 아이템

```text
왕관 또는 신들의 조각상 접근
↓
F
↓
빈 빠른 슬롯 차지
↓
TwoHandCarryPoint에 표시
↓
기존 양손 운반 잠금 규칙 적용
↓
G
↓
월드 복귀
↓
슬롯 비움
```

---

# 17. 최종 구조

Day20 완료 후 아이템 계층은 다음 방향으로 정리된다.

```text
                WorldItem
                    │
            PlayerInventory
                    │
              QuickSlot 1~6
                    │
         PlayerCarryController
            ┌───────┴───────┐
            │               │
     OneHandCarryPoint  TwoHandCarryPoint
            │               │
          오른손         화면 중앙
```

회수품이라고 해서 별도의 인벤토리나
별도의 운반 컨트롤러를 만들지 않는다.

---

# 18. Day20 완료 결과

이번 일차에서 최종적으로 정리된 핵심은 다음과 같다.

```text
기존 WorldItem 시스템 재사용
기존 PlayerInventory 재사용
기존 QuickSlot 6칸 재사용
기존 QuickSlotHud 재사용
기존 PlayerCarryController 재사용

한손 회수품 → 오른손
한손 회수품 → 빠른 슬롯 점유
양손 회수품 → 기존 양손 운반 규칙

별도 Recoverable 운반 시스템 제거
별도 왼손 CarryPoint 제거
PlayerRecoverableCarrier 제거
Missing Script 잔존 참조 제거
```

새 시스템을 중복 추가하는 방식보다
이미 안정화된 기존 아이템 시스템을 확장하는 방향으로 통합했다.

---

# 다음 개발 방향

Phase 5 다음 단계는:

```text
21일차
마차·적재·보관 시스템
```

이다.

Day20에서 회수품이 실제 `WorldItem`으로
플레이어 슬롯에 들어오고 손에 들리는 구조를 정리했으므로,
다음 단계에서는 이 물건을 마차까지 운반한 뒤:

```text
마차 적재 구역 진입
↓
회수품 내려놓기
↓
마차 적재 판정
↓
확보 상태 기록
```

으로 연결하는 것이 핵심이다.

또한 일반 장비는 마차의 공동 보관함,
회수품은 실제 적재 공간으로 구분하는 방향으로 확장한다.
