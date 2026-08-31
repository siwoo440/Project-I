# Project I 개발 일지

## Day 15 — 검·도끼 단발 근접 공격 및 쿨타임·경직·넉백 시스템 구현

- 날짜: 2026-08-31
- 개발 단계: Phase 4 — 전투·몬스터·함정
- 개발 내용 기준 커밋(amend 전): `9e568e26ff7461ba307e85483b5e41c00d4dae81`
- 현재 커밋 메시지: `15`
- 이전 커밋: `a459a27a63b96cf7616564c6378c90fd6fe38e57`
- 기준 씬: `Assets/ProjectI/Scenes/ExplorationOffice.unity`

---

## 개발 목표

Day 14에서 구축한 공통 Damage Pipeline과 근접 무기 궤적 기반 위에
실제 플레이에 사용할 검·도끼 단발 근접 공격을 구현했다.

이번 일차에서는 콤보와 맨손 공격을 추가하지 않고
각 무기가 좌클릭 한 번에 한 번만 휘둘러진 뒤 무기별 쿨타임을 거치는 구조로 확정했다.

핵심 목표:

- 검·도끼 정식 테스트 월드 아이템 구현
- 검·도끼를 테스트 맵에 직접 배치
- 프리미티브 기반 검·도끼 상세 모델링
- 무기별 한 번 휘두르는 단발 공격
- 검·도끼별 공격 쿨타임
- 기존 PlayerStamina와 공격 비용 연결
- 공격 중 이동 제한 유지
- StaggerPower 실제 Damage Pipeline 연결
- 경직 누적과 Threshold 반응
- 넉백과 넉백 저항 구현
- 벽 충돌을 고려한 넉백 이동
- 일반 더미와 중장 더미 비교 시험
- F1 Combat 페이지 확장
- Day 14 / Day 15 테스트 공간 위치 조정
- 1인칭 무기 들기 자세와 날 방향 조정
- 검·도끼 베기 방향 최종 보정
- Day 15 Setup / Validator 구성

---

## 1. 단발 공격 구조 확정

Day 15의 근접 공격은 콤보 방식이 아니다.

```text
좌클릭
↓
스태미나 확인
↓
Windup
↓
Active
↓
Recovery
↓
Cooldown
↓
다음 공격 가능
```

공격 도중 좌클릭을 다시 눌러도 다음 공격을 저장하거나 예약하지 않는다.

제외한 기능:

```text
맨손 공격
검 추가타
도끼 추가타
Combo Window
Input Buffer
Queued Attack
```

따라서 모든 근접 무기는 한 번의 입력에 한 번만 휘두른다.

---

## 2. CombatState에 Cooldown 추가

Day 14의 전투 상태를 확장해
공격 종료 후 다음 공격이 가능한 시점까지 별도의 Cooldown 상태를 사용한다.

```text
Idle
↓
Attacking
↓
Cooldown
↓
Idle
```

내부 공격 단계는 기존 구조를 유지한다.

```text
Windup
Active
Recovery
```

쿨타임은 공격 동작이 끝난 뒤부터 전체 시간을 다시 세는 방식이 아니라
공격 시작 시점부터 다음 공격이 가능한 최소 간격으로 계산한다.

---

## 3. 검 단발 공격

정식 테스트 검:

```text
Day15_IronSword
```

공격 데이터:

```text
Display Name       Iron Sword Slash
Damage             25
Stamina Cost       10
Windup             0.12 s
Active             0.16 s
Recovery           0.20 s
Cooldown           0.65 s
Movement Modifier  0.65
Trace Radius       0.11
Stagger Power      10
Knockback          1.4
```

공격은 빠른 단발 베기로 구성했다.

```text
기본 자세
↓
뒤로 당김
↓
한 번 베기
↓
기본 자세 복귀
↓
남은 쿨타임 대기
```

최종 공격 방향은 준비 자세와 타격 자세의 좌우 회전이 실제 화면 방향과 맞도록 보정했다.

```text
Windup Euler  (-14, 6, -26)
Strike Euler  (10, -10, 44)
```

---

## 4. 도끼 단발 공격

정식 테스트 도끼:

```text
Day15_IronAxe
```

공격 데이터:

```text
Display Name       Iron Axe Heavy Swing
Damage             45
Stamina Cost       20
Windup             0.28 s
Active             0.20 s
Recovery           0.42 s
Cooldown           1.15 s
Movement Modifier  0.40
Trace Radius       0.16
Stagger Power      35
Knockback          3.5
```

도끼는 검보다 느리고 공격 중 이동이 크게 제한되지만
높은 피해량·경직·넉백을 가지도록 구성했다.

최종 휘두르기 방향:

```text
Windup Euler  (-20, 8, -34)
Strike Euler  (18, -12, 56)
```

검과 동일하게 한 번 뒤로 당긴 뒤 한 번만 앞으로 휘두른다.

---

## 5. AttackDefinition 확장

기존 `AttackDefinition`에 단발 공격을 위한 데이터를 추가했다.

주요 확장 항목:

```text
CooldownDuration
WindupEuler
StrikeEuler
```

공격 동작 시간:

```text
TotalAttackDuration
=
Windup
+
Active
+
Recovery
```

쿨타임은 최소 공격 간격으로 별도 관리한다.

---

## 6. 기존 스태미나와 공격 비용 공유

전투용 스태미나를 따로 만들지 않았다.

기존:

```text
PlayerStamina
StaminaState
```

를 그대로 사용한다.

검:

```text
10 소비
```

도끼:

```text
20 소비
```

현재 스태미나가 공격 비용보다 부족하면 공격을 시작하지 않는다.

달리기와 공격이 동일한 스태미나 자원을 공유한다.

---

## 7. 공격 중 이동 제한

기존 `PlayerMovement`의 외부 이동 배율을 사용한다.

검:

```text
Movement Modifier = 0.65
```

도끼:

```text
Movement Modifier = 0.40
```

공격 중에는 달리기도 제한되며
공격과 쿨타임 처리 종료 후 정상 이동 상태로 복구된다.

---

## 8. StaggerPower를 Damage Pipeline에 연결

Day 14에서는 `StaggerPower`가 공격 데이터에만 준비되어 있었다.

Day 15부터 실제 피해 데이터로 전달한다.

```text
AttackDefinition
↓
DamageInfo.StaggerPower
↓
DamagePipeline
↓
ICombatReactionReceiver
↓
CombatReaction
```

체력 피해가 실제 승인된 경우에만 피격 반응도 처리한다.

Friendly Fire나 기타 규칙으로 피해가 거부되면
경직과 넉백도 적용되지 않는다.

---

## 9. ICombatReactionReceiver 추가

공통 체력 처리와 피격 반응 처리를 분리했다.

```text
IDamageable
→ 체력 피해

ICombatReactionReceiver
→ 경직
→ 넉백
```

이를 통해 향후 몬스터마다:

```text
경직 저항
넉백 저항
경직 면역
넉백 면역
```

등을 별도로 설정할 수 있는 기반을 마련했다.

---

## 10. CombatReaction 구현

새 파일:

```text
Assets/ProjectI/Scripts/Combat/CombatReaction.cs
```

주요 기능:

```text
Stagger 누적
Stagger Threshold
경직 유지 시간
Stagger 감소
Knockback Resistance
남은 Knockback 이동
벽 충돌 검사
간단한 피격 흔들림
```

공격이 들어오면 경직 수치를 누적하고
Threshold에 도달할 경우 짧은 경직 반응을 발생시킨다.

---

## 11. 일반 더미 경직 설정

기존 Day 14 일반 적 더미에도 `CombatReaction`을 추가했다.

대표 일반 적 더미:

```text
Stagger Threshold = 30
Knockback Resistance = 낮음
```

검:

```text
10 + 10 + 10
→ Threshold 30
→ 경직 발생
```

도끼:

```text
Stagger Power 35
→ 한 번에 일반 더미 경직 가능
```

검과 도끼의 역할 차이를 바로 비교할 수 있다.

---

## 12. 중장 더미 추가

새로운 테스트 대상:

```text
CombatDummy_Heavy
```

기본 설정:

```text
HP                  180
Faction             Enemy
Stagger Threshold   80
Knockback Resistance 65%
```

프리미티브 모델링으로 일반 더미보다 무거운 외형을 구성했다.

```text
Pedestal
Body
ChestArmor
Head
Helmet
Shoulder_L
Shoulder_R
Belt
```

도끼의 높은 경직·넉백과
검의 빠른 반복 공격 차이를 비교하는 용도다.

---

## 13. 넉백 구현

공격자에서 피격 위치 방향을 계산해 넉백 힘을 전달한다.

```text
Attacker
↓
Hit Target Direction
↓
DamageInfo.Force
↓
CombatReaction
↓
Knockback
```

검:

```text
1.4
```

도끼:

```text
3.5
```

중장 더미는 높은 Knockback Resistance로 실제 이동량을 줄인다.

---

## 14. 넉백 벽 충돌

넉백 중 대상이 벽을 그대로 통과하지 않도록
진행 방향에 대한 물리 검사를 추가했다.

```text
Target
↓
Knockback Direction
↓
SphereCast
↓
벽 감지
↓
이동 가능 거리까지만 이동
```

향후 몬스터 NavMesh 이동과 연결하기 전
공통 피격 반응 단계에서 사용할 수 있는 기본 충돌 방지 구조다.

---

## 15. 검 상세 모델링

`Day15_IronSword`는 단순 막대 형태에서 벗어나
여러 Primitive Part를 조합해 구성했다.

```text
Pommel_Core
Pommel_BrassRing
Grip_Core
Grip_Wrap × 6
Guard_Center
Guard_Left
Guard_Right
Blade_Collar
Blade_Core
Blade_Edge_L
Blade_Edge_R
Blade_Fuller
Blade_Tip
TraceStart
TraceEnd
```

재질도 부품 성격에 따라 나눴다.

```text
Sword Steel
Dark Steel
Leather
Brass
```

---

## 16. 도끼 상세 모델링

`Day15_IronAxe`도 여러 부품을 조합해 모델링했다.

```text
Handle_Core
Handle_Butt
Handle_Wrap × 5
Head_Socket
Head_Core
Axe_Cheek
Axe_Edge
Head_Poll
Head_TopBand
TraceStart
TraceEnd
```

재질:

```text
Axe Steel
Dark Steel
Wood
Leather
```

검과 도끼 모두 기존 `MeleeWeaponTrace`의
실제 날 기준점을 모델 안에 배치했다.

---

## 17. 1인칭 무기 들기 자세 조정

월드에서 획득한 검과 도끼가
기존처럼 수평으로 길게 튀어나오지 않도록 Carry Pose를 조정했다.

검:

```text
Position  (0.24, -0.24, 0.19)
Rotation  (14, 72, 2)
```

도끼:

```text
Position  (0.27, -0.25, 0.18)
Rotation  (12, 74, -2)
```

화면 오른쪽에서 무기를 세워 들고
검날과 도끼날 면이 전방을 향하도록 보정했다.

---

## 18. 검·도끼 베기 방향 보정

첫 단순 휘두르기 보정 이후
뒤로 당겼다가 앞으로 베는 실제 회전 방향이 반대로 보이는 문제가 있었다.

최종적으로 Windup과 Strike의 좌우 회전 부호를 반전해
사용자가 보는 방향과 실제 베기 방향을 맞췄다.

최종 자동 Setup 마커:

```text
===Day15 Single Melee Combat Ready v5===
```

이전 v4 이하 마커를 제거하고
최종 공격 방향으로 다시 구성되도록 했다.

---

## 19. 무기 테스트 맵 배치

검·도끼 전시 공간을
기존 남동쪽 임시 전투 공간에서 Day 3 테스트 맵의 파란 `01_SprintLane` 영역으로 이동했다.

Day 15 기준 중심:

```text
(-27, 0, 2)
```

구성:

```text
Sword Display Stand
Day15_IronSword

Axe Display Stand
Day15_IronAxe

Weapon Display Back Rail
Weapon Display Accent

CombatDummy_Heavy
```

검과 도끼는 직접 F로 획득할 수 있는 `WorldItem`이다.

---

## 20. Day 14 Combat Foundation 위치 조정

Day 15 무기 시험 공간과 함께 사용할 수 있도록
기존:

```text
===Day14 Combat Foundation===
```

시험장도 위치를 조정했다.

새 중심:

```text
(-27, 0, 12)
```

즉 `01_SprintLane` 북쪽 개방 공간에
Day 14 피해·진영·벽 차단 더미 시험장이 배치된다.

Day 14 자동 구성 마커도 위치 변경을 반영해:

```text
===Day14 Combat Foundation Ready v2===
```

로 갱신했다.

---

## 21. 기존 Day 14 임시 검 정리

Day 15 정식 검·도끼를 추가하면서
씬에 남아 있던:

```text
Day14_CombatTestSword
```

는 자동 Setup에서 제거한다.

Day 14 공격 데이터 자체는
이전 일차 재현과 구조 호환을 위해 유지한다.

---

## 22. F1 Combat 진단 확장

기존 F1 `Combat` 페이지를 Day 15 상태에 맞게 확장했다.

주요 표시 정보:

```text
Combat State
Attack Phase
Can Attack
Cooldown Remaining
Cooldown Progress

Weapon
Damage
Damage Type
Stamina Cost
Stagger Power
Knockback

Last Damage
Last Stagger
Last Force

Combat Target HP
Stagger / Threshold
Stagger Active
Knockback Resistance
Remaining Knockback
```

이를 통해 공격 입력부터 피해·경직·넉백까지
한 페이지에서 확인할 수 있도록 했다.

---

## 23. Day 15 자동 Setup

새 파일:

```text
Assets/ProjectI/Editor/Phase4Day15Setup.cs
```

주요 자동 처리:

```text
Day15 공격 에셋 생성·갱신
Day15 재질 생성
Day14 임시 검 제거
검 전시대 생성
도끼 전시대 생성
검 상세 모델 생성
도끼 상세 모델 생성
일반 더미 CombatReaction 연결
Heavy Dummy 생성
F1 Combat 재연결
씬 저장
완료 마커 생성
```

최종 완료 마커:

```text
===Day15 Single Melee Combat Ready v5===
```

---

## 24. Day 15 Validator

새 파일:

```text
Assets/ProjectI/Editor/Phase4Day15Validator.cs
```

검증 대상:

```text
Day15 완료 마커
Day15 시험장 루트
CombatController
CombatDebugPage
Day15_IronSword
Day15_IronAxe
SprintLane 무기 배치 위치
Day14 임시 테스트 검 제거
WorldItem 연결
MeleeWeaponTrace 연결
검·도끼 상세 모델 파트 수
Sword AttackDefinition
Axe AttackDefinition
검 피해량과 쿨타임
도끼 피해량과 쿨타임
검·도끼 스태미나 차이
검·도끼 경직 차이
검·도끼 넉백 차이
공격 중 이동 제한 차이
CombatReaction 대상 수
일반 더미 Threshold
중장 더미 Threshold
중장 더미 Knockback Resistance
기존 Player → Enemy 피해 규칙
```

Day 14 Validator도
이동된 Combat Foundation 위치를 확인하도록 갱신했다.

---

## 25. 주요 생성 파일

```text
Assets/ProjectI/Scripts/Combat/
├─ CombatReaction.cs
└─ ICombatReactionReceiver.cs

Assets/ProjectI/Editor/
├─ Phase4Day15Setup.cs
└─ Phase4Day15Validator.cs

Assets/ProjectI/Resources/Combat/
├─ Day15_SwordSlash.asset
└─ Day15_AxeSwing.asset

Assets/ProjectI/Art/Generated/Day15/
├─ Melee_SwordSteel.mat
├─ Melee_AxeSteel.mat
├─ Melee_DarkSteel.mat
├─ Melee_Leather.mat
├─ Melee_Wood.mat
├─ Melee_Brass.mat
├─ Melee_Stand.mat
├─ Melee_Accent.mat
├─ Melee_HeavyBody.mat
└─ Melee_HeavyArmor.mat
```

각 신규 에셋에 필요한 `.meta`도 함께 생성됐다.

---

## 26. 주요 수정 파일

```text
Assets/ProjectI/Scripts/Combat/
├─ AttackDefinition.cs
├─ CombatController.cs
├─ CombatState.cs
├─ DamageInfo.cs
├─ DamagePipeline.cs
├─ DamageSource.cs
└─ MeleeWeaponItem.cs

Assets/ProjectI/Scripts/Diagnostics/
└─ CombatDebugPage.cs

Assets/ProjectI/Editor/
├─ Phase4Day14Setup.cs
└─ Phase4Day14Validator.cs

Assets/ProjectI/Resources/Combat/
└─ Day14_TestSword.asset

Assets/ProjectI/Scenes/
└─ ExplorationOffice.unity
```

---

## 27. Day 15 테스트 흐름

```text
1. Play Mode 진입
2. 파란 SprintLane의 검·도끼 전시 공간 이동
3. 검 F 획득
4. 좌클릭 1회
5. 검이 한 번만 휘둘러지는지 확인
6. 0.65초 이전 재공격이 차단되는지 확인
7. 일반 더미 경직 누적 확인
8. 도끼 F 획득
9. 좌클릭 1회
10. 도끼가 한 번만 휘둘러지는지 확인
11. 1.15초 이전 재공격이 차단되는지 확인
12. 검보다 높은 피해·경직·넉백 확인
13. Heavy Dummy에서 저항 차이 확인
14. F1 Combat 페이지 확인
15. Day15 Validator 실행
```

---

## 28. Day 15 완료 결과

최종 근접 전투 구조:

```text
검 / 도끼 획득
↓
빠른 슬롯 선택
↓
좌클릭
↓
스태미나 소비
↓
Windup
↓
단일 Swing
↓
Melee Weapon Trace
↓
Damage Pipeline
↓
HP Damage
+
Stagger
+
Knockback
↓
Recovery
↓
Cooldown
↓
다음 공격 가능
```

검:

```text
빠른 공격
낮은 스태미나
짧은 쿨타임
보통 피해
보통 경직
약한 넉백
```

도끼:

```text
느린 공격
높은 스태미나
긴 쿨타임
높은 피해
강한 경직
강한 넉백
```

조작 방식은 동일하지만
공격 성능과 무게감이 확실히 다르게 동작하도록 기반을 구성했다.

---

## 다음 개발 방향

### Day 16 — 원거리 전투 완성

Day 14의 공통 Damage Pipeline을 유지하면서
다음 단계에서는 활과 화살을 연결한다.

예정 범위:

```text
활 조준
활 충전
활 발사
화살 Projectile
거리·충전 기반 피해
화살 충돌
부위별 명중 판정
화살 회수
Damage Pipeline 통합
F1 원거리 전투 진단
```

검·도끼와 마찬가지로
활도 피해 대상의 체력을 직접 수정하지 않고
공통 Damage Pipeline을 사용하도록 구성한다.
