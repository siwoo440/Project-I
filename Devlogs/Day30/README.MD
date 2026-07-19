# 30일차 : 마을 허브 및 마차 출발 흐름

## 개발 목표

- 상점과 빚 정산에 사용할 마을 허브 구성
- 던전 종료 후 마을 자동 복귀 구현
- 마을 마차 위 플레이어 도착 처리
- 출발 레버와 마차 이동 구현
- 마차 이동 완료 후 던전 선택 화면 활성화
- 선택한 던전에 따른 난이도와 보상 차등 적용

## 구현 내용

### 1. 마을 허브 구성

- Village Scene에 마을 광장과 이동 가능한 길을 구성했다.
- 아이템 상점, 빚 정산소, 선술집, 대장간 건물의 임시 형태를 배치했다.
- 우물, 화로, 조명 등 마을 환경 오브젝트를 추가했다.
- 다음 던전으로 이동할 마을 전용 마차를 제작했다.
- 마을 마차에는 던전 적재용 Wagon 컴포넌트를 사용하지 않도록 분리했다.

### 2. 던전 종료 후 자동 마을 복귀

- 기존 F6 마을 이동 방식을 제거했다.
- 던전 결과가 확정되면 결과 화면을 10초 동안 표시하도록 수정했다.
- 결과 화면에 마을 이동까지 남은시간을 표시했다.
- Time.timeScale이 0인 상태에서도 시간이 흐르도록 Time.unscaledDeltaTime을 사용했다.
- 10초가 지나면 Village Scene으로 자동 이동하도록 구현했다.

### 3. 마을 플레이어 배치

- VillagePlayerSpawner를 추가했다.
- 마을에 도착한 플레이어를 마차의 PassengerSpawn 위치에 배치했다.
- 기존 Scene 플레이어를 재사용하거나 Player 프리팹을 생성할 수 있도록 구성했다.
- CharacterController를 잠시 비활성화한 뒤 위치를 변경하여 충돌 문제를 방지했다.
- 정산 창이 열려 있는 동안 플레이어 이동, 전투, 상호작용을 차단했다.
- 정산 창을 닫으면 플레이어 조작이 자동으로 복구되도록 구현했다.

### 4. 마차 출발 레버

- VillageDepartureLever를 IInteractable 방식으로 구현했다.
- 플레이어가 레버를 바라보면 E 상호작용 안내가 표시되도록 연결했다.
- 정산이 완료되지 않은 상태에서는 출발할 수 없도록 제한했다.
- 캠페인 성공 또는 실패 상태에서도 다음 던전 출발을 차단했다.
- 레버 사용에 성공하면 레버가 회전하도록 임시 연출을 추가했다.
- 중복으로 레버를 작동할 수 없도록 처리했다.

### 5. 마차 이동

- VillageWagonTravelController를 추가했다.
- Kinematic Rigidbody와 MovePosition을 사용하여 마차를 이동시켰다.
- 마차 이동속도는 초당 4m로 설정했다.
- 마차 이동거리는 25m로 설정했다.
- 마차 모델의 앞쪽 방향에 맞춰 Local Z -1 방향으로 이동하도록 구성했다.
- 이동 중 플레이어를 PassengerSpawn에 연결하여 마차와 함께 이동시켰다.
- 이동 중에는 플레이어의 이동, 전투, 상호작용을 차단했다.
- 목적지에 도착하면 플레이어를 마차에서 분리했다.

### 6. 던전 선택 데이터

- DungeonRouteData ScriptableObject를 추가했다.
- 던전별 이름, 설명, Scene 이름, 위험도, 방 개수, 스폰 배율, 보상 배율을 저장하도록 구성했다.
- 세 종류의 임시 던전 경로를 제작했다.

#### 폐허 지하묘지

- 위험도: 1/5
- 방 개수: 8개
- 몬스터 배율: 0.85배
- 함정 배율: 0.75배
- 보물 가치: 1.00배

#### 버려진 광산

- 위험도: 3/5
- 방 개수: 10개
- 몬스터 배율: 1.00배
- 함정 배율: 1.00배
- 보물 가치: 1.25배

#### 침수된 성채

- 위험도: 5/5
- 방 개수: 12개
- 몬스터 배율: 1.25배
- 함정 배율: 1.35배
- 보물 가치: 1.50배

### 7. 던전 선택 매니저

- DungeonSelectionManager를 추가했다.
- 선택한 DungeonRouteData를 Scene 전환 이후에도 유지하도록 구현했다.
- 캠페인의 다음 탐험 가능 여부를 확인한 뒤 Dungeon Scene을 로드하도록 연결했다.
- 선택한 던전의 방 개수와 시드 보정값을 DungeonGenerator에 적용했다.
- 선택한 던전의 몬스터 배율을 MonsterSpawnManager에 적용했다.
- 선택한 던전의 함정 배율을 TrapSpawnManager에 적용했다.
- 선택한 던전의 보상 배율을 TreasureSpawnManager에 적용했다.

### 8. 던전 선택 화면 표시 시점

- VillageDungeonSelectionUI를 OnGUI 방식으로 구현했다.
- 마을에 처음 도착했을 때는 던전 선택 화면이 표시되지 않도록 수정했다.
- LockSelection과 UnlockSelection을 추가하여 화면 표시와 입력을 명시적으로 관리했다.
- 마차가 이동하는 동안에도 던전 선택 화면을 숨겼다.
- 마차가 25m 이동해 목적지에 도착한 뒤에만 선택 화면을 활성화했다.
- 선택 화면이 열리면 플레이어 입력을 차단하고 마우스 커서를 표시했다.
- UI GameObject와 컴포넌트 활성 상태가 잘못되어 화면이 나오지 않던 문제를 수정했다.

### 9. 기존 출발 UI 정리

- 기존 VillageCampaignFlowUI의 F5 즉시 출발 방식을 비활성화했다.
- 기존 UI와 새로운 마차 출발 UI가 동시에 표시되던 문제를 해결했다.
- 던전 출발 흐름을 다음 순서로 통일했다.

1. 던전 종료
2. 결과 화면 10초 표시
3. Village Scene 자동 이동
4. 마차 Floor 위 플레이어 배치
5. 마을 정산
6. 마을 자유 탐색
7. 출발 레버 상호작용
8. 마차 이동
9. 던전 선택 화면 표시
10. 선택한 던전으로 이동

## 주요 Hierarchy

- VillageEnvironment
  - Ground
  - Roads
    - MainRoad
    - DepartureRoad
  - Buildings
  - VillageWagon
    - CartBody
    - WagonFloor
    - PassengerSpawn
    - DepartureLever
      - LeverBase
      - LeverHandle
    - Wheel_FL
    - Wheel_FR
    - Wheel_BL
    - Wheel_BR
- VillageSystems
  - PersistentManagers
    - CampaignManager
    - RunResultManager
    - DungeonSelectionManager
  - VillagePlayerSpawner
  - VillageSettlementUI
  - VillageDungeonSelectionUI

## 주요 설정값

- 마을 자동 이동 대기시간: 10초
- 마차 이동속도: 초당 4m
- 마차 이동거리: 25m
- 마차 이동 방향: Local Position 기준 (0, 0, -1)
- 마차 시작 Position: (0, 0, 16)
- 마차 도착 Position: (0, 0, 41)
- PassengerSpawn Local Position: (0, 3.05, 0)
- PassengerSpawn Local Rotation: (0, 180, 0)

## 문제 해결

### CampaignManager 누락

- Village Scene 직접 실행 시 CampaignManager를 찾지 못하는 오류가 발생했다.
- Village Scene에도 CampaignManager 프리팹을 배치했다.
- 싱글톤 처리로 Dungeon에서 유지된 기존 인스턴스와의 중복을 방지했다.

### 던전 선택 화면 조기 표시

- VillageDungeonSelectionUI가 Scene 시작부터 OnGUI를 실행해 선택 화면이 표시됐다.
- selectionUnlocked 상태와 LockSelection을 추가하여 마차 도착 전 화면을 숨겼다.
- 기존 VillageCampaignFlowUI를 비활성화하여 F5 출발 안내가 중복 표시되지 않도록 수정했다.

### 던전 선택 화면 미표시

- UI 컴포넌트 자체가 비활성화되어 UnlockSelection 이후에도 OnGUI가 실행되지 않는 문제가 발생했다.
- UI GameObject와 컴포넌트는 활성 상태로 유지하도록 변경했다.
- 화면 표시 여부는 selectionUnlocked 상태만으로 제어하도록 수정했다.
- 마차 도착 시 UnlockSelection을 호출하여 선택 화면과 마우스 커서를 활성화했다.

## 최종 테스트 결과

- 던전 종료 후 결과 화면이 10초 동안 정상 표시됐다.
- F6 입력 없이 Village Scene으로 자동 이동했다.
- 플레이어가 마차 Floor 위에 정상 배치됐다.
- 정산 중 플레이어 입력이 정상적으로 차단됐다.
- 정산 완료 후 마을을 자유롭게 둘러볼 수 있었다.
- 출발 레버가 E 상호작용으로 정상 작동했다.
- 마차와 플레이어가 함께 이동했다.
- 마차 이동 전과 이동 중에는 던전 선택 화면이 표시되지 않았다.
- 마차가 지정된 거리에 도착한 뒤에만 던전 선택 화면이 표시됐다.
- 선택한 던전의 방 개수와 난이도 및 보상 배율이 정상적으로 적용됐다.
- Console에서 컴파일 오류와 주요 참조 오류가 발생하지 않았다.