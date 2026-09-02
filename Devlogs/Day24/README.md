# Project I 개발 일지

## Day 24 — Persistent 마차·Additive 맵 전환·Cargo 보존 및 일차 복구 시스템 구축

- 날짜: 2026-09-02
- 개발 단계: Phase 5 — 원정 루프·마차·아이템 영속성·저장 복구
- 마무리 전 기준 커밋: `25ab7abc64c78f6a4347c348cca271f5e3662768`
- 기준 커밋 메시지: `24`
- Persistent 씬: `Assets/ProjectI/Scenes/00_WagonPersistent.unity`
- 사무소 환경 씬: `Assets/ProjectI/Scenes/01_Office.unity`
- 테스트 던전 환경 씬: `Assets/ProjectI/Scenes/02_TestDungeon.unity`
- 공통 마차 프리팹: `Assets/ProjectI/Prefabs/Wagon/Wagon.prefab`

---

## 개발 목표

Day23에서는 사무소 테스트맵과 마차, 회수품 보관·판매·채무 시스템까지 연결했다.

Day24에서는 이후 여러 탐사 지역을 같은 방식으로 확장할 수 있도록
플레이어와 마차를 환경 맵과 분리하고,
사무소와 던전을 필요할 때 불러오는 구조로 전환했다.

또한 마차에 실은 실제 아이템이 환경 맵 교체 중 사라지지 않도록
실제 `WorldItem` GameObject를 유지하는 Cargo 보존 구조를 추가하고,
던전 진행 중 오류나 강제 종료가 발생하더라도
마지막 정상 사무소 상태로 돌아갈 수 있는 일차 체크포인트·복구 시스템을 구성했다.

핵심 흐름은 다음과 같다.

```text
00_WagonPersistent
├─ Player
├─ Wagon
├─ CargoArea
├─ Travel Bell
└─ PersistentMapLoader

        +

01_Office 또는 02_TestDungeon
```

환경 이동 시 Player와 Wagon은 유지하고
Office와 Dungeon 중 하나만 Additive 방식으로 교체한다.

---

# 1. 아이템 드롭 물리 개선

아이템을 내려놓을 때 들고 있던 방향을 최대한 유지하도록
`PlayerCarryController`의 드롭 처리를 보완했다.

아이템의 안정화 방식은 다음 세 종류로 분리했다.

```text
Free
→ 일반 Rigidbody 회전 사용

Upright
→ X/Z 회전 고정
→ Y 회전 허용

FixedPose
→ X/Y/Z 회전 고정
```

회수품처럼 세워져 있어야 하는 물체는 `Upright`를 사용할 수 있고,
무기처럼 자유 회전이 필요한 물체는 `Free`를 사용할 수 있다.

투척 시에는 안정화 제한보다 실제 물리 반응을 우선한다.

주요 파일:

```text
Assets/ProjectI/Scripts/Items/ItemStabilityMode.cs
Assets/ProjectI/Scripts/Items/WorldItemDropProfile.cs
Assets/ProjectI/Scripts/Items/PlayerCarryController.cs
```

---

# 2. Wagon CargoArea 확대

마차의 실제 적재·회수 판정 범위를 확대했다.

설정값:

```text
Position = (0, 2.55, -1.25)
Size     = (3.65, 2.60, 9.50)
```

기존 `WagonCargoArea`의 회수품과 사망 플레이어 판정 구조는 유지한다.

따라서 앞으로도 동일 CargoArea를 기준으로
회수품 확보, 사망 플레이어 회수, 원정 귀환 판정을 이어갈 수 있다.

---

# 3. 마차 이동 종 추가

Wagon Prefab에 이동용 벽걸이 종을 추가했다.

구조 예시:

```text
Wagon
└─ Day24_WagonTravelBell
   ├─ BellPivot
   └─ Rope
```

플레이어가 F로 종을 사용하면
`TravelRequested` 이벤트를 발생시키고
종과 줄에 간단한 흔들림 연출을 재생한다.

주요 파일:

```text
Assets/ProjectI/Scripts/Loop/WagonTravelBellInteractable.cs
Assets/ProjectI/Prefabs/Wagon/Wagon.prefab
```

---

# 4. 씬 구조를 Persistent + Environment 방식으로 분리

Day23까지는 `ExplorationOffice`에 플레이어와 마차, 환경이 함께 존재했다.

Day24부터는 역할을 세 씬으로 분리했다.

```text
00_WagonPersistent
→ 계속 유지되는 런타임 씬

01_Office
→ 사무소 환경 전용 씬

02_TestDungeon
→ 테스트 던전 환경 전용 씬
```

`00_WagonPersistent`에는 다음 핵심 객체가 존재한다.

```text
Player
Wagon
CargoArea
Travel Bell
PersistentMapLoader
Fade UI
DailySnapshotService
```

반면 `01_Office`, `02_TestDungeon`에는
각 지역의 환경과 마차 진입·정차 지점만 둔다.

---

# 5. Office와 TestDungeon Additive 로드

게임 시작 시 기본 구성:

```text
00_WagonPersistent
+
01_Office
```

던전 이동:

```text
마차 종 사용
↓
화면 암전
↓
02_TestDungeon Additive Load
↓
던전 WagonEntryPoint 배치
↓
01_Office Unload
↓
마차 진입 이동
↓
화면 Fade In
```

귀환은 반대로 처리한다.

```text
00_WagonPersistent 유지
+
02_TestDungeon 제거
+
01_Office Additive Load
```

중요한 점은 Player와 Wagon을 매번 새로 생성하지 않는 것이다.

환경만 교체하므로
마차와 플레이어를 이후 여러 탐사 지역에서 공통으로 사용할 수 있다.

주요 파일:

```text
Assets/ProjectI/Scripts/Loop/PersistentMapLoader.cs
Assets/ProjectI/Scripts/Loop/MapTravelAnchor.cs
Assets/ProjectI/Scripts/Loop/TravelDestination.cs
Assets/ProjectI/Scripts/Core/SceneFlowManager.cs
```

---

# 6. TestDungeon 테스트 환경

Additive 환경 전환을 검증하기 위한
간단한 `02_TestDungeon` 테스트맵을 추가했다.

테스트 던전에는 다음 요소를 둔다.

```text
Stone Floor
Stone Wall
Metal 계열 재질
복도
메인 공간
기둥
조명
WagonEntryPoint
WagonStopPoint
```

이번 단계의 목적은 최종 던전 아트 제작이 아니라
Persistent 마차와 환경 교체 구조를 검증할 수 있는 최소 테스트 환경을 확보하는 것이다.

---

# 7. 실제 Cargo GameObject 보존

이전 시도처럼 마차 적재물을
데이터로 변환한 뒤 Destroy하고 목적지에서 다시 Instantiate하지 않는다.

Day24의 정상 이동 구조:

```text
실제 WorldItem
↓
CargoArea 내부 판정
↓
현재 GameObject 참조 확보
↓
00_WagonPersistent 씬으로 소속 이동
↓
이동 중 Rigidbody 잠금
↓
마차와 함께 Transform 동기화
↓
도착
↓
기존 Rigidbody 상태 복구
```

즉 일반적인 Office ↔ Dungeon 이동에서는 다음을 사용하지 않는다.

```text
Destroy 후 재생성
ItemInstanceData를 이용한 여행 복원
ItemFactory를 이용한 여행 Spawn
```

동일한 실제 `WorldItem`과 `Rigidbody`를 유지한다.

주요 파일:

```text
Assets/ProjectI/Scripts/Loop/WagonCargoPersistence.cs
```

---

# 8. 마차 밖에 내려놓은 아이템 처리

마차 Cargo로 Persistent 씬에 들어간 아이템이라도
도착 후 플레이어가 마차 밖 환경에 내려놓으면
현재 활성 환경 씬 소속으로 되돌린다.

예시:

```text
마차 내부
→ 00_WagonPersistent

Dungeon 바닥에 내려놓음
→ 02_TestDungeon

Office 바닥에 내려놓음
→ 01_Office
```

따라서 마차 밖에 버린 물체까지
다음 원정으로 무조건 따라오는 문제를 방지한다.

---

# 9. ItemDefinition과 ItemInstanceData

아이템 데이터 구조를 추가했다.

```text
ItemDefinition
├─ ItemId
├─ DisplayName
└─ RecoveryPrefab

ItemInstanceData
├─ InstanceId
├─ ItemId
├─ Value
├─ Location
├─ SlotIndex
├─ StorageKey
├─ Position
└─ Rotation
```

이 구조는 일반적인 씬 이동을 위한 것이 아니다.

사용 목적:

```text
일차 체크포인트 저장
↓
데이터 손상 또는 강제 종료
↓
이전 정상 상태 복구
```

즉 Cargo 이동은 실제 GameObject 유지,
장기 복구는 ItemDefinition / ItemInstanceData를 사용한다.

---

# 10. Daily Snapshot 저장 구조

저장 데이터는 다음 구조를 사용한다.

```text
ProjectI/Saves
├─ Current
│  └─ current.json
└─ DailySnapshots
   ├─ Day_001.json
   ├─ Day_002.json
   └─ ...
```

`Current`는 현재 안전한 사무소 상태를 갱신하는 파일이다.

`DailySnapshots`는 완료한 일차를 보관하는 복구용 백업이다.

각 파일은 실제 Snapshot JSON과 함께 SHA-256 체크섬을 저장한다.

```text
DailySnapshotEnvelope
├─ schemaVersion
├─ checksum
└─ payloadJson
```

로드할 때 체크섬을 다시 계산해
파일이 정상인지 확인한다.

주요 파일:

```text
Assets/ProjectI/Scripts/Persistence/DailySnapshotData.cs
Assets/ProjectI/Scripts/Persistence/DailySnapshotStore.cs
Assets/ProjectI/Scripts/Persistence/DailySnapshotService.cs
Assets/ProjectI/Scripts/Persistence/Day23SnapshotBridge.cs
```

---

# 11. 사무소를 안전 체크포인트로 사용

Day24 마무리에서 저장 규칙을 단순화했다.

```text
Office
→ 안전 저장 가능

Dungeon
→ 진행 중 자동 저장 금지
```

사무소에 머무는 동안 `Current`를 주기적으로 갱신한다.

또한 던전으로 출발하기 직전에
마지막 사무소 상태를 한 번 더 강제 저장한다.

예시:

```text
5일차 던전 클리어
↓
Office 귀환
↓
5일차 완료 상태 저장
↓
6일차 Office 준비
↓
장비·보관품·마차 적재 상태 자동 저장
↓
6일차 던전 출발 직전 강제 저장
↓
6일차 Dungeon 시작
```

Dungeon에서 오류 또는 강제 종료가 발생해도
Dungeon 진행 상태로 `Current`를 덮어쓰지 않는다.

---

# 12. 오류·튕김 시 복구 규칙

6일차 Dungeon 진행 중 오류가 발생한 예시:

```text
6일차 Dungeon
↓
오류 / 강제 종료
↓
재실행
↓
마지막 정상 Office Current 확인
↓
6일차 출발 전 Office 상태 복원
```

`Current` 자체가 손상됐다면
가장 최근 정상 완료 일차 Snapshot을 사용한다.

```text
Current 손상
↓
Day_005 정상 백업 검색
↓
Day 5 완료 상태 복원
↓
Office에서 Day 6 다시 시작
```

진행 중이던 6일차 Dungeon은
없었던 원정으로 처리한다.

---

# 13. 복구 실패 시 기존 아이템 보호

Snapshot 복구 시 환경 준비보다 아이템 삭제가 먼저 실행되면
Office 로드 실패 상황에서 현재 아이템까지 잃을 수 있다.

이를 방지하기 위해 순서를 다음과 같이 변경했다.

```text
Office Additive Load
↓
WagonStopPoint 확인
↓
복구 환경 준비 성공 확인
↓
성공한 경우에만 기존 WorldItem 정리
↓
Snapshot 아이템 복원
```

복구 환경 준비 실패:

```text
Office 로드 실패
또는
WagonStopPoint 누락
↓
복구 취소
↓
기존 WorldItem 유지
```

따라서 파괴적인 복구 처리는
안전 환경 준비가 성공한 뒤에만 시작한다.

---

# 14. 일차 완료 저장 순서 보정

일차를 증가시키기 전에
다음 일차의 Office `Current` 저장 성공을 확인한다.

정상 순서:

```text
Day 5 완료 상태 캡처
↓
Day_005.json 확인 또는 생성
↓
Day 6 Office Current 생성
↓
Current 저장 성공 확인
↓
currentDay = 6
```

Day 6 Current 저장이 실패하면:

```text
currentDay 증가 안 함
↓
Office 상태 유지
↓
저장 재시도 가능
```

이미 정상 `Day_005.json`이 존재한다면
재시도 시 해당 백업을 정상 완료 상태로 다시 사용할 수 있다.

---

# 15. 서버 백업 확장 지점

현재 Day24에서는 실제 서버 연동을 구현하지 않았다.

대신 다음 인터페이스를 준비했다.

```text
IRemoteDailySnapshotStore
├─ Upload
└─ TryDownload
```

향후 서버 또는 클라우드를 연결하면
로컬에서 생성한 동일 Snapshot Envelope를 업로드하는 방식으로 확장할 수 있다.

현재 상태:

```text
로컬 Current 저장        구현
일차 Snapshot 저장       구현
SHA-256 검증             구현
이전 정상 일차 복구      구현
실제 원격 서버 업로드     미구현
```

---

# 16. Day24 최종 플레이 흐름

```text
게임 시작
↓
00_WagonPersistent
+
01_Office
↓
사무소 안전 상태 자동 저장
↓
던전 선택 / 출발 준비
↓
출발 직전 Office Current 저장
↓
마차 종 사용
↓
02_TestDungeon Additive Load
↓
01_Office Unload
↓
Persistent Player + Wagon + Cargo 유지
↓
던전 플레이
↓
귀환
↓
01_Office Additive Load
↓
02_TestDungeon Unload
↓
사무소 안전 상태 갱신
↓
일차 완료
↓
불변 Day_N Snapshot 생성
↓
다음 일차 Office 시작
```

---

# 17. Day24 주요 생성·수정 항목

주요 신규 스크립트:

```text
Assets/ProjectI/Scripts/Items/ItemDefinition.cs
Assets/ProjectI/Scripts/Items/ItemFactory.cs
Assets/ProjectI/Scripts/Items/ItemInstanceData.cs
Assets/ProjectI/Scripts/Items/ItemRegistry.cs
Assets/ProjectI/Scripts/Items/ItemStabilityMode.cs
Assets/ProjectI/Scripts/Items/WorldItemDropProfile.cs
Assets/ProjectI/Scripts/Items/WorldItemIdentity.cs
Assets/ProjectI/Scripts/Loop/MapTravelAnchor.cs
Assets/ProjectI/Scripts/Loop/PersistentMapLoader.cs
Assets/ProjectI/Scripts/Loop/TravelDestination.cs
Assets/ProjectI/Scripts/Loop/WagonCargoPersistence.cs
Assets/ProjectI/Scripts/Loop/WagonTravelBellInteractable.cs
Assets/ProjectI/Scripts/Persistence/DailySnapshotData.cs
Assets/ProjectI/Scripts/Persistence/DailySnapshotService.cs
Assets/ProjectI/Scripts/Persistence/DailySnapshotStore.cs
Assets/ProjectI/Scripts/Persistence/Day23SnapshotBridge.cs
Assets/ProjectI/Scripts/Persistence/IRemoteDailySnapshotStore.cs
```

주요 생성 씬:

```text
Assets/ProjectI/Scenes/00_WagonPersistent.unity
Assets/ProjectI/Scenes/01_Office.unity
Assets/ProjectI/Scenes/02_TestDungeon.unity
```

주요 수정 항목:

```text
Assets/ProjectI/Prefabs/Wagon/Wagon.prefab
Assets/ProjectI/Scripts/Core/SceneFlowManager.cs
Assets/ProjectI/Scripts/Items/PlayerCarryController.cs
```

---

# 18. 검증 상태

Day24 마무리 시 코드 구조상 다음 항목을 정적 확인했다.

```text
Persistent + Environment 씬 분리
Office/TestDungeon Additive 전환
여행 중 Cargo 실제 GameObject 유지
Dungeon 중 Current 저장 차단
Office Current 자동 갱신
던전 출발 전 Office 강제 저장
Current 저장 성공 전 currentDay 증가 차단
Office 복구 준비 성공 전 WorldItem 삭제 차단
SHA-256 Snapshot 검증
최근 정상 완료 일차 폴백
```

다만 ChatGPT 작업 환경에서는 Unity Editor를 직접 실행할 수 없으므로
실제 Unity 컴파일과 Play Mode 동작 검증은 별도로 수행해야 한다.

---

# 19. 다음 일차에서 확인할 항목

Day25에서 우선 확인할 내용:

```text
1. Unity 컴파일 오류 유무
2. Office → TestDungeon → Office 왕복 반복 테스트
3. 마차 Cargo 여러 개 적재 상태 왕복 테스트
4. Dungeon 강제 종료 후 Office 체크포인트 복구 테스트
5. 손상된 Current 파일에서 Day_N Snapshot 폴백 테스트
6. 판매·보관·채무 시스템 회귀 테스트
7. 일차 완료 후 다음 일차 Office 시작 테스트
```

추후 개선 후보:

```text
ItemId를 DisplayName 기반 LEGACY ID에서 명시적 고정 ID로 정리
Player Health/Stamina 등 Snapshot 범위 확장 여부 결정
Editor 자동 마이그레이션 도구 정리
실제 원격 Snapshot 저장소 연결
```

---

## Day 24 결과

Day24에서는 기존 사무소 중심 구조를
`Persistent Player/Wagon + Additive Environment` 구조로 전환했다.

마차의 실제 Cargo를 환경 맵 교체와 분리했으며,
일반 여행 중에는 실제 GameObject 자체를 유지하도록 했다.

또한 Dungeon 진행 데이터를 복구 기준으로 사용하지 않고
Office를 안전 체크포인트로 지정했다.

이제 원정 도중 오류가 발생해도
마지막 정상 사무소 상태 또는 최근 완료 일차 Snapshot을 기준으로
다음 원정을 다시 시작할 수 있는 기반이 마련되었다.
