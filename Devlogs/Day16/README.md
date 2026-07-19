# 프로젝트 I — 16일차 개발 일지

단계: Phase 1 — 프로토타입(싱글 핵심 루프)

## 목표

던전 생성이 완료된 뒤 각 방의 밝기를 기준으로 몬스터를 자동 배치하고, 기존에 씬에 직접 배치한 테스트 몬스터를 자동 스폰 방식으로 전환한다.

## 한 일

### 1. 던전 생성 완료 이벤트 추가

- `DungeonGenerator`에 현재 생성된 방 목록을 반환하는 `PlacedRooms` 추가
- 원점에 생성된 시작 방을 반환하는 `StartRoom` 추가
- 던전 생성 완료 시 실행되는 `GenerationCompleted` 이벤트 추가
- 방 생성과 플레이어 시작 위치 배치가 끝난 뒤 생성 완료 이벤트 호출

### 2. SpawnManager 몬스터 자동 스폰 구현

- 기존 `SpawnManager` 스텁을 실제 몬스터 자동 스폰 기능으로 교체
- `DungeonGenerator.GenerationCompleted` 이벤트 구독
- 던전 생성 완료 후 전체 방을 순회하여 몬스터 생성
- 시작 방은 자동 스폰 대상에서 제외
- 몬스터 프리팹 목록에서 무작위 프리팹 선택
- 자동 생성한 몬스터를 목록으로 관리하고 재생성 시 기존 몬스터 제거

### 3. 밝기 기반 스폰 배율 적용

- 방의 `LightRoom.FixedBrightness`를 기준으로 몬스터 수 계산
- 매우 밝은 방은 기본 스폰량의 50% 적용
- 밝은 방은 기본 스폰량의 80% 적용
- 보통 밝기 방은 기본 스폰량의 100% 적용
- 어두운 방은 기본 스폰량의 150% 적용
- 칠흑 방은 기본 스폰량의 200% 적용

### 4. 안전한 스폰 위치 탐색

- `LightRoom`의 Collider 범위에서 무작위 위치 선택
- 벽 근처를 제외하기 위한 `Wall Margin` 적용
- 위에서 아래로 Raycast하여 방 바닥 위치 확인
- 플레이어 주변의 `Player Safe Distance` 범위 제외
- `Physics.CheckSphere`로 장애물과 겹치는 위치 제외
- 안전한 위치를 찾지 못하면 해당 몬스터 생성을 건너뛰고 경고 출력

### 5. Dungeon Hierarchy 구성

- 기존 `Dungeon` 오브젝트의 `DungeonGenerator`와 `DungeonTimeSystem` 유지
- Hierarchy 최상위에 `SpawnManager` 오브젝트 생성
- `SpawnManager` 컴포넌트 추가
- `Dungeon Generator` 항목에 기존 `Dungeon` 오브젝트 연결
- `SpawnManager`를 `Dungeon`이나 `Player`의 자식으로 배치하지 않고 독립 오브젝트로 구성

### 6. 몬스터 프리팹 생성

- 기존 씬의 수동 배치 `Monster` 오브젝트 확인
- `MonsterAI`, `CharacterController`, Renderer가 포함된 상태로 프리팹 생성
- `Assets/Prefabs/Monsters/Monster_Basic.prefab` 생성
- `SpawnManager.Monster Prefabs` 배열에 `Monster_Basic` 연결
- 프리팹 연결 후 씬에 직접 배치했던 기존 `Monster` 삭제

### 7. 방 프리팹 스폰 영역 확인

- 각 방 프리팹의 `Room` 컴포넌트와 4방향 벽 연결 확인
- `LightRoom`과 Box Collider가 같은 오브젝트에 있는지 확인
- LightRoom Box Collider를 방 내부 영역에 맞게 설정
- LightRoom Collider의 `Is Trigger` 활성화 확인
- 바닥 오브젝트의 Box Collider 또는 Mesh Collider 확인
- 바닥 Collider의 `Is Trigger` 비활성화 확인

### 8. SpawnManager 설정

- 방별 기본 몬스터 수를 1~2마리로 설정
- 시작 방 스폰 제외 활성화
- 벽 여백을 1.5로 설정
- 몬스터 생성 높이를 1.1로 설정
- 충돌 확인 반경을 0.45로 설정
- 위치 검색 횟수를 12회로 설정
- 플레이어 안전거리를 4로 설정
- 자동 스폰 몬스터 수 디버그 표시 활성화

## 최종 Hierarchy

```text
Dungeon
Player
SpawnManager
Wagon
기타 테스트 아이템
```

주요 컴포넌트 구성:

```text
Dungeon
├─ DungeonGenerator
└─ DungeonTimeSystem

SpawnManager
└─ SpawnManager

Player
├─ CharacterController
├─ PlayerController
├─ InventorySystem
├─ PlayerInteractor
└─ PlayerCombat
```

## 결과

- 던전 생성이 완료된 직후 몬스터 자동 스폰이 실행된다.
- 시작 방을 제외한 나머지 방에 몬스터가 자동 배치된다.
- 방마다 생성되는 몬스터의 수와 위치가 달라진다.
- 어두운 방일수록 더 많은 몬스터가 생성된다.
- 몬스터가 벽과 플레이어 주변을 피해서 생성된다.
- 자동 생성된 몬스터가 기존 배회·탐지·추격·공격 AI를 정상적으로 사용한다.
- 씬에 직접 배치했던 테스트 몬스터가 제거되고 프리팹 기반 자동 스폰으로 전환되었다.
- Game 뷰에서 현재 살아 있는 자동 스폰 몬스터 수를 확인할 수 있다.

## 다음 예정

17일차에는 `SpawnManager`를 확장하여 보물을 방마다 자동 배치하고, 방 밝기에 따라 보물의 생성 수량과 생성 가치를 조정한다.

## 참고 TODO

- 현재 몬스터 종류는 `Monster Prefabs` 배열에 등록된 프리팹 중 무작위로 선택한다.
- 몬스터 종류별 출현 확률과 던전별 스폰 테이블은 이후 데이터 시스템으로 분리한다.
- 방 내부 장애물 배치가 복잡해지면 전용 스폰 포인트 또는 스폰 가능 영역 시스템으로 교체한다.
- 몬스터 오브젝트 풀링은 최적화 단계에서 적용한다.
- 멀티플레이 단계에서는 호스트가 몬스터 스폰과 AI를 관리하도록 변경한다.
