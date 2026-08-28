# Project I 개발 일지

## Day 09 — 고정 환경 광원 및 F1 광원 진단 시스템 구현

- 날짜: 2026-08-28
- 개발 단계: Phase 3 — 조명·화염·전력 시스템
- 기준 커밋: `7d4c40c820fd463bfd4402316f000a47120ca3ae`
- 현재 커밋 메시지: `9`
- 이전 커밋: `0511019fdd6e4d942d21d29861eb8138ab66fd4c`

---

## 개발 목표

Day 08에서 구축한 휴대 광원 시스템에 이어
맵에 고정된 환경 광원과 광원 디버깅 기능을 구현한다.

이번 일차의 핵심 목표:

- 벽 횃불과 화로 형태의 Fixed 광원 구현
- 기존 실내 테스트 램프를 실제 고정 광원 테스트 구조로 교체
- F 상호작용으로 고정 광원 점화 / 소화
- 같은 방의 Fixed 광원만 현재 밝기 계산에 포함
- 휴대 광원과 고정 광원을 동일한 BrightnessSource 체계에서 합산
- F1 활성화 시 각 광원 옆에 현재 플레이어 기준 기여 밝기 표시
- 현재 위치에서 모든 광원의 계산 결과를 확인하는 Light Calculation 페이지 추가
- 광원이 제외되는 이유를 상태값으로 표시
- Day 09 Setup / Validator 추가

---

## 1. Fixed 환경 광원 구현

새 `FixedLightController`를 추가했다.

대상:

```text
FixedLightController
├─ WallTorch
└─ Brazier
```

주요 역할:

```text
IsLit
TurnOn()
TurnOff()
Toggle()
```

고정 광원은 `BrightnessSourceType.Fixed`를 사용한다.

Portable 광원처럼 현재 위치를 매번 찾아서 방을 판정하지 않고
부모의 `IndoorBrightnessArea`를 기준으로 소속 방을 결정한다.

---

## 2. F 상호작용 점화 / 소화

`FixedLightController`는 기존 `IInteractable` 구조를 사용한다.

```text
InteractionType.Toggle
```

플레이어가 벽 횃불 또는 화로를 바라보고 F를 누르면:

```text
OFF
→ F
→ ON

ON
→ F
→ OFF
```

상태가 전환된다.

상태 변경 시 다음 요소가 함께 동기화된다.

- `FixedLightController.IsLit`
- Unity `Light`
- 게임용 `BrightnessSource`
- 불꽃 / 발광 시각 오브젝트

---

## 3. 벽 횃불

실내 테스트 방에 벽 횃불 3개를 배치했다.

```text
WallTorch_North
WallTorch_South
WallTorch_East
```

기본 설정:

```text
Brightness : 0.30
Range      : 7m
SourceType : Fixed
Shape      : Omnidirectional
Start      : OFF
```

벽 횃불은 방 가장자리의 근거리 조명을 담당한다.

---

## 4. 중앙 화로

실내 방 중앙에 더 강한 고정 광원인 화로를 추가했다.

```text
Brazier_Center
```

기본 설정:

```text
Brightness : 0.50
Range      : 11m
SourceType : Fixed
Shape      : Omnidirectional
Start      : OFF
```

벽 횃불보다 넓은 범위와 높은 밝기를 사용한다.

---

## 5. 기존 실내 테스트 램프 제거

Day 07에서 밝기 테스트 목적으로 사용하던:

```text
IndoorLamp_A
IndoorLamp_B
IndoorLamp_C
```

를 Day 09 Setup에서 제거한다.

교체 후 구조:

```text
IndoorRoomArea
├─ WallTorch_North
├─ WallTorch_South
├─ WallTorch_East
└─ Brazier_Center
```

단순 테스트 램프 대신
실제 게임 구조에 가까운 고정 조명을 사용하도록 변경했다.

---

## 6. Fixed + Portable 광원 통합

Day 08 Portable 광원과 Day 09 Fixed 광원 모두
기존 `BrightnessSource` 목록에 등록된다.

현재 위치 밝기 계산은 동일하게:

```text
Fixed Lights
+
Portable Lights
=
Local Brightness
```

구조로 합산한다.

따라서 방에 켜진 벽 횃불과 화로가 있고
플레이어가 휴대 횃불이나 랜턴을 켜면
모든 유효한 광원의 기여값이 함께 계산된다.

---

## 7. F1 월드 광원 숫자 표시

새 `LightDebugLabelManager`를 추가했다.

F1이 닫혀 있으면:

```text
광원 숫자 표시 없음
```

F1을 열면 현재 등록된 모든 `BrightnessSource`의 위치 옆에:

```text
광원 이름
0.183
```

형태로 숫자가 표시된다.

여기서 표시하는 값은 광원 설정의 기본 `Brightness` 값이 아니라
현재 플레이어 위치에 실제로 기여하는 값이다.

플레이어가 멀어지면 거리 감쇠에 따라 값이 감소하고
영향이 없으면 `0.000`으로 표시된다.

---

## 8. 모든 BrightnessSource를 월드 라벨 대상으로 사용

월드 숫자 라벨 대상:

- 벽 횃불
- 화로
- 휴대 횃불
- 랜턴 주 빔
- 랜턴 주변 보조광
- 기존 외부 BrightnessSource

현재 씬에서 게임 밝기 계산에 참여할 수 있는
모든 `BrightnessSource`를 동일한 방식으로 확인할 수 있다.

---

## 9. BrightnessDebugUtility

F1 월드 숫자와 상세 계산 페이지가 같은 진단 로직을 공유하도록
`BrightnessDebugUtility`를 추가했다.

광원 하나의 진단 결과:

```text
LightContributionDebugInfo
├─ Source
├─ DisplayName
├─ Contribution
├─ Distance
└─ Status
```

실제 기여값은 기존 `BrightnessSource.GetContribution()`을 사용한다.

---

## 10. 광원 제외 이유 표시

광원이 현재 플레이어 밝기에 기여하지 않는 이유:

```text
Active
Disabled
Out Of Range
Different Area
Outside Cone
```

- `Active`: 현재 실제 밝기에 기여
- `Disabled`: 광원이 꺼져 있음
- `Out Of Range`: 플레이어가 영향 범위 밖
- `Different Area`: 플레이어와 다른 Indoor / Outdoor 공간
- `Outside Cone`: 방향성 광원의 조사 각도 밖

---

## 11. Fixed Light Debug 페이지

F1 통합 디버그에:

```text
Fixed Light Debug
```

페이지를 추가했다.

확인 가능한 정보:

```text
State
Room
Brightness
Range
```

고정 광원 자체의 설정과 켜짐 상태를 빠르게 확인한다.

---

## 12. Light Calculation 페이지

현재 플레이어 위치에서 등록된 모든 광원을 하나씩 계산하는
상세 페이지를 추가했다.

```text
Light Calculation
```

표시 항목:

```text
Area
Room
Position
Sources

각 광원:
Contribution
Status
Distance
```

페이지를 보는 동안 플레이어 밝기를 즉시 다시 샘플링하여
현재 위치 기준 값을 표시한다.

---

## 13. 밝기 합계 디버그

Light Calculation 페이지 하단:

```text
Raw Local Sum
Local Total
Natural Total
Final
Level
```

- `Raw Local Sum`: 개별 광원 기여값의 Clamp 전 합계
- `Local Total`: 0~1 범위로 제한한 지역 광원 합계
- `Natural Total`: 현재 자연광
- `Final`: 최종 플레이어 밝기
- `Level`: 5단계 밝기 등급

---

## 14. F1 페이지 구성

Day 09 기준:

```text
Player Debug
Brightness Debug
Portable Light Debug
Fixed Light Debug
Light Calculation
```

기존 `DebugPageProvider` / `DebugPageRegistry` 구조를 그대로 사용한다.

---

## 15. Day 09 자동 Setup

새 에디터 도구:

```text
Phase3Day9Setup.cs
```

주요 작업:

- `10_BrightnessTest` 검색
- 기존 실내 테스트 램프 제거
- 벽 횃불 3개 생성
- 중앙 화로 1개 생성
- Fixed Light Debug 페이지 추가
- Light Calculation 페이지 추가
- F1 월드 광원 숫자 관리자 추가
- Day 09 완료 마커 생성
- 씬 저장
- Day 09 Validator 실행

수동 실행:

```text
Tools
→ Project I
→ Day 9
→ Apply Fixed Lights + Debug
```

---

## 16. Day 09 Validator

새 검증 도구:

```text
Phase3Day9Validator.cs
```

검증 항목:

- Fixed 광원 4개 존재
- 고정 광원 이름
- `BrightnessSourceType.Fixed`
- 시작 상태 OFF
- `TurnOn / TurnOff`와 BrightnessSource 상태 동기화
- 기존 `IndoorLamp_A/B/C` 제거
- F1 Fixed Light Debug 페이지
- F1 Light Calculation 페이지
- F1 월드 광원 기여값 라벨 관리자

수동 실행:

```text
Tools
→ Project I
→ Day 9
→ Validate
```

---

## 주요 생성 파일

```text
Assets/ProjectI/Scripts/Lighting/
├─ FixedLightController.cs
└─ FixedLightDebugPage.cs

Assets/ProjectI/Scripts/Diagnostics/
├─ BrightnessDebugUtility.cs
├─ LightCalculationDebugPage.cs
└─ LightDebugLabelManager.cs

Assets/ProjectI/Editor/
├─ Phase3Day9Setup.cs
└─ Phase3Day9Validator.cs
```

각 신규 C# 파일의 `.meta`도 함께 생성했다.

---

## 주요 씬 변경

```text
Assets/ProjectI/Scenes/ExplorationOffice.unity
```

테스트 씬의 조명 구조와
플레이어 F1 디버그 컴포넌트 구성이 변경됐다.

---

## 저장소 확인 결과

확인한 최신 커밋:

```text
7d4c40c820fd463bfd4402316f000a47120ca3ae
```

현재 커밋 메시지:

```text
9
```

이 커밋은 Day 08 커밋:

```text
0511019fdd6e4d942d21d29861eb8138ab66fd4c
```

보다 정확히 1개 커밋 앞선 상태다.

GitHub 비교 결과 Day 09 변경은 고정 광원, F1 광원 계산 진단,
Day 09 Setup / Validator 및 `ExplorationOffice.unity`에 집중되어 있다.

GitHub Commit Status에는 등록된 CI 상태가 없다.

저장소 변경 목록에서 즉시 확인되는 Day 09 범위 외의
별도 코드 변경은 확인되지 않았다.

실제 Unity Editor Compile 및 Play Mode 실행 여부는
GitHub 저장소만으로 검증할 수 없으므로 로컬 실행 결과가 최종 기준이다.

---

## 로컬 최종 확인 항목

- Unity Console Compile Error 0개
- `Tools → Project I → Day 9 → Validate` PASS
- 벽 횃불 3개 존재
- 중앙 화로 1개 존재
- 각 고정 광원 F 점화 / 소화
- 불꽃 표시와 Unity Light 동기화
- BrightnessSource ON / OFF 동기화
- 같은 방 Fixed 광원 밝기 합산
- Portable + Fixed 광원 동시 합산
- F1 OFF 시 월드 밝기 숫자 숨김
- F1 ON 시 모든 광원 옆 기여 밝기 숫자 표시
- 플레이어 이동 시 광원 숫자 변화
- 다른 방 `Different Area`
- 꺼진 광원 `Disabled`
- 범위 밖 `Out Of Range`
- 랜턴 방향 밖 `Outside Cone`
- Fixed Light Debug 페이지
- Light Calculation 페이지
- Raw Local Sum / Local Total / Natural Total / Final 값 확인
