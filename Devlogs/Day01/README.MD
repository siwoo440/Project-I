# 프로젝트 I — 1일차 개발 일지

> **단계**: Phase 0 — 프로젝트 셋업
> **목표**: 앞으로 모든 개발이 얹힐 프로젝트 뼈대(프로젝트·폴더·씬·매니저·Git) 구축

---

## 오늘의 목표
게임 로직 없이, **개발 기반 구조만** 세운다.

## 한 일

### 1. 유니티 프로젝트 생성
- **Unity 6000.3.9f1 (Unity 6)** + **URP(Universal Render Pipeline)** 템플릿으로 생성
- 프로젝트 위치: `F:\Project I`

### 2. 폴더 구조 구성
```
Assets/
├─ Scripts/
│  ├─ Managers/   (게임 전역 매니저)
│  ├─ Player/     (플레이어)
│  ├─ Dungeon/    (던전 생성)
│  ├─ Monster/    (몬스터 AI)
│  ├─ Items/      (아이템·인벤토리)
│  └─ UI/         (UI)
├─ Prefabs/
├─ ScriptableObjects/
├─ Scenes/
├─ Art/
├─ Audio/
└─ Data/
```

### 3. 씬 4개 생성 + 빌드 등록
- **Title(0) → Lobby(1) → Village(2) → Dungeon(3)** 순으로 Build Profiles에 등록
- 화면 흐름(기획서 PART 11.2.1)에 대응하는 씬 골격

### 4. 매니저 스켈레톤 스크립트 11종 작성
기획서 PART 13.3.1의 매니저 구성대로 빈 뼈대 클래스 생성 (`namespace ProjectI`):

| 스크립트 | 위치 | 역할 |
| :-- | :-- | :-- |
| GameManager | Managers | 전체 흐름·씬 전환 허브(싱글톤) |
| GameNetworkManager | Managers | 네트워킹(Steam P2P) ※Netcode 충돌 회피 명명 |
| SpawnManager | Managers | 랜덤 스폰 |
| LightSystem | Managers | 밝기 시스템 ★핵심 |
| EconomyManager | Managers | 경제·빚 상환 |
| SaveManager | Managers | 세이브(일차 단위) |
| AudioManager | Managers | 3D 오디오·소음 |
| UIManager | Managers | HUD·메뉴 |
| DungeonGenerator | Dungeon | 절차적 던전 생성 |
| MonsterAI | Monster | 몬스터 행동·탐지 |
| InventorySystem | Items | 인벤토리·무게 |

- 현재는 로직 없이 `Awake()`에서 초기화 로그만 출력(컴파일·구조 확인용)
- `GameManager`는 싱글톤 + `LoadScene()` 씬 전환 메서드 보유

### 5. 버전 관리(Git) 설정
- `.gitignore` (Unity 생성 폴더 Library/Temp/Logs 등 제외)
- `.gitattributes` (Git LFS — 이미지·모델·오디오 등 대용량 바이너리 관리)
- Git 저장소 초기화 + LFS 활성화
- GitHub 원격 연결: https://github.com/siwoo440/Project-I

## 결과
- 실행되는 빈 프로젝트 + 씬 전환 골격 + 매니저 뼈대 완성
- 이후 모든 시스템이 이 구조 위에 얹힘

## 다음 (2일차 예정)
- **플레이어 컨트롤러(1인칭)** — 이동/마우스룩/달리기/점프/앉기 + 체력·스태미너
