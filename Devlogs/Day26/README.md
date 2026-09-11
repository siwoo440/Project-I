# Project I 개발 일지

## Day 26 — 일차 루프·하루 1회 원정·고정 ItemId·Terrain 테스트 던전 및 Persistent HUD 정리

- 날짜: 2026-09-12
- 개발 단계: Phase 5 마무리 — 일차 루프·데이터 정리
- 기준 커밋: `2015c0d` (25일차)
- 검증 환경: Unity 6000.3.21f1 배치 모드 + Play Mode 자동 검증 하네스 (프로젝트 복사본, 별도 저장 경로)
- 테스트 시작 씬: `00_WagonPersistent` → `01_Office`

---

## 개발 목표

Day25까지는 원정을 한 번 다녀오는 흐름은 완성됐지만
게임 안에서 하루를 끝내고 다음 날로 넘어갈 방법이 없었고,
같은 날 던전을 몇 번이든 다녀올 수 있었다.

Day26에서는 설계 문서의 하루 흐름을 게임 안에 연결했다.

```text
사무소 준비
↓
마차 종 (하루 1회 출발)
↓
던전 탐사·회수
↓
마차 종으로 귀환 → 원정 결과
↓
판매·보관·채무 상환
↓
일차 마감 장부
↓
다음 날 사무소 준비
```

함께 다음 뒷정리를 진행했다.

```text
에디터 자동 실행 훅 정리
LEGACY_표시이름 ItemId → 고정 ItemId
Terrain 테스트 던전과 회수품 배치
플레이어 HUD·F1 창을 Persistent 씬으로 이동
```

---

# 1. 에디터 자동 훅 정리

## 증상

Unity를 열 때마다 Build Settings가 옛 목록으로 되돌아가

```text
[Project I] Build Settings에 씬이 없습니다: 00_WagonPersistent
```

오류가 발생했다.

## 원인

2~24일차 Setup 스크립트 32개가 모두 `[InitializeOnLoad]`로
컴파일할 때마다 자동 실행되고 있었다.

그중 두 도구가 서로 반대로 설정을 덮어썼다.

```text
2일차 Phase1Day2Finalize
→ Build Settings = Boot / MainMenu / ExplorationOffice 로 되돌리고 파일 저장
→ Play 시작 씬 = Boot

24일차 Step2
→ Build Settings에 00 / 01 / 02 추가 (메모리에서만)
```

24일차 커밋에 씬 목록이 빠져 있던 것도 같은 원인이었다.

## 수정

```text
32개 Setup 스크립트의 자동 실행 제거
→ Tools > Project I 메뉴 수동 실행은 유지

Phase1Day2Finalize / Phase1Validator 필수 씬 목록
→ Boot / MainMenu / 00_WagonPersistent / 01_Office / 02_TestDungeon

Play 시작 씬
→ 00_WagonPersistent (에디터 메모리 설정만 변경)
```

Step4 도구가 다시 실행돼도 고정 ItemId를 LEGACY로 되돌리지 않도록
기존 정의를 별칭으로 먼저 찾게 했다.

---

# 2. 일차 원정 단계 저장

하루 안의 원정 단계를 저장 데이터에 추가했다.

```text
ExpeditionDayPhase
├─ OfficePrep    사무소 준비 (출발 가능)
├─ OnExpedition  원정 중 (저장하지 않는 런타임 단계)
└─ Returned      귀환 완료 (일차 마감 전 재출발 불가)
```

`DailySnapshotData.schemaVersion`을 2로 올렸다.
버전 1 저장 파일은 `OfficePrep`으로 해석한다.

저장 규칙:

```text
출발 직전 강제 저장 → OfficePrep
→ 던전에서 튕기면 출발 전 사무소로 복구

귀환 직후 저장 → Returned
→ 재시작해도 같은 날 다시 출발할 수 없음

일차 마감 → Day_N(Returned) 기록 후 다음 날 Current(OfficePrep)
```

---

# 3. 하루 1회 원정과 탑승 판정

마차 종은 출발할 수 없는 이유를 안내 문구로 보여준다.

```text
마차 창고 안에 탑승한 뒤 종을 울리세요
오늘 원정 완료 — 일차 마감 장부에서 N일차를 마감하세요
사무소 기록 정리 중 — 잠시 후 다시 시도하세요
```

탑승 판정은 플레이어 몸통 위치가 마차 적재칸 박스(+0.4m) 안에 있는지로 확인한다.
던전에서 사무소로 돌아가는 귀환은 원정 단계와 관계없이 항상 허용한다.

주요 파일:

```text
Assets/ProjectI/Scripts/Persistence/ExpeditionDayPhase.cs
Assets/ProjectI/Scripts/Persistence/DailySnapshotData.cs
Assets/ProjectI/Scripts/Persistence/DailySnapshotService.cs
Assets/ProjectI/Scripts/Loop/PersistentMapLoader.cs
Assets/ProjectI/Scripts/Loop/WagonTravelBellInteractable.cs
```

---

# 4. 일차 마감 장부

사무소 채무 장부 왼쪽에 일차 마감 장부를 배치했다.

```text
Day26_DayEndLedger
├─ Desk
├─ Book
└─ Candle
```

F를 길게 누르면 `Returned` 단계일 때만 `CompleteCurrentDay()`를 호출한다.
원정 전에는 "원정을 다녀온 뒤 마감할 수 있습니다"를 표시한다.

배치 위치는 채무 장부 주변 후보를 겹침 검사해 빈 자리를 찾는다.
처음에는 바닥 높이를 장부 크기로 추정해 모든 후보가 바닥과 겹쳐 거부됐고,
레이캐스트로 실제 바닥 높이를 측정하도록 수정했다.

주요 파일:

```text
Assets/ProjectI/Scripts/Loop/DayEndLedgerInteractable.cs
Assets/ProjectI/Editor/Phase5Day26TestTerrainSetup.cs
```

---

# 5. 원정 결과

출발할 때 마차 적재칸과 플레이어 소지품을 기록하고,
귀환할 때 다시 비교한다.

```text
귀환한 물건 수
신규 회수품 수와 가치 합계
가져갔다가 두고 온 장비 수
```

화면 상단에 12초 동안 표시하며, 평소에는 `N일차 · 단계`를 표시한다.

22일차 `ExpeditionOutcomeController`는 모든 WorldItem을 검사하므로
사무소 보관 중인 아이템까지 손실로 처리할 위험이 있어
귀환 결과는 별도 `ExpeditionReportTracker`로 계산했다.

주요 파일:

```text
Assets/ProjectI/Scripts/Loop/ExpeditionReportTracker.cs
```

---

# 6. 고정 ItemId

표시 이름 기반 `LEGACY_` ID를 바뀌지 않는 고정 ID로 교체했다.

| 표시 이름 | 고정 ItemId |
|---|---|
| 6연발 리볼버 | weapon.revolver_6shot |
| Iron Axe | weapon.iron_axe |
| Iron Sword | weapon.iron_sword |
| 검 | weapon.sword |
| 석궁 | weapon.crossbow |
| 곡괭이 | tool.pickaxe |
| 작은 도구 | tool.small_tool |
| 손전등 | light.flashlight |
| 열쇠 | key.basic |
| 회복 아이템 | consumable.healing |
| 신들의 조각상 | recoverable.gods_statue |
| 왕관 | recoverable.crown |
| 은 동전 | recoverable.silver_coin |
| 장인의 금속 장식 | recoverable.artisan_metal_ornament |
| 작은 회수품 | recoverable.small_relic |
| 시험용 검·곡괭이·랜턴·횃불 | test.sword / test.pickaxe / test.lantern / test.torch |

기존 ID는 `legacyIds`로 남겨 25일차 저장 파일도 그대로 불러오며,
다음 자동 저장부터 새 ID로 기록된다.
에셋 GUID는 유지했으므로 씬·프리팹 참조는 바뀌지 않는다.

`Tools > Project I > Day 26 > Validate Item Definitions`로
ID 누락·중복·LEGACY 사용·복구 Prefab을 검사한다.

주요 파일:

```text
Assets/ProjectI/Scripts/Items/ItemDefinition.cs
Assets/ProjectI/Scripts/Items/ItemRegistry.cs
Assets/ProjectI/Resources/Day24Recovery/Definitions/*.asset
```

---

# 7. Terrain 테스트 던전

`02_TestDungeon`을 64×64m Terrain 야외 맵으로 교체했다.
씬 GUID와 마차 진입·정차 좌표는 Day24와 동일하다.

```text
흙길 (마차 진입로·정차 구역 평탄화)
바위 경사 가장자리 언덕
왼쪽 언덕 / 오른쪽 움푹한 곳
바위 14개 · 나무 10그루
투명 경계벽
```

회수품 7개와 위치 표시용 발광 기둥:

```text
은 동전 250 / 300 / 400
장인의 금속 장식 650 / 900
신들의 조각상 1500
왕관 2250
```

TerrainData를 에셋으로 먼저 저장하지 않으면
지면 재질 맵이 저장되지 않아 전부 풀로 보이는 문제가 있어
저장 순서를 수정했다.

`Tools > Project I > Day 26 > Build Terrain Test Dungeon + Day-End Ledger`로 다시 생성할 수 있다.

---

# 8. Snapshot 복구 위치 버그 (Day24부터 존재)

## 증상

재시작 후 마차에 실어 둔 은화 2개가
마차가 아니라 같은 좌표(16.4, 0, 20)에 나타났다.

## 원인

복구 아이템은 복구 Prefab 원본 위치에 생성된 뒤 Transform만 저장 위치로 옮겼다.
복구 Prefab의 Rigidbody 보간(Interpolate)이 켜져 있어
물리 위치가 원본 좌표에 남은 채 Transform을 되돌렸다.

사무소 기본 아이템은 Prefab 원본 좌표가 원래 자리와 같아 우연히 가려져 있었다.

## 수정

```text
Transform 위치 지정
+ Rigidbody.position / rotation 지정
+ Physics.SyncTransforms()
```

사무소 아이템 보관 복귀(`OfficeWorldItemKeeper`)에도 같은 처리를 적용했고,
검증 하네스에 아이템 위치 비교(±0.35m)를 추가했다.

---

# 9. 밝기 센서 재연결

`PlayerBrightnessSensor`가 `Awake`에서 한 번만 `BrightnessManager`를 찾아
Persistent 플레이어는 밝기가 항상 0이었다.

관리자가 없으면 1초 간격으로 다시 찾도록 수정했다.
던전처럼 관리자가 없는 환경에서는 기본값(어둠)으로 표시한다.

---

# 10. 빠른 슬롯 HUD·F1 창 Persistent 이동

## 증상

```text
던전에서 빠른 슬롯 UI가 보이지 않음
아이템을 들어도 슬롯에 이름이 표시되지 않음
```

## 원인

24일차 씬 분리 때 `PlayerHUDCanvas`와 `DebugPageCanvas`가 `01_Office`에 남았다.
플레이어 Prefab의 `QuickSlotHud` 참조는 모두 끊겨 있어
사무소에 보이던 슬롯은 갱신되지 않는 빈 틀이었다.

## 수정

```text
PlayerHUDCanvas / DebugPageCanvas
→ 00_WagonPersistent로 이동
→ QuickSlotHud 슬롯 참조 재연결
```

## 새 던전 추가 시 재발 방지

```text
1. QuickSlotHud 자급화
   Persistent 씬에서 슬롯 UI를 찾지 못하면 직접 생성·연결

2. EnvironmentSceneGuard
   환경 씬 로드 직후 HUD·F1 창, 플레이어·마차·저장 시스템 복제본,
   중복 EventSystem·AudioListener를 비활성화하고 경고

3. Tools > Project I > Validate Environment Scenes
   Persistent 필수 구성과 모든 환경 씬 규칙 검사
   (환경 씬은 MapTravelAnchor 정확히 1개)
```

새 환경 씬에는 지형·조명·오브젝트·회수품과 마차 진입·정차 지점만 둔다.

주요 파일:

```text
Assets/ProjectI/Scripts/Items/QuickSlotHud.cs
Assets/ProjectI/Scripts/Loop/EnvironmentSceneGuard.cs
Assets/ProjectI/Editor/ProjectIEnvironmentSceneValidator.cs
```

---

# 11. 검증 결과

| 검증 단계 | 내용 | 결과 |
|---|---|---|
| H | 빠른 슬롯 HUD 사무소·던전·귀환 (자급화 전후 모두) | 22 / 0 |
| F1 | 하루 흐름·판매·재출발 거부·일차 마감·Day 2 강제 종료 | 52 / 0 |
| F2 | Day 2 강제 종료 복구 (위치 포함) → 원정 → 귀환 | 26 / 0 |
| F3 | 귀환 후 재시작 → 재출발 거부 → Day 3 | 5 / 0 |
| A | 25일차 회귀 (매 왕복 후 일차 마감) | 137 / 0 |
| B·C | 강제 종료 복구 · 재시작 | 53 / 0 |
| D·E | Current 손상 폴백 · 던전 중 롤백 | 37 / 0 |
| L | 25일차 LEGACY 저장 파일 로드 호환 | 5 / 0 |

```text
PASS 337 / FAIL 0
```

원본 프로젝트에서도 컴파일 성공, ItemDefinition 검증 오류 0건,
환경 씬 검증 오류 0건, Unity 실행 후 Build Settings 변동 없음을 확인했다.

---

# 12. 남은 문제

```text
1. 던전 사망 시 부활 수단 없음 (PlayerDeathController에 되살리기 기능 없음)
2. GameTimeController·BrightnessManager는 아직 사무소 씬에만 존재
3. TravelDestination·씬 이름이 코드에 고정되어 새 던전 추가 시 코드 수정 필요
4. 22일차 원정 결과 단말은 사무소에 남아 있음 (귀환 결과는 ExpeditionReportTracker 사용)
```

---

## Day 26 결과

게임 안에서 하루를 끝내고 다음 날로 넘어가는 일차 루프를 연결했다.

하루 1회 원정, 탑승 판정, 원정 결과, 일차 마감이 저장·복구 규칙과 함께 동작하며,
아이템은 고정 ItemId로 저장되어 이전 저장 파일과도 호환된다.

Terrain 테스트 던전에서 회수품을 가져와 판매하는 전체 흐름을 직접 확인할 수 있고,
플레이어 HUD와 F1 창은 Persistent 씬으로 옮겨 새 던전이 추가돼도 사라지지 않도록 대비했다.

다음 단계는 Phase 8 절차적 던전 생성이다.
