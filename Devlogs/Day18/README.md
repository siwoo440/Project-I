# Project I 개발 일지

## Day 18 — 바닥·천장 가시 및 도끼·압력판 함정과 공통 Damage Pipeline 구현

- 날짜: 2026-08-31
- 개발 단계: Phase 4 — 전투·몬스터·함정
- 개발 내용 기준 커밋(amend 전): `d0c19a7f7327f547c79615a2ff78e356b1de9fb3`
- 현재 커밋 메시지: `18`
- 이전 커밋: `433580c9c42a2a9c90c6a0a64c52a4765014ed7f`
- 기준 씬: `Assets/ProjectI/Scenes/ExplorationOffice.unity`

---

## 개발 목표

Day 14~17에서 구축한 공통 Damage Pipeline과 플레이어·몬스터 피해 구조에
환경 위험 요소인 함정을 연결했다.

이번 일차에서는 다음 함정 4종을 구현했다.

```text
바닥 돌출 가시
천장 주기 내려찍기 가시
회전 도끼
압력판
```

모든 피해형 함정은 플레이어 체력을 직접 수정하지 않고
`CombatFaction.Environment`와 `CombatDamageType.Trap`을 사용해
기존 `DamagePipeline`으로 피해를 전달한다.

핵심 목표:

- 함정 공통 상태 구조 작성
- 함정 공통 DamageSource 작성
- 한 작동 주기에서 같은 대상에게 1회만 피해
- 플레이어와 피해 가능한 몬스터 모두 함정 피해 허용
- 바닥 가시 상승·하강
- 천장 가시판 자동 반복 Slam
- 통로형 Swing 도끼
- 플레이어·몬스터 공용 압력판
- 숨은 Trigger를 통한 도끼 자동 작동
- 웃는 석상 비피격·불사 규칙 유지
- F1 Trap 진단 페이지 추가
- Day18 Setup / Validator 자동 구성

---

## 1. TrapState 공통 상태

새 공통 상태:

```text
Ready
Warning
Active
Returning
Cooldown
Waiting
```

을 기반으로 각 함정이 필요한 단계만 사용한다.

이를 통해 함정마다 임의의 bool 조합을 만드는 대신
현재 동작 단계를 명확히 구분할 수 있게 했다.

---

## 2. TrapControllerBase

새 공통 기반:

```text
TrapControllerBase
```

를 추가했다.

공통 관리 항목:

```text
Display Name
Trap State
TrapDamageSource
Activation Count
Last Trigger Source
Attack ID
```

각 함정은 이 기반을 상속하고
자신의 움직임과 작동 주기만 별도로 구현한다.

---

## 3. TrapDamageSource

피해 판정은:

```text
TrapDamageSource
```

로 통일했다.

피해 흐름:

```text
Trap
↓
TrapDamageSource
↓
DamagePipeline.FindDamageable()
↓
DamageInfo

SourceFaction = Environment
DamageType = Trap

↓
DamagePipeline.TryApply()
```

따라서 함정별로 PlayerHealth나 CombatHealth를 직접 찾지 않는다.

---

## 4. Environment 진영 피해 규칙

기존 `CombatFactionRules`에는 환경 피해가:

```text
Environment → Player
Environment → Ally
Environment → Enemy
Environment → Neutral
```

대상을 공격할 수 있는 규칙이 이미 존재한다.

Day18은 이 규칙을 그대로 사용한다.

따라서:

```text
Player
부패한 망자
부패한 망자 궁수
상자 미믹
```

모두 함정의 공통 Damage Pipeline 대상이 될 수 있다.

---

## 5. 웃는 석상 함정 면역 유지

Day17 최종 웃는 석상은:

```text
CombatHealth 없음
IDamageable 없음
```

규칙을 가진다.

따라서 Day18 함정도 웃는 석상에게
별도의 예외 코드를 작성하지 않는다.

```text
TrapDamageSource
↓
DamagePipeline.FindDamageable()
↓
대상 없음
↓
피해 미적용
```

으로 자연스럽게 무시된다.

즉:

```text
압력판 작동 가능
함정 피해 불가능
사망 불가능
```

규칙을 유지한다.

---

## 6. 작동당 중복 피해 방지

가시나 도끼 Damage Trigger에
대상 Collider가 여러 프레임 겹쳐 있어도
매 프레임 피해가 들어가지 않도록 했다.

구조:

```text
새 Activation 시작
↓
Damaged Target 기록 초기화
↓
Target 최초 접촉
→ 피해 적용
→ Target ID 기록

같은 Activation에서 재접촉
→ 피해 차단
```

즉:

```text
한 Activation
+
한 Target
=
1회 피해
```

이다.

다음 함정 주기가 시작되면
같은 대상도 다시 피해를 받을 수 있다.

---

# 바닥 가시 함정

## 7. FloorSpikeTrap

첫 번째 함정:

```text
FloorSpikeTrap_01
```

동작:

```text
Ready
↓
Pressure Plate 작동
↓
Warning
↓
가시 상승
↓
Active Damage
↓
가시 하강
↓
Cooldown
↓
Ready
```

---

## 8. 바닥 가시 수치

기본 테스트값:

```text
Damage       35
Stagger      25
Knockback    0.3

Warning      0.18초
Active       0.32초
Cooldown     1.60초
```

---

## 9. 바닥 가시 모델링

프리미티브 기반 구성:

```text
StoneBase
IronFrame

MovingSpikes
├─ Spike × 9
├─ SpikeTip × 9
└─ DamageVolume
```

3×3 구조의 가시판으로 구성했다.

평상시에는 가시가 바닥 아래에 숨고,
작동 시 빠르게 위로 올라온다.

---

# 천장 주기 가시판

## 10. CeilingSpikeSlamTrap

두 번째 함정:

```text
CeilingSpikeSlamTrap_01
```

은 압력판 없이 일정 주기로 자동 작동한다.

전체 주기:

```text
Waiting
↓
Warning
↓
Slamming
↓
Active
↓
Returning
↓
Waiting
```

---

## 11. 천장 가시 주기

최종 테스트값:

```text
대기       2.50초
경고       0.55초
내려찍기   0.18초
바닥 유지  0.35초
복귀       0.75초
```

플레이어가 반복 패턴을 보고
통과 타이밍을 판단할 수 있도록 완전 랜덤으로 만들지 않았다.

---

## 12. 천장 가시 경고

내려찍기 전 Warning 단계에서는
이동 철판에 작은 흔들림을 적용한다.

개념:

```text
천장 대기
↓
철판 진동
↓
곧 내려옴
↓
Slam
```

향후 사운드·먼지 VFX를 추가하기 위한 기반이다.

---

## 13. 천장 가시 피해

기본값:

```text
Damage       70
Stagger      55
Knockback    1.0
```

바닥 가시보다 강한 함정으로 설정했다.

Validator에서도:

```text
Ceiling Spike Damage > Floor Spike Damage
```

를 검사한다.

---

## 14. 천장 가시 모델링

구성:

```text
CeilingFrame
GuideRail_L
GuideRail_R
Chain_L
Chain_R

MovingSpikePlate
├─ HeavyPlate
├─ Spike × 9
└─ DamageVolume
```

고정 프레임에서 무거운 철제 가시판이
수직으로 내려찍히는 형태다.

---

# Swing 도끼 함정

## 15. SwingingAxeTrap

세 번째 피해형 함정:

```text
SwingingAxeTrap_01
```

은 통로 앞 숨은 Trigger로 작동한다.

흐름:

```text
Player / Monster 통로 진입
↓
Hidden Trigger
↓
Warning
↓
Swing
↓
Damage Window
↓
Return
↓
Cooldown
```

---

## 16. 도끼 수치

기본값:

```text
Damage       55
Stagger      40
Knockback    2.0

Warning      0.28초
Cooldown     1.20초
```

가시와 비교해
강한 횡방향 넉백을 특징으로 둔다.

---

## 17. 도끼 모델링

프리미티브 기반 구성:

```text
Stone Support
Pivot
Wall Bracket
Long Handle
Axe Head
Blade Edge
Counter Weight
```

Pivot Transform의 회전을 이용해
통로를 크게 가로지르도록 만들었다.

---

# 압력판

## 18. PressurePlate

네 번째 요소:

```text
PressurePlate_01
```

은 피해를 직접 주는 함정이 아니라
연결된 함정을 작동시키는 Trigger 장치다.

현재 연결:

```text
PressurePlate_01
↓
FloorSpikeTrap_01
```

---

## 19. 압력판 작동 대상

압력판은 플레이어 전용으로 만들지 않았다.

`TrapActorUtility`를 통해:

```text
Player
IDamageable Monster
Smiling Statue
```

등 실제 행위자를 판별한다.

현재 테스트에서는 플레이어와 몬스터가 모두 압력판을 작동시킬 수 있다.

---

## 20. 압력판 시각 모션

밟으면 상판이 실제로 내려간다.

```text
Released Position
↓
Pressed Position
```

마지막 행위자가 벗어나면 원래 위치로 돌아온다.

여러 Collider가 동시에 들어와도
Occupant를 개별 추적해 잘못 해제되지 않도록 구성했다.

---

## 21. 다중 함정 연결 기반

현재는 바닥 가시 하나에 연결하지만
구조 자체는 배열을 사용한다.

```text
PressurePlate
↓
LinkedTraps[]
```

따라서 향후:

```text
압력판 하나
├─ 가시
├─ 도끼
├─ 철문
└─ 기타 장치
```

형태로 확장할 수 있다.

---

# 숨은 Trigger

## 22. TrapTriggerVolume

도끼 함정에는:

```text
TrapTriggerVolume
```

을 연결했다.

보이지 않는 Trigger Volume에
플레이어 또는 몬스터가 진입하면
연결된 함정을 작동시킨다.

현재:

```text
Hidden Trigger
↓
SwingingAxeTrap_01
```

구조다.

---

## 23. TrapActorUtility

압력판과 숨은 Trigger가
Collider 하나하나를 별도 개체로 오인하지 않도록:

```text
TrapActorUtility
```

를 추가했다.

플레이어와 일반 몬스터는
대표 DamageTransform의 Root를 행위자로 사용한다.

이를 통해 팔·몸·다리 Collider가 여러 개 있어도
하나의 Actor로 처리한다.

---

# 테스트 맵 구성

## 24. Day18 시험장

테스트 중심:

```text
(-27, 0, 18.2)
```

Day17 몬스터 Spawn Line 앞쪽의 기존 SprintLane 영역에 생성한다.

배치 개념:

```text
Day17 Monster Spawn

[망자] [궁수] [석상] [미믹]

          ↓

Day18 Trap Test

좌측
Pressure Plate
↓
Floor Spike

중앙
Hidden Trigger
↓
Swinging Axe

우측
Ceiling Spike Slam

          ↓
        Player
```

몬스터를 플레이어 쪽으로 유인하면서
함정 상호작용을 함께 시험할 수 있는 구조다.

---

## 25. 시험장 시각 구획

Day18 Setup은 다음도 생성한다.

```text
Trap_TestFloor
Trap_LaneDivider_L
Trap_LaneDivider_R
```

함정 종류별 시험 공간을 구분하기 위한 얇은 바닥과 구획선이다.

---

# F1 진단

## 26. TrapDebugPage

새 페이지:

```text
Assets/ProjectI/Scripts/Diagnostics/TrapDebugPage.cs
```

를 추가했다.

페이지 이름:

```text
Trap
```

주요 표시 정보:

```text
Active Trap Controllers
Pressure Plates

Trap Name
State
Activation Count
Last Trigger
Damage
Stagger
Knockback
Damage Window
Damaged Target Count

Pressure Plate
Pressed
Occupant Count
Linked Traps

Last Trap Hit
Target
Applied Damage
```

---

# 자동 구성

## 27. Phase4Day18Setup

새 파일:

```text
Assets/ProjectI/Editor/Phase4Day18Setup.cs
```

자동 처리:

```text
ExplorationOffice 열기
↓
기존 Day18 Root 정리
↓
Day18 재질 생성
↓
함정 시험장 생성
↓
Floor Spike 생성
↓
Pressure Plate 생성·연결
↓
Swing Axe 생성
↓
Hidden Trigger 연결
↓
Ceiling Spike 생성
↓
F1 Trap 페이지 추가
↓
Ready Marker
↓
씬 저장
↓
Validator 실행
```

완료 마커:

```text
===Day18 Trap System Ready===
```

---

## 28. Day18 생성 재질

자동 생성 경로:

```text
Assets/ProjectI/Art/Generated/Day18/
```

재질:

```text
Trap_Stone
Trap_DarkMetal
Trap_Blade
Trap_Rust
Trap_Warning
```

---

# Validator

## 29. Phase4Day18Validator

새 검증 파일:

```text
Assets/ProjectI/Editor/Phase4Day18Validator.cs
```

주요 검사:

```text
Day18 Root
Ready Marker

Floor Spike 존재
Ceiling Spike 존재
Swing Axe 존재
Pressure Plate 존재
Hidden Trigger 존재
Trap Debug Page 존재

Environment → Player 피해
Environment → Enemy 피해

Floor Spike Damage >= 35
Ceiling Spike Damage >= 70
Ceiling > Floor Damage
Axe Damage >= 55

Pressure Plate → Floor Spike 연결
Hidden Trigger → Swing Axe 연결

웃는 석상 IDamageable 없음
```

---

# 주요 신규 파일

## 30. Traps 코드

```text
Assets/ProjectI/Scripts/Traps/
├─ TrapState.cs
├─ TrapActorUtility.cs
├─ TrapControllerBase.cs
├─ TrapDamageSource.cs
├─ FloorSpikeTrap.cs
├─ CeilingSpikeSlamTrap.cs
├─ SwingingAxeTrap.cs
├─ TrapTriggerVolume.cs
└─ PressurePlate.cs
```

---

## 31. Diagnostics / Editor

```text
Assets/ProjectI/Scripts/Diagnostics/
└─ TrapDebugPage.cs

Assets/ProjectI/Editor/
├─ Phase4Day18Setup.cs
└─ Phase4Day18Validator.cs
```

신규 파일과 `Traps` 폴더의 `.meta`도 함께 추가됐다.

---

# Day18 테스트 흐름

## 32. 바닥 가시

```text
1. Play Mode
2. Pressure Plate 접근
3. Plate가 아래로 내려가는지 확인
4. Floor Spike가 Warning 후 상승하는지 확인
5. Player 피해 확인
6. Cooldown 후 재사용 확인
```

---

## 33. 몬스터와 바닥 가시

```text
1. 부패한 망자를 Player 쪽으로 유인
2. Monster가 Pressure Plate 진입
3. Floor Spike 작동
4. Monster HP 감소 확인
5. 같은 Activation에서 중복 피해가 없는지 확인
```

---

## 34. 도끼

```text
1. 중앙 통로 진입
2. Hidden Trigger 작동
3. 0.28초 Warning
4. Axe Swing
5. Player 또는 Monster 피해 확인
6. 복귀 및 재사용 확인
```

---

## 35. 천장 가시

```text
1. 우측 함정 통로 대기
2. 2.5초 Waiting 확인
3. 0.55초 Warning 진동 확인
4. 0.18초 빠른 Slam 확인
5. 0.35초 바닥 유지 확인
6. 0.75초 복귀 확인
7. 주기가 다시 반복되는지 확인
```

---

## 36. 웃는 석상

```text
1. 웃는 석상을 함정 방향으로 이동시킴
2. Pressure Plate가 작동 가능한지 확인
3. Floor / Ceiling / Axe 피해가 적용되지 않는지 확인
4. 불사·비피격 규칙 유지 확인
```

---

## 37. F1 Trap

```text
1. F1 진단창 열기
2. Trap 페이지 이동
3. 각 함정 State 확인
4. Activation Count 확인
5. Pressure Plate Occupants 확인
6. Last Trap Damage 확인
```

---

# Day18 완료 결과

최종 함정 구조:

```text
Player / Monster
↓
Pressure Plate / Hidden Trigger / Auto Cycle
↓
Trap Controller
↓
TrapDamageSource
↓
DamageInfo

Faction = Environment
Type = Trap

↓
DamagePipeline
↓
Player / Monster HP
+
Stagger
+
Knockback
```

구현된 함정 역할:

```text
Floor Spike
→ 압력판 연동 기본 가시

Ceiling Spike Slam
→ 일정 주기를 보고 통과하는 고위험 가시판

Swinging Axe
→ 통로 Trigger 기반 강한 횡방향 공격

Pressure Plate
→ Player/Monster가 다른 함정을 작동시키는 연결 장치
```

---

## 다음 개발 방향

### Day 19 — 플레이어·몬스터·함정 통합 및 실제 원정 시험

Day 19에서는 새 피해 체계를 추가하기보다
Day14~18까지 만든 전투 구성요소를 실제 플레이 흐름 안에서 함께 시험한다.

예정 방향:

```text
플레이어 근접·원거리 전투
+
4종 몬스터 AI
+
바닥/천장 가시
+
도끼
+
압력판
↓
통합 전투 공간

몬스터 유인
함정 역이용
벽/문/통로 상황
사망·경직·넉백
Spawn 및 Reset
F1 통합 진단
```

공통 Damage Pipeline을 유지하면서
Phase 4 전투·몬스터·함정의 실제 원정 수준 통합을 목표로 한다.
