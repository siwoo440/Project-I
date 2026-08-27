# Project I 개발 일지

## Day 08 — 휴대 조명·연료 및 방향성 밝기 시스템 구현

- 날짜: 2026-08-27
- 개발 단계: Phase 3 — 조명·화염·전력 시스템
- 기준 커밋: `edbbf8da4ced5d9fa259fe5296bd7327081ac75a`
- 기준 커밋 메시지: `8`
- 이전 커밋: `68b22a81d774272ec07454cd5a0e4c60c2dfd09d`

---

## 개발 목표

Day 07에서 구축한 외부·내부 밝기 계산 시스템에
실제 플레이 가능한 휴대 광원을 연결한다.

이번 일차의 핵심 목표:

- 횃불과 랜턴을 빠른 슬롯 아이템으로 사용
- 좌클릭 점화 / 소화
- 연료 소비 및 자동 소화
- 손에 들거나 바닥에 내려놓아도 밝기 계산 유지
- 인벤토리 보관 중 광원과 연료 소비 일시 정지
- 이동형 광원이 현재 위치에 따라 Outdoor / Indoor 영역을 자동 변경
- 횃불과 랜턴에 서로 다른 조명 역할 부여
- F1 통합 디버그 시스템에 휴대 조명 페이지 추가

---

## 1. 휴대 광원 타입 추가

기존 `BrightnessSource`에 광원 소속 방식을 추가했다.

```text
BrightnessSourceType
├─ Fixed
└─ Portable
```

### Fixed

기존 고정 광원 방식이다.

부모 구조에 연결된 `IndoorBrightnessArea`를 기준으로
어느 방에 속하는지 결정한다.

### Portable

횃불과 랜턴처럼 플레이어가 이동시킬 수 있는 광원이다.

```text
광원의 현재 월드 위치
→ IndoorBrightnessArea.FindContaining()
→ Outdoor 또는 현재 Indoor Room 판정
```

따라서 같은 횃불을 들고 외부에서 건물 내부로 이동하면
별도의 재등록 없이 현재 방 광원으로 자동 전환된다.

---

## 2. 이동형 광원의 실제 위치 기반 계산

`BrightnessManager`가 광원의 고정 `OwnerArea`만 확인하지 않고
`BrightnessSource.GetEffectiveArea()`를 사용하도록 수정했다.

Portable 광원:

```text
외부에 있음
→ Outdoor 밝기에 포함

방 안에 있음
→ 해당 IndoorBrightnessArea 밝기에 포함

다른 방으로 이동
→ 새 방의 밝기에 포함
```

고정 광원은 기존 동작을 유지한다.

---

## 3. 휴대 조명 상태 시스템

새 `PortableLightItem`을 추가했다.

기본 상태:

```text
PortableLightState
├─ Extinguished
├─ Ignited
├─ StoredPaused
└─ Empty
```

### Extinguished

연료는 남아 있지만 꺼진 상태.

### Ignited

현재 실제 Light와 게임용 `BrightnessSource`가 활성화된 상태.

### StoredPaused

켜진 상태를 기억하고 있지만
선택되지 않은 빠른 슬롯 아이템으로 보관된 상태.

이 상태에서는:

- 실제 Light OFF
- BrightnessSource OFF
- 연료 소비 정지

한다.

### Empty

연료가 모두 소모되어 점화할 수 없는 상태.

---

## 4. 좌클릭 점화 / 소화

기존 `IUsableItem` 구조를 사용했다.

```text
빠른 슬롯에서 횃불 또는 랜턴 선택
→ 좌클릭
→ 점화

다시 좌클릭
→ 소화
```

별도의 새 입력 키는 만들지 않았다.

---

## 5. 연료 시스템

휴대 조명에 다음 값을 추가했다.

```text
MaxFuel
CurrentFuel
FuelConsumptionPerSecond
```

점화 상태이면서 실제로 손에 들었거나
월드에 내려놓은 경우에만 연료를 소비한다.

```text
Ignited
+
Not Stored
→ 연료 감소
```

연료가 0이 되면:

```text
Fuel = 0
→ isIgnited = false
→ Light OFF
→ BrightnessSource OFF
```

로 자동 소화된다.

---

## 6. 빠른 슬롯 보관 규칙

켜진 조명을 선택한 뒤 다른 슬롯으로 이동하면
아이템은 기존 인벤토리 시스템에 따라 `InventoryStorage`로 이동한다.

이때 점화 상태 자체는 기억한다.

```text
켜진 횃불
→ 다른 슬롯 선택
→ StoredPaused
→ Light OFF
→ BrightnessSource OFF
→ 연료 소비 정지
```

다시 해당 슬롯을 선택하면
이전 점화 상태를 복구한다.

---

## 7. 바닥에 내려놓은 광원

점화된 횃불 또는 랜턴을 `Q`로 내려놓아도
월드에 존재하는 동안 계속 빛을 유지한다.

```text
손에 들고 점화
→ Q
→ 월드에 배치
→ Light 유지
→ BrightnessSource 유지
→ 연료 계속 소비
```

따라서 랜턴을 바닥에 배치하고
다른 아이템을 들고 탐색하는 플레이가 가능하다.

---

## 8. 횃불

시험용 횃불 기본값:

```text
CarryType   : OneHand
Brightness  : 0.35
Range       : 약 7m
Fuel        : 60
Consumption : 초당 1
```

횃불은 `Point Light`를 사용한다.

역할은:

```text
플레이어 주변
+
횃불 주변의 근거리 공간
```

을 밝히는 것이다.

---

## 9. 횃불 광원 위치 수정

초기 테스트에서는 횃불 광원을 플레이어 앞쪽으로 이동시켰지만
최종적으로 광원 중심을 다시 횃불 불꽃 끝부분으로 변경했다.

현재 기준:

```text
TorchLightOrigin
Local Position
(0, 0.52, 0)
```

즉:

```text
     Flame
       ●  ← Light Origin
       │
       │
     Torch
```

형태로 불꽃 끝부분 자체가 주변광의 중심이다.

Validator도 다음 조건을 검사하도록 수정했다.

- Point Light
- Omnidirectional
- 약 7m 범위
- 불꽃 끝부분 높이
- X/Z 중심축 유지

---

## 10. 랜턴

시험용 랜턴 기본값:

```text
CarryType   : OneHand
Fuel        : 120
Consumption : 초당 1
```

랜턴은 횃불과 역할을 분리했다.

```text
횃불
→ 근거리 주변 조명

랜턴
→ 정면 장거리 탐색
```

---

## 11. 랜턴 장거리 Spot Light

랜턴 주 광원을 기존 Point Light에서
장거리 `Spot Light`로 변경했다.

기본 설정:

```text
Range           : 22m
Spot Angle      : 52°
Inner Spot      : 30°
Intensity       : 8
```

플레이어가 복도와 방 정면을 길게 확인하는 용도로 사용한다.

---

## 12. 랜턴 화면 정중앙 조준

`PortableLightAim`을 추가했다.

랜턴을 손에 들고 있으면:

```text
Camera
→ 화면 정중앙
→ 약 24m 전방 조준점 계산
→ LanternBeamOrigin이 해당 지점을 바라봄
```

따라서 랜턴 모델 자체가 화면 오른쪽 아래에 있어도
주 빔은 플레이어 화면 정중앙을 향한다.

랜턴을 바닥에 내려놓으면
카메라 추적을 중단하고 랜턴 오브젝트가 향하는 정면을 비춘다.

---

## 13. 랜턴 주변 보조광

장거리 Spot Light만 사용하면
플레이어와 랜턴 바로 주변이 지나치게 어두워질 수 있기 때문에
약한 근거리 Point Light를 함께 추가했다.

```text
Lantern
├─ Main Beam
│  └─ Spot Light / Cone Brightness
│
└─ Ambient
   └─ Point Light / Omnidirectional
```

보조광 기본 범위:

```text
약 4.5m
```

주 빔과 보조광은 하나의 `PortableLightItem`에서
동시에 점화 / 소화된다.

---

## 14. 방향성 게임 밝기 계산

화면에 보이는 Spot Light만 변경하지 않고
게임 로직의 밝기 판정에도 방향성을 추가했다.

```text
BrightnessEmissionShape
├─ Omnidirectional
└─ Cone
```

### Omnidirectional

횃불이나 주변 보조광처럼
모든 방향으로 동일하게 퍼지는 광원.

### Cone

랜턴 주 빔처럼
정해진 전방 각도 안에서만 밝기를 제공하는 광원.

Cone 계산:

```text
거리 감쇠
+
빔 중심축과 대상 위치의 각도
```

를 함께 사용한다.

빔 중심은 가장 강하고
가장자리로 갈수록 감소하며
원뿔 밖에서는 주 빔 기여도가 0이 된다.

---

## 15. 화면 Light와 게임 밝기 위치 통일

`BrightnessSource.GetContribution()`은
연결된 실제 Unity `Light`가 있으면
그 Light의 위치와 방향을 기준으로 계산한다.

따라서:

```text
실제 화면에서 빛이 시작되는 위치
=
게임에서 밝기를 계산하는 위치
```

가 되도록 맞췄다.

---

## 16. Edit Mode Validator 안정화

Day 08 구현 중 Validator가 실패하는 문제가 있었다.

원인은 일부 검증이 런타임에서 준비되는 캐시에 의존하고 있었기 때문이다.

### IndoorBrightnessArea

Play Mode에서는 기존 `ActiveAreas` Registry를 사용한다.

Edit Mode에서는 Registry에 영역이 등록되지 않은 경우
현재 씬의 `IndoorBrightnessArea`를 직접 검색하도록 보완했다.

### PortableLightItem

다음 참조가 비어 있으면
같은 GameObject에서 다시 조회하도록 보완했다.

- `WorldItem`
- `BrightnessSource`
- 전체 `BrightnessSource[]`

이 변경으로 씬을 다시 연 직후 수행하는 에디터 Validator가
런타임 캐시에 덜 의존하도록 수정했다.

---

## 17. F1 휴대 조명 디버그 페이지

기존 Day 07 통합 디버그 시스템에
`PortableLightDebugPage`를 추가했다.

기본 페이지 순서:

```text
Player Debug
Brightness Debug
Portable Light Debug
```

휴대 조명 페이지에서 확인 가능한 정보:

```text
아이템 이름
State
Current Fuel
Max Fuel
Fuel %
Emitting
현재 Outdoor / Indoor Area
```

별도 Canvas를 만들지 않고
`DebugPageProvider` 구조를 그대로 사용한다.

---

## 18. 테스트 오브젝트

기존 `10_BrightnessTest` 영역에:

```text
PortableLightTest
├─ TestTorch
└─ TestLantern
```

을 생성한다.

두 아이템은 모두 기존 빠른 슬롯 시스템과 연결되는
`WorldItem + OneHand` 아이템이다.

---

## 19. Day 08 Validator

`Phase3Day8Validator`에서 다음 항목을 검사한다.

- TestTorch / TestLantern 존재
- OneHand 규칙
- Portable BrightnessSource
- 횃불 60 / 랜턴 120 연료
- 시작 소화 상태
- Outdoor / Indoor 위치 변경
- F1 Portable Light Debug Page
- 횃불 끝부분 Point Light 프로필
- 랜턴 정중앙 장거리 Spot Light 프로필

---

## 주요 생성 파일

```text
Assets/ProjectI/Scripts/Brightness/
├─ BrightnessEmissionShape.cs
└─ BrightnessSourceType.cs

Assets/ProjectI/Scripts/Lighting/
├─ PortableLightAim.cs
├─ PortableLightDebugPage.cs
├─ PortableLightItem.cs
└─ PortableLightState.cs

Assets/ProjectI/Editor/
├─ Phase3Day8Setup.cs
└─ Phase3Day8Validator.cs
```

---

## 주요 수정 파일

```text
Assets/ProjectI/Scenes/ExplorationOffice.unity

Assets/ProjectI/Scripts/Brightness/
├─ BrightnessManager.cs
├─ BrightnessSource.cs
└─ IndoorBrightnessArea.cs
```

---

## 저장소 확인 결과

최신 확인 커밋:

```text
edbbf8da4ced5d9fa259fe5296bd7327081ac75a
```

현재 커밋 메시지:

```text
8
```

Day 07 커밋 `68b22a81`보다 1개 커밋 앞선 상태이며
Day 08 관련 변경은 현재 한 커밋에 모여 있다.

저장소 파일 기준으로 다음 구조가 존재하는 것을 확인했다.

- 휴대 조명 점화 / 소화
- 연료 소비 및 자동 소화
- InventoryStorage 보관 중 광원·연료 일시 정지
- 여러 BrightnessSource 동시 제어
- Portable 위치 기반 Outdoor / Indoor 판정
- Cone 방향성 게임 밝기
- 랜턴 22m Spot Light
- 랜턴 화면 정중앙 조준
- 랜턴 근거리 보조광
- 횃불 불꽃 끝부분 Point Light
- Day 08 Validator

GitHub Commit Status에는 현재 연결된 CI 상태가 등록되어 있지 않다.

따라서 저장소 구조에서 즉시 확인되는 누락은 없지만
실제 Unity Editor Compile 및 Play Mode 동작은 로컬 실행 결과가 최종 기준이다.

---

## 로컬 최종 확인 항목

- Unity Console Compile Error 0개
- `Tools → Project I → Day 8 → Validate` PASS
- 횃불 / 랜턴 F 획득
- 1~6 또는 마우스 휠 슬롯 전환
- 좌클릭 점화 / 소화
- 횃불 연료 감소
- 랜턴 연료 감소
- 연료 0에서 자동 소화
- 다른 슬롯으로 이동 시 StoredPaused
- StoredPaused 동안 연료 감소 없음
- 다시 슬롯 선택 시 점화 상태 복구
- Q로 내려놓은 점화 조명 계속 발광
- 횃불 Point Light가 불꽃 끝부분에서 시작
- 횃불 주변 약 7m 조명 확인
- 랜턴 Spot Light가 정면으로 길게 조사
- 손에 든 랜턴 빔이 화면 정중앙을 따라감
- 랜턴을 바닥에 놓으면 카메라 추적 중지
- Outdoor에서 휴대 광원 밝기 계산
- 건물 내부에서 현재 Indoor Room 밝기에 포함
- F1 Portable Light Debug 페이지 확인

---

## 다음 단계

Day 08에서 플레이어가 직접 운반하고 배치할 수 있는
휴대 조명 시스템을 완성하기 위한 기반을 구축했다.

다음 조명 단계에서는
현재 `BrightnessSource` 구조를 그대로 활용하여
벽 횃불, 화로, 고정 조명 등
환경에 배치되는 고정 광원을 확장할 수 있다.
