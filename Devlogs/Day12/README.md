# Project I 개발 일지

## Day 12 — 방 단위 배전반·전등·철제문 전력 제어 시스템 구현

- 날짜: 2026-08-31
- 개발 단계: Phase 3 — 조명·화염·전력 시스템
- 개발 내용 기준 커밋(amend 전): `5caf70169c75c1285f8760eef8a69db189254a01`
- 현재 커밋 메시지: `12`
- 이전 커밋: `87b1e76be621f157bb488712800c174c332a166f`

---

## 개발 목표

Day 11에서 구현한 발전기와 전기등의 직접 연결 구조를 확장해,
발전기에서 공급된 전력을 중앙 배전반을 통해 방 단위로 나누고
각 방의 전등과 전동 철제문을 함께 제어할 수 있는 전력 시스템을 구성한다.

이번 일차의 핵심 목표:

- 퓨즈 없이 발전기 → 중앙 배전반 → 방 전력 구역 구조 구현
- 공통 `PowerConsumer` 기반 전력 소비 장치 구조 추가
- 테스트 맵 내부에 전력 제어용 방 3개 생성
- 방별 천장 전기등 연결
- 방별 전동 철제문 연결
- 중앙 배전반에서 방 전력을 개별 ON / OFF
- 중앙 배전반에서 특정 철제문 원격 OPEN / CLOSE
- 철제문 옆 로컬 제어 스위치 추가
- ON / OFF 두 버튼을 하나의 토글 스위치로 통합
- 스위치 상태에 따른 실제 레버 방향 시각화
- 벽걸이형 소형 중앙 배전반 모델 구성
- 방·전등·철제문·배전반의 산업 시설형 상세 모델 구성
- 기존 횃불·화로 삭제 없이 새 시험 구조와 겹치지 않는 위치로 이동
- 방을 기존 홀 외벽에 붙여 증축된 시설처럼 자연스럽게 배치
- Day 12 전용 URP 재질 자동 생성
- Day 12 자동 Setup / Validator 추가
- 라벨 크기 축소 및 글자 방향 보정

---

## 1. 전력 구조 확장

Day 11의 전력 구조는 발전기가 전기등을 직접 제어하는 형태였다.

```text
Generator
↓
ElectricLight
```

Day 12에서는 이를 다음 구조로 확장했다.

```text
Generator
↓
MainDistributionBoard
↓
RoomPowerZone
↓
PowerConsumer
├─ ElectricLight
└─ PoweredIronDoor
```

퓨즈 시스템은 사용하지 않고,
배전반과 방별 전력 요청 상태만으로 시설 전력을 제어한다.

---

## 2. 공통 PowerConsumer

전기등과 철제문을 동일한 전력망에 연결할 수 있도록
새 `PowerConsumer`를 추가했다.

전력 소비 장치는 방에서 실제 전력이 공급되는지 전달받고
연결된 전력 반응 컴포넌트에 상태를 전달한다.

구조:

```text
RoomPowerZone
↓
PowerConsumer
↓
IPowerStateReceiver
```

이를 통해 앞으로 전동문뿐 아니라 승강기, 펌프, 기계 장치 등도
같은 방식으로 확장할 수 있는 기반을 만들었다.

---

## 3. IPowerStateReceiver

전력 상태를 받아 실제 기능을 변경할 장치를 위한 공통 인터페이스를 추가했다.

```text
IPowerStateReceiver
```

현재 Day 12에서는 전기등이 이 구조와 연결된다.

전력 공급:

```text
PowerConsumer ON
→ ElectricLightController ON
→ BrightnessSource ON
→ Unity Light ON
```

전력 차단:

```text
PowerConsumer OFF
→ ElectricLightController OFF
→ BrightnessSource OFF
→ Unity Light OFF
```

---

## 4. RoomPowerZone

각 방을 하나의 독립 전력 구역으로 관리하는 `RoomPowerZone`을 추가했다.

방 하나에는 기본적으로 다음 두 소비 장치를 연결한다.

```text
RoomPowerZone
├─ PoweredCeilingLight
└─ PoweredIronDoor
```

각 방은 다음 두 상태를 구분한다.

```text
Requested Power
Actual Power
```

`Requested Power`는 배전반 스위치가 원하는 ON / OFF 상태이며,
`Actual Power`는 발전기와 메인 배전반 상태까지 포함한 실제 통전 결과다.

따라서 방 스위치가 ON이어도 발전기가 정지하면 실제 전력은 OFF가 된다.

---

## 5. 중앙 배전반

새 `MainDistributionBoardController`를 추가했다.

중앙 배전반은 Day 11 발전기를 전력 입력으로 사용하고
세 개의 방과 세 개의 철제문을 관리한다.

```text
Generator
↓
Main Distribution Board
├─ Room 01
├─ Room 02
└─ Room 03
```

시설 실제 통전 조건:

```text
Generator Running
+
Main Power Requested
=
Facility Power Available
```

발전기가 꺼지면 배전반의 모든 방은 정전되며,
발전기가 다시 켜지면 각 방에 저장된 요청 상태에 따라 전력이 복구된다.

---

## 6. 방별 실제 전력 표시

중앙 배전반에는 각 방의 실제 전력 상태를 표시하는 상태등을 연결했다.

```text
Green
→ Actual Power ON

Red
→ Actual Power OFF
```

방 스위치 조작 결과뿐 아니라 발전기 정지와 메인 전원 차단도
상태등에 즉시 반영된다.

`MainDistributionBoardController`가 프레임마다 방의 실제 전력 상태를 확인해
배전반 표시와 실제 장치 상태가 어긋나지 않도록 구성했다.

---

## 7. 메인 전원 토글 스위치

배전반 전체 시설 전원을 위한 메인 스위치를 추가했다.

기존 ON / OFF 두 버튼 구조를 사용하지 않고
한 개의 토글 스위치를 반복해서 누르는 방식으로 변경했다.

```text
MAIN POWER ON
↓ F
MAIN POWER OFF
↓ F
MAIN POWER ON
```

기존 `IInteractable` 구조를 사용하며:

```text
InteractionType.Toggle
```

로 처리한다.

---

## 8. 방별 단일 토글 스위치

각 방도 ON 버튼과 OFF 버튼을 따로 사용하지 않는다.

```text
ROOM 01 SWITCH
ROOM 02 SWITCH
ROOM 03 SWITCH
```

현재 방 요청 상태를 기준으로 F 입력마다 반전한다.

```text
ON → OFF
OFF → ON
```

이를 통해 작은 배전반에서도 조작부가 복잡해지지 않도록 정리했다.

---

## 9. 철제문 단일 토글 스위치

각 철제문의 OPEN / CLOSE 버튼도 한 개의 스위치로 통합했다.

```text
DOOR 01 SWITCH
DOOR 02 SWITCH
DOOR 03 SWITCH
```

문이 닫혀 있거나 닫히는 중이면 다음 입력으로 열기를 요청하고,
문이 열려 있거나 열리는 중이면 닫기를 요청한다.

```text
CLOSED → OPEN
OPEN → CLOSE
```

이동 중에도 반대 방향으로 전환할 수 있도록 구성했다.

---

## 10. 스위치 레버 시각화

각 배전반 스위치에는 실제 상태를 보여주는 레버 Transform을 연결했다.

기본 각도:

```text
ON / OPEN  : -24°
OFF / CLOSE:  24°
```

따라서 플레이어는 상태등뿐 아니라
스위치 레버의 기울기만으로도 현재 제어 상태를 확인할 수 있다.

다른 위치에서 상태가 바뀌어도 `Update()`에서 레버 방향을 다시 동기화한다.

---

## 11. 테스트 방 3개

기존 `10_BrightnessTest` 대형 실내 공간에
Day 12 전력 시험용 방 3개를 생성했다.

각 방은 독립적인 전력 구역을 가진다.

```text
ROOM 01
ROOM 02
ROOM 03
```

방은 홀 한가운데 독립 박스로 두지 않고
기존 홀의 북쪽 외벽에 붙여 증축된 시설처럼 보이도록 배치했다.

기존 건물 외벽을 방의 후면 경계처럼 활용해
테스트 시설 전체가 하나의 구조물처럼 이어지도록 조정했다.

---

## 12. 방 모델링

각 방은 단순 Cube 하나가 아니라 여러 구조물을 조합했다.

주요 요소:

```text
Floor
Ceiling
Left Wall
Right Wall
Front Wall Segments
Door Lintel
Ceiling Beams
Cable Tray
Exposed Cable Lines
Corner Braces
```

전면에는 철제문이 들어갈 출입구를 남기고
좌우 벽과 상부 린텔을 별도로 구성했다.

천장에는 보강 빔과 케이블 덕트를 배치해
낡은 산업 시설 내부처럼 보이도록 했다.

---

## 13. 방 천장 전기등

각 방에 독립 전력으로 작동하는 상세 천장등을 배치했다.

```text
PoweredCeilingLight
```

주요 외형:

```text
MountPlate
Housing
Glass_Off
Glass_Glow
GuardRing
Guard Bars
Housing Bolts
LightOrigin
```

실제 Unity Light 설정:

```text
Type : Point
Range : 7.2
Intensity : 4.2
Color : Warm Industrial Light
Shadows : Soft
```

게임 밝기 판정용 `BrightnessSource`도 함께 연결했다.

---

## 14. 전동 철제문

각 방 입구에 새 `PoweredIronDoor`를 배치했다.

철제문은 단순 회전문이 아니라 옆으로 이동하는 전동 슬라이딩 구조다.

주요 모델 요소:

```text
Left / Right / Top Frame
Slide Rail
Motor Housing
Motor Cap
Moving Panel
Steel Slab
Horizontal / Vertical Reinforcement
Rivets
Handle
Warning Stripe
```

닫힌 위치와 열린 위치를 별도로 저장하고
전력이 있을 때 목표 위치까지 이동한다.

---

## 15. 철제문 전력 규칙

철제문은 해당 방의 `PowerConsumer`를 통해 전력을 공급받는다.

```text
Room Power ON
→ Door Motor Available

Room Power OFF
→ Door Motor Disabled
```

정전이 발생해도 문이 자동으로 열리거나 닫히지 않는다.

```text
정전
→ 현재 문 위치 유지
```

전력이 복구되면 요청된 이동 상태를 기준으로 다시 움직일 수 있다.

---

## 16. 중앙 원격 문 제어

각 철제문은 중앙 배전반에서 원격으로 제어할 수 있다.

```text
DOOR 01
DOOR 02
DOOR 03
```

각 행에는 문 상태를 표시하기 위한 시각 요소가 연결된다.

```text
OPEN
CLOSED
MOVING
NO POWER
```

따라서 플레이어가 배전반 앞에서 특정 문이 열려 있는지,
닫혀 있는지, 이동 중인지, 정전 상태인지 확인할 수 있다.

---

## 17. 문 옆 로컬 스위치

철제문 옆에도 별도의 로컬 제어반을 배치했다.

중앙 배전반과 동일하게 한 개의 토글 스위치를 사용한다.

```text
Local Door Switch
↓
OPEN ↔ CLOSE
```

중앙 원격 제어와 로컬 제어가 같은 `PoweredIronDoor`를 사용하므로
어느 위치에서 조작해도 문 상태와 스위치 레버 표시가 다시 동기화된다.

---

## 18. 벽걸이형 소형 배전반

초기 테스트 배전반의 크기를 줄이고
홀 내부 벽면에 붙는 형태로 수정했다.

배전반의 주요 모델 요소:

```text
Cabinet
Front Plate
Industrial Trim
Bolts
Vent Details
Section Dividers
Status Indicators
Toggle Switches
Text Labels
```

메인 전원, 방 전력, 보안문 제어 영역을 나누되
버튼을 단일 토글 스위치로 줄여 전체 폭과 높이를 축소했다.

---

## 19. 배전반 라벨 개선

배전반과 방, 문에 표시되는 글자가 지나치게 크게 보이지 않도록
공통 라벨 축소 배율을 적용했다.

```text
LabelScale : 0.5
```

즉 기존 표시 크기의 약 절반으로 줄였다.

또한 TextMesh가 뒤집혀 보이던 문제를 수정해
배전반 정면에서 정상적인 방향으로 읽히도록 회전 기준을 보정했다.

최종 자동 적용 마커:

```text
===Day12 Room Power Ready v3===
```

을 사용한다.

---

## 20. 기존 횃불·화로 위치 조정

Day 9에서 생성한 벽 횃불과 중앙 화로는 삭제하지 않았다.

Day 12의 새 방과 배전반 구조에 겹치지 않도록
기존 대형 홀의 비어 있는 벽면과 공간으로 이동했다.

대상:

```text
WallTorch_North
WallTorch_South
WallTorch_East
Brazier_Center
```

따라서 기존 고정 화염 광원 기능은 유지하면서
새 전력 시험 시설과 공간적으로 충돌하지 않도록 정리했다.

---

## 21. 기존 Day 11 구조 활용

Day 12는 기존 Day 11 발전기를 새로 만들지 않고 그대로 재사용한다.

기존 발전기는 새 시험 구역의 서비스 위치로 이동하고
Day 11에서 발전기에 직접 연결했던 테스트 전기등은
Day 12 방별 전등 시험과 중복되지 않도록 시각적으로 비활성화한다.

전력 흐름은 Day 12부터 다음을 기준으로 한다.

```text
Day 11 Generator
↓
Day 12 Distribution Board
↓
Room Power Zones
↓
Room Devices
```

---

## 22. Day 12 전용 재질

Day 12 모델을 구분하기 위한 URP 재질을 자동 생성한다.

생성 위치:

```text
Assets/ProjectI/Art/Generated/Day12/
```

주요 재질:

```text
Button_Green.mat
Button_Red.mat
Dark_Metal.mat
Equipment_Plate.mat
Indicator_Amber.mat
Indicator_Green.mat
Indicator_NoPower.mat
Indicator_Red.mat
Industrial_Trim.mat
IronDoor_Frame.mat
IronDoor_Steel.mat
Lamp_Housing.mat
Lamp_Off.mat
Lamp_On.mat
Panel_Cabinet.mat
Panel_Front.mat
Power_Cable.mat
Room_Ceiling.mat
Room_Floor.mat
Room_Wall.mat
Steel_Bolt.mat
Warning_Yellow.mat
```

Emission 상태등과 금속 계열 재질을 분리해
전력 상태와 산업 시설 외형을 쉽게 구분할 수 있도록 했다.

---

## 23. Day 12 자동 Setup

새 파일:

```text
Phase3Day12Setup.cs
```

자동 Setup에서 수행하는 주요 작업:

- `ExplorationOffice.unity` 열기
- 기존 `===Day3 Test Map===` 검색
- 기존 `10_BrightnessTest` 검색
- `IndoorBrightnessArea` 검색
- Day 11 발전기 검색
- Day 12 전용 재질 생성
- Day 11 직결 시험 전등 비활성화
- 발전기 위치 조정
- 기존 횃불·화로 위치 이동
- 기존 Day 12 시험 루트 제거 후 재구성
- 서비스 통로 세부 구조 생성
- 벽면에 연결된 방 3개 생성
- 방별 상세 천장 전기등 생성
- 방별 상세 전동 철제문 생성
- 철제문 옆 로컬 토글 스위치 생성
- 벽걸이형 중앙 배전반 생성
- 메인 / 방 / 문 토글 스위치 연결
- 상태 표시등 연결
- 씬 저장
- Day 12 Validator 실행

수동 적용 경로:

```text
Tools
→ Project I
→ Day 12
→ Apply Room Power + Iron Doors
```

---

## 24. Day 12 Validator

새 파일:

```text
Phase3Day12Validator.cs
```

Day 12 자동 구성 후
발전기, 중앙 배전반, 방 전력 구역, 전기등, 철제문,
토글 스위치와 완료 마커 등의 연결 상태를 정적으로 검증한다.

수동 실행:

```text
Tools
→ Project I
→ Day 12
→ Validate
```

Validator는 에디터 씬 구성 검증용이며
실제 Play Mode 전체 동작 검증을 대신하지 않는다.

---

## 25. 주요 생성·수정 파일

```text
Assets/ProjectI/Scripts/Power/
├─ DistributionBoardButton.cs
├─ DistributionBoardButtonAction.cs
├─ IPowerStateReceiver.cs
├─ MainDistributionBoardController.cs
├─ PowerConsumer.cs
├─ PoweredIronDoor.cs
├─ RoomPowerZone.cs
└─ ElectricLightController.cs  [수정]

Assets/ProjectI/Editor/
├─ Phase3Day12Setup.cs
└─ Phase3Day12Validator.cs

Assets/ProjectI/Art/Generated/Day12/
└─ Day 12 전용 재질

Assets/ProjectI/Scenes/
└─ ExplorationOffice.unity  [수정]
```

각 신규 Unity 에셋과 스크립트의 `.meta`도 함께 추가됐다.

---

## 26. 저장소 확인 결과

확인한 최신 커밋:

```text
5caf70169c75c1285f8760eef8a69db189254a01
```

현재 커밋 메시지:

```text
12
```

이 커밋은 Day 11 커밋:

```text
87b1e76be621f157bb488712800c174c332a166f
```

보다 정확히 1개 커밋 앞선 상태다.

GitHub 비교 기준 Day 12 변경은 다음 영역에 집중되어 있다.

```text
방 단위 전력 시스템
중앙 배전반
공통 PowerConsumer
전동 철제문
단일 토글 스위치
방별 전기등
Day 12 전용 재질
Day 12 Setup / Validator
ExplorationOffice 씬
```

`Devlogs/Day12/README.md`는 확인 시점에 아직 저장소에 존재하지 않았다.

GitHub Commit Status에도 현재 등록된 CI 상태가 없다.

저장소 변경 범위에서는 Day 12 기능과 무관한 별도 코드 변경이나
즉시 확인되는 핵심 파일 누락은 확인되지 않았다.

단, 실제 Unity Editor Compile / Play Mode 성공 여부는
GitHub 저장소만으로 확인할 수 없으므로 로컬 Unity 실행 결과가 최종 기준이다.

---

## 로컬 최종 확인 항목

- Unity Console Compile Error 0개
- `Tools → Project I → Day 12 → Validate` PASS
- 발전기 OFF 상태에서 시설 전체 정전 확인
- 발전기 ON 상태에서 메인 배전반 통전 확인
- MAIN POWER 스위치 1개로 전체 ON / OFF 확인
- ROOM 01~03 스위치 각각 독립 ON / OFF 확인
- 방 전원 OFF 시 해당 방 천장등 소등 확인
- 방 전원 OFF 시 해당 철제문 전동 기능 정지 확인
- DOOR 01~03 중앙 스위치로 개별 OPEN / CLOSE 확인
- 문 옆 로컬 스위치로 같은 문 OPEN / CLOSE 확인
- 철제문 이동 중 스위치 재입력 시 반대 방향 전환 확인
- 정전 시 철제문 현재 위치 유지 확인
- 전력 복구 후 장치 상태 정상 반영 확인
- 배전반 상태등과 실제 방/문 상태 일치 확인
- 배전반 스위치 레버 방향과 상태 일치 확인
- 배전반이 벽면에 자연스럽게 붙어 있는지 확인
- 방 3개가 기존 홀 외벽에 자연스럽게 이어지는지 확인
- 기존 횃불·화로가 삭제되지 않고 이동되어 있는지 확인
- 방/문/배전반 라벨 크기가 축소되어 있는지 확인
- 모든 Day 12 글자가 정면에서 정상 방향으로 보이는지 확인

---

## Day 12 완료 기준

```text
Generator ON
↓
Main Distribution Board ON
↓
ROOM 01 / 02 / 03 개별 전력 제어
↓
각 방 천장등 독립 점등 / 소등
↓
각 방 철제문 전력 연결
↓
중앙 배전반 또는 로컬 스위치로 철제문 개폐
```

이 흐름이 정상적으로 동작하면
Day 12의 방 단위 배전반·전등·철제문 전력 제어 기반이 완성된 것으로 본다.
