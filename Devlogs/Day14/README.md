# Project I 개발 일지

## Day 14 — 공통 전투 상태·Damage Pipeline 및 근접 공격 기반 구축

- 날짜: 2026-08-31
- 개발 단계: Phase 4 — 전투·몬스터·함정
- 개발 내용 기준 커밋(amend 전): `d3138d27f05ebc2a3d8911fce5d110e80151aa91`
- 현재 커밋 메시지: `a`
- 이전 커밋: `1b0ba4dad2479965f9221cf104c470f1176b82c9`
- 기준 씬: `Assets/ProjectI/Scenes/ExplorationOffice.unity`

---

## 개발 목표

Phase 4의 첫 단계로 검·활·몬스터·함정·환경 피해가 각각 별도 로직을 가지지 않고
하나의 공통 피해 처리 구조를 사용할 수 있도록 전투 기반 시스템을 구축했다.

이번 일차의 핵심 목표:

- 플레이어·아군·적·환경을 구분하는 공통 진영 체계 구성
- 모든 공격·함정·환경 피해가 공유하는 `DamagePipeline` 구축
- 피해 요청과 결과를 공통 데이터로 전달하는 구조 구성
- 피해 종류와 향후 방어·저항 확장을 위한 `DamageCalculator` 계층 추가
- 플레이어 기존 `PlayerHealth`를 공통 피해 시스템과 연결
- 추락 피해를 공통 Damage Pipeline 경로로 통합
- 공격 상태와 Windup / Active / Recovery 단계 구성
- 기존 `PlayerStamina`에 공격 비용 즉시 소비 기능 연결
- 공격 중 이동 감속과 달리기 제한 기능 연결
- 근접 무기 프레임 간 Sweep 궤적 검사 구성
- 한 공격 안에서 같은 대상을 여러 번 타격하지 않도록 중복 피격 차단
- 근접 공격이 벽을 통과해 뒤의 대상을 맞히지 않도록 벽 차단 구성
- 기존 `WorldItem`·빠른 슬롯·좌클릭 Use 입력과 테스트 검 연동
- 적·아군·벽 뒤 적 더미를 포함한 Day 14 전투 시험장 구성
- F1 `Combat` 진단 페이지 추가
- Day 14 자동 Setup / Validator 추가

---

## 1. 공통 Combat 데이터 구조 추가

새 전투 시스템은 다음 폴더를 중심으로 구성했다.

```text
Assets/ProjectI/Scripts/Combat/
```

주요 공통 타입:

```text
CombatFaction
CombatDamageType
DamageInfo
CombatHitResult
IDamageable
DamageCalculator
DamagePipeline
DamageSource
```

각 무기나 공격 기능이 대상의 체력을 직접 수정하지 않고,
공통 피해 요청 데이터를 만들어 `DamagePipeline`에 전달하도록 구조를 통일했다.

```text
공격 발생
↓
DamageInfo 생성
↓
DamagePipeline
↓
진영 규칙 확인
↓
DamageCalculator
↓
IDamageable
↓
실제 체력 감소
```

---

## 2. CombatFaction 진영 규칙 구축

공통 진영은 다음과 같이 구성했다.

```text
Neutral
Player
Ally
Enemy
Environment
```

현재 기본 피해 규칙:

```text
Player      → Enemy        허용
Player      → Ally         차단
Enemy       → Player       허용
Enemy       → Enemy        차단
Environment → Player       허용
```

Friendly Fire 여부를 각 공격 코드에서 개별 처리하지 않고
`CombatFactionRules`가 공통으로 판단하도록 구성했다.

향후 멀티플레이, 소환물, 중립 NPC, 몬스터 간 전투가 추가되어도
진영 규칙 계층을 확장하는 방식으로 대응할 수 있다.

---

## 3. DamageInfo 공통 피해 요청

공격과 피격에 필요한 정보를 `DamageInfo`로 통합했다.

주요 데이터:

```text
Source
Instigator
SourceFaction
DamageType
BaseDamage
HitPoint
HitNormal
Force
AttackId
```

이를 통해 검, 화살, 몬스터 공격, 가시 함정, 낙하물 등
피해 원인이 달라도 같은 데이터 형식으로 처리할 수 있는 기반을 마련했다.

---

## 4. DamagePipeline 공통 피해 처리

`DamagePipeline.TryApply()`를 모든 피해의 공통 진입점으로 추가했다.

처리 순서:

```text
피격 대상 확인
↓
사망 대상 여부 확인
↓
유효 피해량 확인
↓
진영 간 피해 허용 여부 확인
↓
DamageCalculator에서 최종 피해 계산
↓
IDamageable.ApplyDamage()
↓
CombatHitResult 생성
```

F1 진단을 위해 마지막 피해 요청과 마지막 처리 결과,
실제 적용된 피해 횟수도 기록한다.

피해가 차단된 경우에도 원인을 구분한다.

예:

```text
Target Missing
Target Dead
No Damage
Faction Blocked
Applied
Rejected
```

---

## 5. 기존 PlayerHealth와 공통 피해 시스템 연결

기존 플레이어 체력 시스템은 교체하지 않았다.

```text
PlayerHealth
```

는 그대로 유지하고 새 `PlayerDamageReceiver`를 통해
공통 `IDamageable` 규격과 연결했다.

```text
DamagePipeline
↓
PlayerDamageReceiver
↓
PlayerHealth.TakeDamage()
```

따라서 기존 체력 UI와 사망 이벤트 구조를 유지하면서
앞으로 몬스터·함정·환경 피해도 같은 경로로 플레이어에게 적용할 수 있다.

---

## 6. 추락 피해 Damage Pipeline 통합

기존 `PlayerFallDamage`는 계산된 피해를
`PlayerHealth.TakeDamage()`에 직접 전달했다.

Day 14부터 다음 구조를 사용한다.

```text
PlayerFallDamage
↓
DamageInfo
↓
DamagePipeline
↓
PlayerDamageReceiver
↓
PlayerHealth
```

추락 피해도 `CombatDamageType.Fall`,
`CombatFaction.Environment` 기반 공통 피해로 처리된다.

이를 통해 Phase 4 이후 환경 피해와 전투 피해가
동일한 규칙 체계 안에서 동작하도록 기반을 통합했다.

---

## 7. 공격 상태와 공격 단계 구축

플레이어 전투 상태를 `CombatController`가 관리하도록 구성했다.

주요 전투 상태:

```text
Idle
Attacking
Dead
```

근접 공격은 한 번의 좌클릭으로 즉시 피해를 주지 않고
다음 3단계를 순서대로 거친다.

```text
Windup
↓
Active
↓
Recovery
```

- `Windup`: 공격 준비 단계
- `Active`: 실제 근접 궤적과 피해 판정 활성 단계
- `Recovery`: 공격 후 후딜레이 단계

공격이 정상 종료되거나 무기가 사라지거나 플레이어가 사망하면
현재 공격 상태와 이동 제한을 정리하도록 구성했다.

---

## 8. AttackDefinition 데이터화

테스트 검의 공격 수치를 코드에 직접 고정하지 않고
`AttackDefinition` 에셋으로 분리했다.

Day 14 테스트 검 기본값:

```text
Damage             25
Stamina Cost       12
Windup             0.15 s
Active             0.18 s
Recovery           0.30 s
Movement Multiplier 0.65
Knockback          1.5
```

현재 테스트 데이터:

```text
Assets/ProjectI/Resources/Combat/Day14_TestSword.asset
```

Day 15에서 검·도끼·연속 공격을 구현할 때
공격 데이터 에셋을 추가하는 방식으로 확장할 수 있다.

---

## 9. 기존 PlayerStamina와 공격 비용 연결

전투 전용 스태미나를 새로 만들지 않고
기존 `PlayerStamina`와 `StaminaState`를 확장했다.

추가된 기능:

```text
CanSpend()
TrySpend()
```

테스트 검의 공격 비용은 12다.

예:

```text
100
↓ Attack
88
```

현재 스태미나보다 공격 비용이 크면 소비가 실패하고
공격도 시작되지 않는다.

기존 달리기 스태미나와 전투 스태미나가 같은 자원을 공유한다.

---

## 10. 공격 중 이동 제한

기존 `PlayerMovement`를 교체하지 않고
외부 이동 배율을 받을 수 있도록 확장했다.

공격 시작:

```text
Movement Modifier = AttackDefinition 값
Sprint = 제한
```

테스트 검:

```text
Movement Modifier = 0.65
```

공격 종료:

```text
Movement Modifier = 1.00
Sprint = 정상 복구
```

따라서 무기 종류별로 공격 중 이동 성능을 조정할 수 있다.

---

## 11. 근접 무기 Sweep 궤적 검사

근접 무기는 단순 Collider 접촉 방식이 아니라
이전 프레임과 현재 프레임 사이의 이동 구간을 검사하도록 구성했다.

테스트 검 기준점:

```text
TraceStart
TraceEnd
```

개념:

```text
Previous Trace Position
↓
Sphere Sweep
↓
Current Trace Position
```

빠르게 휘두르는 공격에서도 프레임 사이로 대상이 빠지는 문제를 줄이고,
검날의 실제 이동 구간을 기준으로 피격 여부를 판단한다.

---

## 12. 한 공격당 중복 피격 방지

근접 무기가 같은 대상과 여러 프레임 겹쳐도
한 번의 공격에서는 같은 대상을 한 번만 처리한다.

```text
Attack #1
Enemy A → Hit
Enemy A → Ignore
Enemy A → Ignore

Attack #2
Enemy A → Hit 가능
```

`AttackId`와 공격 중 적중 대상 기록을 사용해
다단 프레임 중복 피해를 차단했다.

---

## 13. 근접 공격 벽 차단

무기가 벽을 통과해 반대편 적을 공격하는 것을 막기 위한
벽 차단 검사를 추가했다.

```text
Player
↓
Sword
↓
Wall
↓
Enemy
```

벽이 공격 경로를 먼저 막으면
뒤의 `IDamageable` 대상에는 피해를 적용하지 않는다.

전투 시험장에는 이를 검증하기 위한
`Combat_BlockerWall`과 벽 뒤 적 더미를 배치했다.

---

## 14. 기존 아이템·빠른 슬롯과 테스트 검 연결

테스트 검은 전투 전용 획득 체계를 새로 만들지 않았다.

기존 구조:

```text
WorldItem
PlayerInventory
PlayerCarryController
IUsableItem
PlayerInputReader.UsePressed
```

에 다음 기능을 연결했다.

```text
MeleeWeaponItem
MeleeWeaponTrace
```

사용 흐름:

```text
F
↓
테스트 검 획득
↓
빠른 슬롯 저장
↓
선택 슬롯에서 손에 표시
↓
좌클릭
↓
MeleeWeaponItem.Use()
↓
CombatController.TryStartAttack()
```

기존 아이템 체계를 유지한 상태에서
정식 근접 무기로 확장할 수 있도록 구성했다.

---

## 15. Day 14 전투 시험장 구성

`ExplorationOffice.unity`의 기존 테스트 구조를 유지하면서
Day 14 전투 기반 시험장을 추가했다.

루트:

```text
===Day14 Combat Foundation===
```

주요 구성:

```text
Combat Test Range
├─ Enemy Dummy
├─ Ally Dummy
├─ Combat Blocker Wall
├─ Enemy Behind Wall
└─ Day14 Combat Test Sword
```

시험 대상은 `CombatHealth`를 사용해
플레이어 체력과 독립된 공통 피해 대상 역할을 한다.

기본 시험:

```text
Enemy Dummy
100 → 75

Ally Dummy
100 → 100

Enemy Behind Wall
벽으로 공격 차단
```

---

## 16. F1 Combat 진단 페이지 추가

새 진단 페이지:

```text
Assets/ProjectI/Scripts/Diagnostics/CombatDebugPage.cs
```

F1 공통 디버그 창에서 전투 상태를 확인할 수 있다.

주요 표시 정보:

```text
Combat State
Attack Phase
Attack ID
Weapon
Damage
Damage Type
Stamina Cost
Current Stamina
Movement Modifier
Damage Pipeline Last Result
Source Faction
Target
Applied Damage
Faction Allowed
Last Wall Hit
Combat Dummy HP
```

앞으로 Day 15~19 전투·몬스터·함정 구현에서도
공통 전투 진단 페이지를 계속 확장할 수 있다.

---

## 17. Day 14 자동 Setup

새 파일:

```text
Assets/ProjectI/Editor/Phase4Day14Setup.cs
```

자동 Setup이 다음 요소를 구성한다.

```text
Player
├─ PlayerDamageReceiver
└─ CombatController

Day14 Combat Test Range
├─ Test Sword
├─ Enemy Dummy
├─ Ally Dummy
├─ Enemy Behind Wall
└─ Blocker Wall

CombatDebugPage
Day14_TestSword.asset
Day14 테스트 재질
```

기존 씬과 플레이어 구조를 최대한 유지하면서
필요한 Day 14 요소만 추가하는 방식으로 구성했다.

---

## 18. Day 14 Validator

새 파일:

```text
Assets/ProjectI/Editor/Phase4Day14Validator.cs
```

검증 항목:

```text
Day14 전투 시험장 존재
PlayerHealth 존재
PlayerStamina 존재
PlayerMovement 존재
PlayerDamageReceiver 연결
CombatController 존재
F1 CombatDebugPage 존재
CombatHealth 테스트 더미 3개 이상
Enemy 진영 대상 존재
Ally 진영 대상 존재
MeleeWeaponItem 테스트 검 존재
WorldItem 연동
TraceStart / TraceEnd 연결
Day14_TestSword AttackDefinition 존재
Damage = 25
Stamina Cost = 12
Combat_BlockerWall Collider 존재
```

순수 규칙 검증:

```text
Player → Enemy       허용
Player → Ally        차단
Enemy → Enemy        차단
Enemy → Player       허용
Environment → Player 허용
```

스태미나 검증:

```text
100 - 12 = 88
보유량 초과 소비 실패
실패한 소비 후 현재값 유지
```

---

## 19. 주요 생성 파일

```text
Assets/ProjectI/Scripts/Combat/
├─ AttackDefinition.cs
├─ AttackPhase.cs
├─ CombatController.cs
├─ CombatDamageType.cs
├─ CombatFaction.cs
├─ CombatFactionRules.cs
├─ CombatHealth.cs
├─ CombatHitResult.cs
├─ CombatState.cs
├─ DamageCalculator.cs
├─ DamageInfo.cs
├─ DamagePipeline.cs
├─ DamageSource.cs
├─ IDamageable.cs
├─ MeleeWeaponItem.cs
├─ MeleeWeaponTrace.cs
└─ PlayerDamageReceiver.cs
```

추가:

```text
Assets/ProjectI/Scripts/Diagnostics/CombatDebugPage.cs
Assets/ProjectI/Editor/Phase4Day14Setup.cs
Assets/ProjectI/Editor/Phase4Day14Validator.cs
Assets/ProjectI/Resources/Combat/Day14_TestSword.asset
Assets/ProjectI/Art/Generated/Day14/*
```

수정:

```text
Assets/ProjectI/Scripts/Player/PlayerFallDamage.cs
Assets/ProjectI/Scripts/Player/PlayerMovement.cs
Assets/ProjectI/Scripts/Player/PlayerStamina.cs
Assets/ProjectI/Scripts/Player/StaminaState.cs
Assets/ProjectI/Scenes/ExplorationOffice.unity
```

기존 시스템 파일을 삭제하지 않고 공통 전투 기반을 추가했다.

---

## 20. Day 14 완료 기준

Day 14 기준 목표 상태:

```text
테스트 검 F 획득
↓
좌클릭 공격
↓
Windup
↓
Active
↓
근접 Sweep 검사
↓
Damage Pipeline
↓
Enemy Dummy 피해
↓
Recovery
↓
Idle
```

추가 검증:

```text
Enemy Dummy       → 피해 적용
Ally Dummy        → Friendly Fire 차단
Enemy Behind Wall → 벽 차단
공격 스태미나     → 12 소비
공격 중 이동      → 65%
추락 피해         → Damage Pipeline 사용
F1 Combat         → 상태 확인
```

---

## 다음 개발 방향

### Day 15 — 근접 전투 완성

Day 14에서 만든 공통 전투 기반 위에
실제 근접 전투 콘텐츠를 확장한다.

예정 내용:

- 검 기본 공격
- 검 연속 공격
- 도끼 기본 공격
- 도끼 연속 공격
- 맨손 밀치기
- 공격별 Windup / Active / Recovery 조정
- 경직 시스템 실제 적용
- 넉백 시스템 실제 적용
- 무기별 공격 궤적 차이
- 근접 공격 시각·피격 반응 개선

Day 14에서는 **전투 공통 기반**을 만들었고,
Day 15부터 이 기반 위에 실제 무기별 전투 감각을 완성한다.
