# Project I 개발 일지

## Day 11 — 발전기·전기등 및 연료 소비 전력 시스템 구현

- 날짜: 2026-08-31
- 개발 단계: Phase 3 — 조명·화염·전력 시스템
- 개발 내용 기준 커밋(amend 전): `00377768438ae8844a9626d9b173bc7247740e21`
- 현재 커밋 메시지: `11`
- 이전 커밋: `37599ab4a39e405121d92f46fe7de5ae706c2834`

---

## 개발 목표

Day 10까지 완성한 시간대·자연광 시스템과 기존 `BrightnessSource`, `IInteractable` 구조 위에
처음으로 연료를 소비해 전력을 공급하는 발전기와 전기등 시스템을 연결한다.

이번 일차의 핵심 목표:

- 발전기 F 상호작용 가동 / 정지
- 발전기 최대 연료와 현재 연료 관리
- 실제 시간 기반 연료 지속 소비
- 연료 고갈 시 발전기 자동 정지
- 발전기 상태와 연결된 전기등 전력 동기화
- 전기등의 실제 Unity Light와 게임용 `BrightnessSource` 동시 제어
- 발전기 작동 / 정지 상태 시각 표시
- 발전기 연료량 5칸 게이지 표시
- 발전기 플라이휠 회전 연출
- 발전기 외형을 보호 프레임·엔진·연료탱크·배기구·제어 패널 형태로 구성
- 천장 전기등 4개 배치
- Day 11 전용 URP 재질 자동 생성
- Day 11 자동 Setup / Validator 추가

---

## 1. 전력 시스템 폴더 추가

새 전력 기능 전용 폴더를 추가했다.

```text
Assets/ProjectI/Scripts/Power/
├─ GeneratorController.cs
└─ ElectricLightController.cs
```

기존 횃불·화로의 `FixedLightController`와 역할을 섞지 않고,
전력 공급 여부에 따라 작동하는 장치는 `ProjectI.Power` 네임스페이스로 분리했다.

Day 11에서는 전력망 전체를 만들지 않고 다음의 가장 작은 전력 흐름만 구현했다.

```text
Generator
↓
ElectricLight
```

주 배전반·구역 배전·퓨즈·공통 PowerConsumer는 다음 일차 확장 대상으로 남긴다.

---

## 2. GeneratorController

새 `GeneratorController`를 추가했다.

발전기의 핵심 데이터:

```text
Max Fuel : 100
Start Fuel : 100
Fuel Consumption : 0.25 / sec
Start State : OFF
```

주요 상태:

```text
isRunning
currentFuel
maxFuel
fuelConsumptionPerSecond
```

발전기는 기존 `IInteractable`을 구현해 Day 5부터 사용한 플레이어 F 상호작용 구조와 그대로 연결된다.

상호작용 방식:

```text
InteractionType.Toggle
```

따라서 발전기를 바라보고 F를 누를 때마다 가동 / 정지가 전환된다.

---

## 3. 발전기 F 상호작용

정지 상태에서 연료가 남아 있으면 다음 흐름으로 동작한다.

```text
[F] 주 발전기 가동 · 연료 100%
↓
Generator ON
↓
연결 전기등 전력 공급
↓
연료 소비 시작
```

작동 중 다시 F를 누르면:

```text
[F] 주 발전기 정지 · 연료 XX%
↓
Generator OFF
↓
전기등 전력 차단
↓
연료 소비 정지
```

연료가 없으면 다음 문구를 반환한다.

```text
주 발전기 · 연료 없음
```

연료가 없는 상태에서는 `StartGenerator()`가 실패하고 정지 상태를 유지한다.

---

## 4. 발전기 연료 소비

발전기는 작동 중 `Update()`에서 실제 경과 시간을 기준으로 연료를 소비한다.

기본 소비량:

```text
0.25 / sec
```

연료 계산 구조:

```text
Current Fuel
-
Fuel Consumption Per Second × Delta Time
=
New Fuel
```

현재 연료는 항상:

```text
0 ~ Max Fuel
```

범위로 제한한다.

정지 상태에서는 연료를 소비하지 않는다.

---

## 5. 연료 고갈 자동 정지

작동 중 연료가 0에 도달하면 발전기를 자동으로 정지한다.

```text
Fuel > 0
→ Generator Running

Fuel = 0
→ StopGenerator()
→ Generator OFF
→ Electric Lights OFF
```

따라서 연료가 고갈된 뒤에도 전기등이 계속 켜져 있는 상태가 남지 않는다.

---

## 6. ElectricLightController

새 `ElectricLightController`를 추가했다.

전기등은 플레이어가 직접 F로 켜는 장치가 아니라
발전기에서 전달받은 전력 상태를 기준으로 동작한다.

```text
GeneratorController
→ SetPowered(true / false)
→ ElectricLightController
```

전력 ON:

```text
BrightnessSource ON
Unity Light ON
점등 시각 요소 ON
소등 시각 요소 OFF
```

전력 OFF:

```text
BrightnessSource OFF
Unity Light OFF
점등 시각 요소 OFF
소등 시각 요소 ON
```

기존 밝기 시스템을 새로 만들지 않고 `BrightnessSource.SetSourceEnabled()`를 그대로 사용한다.

---

## 7. 게임 밝기 시스템 연결

각 전기등에는 기존 `BrightnessSource`를 연결했다.

전기등 기본 설정:

```text
Brightness : 0.26
Range : 8.5
Source Type : Fixed
Emission : Omnidirectional
Start Powered : False
```

실제 화면 표현용 Unity `Light`도 같은 전력 상태에 맞춰 켜지고 꺼진다.

따라서 Day 7~10에서 만든 밝기 판정 구조를 유지하면서
전기등이 게임 로직상의 밝기에도 정상적으로 기여하도록 구성했다.

---

## 8. 천장 전기등 4개

`ExplorationOffice`의 기존 `10_BrightnessTest` 실내 영역에
천장 전기등 4개를 자동 배치한다.

```text
ElectricLight_01
ElectricLight_02
ElectricLight_03
ElectricLight_04
```

배치는 실내 `Bounds`를 기준으로 네 방향에 분산한다.

각 전기등 외형:

```text
CeilingPlate
Housing
Glass_Off
Glass_Glow
LightOrigin
```

실제 화면용 Point Light 설정:

```text
Type : Point
Range : 8.5
Intensity : 4.6
Color : Warm White
Shadows : Soft
```

낡은 시설의 백열 전기등 느낌을 위해 따뜻한 색을 적용했다.

---

## 9. 발전기 외형 개선

Day 11 발전기는 단순 Cube 하나가 아니라 여러 Primitive를 조합해
테스트 단계에서도 발전기라고 인식할 수 있는 실루엣을 만들었다.

주요 구성 요소:

```text
Base
LeftFoot / RightFoot
Protective Frame
Engine Block
Fuel Tank
Fuel Cap
Generator Coil
Flywheel
Exhaust Pipe
Control Panel
Running Indicator
Stopped Indicator
Fuel Gauge
```

하부에는 진동 방지 고무 받침을 배치하고
외부에는 금속 보호 프레임을 구성했다.

엔진 블록, 발전 코일, 원통형 연료 탱크, 배기 파이프를 분리해
일반 상자형 오브젝트보다 발전기 형태가 명확하도록 만들었다.

---

## 10. 발전기 작동 시각 효과

발전기 상태를 외형에서도 확인할 수 있도록 시각 요소를 연결했다.

작동 상태:

```text
Green Indicator ON
Red Indicator OFF
Flywheel Rotation ON
Electric Lights ON
```

정지 상태:

```text
Green Indicator OFF
Red Indicator ON
Flywheel Rotation OFF
Electric Lights OFF
```

플라이휠 기본 회전 속도:

```text
480 deg / sec
```

실제 발전기 애니메이션 에셋을 사용하지 않고 Transform 회전으로 가동 상태를 표현한다.

---

## 11. 발전기 연료 게이지

발전기 제어 패널에 5칸 연료 게이지를 구성했다.

```text
[■][■][■][■][■]
```

현재 연료 비율을 기준으로 표시할 칸 수를 계산한다.

예시:

```text
100%
→ 5칸

60%
→ 3칸

20%
→ 1칸

0%
→ 0칸
```

연료 소비가 진행되면 게이지도 함께 갱신된다.

---

## 12. 발전기 상호작용 Collider

발전기의 장식용 Primitive Collider가 플레이어 Raycast를 가로막지 않도록
시각용 Primitive의 불필요한 Collider를 제거하고 발전기 루트에 하나의 상호작용 Collider를 둔다.

```text
Generator_Main
└─ BoxCollider
```

이 Collider가 전체 발전기 외형을 감싸며
기존 `PlayerInteractor`가 부모의 `IInteractable` 구현체를 찾아 상호작용한다.

---

## 13. Day 11 Hierarchy

자동 Setup 후 주요 구조:

```text
10_BrightnessTest
└─ IndoorBrightnessArea
   └─ Day11_PowerTest
      ├─ ElectricLight_01
      ├─ ElectricLight_02
      ├─ ElectricLight_03
      ├─ ElectricLight_04
      └─ Generator_Main
```

발전기에는 전기등 4개가 직접 연결된다.

자동 적용 완료 마커:

```text
===Day11 Generator Power Ready===
```

도 씬에 생성한다.

---

## 14. Day 11 전용 URP 재질

발전기와 전기등의 가독성을 높이기 위해 전용 재질을 자동 생성한다.

생성 위치:

```text
Assets/ProjectI/Art/Generated/Day11/
```

주요 재질:

```text
ElectricLight_Off.mat
ElectricLight_On.mat
Generator_Body.mat
Generator_DarkMetal.mat
Generator_GreenGlow.mat
Generator_Metal.mat
Generator_RedGlow.mat
Generator_Rubber.mat
Generator_Warning.mat
```

점등 전기등과 상태 표시등은 Emission을 사용해
전력이 들어온 상태를 외형에서도 즉시 구분할 수 있도록 했다.

---

## 15. Day 11 자동 Setup

새 파일:

```text
Phase3Day11Setup.cs
```

자동 Setup에서 수행하는 작업:

- `ExplorationOffice.unity` 열기
- 기존 `===Day3 Test Map===` 검색
- 기존 `10_BrightnessTest` 검색
- `IndoorBrightnessArea` 검색
- Day 11 생성 재질 폴더 확보
- 전기등 / 발전기용 URP 재질 생성
- 기존 `Day11_PowerTest`가 있으면 재구성을 위해 제거
- `Day11_PowerTest` 루트 생성
- 천장 전기등 4개 생성
- 디자인 발전기 생성
- 발전기와 전기등 4개 연결
- 발전기 초기 연료 100 설정
- 발전기 초기 상태 OFF 설정
- 완료 마커 생성
- `ExplorationOffice.unity` 저장
- 생성 재질 저장
- Day 11 Validator 실행

수동 적용 경로:

```text
Tools
→ Project I
→ Day 11
→ Apply Generator + Electric Lights
```

---

## 16. Day 11 Validator

새 파일:

```text
Phase3Day11Validator.cs
```

주요 정적 검증 항목:

```text
Ready Marker
Generator Exists
Max Fuel >= 1
Current Fuel >= 0
Fuel Consumption > 0
Connected Electric Lights >= 4
Electric Lights >= 4
Each Light Has BrightnessSource
Initial Generator State = OFF
Initial Electric Light State = OFF
```

전체 조건이 정상일 경우 Console에 다음 계열의 성공 로그를 출력한다.

```text
[Project I][Day11] 발전기·전기등·연료 소비 구성이 정적으로 정상입니다.
```

수동 실행:

```text
Tools
→ Project I
→ Day 11
→ Validate
```

---

## 17. 주요 생성 파일

```text
Assets/ProjectI/Scripts/Power/
├─ ElectricLightController.cs
└─ GeneratorController.cs

Assets/ProjectI/Editor/
├─ Phase3Day11Setup.cs
└─ Phase3Day11Validator.cs

Assets/ProjectI/Art/Generated/Day11/
├─ ElectricLight_Off.mat
├─ ElectricLight_On.mat
├─ Generator_Body.mat
├─ Generator_DarkMetal.mat
├─ Generator_GreenGlow.mat
├─ Generator_Metal.mat
├─ Generator_RedGlow.mat
├─ Generator_Rubber.mat
└─ Generator_Warning.mat
```

신규 C# 파일과 생성 폴더·재질의 `.meta`도 함께 추가됐다.

---

## 18. 주요 씬 변경

```text
Assets/ProjectI/Scenes/ExplorationOffice.unity
```

씬에는 Day 11 전력 시험 구조가 저장됐다.

포함된 핵심 요소:

- `Day11_PowerTest`
- 발전기 1대
- 전기등 4개
- 발전기 ↔ 전기등 연결
- 발전기 시각 부품
- 발전기 연료 게이지
- Day 11 완료 마커

---

## 저장소 확인 결과

확인한 최신 커밋:

```text
00377768438ae8844a9626d9b173bc7247740e21
```

현재 커밋 메시지:

```text
11
```

이 커밋은 Day 10 커밋:

```text
37599ab4a39e405121d92f46fe7de5ae706c2834
```

보다 정확히 1개 커밋 앞선 상태다.

GitHub 비교 기준 Day 11 변경은 다음 영역에 집중되어 있다.

```text
Power 스크립트
Day 11 Setup / Validator
Day 11 생성 재질
ExplorationOffice 씬
Unity 솔루션 메타 변경
```

현재 저장소에는 `Devlogs/Day01`부터 `Devlogs/Day10`까지 개발일지가 존재하며,
Day 11 개발 내용은 이미 커밋되어 있으나 `Devlogs/Day11/README.md`는 아직 없는 상태였다.

GitHub 저장소에서 확인되는 변경 범위만 기준으로 볼 때
Day 11 핵심 파일과 씬 적용 구조의 즉시 확인되는 누락은 발견되지 않았다.

실제 Unity Editor Compile, Play Mode 및 발전기 런타임 동작 성공 여부는
GitHub 저장소만으로 완전히 증명할 수 없으므로 로컬 실행 결과가 최종 기준이다.

---

## 로컬 최종 확인 항목

- Unity Console Compile Error 0개
- `Tools → Project I → Day 11 → Validate` PASS
- 시작 시 발전기 OFF 확인
- 시작 시 전기등 4개 OFF 확인
- 발전기를 바라보면 F 상호작용 문구 표시
- F 입력 후 발전기 ON 확인
- 발전기 ON 상태에서 전기등 4개 점등 확인
- 전기등 점등 시 실제 화면 Light 활성화 확인
- 전기등 점등 시 `BrightnessSource` 활성화 확인
- 발전기 가동 중 연료 감소 확인
- 발전기 정지 중 연료 감소 중단 확인
- 연료량에 따라 5칸 게이지 감소 확인
- 발전기 가동 중 플라이휠 회전 확인
- 발전기 정지 시 플라이휠 정지 확인
- 연료 0 도달 시 발전기 자동 정지 확인
- 연료 0 도달 시 전기등 전체 소등 확인

---

## Day 11 완료 기준

Day 11은 다음 흐름이 연결된 상태를 목표로 한다.

```text
발전기 OFF
↓
[F] 발전기 가동
↓
발전기 ON
↓
연료 소비 시작
↓
전기등 4개 ON
↓
게임 밝기 + 실제 화면 조명 활성화
↓
연료 지속 감소
↓
연료 0
↓
발전기 자동 OFF
↓
전기등 4개 OFF
```

이를 통해 Project I의 조명 시스템은
자체 연료를 사용하는 횃불·랜턴과 고정 화염 광원뿐 아니라
연료 기반 발전기에서 공급받는 전기 조명까지 확장됐다.

다음 단계에서는 이 단순 `Generator → ElectricLight` 연결을 기반으로
주 배전반·구역 배전·퓨즈·공통 전력 소비 장치 구조로 확장한다.
