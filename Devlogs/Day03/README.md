# Project I 개발 일지

## Day 3 — 1인칭 이동·스태미나 및 테스트 맵 구현

- 날짜: 2026-08-27
- 개발 단계: Phase 2 — 플레이어 조작과 상호작용
- 기준 커밋: `194eb2e11194448c6f24f1c45f6181866e98e23f`
- 기준 커밋 메시지: `a`

---

## 개발 목표

Project I의 기본 1인칭 조작을 구현하고,
이후 점프·웅크리기·추락 피해·상호작용·아이템 물리까지 계속 사용할 수 있는
공용 테스트 맵을 `ExplorationOffice`에 구성한다.

---

## 구현 내용

### 1. 플레이어 입력 구조

새 Input System을 기반으로 플레이어 입력을 별도 래퍼로 분리했다.

현재 연결된 주요 입력은 다음과 같다.

- Move
- Look
- Sprint
- Jump 기반 입력
- Crouch 기반 입력
- Interact 기반 입력

3일차에서는 Move, Look, Sprint를 실제 플레이 기능에 사용하고,
Jump, Crouch, Interact는 이후 일차 확장을 위한 입력 기반으로 유지한다.

### 2. 1인칭 시점

플레이어 자식 Camera를 기준으로 1인칭 시점을 구성했다.

- 마우스 좌우 회전
- 마우스 상하 회전
- 상하 시점 각도 제한
- 플레이 시작 시 커서 잠금
- ESC로 커서 잠금/해제 전환

### 3. 플레이어 이동

`CharacterController` 기반 기본 이동을 구현했다.

- WASD 이동
- 플레이어 방향 기준 이동
- 걷기
- Shift 달리기
- 좌우 이동 속도 보정
- 후진 이동 속도 보정
- 대각선 속도 보정
- 기본 중력 및 지면 밀착

기본 설정값은 다음과 같다.

- 걷기 속도: 4.2 m/s
- 달리기 속도: 6.6 m/s
- 좌우 이동 배율: 0.9
- 후진 이동 배율: 0.8

### 4. 스태미나

달리기와 연결되는 스태미나 시스템을 구현했다.

- 최대 스태미나 100
- 달리기 중 초당 18 소비
- 달리기 종료 후 0.75초 뒤 회복 시작
- 초당 25 회복
- 완전 소진 시 탈진
- 15 이상 회복 후 다시 달리기 가능

런타임 컴포넌트와 별도로 `StaminaState` 순수 상태 모델을 분리하여
이후 UI와 네트워크 시스템에서도 사용할 수 있도록 구성했다.

### 5. ExplorationOffice 테스트 맵

기존 기준선 Scene인 `ExplorationOffice`를
플레이어 기능을 반복 검증할 수 있는 테스트 맵으로 확장했다.

주요 시험 구역은 다음과 같다.

- Main Floor
- Sprint Lane
- Slalom
- Narrow Corridor
- Stair / Ramp
- Crouch Gate
- Fall Test
- Industrial Decoration

Sprint Lane에서는 달리기와 스태미나를,
Slalom에서는 방향 전환과 대각선 이동을,
Narrow Corridor에서는 충돌과 좁은 공간 이동을 시험할 수 있다.

Crouch Gate와 Fall Test는 다음 일차의
웅크리기·점프·추락 피해 검증을 위해 미리 배치했다.

### 6. Day 3 검증 도구

`Tools → Project I → Day 3 → Validate` 메뉴에서 다음을 검사하도록 구성했다.

- Input Action Asset 존재
- Move / Look / Sprint Action 존재
- Player 필수 Component 존재
- Player Main Camera 존재
- Exploration Test Zone 존재
- Main Camera 단일 구성

---

## 로컬 확인 항목

GitHub 저장소 기준으로 3일차 코드와 에셋 구성은 확인했다.

다음 항목은 Unity Editor에서 최종 확인한다.

- Day 3 Validator 전체 PASS
- Console Error 0개
- WASD 이동 정상
- 마우스 시점 정상
- Shift 달리기 정상
- 스태미나 소비 및 회복 정상
- ESC 커서 잠금/해제 정상
- 테스트 맵 충돌 및 이동 정상
- Boot → MainMenu → ExplorationOffice 기존 흐름 유지

---

## 다음 개발

Day 4에서는 플레이어 이동 기능을 확장한다.

- 점프
- 중력 및 착지 처리
- 웅크리기
- 천장 검사
- 계단·경사면 대응
- 추락 판정 및 추락 피해
- 체력·공통 피해 구조
