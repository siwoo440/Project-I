using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    [RequireComponent(typeof(MonsterAI))] // 체력과 피격 처리용 MonsterAI 지정
    [RequireComponent(typeof(CharacterController))] // 석상 이동용 CharacterController 지정
    public class SmilingStatue : MonoBehaviour // 시선을 돌리면 움직이는 웃는 석상
    {
        [Header("활성화")] // Inspector 활성화 설정 구분
        [Tooltip("생성 후 행동 시작 대기시간")] [SerializeField] float activationDelay = 1.5f; // 생성 후 행동 시작 대기시간


        [Header("이동")] // Inspector 이동 설정 구분
        [Tooltip("시선이 벗어났을 때 이동속도")] [SerializeField] float moveSpeed = 2.6f; // 시선이 벗어났을 때 이동속도
        [Tooltip("플레이어 방향 회전속도")] [SerializeField] float rotationSpeed = 8f; // 플레이어 방향 회전속도
        [Tooltip("석상 중력 가속도")] [SerializeField] float gravity = -20f; // 석상 중력 가속도

        [Header("공격")] // Inspector 공격 설정 구분
        [Tooltip("석상 공격 피해량")] [SerializeField] float attackDamage = 45f; // 석상 공격 피해량
        [Tooltip("석상 공격 사거리")] [SerializeField] float attackRange = 1.45f; // 석상 공격 사거리
        [Tooltip("석상 공격 대기시간")] [SerializeField] float attackCooldown = 1.2f; // 석상 공격 대기시간

        [Header("디버그")] // Inspector 디버그 설정 구분
        [Tooltip("상태 변경 로그 표시 여부")] [SerializeField] bool showDebug = true; // 상태 변경 로그 표시 여부

        CharacterController controller; // 석상 CharacterController
        MonsterAI monsterAI; // 석상 체력과 방어력 처리용 AI
        ThreatFeedback threatFeedback; // 석상 이동과 공격 피드백
        PlayerController targetPlayer; // 석상이 추적할 플레이어
        Camera targetCamera; // 플레이어 시선 판정용 카메라
        Renderer[] statueRenderers; // 석상 전체 화면 영역 계산용 Renderer 목록


        float activationTime; // 석상 행동 시작 시각
        float verticalVelocity; // 석상 수직 이동속도
        float lastAttackTime = -999f; // 마지막 공격 시각
        bool isActivated; // 석상 활성화 여부
        bool isWatched; // 현재 플레이어가 바라보는지 여부
        bool previousWatchedState; // 이전 프레임 시선 상태

        public bool IsWatched => isWatched; // 외부 현재 시선 상태 확인
        public PlayerController TargetPlayer => targetPlayer; // 외부 목표 플레이어 확인

        void Awake() // 석상 필수 컴포넌트 초기화
        {
            controller = GetComponent<CharacterController>(); // CharacterController 가져오기
            monsterAI = GetComponent<MonsterAI>(); // MonsterAI 가져오기
            threatFeedback = GetComponent<ThreatFeedback>(); // 석상 공통 피드백 가져오기
            statueRenderers = GetComponentsInChildren<Renderer>(true); // 석상과 모든 자식 외형 Renderer 검색
            monsterAI.enabled = false; // 일반 FSM을 끄고 석상 전용 행동 사용
        }

        void Start() // 수동 배치된 석상의 플레이어 자동 검색
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

        void Update() // 시선 판정과 이동 및 공격 처리
        {
            Vector3 horizontalVelocity = Vector3.zero; // 이번 프레임 수평 이동속도 초기화

            if (CanAct()) // 석상이 행동할 수 있는 상태인지 확인
            {
                isWatched = IsPlayerLookingAtStatue(); // 현재 플레이어 시선 판정

                if (isWatched != previousWatchedState) // 시선 상태 변화 여부 확인
                {
                    LogWatchedState(); // 시선 상태 변화 로그 출력
                    previousWatchedState = isWatched; // 현재 상태를 이전 상태로 저장
                }

                if (!isWatched) // 플레이어 시선이 벗어났는지 확인
                {
                    horizontalVelocity = ChaseAndAttack(); // 플레이어 추적과 공격 처리
                }
            }
            else // 아직 행동할 수 없는 상태
            {
                isWatched = false; // 시선 상태 초기화
            }

            ApplyMovement(horizontalVelocity); // 수평 이동과 중력 적용
        }

        public void Activate(PlayerController target) // 지정된 플레이어를 석상 목표로 설정
        {
            if (target == null) // 전달된 플레이어 존재 여부 확인
            {
                return; // 활성화 중단
            }

            targetPlayer = target; // 목표 플레이어 저장
            targetCamera = target.GetComponentInChildren<Camera>(); // 플레이어 자식 카메라 검색
            activationTime = Time.time + Mathf.Max(0f, activationDelay); // 행동 시작 시각 계산
            isActivated = true; // 석상 활성화 상태 저장
            isWatched = false; // 시선 상태 초기화
            previousWatchedState = true; // 처음 화면 밖에서 움직일 때 상태 변화를 발생시키기 위한 초기값

            if (showDebug) // 디버그 로그 표시 여부 확인
            {
                Debug.Log($"[SmilingStatue] 목표 지정 — {activationDelay:F1}초 후 행동 시작"); // 목표 지정 결과 출력
            }
        }

        bool CanAct() // 석상이 현재 행동할 수 있는지 확인
        {
            if (!isActivated) // 활성화 여부 확인
            {
                return false; // 행동 불가 반환
            }

            if (targetPlayer == null || targetPlayer.IsDead) // 목표 유효성과 사망 상태 확인
            {
                return false; // 행동 불가 반환
            }

            if (targetCamera == null) // 플레이어 카메라 존재 여부 확인
            {
                targetCamera = targetPlayer.GetComponentInChildren<Camera>(); // 카메라 다시 검색
            }

            return targetCamera != null && Time.time >= activationTime; // 카메라와 활성화 시간 확인
        }

        bool IsPlayerLookingAtStatue() // 석상 외형이 플레이어 화면에 보이는지 판정
        {
            Bounds statueBounds = GetStatueBounds(); // 석상 전체 외형의 월드 영역 계산
            Plane[] cameraPlanes = GeometryUtility.CalculateFrustumPlanes(targetCamera); // 플레이어 카메라 화면 경계 계산

            if (!GeometryUtility.TestPlanesAABB(cameraPlanes, statueBounds)) // 석상 영역과 카메라 화면의 교차 여부 확인
            {
                return false; // 석상이 화면 밖이면 시선 이탈 반환
            }

            Vector3 center = statueBounds.center; // 석상 영역 중심점 계산
            Vector3 top = new Vector3(center.x, statueBounds.max.y, center.z); // 석상 위쪽 확인 지점 계산
            Vector3 bottom = new Vector3(center.x, statueBounds.min.y, center.z); // 석상 아래쪽 확인 지점 계산
            Vector3 left = new Vector3(statueBounds.min.x, center.y, center.z); // 석상 왼쪽 확인 지점 계산
            Vector3 right = new Vector3(statueBounds.max.x, center.y, center.z); // 석상 오른쪽 확인 지점 계산

            if (HasClearSightToPoint(center)) // 석상 중심이 실제로 보이는지 확인
            {
                return true; // 화면에 보이는 상태 반환
            }

            if (HasClearSightToPoint(top)) // 석상 위쪽이 실제로 보이는지 확인
            {
                return true; // 화면에 보이는 상태 반환
            }

            if (HasClearSightToPoint(bottom)) // 석상 아래쪽이 실제로 보이는지 확인
            {
                return true; // 화면에 보이는 상태 반환
            }

            if (HasClearSightToPoint(left)) // 석상 왼쪽이 실제로 보이는지 확인
            {
                return true; // 화면에 보이는 상태 반환
            }

            if (HasClearSightToPoint(right)) // 석상 오른쪽이 실제로 보이는지 확인
            {
                return true; // 화면에 보이는 상태 반환
            }

            return false; // 화면 안에 있지만 벽에 완전히 가려진 상태 반환
        }
        Bounds GetStatueBounds() // 석상 자식 Renderer를 합친 전체 외형 영역 계산
        {
            Bounds combinedBounds = new Bounds( // Renderer가 없을 때 사용할 기본 영역 생성
                transform.position + Vector3.up * 1.35f, // 기본 영역 중심 위치
                new Vector3(1f, 2.7f, 1f)); // 기본 영역 크기

            bool foundRenderer = false; // 유효한 Renderer 검색 여부 초기화

            foreach (Renderer statueRenderer in statueRenderers) // 석상의 모든 Renderer 순회
            {
                if (statueRenderer == null) // Renderer 유효성 확인
                {
                    continue; // 삭제된 Renderer 제외
                }

                if (!statueRenderer.enabled || !statueRenderer.gameObject.activeInHierarchy) // 실제 표시 상태 확인
                {
                    continue; // 비활성화된 외형 제외
                }

                if (!foundRenderer) // 첫 번째 유효 Renderer인지 확인
                {
                    combinedBounds = statueRenderer.bounds; // 전체 영역을 첫 Renderer 영역으로 설정
                    foundRenderer = true; // Renderer 검색 성공 상태 저장
                }
                else // 두 번째 이후 Renderer 처리
                {
                    combinedBounds.Encapsulate(statueRenderer.bounds); // 현재 Renderer 영역을 전체 영역에 포함
                }
            }

            return combinedBounds; // 계산된 석상 전체 영역 반환
        }

        bool HasClearSightToPoint(Vector3 targetPoint) // 카메라와 석상 확인 지점 사이 장애물 검사
        {
            Vector3 origin = targetCamera.transform.position; // 카메라 Ray 시작 위치 계산
            Vector3 difference = targetPoint - origin; // 카메라에서 확인 지점까지 방향 계산
            float distance = difference.magnitude; // 확인 지점까지 거리 계산

            if (distance < 0.01f) // Raycast 가능한 거리인지 확인
            {
                return true; // 카메라와 거의 같은 위치면 보이는 상태 반환
            }

            RaycastHit[] hits = Physics.RaycastAll( // 확인 지점까지 모든 충돌 검색
                origin, // Ray 시작 위치
                difference.normalized, // Ray 진행 방향
                distance + 0.2f, // 석상 표면을 포함하는 검사 거리
                Physics.AllLayers, // 전체 물리 레이어 검사
                QueryTriggerInteraction.Ignore); // Trigger Collider 제외

            float nearestDistance = float.MaxValue; // 가장 가까운 유효 충돌 거리 초기화
            SmilingStatue nearestStatue = null; // 가장 가까운 충돌의 석상 초기화

            foreach (RaycastHit hit in hits) // 검색된 모든 충돌 순회
            {
                PlayerController hitPlayer = hit.collider.GetComponentInParent<PlayerController>(); // 충돌 대상의 플레이어 검색

                if (hitPlayer == targetPlayer) // 플레이어 자신의 Collider인지 확인
                {
                    continue; // 플레이어 Collider 제외
                }

                if (hit.distance >= nearestDistance) // 기존 충돌보다 멀리 있는지 확인
                {
                    continue; // 더 먼 충돌 제외
                }

                nearestDistance = hit.distance; // 가장 가까운 충돌 거리 갱신
                nearestStatue = hit.collider.GetComponentInParent<SmilingStatue>(); // 가장 가까운 충돌의 석상 검색
            }

            return nearestStatue == this; // 가장 먼저 보이는 대상이 현재 석상인지 반환
        }

        Vector3 ChaseAndAttack() // 시선이 벗어났을 때 플레이어 추적과 공격
        {
            Vector3 difference = targetPlayer.transform.position - transform.position; // 플레이어 방향 계산
            difference.y = 0f; // 수직 방향 제외
            float distance = difference.magnitude; // 플레이어까지 수평거리 계산

            if (difference.sqrMagnitude < 0.01f) // 유효한 이동 방향인지 확인
            {
                return Vector3.zero; // 이동 없음 반환
            }

            Vector3 direction = difference.normalized; // 플레이어 방향 단위 벡터 계산
            Quaternion targetRotation = Quaternion.LookRotation(direction); // 플레이어 방향 회전값 계산
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime); // 플레이어 방향 회전 적용

            if (distance <= attackRange) // 플레이어가 공격 범위 안인지 확인
            {
                TryAttack(); // 플레이어 공격 시도
                return Vector3.zero; // 공격 중 이동 정지
            }

            return direction * moveSpeed; // 플레이어 방향 이동속도 반환
        }

        void TryAttack() // 공격 대기시간 확인 후 플레이어 피해 적용
        {
            if (Time.time - lastAttackTime < attackCooldown) // 공격 대기시간 확인
            {
                return; // 공격 처리 중단
            }

            lastAttackTime = Time.time; // 마지막 공격 시각 갱신
            targetPlayer.TakeDamage(Mathf.Max(0f, attackDamage)); // 플레이어에게 석상 피해 적용

            if (threatFeedback != null) // 석상 피드백 존재 여부 확인
            {
                threatFeedback.PlayAttack(); // 석상 공격 소리와 파티클 재생
            }

            if (showDebug) // 디버그 로그 표시 여부 확인
            {
                Debug.Log($"[SmilingStatue] 공격 — {attackDamage:F0} 피해"); // 석상 공격 결과 출력
            }
        }

        void ApplyMovement(Vector3 horizontalVelocity) // 수평 이동과 중력을 CharacterController에 적용
        {
            if (controller.isGrounded && verticalVelocity < 0f) // 지면 접촉과 하강 상태 확인
            {
                verticalVelocity = -2f; // 지면 접촉 유지속도 설정
            }

            verticalVelocity += gravity * Time.deltaTime; // 수직속도에 중력 적용
            Vector3 finalVelocity = horizontalVelocity + Vector3.up * verticalVelocity; // 수평과 수직 이동속도 결합
            controller.Move(finalVelocity * Time.deltaTime); // CharacterController 이동 적용
        }

        void LogWatchedState() // 플레이어 화면 노출 상태 변화 출력과 경고 재생
        {
            if (!isWatched && threatFeedback != null) // 석상이 화면 밖으로 사라졌는지 확인
            {
                threatFeedback.PlayWarning(); // 석상 이동 시작 경고 재생
            }

            if (!showDebug) // 디버그 로그 표시 여부 확인
            {
                return; // 로그 출력 중단
            }

            string stateText = isWatched ? "화면 노출 — 정지" : "화면 이탈 — 접근 시작"; // 현재 상태 안내 계산
            Debug.Log($"[SmilingStatue] {stateText}"); // 석상 상태 출력
        }
    }
}