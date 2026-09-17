# 38일차 — 협동 아이템·마차·경제 동기화 (방장 기준 · 개별 ID)

## 이번 일차 목표

37일차에는 서로의 모습만 보였고 **물건과 돈은 각자 따로**였다. 38일차에는 모든 대원이 같은 물건과 같은 공동 자금을 보게 만든다.

```text
대원 A가 회수품을 주움 → 방장 판정 → 모두의 화면에서 바닥에서 사라지고 A의 손에 보임
A가 마차 창고에 내려놓음 → 모두에게 같은 자리 (마차 기준 좌표) → 함께 출발·도착
참가자가 판매 벨 → 방장이 판매 → 모두의 판매대에서 사라짐 · 공동 자금 동일
```

## 1. 기준 — 개별 ID와 방장 판정

- 모든 `WorldItem`에 이미 있는 `WorldItemIdentity.InstanceId`로 같은 물건을 찾는다. 새 네트워크 오브젝트를 만들지 않는다.
- 방장은 "아이템 → 가진 대원" 표를 가진다. 참가자는 먼저 자기 화면에 반영하고 방장에게 알리며, 충돌이 나면 방장이 되돌린다.
- 받은 변경을 적용하는 동안에는 다시 알리지 않는다 (`NetItemSync.Applying`).
- 혼자 하기에서는 모든 알림이 바로 끝난다 (`NetItemSync.Active` = false).

## 2. 아이템 변경 — `NetItemSync`

| 변경 | 알리는 곳 | 다른 대원 화면 |
| --- | --- | --- |
| 줍기 | `PlayerInventory.TryPickup` | 보관함·단상에서 빼고 그 대원 몸체에 붙임. 늦게 요청한 대원은 되돌린다("다른 원정대원이 먼저 가져갔습니다"). |
| 꺼내기 | `TryReceiveStoredItem` | 줍기와 같음 |
| 손에 든 아이템 | `RefreshSelectedItem` (바뀔 때만) | 몸체 **손**에 실제 모델, 나머지 소지품은 **주머니**에 숨김 |
| 내려놓기·던지기 | `PlayerCarryController.Drop/Throw` | 위치·회전·속도 적용. 마차 창고 안이면 **마차 기준 좌표** |
| 사망 흩뿌리기 | `PlayerDeathController` | 흩뿌린 위치로 다시 알림 |
| 판매대 올리기 | `OfficeSaleCounter.Interact` | 내려놓기와 같음 |
| 마차 보관함 | `WagonSharedStorage` | `AcceptNetworkItem` / `ReleaseNetworkItem` |
| 보관 단상 | `OfficeStoragePedestal` (경로 전달) | 같은 경로 단상에 올림 / `ReleaseNetworkItem` |
| 열쇠 소모 | `DungeonDoor` · `LockedRoomDoor` | 제거 |
| 대원 퇴장 | `NetPlayerAvatar.OnNetworkDespawn` | 그 대원이 가진 물건을 모든 화면에서 그 자리에 떨어뜨림 |

- 다른 대원 몸체 아래에 있던 물건을 내려놓으면, 로컬에서 내려놓을 때와 같은 씬으로 옮긴다. 마차 밖 물건은 기존 `WagonCargoPersistence`가 맵 씬으로 옮긴다.
- 대원이 다른 맵에 있어 몸체가 숨겨지면 손에 든 물건도 함께 숨긴다 (크기 0).

## 3. 전체 목록 맞추기

- 참가자가 방장과 **같은 맵에 도착**하거나, 방장이 **하루 마감·복구**를 하면(`DailySnapshotService.OfficeStateReloaded` → 재동기화 번호 증가) 방장에게 목록을 요청한다.
- 방장은 활성 아이템을 모두 모아 보낸다. 보관 중인 사무소 물건, 판매된 물건, 하위 아이템은 뺀다.
  - 위치 종류: 바닥(마차 기준 여부) · 소지(대원 · 손) · 마차 보관함 · 단상(경로)
  - JSON으로 **10개씩 나눠** 보낸다.
- 참가자는 받은 목록으로 맞춘다.
  - 없으면 `ItemFactory.SpawnForRecovery`로 만든다.
  - 가치를 맞추고 위치 종류대로 배치한다.
  - 방장에게 없는 아이템은 지운다. 내 소지품과 보관 중인 물건은 지우지 않는다.
- **던전 회수품 ID 고정:** 같은 시드면 모든 대원이 같은 ID를 갖도록 `g{시드}-{순번}`으로 붙인다 (`ModuleDungeonGenerator`, `ProceduralInteriorGenerator`).

## 4. 경제는 방장이 처리

| 참가자 동작 | 처리 |
| --- | --- |
| 판매 창 [판매] | `SaleRequestRpc` → 방장 `OfficeSaleCounter.Sell` → 판매된 아이템마다 방송 → 모두 `PlayNetworkSold`(표시·소리·사라짐) → 요청자에게 "판매 완료 +금액" |
| 구매 창 [구매] | `PurchaseRequestRpc`(상품 번호·수량) → 방장 `Purchase` → 수령대에 생긴 아이템 방송(`SpawnedRpc`) → 결과 알림 |
| 채무 장부 | `DebtPaymentRequestRpc` → 방장 상환 → 결과 알림 |

- `NetWorldState`에 **채무 단계·납부액**을 추가했다. 공동 자금과 함께 방장 값으로 맞춘다 (`DebtLedger.ApplyNetworkState`).

## 5. 구성·버전

- `Project I > Day 38 > Rebuild Network Prefabs (Item Sync)`: 월드 상태 프리팹에 `NetItemSync`를 추가한다. 모든 대원의 프리팹 구성이 같아야 하므로 필수다.
- 접속 확인 문자열 `ProjectI-0.38`, 빌드 버전 `0.38.0-alpha`

## 검증

```text
컴파일        Unity 밖 dotnet (설치된 Netcode DLL) — 오류 0
프리팹        다시 구성 · 월드 상태 고유 번호 1827640032 · 아이템 동기화 포함
빌드          Windows 알파 0.38.0-alpha Succeeded · 127.1MB · 오류 0
2인 확인      미확인 (빌드 실행 기록 없음) — 다음 일차 시작 전에 줍기·마차 적재·판매·구매·재참가 확인 필요
```

## 남은 것

- 2인 실제 확인 결과 반영 (목록 전송/적용 로그 개수 비교)
- 바닥 물건의 물리 움직임은 각자 계산 → 멈춘 뒤 방장 위치로 보정하는 기능
- 양손 물건 함께 들기
- 39일차: 몬스터·함정·전투 동기화
