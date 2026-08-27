# Project I 개발 일지

## Day 07 — 외부·내부 밝기 시스템 및 통합 디버그 환경 구현

- 날짜: 2026-08-27
- 개발 단계: Phase 3 — 조명·화염·전력 시스템
- 기준 커밋: `e0a4914a6fe34fad3483e77ca9c85ec6a1a04cda`
- 기준 커밋 메시지: `7`
- 이전 커밋: `dee926ec2ba12684a7e885ff001344a2fa991b69`

---

## 개발 목표

Project I의 핵심 시스템인 밝기 판정을 시작한다.

화면의 실제 렌더링 결과를 직접 분석하지 않고
게임 로직에서 사용할 0~1 밝기 값을 별도로 계산한다.

공간은 크게 외부와 내부로 나눈다.

- 외부: 태양 + 달빛 + 외부 광원
- 내부: 현재 방 영역에 속한 광원만 계산

이후 횃불, 랜턴, 화염, 전기 조명, 발전기, 몬스터 시야 등
여러 시스템이 동일한 밝기 결과를 참조할 수 있는 기반을 만든다.

---

## 1. 밝기 시스템 기본 구조

새 `Brightness` 시스템을 추가했다.

주요 구성:

- `BrightnessAreaType`
- `BrightnessLevel`
- `BrightnessMath`
- `BrightnessSource`
- `IndoorBrightnessArea`
- `NaturalLightController`
- `BrightnessManager`
- `PlayerBrightnessSensor`
- `BrightnessDebugHud`

최종 밝기 값은 0~1 범위로 관리한다.

---

## 2. 외부 밝기 계산

플레이어가 어떤 `IndoorBrightnessArea`에도 속하지 않으면
현재 위치를 외부로 판정한다.

외부 밝기 계산:

```text
Outdoor Brightness
=
Sun
+
Moon
+
Outdoor BrightnessSource
```

태양과 달빛은 `NaturalLightController`가 관리한다.

일반 광원 중 내부 방에 소속되지 않은 광원만
외부 광원으로 계산한다.

최종 값은 0~1 범위로 제한한다.

---

## 3. 내부 밝기 계산

플레이어 위치가 `IndoorBrightnessArea` 안에 들어오면
해당 영역을 현재 방으로 판정한다.

내부 계산:

```text
Indoor Brightness
=
현재 IndoorBrightnessArea에 소속된 BrightnessSource 합계
```

내부에서는 기본적으로:

- 태양 직접 계산 제외
- 달빛 직접 계산 제외
- 다른 방 광원 제외
- 외부 광원 제외

한다.

따라서 각 방을 독립적인 밝기 계산 단위로 확장할 수 있다.

---

## 4. 광원 거리 감쇠

`BrightnessSource`는 기본 밝기와 영향 거리를 가진다.

광원 영향은 거리에 따라 감소한다.

기본 규칙:

```text
광원 중심      → 100%
Range 절반     → 50%
Range 끝       → 0%
```

계산은 `BrightnessMath`에서 담당한다.

```text
Contribution
=
Brightness × DistanceAttenuation
```

이를 통해 방 반대편의 광원과
플레이어 바로 옆 광원이 동일한 밝기를 주는 문제를 방지한다.

---

## 5. 밝기 단계

계산된 0~1 밝기를 게임 로직용 단계로 분류한다.

현재 기본 기준:

- `Darkness`
- `VeryDark`
- `Dark`
- `Bright`
- `VeryBright`

기본 임계값:

```text
0.00 ~ 0.15 → Darkness
0.15 ~ 0.35 → VeryDark
0.35 ~ 0.55 → Dark
0.55 ~ 0.80 → Bright
0.80 ~ 1.00 → VeryBright
```

이 값은 이후 실제 플레이 테스트를 통해 조정한다.

---

## 6. 플레이어 밝기 센서

플레이어에 `PlayerBrightnessSensor`를 추가했다.

센서는 일정 주기로 현재 위치를 `BrightnessManager`에 전달하고
다음 정보를 보관한다.

- 현재 외부 / 내부 상태
- 현재 Room 이름
- 자연광 기여도
- Local Light 기여도
- 최종 밝기
- 밝기 단계

이 값은 이후 몬스터 감지, 은신, 공포 효과 등에서도 사용할 수 있다.

---

## 7. 대형 밝기 테스트 건축물

기존 테스트 맵에 다음 모듈을 추가했다.

```text
09_InventoryTest
      │
      └─ ConnectorWalkway
              │
              ▼
        OutdoorPlaza
              │
            출입구
              │
              ▼
    MassiveIndoorBuilding
```

건축물 기본 크기:

- 가로 약 24m
- 세로 약 18m
- 높이 약 8m

기존 테스트 맵에 별도의 씬을 만드는 방식이 아니라
새 모듈을 옆에 이어 붙이는 방식으로 구성했다.

---

## 8. 건축물 내부 영역

대형 건물 내부 전체를
하나의 `IndoorBrightnessArea`로 구성했다.

구조 예:

```text
10_BrightnessTest
├─ ConnectorWalkway
├─ OutdoorPlaza
├─ OutdoorLamp_A
├─ OutdoorLamp_B
└─ MassiveIndoorBuilding
   ├─ Floor
   ├─ Roof
   ├─ Walls
   └─ IndoorRoomArea
      ├─ IndoorLamp_A
      ├─ IndoorLamp_B
      └─ IndoorLamp_C
```

플레이어가 출입구를 통과해 영역 안으로 들어가면
밝기 계산이 Outdoor에서 Indoor로 전환된다.

건물 밖으로 나오면 다시 Outdoor 계산으로 복귀한다.

---

## 9. 외부·내부 테스트 광원

외부에는 기본 시험용 광원 2개를 배치했다.

- `OutdoorLamp_A`
- `OutdoorLamp_B`

건물 내부에는 기본 시험용 광원 3개를 배치했다.

- `IndoorLamp_A`
- `IndoorLamp_B`
- `IndoorLamp_C`

각 광원은:

- 실제 화면 표현용 Unity `Light`
- 게임 판정용 `BrightnessSource`

를 함께 사용한다.

렌더링용 Light와 게임용 밝기 판정을 분리한 상태에서
테스트하기 위한 구성이다.

---

## 10. F1 통합 디버그 시스템

기존에는:

- Player Debug
- Brightness Debug

가 각각 별도의 화면 요소로 표시되었다.

이를 하나의 페이지형 디버그 창으로 통합했다.

기본 조작:

```text
F1 → 디버그 창 열기 / 닫기
←  → 이전 페이지
→  → 다음 페이지
```

화면의 `<`, `>` 버튼으로도 페이지 이동이 가능하다.

---

## 11. 현재 디버그 페이지

현재 기본 등록 페이지:

```text
1 / 2
Player Debug

2 / 2
Brightness Debug
```

페이지는 끝에서 다시 처음으로 순환한다.

`PlayerDebugHud`의 기존 디버그 정보는
공통 디버그 페이지 안으로 이동했다.

단 실제 플레이 HUD 요소인:

- HP Bar
- Stamina Bar
- Crosshair

는 F1 디버그 창과 관계없이 계속 표시한다.

---

## 12. 자동 디버그 페이지 등록 구조

향후 디버그 기능을 쉽게 추가할 수 있도록
공통 Diagnostics 시스템을 추가했다.

구성:

- `DebugPageProvider`
- `DebugPageRegistry`
- `DebugPageManager`

새 디버그 페이지가 `DebugPageProvider`를 상속하면
활성화될 때 `DebugPageRegistry`에 자동 등록된다.

예:

```text
DebugPageManager
├─ Player Debug
├─ Brightness Debug
├─ Power Debug
├─ Fire Debug
└─ Monster Debug
```

따라서 이후 발전기, 전력, 화염, 몬스터 등의 디버그 기능도
새 Canvas를 만드는 대신 동일한 F1 페이지 목록에 추가한다.

---

## 13. 디버그 입력도 Input Action 기반

다음 Input Action을 추가했다.

- `DebugToggle`
- `DebugPreviousPage`
- `DebugNextPage`

기본 키:

- `DebugToggle` → F1
- `DebugPreviousPage` → Left Arrow
- `DebugNextPage` → Right Arrow

기존 키 입력 구조와 동일하게 Input Action 기반이므로
이후 설정 화면에서 재바인딩할 수 있다.

---

## 주요 생성 파일

```text
Assets/ProjectI/Scripts/Brightness/
├─ BrightnessAreaType.cs
├─ BrightnessLevel.cs
├─ BrightnessMath.cs
├─ BrightnessSource.cs
├─ IndoorBrightnessArea.cs
├─ NaturalLightController.cs
├─ BrightnessManager.cs
├─ PlayerBrightnessSensor.cs
└─ BrightnessDebugHud.cs

Assets/ProjectI/Scripts/Diagnostics/
├─ DebugPageManager.cs
├─ DebugPageProvider.cs
└─ DebugPageRegistry.cs

Assets/ProjectI/Editor/
├─ Phase3Day7Setup.cs
├─ Phase3Day7Validator.cs
├─ Phase3Day7DebugPagerSetup.cs
└─ Phase3Day7DebugPagerValidator.cs
```

---

## 주요 수정 파일

```text
Assets/InputSystem_Actions.inputactions
Assets/ProjectI/Scenes/ExplorationOffice.unity
Assets/ProjectI/Scripts/Player/GameplayInputActions.cs
Assets/ProjectI/Scripts/Player/PlayerDebugHud.cs
Assets/ProjectI/Scripts/Player/PlayerInputReader.cs
```

---

## 저장소 점검 결과

최신 커밋:

```text
e0a4914a6fe34fad3483e77ca9c85ec6a1a04cda
```

현재 메시지:

```text
7
```

이전 6일차 커밋 `dee926ec`보다 1개 커밋 앞선 상태이며,
7일차 변경 사항이 하나의 커밋에 들어가 있다.

저장소 코드 기준으로 확인된 주요 항목:

- 외부 / 내부 밝기 계산 분리
- 태양 + 달빛 자연광 구조
- 거리 감쇠 광원 계산
- 방 단위 내부 광원 계산
- 플레이어 밝기 센서
- 대형 실내 테스트 건축물
- 외부 / 내부 시험용 광원
- F1 디버그 창 토글
- 좌우 화살표 페이지 이동
- Player / Brightness 디버그 페이지 통합
- 이후 디버그 페이지 자동 등록 구조

GitHub Commit Status에는 연결된 CI 검사가 등록되어 있지 않다.

따라서 저장소 구조상 진행을 막는 문제는 확인되지 않았지만
실제 Unity Editor Compile 및 Play Mode 결과가 최종 검증 기준이다.

---

## 로컬 최종 확인 항목

- Console Error 0개
- 기존 플레이어 이동 정상
- 기존 빠른 슬롯 / 인벤토리 정상
- 외부에서 Area = Outdoor 표시
- 외부에서 Natural 값 계산
- 외부 광원 접근 시 Local 증가
- 건물 진입 시 Area = Indoor 전환
- 내부에서 Natural = 0
- 현재 방 내부 광원만 Local 계산
- 건물 밖으로 나오면 Outdoor 복귀
- 광원에서 멀어질수록 밝기 감소
- F1 디버그 창 ON / OFF
- Left Arrow 이전 페이지
- Right Arrow 다음 페이지
- 페이지 끝에서 순환
- Player Debug 표시
- Brightness Debug 표시
- HP / Stamina Bar 계속 표시
- Crosshair 계속 표시
- 기존 Brightness 전용 Canvas 중복 표시 없음

---

## 다음 단계

Day 07에서 게임 전체의 밝기 판정 기반을 만들었다.

다음 단계에서는 이 기반에 실제 플레이 가능한 조명 도구를 연결한다.

예:

```text
BrightnessSource
      ↑
횃불 / 랜턴
      ↑
점화 / 소화
      ↑
연료 소비
```

즉 이후 조명 시스템은 새 밝기 계산 방식을 만드는 것이 아니라
Day 07의 `BrightnessSource`에 실제 게임 기능을 연결하는 방향으로 확장한다.
