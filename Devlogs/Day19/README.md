# Project I 개발 일지

## Day 19 — 함정 테스트 구역 재배치 및 주기형 도끼·바닥 가시 동작 보정

- 날짜: 2026-09-01
- 개발 단계: Phase 4 — 전투·몬스터·함정
- 기준 최신 커밋: `dc33e670a801ac37937ff706360d3524c8271732`
- 기준 커밋 제목: `18일차 : 바닥·천장 가시 및 도끼·압력판 함정과 공통 Damage Pipeline 구현`
- 기준 씬: `Assets/ProjectI/Scenes/ExplorationOffice.unity`

---

## 개발 목표

Day18에서 구축한 함정 시스템의 기본 기능은 유지하면서,
실제 테스트 과정에서 확인된 배치와 움직임을 보정했다.

이번 일차에서는 새로운 함정 종류를 추가하기보다
기존 바닥 가시와 회전 도끼가 테스트맵에서 더 명확하게 동작하도록 다음 내용을 수정했다.

```text
1. Day18 함정 시험장 전체 위치 이동
2. 도끼를 Trigger형에서 상시 왕복형으로 변경
3. 도끼 회전 범위를 정확한 180°로 확대
4. 바닥 가시를 자동 반복형으로 변경
5. 도끼와 바닥 가시의 속도를 최종적으로 50% 감소
```

---

## 1. 함정 시험장 위치 이동

기존 Day18 함정 시험장은 Day17 몬스터 Spawn Line과
SprintLane 부근에 가까워 다른 테스트 오브젝트와 시각적으로 겹칠 가능성이 있었다.

이에 Day18 함정 시험장 전체를
테스트맵 남동쪽의 비교적 비어 있는 영역으로 이동하도록 보정했다.

기존 중심:

```text
(-27, 0, 18.2)
```

변경 중심:

```text
(21, 0, -24.5)
```

대략적인 시험장 범위:

```text
X : 15.6 ~ 26.4
Z : -28.5 ~ -20.5
```

---

## 2. 루트 단위 이동

개별 함정을 각각 다시 배치하지 않고:

```text
===Day18 Trap System===
```

루트 전체를 평행 이동하도록 구성했다.

따라서 다음 상대 배치는 유지된다.

```text
Pressure Plate
↓
Floor Spike

Swinging Axe

Ceiling Spike Slam
```

함정 내부 Transform과 연결 관계는 그대로 보존한다.

---

## 3. Phase4Day18Relocation 추가

새 Editor 보정 스크립트:

```text
Assets/ProjectI/Editor/Phase4Day18Relocation.cs
```

를 추가했다.

주요 역할:

```text
ExplorationOffice 확인
↓
Day18 Trap Root 검색
↓
Trap_TestFloor 위치 확인
↓
목표 중심과 현재 위치 차이 계산
↓
Day18 Root 전체 평행 이동
↓
씬 저장
```

자동 적용과 함께 수동 메뉴도 제공한다.

```text
Tools
→ Project I
→ Day 18
→ Relocate Trap Test Area
```

---

# 회전 도끼 보정

## 4. 기존 도끼 동작

Day18 초기 도끼는:

```text
Hidden Trigger 진입
↓
Warning
↓
한쪽에서 반대쪽으로 Swing
↓
잠시 유지
↓
원위치 복귀
↓
Cooldown
```

형태였다.

이 방식은 통로 함정의 기능 확인에는 적합했지만
계속 움직이는 장애물 형태의 테스트에는 맞지 않았다.

---

## 5. 도끼 상시 왕복형 변경

`SwingingAxeTrap`을 외부 Trigger 없이
Play Mode 시작부터 계속 움직이는 함정으로 변경했다.

최종 흐름:

```text
-90°
↓
+90°
↓
-90°
↓
+90°
↓
계속 반복
```

한쪽 끝에 도달하면 별도의 정지 단계 없이
즉시 반대 방향으로 다시 움직인다.

---

## 6. 도끼 정확한 180° 회전

초기 Day18 각도:

```text
-72° ↔ +72°
```

총 회전 범위:

```text
144°
```

였다.

Day19에서:

```text
-90° ↔ +90°
```

로 수정했다.

최종 회전 범위:

```text
180°
```

즉 도끼날이 한쪽 끝에서 반대쪽 끝까지
완전한 반원 범위를 반복해서 가로지른다.

---

## 7. 도끼 왕복별 피해 초기화

도끼는 계속 움직이지만
한 번의 이동 중 같은 대상에게 매 프레임 피해가 들어가면 안 된다.

따라서:

```text
-90° → +90°
= 하나의 Attack ID

+90° → -90°
= 새로운 Attack ID
```

로 구분한다.

한 왕복 구간에서 이미 맞은 대상은 다시 피해를 받지 않고,
방향이 바뀌어 새로운 구간이 시작되면 다시 피격 가능 상태가 된다.

---

## 8. 도끼 Trigger 비의존 구조

최종 도끼는 자동 왕복형이므로:

```text
TriggerTrap()
```

호출을 동작 시작 조건으로 사용하지 않는다.

Play Mode 시작 시 자동으로:

```text
BeginAutomaticSweep()
```

를 수행한다.

기존 Hidden Trigger가 씬에 남아 있더라도
도끼의 지속 왕복 동작에는 영향을 주지 않는다.

---

# 바닥 가시 보정

## 9. 기존 바닥 가시 동작

Day18 초기 바닥 가시는:

```text
Pressure Plate
↓
Warning
↓
Rise
↓
Active
↓
Reset
↓
Cooldown
```

형태였다.

즉 기본적으로 외부 Trigger를 기다리는 방식이었다.

---

## 10. 바닥 가시 자동 반복형 변경

Day19에서 바닥 가시도
압력판 없이 스스로 반복 작동하도록 변경했다.

흐름:

```text
숨김 상태
↓
자동 대기
↓
Warning
↓
Rise
↓
Active
↓
Reset
↓
다시 자동 대기
↓
반복
```

따라서 플레이어는
가시가 올라오는 주기를 확인하며 이동해야 한다.

---

## 11. 기존 압력판 호환 유지

바닥 가시는 자동 반복형으로 변경했지만
기존 `TriggerTrap()` 인터페이스는 제거하지 않았다.

따라서 가시가 자동 대기 상태일 때:

```text
Pressure Plate
↓
TriggerTrap()
↓
자동 대기 시간이 남아 있어도 즉시 Warning 시작
```

이 가능하다.

즉:

```text
자동 주기
+
외부 조기 작동
```

을 모두 지원한다.

---

# 최종 속도 조정

## 12. 도끼 속도 50% 감소

상시 180° 왕복형으로 변경한 최초 값:

```text
-90° → +90°
0.85초
```

를 테스트 가독성을 위해 절반 속도로 낮췄다.

최종:

```text
-90° → +90°
1.70초

+90° → -90°
1.70초
```

따라서 한쪽 끝에서 반대쪽 끝까지 이동 속도가
기존 보정 버전의 50%가 됐다.

회전 범위와 피해량은 유지한다.

```text
회전 범위 180°
Damage 55
```

---

## 13. 바닥 가시 전체 속도 50% 감소

자동 반복형으로 변경한 최초 값:

```text
자동 대기     1.60초
Warning       0.18초
Rise          0.12초
Active        0.32초
Reset         0.36초
```

최종적으로 모든 시간을 2배로 늘렸다.

```text
자동 대기     3.20초
Warning       0.36초
Rise          0.24초
Active        0.64초
Reset         0.72초
```

따라서 단순히 대기시간만 늘어난 것이 아니라
실제 상승·유지·하강 모션을 포함한 전체 사이클이
기존 자동 반복 버전의 절반 속도로 동작한다.

피해량은 유지한다.

```text
Damage 35
```

---

## 14. 천장 가시 유지

이번 Day19 보정에서는:

```text
CeilingSpikeSlamTrap
```

의 주기와 동작은 변경하지 않았다.

Day18에서 구현한:

```text
Waiting
↓
Warning
↓
Slam
↓
Active
↓
Return
```

자동 반복 구조를 그대로 유지한다.

---

## 15. 압력판 유지

`PressurePlate` 자체의 작동 규칙도 변경하지 않았다.

플레이어와 몬스터가 압력판을 밟을 수 있으며,
기존 바닥 가시 연결 역시 유지한다.

바닥 가시가 자동 반복형이 된 이후에는
압력판이 가시를 조기에 작동시킬 수 있는 추가 Trigger 역할을 한다.

---

## 16. Damage Pipeline 유지

함정 피해 구조는 Day18 설계를 그대로 유지한다.

```text
Trap
↓
TrapDamageSource
↓
DamageInfo

Faction = Environment
DamageType = Trap

↓
DamagePipeline
↓
Player / Damageable Monster
```

따라서 Day19에서는
Damage Pipeline 자체를 수정하지 않았다.

---

## 17. 웃는 석상 규칙 유지

웃는 석상은 계속:

```text
CombatHealth 없음
IDamageable 없음
```

상태다.

따라서 이번에 지속 움직임으로 변경된 도끼와
자동 반복 바닥 가시에도 피해를 받지 않는다.

```text
Floor Spike
Swinging Axe
Ceiling Spike

→ Smiling Statue Damage 없음
```

---

# 주요 수정 파일

## 18. 수정된 기존 파일

```text
Assets/ProjectI/Scripts/Traps/
├─ SwingingAxeTrap.cs
└─ FloorSpikeTrap.cs
```

### SwingingAxeTrap.cs

변경:

```text
Trigger형 Swing 제거
상시 자동 왕복
-90° ↔ +90°
180° 이동
왕복별 Attack ID 갱신
이동 시간 1.70초
```

### FloorSpikeTrap.cs

변경:

```text
자동 반복 대기 추가
Pressure Plate 호환 유지

3.20초 대기
0.36초 경고
0.24초 상승
0.64초 유지
0.72초 하강
```

---

## 19. 신규 파일

```text
Assets/ProjectI/Editor/
├─ Phase4Day18Relocation.cs
└─ Phase4Day18Relocation.cs.meta
```

Day18 함정 시험장 전체 위치를
테스트맵 남동쪽으로 이동시키는 Editor 보정 파일이다.

---

# 최종 Day19 테스트 흐름

## 20. 도끼 시험

```text
Play Mode
↓
외부 Trigger 없이 도끼가 움직이는지 확인
↓
-90°에서 +90°까지 이동
↓
즉시 반대 방향
↓
계속 반복
```

확인 항목:

```text
회전 범위 180°
한 방향 1.70초
양방향 지속 반복
다음 왕복에서 재피격 가능
```

---

## 21. 바닥 가시 시험

```text
Play Mode
↓
가시 숨김
↓
3.20초 대기
↓
0.36초 Warning
↓
0.24초 Rise
↓
0.64초 Active
↓
0.72초 Reset
↓
다시 반복
```

---

## 22. 압력판 조기 작동 시험

```text
Floor Spike 자동 대기 중
↓
Player / Monster가 Pressure Plate 밟음
↓
남은 자동 대기시간 무시
↓
Warning
↓
Rise
```

을 확인한다.

---

## 23. 새 시험장 위치 확인

씬에서 Day18 함정 시험장이:

```text
중심 X 21
Z -24.5
```

부근으로 이동했는지 확인한다.

확인 대상:

```text
Player 시작 지점
SprintLane
Day17 Monster Spawn
CrouchTunnel
기존 이동 테스트 오브젝트
```

와 물리적으로 겹치지 않는지 Scene View에서 최종 확인한다.

---

# Day19 완료 결과

Day18에서 만든 함정 시스템을
단순 기능 검증 상태에서 반복 관찰이 쉬운 테스트 구조로 보정했다.

최종 변화:

```text
기존
Trigger형 도끼
압력판 중심 바닥 가시
기존 테스트 구역

↓

Day19

도끼
→ -90° ↔ +90°
→ 180° 상시 왕복
→ 한 방향 1.70초

바닥 가시
→ 자동 반복
→ 전체 속도 50% 감소
→ 압력판 조기 작동 유지

함정 시험장
→ 테스트맵 남동쪽 독립 구역으로 이동
```

기존:

```text
Damage Pipeline
TrapDamageSource
Pressure Plate
Ceiling Spike Slam
Player / Monster 피해 규칙
웃는 석상 비피격 규칙
```

은 유지했다.

---

## 다음 개발 방향

다음 단계에서는 Day14~19까지 구축한:

```text
플레이어 근접 전투
플레이어 원거리 전투
4종 몬스터 AI
바닥 가시
천장 가시
회전 도끼
압력판
```

을 실제 플레이 흐름에 함께 배치해
전투·추격·함정 회피·몬스터 유인을 연속적으로 시험하는 통합 테스트가 적절하다.
