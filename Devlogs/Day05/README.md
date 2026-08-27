# Project I 개발 일지

## Day 5 — 상호작용 및 월드 아이템 운반 시스템 구현

- 날짜: 2026-08-27
- 개발 단계: Phase 2 — 플레이어 조작과 상호작용
- 기준 커밋: `1ee15109eb8527255463889e5aeca8753737b942`
- 기준 커밋 메시지: `a`
- 이전 기준: `af221fc0adbbd12296349579f8346e5460f20055`

---

## 개발 목표

플레이어가 월드의 상호작용 대상을 바라보고 F 키로 조작할 수 있는 공통 상호작용 구조를 구현한다.

동시에 월드 아이템을 직접 줍고, 화면상 운반 포즈로 들고 다니며,
플레이어 앞에 내려놓거나 투척할 수 있는 기본 물리 운반 시스템을 구축한다.

---

## 구현 내용

### 1. F 상호작용 입력

기존 Interact 입력을 E에서 F로 변경했다.

또한 Input Action에 고정되어 있던 Hold Interaction을 제거하고,
대상이 자체적으로 Press / Hold / Toggle 방식을 결정하도록 변경했다.

지원 방식:

- Press — F 한 번으로 즉시 실행
- Hold — F를 일정 시간 유지하여 실행
- Toggle — F를 누를 때마다 상태 전환

### 2. 공통 상호작용 구조

`IInteractable` 기반의 공통 상호작용 구조를 추가했다.

플레이어는 카메라 중앙 Raycast로 대상을 탐색하고,
대상의 종류를 직접 판단하지 않고 공통 인터페이스를 통해 상호작용한다.

구성 요소:

- `InteractionType`
- `IInteractable`
- `InteractionProgress`
- `PlayerInteractor`
- `InteractionPromptHud`
- `TestInteractable`

### 3. 상호작용 안내 UI

플레이어가 사용할 수 있는 대상을 바라보면 화면에 상호작용 문구를 표시한다.

예:

- `[F] 버튼 누르기`
- `[F 길게] 밸브 돌리기`
- `[F] 전원 전환`
- `[F] 시험용 검 줍기`

Hold 방식에서는 진행도를 함께 표시한다.

### 4. 월드 아이템

`WorldItem`과 `PlayerCarryController`를 추가하여
Rigidbody 기반 월드 아이템을 직접 줍고 운반할 수 있도록 구성했다.

기본 흐름:

`월드 아이템 → F로 줍기 → CarryPoint 종속 → G로 내려놓기 / 좌클릭으로 투척 → 월드 물리 복구`

### 5. CarryType 기초 구조

아이템 데이터에 다음 운반 방식을 추가했다.

- `CarryType.OneHand`
- `CarryType.TwoHand`

이번 단계에서는 별도의 장비 슬롯으로 사용하지 않는다.

`CarryType`은 아이템이 화면에서 어떤 운반 포즈를 사용할지 정하는 기초 데이터이며,
빠른 슬롯 6칸 및 양손 운반 중 슬롯 전환 제한은 Day 6에서 통합한다.

### 6. 한손 / 양손 CarryPoint

카메라 하위에 두 개의 운반 위치를 구성했다.

- `OneHandCarryPoint`
  - 화면 오른쪽 아래
  - 작은 한손 아이템 운반 위치

- `TwoHandCarryPoint`
  - 화면 중앙 아래
  - 양손으로 받쳐 드는 아이템 운반 위치

아이템을 집는 순간 해당 CarryPoint의 자식으로 연결한다.

운반 중에는 Lerp / Slerp 추종을 사용하지 않으며,
카메라 회전 후에도 `LateUpdate`에서 CarryPoint의 로컬 위치와 회전에 다시 맞춘다.

### 7. 운반 중 물리 안정화

운반 중에는 다음 규칙을 적용한다.

- Rigidbody Kinematic 전환
- 중력 비활성화
- Rigidbody Interpolation 비활성화
- Collider 비활성화
- CarryPoint 자식 Transform으로 직접 종속

따라서 빠른 이동이나 빠른 시점 회전에서도
아이템이 별도의 물리 보간으로 늦게 따라오는 구조를 사용하지 않는다.

내려놓기 또는 투척 시에는 Rigidbody / Collider / Interpolation을 월드 상태로 복구한다.

### 8. 플레이어와 아이템 충돌 무시

월드 아이템과 플레이어의 Collider 사이에는 `Physics.IgnoreCollision`을 설정했다.

아이템은 플레이어 몸과 충돌하지 않고 겹칠 수 있다.

운반 중에는 Collider도 비활성화되어
플레이어와 들고 있는 아이템 사이의 물리 충돌을 방지한다.

### 9. 아이템끼리 충돌 무시

활성화된 `WorldItem` 목록을 관리하고,
새로운 아이템이 활성화될 때 기존 WorldItem들과 `Physics.IgnoreCollision`을 설정한다.

따라서 여러 아이템을 같은 위치에 내려놓아도 서로 밀어내지 않고 겹칠 수 있다.

바닥과 벽 등 일반 월드 Collider와의 충돌은 유지한다.

### 10. 내려놓기

G 키로 들고 있는 아이템을 내려놓는다.

기존처럼 카메라 앞 먼 곳에 배치하지 않고,
플레이어 몸체 기준 약 0.6m 앞의 낮은 위치에서 물리 상태로 복귀하도록 변경했다.

카메라가 위아래를 보고 있어도 수평 전방을 기준으로 계산한다.

### 11. 투척

마우스 왼쪽 버튼으로 들고 있는 아이템을 시선 방향으로 투척할 수 있다.

투척 시:

- CarryPoint 부모 연결 해제
- Collider 복구
- Rigidbody 동적 상태 복구
- 중력 복구
- 기존 Interpolation 복구
- `ForceMode.VelocityChange` 기반 전방 힘 적용

### 12. 이동 플랫폼 탑승 처리 수정

기존에는 이동 플랫폼 위 플레이어를 플랫폼의 자식으로 연결했지만,
CharacterController 이동과 충돌할 수 있어 구조를 변경했다.

현재는 플랫폼의 실제 프레임 이동량을 계산하고
등록된 CharacterController에 `Move(frameDelta)`로 직접 전달한다.

탑승 감지는 다음을 사용한다.

- `OnTriggerEnter`
- `OnTriggerStay`
- `OnTriggerExit`

### 13. 테스트 맵 겹침 수정

기존 `07_MovingPlatformTest`와 `08_InteractionTest` 바닥이
같은 위치에 겹쳐 Z-fighting이 발생하던 문제를 수정했다.

`08_InteractionTest`를 테스트 맵 좌하단의 별도 공간으로 이동했다.

### 14. 작은 시험 아이템

기존 큰 상자와 원통 대신
실제 게임의 작은 장비 크기를 가정한 시험 아이템으로 변경했다.

- `TestItem_Sword`
  - 작은 검 형태
  - OneHand

- `TestItem_Pickaxe`
  - 작은 곡괭이 형태
  - TwoHand

현재 Primitive 기반 테스트 오브젝트이며,
실제 모델링 에셋은 이후 교체한다.

### 15. Day 5 테스트 구역

`08_InteractionTest`에서 다음 기능을 시험할 수 있다.

- Press 상호작용
- Hold 상호작용
- Toggle 상호작용
- 한손 아이템 획득
- 양손 아이템 획득
- CarryPoint 운반
- 내려놓기
- 투척
- 아이템 중첩

---

## 주요 생성 파일

- `Assets/ProjectI/Scripts/Interaction/InteractionType.cs`
- `Assets/ProjectI/Scripts/Interaction/IInteractable.cs`
- `Assets/ProjectI/Scripts/Interaction/InteractionProgress.cs`
- `Assets/ProjectI/Scripts/Interaction/PlayerInteractor.cs`
- `Assets/ProjectI/Scripts/Interaction/InteractionPromptHud.cs`
- `Assets/ProjectI/Scripts/Interaction/TestInteractable.cs`
- `Assets/ProjectI/Scripts/Items/CarryType.cs`
- `Assets/ProjectI/Scripts/Items/WorldItem.cs`
- `Assets/ProjectI/Scripts/Items/PlayerCarryController.cs`
- `Assets/ProjectI/Editor/Phase2Day5Setup.cs`
- `Assets/ProjectI/Editor/Phase2Day5Validator.cs`
- `Assets/ProjectI/Editor/Phase2Day5CarryPlatformMapFix.cs`
- `Assets/ProjectI/Editor/Phase2Day5ItemDropOverlapFix.cs`

## 주요 수정 파일

- `Assets/InputSystem_Actions.inputactions`
- `Assets/ProjectI/Scenes/ExplorationOffice.unity`
- `Assets/ProjectI/Scripts/Player/PlayerInputReader.cs`
- `Assets/ProjectI/Scripts/World/MovingPlatform.cs`
- `Assets/ProjectI/Scripts/World/MovingPlatformPassengerTrigger.cs`
- `Assets/ProjectI/Editor/Phase2Day4Setup.cs`

---

## 저장소 점검 결과

최신 커밋 `1ee15109`를 기준으로 5일차 변경 내용을 확인했다.

저장소에서 다음 사항이 확인되었다.

- Interact 키 F 적용
- 월드 아이템 CarryPoint 종속 구조
- 카메라 회전 후 아이템 위치 재동기화
- 플레이어와 아이템 충돌 무시
- WorldItem끼리 충돌 무시
- 플레이어 바로 앞 내려놓기
- 작은 시험용 검 / 곡괭이 구성
- 이동 플랫폼 탑승 이동량 전달 방식
- 상호작용 테스트 구역 분리

GitHub에 연결된 CI 상태 검사는 현재 등록되어 있지 않다.

따라서 저장소 구조 검토와 별개로
Unity Editor 컴파일 및 Play Mode 동작은 로컬 환경에서 최종 확인한다.

---

## 로컬 최종 확인 항목

- Console Error 0개
- `Tools → Project I → Day 5 → Validate` 검증
- F 대상 감지
- Press 정상 실행
- Hold 진행 및 완료
- Hold 중간 취소
- Toggle 상태 전환
- 시험용 검 F 획득
- 시험용 곡괭이 F 획득
- 아이템이 시점 회전을 즉시 따라감
- 달리면서 아이템 운반 정상
- 플레이어와 아이템 충돌 없음
- 아이템 여러 개 중첩 가능
- G 입력 시 플레이어 바로 앞에 떨어짐
- 좌클릭 투척 정상
- 바닥 및 벽과 아이템 충돌 유지
- 이동 플랫폼 위에서 플레이어가 같이 이동
- 07 / 08 테스트 구역 Z-fighting 없음
- 기존 이동 / 점프 / 웅크리기 / 스태미나 / HP / 추락 피해 정상

---

## 다음 개발

Day 6에서는 Phase 2의 마지막 단계로 빠른 슬롯과 아이템 운반 제약을 통합한다.

주요 예정 기능:

- 빠른 슬롯 6칸
- 숫자키 / 마우스 휠 슬롯 선택
- 아이템 월드 ↔ 빠른 슬롯 연결
- `CarryType.OneHand` 자유 슬롯 전환
- `CarryType.TwoHand` 운반 중 현재 슬롯 고정
- 양손 아이템 내려놓기 전 슬롯 전환 제한
- 아이템 사용 / 선택 / 해제
- Phase 2 전체 통합 시험
