using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    [RequireComponent(typeof(MonsterAI))] // 체력과 피격 처리용 MonsterAI 지정
    [RequireComponent(typeof(CharacterController))] // 이동 충돌용 CharacterController 지정
    public class Ghost : MonoBehaviour // 플레이어 뒤로 순간이동하는 고스트 기믹
    {
        [Header("외형")] // Inspector 외형 설정 구분
        [SerializeField] GameObject ghostVisual; // 순간이동 후 표시할 고스트 외형

        [Header("순간이동")] // Inspector 순간이동 설정 구분
        [SerializeField] float activationDelay = 2.5f; // 생성 후 순간이동 대기시간
        [SerializeField] float warningLeadTime = 0.7f; // 순간이동 전 경고를 재생할 시간
        [SerializeField] float teleportDistance = 3f; // 플레이어 뒤쪽 순간이동 거리
        [SerializeField] float groundSearchHeight = 4f; // 바닥 검색 시작 높이
        [SerializeField] float groundSearchDistance = 8f; // 바닥 검색 최대 거리

        [Header("기습 공격")] // Inspector 기습 공격 설정 구분
        [SerializeField] float ambushDelay = 0.35f; // 순간이동 후 공격 대기시간
        [SerializeField] float ambushDamage = 35f; // 고스트 기습 피해량
        [SerializeField] float ambushRange = 4f; // 고스트 기습 공격 거리
        [SerializeField] float rotationSpeed = 12f; // 플레이어 방향 회전속도

        [Header("디버그")] // Inspector 디버그 설정 구분
        [SerializeField] bool showDebug = true; // 고스트 상태 로그 표시 여부

        static readonly float[] TeleportAngles = // 순간이동 위치 검색 각도 목록
        {
            0f, // 플레이어 정후방
            -25f, // 플레이어 우측 후방
            25f, // 플레이어 좌측 후방
            -45f, // 플레이어 넓은 우측 후방
            45f // 플레이어 넓은 좌측 후방
        };

        CharacterController controller; // 고스트 CharacterController
        MonsterAI monsterAI; // 기존 체력과 추격 처리용 AI
        ThreatFeedback threatFeedback; // 고스트 경고와 공격 피드백
        PlayerController targetPlayer; // 기습 대상 플레이어

        float teleportTime; // 실제 순간이동 실행 시각
        float attackTime; // 실제 기습 공격 실행 시각
        bool isActivated; // 고스트 활성화 여부
        bool hasTeleported; // 순간이동 완료 여부
        bool hasAttacked; // 기습 공격 완료 여부
        bool warningPlayed; // 순간이동 경고 재생 완료 여부

        public bool HasTeleported => hasTeleported; // 외부 순간이동 상태 확인
        public PlayerController TargetPlayer => targetPlayer; // 외부 목표 플레이어 확인

        void Awake() // 고스트 필수 컴포넌트와 초기 상태 설정
        {
            controller = GetComponent<CharacterController>(); // CharacterController 가져오기
            monsterAI = GetComponent<MonsterAI>(); // MonsterAI 가져오기
            threatFeedback = GetComponent<ThreatFeedback>(); // 고스트 공통 피드백 가져오기
            monsterAI.enabled = false; // 순간이동 전 일반 FSM 정지
            controller.enabled = false; // 보이지 않는 고스트 충돌 비활성화
            SetVisual(false); // 순간이동 전 외형 숨기기
        }

        void Start() // 수동 배치된 고스트의 플레이어 자동 검색
        {
            if (targetPlayer != null) // 이미 목표가 지정되었는지 확인
            {
                return; // 중복 목표 지정 방지
            }

            PlayerController foundPlayer = FindFirstObjectByType<PlayerController>(); // Scene에서 플레이어 검색

            if (foundPlayer != null) // 플레이어 검색 성공 여부 확인
            {
                Activate(foundPlayer); // 검색한 플레이어를 목표로 활성화
            }
        }

        void Update() // 순간이동과 기습 공격 상태 처리
        {
            if (!isActivated) // 고스트 활성화 여부 확인
            {
                return; // 비활성 상태 처리 중단
            }

            if (targetPlayer == null || targetPlayer.IsDead) // 목표 유효성과 사망 상태 확인
            {
                return; // 기습 처리 중단
            }

            if (!hasTeleported) // 아직 순간이동하지 않았는지 확인
            {
                if (Time.time < teleportTime) // 순간이동 대기시간 확인
                {
                    return; // 대기시간 동안 처리 중단
                }

                if (TryTeleportBehindTarget()) // 플레이어 뒤쪽 순간이동 시도
                {
                    hasTeleported = true; // 순간이동 완료 상태 저장
                    attackTime = Time.time + Mathf.Max(0f, ambushDelay); // 기습 공격 시각 계산

                    if (showDebug) // 디버그 로그 표시 여부 확인
                    {
                        Debug.Log("[Ghost] 플레이어 뒤쪽으로 순간이동했습니다."); // 순간이동 결과 출력
                    }
                }
                else // 안전한 순간이동 위치를 찾지 못한 경우
                {
                    BeginNormalChase(); // 현재 위치에서 일반 추격 시작
                }

                return; // 현재 프레임 처리 종료
            }

            if (!warningPlayed && Time.time >= teleportTime - warningLeadTime) // 순간이동 경고 시각 확인
            {
                warningPlayed = true; // 경고 중복 재생 방지

                Vector3 playerForward = targetPlayer.transform.forward; // 플레이어 전방 방향 가져오기
                playerForward.y = 0f; // 수직 방향 제외

                if (playerForward.sqrMagnitude < 0.01f) // 전방 방향 유효성 확인
                {
                    playerForward = Vector3.forward; // 기본 전방 방향 사용
                }

                Vector3 warningPosition = targetPlayer.transform.position - playerForward.normalized * teleportDistance; // 플레이어 뒤쪽 경고 위치 계산

                if (threatFeedback != null) // 고스트 피드백 존재 여부 확인
                {
                    threatFeedback.PlayWarningAt(warningPosition); // 플레이어 뒤쪽에서 순간이동 경고 재생
                }
            }



            FaceTarget(); // 기습 전 플레이어 방향으로 회전

            if (!hasAttacked && Time.time >= attackTime) // 기습 공격 가능 시각 확인
            {
                AttackTarget(); // 플레이어 기습 공격 실행
            }
        }

        public void Activate(PlayerController target) // 지정된 플레이어를 고스트 목표로 설정
        {
            if (target == null) // 전달된 플레이어 존재 여부 확인
            {
                return; // 활성화 중단
            }

            targetPlayer = target; // 목표 플레이어 저장
            teleportTime = Time.time + Mathf.Max(0f, activationDelay); // 순간이동 실행 시각 계산
            isActivated = true; // 고스트 활성화 상태 저장
            hasTeleported = false; // 순간이동 상태 초기화
            hasAttacked = false; // 기습 공격 상태 초기화
            warningPlayed = false; // 순간이동 경고 상태 초기화
            monsterAI.enabled = false; // 일반 FSM 비활성화
            controller.enabled = false; // 순간이동 전 충돌 비활성화
            SetVisual(false); // 순간이동 전 외형 숨기기

            if (showDebug) // 디버그 로그 표시 여부 확인
            {
                Debug.Log($"[Ghost] 목표 지정 — {activationDelay:F1}초 후 순간이동"); // 목표 지정 결과 출력
            }
        }

        bool TryTeleportBehindTarget() // 플레이어 뒤쪽의 안전한 순간이동 위치 검색
        {
            Vector3 playerForward = targetPlayer.transform.forward; // 플레이어 전방 방향 가져오기
            playerForward.y = 0f; // 수직 방향 제외

            if (playerForward.sqrMagnitude < 0.01f) // 전방 방향 유효성 확인
            {
                playerForward = Vector3.forward; // 기본 전방 방향 사용
            }

            playerForward.Normalize(); // 전방 방향 단위 벡터 변환

            foreach (float angle in TeleportAngles) // 준비된 후방 검색 각도 순회
            {
                Vector3 rotatedForward = Quaternion.Euler(0f, angle, 0f) * playerForward; // 검색 각도 반영
                Vector3 desiredPosition = targetPlayer.transform.position - rotatedForward * teleportDistance; // 후방 후보 위치 계산
                Vector3 rayOrigin = desiredPosition + Vector3.up * groundSearchHeight; // 바닥 검색 Ray 시작점 계산

                if (!Physics.Raycast( // 후보 위치 아래쪽 바닥 검색
                    rayOrigin, // Ray 시작 위치
                    Vector3.down, // Ray 진행 방향
                    out RaycastHit groundHit, // 바닥 충돌 정보
                    groundSearchDistance, // 최대 검색 거리
                    Physics.AllLayers, // 전체 물리 레이어 검사
                    QueryTriggerInteraction.Ignore)) // Trigger Collider 제외
                {
                    continue; // 바닥이 없으면 다음 각도 검색
                }

                Vector3 rootPosition = groundHit.point; // 바닥 기준 루트 위치 계산

                if (!IsSpaceFree(rootPosition)) // 고스트가 들어갈 공간 확인
                {
                    continue; // 막힌 위치면 다음 각도 검색
                }

                transform.position = rootPosition; // 고스트를 검색된 위치로 이동
                SnapFaceTarget(); // 플레이어를 즉시 바라보도록 회전
                controller.enabled = true; // 순간이동 후 충돌 활성화
                SetVisual(true); // 순간이동 후 외형 표시
                if (threatFeedback != null) // 고스트 피드백 존재 여부 확인
                {
                    threatFeedback.PlayAppear(); // 고스트 출현 소리와 파티클 재생
                }
                return true; // 순간이동 성공 반환
            }

            return false; // 모든 위치 검색 실패 반환
        }

        bool IsSpaceFree(Vector3 rootPosition) // CharacterController 크기의 빈 공간 검사
        {
            float radius = Mathf.Max(0.05f, controller.radius); // 안전한 충돌 반경 계산
            float height = Mathf.Max(radius * 2f, controller.height); // 안전한 충돌 높이 계산
            Vector3 worldCenter = rootPosition + transform.rotation * controller.center; // 후보 캡슐 중심 계산
            Vector3 bottom = worldCenter + Vector3.up * (-height * 0.5f + radius); // 캡슐 아래쪽 중심 계산
            Vector3 top = worldCenter + Vector3.up * (height * 0.5f - radius); // 캡슐 위쪽 중심 계산

            return !Physics.CheckCapsule( // 후보 위치의 장애물 중첩 검사
                bottom, // 캡슐 아래쪽 중심
                top, // 캡슐 위쪽 중심
                radius, // 캡슐 반경
                Physics.AllLayers, // 전체 물리 레이어 검사
                QueryTriggerInteraction.Ignore); // Trigger Collider 제외
        }

        void FaceTarget() // 플레이어 방향으로 부드럽게 회전
        {
            Vector3 direction = targetPlayer.transform.position - transform.position; // 플레이어 방향 계산
            direction.y = 0f; // 수직 방향 제외

            if (direction.sqrMagnitude < 0.01f) // 회전 가능한 방향인지 확인
            {
                return; // 회전 처리 중단
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized); // 목표 회전값 계산
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime); // 회전 보간 적용
        }

        void SnapFaceTarget() // 순간이동 직후 플레이어를 즉시 바라보도록 회전
        {
            Vector3 direction = targetPlayer.transform.position - transform.position; // 플레이어 방향 계산
            direction.y = 0f; // 수직 방향 제외

            if (direction.sqrMagnitude < 0.01f) // 회전 가능한 방향인지 확인
            {
                return; // 회전 처리 중단
            }

            transform.rotation = Quaternion.LookRotation(direction.normalized); // 플레이어 방향 즉시 적용
        }

        void AttackTarget() // 순간이동 후 플레이어 기습 공격
        {
            hasAttacked = true; // 중복 기습 공격 방지
            Vector3 difference = targetPlayer.transform.position - transform.position; // 플레이어까지의 방향 계산
            difference.y = 0f; // 높이 차이 제외
            float distance = difference.magnitude; // 수평거리 계산

            if (distance <= ambushRange && HasClearLineToTarget()) // 공격 거리와 시야 확보 여부 확인
            {
                targetPlayer.TakeDamage(Mathf.Max(0f, ambushDamage)); // 플레이어에게 기습 피해 적용
                
                if (threatFeedback != null) // 고스트 피드백 존재 여부 확인
                {
                    threatFeedback.PlayAttack(); // 고스트 기습 공격 피드백 재생
                }

                if (showDebug) // 디버그 로그 표시 여부 확인
                {
                    Debug.Log($"[Ghost] 기습 공격 — {ambushDamage:F0} 피해"); // 기습 공격 결과 출력
                }
            }
            else if (showDebug) // 공격에 실패한 경우 로그 확인
            {
                Debug.Log("[Ghost] 플레이어가 기습 범위에서 벗어났습니다."); // 기습 회피 결과 출력
            }

            BeginNormalChase(); // 기습 후 기존 MonsterAI 추격 시작
        }

        bool HasClearLineToTarget() // 고스트와 플레이어 사이 장애물 검사
        {
            Vector3 origin = transform.position + Vector3.up; // 고스트 시야 시작점 계산
            Vector3 targetPoint = targetPlayer.transform.position + Vector3.up; // 플레이어 목표 지점 계산
            Vector3 direction = targetPoint - origin; // 목표 방향 계산
            float distance = direction.magnitude; // 목표까지 거리 계산

            if (!Physics.Raycast( // 플레이어 방향 Raycast 실행
                origin, // Ray 시작점
                direction.normalized, // Ray 방향
                out RaycastHit hit, // 첫 충돌 정보
                distance + 0.2f, // 플레이어까지 검사 거리
                Physics.AllLayers, // 전체 물리 레이어 검사
                QueryTriggerInteraction.Ignore)) // Trigger Collider 제외
            {
                return false; // 충돌 대상이 없으면 공격 불가 반환
            }

            return hit.collider.GetComponentInParent<PlayerController>() == targetPlayer; // 첫 충돌 대상이 플레이어인지 반환
        }

        void BeginNormalChase() // 고스트 전용 기습 종료 후 일반 추격 시작
        {
            isActivated = false; // 고스트 전용 상태 종료
            controller.enabled = true; // 이동 충돌 활성화
            SetVisual(true); // 고스트 외형 표시
            monsterAI.enabled = true; // 기존 MonsterAI 활성화
            monsterAI.Alert(); // 기존 FSM을 즉시 추격 상태로 전환
        }

        void SetVisual(bool visible) // 고스트 외형 표시 상태 변경
        {
            if (ghostVisual != null) // 외형 연결 여부 확인
            {
                ghostVisual.SetActive(visible); // 외형 활성 상태 적용
            }
        }
    }
}