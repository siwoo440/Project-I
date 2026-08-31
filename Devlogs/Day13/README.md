# Project I 개발 일지

## Day 13 — 조명·전력 상태 복구 및 이벤트 기반 성능 최적화

- 날짜: 2026-08-31
- 개발 단계: Phase 3 — 밝기·조명·전력 시스템 마무리
- 개발 내용 기준 커밋(amend 전): `20b2baf5f8831e9085a0b0c254ced6166585c4f7`
- 현재 커밋 메시지: `13`
- 이전 커밋: `a7924416e383a7b7a25fb20b268f2221c430c2d4`
- 기준 씬: `Assets/ProjectI/Scenes/ExplorationOffice.unity`

---

## 개발 목표

Day 12까지 구현한 발전기 → 중앙 배전반 → 방 단위 전력 → 전기등·전동 철제문 구조를 기반으로,
Phase 3의 마지막 단계인 조명·전력 상태 복구 기반과 성능 최적화를 진행했다.

이번 일차의 핵심 목표:

- 발전기 연료와 가동 상태 런타임 캡처·복구
- 중앙 배전반 메인 전원 요청 상태 캡처·복구
- 방별 전력 스위치 상태 캡처·복구
- 철제문 열림·닫힘 상태 캡처·복구
- 벽 횃불·화로의 점화 상태 캡처·복구
- 휴대 횃불·랜턴의 연료와 점화 상태 캡처·복구
- F6 상태 캡처 / F7 상태 복구 디버그 기능
- 발전기·배전반·방·문·스위치의 이벤트 기반 상태 갱신
- 불필요한 프레임별 Polling 감소
- 철제문 비이동 상태 Update 비용 최소화
- F1 `Power System` 통합 진단 페이지 추가
- Day 13 자동 Setup / Validator 추가
- Phase 3 밝기·조명·전력 기반 마무리

---

## 1. PowerLightingStateManager 추가

새 파일:

```text
Assets/ProjectI/Scripts/Power/PowerLightingStateManager.cs
```

조명·전력 시스템의 현재 상태를 런타임 메모리에 저장하고 다시 복구하는
`PowerLightingStateManager`를 추가했다.

관리 대상:

```text
GeneratorController
MainDistributionBoardController
RoomPowerZone[]
PoweredIronDoor[]
FixedLightController[]
PortableLightItem[]
```

현재 단계에서는 실제 저장 파일을 생성하지 않는다.

```text
현재 게임 상태
↓
RuntimeSnapshot
↓
메모리에 보관
↓
RestoreSnapshot()
↓
기존 상태 복구
```

향후 정식 SaveData를 구현할 때 현재 캡처 구조를 저장 데이터 계층에 연결할 수 있도록 구성했다.

---

## 2. 발전기 상태 캡처·복구

발전기에서 다음 상태를 저장한다.

```text
Current Fuel
Is Running
```

예시:

```text
Fuel    : 64.5 / 100
Running : ON
```

상태를 캡처한 뒤 연료와 가동 여부를 변경해도
마지막 스냅샷을 복구하면 저장 당시 값으로 돌아갈 수 있도록 구성했다.

`GeneratorController`에는 Day 13용 복구 API를 추가했다.

```text
RestoreState(float restoredFuel, bool restoredRunning)
```

복구된 연료는 최대 연료 범위 안으로 제한되며,
연료가 0인 상태에서는 저장값이 ON이어도 발전기가 강제로 가동되지 않는다.

---

## 3. 발전기 상태 변경 이벤트

기존 발전기에는 `StateChanged` 이벤트를 추가했다.

```text
Generator Start
Generator Stop
Fuel Empty → Auto Stop
Snapshot Restore
        ↓
StateChanged
```

발전기의 실제 가동 상태가 변경될 때 중앙 배전반이 이벤트를 받아
시설 전체 전력 상태를 갱신한다.

따라서 중앙 배전반이 매 프레임 발전기 상태를 검사할 필요가 없어졌다.

---

## 4. 중앙 배전반 이벤트 기반 갱신

Day 12의 중앙 배전반은 발전기·방·철제문 상태를 프레임마다 확인했다.

Day 13에서는 다음 이벤트를 구독하는 구조로 변경했다.

```text
GeneratorController.StateChanged
RoomPowerZone.StateChanged
PoweredIronDoor.StateChanged
```

동작 흐름:

```text
상태 변경
↓
이벤트 발생
↓
MainDistributionBoardController
↓
필요한 표시와 전력 상태만 갱신
```

중앙 배전반 자체의 상태 변경도:

```text
MainDistributionBoardController.StateChanged
```

이벤트로 외부에 전달한다.

---

## 5. 방 단위 전력 이벤트

`RoomPowerZone`도 요청 상태 또는 실제 통전 상태가 변경될 때
`StateChanged` 이벤트를 전달하도록 확장했다.

예시:

```text
ROOM 02
Requested ON
↓
Requested OFF
↓
RoomPowerZone.StateChanged
↓
PowerConsumer OFF
↓
배전반 ROOM 02 상태등 갱신
```

상태가 실제로 바뀌지 않는 중복 요청에서는
불필요한 장치 갱신을 최소화한다.

---

## 6. 철제문 상태 복구

전동 철제문은 스냅샷에 안정된 최종 상태를 저장한다.

```text
Closed  → Closed
Open    → Open
Opening → Open
Closing → Closed
```

따라서 문이 이동 중인 순간 F6으로 상태를 저장해도
복구 시 애매한 중간 Transform 위치에 멈추지 않는다.

복구 시에는 열린 위치 또는 닫힌 위치로 즉시 맞춘다.

---

## 7. 철제문 프레임 처리 최적화

철제문은 실제 이동 애니메이션 때문에 `Update()`가 필요하다.

하지만 Day 13에서는 완전히 열린 상태와 닫힌 상태에서는
실질적인 이동 계산을 즉시 종료하도록 구조를 정리했다.

```text
Closed / Open
→ 이동 계산 없음

Opening / Closing
→ MoveTowards 실행
```

문이 많아져도 실제로 움직이는 문만 주요 이동 계산을 수행하도록 했다.

또한 문 상태가 바뀔 때 `StateChanged` 이벤트를 전달해
중앙 배전반과 로컬 스위치가 상태 변화를 즉시 반영한다.

---

## 8. 단일 토글 스위치 이벤트화

Day 12에서 만든 배전반 및 철제문 옆 단일 토글 스위치는
기존에 매 프레임 대상 상태를 확인해 레버 방향을 갱신했다.

Day 13에서는 대상의 상태 변경 이벤트를 구독한다.

```text
Main Power Toggle
→ MainDistributionBoardController.StateChanged

Room Power Toggle
→ RoomPowerZone.StateChanged

Door Toggle
→ PoweredIronDoor.StateChanged
```

상태가 변경된 순간에만 스위치 레버의 시각 방향을 다시 계산한다.

이를 통해 배전반과 로컬 문 스위치의 불필요한 프레임별 Polling을 제거했다.

---

## 9. 고정 횃불·화로 상태 복구

Day 9에서 구현한 고정 환경 조명도 스냅샷 대상에 포함했다.

대상:

```text
Wall Torch
Wall Torch
Wall Torch
Brazier
```

각 `FixedLightController`의 현재 `IsLit` 값을 저장한다.

복구 시:

```text
저장 ON  → TurnOn()
저장 OFF → TurnOff()
```

를 실행해 다음 요소가 함께 원래 상태로 돌아간다.

```text
BrightnessSource
Unity Light
Flame Visual
```

---

## 10. 휴대 횃불·랜턴 상태 복구

Day 8의 휴대 조명도 상태 복구 대상에 포함했다.

저장 대상:

```text
Current Fuel
Is Ignited
```

따라서 휴대 횃불이나 랜턴의 남은 연료와 사용자가 마지막으로 지정한
점화 상태를 런타임 스냅샷에 포함한다.

현재 단계에서는 월드 위치, 빠른 슬롯 번호, 전체 인벤토리 저장까지 확대하지 않는다.

Day 13의 책임은 조명 자체의 런타임 상태 복구 기반까지로 제한했다.

---

## 11. F6 / F7 런타임 테스트

`PowerLightingStateManager`에서 디버그 단축키를 제공한다.

```text
F6
→ CaptureSnapshot()
→ 현재 조명·전력 상태 저장

F7
→ RestoreSnapshot()
→ 마지막 저장 상태 복구
```

신규 Input System과 Legacy Input Manager 조건을 각각 지원하도록 구성했다.

캡처 성공 시 Console에는 저장된 대상 개수를 포함한 로그를 출력한다.

```text
Room
Door
Fixed Light
Portable Light
```

복구 대상 스냅샷이 없을 때는 경고 로그를 출력한다.

---

## 12. Editor 메뉴 캡처·복구

키보드 단축키 외에도 Play Mode에서 Editor 메뉴로 같은 기능을 실행할 수 있다.

```text
Tools
→ Project I
→ Day 13
→ Capture Runtime Snapshot
```

```text
Tools
→ Project I
→ Day 13
→ Restore Runtime Snapshot
```

Play Mode가 아니거나 상태 관리자가 없으면 실행하지 않고 안내 대화상자를 표시한다.

---

## 13. F1 Power System 진단 페이지

새 파일:

```text
Assets/ProjectI/Scripts/Diagnostics/PowerSystemDebugPage.cs
```

기존 공통 `DebugPageProvider`를 사용해 F1 진단 시스템에
`Power System` 페이지를 추가했다.

페이지 정렬 순서:

```text
SortOrder : 70
```

표시 정보:

```text
POWER / LIGHTING STATE

[Generator]
Running
Fuel / Max Fuel / %

[Distribution Board]
Main Requested
Main Actual

각 Room
Requested
Actual
Consumer Count

[Powered Iron Doors]
Door State
Power State

[Lighting]
Fixed Lights ON / Total
Portable Lights Emitting / Total

[Runtime Snapshot]
Stored YES / NO
Captured At
F6 / F7 안내
```

따라서 Phase 3의 주요 조명·전력 상태를 F1 한 페이지에서 확인할 수 있다.

---

## 14. Day 13 자동 Setup

새 파일:

```text
Assets/ProjectI/Editor/Phase3Day13Setup.cs
```

자동 Setup은 기존 Day 11 발전기와 Day 12 중앙 배전반을 찾아
새로운 Day 13 관리 구조를 생성한다.

생성되는 씬 구조:

```text
===Day13 Power Recovery System===
├─ PowerLightingStateManager
└─ PowerSystemDebugPage

===Day13 Power Recovery Ready===
```

Setup 과정:

```text
ExplorationOffice.unity 열기
↓
GeneratorController 검색
↓
MainDistributionBoardController 검색
↓
FixedLightController 전체 검색
↓
PortableLightItem 전체 검색
↓
기존 Day13 시스템 루트 제거
↓
새 상태 관리자 생성
↓
F1 Power System 페이지 생성
↓
완료 마커 생성
↓
씬 저장
↓
Day13 Validator 실행
```

수동 실행 경로:

```text
Tools
→ Project I
→ Day 13
→ Apply Power Recovery + Optimization
```

---

## 15. Day 13 Validator

새 파일:

```text
Assets/ProjectI/Editor/Phase3Day13Validator.cs
```

정적 검증 대상:

```text
ExplorationOffice.unity 존재
Day13 완료 마커 존재
Day13 시스템 루트 존재
PowerLightingStateManager 존재
PowerSystemDebugPage 존재
GeneratorController 존재
MainDistributionBoardController 존재
상태 관리자 핵심 참조 구성
배전반 연결 방 >= 3
배전반 연결 철제문 >= 3
고정 횃불·화로 >= 4
휴대 횃불·랜턴 >= 2
Day12 토글 스위치 >= 7
```

모든 조건을 만족하면 Console에 다음 계열의 로그를 출력한다.

```text
[Project I][Day13] 조명·전력 상태 복구·이벤트 기반 최적화·F1 진단 구성이 정적으로 정상입니다.
```

수동 검증:

```text
Tools
→ Project I
→ Day 13
→ Validate
```

---

## 16. 주요 신규 파일

```text
Assets/ProjectI/Scripts/Power/
└─ PowerLightingStateManager.cs

Assets/ProjectI/Scripts/Diagnostics/
└─ PowerSystemDebugPage.cs

Assets/ProjectI/Editor/
├─ Phase3Day13Setup.cs
└─ Phase3Day13Validator.cs
```

각 신규 C# 파일의 `.meta`도 함께 추가했다.

---

## 17. 주요 수정 파일

```text
Assets/ProjectI/Scripts/Power/
├─ GeneratorController.cs
├─ MainDistributionBoardController.cs
├─ RoomPowerZone.cs
├─ PoweredIronDoor.cs
└─ DistributionBoardButton.cs
```

핵심 변경 내용:

```text
GeneratorController
→ StateChanged 이벤트
→ RestoreState()

MainDistributionBoardController
→ 프레임 Polling 제거
→ 발전기·방·문 이벤트 구독
→ StateChanged 이벤트

RoomPowerZone
→ 상태 변경 이벤트

PoweredIronDoor
→ 상태 변경 이벤트
→ 안정 상태 즉시 복구
→ 비이동 상태 계산 최소화

DistributionBoardButton
→ 대상 상태 이벤트 구독
→ 스위치 레버 프레임 Polling 제거
```

---

## 18. ExplorationOffice 씬 변경

Day 13 자동 Setup 적용 결과
`ExplorationOffice.unity`에 상태 복구 전용 시스템 루트와 완료 마커가 추가됐다.

기존 Day 12의:

```text
발전기
배전반
3개 방
전기등
전동 철제문
토글 스위치
```

구조는 유지하고 Day 13 관리 계층을 추가하는 방식으로 확장했다.

---

## 19. Day 13 테스트 흐름

기본 확인 흐름:

```text
1. Play Mode 진입
2. 발전기 가동
3. 방별 전원 상태 변경
4. 철제문 일부 열기
5. 횃불·화로 상태 변경
6. 휴대 조명 점화 상태 변경
7. F6으로 Snapshot 캡처
8. 모든 상태를 다시 변경
9. F7으로 Snapshot 복구
10. F1 Power System 페이지 확인
```

복구 확인 항목:

```text
Generator Fuel
Generator Running
Main Power
Room Requested Power
Iron Door Stable State
Fixed Light Lit State
Portable Light Fuel
Portable Light Ignited State
```

---

## 20. 성능 최적화 결과 방향

Day 13의 핵심 최적화는 대규모 그래픽 최적화보다
전력 시스템의 상태 확인 방식을 Polling에서 Event 기반으로 바꾸는 데 집중했다.

기존:

```text
매 프레임
→ Generator 확인
→ Room 확인
→ Door 확인
→ Switch 확인
```

변경:

```text
실제 상태 변경
→ StateChanged
→ 관련 시스템만 갱신
```

앞으로 절차 생성으로 방과 전력 장치 수가 증가할 때
불필요한 반복 상태 검사를 줄일 수 있는 기반을 마련했다.

---

## 21. Phase 3 진행 정리

```text
Day 7  : 외부·내부 밝기 시스템
Day 8  : 휴대 횃불·랜턴 및 연료
Day 9  : 벽 횃불·화로 고정 광원
Day 10 : 시간대·자연광 변화
Day 11 : 발전기·전기등·연료 소비
Day 12 : 방 단위 배전반·전기등·철제문
Day 13 : 조명·전력 상태 복구·이벤트 최적화
```

Day 13을 기준으로 Phase 3의 밝기·조명·전력 기반 구현을 마무리하고,
다음 Day 14부터 절차적 시설 출입 구조와 방 생성 기반으로 진행한다.

---

## 22. 다음 개발 방향

다음 Day 14의 핵심은 전력 시스템 기능 추가가 아니라
현재까지 만든 테스트 환경을 실제 던전 생성 구조로 옮길 수 있는 시설 기반을 만드는 것이다.

예정 방향:

```text
Main Door
↓
Start Room
↓
Room Grid
↓
Room Size Rule
↓
Corridor
↓
Branch
```

Day 12~13에서 만든 `RoomPowerZone`, `PowerConsumer`, 전기등, 전동 철제문 구조는
향후 생성된 방에 자동 연결하는 기반으로 재사용한다.

---

## 개발 결과 요약

Day 13에서는 새로운 전력 장치를 추가하기보다
Day 7~12에서 구현한 조명·전력 시스템을 이후 절차 생성 단계에서도 사용할 수 있도록
상태 복구와 이벤트 기반 구조로 정리했다.

핵심 결과:

```text
Runtime Snapshot
+
Generator Restore
+
Room Power Restore
+
Iron Door Restore
+
Fixed / Portable Light Restore
+
Event Driven Power Update
+
F1 Power System Diagnostics
+
Day13 Setup / Validator
```

이를 통해 Phase 3 밝기·조명·전력 기반을 하나의 연결된 시스템으로 마무리했다.
