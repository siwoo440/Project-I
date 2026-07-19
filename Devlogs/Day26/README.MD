# 26일차 : 성능 및 반복 생성 안정화

---

## 목표

- 반복 던전 생성 시 기존 런타임 오브젝트를 안전하게 정리한다.
- 효과음과 파티클의 반복 생성 및 제거 비용을 줄인다.
- FPS, 메모리와 잔류 오브젝트를 실행 중에 검사한다.

---

## 한 일

- DungeonGenerator에 GenerationStarted 이벤트를 추가했다.
- 던전 중복 생성 방지 상태와 생성 횟수를 추가했다.
- 기존 방을 제거하기 전에 즉시 비활성화하도록 수정했다.
- 모든 스폰 매니저를 GenerationStarted 이벤트와 연결했다.
- 기존 몬스터, 보물, 미믹, 함정, 스토커와 기믹 몬스터를 지연 삭제 전에 비활성화하도록 수정했다.
- AudioManager에 AudioSource 풀을 구현했다.
- 임시 전자음 AudioClip을 캐시해 반복 생성을 줄였다.
- ParticleEffectPool을 구현했다.
- ThreatFeedback의 파티클 생성을 풀 방식으로 변경했다.
- 반복 FindFirstObjectByType 호출을 제거했다.
- RuntimePerformanceMonitor를 구현했다.
- F9 수동 던전 재생성 기능을 추가했다.
- F11 자동 10회 반복 생성 검사를 추가했다.
- 평균 FPS, 프레임 시간과 관리 메모리를 표시하도록 했다.
- 반복 생성 후 잔류 방과 잔류 스폰 오브젝트를 검사하도록 했다.
- 오디오와 파티클 풀의 활성 및 대기 개수를 표시하도록 했다.
- 증가한 매니저 오브젝트를 역할별 Hierarchy로 정리했다.
- GameManagers 아래에 Scene, Spawn, Diagnostic, Persistent 분류를 추가했다.
- ManagerHub를 구현해 주요 매니저 참조를 한 Inspector에서 확인하도록 했다.
- Context Menu를 이용한 매니저 자동 연결과 누락 검사를 추가했다.
- AudioManager와 ParticleEffectPool이 Play 시 루트로 분리되도록 수정했다.
- 자식 오브젝트 상태에서 발생할 수 있는 DontDestroyOnLoad 경고를 방지했다.
---

## 결과

- 던전 재생성 시 이전 Collider가 새로운 스폰 위치 검사를 방해하는 현상을 줄였다.
- 반복 효과음 재생 시 AudioSource GameObject를 재사용한다.
- 반복 파티클 재생 시 기존 ParticleSystem을 재사용한다.
- 반복 던전 생성 시 남은 오브젝트를 화면과 Console에서 확인할 수 있다.
- 자동 반복 검사를 통해 장시간 플레이 안정성을 확인할 기반을 마련했다.
- 매니저가 역할별로 분리되어 Hierarchy 확인이 쉬워졌다.
- 주요 매니저의 누락 여부를 ManagerHub에서 한 번에 검사할 수 있다.
- 영구 매니저와 Scene 전용 매니저의 수명주기가 분리됐다.
---

## 현재 제한 사항

- 일반 몬스터, 보물과 함정 자체에는 오브젝트 풀을 적용하지 않았다.
- 관리 메모리는 가비지 컬렉션 시점에 따라 일시적으로 증가할 수 있다.
- Editor FPS에는 Inspector와 Scene View 처리 시간이 포함될 수 있다.
- 정식 성능 기준은 Windows Development Build와 Unity Profiler에서 다시 측정해야 한다.
- 몬스터 경로탐색은 아직 CharacterController 직선 이동 방식이다.
- 멀티플레이어와 네트워크 동기화는 구현하지 않았다.

---

## 다음 예정

27일차에는 마을 정산과 빚 상환 흐름을 구현한다.

- 익스트랙션 결과 데이터 저장
- 확보한 보물 가치 정산
- 빚 잔액과 남은 기한 관리
- 하루 진행 결과 표시
- 다음 던전 출발 흐름 연결