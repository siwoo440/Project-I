# Project I — 41일차 개발일지

## 작업 주제

절차적 던전 런타임 NavMesh 및 일반 몬스터 경로 이동

## 개발 목표

- 절차적으로 생성되는 던전에 런타임 NavMesh 생성
- 일반 몬스터가 벽과 꺾인 통로를 따라 이동하도록 개선
- 기존 CharacterController 기반 이동과 전투 FSM 유지
- 몬스터 생성 위치를 NavMesh 위로 보정
- 던전 재생성 시 기존 NavMesh 제거 및 새로운 NavMesh 생성
- NavMesh 적용 후 몬스터 외형이 바닥에 파묻히는 문제 수정

## GitHub 기준

41일차 작업 시작 전 main 브랜치의 최신 커밋은 다음과 같다.

- 커밋: `40일차 : Canvas 인벤토리 및 공통 HUD 연결`
- 기준 커밋: `cc5695525d8384c9eb314527a910c8cf07f3fbb5`

Hierarchy 및 에셋 폴더 정리는 아직 완료되지 않았으므로, 이번 커밋에는 런타임 NavMesh와 몬스터 이동에 필요한 변경 사항을 중심으로 포함했다.

## 구현 내용

### 1. RuntimeNavMeshBuilder 추가

절차적으로 배치된 Room Collider를 기반으로 런타임 NavMesh를 생성하는 `RuntimeNavMeshBuilder`를 추가했다.

`DungeonGenerator`의 다음 이벤트에 연결했다.

- `GenerationStarted`
- `GenerationCompleted`

새로운 던전 생성이 시작되면 이전 NavMesh 데이터를 제거한다.

모든 방 배치가 완료되면 현재 활성화된 방과 바닥 Collider를 기반으로 새로운 NavMesh를 생성한다.

시작 방 주변에서 실제 NavMesh 위치를 검색하여 생성 성공 여부도 검증하도록 구성했다.

### 2. NavMeshSurface 설정

DungeonGenerator 오브젝트에 `NavMeshSurface`를 추가했다.

적용한 설정은 다음과 같다.

- Agent Type: Humanoid
- Default Area: Walkable
- Generate Links: 비활성화
- Use Geometry: Physics Colliders
- Collect Objects: Current Object Hierarchy
- Include Layers: Everything
- Override Voxel Size: 비활성화
- Override Tile Size: 비활성화
- Build Height Mesh: 비활성화

AI Navigation 2.0에서는 기존 `Children` 옵션이 Inspector에서 `Current Object Hierarchy`라는 이름으로 표시되는 것을 확인했다.

`Current Object Hierarchy`를 사용하여 DungeonGenerator 자신과 런타임에 생성되는 모든 활성 자식 Room만 NavMesh 생성 대상으로 수집했다.

던전은 실행할 때마다 구조가 달라지므로 Inspector의 Bake 버튼은 사용하지 않고 런타임 빌드를 사용했다.

### 3. MonsterNavigation 추가

기존 CharacterController 이동을 유지하면서 NavMeshAgent의 경로 계산 결과만 제공하는 `MonsterNavigation`을 추가했다.

NavMeshAgent는 다음과 같이 동작한다.

- NavMesh 위의 현재 위치 확인
- 플레이어 주변의 이동 가능 위치 검색
- 플레이어까지의 경로 계산
- 경로의 다음 이동 방향을 MonsterAI에 전달
- 실제 Transform 이동은 기존 CharacterController가 담당
- 실제 몬스터 위치와 NavMeshAgent 내부 위치를 매 프레임 동기화

다음 NavMeshAgent 기능은 코드에서 비활성화했다.

- Update Position
- Update Rotation

이를 통해 NavMeshAgent와 CharacterController가 동시에 Transform을 이동시키는 충돌을 방지했다.

### 4. MonsterAI 연동

기존 `MonsterAI`의 Patrol, Chase, Attack FSM은 유지했다.

`MoveToward` 메서드가 `MonsterNavigation`의 경로 방향을 우선 사용하도록 수정했다.

NavMesh를 정상적으로 사용할 수 있으면 통로 경로 방향으로 이동한다.

NavMesh가 없거나 몬스터 주변에서 이동 가능 영역을 찾지 못하면 기존 직선 이동을 fallback 방식으로 사용한다.

기존 기능 중 다음 항목은 그대로 유지했다.

- 시각 감지
- 청각 감지
- 밝기 연동 감지력
- 배회
- 추격
- 공격
- 공격 대기시간
- 피격
- 방어력
- 사망
- CharacterController 중력

### 5. DungeonSpawnUtility 확장

`DungeonSpawnUtility`에 입력 위치 주변의 NavMesh 위치를 검색하는 공통 메서드를 추가했다.

몬스터 생성 위치를 물리 검사로 먼저 찾은 뒤, 해당 위치 주변에서 실제 이동 가능한 NavMesh 위치를 다시 검색할 수 있도록 구성했다.

### 6. MonsterSpawnManager 연동

MonsterSpawnManager가 몬스터를 생성하기 전에 최종 생성 위치를 NavMesh 위로 보정하도록 수정했다.

NavMesh 위치를 찾지 못하면 잘못된 위치에 몬스터를 생성하지 않고 현재 생성 요청을 건너뛰도록 처리했다.

Inspector에는 다음 설정을 추가했다.

- Nav Mesh Sample Distance: 2

### 7. 일반 몬스터 프리팹 설정

일반 몬스터 프리팹에 다음 컴포넌트를 연결했다.

- CharacterController
- NavMeshAgent
- MonsterAI
- MonsterNavigation

NavMeshAgent는 경로 계산을 담당하고 CharacterController는 실제 충돌, 이동 및 중력을 담당하도록 역할을 분리했다.

NavMeshAgent의 주요 설정은 다음과 같다.

- Agent Type: Humanoid
- Base Offset: 0
- Speed: 3.5
- Angular Speed: 720
- Acceleration: 20
- Stopping Distance: 0.1
- Radius: 0.5
- Height: 2
- Auto Braking: 비활성화
- Auto Repath: 활성화
- Avoidance Priority: 50

MonsterNavigation의 주요 설정은 다음과 같다.

- Self Sample Distance: 2
- Target Sample Distance: 3
- Repath Interval: 0.15
- Target Move Threshold: 0.5
- Stopping Distance: 0.1
- Acceleration: 20

### 8. 몬스터 외형 바닥 파묻힘 수정

NavMesh 적용 후 몬스터 외형이 바닥에 절반 정도 파묻히는 문제가 발생했다.

원인은 NavMesh가 몬스터 루트를 바닥 높이에 배치했지만, 임시 몬스터 외형의 중심점도 루트의 Y 0에 위치하고 있었기 때문이다.

몬스터 프리팹을 다음 구조로 변경했다.

```text
Monster
├─ CharacterController
├─ NavMeshAgent
├─ MonsterAI
├─ MonsterNavigation
└─ Visual
   ├─ MeshFilter
   └─ MeshRenderer

몬스터 루트는 바닥 기준점으로 사용하고 실제 외형은 Visual 자식으로 분리했다.

몬스터 루트 Transform은 다음 값으로 설정했다.

Position: X 0, Y 0, Z 0
Rotation: X 0, Y 0, Z 0
Scale: X 1, Y 1, Z 1

Unity 기본 Sphere를 사용하는 Visual은 다음 값으로 설정했다.

Position: X 0, Y 1, Z 0
Rotation: X 0, Y 0, Z 0
Scale: X 1, Y 2, Z 1

Visual에 자동으로 추가된 SphereCollider는 제거했다.

몬스터 충돌은 루트의 CharacterController만 사용하도록 구성했다.

CharacterController는 다음 값으로 설정했다.

Center: X 0, Y 1, Z 0
Radius: 0.5
Height: 2
Slope Limit: 45
Step Offset: 0.3
Skin Width: 0.08
Min Move Distance: 0

이 구조를 통해 다음 위치 관계를 통일했다.

몬스터 루트: NavMesh 바닥 위치
CharacterController 하단: 바닥 위치
Visual 하단: 바닥 위치
NavMeshAgent Base Offset: 0

Visual 위치 문제를 NavMeshAgent의 Base Offset으로 보정하지 않고 프리팹의 루트와 외형 구조를 수정하여 해결했다.

적용 범위

이번 NavMesh 이동은 다음 대상에 적용했다.

일반 MonsterAI 몬스터
MonsterAI를 사용하는 미믹

다음 기믹 몬스터는 고유 이동 방식 유지를 위해 이번 작업 범위에서 제외했다.

고스트
웃는 석상
스토커
테스트 내용
런타임 NavMesh 생성
Dungeon Scene 실행 후 NavMesh 자동 생성 확인
시작 방 주변에서 이동 가능 영역 검색 확인
DungeonGenerator 자식 Room만 수집되는 것 확인
별도의 수동 Bake 없이 런타임 생성 확인
일반 몬스터 이동
일반 몬스터 자동 생성 확인
생성된 몬스터가 NavMesh 위에 배치되는 것 확인
몬스터가 직선 통로를 따라 이동하는 것 확인
몬스터가 벽을 향해 계속 이동하지 않는 것 확인
꺾인 통로에서 연결된 출입구를 따라오는 것 확인
플레이어 접근 후 기존 공격이 실행되는 것 확인
미믹
미믹이 위장 상태에서 정지하는 것 확인
접근 또는 상호작용 시 위장 해제 확인
위장 해제 후 기습 피해 확인
기습 후 NavMesh 경로 추적 확인
던전 재생성
F9 던전 재생성 확인
이전 NavMesh 데이터 제거 확인
이전 몬스터 제거 확인
새로운 방 배치 후 NavMesh 재생성 확인
새로 생성된 몬스터의 NavMesh 이동 확인
외형 위치
몬스터 루트가 NavMesh 바닥 높이에 배치되는 것 확인
Visual의 Local Position Y가 1로 유지되는 것 확인
몬스터 외형의 하단이 바닥에 닿는 것 확인
이동 중 외형이 바닥에 파묻히지 않는 것 확인
이동 중 외형이 바닥 위로 뜨지 않는 것 확인
현재 제한사항
동적으로 열리는 문은 NavMesh에 즉시 반영되지 않는다.
잠긴 문과 금간 벽은 NavMeshObstacle 연동이 필요하다.
고스트는 순간이동 중심의 기존 이동을 유지한다.
웃는 석상은 화면 판정 기반의 기존 이동을 유지한다.
스토커는 기존 고유 추적 방식을 유지한다.
방 프리팹의 바닥과 벽 Collider 설정에 따라 NavMesh 결과가 달라질 수 있다.
결과

절차적으로 생성되는 던전에 런타임 NavMesh 이동 기반을 추가했다.

일반 몬스터와 미믹은 기존 CharacterController 기반 이동, 중력 및 전투 FSM을 유지하면서 벽과 꺾인 통로를 고려해 플레이어를 추적할 수 있다.

AI Navigation 2.0의 Current Object Hierarchy 설정을 사용하여 DungeonGenerator 아래에 생성되는 Room만 NavMesh 생성 대상으로 제한했다.

NavMesh 적용 후 발생한 몬스터 외형의 바닥 파묻힘 문제는 몬스터 루트와 Visual을 분리하고 Visual의 높이를 보정하여 해결했다.