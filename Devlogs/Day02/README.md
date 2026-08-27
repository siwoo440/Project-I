# Project I 개발 일지

## Day 2 — 디버그 환경 및 기준선 통합 검증

- 날짜: 2026-08-27
- 개발 단계: Phase 1 마무리
- 기준 커밋: `3d25eab4d601481dbd55930d316fdb66292f91e8`
- 기준 커밋 메시지: `a`

---

## 개발 목표

1일차에 구성한 프로젝트 기반을 실제 개발 기준선으로 정리하고,
Scene 흐름, 프로젝트 설정, 검증 도구 및 Windows Development Build 환경을 확정한다.

---

## 구현 내용

### 1. Phase 1 마무리 도구 추가

`Phase1Day2Finalize`를 추가하여 다음 항목을 자동 정리하도록 구성했다.

- Unity 기본 `Readme.asset` 제거
- 기본 `Assets/Scenes` 제거
- 기본 `Assets/TutorialInfo` 제거
- Build Settings 재설정
- Play Mode 시작 Scene을 `Boot`로 지정
- Product Name을 `Project I`로 지정
- Linear Color Space 적용
- Development Build 기본 활성화

### 2. Phase 1 Validator 확장

기존 검증 항목을 확장하여 다음 항목을 확인하도록 구성했다.

- 필수 Scene 존재
- Build Settings 순서
- Play Mode 시작 Scene
- Linear Color Space
- URP Render Pipeline
- Product Name
- Development Build 설정
- Unity 기본 템플릿 정리 상태

### 3. URP 검증 오류 수정

초기 Validator는 `GraphicsSettings.defaultRenderPipeline`만 검사하여
Quality Settings의 PC 전용 URP Override가 활성화된 프로젝트에서도 실패하는 문제가 있었다.

현재 활성 품질 설정을 반영하는 `GraphicsSettings.currentRenderPipeline`을 사용하고,
활성 Render Pipeline이 `UniversalRenderPipelineAsset`인지 검사하도록 수정했다.

### 4. 프로젝트 기준선 정리

Project I에서 실제 사용하는 Scene을 다음 구조로 유지한다.

1. `Boot`
2. `MainMenu`
3. `ExplorationOffice`

기본 흐름은 다음과 같다.

`Boot → MainMenu → ExplorationOffice`

---

## 검증 항목

Unity Editor에서 다음 메뉴를 실행한다.

`Tools → Project I → Validate Phase 1`

다음 8개 항목이 모두 PASS인지 확인한다.

- 필수 Scene
- Build Settings 순서
- Play Mode Boot 시작
- Linear Color Space
- URP Render Pipeline Asset
- Product Name
- Development Build 기본 설정
- Unity 기본 템플릿 정리

추가로 다음 항목을 직접 확인한다.

- Console Error 0개
- `Boot → MainMenu → ExplorationOffice` Scene 전환
- Windows Development Build 생성
- 빌드된 실행 파일의 Scene 전환

---

## 완료 기준

Validator 8개 항목 PASS,
Console Error 0개,
Editor와 Windows Development Build에서 Scene 흐름이 정상 동작하면
Phase 1을 완료한다.

---

## 다음 개발

Day 3부터 Phase 2를 시작한다.

- Input System 입력 래퍼 구성
- 1인칭 카메라
- 마우스 시점 조작
- 플레이어 이동 시스템 기반 구현
