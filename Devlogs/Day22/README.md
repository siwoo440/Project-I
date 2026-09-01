# Project I 개발 일지

## Day 22 — 플레이어 본체 래그돌 사망 및 마차 공통 회수·원정 손실 시스템 구현

- 날짜: 2026-09-01
- 개발 단계: Phase 5 — 아이템·회수품·마차·경제
- 기준 커밋: `ad0e4bea3418c7da89d7d5c95e8d95ae46c0836a`
- 기준 커밋 메시지: `22`
- 기준 씬: `Assets/ProjectI/Scenes/ExplorationOffice.unity`
- 공통 마차 프리팹: `Assets/ProjectI/Prefabs/Wagon/Wagon.prefab`

---

## 개발 목표

Day21에서 마차의 대형 후방 창고와 회수품 확보 판정,
공동 보관함을 구현했다.

Day22에서는 플레이어 사망 이후의 원정 손실 흐름을 연결했다.

핵심 방향은 별도의 시체 오브젝트를 생성하지 않고
기존 Player 오브젝트 자체가 사망 상태로 전환되는 것이다.

```text
피해
↓
PlayerHealth HP 0
↓
PlayerHealth.Died
↓
PlayerDeathController
↓
생존 조작 기능 정지
↓
Player 내부 DeathRagdoll 활성화
↓
몸이 힘없이 물리로 쓰러짐
↓
안정화 후 Rigidbody 물리 고정
```

사망자가 소지하고 있던 기존 `WorldItem`은 삭제하지 않고
사망 위치 주변의 월드 물체로 되돌린다.

또한 Day21 마차의 `CargoArea`를 시체와 회수품으로 분리하지 않고,
대형 후방 창고 전체를 하나의 공통 회수 영역으로 사용하도록 확장했다.

---

# 1. 별도 시체 생성 방식 미사용

Day22에서는 다음 구조를 사용하지 않는다.

```text
Player 사망
→ Player 비활성화
→ PlayerCorpse.prefab 생성
```

대신:

```text
Player
├─ Alive State
└─ Dead State
   └─ DeathRagdoll
```

형태로 같은 Player가 생존 상태에서 사망 상태로 전환된다.

따라서 별도의:

```text
Assets/ProjectI/Prefabs/Player/PlayerCorpse.prefab
```

은 만들지 않는다.

---

# 2. 기존 PlayerHealth 사망 이벤트 재사용

기존 플레이어 체력 시스템에는 이미:

```text
PlayerHealth.Died
```

이벤트가 존재한다.

Day22는 새로운 체력 시스템을 만들지 않고
`PlayerDeathController`가 해당 이벤트를 구독한다.

최종 연결:

```text
DamagePipeline
↓
PlayerDamageReceiver
↓
PlayerHealth.TakeDamage()
↓
HP 0
↓
PlayerHealth.Died
↓
PlayerDeathController.HandleDeath()
```

---

# 3. PlayerDeathController

새 사망 상태 제어 파일:

```text
Assets/ProjectI/Scripts/Player/PlayerDeathController.cs
```

을 추가했다.

주요 책임:

```text
PlayerHealth.Died 연결
사망 상태 중복 진입 방지
사망 위치 저장
소지품 월드 드롭
생존 조작 기능 정지
CharacterController 비활성화
DeathRagdoll 활성화
래그돌 안정화 판정
안정화 후 물리 고정
마차 회수 상태 기록
사망 후 카메라 처리
```

외부에서 확인 가능한 주요 상태:

```text
IsDead
IsRagdollFrozen
IsRecovered
RecoveredArea
DeathPosition
RagdollCenter
```

---

# 4. 사망 순간 생존 기능 정지

HP가 0이 되면 기존 Player의 생존 상태 기능을 중지한다.

개념적으로:

```text
PlayerMovement
PlayerLook
PlayerInteractor
PlayerInventory 입력 처리
전투 관련 Behaviour
CharacterController
```

등 살아있는 상태에서만 필요한 기능을 정지한다.

Player GameObject 자체를 삭제하거나 비활성화하지 않기 때문에
사망한 Player는 계속 씬에 존재한다.

---

# 5. DeathRagdoll

`ExplorationOffice`의 기존 Player 내부에:

```text
Player
└─ DeathRagdoll
```

을 생성한다.

생존 중에는:

```text
DeathRagdoll.activeSelf = false
```

상태를 유지한다.

사망 순간 기존 1인칭 제어를 끄고
DeathRagdoll을 활성화한다.

---

# 6. Primitive 기반 인간형 래그돌

현재 프로젝트에는 최종 플레이어 인간형 3D 모델과 Bone 구조가 없으므로
Unity Primitive 기반 임시 인간형 Ragdoll을 자동 생성한다.

구성:

```text
DeathRagdoll
├─ Pelvis
├─ Chest
├─ Head
├─ UpperArm_L
├─ LowerArm_L
├─ UpperArm_R
├─ LowerArm_R
├─ UpperLeg_L
├─ LowerLeg_L
├─ UpperLeg_R
└─ LowerLeg_R
```

총 11개의 Rigidbody 신체 부위로 구성한다.

---

# 7. CharacterJoint 연결

각 신체 부위를 완전히 독립적인 물리 오브젝트로 두지 않고
`CharacterJoint`를 이용해 연결한다.

검증 기준:

```text
Rigidbody 11개 이상
CharacterJoint 10개 이상
```

이를 통해 사망 순간:

```text
서 있던 상태
↓
중력 적용
↓
골반·몸통이 무너짐
↓
팔과 다리가 관절을 따라 움직임
↓
바닥에 털썩 쓰러짐
```

형태를 목표로 한다.

---

# 8. 래그돌 물리 유지 시간

사망 직후 즉시 Kinematic으로 전환하면
몸이 쓰러지는 장면이 보이지 않는다.

따라서 최소 물리 유지 시간을 둔다.

현재 기본값:

```text
minimumPhysicsTime = 1.8초
```

최소 1.8초 동안은 실제 Rigidbody 물리를 유지한다.

---

# 9. 정지 상태 판정

물리를 무조건 일정 시간만 실행하는 것이 아니라
몸이 실제로 거의 움직이지 않는지 검사한다.

현재 기준:

```text
linearSleepThreshold  = 0.12
angularSleepThreshold = 0.85
stillRequiredTime     = 0.85초
```

모든 신체가 저속 상태를 일정 시간 유지하면
래그돌이 안정되었다고 판단한다.

---

# 10. 최대 물리 시간

경사면이나 다른 Collider 때문에
래그돌이 계속 미세하게 흔들릴 수 있다.

이를 방지하기 위해:

```text
maximumPhysicsTime = 6초
```

를 사용한다.

즉:

```text
충분히 정지
또는
사망 후 최대 6초 경과
```

중 하나를 만족하면 물리 절전으로 들어간다.

---

# 11. Ragdoll Freeze

안정화된 래그돌은:

```text
linearVelocity = 0
angularVelocity = 0
Rigidbody.isKinematic = true
Rigidbody.Sleep()
```

상태로 전환한다.

Collider는 유지한다.

따라서 죽은 Player는 씬에 계속 존재하지만
가만히 누워 있는 동안 매 프레임 복잡한 물리 계산을 계속하지 않는다.

---

# 12. Ragdoll Wake

향후 죽은 동료를 이동시키거나
외부 힘에 다시 반응시킬 수 있도록:

```text
WakeRagdoll()
```

기능도 준비했다.

Wake 시:

```text
isKinematic = false
detectCollisions = true
WakeUp()
```

로 복구하고 안정화 타이머를 다시 시작한다.

현재 Day22의 핵심은 사망 상태와 회수 판정이며,
본격적인 멀티플레이 시체 끌기·들기 조작은 이후 확장 가능하다.

---

# 13. 사망 카메라

1인칭 카메라를 Ragdoll 머리에 직접 붙여두면
사망 물리 회전에 따라 화면이 과도하게 뒤집힐 수 있다.

따라서 사망 후 기존 카메라를
래그돌 중심 주변의 외부 관찰 위치로 이동한다.

기본 오프셋:

```text
deathCameraOffset = (0, 1.35, -2.6)
```

카메라는 `RagdollCenter`를 바라보도록 보간된다.

---

# 14. 사망 시 소지품 처리

사망자의 빠른 슬롯 아이템은 삭제하지 않는다.

기존:

```text
PlayerInventory
WorldItem
PlayerCarryController
```

구조를 유지한다.

사망 시 각 슬롯의 아이템을 월드에 떨어뜨린다.

예:

```text
사망 전

[1] 검
[2] 랜턴
[3] 은 동전
[4] 왕관

↓

사망 후

DeathRagdoll
주변
├─ 검
├─ 랜턴
├─ 은 동전
└─ 왕관
```

---

# 15. DropSelectedItem 반환형 오류 수정

개발 중 `PlayerInventory.DropSelectedItem()`의 반환값을
`WorldItem`으로 잘못 받는 코드가 있었다.

실제 반환형:

```text
bool
```

이므로 다음과 같은 컴파일 오류가 발생했다.

```text
CS0029
Cannot implicitly convert type 'bool'
to 'ProjectI.Items.WorldItem'
```

최종 코드에서는:

```text
드롭 전 SelectedItem 참조 저장
↓
DropSelectedItem() 호출
↓
bool 성공 여부 확인
↓
성공한 경우 저장해 둔 WorldItem 참조 사용
```

방식으로 수정했다.

따라서 아이템 참조와 드롭 성공 여부를 분리해서 처리한다.

---

# 16. 사망 아이템 분산

모든 아이템을 정확히 같은 위치에 생성하면
겹침이나 시각적 확인이 어려워질 수 있다.

따라서 사망 위치 주변에 아이템을 분산시켜
월드에 복귀시킨다.

목표는:

```text
죽은 동료 발견
↓
주변 아이템 확인
↓
필요한 아이템만 다시 F로 획득
```

할 수 있는 구조다.

---

# 17. Day21 WagonCargoArea 확장

Day21의:

```text
Assets/ProjectI/Scripts/Wagon/WagonCargoArea.cs
```

를 유지하면서 기능을 확장했다.

기존 역할:

```text
WorldItem
→ CargoArea
→ Secured
```

Day22 이후:

```text
WorldItem
→ CargoArea
→ Secured

Dead Player
→ 같은 CargoArea
→ Recovered
```

로 변경했다.

새로운 별도 마차 회수 시스템을 만들지 않는다.

---

# 18. CargoArea / CorpseArea 분리 폐기

Day22 초기 구현에서는 개념적으로:

```text
CargoArea
CorpseArea
```

를 나누는 구조가 잠시 존재했다.

최종 설계에서는 폐기했다.

마차 후방 창고는:

```text
┌─────────────────┐
│                 │
│                 │
│    CargoArea    │
│  공통 회수 영역 │
│                 │
│                 │
└──── 후방 입구 ──┘
```

하나의 영역으로 사용한다.

---

# 19. 대형 창고 전체가 공통 회수 영역

공통 `CargoArea` Trigger는
Day21의 대형 후방 창고 전체 규격을 유지한다.

현재 검증 기준:

```text
Trigger 길이 약 7.8m
```

회수품과 죽은 플레이어가
같은 창고 안에 함께 놓일 수 있다.

---

# 20. 죽은 플레이어 회수 판정

`PlayerDeathController`는:

```text
IsRecovered
RecoveredArea
```

상태를 가진다.

죽은 플레이어의 `RagdollCenter`가
마차의 공통 CargoArea 안에 들어오면:

```text
IsRecovered = true
```

가 된다.

밖으로 빠져나오면 다시 회수 해제가 가능하도록
공통 마차 영역에서 상태를 관리한다.

---

# 21. WagonCargoArea 공통 관리

Day22 이후 `WagonCargoArea`는 두 종류를 관리한다.

```text
securedItems
recoveredPlayers
```

개념적으로:

```text
SecuredCount
RecoveredPlayerCount
```

를 통해 마차가 현재 확보하고 있는
회수품과 죽은 플레이어 수를 각각 확인할 수 있다.

단, 물리 공간은 하나의 CargoArea를 공유한다.

---

# 22. 이전 CorpseArea 자동 제거

이전 테스트 패치에서 생성되었을 수 있는:

```text
Wagon/CorpseArea
```

는 Day22 Setup이 공통 Wagon.prefab을 열 때 제거한다.

또한 이전 호환 코드:

```text
WagonCorpseArea.cs
```

가 존재할 경우 자동 정리 대상으로 처리한다.

최종 구조에는 별도 CorpseArea가 남지 않는 것이 정상이다.

---

# 23. ExpeditionResult

원정 결과를 구분하기 위해:

```text
Assets/ProjectI/Scripts/Expedition/ExpeditionResult.cs
```

를 추가했다.

결과:

```text
None
NormalReturn
PartialReturn
Failed
```

을 사용한다.

---

# 24. 원정 결과 자동 판정

`ExpeditionOutcomeController`가
현재 `PlayerDeathController`들을 조회한다.

기본 결과:

```text
모두 생존
→ NormalReturn

일부 생존
→ PartialReturn

생존자 0
→ Failed
```

싱글 플레이 테스트에서는:

```text
생존
→ NormalReturn

사망
→ Failed
```

로 동작한다.

---

# 25. 귀환 아이템 판정

정상 또는 부분 귀환에서는 다음 항목을 귀환품으로 유지한다.

```text
WagonCargoItemState.IsSecured == true
마차 SharedStorage 내부 아이템
생존 플레이어가 소지한 아이템
```

이들은:

```text
ReturnedItems
```

목록에 들어간다.

이 목록은 다음 Day23 감정·판매 시스템에서 사용할 수 있도록 공개한다.

---

# 26. 손실 아이템 판정

다음과 같은 아이템은 손실 대상이다.

```text
던전 바닥에 방치
사망 위치 주변에 남음
마차 밖
생존 플레이어 소지품이 아님
```

손실 대상은:

```text
LostItems
```

에 기록한다.

현재 테스트 구현에서는 손실 아이템을:

```text
gameObject.SetActive(false)
```

처리하여 원정 월드에서 제거한다.

---

# 27. 전원 사망

원정 결과가:

```text
Failed
```

이면 `IsReturnedItem()` 여부와 관계없이
현재 원정의 WorldItem을 손실 대상으로 처리한다.

즉 전원 사망 상태에서는
마차에 적재했던 물품도 최종 귀환품으로 확정되지 않는다.

---

# 28. 테스트 초기화

반복 테스트를 위해:

```text
ResetForTesting()
```

기능을 제공한다.

이전에 손실 처리하며 비활성화했던 아이템을 다시 활성화하고:

```text
ReturnedItems 초기화
LostItems 초기화
CurrentResult = None
HasResolved = false
```

상태로 되돌린다.

---

# 29. Day22LethalTester

사망 시스템을 즉시 확인하기 위한 테스트 상호작용:

```text
Day22_Lethal_Ragdoll_Test
```

를 추가했다.

Play Mode에서 빨간 테스트 오브젝트를 보고 F를 누르면
플레이어에게 치명 피해를 적용하여:

```text
HP 0
→ Died
→ Ragdoll
```

흐름을 빠르게 확인할 수 있다.

---

# 30. ExpeditionReturnTerminal

원정 결과 판정을 빠르게 확인하기 위한:

```text
Day22_Return_Result_Terminal
```

도 추가했다.

초록 테스트 오브젝트를 보고 F를 누르면
현재 플레이어 생존 상태를 기준으로:

```text
NormalReturn
PartialReturn
Failed
```

를 판정하고 귀환·손실 아이템을 분류한다.

---

# 31. Day22 테스트 시스템 루트

`ExplorationOffice`에는:

```text
===Day22 Death Expedition System===
```

루트를 생성한다.

주요 구성:

```text
===Day22 Death Expedition System===
├─ ExpeditionOutcomeController
├─ Day22_Lethal_Ragdoll_Test
└─ Day22_Return_Result_Terminal
```

---

# 32. Day22 완료 마커

자동 Setup 적용 완료 여부를 위해:

```text
===Day22 Death Expedition Ready v1===
```

마커를 사용한다.

---

# 33. Phase5Day22Setup

자동 설정 파일:

```text
Assets/ProjectI/Editor/Phase5Day22Setup.cs
```

주요 처리:

```text
ExplorationOffice 확인
Wagon.prefab 확인
Day22 생성 재질 준비

Wagon.prefab 열기
→ 기존 CorpseArea 제거
→ CargoArea를 창고 전체 공통 회수 영역으로 보정
→ 프리팹 저장

ExplorationOffice 열기
→ 기존 Player 검색
→ DeathRagdoll 생성
→ PlayerDeathController 연결
→ Day22 테스트 시스템 생성
→ 씬 저장
→ Validator 실행

이전 WagonCorpseArea 호환 코드 정리
```

---

# 34. 수동 Setup 메뉴

전체 Day22 시스템 재구성:

```text
Tools
→ Project I
→ Day 22
→ Apply Ragdoll Death + Expedition Loss
```

Player 래그돌만 다시 생성:

```text
Tools
→ Project I
→ Day 22
→ Rebuild Player Death Ragdoll
```

---

# 35. Phase5Day22Validator

검증 파일:

```text
Assets/ProjectI/Editor/Phase5Day22Validator.cs
```

주요 검증:

```text
기존 Player 루트 유지
PlayerDeathController 존재
Player 내부 DeathRagdoll 존재
별도 PlayerCorpse.prefab 없음

Rigidbody 11개 이상
CharacterJoint 10개 이상

기존 CharacterController 유지

Day21 Wagon.prefab 유지
CorpseArea 없음
CargoArea 하나만 존재
CargoArea가 창고 전체 범위 사용
동일 CargoArea가 WorldItem과 Dead Player 모두 처리

Day22 시스템 루트 존재
Day22 완료 마커 존재

ExpeditionOutcomeController 존재
치명 피해 테스트 오브젝트 존재
귀환 결과 테스트 오브젝트 존재
```

수동 검증:

```text
Tools
→ Project I
→ Day 22
→ Validate Ragdoll Death + Expedition Loss
```

---

# 36. Day22 생성 재질

기능 테스트를 위해 다음 생성 재질을 추가했다.

```text
DeathRagdoll_Body
DeathTest_Red
ReturnTest_Green
```

용도:

```text
DeathRagdoll_Body
→ 사망 시 보이는 임시 인간형 몸체

DeathTest_Red
→ 치명 피해 테스트 오브젝트

ReturnTest_Green
→ 원정 결과 테스트 오브젝트
```

---

# 37. 최종 사망 테스트 흐름

```text
Play Mode
↓
Day22_Lethal_Ragdoll_Test 접근
↓
F
↓
PlayerHealth HP 0
↓
PlayerDeathController
↓
빠른 슬롯 아이템 월드 드롭
↓
생존 조작 기능 정지
↓
CharacterController OFF
↓
DeathRagdoll ON
↓
Rigidbody Dynamic
↓
몸이 힘없이 쓰러짐
↓
정지 판정
↓
Rigidbody Kinematic
```

---

# 38. 최종 마차 회수 테스트

회수품과 죽은 Player는 같은 창고를 사용한다.

```text
Wagon
└─ CargoArea
   ├─ WorldItem
   ├─ WorldItem
   └─ Dead Player
```

판정:

```text
WorldItem
→ WagonCargoItemState.IsSecured = true

Dead Player
→ PlayerDeathController.IsRecovered = true
```

별도의 CorpseArea는 존재하지 않는다.

---

# 39. 최종 원정 결과 테스트

예:

```text
은 동전
→ CargoArea
→ Secured

검
→ SharedStorage
→ 보관

왕관
→ 던전 바닥
→ 미회수
```

생존 상태에서 귀환 판정:

```text
은 동전 → Returned
검      → Returned
왕관    → Lost
```

전원 사망:

```text
ExpeditionResult.Failed
→ 원정 WorldItem 전체 손실
```

---

# 40. Day22 완료 결과

이번 일차에서 최종적으로 구현된 핵심:

```text
기존 PlayerHealth.Died 재사용

별도 PlayerCorpse 생성 방식 미사용
기존 Player 자체가 Dead 상태로 전환

Player 내부 DeathRagdoll 구성
Rigidbody 11개
CharacterJoint 10개 이상

사망 순간 물리 기반 털썩 쓰러짐
래그돌 정지 감지
정지 후 Rigidbody 물리 고정

사망 시 빠른 슬롯 WorldItem 현장 드롭

Day21 WagonCargoArea 확장
CargoArea와 CorpseArea 분리 폐기
후방 창고 전체를 하나의 공통 회수 영역으로 사용

WorldItem Secured
Dead Player Recovered

NormalReturn
PartialReturn
Failed

ReturnedItems
LostItems

치명 피해 테스트
귀환 결과 테스트

Day22 Setup
Day22 Validator
```

---

# 다음 개발 방향

다음은 Phase 5:

```text
23일차
감정·판매·경제 연결
```

이다.

Day22에서 원정 결과 처리 후:

```text
ExpeditionOutcomeController.ReturnedItems
```

를 확보했다.

따라서 Day23에서는 이 귀환 물품을:

```text
귀환 물품
↓
감정
↓
가치 확정
↓
판매
↓
공동 자금
↓
채무 상환
```

으로 연결하는 것이 핵심이다.

미회수 또는 실패로 손실된:

```text
LostItems
```

는 판매 대상으로 들어가지 않도록 구분한다.
