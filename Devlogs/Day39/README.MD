\# 39일차 개발 일지



\## 작업 주제



Canvas 기반 플레이어 HUD 구현과 기존 OnGUI 디버그 화면 정리



\## 작업 목표



\- 기존 OnGUI 플레이어 상태 UI를 Canvas HUD로 교체

\- 체력과 스태미너 게이지 구현

\- 던전 시간과 남은 제한시간 표시

\- 현재 밝기와 밝기 단계 표시

\- 중앙 조준점 구현

\- 상호작용 안내 문구 구현

\- 기본 조작 안내 문구 구현

\- TextMeshPro 한글 폰트 적용

\- 기존 디버그 UI 기본 숨김 처리

\- 기능별 디버그 UI 단축키 분리

\- 다양한 해상도에서 HUD 위치 유지



\## 구현 내용



\### PlayerHUD 구현



`PlayerHUDCanvas`와 `PlayerHUD` 스크립트를 추가했다.



PlayerHUD는 다음 시스템의 현재 상태를 읽어 Canvas UI에 표시한다.



\- PlayerController

\- PlayerInteractor

\- DungeonTimeSystem

\- LightSystem



플레이어 또는 게임 시스템의 생성 순서가 달라져도 정상적으로 연결되도록 Scene 참조를 주기적으로 자동 검색한다.



\### 체력 UI



현재 체력과 최대 체력을 숫자로 표시한다.



체력 상태는 가로 게이지로 함께 표시하며, 체력이 낮아질수록 게이지가 경고색에 가까워지도록 구성했다.



PlayerController에 다음 읽기 전용 상태값을 추가했다.



\- CurrentHealth

\- MaxHealth

\- CurrentStamina

\- MaxStamina



\### 스태미너 UI



현재 스태미너와 최대 스태미너를 숫자와 가로 게이지로 표시한다.



달리기로 스태미너가 소모되면 게이지가 감소하고, 회복 대기시간이 끝나면 다시 증가한다.



스태미너가 낮아지면 게이지 색상이 경고색으로 변경된다.



\### 던전 시간 UI



DungeonTimeSystem의 게임시간과 남은 현실 제한시간을 표시한다.



표시 형식은 다음과 같다.



\- 던전 시간 06:00

\- 남은 시간 18:00



남은 제한시간이 60초 이하가 되면 시간 글자가 붉은 경고색으로 변경된다.



\### 밝기 UI



LightSystem의 CurrentBrightness와 Stage를 Canvas에 표시한다.



표시 형식은 다음과 같다.



\- 밝기 0 / 100

\- 완전한 어둠



현재 밝기가 높아질수록 밝기 글자 색상이 회색에서 노란색에 가까워지도록 구성했다.



\### 조준점과 상호작용 안내



화면 중앙에 Canvas 기반 조준점을 추가했다.



PlayerInteractor에 CurrentPrompt 읽기 전용 속성을 추가했다.



플레이어가 문, 레버, 보물, 상자 등의 상호작용 대상을 바라볼 때만 조준점 아래에 상호작용 문구가 표시된다.



상호작용 대상을 바라보지 않으면 안내 문구가 자동으로 숨겨진다.



\### 기본 조작 안내



화면 아래쪽 중앙에 다음 기본 조작법을 표시했다.



\- WASD 이동

\- Shift 달리기

\- Space 점프

\- Ctrl 앉기

\- E 상호작용

\- Q 아이템 버리기

\- R 아이템 사용

\- F 광원 켜기 및 끄기



\### Canvas 해상도 대응



PlayerHUDCanvas의 Canvas Scaler를 다음과 같이 설정했다.



\- UI Scale Mode: Scale With Screen Size

\- Reference Resolution: 1920 × 1080

\- Screen Match Mode: Match Width Or Height

\- Match: 0.5



1920×1080, 1600×900, 1280×720 해상도에서 HUD가 화면 밖으로 벗어나지 않도록 Anchor와 Pivot을 설정했다.



\### TextMeshPro 한글 폰트



기본 TextMeshPro 폰트에 한글 글리프가 없어 한글이 사각형이나 빈칸으로 출력되는 문제를 수정했다.



한글을 지원하는 폰트 파일로 TextMeshPro Font Asset을 생성했다.



한글 Font Asset은 다음 설정을 사용했다.



\- Atlas Population Mode: Dynamic

\- Atlas Width: 2048

\- Atlas Height: 2048

\- Multi Atlas Textures: 활성화

\- Clear Dynamic Data On Build: 해제



PlayerHUDCanvas 내부의 모든 TextMeshProUGUI 컴포넌트에 한글 Font Asset과 동일한 Material을 연결했다.



체력, 스태미너, 던전 시간, 밝기, 상호작용 안내, 기본 조작 안내에서 한글이 정상적으로 표시되도록 수정했다.



기본값으로 남아 있던 `New Text` 문구도 각각 올바른 UI 문구로 변경했다.



\### 기존 OnGUI 디버그 화면 정리



새 Canvas HUD와 기존 OnGUI 화면이 동시에 출력되어 글자와 패널이 겹치는 문제를 수정했다.



새 PlayerHUDCanvas는 항상 표시하고, 기존 OnGUI 정보는 Scene 시작 시 모두 숨기도록 변경했다.



기존 디버그 화면은 삭제하지 않고 기능키를 이용해 필요한 항목만 확인할 수 있도록 유지했다.



\### DebugUIToggleController



기존 OnGUI 디버그 UI를 통합 관리하는 DebugUIToggleController를 추가했다.



DebugUIToggleController는 새 Input System의 Keyboard.current를 사용한다.



Action Map은 사용하지 않는다.



Scene 시작 시 다음 디버그 UI는 모두 숨김 상태로 초기화된다.



\- 플레이어 정보

\- 인벤토리와 마차 정보

\- 자동 스폰 정보

\- 캠페인 정보

\- 수직 슬라이스 검증 패널

\- 성능 검사 패널



\### 디버그 UI 단축키



기능별 단축키는 다음과 같이 구성했다.



| 단축키 | 기능 |

| --- | --- |

| F1 | 기존 플레이어, 밝기, 시간, 상태 이상, 상호작용 디버그 정보 표시 및 숨김 |

| F2 | 기존 인벤토리, 핫바, 마차 적재 정보 표시 및 숨김 |

| F3 | 25일차 수직 슬라이스 검증 패널 표시 및 숨김 |

| F4 | 몬스터, 보물, 함정, 미믹, 스토커, 고스트, 웃는 석상 스폰 정보 표시 및 숨김 |

| F7 | 캠페인 날짜, 골드, 빚 정보 표시 및 숨김 |

| F8 | 수직 슬라이스 즉시 재검증 |

| F9 | 던전 1회 재생성 |

| F10 | 26일차 성능 및 재생성 검사 패널 표시 및 숨김 |

| F11 | 던전 자동 10회 반복 생성 검사 |



기존 Scene 이동이나 테스트 기능과 충돌할 가능성이 있는 F5와 F6은 디버그 UI 단축키로 사용하지 않았다.



\### 기존 시스템 연결



다음 기존 OnGUI 메서드에 DebugUIToggleController 표시 조건을 연결했다.



\- PlayerController

\- PlayerInteractor

\- PlayerStatusEffectSystem

\- LightSystem

\- DungeonTimeSystem

\- InventorySystem

\- Wagon

\- MonsterSpawnManager

\- TreasureSpawnManager

\- TrapSpawnManager

\- StalkerSpawnManager

\- GimmickMonsterSpawnManager

\- CampaignManager



게임 결과와 직접 관련된 다음 화면은 디버그 단축키와 관계없이 계속 표시되도록 유지했다.



\- 플레이어 최종 사망 화면

\- 던전 제한시간 실패 화면

\- 마차 탈출 성공 화면

\- RunResultManager 결과 화면

\- 캠페인 성공 및 실패 화면



\### Hierarchy 정리



Dungeon Scene의 UI와 디버그 관리 구조를 다음과 같이 정리했다.



```text

UI

└─ PlayerHUDCanvas

&#x20;  ├─ CrosshairText

&#x20;  ├─ DungeonInfoPanel

&#x20;  │  ├─ TimeText

&#x20;  │  └─ BrightnessText

&#x20;  ├─ PlayerStatusPanel

&#x20;  │  ├─ HealthLabel

&#x20;  │  ├─ HealthValueText

&#x20;  │  ├─ HealthBarBackground

&#x20;  │  │  └─ HealthBarFill

&#x20;  │  ├─ StaminaLabel

&#x20;  │  ├─ StaminaValueText

&#x20;  │  └─ StaminaBarBackground

&#x20;  │     └─ StaminaBarFill

&#x20;  ├─ InteractionText

&#x20;  └─ ControlGuideText



Managers

└─ DebugManagers

&#x20;  └─ DebugUIToggleController
PlayerHUDCanvas와 DebugUIToggleController에는 Collider와 Rigidbody를 사용하지 않는다.

테스트 결과
Scene 시작 시 기존 OnGUI 디버그 UI 숨김 정상
새 Canvas HUD 표시 정상
체력 수치와 게이지 갱신 정상
스태미너 소모와 회복 표시 정상
던전 시간과 남은 제한시간 표시 정상
남은 시간 60초 이하 경고색 표시 정상
현재 밝기와 밝기 단계 표시 정상
중앙 조준점 표시 정상
상호작용 대상 감지 문구 표시 정상
상호작용 대상이 없을 때 문구 숨김 정상
TextMeshPro 한글 표시 정상
기본 New Text 문구 제거 정상
F1 플레이어 디버그 UI 전환 정상
F2 인벤토리와 마차 디버그 UI 전환 정상
F3 수직 슬라이스 검증 패널 전환 정상
F4 자동 스폰 디버그 UI 전환 정상
F7 캠페인 디버그 UI 전환 정상
F8 수직 슬라이스 재검증 정상
F9 던전 1회 재생성 정상
F10 성능 검사 패널 전환 정상
F11 자동 반복 생성 검사 정상
디버그 UI 사이의 단축키 충돌 없음
플레이어 사망과 던전 종료 화면 유지 정상
1920×1080 해상도 HUD 배치 정상
1600×900 해상도 HUD 배치 정상
1280×720 해상도 HUD 배치 정상
다음 작업

40일차에는 기존 OnGUI 인벤토리와 하단 핫바를 Canvas 기반 정식 UI로 교체한다.

다음 기능을 구현할 예정이다.

인벤토리 슬롯 표시
현재 선택 슬롯 강조
아이템 이름 표시
아이템 무게 표시
전체 무게와 제한 무게 표시
두손 운반 상태 표시
아이템 획득과 버리기 갱신
가방 장착에 따른 슬롯 증가 표시
기존 OnGUI 인벤토리 완전 교체
