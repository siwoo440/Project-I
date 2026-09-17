# 35일차 — 부트·메인 메뉴·서버 목록·설정·일시정지

## 이번 일차 목표

게임을 켜면 바로 마을이 아니라 **로고 → 메인 메뉴**가 나오게 한다. 메뉴의 "이어하기" 아래 **서버**를 누르면 카메라가 옮겨 가며 서버 목록 창이 열린다.

```text
Boot      "PROJECT I" 로고 1.6초 → MainMenu
MainMenu  배경: 밤의 공업 도시 거리 (카메라가 천천히 흔들림)
  ├ 이어하기 (N일차)   저장이 없으면 꺼짐 → 00_WagonPersistent → 저장 복원
  ├ 서버               카메라가 전신국 앞으로 이동 → 서버 목록 창
  ├ 새 게임            저장이 있으면 확인 → 기존 저장을 SavesArchive로 옮기고 1일차
  ├ 설정
  └ 종료
게임 중 Esc  계속하기 · 설정 · 메인 메뉴로 · 게임 종료
```

## 0. 확인한 기존 상태

- `Boot`·`MainMenu` 씬은 1일차에 만든 흐름 확인용 빈 씬(카메라 + 영어 OnGUI 버튼)이었다.
- Build 순서는 이미 Boot → MainMenu → 00_WagonPersistent → 01_Office → 02_TestDungeon.
- 에디터 Play는 `00_WagonPersistent`에서 시작하도록 고정돼 있어 두 씬을 거치지 않았다.

## 1. 공통 화면 모양 — `RetroUi`

어두운 바탕 · 주황 글자 · 모서리 꺾쇠 · 빨간 테두리/스크롤바의 단말기 느낌 UI를 코드로 만든다 (uGUI).

- 캔버스(1920×1080 기준 비율), 테두리, 꺾쇠, 글자, 글자 버튼(마우스를 올리면 `> ` 표시), 테두리 버튼, 입력창, 슬라이더, 세로 스크롤 목록
- 글꼴: OS 글꼴 D2Coding → 맑은 고딕 순 (한글 표시)
- 씬에 UI 입력 처리기가 없으면 새 Input System 모듈로 만든다.

## 2. 메인 메뉴 — `MainMenuSceneController` · `MenuCameraRig`

- 메뉴 순서: 이어하기 · **서버** · 새 게임 · 설정 · 종료
- 이어하기는 저장 파일을 읽어 `이어하기 (N일차)`로 표시한다.
- 새 게임은 저장을 지우지 않고 `persistentDataPath/ProjectI/SavesArchive/날짜_시각`으로 옮긴다. 옮기기에 실패하면 시작하지 않는다.
- 카메라 자세 2개(전경 / 서버) 사이를 1.6초 동안 부드럽게 이동하고, 도착하면 창을 연다. 멈춰 있을 때는 조금씩 흔들린다.
- Esc: 확인 창 → 설정 → 서버 목록 순서로 닫는다.

## 3. 서버 목록 — `ServerListPanel`

| 영역 | 내용 |
| --- | --- |
| 위 | 제목 "서버" · 이름 검색(초록) · `도전 원정 포함 [X]` · `정렬: 전 세계 ▾` · `[ 새로고침 ]` |
| 가운데 | 빨간 테두리 목록 — 이름(도전 원정은 `[도전]`) · `2 / 4` · 지역·지연(색 구분) · `참가` |
| 아래 | 상태 줄 · `메뉴로 돌아가기` |

- 정렬: 전 세계 / 가까운 순 / 인원 많은 순 / 이름순
- 가득 찬 서버는 버튼이 꺼진다.
- `IServerListProvider`로 목록 공급을 분리했다. 지금은 `SampleServerListProvider`(예시 목록)이고, 참가하면 "멀티플레이 일차에 연결" 안내만 한다. 멀티플레이 일차에 Steam 로비 공급자로 바꾼다.

## 4. 설정 — `GameSettings` · `SettingsPanel`

- 마우스 감도 배율(0.2~3, `PlayerLook`에 적용) · 전체 음량 · 전체 화면 · 해상도 · 수직 동기화
- PlayerPrefs에 저장하고 게임 시작 시 적용한다. 해상도·전체 화면은 빌드한 게임에서만 바뀐다.
- 메인 메뉴와 일시정지 창에서 같은 창을 쓴다.

## 5. 일시정지 — `PauseMenu`

- 핵심 루트(`===ProjectI Core===`)에 붙고, 플레이어가 있을 때만 Esc로 열린다.
- `PlayerControlLock`으로 이동·상호작용을 멈추고 커서를 보인다. 협동 게임이라 시간은 멈추지 않는다.
- 판매·구매 창이 열려 있으면 Esc는 기존처럼 그 창을 닫는다.
- 메인 메뉴로 / 종료: 사무소면 체크포인트를 저장하고, 원정·이동 중이면 "출발 전 저장부터 다시 시작" 경고 후 나간다.

## 6. 부트 — `BootSceneController`

검은 화면에 로고가 나타났다 사라진 뒤 `SceneFlowManager.LoadMainMenu()`로 넘어간다.

`SceneFlowManager`에 `TryGetContinueDay` · `ContinueGame` · `StartNewGame` · `QuitGame`을 추가했다.

## 7. 배치 도구

```text
Project I > Day 35 > Build Main Menu Scene
Project I > Play From Boot (Main Menu)      에디터 Play 시작 씬 전환 (기본: 마을에서 바로 시작)
```

- 메인 메뉴 씬을 비우고 34일차 마을 제작 도구로 밤거리를 짓는다.
  - 잡화점 · 전신국(실내 접수대와 전신 단말기) · 화물 창고
  - 가스등 · 우체통 · 게시판 · 남쪽 벽돌 담
  - 뒤쪽 연립 건물 윤곽 · 제철소 · 연기 나는 굴뚝 3개
- 달빛 · 어두운 환경광 · 매연 안개, 카메라 리그, UI 입력 처리기, 메뉴 제어기를 배치한다.
- 카메라와 목표 사이가 막혔는지 확인하고, Build 씬 순서를 Boot → MainMenu 우선으로 맞춘다.

## 검증

```text
메뉴 씬 구성   완료 보고 · 건물 3채 · 조명 13개 · 렌더러 3204개
              전경 시선 열림 · 서버 시선 열림
              Build 씬: Boot → MainMenu → 00_WagonPersistent → 01_Office → 02_TestDungeon
실행 흐름     Boot 시작 → MainMenu → 이어하기 → 00_WagonPersistent + 01_Office
              → Snapshot 전체 복구 완료 (Day=5, Items=17) · 예외 없음
컴파일        에디터 밖 dotnet 빌드 — 게임·에디터 오류 0
플레이        에디터에서 직접 확인 (서버 창·설정·Esc 창)
```

## 남은 것

- 멀티플레이 일차: Steam 로비 목록·참가를 `IServerListProvider`로 연결
- 메뉴 음악·효과음 (AudioMixer), 게임패드 메뉴 이동
- 하네스 SH · BS 및 회귀 실행 (하네스 확인용 빌드 목록에 34·35일차 파일 추가 필요)
