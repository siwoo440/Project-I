# Project I 개발 일지

## Day 4 — 플레이어 이동 확장 및 체력·추락 피해 구현

- 날짜: 2026-08-27
- 개발 단계: Phase 2 — 플레이어 조작과 상호작용
- 기준 커밋: `ebc23ecdce87c19a18dc636e2690f47f9ecbbcae`
- 기준 커밋 메시지: `a`
- 이전 기준: `112b9267c03d1ed06a715f045a75e4c80b7c0f96`

---

## 개발 목표

3일차에 구현한 1인칭 이동·달리기·스태미나 기반을 확장하여
실제 탐사 맵에서 사용할 수 있는 점프·웅크리기·추락·체력 시스템을 구성한다.

또한 `ExplorationOffice` 테스트 맵에
웅크리기·추락 피해·급경사·이동 플랫폼 시험 구역을 추가하여
이후 플레이어 기능을 계속 검증할 수 있는 공용 테스트 환경으로 확장한다.

---

## 구현 내용

### 1. 점프·중력·착지

기존 `PlayerMovement`를 확장하여 다음 기능을 구현했다.

- 지상 상태에서만 점프
- 점프 높이 기반 초기 수직 속도 계산
- 공중 중력 가속
- 지면 밀착용 하강 속도
- 공중 최고 높이 기록
- 착지 상태 감지
- 실제 추락 거리 계산
- 착지 이벤트 발생

점프 중 웅크린 상태에서는 점프가 발생하지 않으며,
사망 상태에서는 플레이어 이동 입력을 차단한다.

### 2. 웅크리기

`PlayerCrouch`를 추가했다.

- CharacterController 높이 변경
- 카메라 높이 변경
- 부드러운 서기/웅크리기 전환
- 웅크린 상태 이동 속도 감소
- 웅크린 상태 달리기 제한
- 천장 공간 검사
- 낮은 천장 아래에서 일어서기 차단

### 3. 체력 시스템

`HealthState`와 `PlayerHealth`를 추가했다.

기본 최대 체력은 100이며 다음 기능을 제공한다.

- 공통 피해 처리
- 체력 회복
- 현재/최대 체력 조회
- 체력 비율 조회
- 사망 상태 판정
- 체력 변경 이벤트
- 사망 이벤트

이후 몬스터·함정·환경 피해도 동일한 `PlayerHealth.TakeDamage()` 구조를 사용할 수 있다.

### 4. 추락 피해

`FallDamageCalculator`와 `PlayerFallDamage`를 추가했다.

기본 규칙은 다음과 같다.

- 안전 추락 거리: 3m
- 안전 거리 이하는 피해 없음
- 초과 1m당 20 피해
- 한 번의 추락 최대 피해: 100

착지 시 `PlayerMovement`에서 전달되는 실제 추락 거리를 사용하여
피해를 계산하고 공통 체력 시스템에 적용한다.

### 5. 이동 플랫폼

`MovingPlatform`과 `MovingPlatformPassengerTrigger`를 추가했다.

- Rigidbody 기반 왕복 이동
- Kinematic 이동
- 보간 적용
- 양 끝 지점 대기
- 플랫폼 탑승 시 플레이어 이동 연동
- 플랫폼 이탈 시 연결 해제

### 6. ExplorationOffice 테스트 맵 확장

기존 3일차 공용 테스트 맵을 유지하면서
다음 4일차 시험 구역을 추가했다.

- `05_CrouchTest`
- `06_FallTest`
- `07_MovingPlatformTest`
- 기존 Stair/Ramp 구역의 급경사 시험 요소

기존 예비 구역인 다음 오브젝트는 실제 시험 구역으로 교체했다.

- `05_CrouchGate_Future`
- `06_FallTest_Future`

### 7. 웅크리기 시험 구역

`05_CrouchTest`에서 다음을 확인할 수 있다.

- 일반 높이 상태에서 낮은 통로 진입 제한
- 웅크린 상태 통과
- 낮은 천장 아래 일어서기 차단
- 통로 이탈 후 정상적으로 다시 일어서기

### 8. 추락 시험 구역

`06_FallTest`에 고도별 추락 발판을 배치했다.

- `SafeDrop_2m`
- `DamageDrop_4m`
- `HighDrop_7m`

낙하 높이에 따라 무피해·일반 피해·고위험 피해를 비교할 수 있다.

### 9. Day 4 자동 적용 및 검증

`Phase2Day4Setup`을 추가하여
Unity Editor에서 4일차 플레이어 컴포넌트와 테스트 구역을 자동 구성하도록 했다.

수동 적용 메뉴:

`Tools → Project I → Day 4 → Apply Day 4 Upgrade`

검증 메뉴:

`Tools → Project I → Day 4 → Validate`

Validator는 다음 항목을 확인한다.

- Move / Look / Sprint / Jump / Crouch 입력
- 기존 Player 핵심 Component
- Health / Crouch / Fall Damage Component
- Crouch / Fall / Moving Platform 테스트 구역
- Moving Platform Component
- Health 상태 로직
- Fall Damage 계산 로직

---

## 주요 생성 파일

- `Assets/ProjectI/Editor/Phase2Day4Setup.cs`
- `Assets/ProjectI/Editor/Phase2Day4Validator.cs`
- `Assets/ProjectI/Scripts/Player/FallDamageCalculator.cs`
- `Assets/ProjectI/Scripts/Player/HealthState.cs`
- `Assets/ProjectI/Scripts/Player/PlayerCrouch.cs`
- `Assets/ProjectI/Scripts/Player/PlayerFallDamage.cs`
- `Assets/ProjectI/Scripts/Player/PlayerHealth.cs`
- `Assets/ProjectI/Scripts/World/MovingPlatform.cs`
- `Assets/ProjectI/Scripts/World/MovingPlatformPassengerTrigger.cs`

## 주요 수정 파일

- `Assets/ProjectI/Scenes/ExplorationOffice.unity`
- `Assets/ProjectI/Scripts/Player/PlayerDebugHud.cs`
- `Assets/ProjectI/Scripts/Player/PlayerInputReader.cs`
- `Assets/ProjectI/Scripts/Player/PlayerMovement.cs`

---

## 저장소 점검 결과

3일차 기준 커밋에서 4일차 커밋까지의 변경 파일을 확인했다.

4일차에서 계획한 주요 코드와 Scene 변경이 저장소에 반영되어 있으며,
체력·웅크리기·추락 피해·이동 플랫폼 및 Day 4 Validator 구조에서
즉시 진행을 막을 만한 문제는 확인되지 않았다.

GitHub에 연결된 CI 상태 검사는 현재 없으므로
Unity 컴파일과 Play Mode 실행 여부는 로컬 Editor에서 최종 확인한다.

---

## 로컬 최종 확인 항목

- `Tools → Project I → Day 4 → Validate` 전체 PASS
- Console Error 0개
- 기존 WASD 이동 정상
- 기존 마우스 시점 정상
- 기존 Shift 달리기·스태미나 정상
- Space 점프 정상
- 공중 추가 점프 차단
- 계단 이동 정상
- 일반 경사 이동 정상
- 급경사 진입 제한
- 웅크리기 정상
- 낮은 천장 아래 일어서기 차단
- 2m 추락 피해 없음
- 4m 추락 피해 발생
- 7m 추락 큰 피해 발생
- HP 감소 정상
- 왕복 이동 플랫폼 정상
- Boot → MainMenu → ExplorationOffice 기존 흐름 유지

---

## 다음 개발

Day 5에서는 플레이어 상호작용과 월드 아이템 시스템을 구현한다.

- F 상호작용 대상 감지
- 상호작용 안내 UI
- 누르기 상호작용
- 길게 누르기 상호작용
- 전환형 상호작용
- 월드 아이템 줍기
- 아이템 내려놓기
- 아이템 투척
- 월드 아이템 물리 안정화
