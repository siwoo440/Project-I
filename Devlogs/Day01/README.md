# Project I 개발 일지

## Day 1 — 프로젝트 기반 구성

- 날짜: 2026-08-27
- 커밋: `86f22eb3390f64175239eb18aa40eff8f496241a`
- 커밋 제목: `1일차 : 프로젝트 기반 구성`
- 개발 단계: Phase 1 — 프로젝트 기반 구성

---

## 개발 목표

본격적인 게임 기능 구현 전에 Project I의 Unity 프로젝트 기준 구조를 확정하고,
Scene 전환, 공통 Manager, 서비스, 로그, 검증 및 빌드 기반을 구성한다.

---

## 구현 내용

### 1. Unity 프로젝트 기반

- Unity `6000.3.21f1` 기준 프로젝트 구성
- Universal Render Pipeline(URP) 사용
- `ProjectI` 네임스페이스 기준 적용
- 프로젝트용 폴더 구조 생성
- Unity 패키지 및 프로젝트 설정 저장

### 2. Git 저장소 설정

- `.gitignore` 추가
- `.gitattributes` 추가
- Git LFS 대상 확장자 정의
- `.editorconfig` 추가
- C# Allman Style 기준 설정

### 3. 기본 Scene 구성

다음 Scene을 생성하고 Build Settings에 순서대로 등록했다.

1. `Boot`
2. `MainMenu`
3. `ExplorationOffice`

기본 실행 흐름은 다음과 같다.

`Boot → MainMenu → ExplorationOffice`

### 4. 게임 공통 구조

- `GameManager`
- `GameState`
- `ProjectBootstrap`
- `ProjectServices`
- `IProjectService`
- `GameEvents`
- `SceneFlowManager`

게임 전체 상태와 Scene 전환, 공통 서비스 등록을 이후 기능에서 확장할 수 있도록
기본 골격을 구성했다.

### 5. Scene 제어 구조

- `BootSceneController`
- `MainMenuSceneController`
- `ExplorationOfficeSceneController`

각 Scene별 최소 제어 구조를 분리하여 Scene 흐름을 검증할 수 있도록 구성했다.

### 6. 로그 및 개발 설정 기반

- `ProjectLog`
- `ProjectDevelopmentSettings`
- `ProjectDevelopmentSettings.asset`

일반 로그, 경고, 오류에 `[Project I]` 접두사를 적용하고
개발 환경에서 상세 로그 사용 여부를 설정할 수 있도록 구성했다.

### 7. Phase 1 에디터 도구

- `Phase1ProjectSetup`
- `Phase1Validator`
- `Phase1WindowsBuild`

Unity Editor에서 Phase 1 환경을 구성하고 다음 항목을 검증할 수 있도록 했다.

- 필수 Scene 존재
- Build Settings Scene 순서
- Linear Color Space
- URP Render Pipeline Asset

Windows 64비트 Development Build를 생성하는 메뉴도 추가했다.

---

## 현재 확인 상태

GitHub 최신 커밋 기준으로 다음 항목을 확인했다.

- Unity 버전: `6000.3.21f1`
- URP 패키지: `17.3.0`
- Input System 패키지: `1.20.0`
- `Boot → MainMenu → ExplorationOffice` Build Settings 등록 확인
- Phase 1 핵심 코드 및 프로젝트 설정 파일 존재 확인

GitHub Actions/CI가 아직 구성되어 있지 않으므로
실제 Unity Editor 컴파일 및 Windows 실행 파일 빌드는 로컬 Unity에서 최종 확인한다.

---

## 다음 개발

Day 2에서는 다음 내용을 진행한다.

- Unity Console 오류 확인 및 수정
- Phase 1 Validator 전체 PASS 확인
- Scene 전환 통합 테스트
- 로그 및 개발 설정 검증
- 불필요한 기본 Unity 템플릿 파일 정리
- Windows Development Build
- 실행 파일에서 `Boot → MainMenu → ExplorationOffice` 최종 확인

Day 2 완료 시 Phase 1을 종료한다.
