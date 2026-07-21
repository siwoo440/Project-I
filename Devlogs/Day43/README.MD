# 43일차 개발 일지

## 개발 목표

- 던전별 전용 Scene 구성
- 고정 외부 맵 제작을 위한 Terrain 기반 마련
- 외부 주요 지점과 이동 경계 구성

## 구현 내용

### 1. 던전별 전용 Scene 구성

기존 Dungeon Scene을 기준으로 다음 전용 Scene을 구성했다.

- 폐허 지하묘지: Dungeon_Catacombs
- 버려진 광산: Dungeon_Mine
- 침수된 성채: Dungeon_SunkenKeep

각 DungeonRouteData의 Scene Name을 전용 Scene 이름으로 변경하고 Build Profiles에 등록했다.

### 2. 외부 Terrain 생성

각 던전 Scene에 독립적인 Terrain을 생성했다.

- Terrain_Catacombs
- Terrain_Mine
- Terrain_SunkenKeep

각 Terrain은 서로 다른 Terrain Data를 사용하도록 구성했다.

Terrain의 높이 조형은 이번 작업에서 진행하지 않았으며 평평한 기본 지형 상태로 유지했다.

### 3. 외부 맵 Hierarchy 구성

각 던전 Scene에 ExteriorRoot를 추가하고 다음 구조를 구성했다.

- Terrain_Exterior
- ExteriorMarkers
- ExteriorBoundary

### 4. 외부 주요 지점 구성

외부 맵 제작과 이후 기능 연결을 위해 다음 지점을 배치했다.

- CarriageArrivalPoint
- PlayerExteriorSpawn
- MainEntrancePoint
- SubEntrancePoint

### 5. 외부 이동 경계 구성

Terrain 외곽에 보이지 않는 Box Collider 경계를 배치했다.

- Boundary_North
- Boundary_South
- Boundary_East
- Boundary_West

### 6. 던전 자동 생성 임시 중지

외부 맵 작업 중 기존 내부 던전이 자동으로 생성되지 않도록 각 전용 Scene의 DungeonGenerator 컴포넌트를 임시 비활성화했다.

## 현재 상태

- 던전별 전용 Scene 구성 완료
- 던전별 독립 Terrain 생성 완료
- 외부 주요 지점 구성 완료
- 외부 경계 구성 완료
- Terrain 높이 조형 미진행
- 외부 플레이어 배치 미구현
- 메인·서브 입구 기능 미구현
- 내부 던전 연결 미구현

## 다음 개발 방향

- 공용 Carriage Prefab 배치
- 플레이어 외부 시작 위치 적용
- 메인·서브 입구 임시 오브젝트 제작
- InteriorRoot와 Room_Start 구성
- 입구 진입 시 절차적 내부 던전 생성