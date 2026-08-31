# Project I 개발 일지

## Day 16 — 석궁 포물선 사격·확대 조준 및 리볼버 6발·탄퍼짐·장전 시스템 구현

- 날짜: 2026-08-31
- 개발 단계: Phase 4 — 전투·몬스터·함정
- 개발 내용 기준 커밋(amend 전): `eb0d365d380e22c664a0a2557bdaece21cb387fa`
- 현재 커밋 메시지: `a`
- 이전 커밋: `a9e36bcd41ef3d6d4beeae1cb9b125d0771545a6`
- 기준 씬: `Assets/ProjectI/Scenes/ExplorationOffice.unity`

---

## 개발 목표

Day 14~15에서 구축한 공통 Damage Pipeline과 빠른 슬롯 무기 사용 구조를 유지하면서
플레이어가 사용할 수 있는 원거리 무기 2종을 추가했다.

이번 일차에서는 기존 계획의 활 대신 석궁을 채택하고,
추가 원거리 무기로 6발 리볼버를 구현했다.

핵심 목표:

- 석궁과 리볼버를 실제 WorldItem으로 테스트 맵에 배치
- 기존 빠른 슬롯과 좌클릭 Use 구조 재사용
- 우클릭 조준과 R 재장전 입력 추가
- 석궁 조준 시 화면 중앙 이동과 강한 FOV 확대
- 석궁 볼트 Rigidbody 기반 포물선 비행
- 석궁 볼트 Damage Pipeline 연결
- 석궁 볼트 F 회수와 예비 볼트 복구
- 석궁 재장전 중 시위와 무기 움직임 표현
- 리볼버 6발 실린더 구조
- 리볼버 빠른 연사 시 누적 탄퍼짐
- 리볼버 조준 시 탄퍼짐 감소
- 리볼버 실린더 장전 모션
- 리볼버 탄환 비회수 Hitscan 구조
- 석궁·리볼버 상세 프리미티브 모델링
- 리볼버 모델 크기 축소 및 실린더·탄약 방향 보정
- 석궁 볼트 탄속 38 m/s → 95 m/s 상향
- Day16 원거리 시험 표적 구성
- F1 Ranged Combat 진단 페이지 추가
- Day16 Setup / Validator 구성

---

## 1. 원거리 전투 구조 추가

새 원거리 전투 코드는 다음 폴더를 중심으로 구성했다.

```text
Assets/ProjectI/Scripts/Combat/Ranged/
```

주요 구성:

```text
RangedWeaponItemBase
CrossbowWeaponItem
CrossbowBoltProjectile
RevolverWeaponItem
```

석궁과 리볼버는 각각 다른 발사 방식을 사용하지만
피해 적용 단계는 기존 공통 시스템으로 통일했다.

```text
석궁 볼트 / 리볼버 탄도
↓
DamageInfo
↓
DamagePipeline
↓
IDamageable
↓
CombatReaction
```

---

## 2. 기존 빠른 슬롯·WorldItem 구조 재사용

원거리 무기 전용 인벤토리를 새로 만들지 않았다.

기존:

```text
WorldItem
PlayerInventory
PlayerCarryController
IUsableItem
```

구조를 그대로 사용한다.

사용 흐름:

```text
F
↓
월드 무기 획득
↓
빠른 슬롯 저장
↓
선택 슬롯 장착
↓
좌클릭 Use
↓
원거리 무기 발사
```

석궁은 실제 외형상 양손 무기지만
현재 빠른 슬롯의 TwoHand 선택 잠금 규칙과 충돌하지 않도록
WorldItem 운반 규칙은 OneHand를 사용하고 Carry Pose로 화면 위치를 보정했다.

---

## 3. Aim / Reload 입력 확장

기존 `PlayerInputReader`와 `GameplayInputActions`에
원거리 무기용 조준과 재장전 입력을 추가했다.

기본 조작:

```text
RMB Hold
→ Aim

LMB
→ Fire

R
→ Reload

F
→ Pickup / Crossbow Bolt Recovery
```

입력 액션 에셋에 Aim / Reload가 존재하면 해당 InputAction을 사용하고,
누락된 경우 현재 테스트 환경에서 바로 사용할 수 있도록
마우스 우클릭과 Keyboard R을 fallback 입력으로 사용한다.

---

## 4. 원거리 무기 공통 기반

`RangedWeaponItemBase`에서 석궁과 리볼버가 공통으로 사용하는 기능을 처리한다.

공통 기능:

```text
Aim 입력
조준 상태
Camera FOV 변경
무기 VisualPivot 이동
무기 VisualPivot 회전
조준 중 이동 배율
조준 중 Sprint 제한
Reload 입력
Reload 진행률
Reload 중 무기 포즈
```

이를 통해 석궁과 리볼버가 서로 다른 발사·장전 규칙을 가지면서도
조준과 플레이어 이동 제약 구조는 공통으로 유지한다.

---

## 5. 석궁 WorldItem 구현

테스트 맵에 다음 무기를 배치했다.

```text
Day16_Crossbow
```

월드에서 F로 획득할 수 있고
기존 빠른 슬롯에서 선택해 사용할 수 있다.

석궁은 빠른 연사 무기가 아니라:

```text
장전
↓
조준
↓
1발 발사
↓
빈 상태
↓
R 재장전
```

방식의 강한 단발 원거리 무기로 구성했다.

---

## 6. 석궁 상세 모델링

프리미티브를 조합해 다음 파트를 구성했다.

```text
Stock_Main
Stock_Butt
Rail
TriggerHousing
Grip
Trigger
BowCenter
Limb_Left
Limb_Right
LimbCap_Left
LimbCap_Right
StringRoot
String_Left
String_Right
Stirrup
ScopeBody
ScopeFront
ScopeRear
LoadedBoltVisual
Muzzle
```

주요 재질:

```text
Dark Steel
Bright Steel
Wood
Leather
Brass
String
```

몸체, 활대, 시위, 레일, 발판, 조준경, 장전 볼트가
하나의 석궁 형태로 보이도록 구성했다.

---

## 7. 석궁 확대 조준

석궁을 들고 우클릭을 유지하면
무기가 화면 중앙 쪽으로 이동하고 Camera FOV가 감소한다.

설정:

```text
Aim FOV = 40
```

따라서 리볼버보다 훨씬 강한 확대 조준을 사용한다.

조준 상태에서는:

```text
무기 중앙 정렬
FOV 축소
이동 속도 감소
Sprint 제한
```

이 동시에 적용된다.

우클릭을 놓으면 무기 위치와 FOV가 원래 상태로 복구된다.

---

## 8. 석궁 볼트 포물선 비행

석궁은 리볼버와 달리 실제 Projectile을 생성한다.

```text
CrossbowBoltProjectile
```

발사 흐름:

```text
좌클릭
↓
Muzzle에서 볼트 복제
↓
Rigidbody 속도 부여
↓
useGravity = true
↓
중력 영향
↓
포물선 비행
```

비행 중 볼트의 방향은 현재 이동 속도 방향을 따라가도록 구성했다.

따라서 화살처럼 장거리에서 아래로 떨어지는 탄도를 가진다.

---

## 9. 석궁 볼트 탄속 상향

초기 Day16 구현에서는 볼트 발사 속도를:

```text
38 m/s
```

로 구성했다.

테스트 후 2.5배 상향해 최종 Setup 값은:

```text
95 m/s
```

로 변경했다.

```text
38 × 2.5 = 95
```

중력 적용 자체는 유지하므로
포물선 탄도는 남아 있지만 기존보다 훨씬 빠르고 완만한 궤적으로 날아간다.

Day16 Validator에서도 최종 속도가 약 95 m/s 이상인지 검사한다.

---

## 10. 석궁 피해 규칙

석궁 기본 설정:

```text
Damage          55
Damage Type     Piercing
Stagger Power   28
Knockback       1.0
Reload Time     1.45 s
Reserve Bolts   12
Starts Loaded   YES
```

발사된 볼트가 공통 피해 대상을 명중하면:

```text
CrossbowBoltProjectile
↓
DamageInfo
↓
DamagePipeline.TryApply()
↓
HP Damage
↓
CombatReaction
```

순서로 처리된다.

---

## 11. 석궁 볼트 충돌·박힘

볼트는 물리 Projectile이므로
벽, 바닥, 전투 대상 등과 실제로 충돌한다.

충돌 후에는 비행을 끝내고
피격 위치에 박힌 상태로 전환된다.

볼트는 리볼버 탄환과 달리
재사용 가능한 탄약으로 취급한다.

---

## 12. 석궁 볼트 회수

박힌 볼트에 접근하면 상호작용을 사용할 수 있다.

```text
[F] 석궁 볼트 회수
```

회수 시:

```text
CrossbowWeaponItem.AddReserveBolts(1)
↓
Reserve Bolts +1
↓
월드 볼트 제거
```

가 실행된다.

따라서 실제로 발사한 석궁 볼트를 다시 회수해
예비 탄약으로 사용할 수 있다.

---

## 13. 석궁 재장전

석궁은 한 발을 발사하면:

```text
Loaded = false
```

가 된다.

이 상태에서 예비 볼트가 있으면 R로 재장전을 시작한다.

장전 시간:

```text
1.45 s
```

장전 모션:

```text
석궁을 아래로 이동
↓
무기를 기울임
↓
StringRoot 뒤로 이동
↓
시위 당김 표현
↓
예비 볼트 1발 소비
↓
LoadedBoltVisual 표시
↓
기본 자세 복구
```

장전이 완료되기 전에는 다시 발사할 수 없다.

---

## 14. 리볼버 WorldItem 구현

두 번째 원거리 무기로:

```text
Day16_Revolver
```

를 추가했다.

리볼버는 석궁과 반대로:

```text
즉각적인 발사
6발 실린더
빠른 재사격
연사 탄퍼짐
```

을 중심으로 구성했다.

---

## 15. 리볼버 상세 모델링

프리미티브를 조합해 다음 파트를 구성했다.

```text
Frame
Barrel
UnderLug
EjectorRod
CylinderRoot
Cylinder
Round_1 ~ Round_6
GripCore
GripBackstrap
Hammer
Trigger
TriggerGuard_Left
TriggerGuard_Right
FrontSight
RearSight
Muzzle
MuzzleFlash
```

재질:

```text
Dark Steel
Bright Steel
Wood
Brass
```

실린더 안에는 황동 탄약 6발을 별도 시각 요소로 구성했다.

---

## 16. 리볼버 크기 보정

초기 모델 테스트 후
월드와 1인칭 화면에서 리볼버가 다소 크게 보이는 문제를 수정했다.

최종 Day16 Setup에서는:

```text
VisualPivot Scale = 0.82
```

를 적용해 전체 시각 모델 크기를 줄였다.

동시에:

```text
WorldItem Carry Radius
Carry Position
BoxCollider
```

도 축소된 모델에 맞게 보정했다.

---

## 17. 리볼버 실린더 방향 보정

초기 리볼버 모델에서 실린더와 내부 탄약이
총열 방향과 맞지 않는 형태로 배치되는 문제가 있었다.

이를 수정해 실린더 축을 총열 방향과 맞췄다.

최종 실린더:

```text
Cylinder
Rotation = (90, 0, 0)
```

6발 탄약도 동일한 총열 방향을 따라가도록 배치했다.

약실 배치는 실린더 정면 기준 원형으로 계산한다.

```text
Round_1
Round_2
Round_3
Round_4
Round_5
Round_6
```

---

## 18. 리볼버 6발 실린더

기본 장탄:

```text
Cylinder Capacity = 6
Loaded Rounds      = 6
Reserve Rounds     = 24
```

발사할 때마다:

```text
6 → 5 → 4 → 3 → 2 → 1 → 0
```

으로 감소한다.

남은 탄약 수와 실린더 안 황동 탄약 시각 요소도 함께 동기화한다.

발사할 때 실린더는 다음 약실 위치로 1칸씩 회전한다.

---

## 19. 리볼버 Hitscan 발사

리볼버 탄환은 석궁 볼트와 달리
월드 Projectile GameObject를 만들지 않는다.

발사:

```text
Aim Camera 방향
↓
탄퍼짐 적용
↓
Physics.Raycast
↓
첫 충돌
↓
DamageInfo
↓
DamagePipeline
```

방식으로 처리한다.

기본 설정:

```text
Damage          28
Damage Type     Piercing
Stagger Power   8
Knockback       0.55
Range           75 m
Fire Interval   0.16 s
```

총알 Projectile이나 WorldItem을 생성하지 않기 때문에
리볼버 탄환은 회수할 수 없다.

---

## 20. 리볼버 탄퍼짐

리볼버는 발사를 빠르게 반복할수록 탄퍼짐이 증가한다.

설정:

```text
Hip Base Spread        0.85°
Aimed Base Spread      0.18°
Rapid Shot Spread      +0.75°
Max Additional Spread  4.5°
Spread Recovery        3.2°/s
Rapid Fire Window      0.48 s
```

동작:

```text
천천히 발사
→ 낮은 Spread

빠르게 연사
→ Additional Spread 누적

발사를 멈춤
→ Spread 점진 회복
```

우클릭 조준 중에는 누적 탄퍼짐의 영향도 줄어들도록 구성했다.

---

## 21. 리볼버 조준

리볼버도 우클릭 조준을 사용한다.

설정:

```text
Aim FOV = 55
```

석궁:

```text
FOV 40
```

보다 확대가 약하도록 구성해
두 무기의 장거리 역할 차이를 유지했다.

우클릭 조준 시 총이 화면 중앙으로 이동하고
FrontSight / RearSight 방향으로 정렬되는 기반을 사용한다.

---

## 22. 리볼버 재장전

실린더가 가득 차 있지 않고
예비 탄약이 있을 때 R로 재장전할 수 있다.

장전 시간:

```text
1.65 s
```

장전 흐름:

```text
총기 옆으로 회전
↓
CylinderRoot 왼쪽 이동
↓
실린더 오픈 표현
↓
실린더 회전
↓
부족한 탄약 보충
↓
실린더 기본 위치 복귀
↓
총기 원위치
```

실제로 비어 있는 약실 수만큼만 Reserve Rounds에서 보충한다.

---

## 23. 총구 화염 표현

리볼버 발사 시:

```text
MuzzleFlash
```

오브젝트를 짧게 활성화한다.

표시 시간은 약:

```text
0.045 s
```

이며 발사 후 자동으로 다시 비활성화된다.

---

## 24. Day16 원거리 시험장

기존 Day3의 파란 SprintLane 남쪽 영역을 이용했다.

시험장 루트:

```text
===Day16 Ranged Combat===
```

기준 중심:

```text
(-27, 0, -13.5)
```

구성:

```text
Crossbow Display
Day16_Crossbow

Revolver Display
Day16_Revolver

Ranged_DisplayLane

RangedTarget_06m
RangedTarget_10m
RangedTarget_13m

Day16_CrossbowBoltTemplate
RangedCombatDebugPage
```

검·도끼 시험장과 분리된 원거리 무기 시험 구역으로 사용한다.

---

## 25. 원거리 시험 표적

최소 3개의 표적을 배치했다.

```text
RangedTarget_06m
RangedTarget_10m
RangedTarget_13m
```

각 표적은 공통:

```text
CombatHealth
CombatReaction
Collider
```

기반을 사용해
석궁과 리볼버의 피해·경직을 실제 Damage Pipeline으로 시험할 수 있다.

---

## 26. F1 Ranged Combat 페이지

새 진단 페이지:

```text
Assets/ProjectI/Scripts/Diagnostics/RangedCombatDebugPage.cs
```

석궁 주요 정보:

```text
Aiming
Aim FOV
Loaded
Reserve Bolts
Projectile Speed
Damage
Reload Progress
```

리볼버 주요 정보:

```text
Aiming
Aim FOV
Loaded Rounds / Capacity
Reserve Rounds
Current Spread
Damage
Reload Progress
```

공통으로 마지막 Damage Pipeline 처리 결과도 함께 확인할 수 있다.

---

## 27. Day16 자동 Setup

새 파일:

```text
Assets/ProjectI/Editor/Phase4Day16Setup.cs
```

자동 구성:

```text
기존 Day16 루트 정리
↓
Day16 재질 생성
↓
Crossbow Bolt Template 생성
↓
석궁·리볼버 전시대 생성
↓
상세 석궁 생성
↓
상세 리볼버 생성
↓
원거리 표적 생성
↓
F1 Ranged Combat 페이지 추가
↓
씬 저장
↓
Validator 실행
```

최종 완료 마커:

```text
===Day16 Ranged Combat Ready v2===
```

v2는 다음 최종 보정이 반영된 버전이다.

```text
석궁 볼트 95 m/s
리볼버 크기 축소
리볼버 실린더 방향 수정
리볼버 탄약 방향 수정
```

---

## 28. Day16 Validator

새 파일:

```text
Assets/ProjectI/Editor/Phase4Day16Validator.cs
```

검증 항목:

```text
Day16 Ranged Combat 루트
Day16 Ready v2 마커
PlayerInputReader
F1 RangedCombatDebugPage
Day16_Crossbow
Day16_Revolver
두 무기의 WorldItem 연결
석궁 상세 모델 파트 수
리볼버 상세 모델 파트 수
석궁 Muzzle
석궁 Bolt Template
석궁 Projectile Speed >= 94.9
석궁 Damage >= 50
석궁 Aim FOV <= 45
리볼버 Cylinder Capacity = 6
리볼버 Loaded Rounds = 6
리볼버 CylinderRoot
리볼버 Muzzle
리볼버 확대가 석궁보다 약한지
원거리 시험 표적 3개 이상
Crossbow Display
Revolver Display
```

---

## 29. 주요 신규 파일

```text
Assets/ProjectI/Scripts/Combat/Ranged/
├─ RangedWeaponItemBase.cs
├─ CrossbowWeaponItem.cs
├─ CrossbowBoltProjectile.cs
└─ RevolverWeaponItem.cs

Assets/ProjectI/Scripts/Diagnostics/
└─ RangedCombatDebugPage.cs

Assets/ProjectI/Editor/
├─ Phase4Day16Setup.cs
└─ Phase4Day16Validator.cs
```

각 신규 스크립트와 `Ranged` 폴더의 `.meta`도 함께 추가됐다.

---

## 30. 주요 수정 파일

```text
Assets/ProjectI/Scripts/Player/
├─ GameplayInputActions.cs
└─ PlayerInputReader.cs

Assets/ProjectI/Scenes/
└─ ExplorationOffice.unity
```

자동 Setup 적용 과정에서 Day16용 재질도 생성됐다.

```text
Assets/ProjectI/Art/Generated/Day16/
```

---

## 31. Day16 테스트 흐름

### 석궁

```text
1. F로 석궁 획득
2. 빠른 슬롯에서 석궁 선택
3. 우클릭 유지
4. 화면 중앙 조준과 FOV 40 확인
5. 좌클릭 발사
6. 볼트 포물선 비행 확인
7. 볼트가 Damage Pipeline으로 표적에 피해를 주는지 확인
8. R 재장전
9. 시위 당김과 무기 장전 모션 확인
10. 박힌 볼트 F 회수
11. Reserve Bolts 증가 확인
```

### 리볼버

```text
1. F로 리볼버 획득
2. 우클릭 조준
3. 좌클릭으로 6발 발사
4. 실린더 장탄 수 감소 확인
5. 빠르게 발사해 Spread 증가 확인
6. 발사를 멈춰 Spread 회복 확인
7. R 재장전
8. 실린더 오픈·회전 모션 확인
9. 6발 복구 확인
10. 발사 후 총알 회수 오브젝트가 생성되지 않는지 확인
```

---

## 32. Day16 완료 결과

석궁:

```text
F 획득
↓
RMB 확대 조준
↓
LMB 단발
↓
95 m/s 포물선 볼트
↓
Damage Pipeline
↓
볼트 박힘
↓
F 회수
↓
R 재장전
```

리볼버:

```text
F 획득
↓
RMB 조준
↓
LMB 발사
↓
6발 실린더
↓
빠른 연사 시 Spread 증가
↓
Damage Pipeline
↓
탄환 비회수
↓
R 실린더 재장전
```

두 무기가 같은 Damage Pipeline을 사용하면서도
탄도·탄약·조준·장전·사격 템포가 서로 다르게 동작하는 원거리 전투 기반을 구축했다.

---

## 다음 개발 방향

### Day 17 — 몬스터 공통 AI 구축

Day 14~16까지 완성된 플레이어 전투 시스템을
몬스터 공통 행동 구조와 연결한다.

예정 방향:

```text
Monster Data
Spawn 구조
시각 감지
청각 감지
특수 감지
대상 선정
추적
마지막 확인 위치
공통 이동
공통 공격 상태
Damage Pipeline 연결
F1 Monster AI 진단
```

Day 17부터 몬스터의 공격 역시
플레이어 전투와 동일한 Damage Pipeline을 사용하는 구조로 진행한다.
