# Project I 개발 일지

## Day 25 — Day24 Unity 회귀 검증 및 Office 아이템 상태 보존·빌드 씬 등록 수정

- 날짜: 2026-09-12
- 개발 단계: Phase 5 마무리 — 싱글 수직 슬라이스 회귀 검증
- 기준 커밋: `ebb511b` (24일차)
- 검증 환경: Unity 6000.3.21f1 배치 모드 + Play Mode 자동 검증 하네스
- 검증 방식: 원본 프로젝트 복사본에서 별도 저장 경로(`Project I Day25Harness`)로 실행

---

## 개발 목표

Day24에서 구성한 `Persistent 마차 + Additive 환경` 구조와
사무소 체크포인트·일차 복구 시스템은 코드 정적 확인만 된 상태였다.

Day25에서는 새 기능을 추가하기 전에
실제 Unity에서 컴파일과 Play Mode 동작을 검증하고,
문제가 발견되면 증상이 아니라 원인을 확인한 뒤 수정하는 것을 목표로 했다.

---

# 1. 검증 방법

사용자 저장 파일과 원본 프로젝트를 건드리지 않도록
프로젝트를 복사한 뒤 제품 이름만 바꿔 별도 저장 경로를 사용했다.

```text
Unity 배치 모드 실행
↓
00_WagonPersistent 열기 → Play Mode 진입
↓
검증 하네스가 실제 종 상호작용·줍기·판매·보관을 호출
↓
PASS / FAIL 보고서 기록
```

강제 종료 테스트는 코드로 흉내내지 않고
던전 체류 중 실제 Unity 프로세스를 외부에서 강제 종료한 뒤 재실행했다.

검증 단계:

```text
Phase A  초기 상태·자동 저장·왕복·Cargo·판매/보관/채무 → 던전에서 강제 종료
Phase B  재실행 → 출발 직전 Office 복구 → 일차 완료 → 다음 일차 왕복
Phase C  재실행 → 다음 일차 Office Current 복구
(Current 파일 한 글자 변조)
Phase D  재실행 → SHA-256 불일치 감지 → Day_001 폴백 → 복구 실패 시 아이템 보호
Phase E  던전 체류 중 최근 Day_N 롤백
```

---

# 2. 회귀 검증 결과

| 항목 | 수정 전 | 수정 후 |
|---|---|---|
| 1. Unity 컴파일 | 오류 0건 | 오류 0건 |
| 2. Office ↔ TestDungeon 반복 왕복 | 씬 교체 정상 / 아이템 복제 | 통과 |
| 3. Cargo 다중 적재 왕복 · 동일 GameObject | 동일 GameObject 유지 정상 / Office 쪽 복제본 생성 | 통과 |
| 4. 던전 강제 종료 → Office 체크포인트 | 통과 | 통과 |
| 5. Current 손상 → Day_N 폴백 | 통과 | 통과 |
| 6. Day23 판매·보관·채무 | 자금·채무 정상 / 단상 소실·판매품 부활 | 통과 |
| 7. 일차 완료 → 다음 일차 Office 시작 | 통과 | 통과 |

수정 후 전체 결과:

```text
PASS 205 / FAIL 0
```

---

# 3. 발견된 문제 1 — Office 재로드 시 아이템 상태 초기화

## 증상

```text
마차에 실어 간 왕관이 사무소에 하나 더 생김 (복제)
판매한 왕관이 사무소에 다시 생김 (무한 판매)
보관 단상에 올린 회수품이 사라짐 → 귀환 직후 자동 저장으로 영구 손실
던전에서 잃어버린 도끼가 사무소에 다시 생김
사무소 바닥에서 옮겨 둔 물건이 원래 위치로 돌아감
```

## 원인

`01_Office` 씬 파일에는 WorldItem 19개가 배치되어 있다.

Office를 다시 Additive 로드할 때마다
씬 파일의 19개가 처음 상태로 다시 생성되었다.

Day24에서는 공동 자금과 채무만 메모리로 이어받고
사무소 아이템 상태는 이어받지 않았다.

```text
증상 5개 → 원인 1개
"씬 파일을 매번 현재 사무소 상태로 취급"
```

## 수정

Cargo 보존과 같은 원칙을 사무소에도 적용했다.

```text
Office를 떠날 때
↓
사무소 씬의 실제 WorldItem을 같은 GameObject 그대로
00_WagonPersistent의 비활성 보관 루트로 이동
(단상 보관품은 단상 계층 경로도 기록)
↓
Office 언로드

Office로 돌아올 때
↓
새로 생성된 씬 기본 아이템 제거
↓
보관한 아이템을 Office 씬으로 반환
(단상 보관품은 같은 경로의 새 단상에 다시 연결)
```

규칙:

```text
판매 완료·원정 손실로 비활성화된 아이템 → 보관하지 않음
세션 최초 Office 로드 → 씬 기본 아이템이 초기 상태
Snapshot 복구 → 보관 상태를 폐기하고 Snapshot을 기준으로 재구성
```

주요 파일:

```text
Assets/ProjectI/Scripts/Loop/OfficeWorldItemKeeper.cs   (신규)
Assets/ProjectI/Scripts/Loop/PersistentMapLoader.cs
Assets/ProjectI/Scripts/Persistence/DailySnapshotService.cs
```

---

# 4. 발견된 문제 2 — Build Settings 씬 미등록

## 증상

에디터 자동 훅이 실행되지 않은 환경에서
`01_Office` 로드가 실패하고 화면이 암전 상태로 멈췄다.

오류 안내도 실제 원인과 다른
`ItemDefinition 누락 여부를 확인하세요`가 출력되었다.

## 원인

`ProjectSettings/EditorBuildSettings.asset`에는
Boot / MainMenu / ExplorationOffice만 저장되어 있었다.

Day24 Step2 에디터 훅이 매 컴파일마다
메모리에서만 씬 목록을 보정하고 있었기 때문에
빌드·배치 모드·새로 클론한 PC에서는 동작하지 않는다.

## 수정

```text
Boot
MainMenu
00_WagonPersistent
01_Office
02_TestDungeon
```

위 목록을 파일로 저장했다.
Step2 훅이 만드는 결과와 동일하므로 훅이 다시 실행되어도 변경이 생기지 않는다.

또한 Office가 로드되지 않으면
저장 시스템 초기화를 중단하고 원인을 정확히 안내하도록 변경했다.
이 경우 기존 저장 파일은 변경하지 않는다.

---

# 5. 확인된 정상 동작

```text
환경 씬은 항상 하나만 로드
Player / Wagon 재생성 없음
Cargo 아이템 동일 GameObject 유지 · 짐칸 내부 위치 유지 · Rigidbody 원복
던전 바닥에 내려놓은 아이템 → 02_TestDungeon 소속 → 던전과 함께 제거
Office 체류 중 Current 2초 간격 자동 저장
출발 직전 강제 저장 / 던전 체류 중 Current 미갱신
던전에서 일차 완료 요청 거부
강제 종료 후 출발 직전 Office 상태 복구 (자금·채무·아이템·단상·빠른 슬롯)
Day_001 생성 후 Current 저장 성공 시에만 currentDay 증가
SHA-256 불일치 감지 → Day_001 폴백 → Day 2 Office 시작 → Current 재생성
WagonStopPoint 누락 시 복구 취소 · 기존 아이템 유지
```

---

# 6. 남은 문제와 후속 후보

이번에 수정하지 않은 구조 문제:

```text
1. CompleteCurrentDay()를 호출하는 게임 내 상호작용이 없음
   → 실제 플레이에서는 계속 1일차

2. 하루 1회 원정 규칙이 없음
   → 같은 날 여러 번 왕복 가능

3. Office 씬에만 존재하는 전역 시스템
   DebugPageManager(F1) / GameTimeController / BrightnessManager / ExpeditionOutcomeController
   → 던전에서는 F1 없음, 시간은 Office 재로드마다 초기화

4. 마차 밖에 있는 플레이어도 상대 위치 그대로 이동
   → 도착지에 바닥이 없으면 낙하 (종은 창고 내부에 있어 일반 플레이에서는 드묾)

5. Day24 Editor 자동 적용 훅이 컴파일마다 씬·프리팹을 다시 저장

6. ItemDefinition이 없는 아이템이 하나라도 있으면 Office 저장 거부 → 출발 차단
   → 절차 생성 던전 전리품 도입 전에 고정 ItemId 체계 필요
```

---

## Day 25 결과

Day24 구조를 실제 Unity에서 검증했다.

Persistent 마차와 Additive 환경 교체, 강제 종료 복구, Snapshot 폴백은
설계대로 동작하는 것을 확인했다.

Office 재로드 시 아이템이 초기화되는 원인을 찾아
사무소 아이템도 Cargo와 같이 실제 GameObject로 보존하도록 수정했고,
누락된 빌드 씬 등록을 파일로 고정했다.

이제 다음 단계는 게임 안에서 하루를 끝내고 다음 날로 넘어가는
일차 루프를 연결하는 것이다.
