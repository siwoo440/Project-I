# Project I 개발 일지

## Day 17 — 몬스터 공통 감지·추적 AI 및 4종 몬스터 전투 기믹 구현

- 날짜: 2026-08-31
- 개발 단계: Phase 4 — 전투·몬스터·함정
- 개발 내용 기준 커밋(amend 전): `3baa3ef5239a37869d30d034c2faaf6f1c431b72`
- 현재 커밋 메시지: `17`
- 이전 커밋: `d82eb0c78f3f4f8adf5deb4bbf2f14c2acf5ee7b`
- 기준 씬: `Assets/ProjectI/Scenes/ExplorationOffice.unity`

---

## 개발 목표

Day 14~16에서 완성한 공통 Damage Pipeline과 플레이어 근접·원거리 전투 기반을
몬스터 행동 시스템과 연결했다.

이번 일차에서는 단순한 테스트 더미를 넘어
몬스터가 플레이어를 보고, 듣고, 추적하고, 마지막 위치를 기억하고,
공격 가능한 상황을 판단하도록 공통 AI 계층을 구성했다.

또한 공통 AI 검증용 실제 몬스터로 다음 4종을 추가했다.

```text
부패한 망자
부패한 망자 궁수
웃는 석상
상자 미믹
```

각 몬스터는 동일한 공통 감지·이동·Damage Pipeline 기반을 최대한 공유하면서
서로 다른 전투 규칙을 가지도록 구성했다.

핵심 목표:

- MonsterData 기반 공통 몬스터 수치 관리
- MonsterState 기반 AI 상태 구성
- 거리·시야각·벽 차단을 포함한 시각 감지
- Noise Event 기반 청각 감지
- 플레이어 걷기·달리기 소음 연결
- 석궁·리볼버 발사 소음을 몬스터 청각에 연결
- 대상 선정과 Last Known Position 기억
- Chase / Investigate / Retreat 이동 구조
- 플레이어에게 피격되면 공격자를 Target으로 전환
- Enemy → Player Damage Pipeline 연결
- MonsterSpawnPoint 기반 런타임 생성
- 지정된 SprintLane 북쪽 위치에 4종을 나란히 Spawn
- 근접 부패한 망자 구현
- 활을 사용하는 부패한 망자 궁수 구현
- 포물선 적 화살 구현
- 웃는 석상 관찰 기반 규칙 구현
- 웃는 석상 무체력·비피격·불사 규칙 구현
- 상자 미믹 위장·변신·근접 공격 구현
- F1 Monster AI 진단 페이지 추가
- Day17 자동 Setup / Validator 구성

---

## 1. MonsterData 공통 데이터 구조

새 몬스터 수치 데이터:

```text
Assets/ProjectI/Scripts/Monsters/MonsterData.cs
```

를 추가했다.

몬스터별 핵심 수치를 ScriptableObject로 분리해
AI 코드에 직접 수치를 고정하지 않도록 구성했다.

주요 데이터:

```text
Display Name
Archetype

Max Health

Move Speed
Chase Speed
Retreat Speed
Turn Speed

Vision Range
Vision Angle
Vision Interval

Hearing Range
Memory Duration
Investigate Duration

Preferred Min Range
Preferred Max Range

Attack Range
Aim / Windup Time
Attack Cooldown

Projectile Speed
Attack Damage
Stagger Power
Knockback Force

Stagger Threshold
Knockback Resistance
```

이를 통해 공통 `MonsterBrain`을 유지하면서
몬스터별 전투 역할을 데이터로 구분할 수 있다.

---

## 2. MonsterArchetype 추가

몬스터의 행동 유형을 구분하기 위해 다음 유형을 사용한다.

```text
CorruptedUndead
CorruptedUndeadArcher
SmilingStatue
ChestMimic
```

각 Archetype은 공통 MonsterData를 사용하지만
필요한 경우 별도의 특수 Behavior를 추가한다.

---

## 3. MonsterState 공통 상태

공통 AI 상태:

```text
Idle
Suspicious
Investigate
Chase
Attack
Retreat
Staggered
Dead
```

를 구성했다.

일반적인 몬스터 행동:

```text
Idle
↓
플레이어 감지
↓
Chase
↓
Attack
```

플레이어를 놓친 경우:

```text
직접 시야 상실
↓
Last Known Position
↓
Investigate
↓
재발견 실패
↓
Idle
```

원거리 궁수는 플레이어가 너무 가까우면:

```text
Attack
↓
Retreat
↓
선호 거리 회복
↓
Attack
```

을 사용한다.

---

## 4. MonsterSensor 시각 감지

새 공통 감지 계층:

```text
MonsterSensor
```

에서 다음 세 조건을 함께 검사한다.

```text
거리
+
시야각
+
벽 차단
```

즉 단순히 플레이어가 일정 거리 안에 있다고
자동으로 발견하지 않는다.

개념:

```text
Monster
   \ Vision Angle /
        Player
```

이어도 중간에 벽이 있으면:

```text
Monster
↓
Wall
↓
Player

Visible = false
```

로 처리한다.

시각 Raycast는 몬스터마다 매 프레임 반복하지 않고
MonsterData의 `VisionInterval`을 사용한다.

---

## 5. MonsterSensor Object 모호성 오류 수정

Day17 최초 적용 과정에서:

```text
MonsterSensor.cs
CS0104
'Object' is an ambiguous reference
```

컴파일 오류가 발생했다.

원인은:

```text
System.Object
UnityEngine.Object
```

가 동시에 유효한 상태에서
`Object.FindFirstObjectByType`를 호출한 것이었다.

최종 수정:

```text
UnityEngine.Object.FindFirstObjectByType<PlayerDamageReceiver>()
```

처럼 Unity Object를 명시했다.

최종 Day17 코드에는 이 수정이 포함되어 있다.

---

## 6. MonsterNoiseSystem 청각 이벤트

몬스터 청각 감지를 위해:

```text
MonsterNoiseSystem
```

을 추가했다.

청각은 모든 몬스터가 매 프레임 주변을 Physics 검사하지 않고
실제 소음이 발생하는 순간 Event를 전달한다.

기본 구조:

```text
Player / Weapon
↓
Noise Emit
↓
MonsterNoiseSystem
↓
주변 MonsterSensor
↓
Hearing Range 검사
↓
소음 위치 기억
```

---

## 7. 플레이어 이동 소음

플레이어에:

```text
PlayerNoiseEmitter
```

를 추가했다.

현재 기본 테스트값:

```text
걷기 소음     약 6m
달리기 소음   약 14m
```

발생 간격도 달리기가 걷기보다 짧게 구성했다.

따라서 몬스터 시야 밖에서도
플레이어가 가까이서 달리면 위치를 조사할 수 있다.

---

## 8. 플레이어 원거리 무기 소음 연결

Day16 원거리 무기와 MonsterNoiseSystem을 연결했다.

석궁:

```text
Player Crossbow Shot
→ 약 12m 소음
```

리볼버:

```text
Revolver Gunshot
→ 약 30m 소음
```

따라서 벽 뒤에서 총을 쏴도
시야 감지와 별개로 몬스터가 소리를 들을 수 있다.

예:

```text
Monster
↓
Wall
↓
Player Revolver Shot

Visible = false
Heard = true
↓
Investigate
```

---

## 9. MonsterTargetSelector

대상과 기억 위치를 분리 관리하는:

```text
MonsterTargetSelector
```

를 추가했다.

주요 정보:

```text
Current Target
Last Known Position
Last Seen Time
Last Heard Position
Memory
```

플레이어가 벽 뒤로 숨는 순간
AI가 즉시 Idle 상태로 돌아가지 않도록 한다.

---

## 10. Last Known Position

플레이어를 직접 보고 있을 때:

```text
LastKnownPosition = Player.position
```

을 계속 갱신한다.

이후 시야가 끊기면:

```text
Player Lost
↓
Last Known Position으로 이동
↓
주변 조사
↓
재발견 여부 확인
```

으로 동작한다.

몬스터가 벽 뒤에서 플레이어를 계속 정확히 추적하지 않으면서도
시야가 끊긴 순간 멈추는 현상을 방지했다.

---

## 11. MonsterMotor 공통 이동 계층

AI 판단과 실제 이동을 분리하기 위해:

```text
MonsterBrain
→ 행동 결정

MonsterMotor
→ 실제 이동 / 회전
```

구조를 사용한다.

MonsterMotor 주요 기능:

```text
MoveTo
MoveAwayFrom
Stop
FaceTarget
```

이를 통해 근접형·궁수·석상·미믹이
필요한 이동 명령만 다르게 사용할 수 있다.

---

## 12. MonsterBrain 공통 AI

일반 몬스터용 공통 상태 머신:

```text
MonsterBrain
```

을 구성했다.

MonsterBrain은 다음을 연결한다.

```text
MonsterData
MonsterSensor
MonsterTargetSelector
MonsterMotor
CombatHealth
CombatReaction
MonsterMeleeAttack
CorruptedUndeadArcherAttack
```

즉 하나의 Brain이
근접 공격과 원거리 공격을 모두 지원할 수 있다.

---

## 13. 피격 Aggro

기존 `CombatHealth`에 실제 피해가 발생했을 때
DamageInfo를 전달하는 이벤트를 추가했다.

흐름:

```text
Player Weapon
↓
Damage Pipeline
↓
CombatHealth
↓
Damaged Event
↓
MonsterBrain
↓
Instigator 확인
↓
Player를 Target으로 등록
```

따라서 플레이어가 몬스터 뒤에서 공격해도
몬스터가 공격자를 인식하고 전투 상태로 전환할 수 있다.

---

## 14. MonsterMeleeAttack 공통 근접 공격

근접 몬스터가 공유하는:

```text
MonsterMeleeAttack
```

을 추가했다.

사용 몬스터:

```text
부패한 망자
웃는 석상
상자 미믹
```

기본 공격 흐름:

```text
Attack Range 진입
↓
Windup
↓
목표 재확인
↓
벽 차단 검사
↓
DamageInfo
↓
DamagePipeline
↓
PlayerDamageReceiver
```

몬스터가 플레이어 체력을 직접 수정하지 않는다.

---

## 15. Enemy → Player Damage Pipeline

모든 몬스터 공격은 기존 공통 전투 시스템을 사용한다.

```text
Monster Attack
↓
DamageInfo

Faction = Enemy

↓
DamagePipeline
↓
CombatFactionRules
↓
PlayerDamageReceiver
↓
PlayerHealth
```

따라서 Player → Enemy 피해와
Enemy → Player 피해가 같은 Pipeline을 사용한다.

---

# 몬스터 4종

## 16. 부패한 망자

가장 기본적인 근접 추적 몬스터다.

최종 테스트 체력:

```text
HP 70
```

주요 행동:

```text
Idle
↓
시각 / 청각 감지
↓
Chase
↓
근접 거리
↓
Windup
↓
근접 공격
↓
Cooldown
```

대표 설정:

```text
Move Speed      2.15
Chase Speed     3.45
Attack Damage   22
Attack Range    1.85m
Windup          0.36s
Cooldown        1.15s
```

플레이어의 검·도끼·석궁·리볼버 공격으로
CombatHealth와 CombatReaction을 받을 수 있다.

---

## 17. 부패한 망자 외형

프리미티브 조합으로
단순 캡슐 테스트 대상보다 몬스터 형태가 보이도록 구성했다.

주요 파트:

```text
Head
Skull
Jaw
Red Eyes

Torso
Pelvis
Ribs

Left / Right Arms
Left / Right Legs
Boots

Rust Chest Armor
Shoulder Armor
Torn Cloth
```

---

## 18. 부패한 망자 궁수

원거리 AI 테스트용 실제 전투 몬스터다.

최종 테스트 체력:

```text
HP 55
```

핵심 행동:

```text
Player 발견
↓
거리 확인

너무 멂
→ Chase

선호 거리
→ Aim / Fire

너무 가까움
→ Retreat
```

선호 전투 거리:

```text
약 8 ~ 13m
```

---

## 19. 궁수 활 모델

궁수 손에 프리미티브 기반 활을 구성했다.

```text
Bow Grip
Curved Limb Parts
String
Nocked Arrow
Muzzle
```

등에는:

```text
Quiver
Arrow ×5
```

시각 요소도 추가했다.

---

## 20. 궁수 활 공격 모션

`CorruptedUndeadArcherAttack`이
Animator 없이 Transform 기반 기본 공격 모션을 담당한다.

```text
활 들어 올림
↓
플레이어 방향 정렬
↓
팔을 뒤로 당김
↓
StringRoot 이동
↓
Aim
↓
Arrow 발사
↓
활 / 시위 기본 자세 복구
↓
Cooldown
```

Aim Time과 Cooldown은 MonsterData에서 관리한다.

---

## 21. 궁수 포물선 화살

새 적 투사체:

```text
MonsterArrowProjectile
```

을 추가했다.

기본 테스트 속도:

```text
24 m/s
```

이며 Rigidbody Gravity를 사용한다.

```text
Bow
↓
Ballistic Launch
↓
Rigidbody
↓
Gravity
↓
Arc
↓
Player
```

따라서 플레이어 석궁의 빠른 볼트와 다르게
화살이 날아오는 것을 확인하고 회피할 수 있는 속도로 구성했다.

---

## 22. MonsterBallistics

궁수가 단순히 플레이어 현재 위치를 향해 직선으로 쏘지 않고
중력 환경에서 목표 지점까지 날아갈 초기 속도를 계산하도록:

```text
MonsterBallistics
```

를 추가했다.

계산에 성공하면 해당 초기 Velocity를 사용하고,
계산할 수 없는 조건에서는 안전한 대체 방향을 사용한다.

---

## 23. 적 화살 회수 불가

플레이어 석궁 볼트와 규칙을 분리했다.

플레이어 볼트:

```text
충돌
→ F 회수 가능
```

몬스터 화살:

```text
충돌
→ 잠시 박힘
→ 자동 삭제
```

`IInteractable`을 구현하지 않으므로
플레이어가 적 화살을 탄약으로 회수할 수 없다.

---

# 웃는 석상

## 24. 웃는 석상 기본 규칙

웃는 석상은 일반적인 HP 몬스터가 아니다.

최종 규칙:

```text
체력 없음
피격 불가능
경직 불가능
넉백 불가능
사망 불가능
```

MonsterData에서도:

```text
MaxHealth = 0
```

을 유지한다.

---

## 25. 웃는 석상 Damageable 제거

웃는 석상 Prototype에는:

```text
CombatHealth
CombatReaction
```

을 생성하지 않는다.

따라서:

```text
검
도끼
석궁
리볼버
```

어떤 플레이어 공격도 웃는 석상에
체력 피해·경직·넉백을 적용할 수 없다.

즉 웃는 석상은 반드시 죽지 않는 규칙형 위협이다.

---

## 26. 웃는 석상 화면 관찰 규칙

초기 구현에서는 카메라 정면과 석상 사이의 각도를 이용했지만,
최종 버전에서는 실제 Camera Viewport를 사용한다.

검사 지점:

```text
Face
Torso
Base
```

각 지점을:

```text
Camera.WorldToViewportPoint()
```

로 변환한다.

다음 조건을 만족하면 화면 내로 판단한다.

```text
Viewport X = 0 ~ 1
Viewport Y = 0 ~ 1
Viewport Z > 0
```

---

## 27. 웃는 석상 벽 차단 판정

단순히 화면 좌표 안에 있다고 관찰되는 것이 아니다.

카메라에서 석상 검사 지점까지:

```text
RaycastAll
```

을 실행한다.

```text
Camera
↓
Wall
↓
Statue
```

이면:

```text
Observed = false
```

이다.

실제 석상이 화면 안에서 직접 보일 때만
관찰 상태가 된다.

---

## 28. 화면 안에서 완전 동결

얼굴·몸통·하단 중 한 지점이라도
실제로 화면 안에서 보이면:

```text
Observed = true
```

즉시:

```text
MonsterMotor.Stop()
MonsterMeleeAttack.CancelAttack()
```

을 실행한다.

중요 규칙:

```text
이동 금지
회전 금지
공격 금지
진행 중 공격 취소
```

따라서 석상이 플레이어 바로 앞까지 접근한 상태에서도
플레이어 화면에 다시 들어오는 순간 공격할 수 없다.

---

## 29. 웃는 석상 화면 밖 행동

석상이 화면에서 완전히 벗어나면:

```text
Observed = false
```

가 되고 다시 행동할 수 있다.

```text
화면 밖
↓
빠르게 접근
↓
공격 거리 진입
↓
Player가 계속 보지 않는 경우
↓
근접 공격
```

플레이어가 다시 화면 안으로 석상을 넣으면
현재 공격을 즉시 취소한다.

---

## 30. 웃는 석상 F1 진단

Monster AI 페이지에서 웃는 석상은:

```text
HP         : NONE / INVULNERABLE
Damageable : NO
Observed   : YES / NO
Distance
```

형태로 표시한다.

일반 HP 몬스터와 명확히 구분된다.

---

# 상자 미믹

## 31. 상자 미믹

상자 미믹은 위장 상태에서 시작한다.

최종 테스트 체력:

```text
HP 80
```

위장 중:

```text
닫힌 상자
AI 비활성
이동 없음
공격 없음
```

---

## 32. 미믹 접근 변신

플레이어가 설정된 거리 안으로 접근하면:

```text
Disguised
↓
Reveal
```

을 시작한다.

변신 표현:

```text
Lid Open
↓
Eyes Show
↓
Teeth Show
↓
Tongue Show
↓
Legs Show
↓
MonsterBrain 활성화
```

---

## 33. 미믹 피격 변신

플레이어가 위장된 상자를 먼저 공격해도
Reveal을 시작한다.

```text
Player Attack
↓
CombatHealth.Damaged
↓
ChestMimicBehavior
↓
BeginReveal()
```

즉 단순한 장식 상자처럼 맞고만 있지 않는다.

---

## 34. 변신 후 미믹 공격

Reveal 완료 후:

```text
MonsterBrain 활성
↓
Player Target
↓
Chase
↓
MonsterMeleeAttack
↓
Damage Pipeline
```

을 사용한다.

대표 설정:

```text
HP              80
Chase Speed     4.0
Attack Damage   30
Attack Range    1.70m
Windup          0.28s
Cooldown        1.05s
```

---

## 35. 미믹 모델링

프리미티브 조합:

```text
Wooden Chest
Metal Bands
Latch
Lid

Reveal:
Eyes
Upper Teeth
Lower Teeth
Tongue
Legs
```

를 사용해 위장 전과 변신 후가 시각적으로 구분되도록 했다.

---

# Spawn / 테스트 맵

## 36. MonsterSpawnPoint

런타임 몬스터 생성을 위한:

```text
MonsterSpawnPoint
```

를 추가했다.

SpawnPoint는:

```text
Prototype
Runtime Name
Auto Spawn
Spawn Delay
```

를 관리한다.

Prototype 자체는 테스트 씬에서 비활성 상태로 두고
Play Mode에서 복제 인스턴스를 생성한다.

---

## 37. 4종 Spawn Line

사용자가 지정한 `01_SprintLane` 북쪽 위치에
4개 SpawnPoint를 가로로 정렬했다.

중심:

```text
(-27, 0, 25.8)
```

Spawn 간격:

```text
2.15m
```

배치:

```text
북쪽

[부패한 망자]
[부패한 망자 궁수]
[웃는 석상]
[상자 미믹]

──────── SprintLane ────────
```

각 SpawnPoint에는 서로 다른 Prototype이 연결되어 있다.

---

## 38. Spawn 재질과 표시

Day17 테스트 전용:

```text
Monster_Spawn
Monster_SpawnAccent
```

재질을 생성해
어느 위치에서 몬스터가 생성되는지
Edit Mode에서도 확인할 수 있게 구성했다.

---

# 전투 밸런스

## 39. 최종 테스트 체력

테스트 과정에서 일반 몬스터의 체력을 초기값보다 낮췄다.

최종값:

```text
부패한 망자       70 HP
부패한 망자 궁수  55 HP
웃는 석상          HP 없음
상자 미믹          80 HP
```

웃는 석상은 110 HP로 낮추는 단계를 거친 뒤
최종적으로 HP 시스템 자체를 제거했다.

---

## 40. CombatReaction 연결

HP를 사용하는:

```text
부패한 망자
부패한 망자 궁수
상자 미믹
```

은 기존 Day15:

```text
CombatReaction
```

을 재사용한다.

따라서 플레이어 무기에 의해:

```text
Stagger
Knockback
```

반응을 받을 수 있다.

웃는 석상은 이 반응 계층에서도 제외된다.

---

# 진단 시스템

## 41. F1 Monster AI 페이지

새 진단 페이지:

```text
Assets/ProjectI/Scripts/Diagnostics/MonsterAIDebugPage.cs
```

를 추가했다.

일반 AI 표시:

```text
State
HP
Target
Distance

Visible
Heard
Memory
Last Known Position

Vision Range
Vision Angle

Attack Range
Cooldown
```

---

## 42. 궁수 진단

궁수는 추가로:

```text
Aim Progress
Attack Cooldown
Arrow Speed
```

을 표시한다.

런타임 적 화살 개수도 확인할 수 있다.

---

## 43. 웃는 석상 진단

웃는 석상:

```text
Observed
Invulnerable
Damageable
Distance
```

을 별도로 표시한다.

특히:

```text
HP NONE
Damageable NO
```

를 통해 일반 전투 몬스터와 다른 규칙을 확인할 수 있다.

---

## 44. 미믹 진단

미믹:

```text
Disguised
Revealing
Reveal Progress
Brain Active
Distance
```

등을 확인할 수 있다.

---

# 자동 Setup / Validator

## 45. Phase4Day17Setup

새 자동 구성 파일:

```text
Assets/ProjectI/Editor/Phase4Day17Setup.cs
```

최종 자동 적용 마커:

```text
===Day17 Monster AI Ready v4===
```

v4는 다음 보정까지 포함한다.

```text
4종 몬스터 구성
몬스터 일반 체력 감소
웃는 석상 무체력
웃는 석상 비피격
웃는 석상 Viewport 관찰
관찰 중 공격 즉시 취소
```

---

## 46. 자동 생성 MonsterData

Setup에서 다음 에셋을 생성·갱신한다.

```text
Assets/ProjectI/Resources/Monsters/
├─ Day17_CorruptedUndead.asset
├─ Day17_CorruptedUndeadArcher.asset
├─ Day17_SmilingStatue.asset
└─ Day17_ChestMimic.asset
```

---

## 47. Day17 자동 생성 재질

```text
Assets/ProjectI/Art/Generated/Day17/
```

에 몬스터 테스트 모델용 재질을 생성한다.

예:

```text
Monster_Flesh
Monster_Bone
Monster_Rust
Monster_Cloth
Monster_BowWood
Monster_String
Monster_Eye
Monster_StatueStone
Monster_StatueDark
Monster_MimicTongue
Monster_Spawn
Monster_SpawnAccent
```

---

## 48. Phase4Day17Validator

Day17 Validator는 다음 주요 조건을 검사한다.

```text
Day17 Root
Ready v4 Marker

SpawnPoint 4개
4개 서로 다른 Prototype
Spawn 위치

부패한 망자 데이터
궁수 데이터
상자 미믹 데이터

MonsterBrain
MonsterSensor
MonsterMotor
MonsterTargetSelector

근접 공격 연결
궁수 공격 연결
Enemy → Player 피해 규칙

웃는 석상 CombatHealth 없음
웃는 석상 CombatReaction 없음
웃는 석상 IDamageable 없음
웃는 석상 Invulnerable 규칙

F1 Monster AI
```

---

## 49. 주요 신규 코드

```text
Assets/ProjectI/Scripts/Monsters/
├─ MonsterArchetype.cs
├─ MonsterData.cs
├─ MonsterState.cs
├─ MonsterBrain.cs
├─ MonsterSensor.cs
├─ MonsterTargetSelector.cs
├─ MonsterMotor.cs
├─ MonsterNoiseSystem.cs
├─ PlayerNoiseEmitter.cs
├─ MonsterSpawnPoint.cs
├─ MonsterBallistics.cs
├─ MonsterArrowProjectile.cs
├─ MonsterMeleeAttack.cs
├─ CorruptedUndeadArcherAttack.cs
├─ SmilingStatueBehavior.cs
├─ ChestMimicBehavior.cs
└─ IMonsterSpecialSense.cs
```

---

## 50. 주요 수정 코드

```text
Assets/ProjectI/Scripts/Combat/
└─ CombatHealth.cs

Assets/ProjectI/Scripts/Combat/Ranged/
├─ CrossbowWeaponItem.cs
└─ RevolverWeaponItem.cs
```

변경 목적:

```text
CombatHealth
→ 피격 공격자 Aggro 이벤트

CrossbowWeaponItem
→ 석궁 발사 소음

RevolverWeaponItem
→ 리볼버 총성 소음
```

---

## 51. Day17 테스트 흐름

### 시각 감지

```text
플레이어가 정면 접근
↓
Monster Visible
↓
Chase / Attack
```

벽 뒤:

```text
Player
↓
Wall
↓
Monster

Visible = false
```

---

### 청각 감지

```text
벽 뒤에서 리볼버 발사
↓
Gunshot Noise
↓
MonsterSensor
↓
Investigate
```

---

### Last Known Position

```text
Player 발견
↓
벽 뒤로 이동
↓
시야 상실
↓
Last Known Position 이동
↓
주변 조사
```

---

### 부패한 망자

```text
감지
↓
Chase
↓
근접 공격
↓
Player Damage
```

---

### 부패한 망자 궁수

```text
감지
↓
거리 조절
↓
Aim
↓
포물선 화살
↓
Player Damage
```

---

### 웃는 석상

```text
화면 밖
↓
빠르게 접근

화면 안
↓
즉시 완전 정지

화면 안 + 근접 상태
↓
공격 불가

플레이어 공격
↓
피해 없음
사망 없음
```

---

### 상자 미믹

```text
Closed Chest
↓
플레이어 접근 / 공격
↓
Reveal
↓
MonsterBrain 활성
↓
Chase
↓
Bite Attack
```

---

## 52. Day17 완료 결과

Day17을 통해 플레이어 전투 시스템과 연결되는
공통 몬스터 행동 기반을 구축했다.

최종 흐름:

```text
Monster Spawn
↓
Visual / Hearing Sense
↓
Target Selection
↓
Last Known Position
↓
Chase / Investigate / Retreat
↓
Monster-specific Behavior
↓
Attack
↓
Damage Pipeline
↓
Player
```

개별 몬스터:

```text
부패한 망자
→ 기본 근접 추적

부패한 망자 궁수
→ 거리 유지 + 포물선 원거리 공격

웃는 석상
→ 화면 내 완전 정지 + 불사·비피격

상자 미믹
→ 상자 위장 + 접근/피격 변신 + 근접 공격
```

으로 역할을 구분했다.

---

## 다음 개발 방향

### Day 18 — 몬스터 3종 행동 완성 및 실제 던전 적용 준비

Day17에서 공통 AI와 4종 테스트 몬스터의 기능 기반을 만들었으므로
다음 단계에서는 테스트용 행동을 실제 게임용 수준으로 정리한다.

예정 방향:

```text
부패한 망자
→ 이동·공격 반응 개선

웃는 석상
→ 관찰 규칙 세부 안정화

상자 미믹
→ 위장·변신 연출 보강

몬스터 모델 / 애니메이션 교체 가능 구조 정리
Spawn 규칙 정리
실제 절차 생성 던전 배치 연결 준비
전투 밸런스 조정
```

Day17의 공통 감지·타겟·이동·Damage Pipeline 구조는 유지한다.
