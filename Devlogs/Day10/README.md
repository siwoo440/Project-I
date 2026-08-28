# Project I 개발 일지

## Day 10 — 시간대 및 자연광 변화 시스템 구현

- 날짜: 2026-08-28
- 개발 단계: Phase 3 — 조명·화염·전력 시스템
- 기준 커밋: `b5e6e3a3e09a4b1418fbba7799b602829a83c775`
- 현재 커밋 메시지: `10`
- 이전 커밋: `86750567b5729bbc5772925a97dae398d2697658`

---

## 개발 목표

Day 09까지 완성한 Fixed / Portable 광원과 F1 광원 진단 시스템 위에
24시간 게임 시간과 태양·달 자연광 변화를 연결한다.

이번 일차의 핵심 목표:

- 0~24시간 게임 시간 진행
- 새벽 / 낮 / 저녁 / 밤 시간대 구분
- 시간대에 따른 태양 게임 밝기 변화
- 시간대에 따른 달 게임 밝기 변화
- 기존 `NaturalLightController`와 시간 시스템 연결
- 태양 / 달 Directional Light 생성 및 시각 강도 동기화
- 태양 / 달 방향을 시간에 따라 회전
- 기존 Directional Light 중복 비활성화
- 실내 자연광 제외 규칙 유지
- F1 `Time / Natural Light` 페이지 추가
- Day 10 자동 Setup / Validator 추가

---

## 1. 게임 시간 시스템

새 `GameTimeController`를 추가했다.

기본 설정:

```text
Start Hour : 12:00
Real 1 sec : Game 1 min
```

게임 시간은 매 프레임 진행되고
24시간을 넘으면 다시 0시부터 순환한다.

```text
23:59
→ 00:00
→ 다음 하루 진행
```

외부 시스템과 디버그 기능에서 사용할 수 있도록
다음 기능도 제공한다.

```text
SetTime()
SetPaused()
SetRealSecondsPerGameMinute()
```

---

## 2. 시간대 분류

새 `GameTimePhase`를 추가했다.

```text
Dawn
Day
Dusk
Night
```

기본 시간대:

```text
05:00 ~ 07:00
→ Dawn

07:00 ~ 18:00
→ Day

18:00 ~ 20:00
→ Dusk

20:00 ~ 05:00
→ Night
```

시간대 판정은 `GameTimeProfile.EvaluatePhase()`에서 처리한다.

---

## 3. 게임 시간 프로필

새 `GameTimeProfile`에서
시간에 따른 태양·달 게임 밝기 값을 계산한다.

태양 최대 밝기:

```text
0.65
```

달 최대 밝기:

```text
0.08
```

시간 값은 `NormalizeHour()`를 통해
항상 0 이상 24 미만 범위로 순환한다.

---

## 4. 태양 밝기 변화

태양 밝기는 시간에 따라 다음 흐름으로 변화한다.

```text
05:00
→ 0.00

05:00 ~ 07:00
→ 0.00 → 0.40

07:00 ~ 09:00
→ 0.40 → 0.65

09:00 ~ 17:00
→ 0.65

17:00 ~ 20:00
→ 0.65 → 0.00

20:00 ~ 05:00
→ 0.00
```

값이 순간적으로 바뀌지 않고
시간 구간 안에서 선형 보간되도록 구성했다.

---

## 5. 달빛 변화

달빛은 태양과 반대 흐름을 사용한다.

```text
20:00 ~ 05:00
→ 0.08

05:00 ~ 07:00
→ 0.08 → 0.00

07:00 ~ 18:00
→ 0.00

18:00 ~ 20:00
→ 0.00 → 0.08
```

따라서 밤 외부에도 약한 자연광이 남는다.

---

## 6. 기존 NaturalLightController 연결

Day 07부터 사용한 기존:

```text
NaturalLightController
```

를 새 시간 시스템에서 그대로 사용한다.

현재 시간에 맞춰:

```text
SetSunBrightness()
SetMoonBrightness()
```

를 호출한다.

즉 기존 `BrightnessManager`의 Outdoor 계산 구조를 교체하지 않고
시간 시스템이 자연광 입력값만 갱신하는 방식으로 연결했다.

---

## 7. 태양 Directional Light

실제 화면의 낮 변화를 표현하기 위해
새 태양 Directional Light를 생성한다.

```text
Day10_SunDirectionalLight
```

기본 화면 최대 강도:

```text
1.20
```

게임용 태양 밝기를 최대값 기준 0~1로 변환한 뒤
실제 Unity Light 강도에 적용한다.

태양 게임 밝기가 사실상 0이 되면:

```text
Sun Light
→ OFF
```

처리한다.

---

## 8. 달 Directional Light

밤 화면을 위한 별도 Directional Light도 생성한다.

```text
Day10_MoonDirectionalLight
```

기본 화면 최대 강도:

```text
0.22
```

달 게임 밝기가 0이면 비활성화하고
밤이 되면 현재 달빛 값에 맞춰 활성화한다.

태양과 달은 서로 다른 색을 사용한다.

```text
Sun
→ 따뜻한 색

Moon
→ 차가운 색
```

---

## 9. 태양과 달 방향 회전

`GameTimeController`가 현재 시간을 기준으로
태양과 달의 Directional Light 회전을 계속 갱신한다.

태양은:

```text
06:00
→ 하루 회전 기준 시작

시간 진행
→ Directional Light 회전
```

구조로 움직인다.

달은 태양과 반대편을 기준으로 약 180도 차이를 두고 회전한다.

천문학적으로 정확한 궤도 계산이 아니라
게임 플레이용 기본적인 주야 시각 변화에 초점을 맞췄다.

---

## 10. 기존 Directional Light 중복 제거

Day 10 Setup에서
새 태양·달 이외의 기존 Directional Light를 찾아 비활성화한다.

목적:

```text
기존 환경 Directional Light
+
새 태양 Directional Light
+
새 달 Directional Light
```

가 동시에 화면을 밝히는 중복 문제를 방지하기 위함이다.

Unity의:

```text
RenderSettings.sun
```

도 Day 10 태양 Light로 연결한다.

---

## 11. Day 10 시간 시스템 Hierarchy

자동 Setup 후 주요 구조:

```text
===Day10 Time Of Day===
├─ Day10_SunDirectionalLight
└─ Day10_MoonDirectionalLight
```

루트에는:

```text
GameTimeController
```

가 연결된다.

자동 적용 완료 마커도 별도로 생성한다.

---

## 12. 실내 자연광 규칙 유지

기존 밝기 규칙은 유지한다.

외부:

```text
Natural Light
+
Fixed / Portable Local Light
=
Final Brightness
```

실내:

```text
Natural Brightness = 0

Fixed / Portable Local Light
=
Final Brightness
```

Day 10 Validator에서도
낮 시간대에 실내 중심을 샘플링했을 때
`NaturalBrightness == 0`인지 확인하도록 구성했다.

---

## 13. F1 시간·자연광 디버그 페이지

새 F1 페이지:

```text
Time / Natural Light
```

를 추가했다.

SortOrder:

```text
60
```

으로 설정되어
기존 광원 계산 페이지 다음에 표시된다.

확인 가능한 정보:

```text
Time
Phase
Paused
1 Game Minute

Sun Brightness
Sun Visual Intensity / ON/OFF

Moon Brightness
Moon Visual Intensity / ON/OFF

Natural Total
```

---

## 14. F1 표시 예시

```text
Time : 18:42
Phase : Dusk
Paused : False
1 Game Minute : 1.00s

Sun Brightness : 0.xxx
Sun Visual : x.xx / ON

Moon Brightness : 0.xxx
Moon Visual : x.xx / ON

Natural Total : 0.xxx
```

Day 09의:

```text
Light Calculation
```

과 함께 사용하면
현재 자연광과 개별 Fixed / Portable 광원의 최종 합산을 비교할 수 있다.

---

## 15. Day 10 자동 Setup

새 파일:

```text
Phase3Day10Setup.cs
```

자동 Setup에서 수행하는 작업:

- `ExplorationOffice.unity` 열기
- 기존 `Player` 검색
- 기존 `NaturalLightController` 검색
- `===Day10 Time Of Day===` 루트 생성
- 태양 Directional Light 생성
- 달 Directional Light 생성
- 기존 기타 Directional Light 비활성화
- `RenderSettings.sun` 연결
- `GameTimeController` 추가 및 설정
- 플레이어에 `NaturalLightDebugPage` 추가
- 완료 마커 생성
- 씬 저장
- Validator 실행

수동 적용 경로:

```text
Tools
→ Project I
→ Day 10
→ Apply Time + Natural Light
```

---

## 16. Day 10 Validator

새 파일:

```text
Phase3Day10Validator.cs
```

주요 검증 항목:

```text
Time Profile
Core Components
12:00 Start / 1s = 1 Game Minute
Sun + Moon Directional Lights
Day / Night Visual Sync
Indoor Natural Light Exclusion
F1 Time / Natural Light Page
```

검증 과정에서:

```text
12:00
```

과:

```text
00:00
```

을 직접 적용해
낮 / 밤 자연광과 실제 Directional Light 활성 상태가 함께 바뀌는지 확인한다.

검증 후 시작 시간으로 복원한다.

수동 실행:

```text
Tools
→ Project I
→ Day 10
→ Validate
```

---

## 주요 생성 파일

```text
Assets/ProjectI/Scripts/TimeOfDay/
├─ GameTimeController.cs
├─ GameTimePhase.cs
└─ GameTimeProfile.cs

Assets/ProjectI/Scripts/Diagnostics/
└─ NaturalLightDebugPage.cs

Assets/ProjectI/Editor/
├─ Phase3Day10Setup.cs
└─ Phase3Day10Validator.cs
```

새 `TimeOfDay` 폴더와 신규 C# 파일의 `.meta`도 추가됐다.

---

## 주요 씬 변경

```text
Assets/ProjectI/Scenes/ExplorationOffice.unity
```

씬에는 Day 10 시간 시스템,
태양·달 Directional Light,
F1 시간·자연광 디버그 페이지 연결이 반영됐다.

---

## 저장소 확인 결과

확인한 최신 커밋:

```text
b5e6e3a3e09a4b1418fbba7799b602829a83c775
```

현재 커밋 메시지:

```text
10
```

이 커밋은 Day 09 커밋:

```text
86750567b5729bbc5772925a97dae398d2697658
```

보다 정확히 1개 커밋 앞선 상태다.

GitHub 비교 기준 Day 10 변경 파일은
시간 시스템, F1 자연광 디버그, Day 10 Setup / Validator,
`ExplorationOffice.unity`에 집중되어 있다.

확인된 Day 10 변경 파일:

```text
Assets/ProjectI/Editor/Phase3Day10Setup.cs
Assets/ProjectI/Editor/Phase3Day10Validator.cs
Assets/ProjectI/Scenes/ExplorationOffice.unity
Assets/ProjectI/Scripts/Diagnostics/NaturalLightDebugPage.cs
Assets/ProjectI/Scripts/TimeOfDay.meta
Assets/ProjectI/Scripts/TimeOfDay/GameTimeController.cs
Assets/ProjectI/Scripts/TimeOfDay/GameTimePhase.cs
Assets/ProjectI/Scripts/TimeOfDay/GameTimeProfile.cs
```

각 신규 스크립트의 `.meta`도 함께 포함되어 있다.

GitHub Commit Status에는 현재 등록된 CI 상태가 없다.

저장소 변경 범위에서는
Day 10과 무관한 별도 파일 변경이나 즉시 확인되는 구조적 누락은 확인되지 않았다.

실제 Unity Editor Compile 및 Play Mode 성공 여부는
GitHub 저장소만으로 확인할 수 없으므로 로컬 실행 결과가 최종 기준이다.

---

## 로컬 최종 확인 항목

- Unity Console Compile Error 0개
- `Tools → Project I → Day 10 → Validate` PASS
- 시작 시간이 12:00인지 확인
- 현실 1초마다 게임 시간 약 1분 진행
- 24시간 이후 00:00으로 순환
- 06:00 새벽 태양 밝기 증가
- 12:00 낮 태양 최대 밝기
- 19:00 저녁 태양 감소 / 달빛 증가
- 00:00 태양 OFF / 달 ON
- 태양 Directional Light 시간에 따라 회전
- 달 Directional Light 시간에 따라 회전
- 낮과 밤 실제 화면 조명 전환
- Outdoor 자연광 값 변화
- Indoor Natural Brightness 0 유지
- F1 `Time / Natural Light` 페이지 표시
- Day 09 `Light Calculation`과 최종 밝기 비교
