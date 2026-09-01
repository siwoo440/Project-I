# Project H — Phase 0 Day 2 개발 일지

- 날짜: 2026-08-31
- 단계: Phase 0 / Day 2
- 기준 커밋: `fd77f905f9b86d9fffb3a1aa004514ec9f91de0c`
- Unity: 6000.3.21f1

---

## 목표

캐릭터, 몬스터, 던전, 아이템 데이터를 코드에 직접 고정하지 않고 Unity 데이터 에셋으로 관리하고, 고유 ID를 통해 런타임에서 조회할 수 있는 공통 데이터 시스템의 기반을 구축한다.

---

## 완료 작업

### 데이터 구조

다음 ScriptableObject 데이터 타입을 추가했다.

- `CharacterData`
- `MonsterData`
- `DungeonData`
- `ItemData`
- `ProjectHDataCatalog`

모든 데이터 타입은 `IDataRecord`를 통해 공통 고유 ID 규칙을 사용한다.

### 데이터 Registry

`DataRegistry<T>`를 추가했다.

- ID 기반 데이터 등록
- ID 기반 조회
- 빈 ID 검증
- 중복 ID 검증
- null 데이터 검증
- Registry 재구성 시 기존 데이터 초기화

### DataManager

`DataManager`를 추가했다.

- Character Registry 관리
- Monster Registry 관리
- Dungeon Registry 관리
- Item Registry 관리
- 데이터 카탈로그 기반 초기화
- 검증 오류 수집 및 Console 출력
- ID 기반 데이터 조회 API 제공

주요 조회 API:

- `GetCharacter(string id)`
- `GetMonster(string id)`
- `GetDungeon(string id)`
- `GetItem(string id)`

### GameManager 연동

기존 `GameManager`에 `DataManager` 의존성을 연결했다.

- `RequireComponent`를 통한 DataManager 보장
- GameManager 초기화 과정에서 DataManager 초기화
- 데이터 초기화 실패 시 GameManager 초기화 중단
- `GameManager.Data`를 통한 데이터 시스템 접근

### Bootstrap 연동

`Bootstrap.unity`의 `[ProjectH] Bootstrap` 오브젝트에 DataManager를 연결하고 `ProjectHDataCatalog`를 참조하도록 구성했다.

### 샘플 데이터

캐릭터:

- `CH_SERENA`
- `CH_ELLEN`
- `CH_LILIA`
- `CH_EVE`

몬스터:

- `MON_CORRUPTED_WOLF`
- `MON_POLLUTED_PLANT`
- `MON_CORRUPTED_SOLDIER`

던전:

- `DG_LETICIA_FOREST`

아이템:

- `IT_POTION_SMALL`
- `IT_MATERIAL_001`

현재 수치는 데이터 로딩과 구조 검증을 위한 프로토타입 값이다.

### Editor 자동 설정

`Phase0Day2Setup`을 추가했다.

`Tools > Project H > Phase 0 > 2일차 설정 실행`

실행 시 데이터 폴더, 샘플 데이터 에셋, 데이터 카탈로그, Bootstrap의 DataManager 연결을 구성한다.

### 테스트

EditMode `DataRegistryTests`를 추가했다.

검증 항목:

- 고유 ID 데이터 정상 등록 및 조회
- 중복 ID 검출
- 빈 ID 검출

Runtime, Editor, EditMode Tests용 Assembly Definition을 분리했다.

---

## 검토 결과

최신 커밋 기준 정적 검토에서 Phase 0 Day 2 진행을 막는 명확한 구조 문제는 확인되지 않았다.

- DataManager와 GameManager 연결 확인
- Bootstrap의 DataManager 및 Catalog 참조 확인
- 4종 데이터 Registry 구성 확인
- 데이터 카탈로그에 4 캐릭터, 3 몬스터, 1 던전, 2 아이템 참조 확인
- 중복 ID와 빈 ID에 대한 검증 코드 확인
- EditMode Registry 테스트 코드 확인
- Unity Test Framework 패키지 사용 구조 확인

`Project-H.slnx` 변경은 Assembly Definition 추가에 따른 IDE 프로젝트 목록 갱신이며 게임 런타임 동작을 막는 변경은 아니다.

GitHub Actions 또는 별도 CI 상태 검사는 등록되어 있지 않으므로, 저장소 상태만으로 Unity Editor 컴파일과 EditMode Test Runner의 실제 성공 여부까지 증명되지는 않는다.

---

## Day 2 완료 기준

- Bootstrap에서 DataManager가 초기화될 것
- ProjectHDataCatalog가 DataManager에 연결되어 있을 것
- 캐릭터 4개가 Registry에 등록될 것
- 몬스터 3개가 Registry에 등록될 것
- 던전 1개가 Registry에 등록될 것
- 아이템 2개가 Registry에 등록될 것
- ID를 통해 각 데이터를 조회할 수 있을 것
- 중복 ID 또는 빈 ID가 검증 오류로 탐지될 것

Phase 0 Day 2 데이터 시스템 기반 구축.
