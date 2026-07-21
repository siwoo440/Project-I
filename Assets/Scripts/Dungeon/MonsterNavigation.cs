using UnityEngine; // Unity Transform과 이동 방향 계산 기능 사용
using UnityEngine.AI; // NavMeshAgent와 NavMesh 경로 검색 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    [DisallowMultipleComponent] // 같은 오브젝트에 중복 추가되는 것을 방지
    [RequireComponent(typeof(NavMeshAgent))] // 경로 계산을 위한 NavMeshAgent 필수 지정
    public class MonsterNavigation : MonoBehaviour // CharacterController용 NavMesh 이동 방향 제공
    {
        [Header("경로 검색")] // Inspector 경로 검색 설정 구분
        [SerializeField] float selfSampleDistance = 2f; // 몬스터 주변 NavMesh 검색 거리
        [SerializeField] float targetSampleDistance = 3f; // 목표 주변 NavMesh 검색 거리
        [SerializeField] float repathInterval = 0.15f; // 새로운 경로를 계산하는 시간 간격
        [SerializeField] float targetMoveThreshold = 0.5f; // 목표가 이동했을 때 경로를 갱신할 최소 거리

        [Header("이동 설정")] // Inspector NavMeshAgent 설정 구분
        [SerializeField] float stoppingDistance = 0.1f; // 목표 앞에서 멈출 거리
        [SerializeField] float acceleration = 20f; // 경로 이동 방향 가속도
        [SerializeField] bool showDebugWarning = true; // NavMesh 진입 실패 경고 표시 여부

        NavMeshAgent agent; // 실제 경로를 계산할 NavMeshAgent
        Vector3 lastTarget; // 마지막으로 경로를 요청한 목표 위치
        float nextRepathTime; // 다음 경로 갱신 가능 시각
        bool hasDestination; // 현재 유효한 목표 경로 보유 여부
        bool warnedMissingNavMesh; // NavMesh 누락 경고 중복 출력 방지

        public bool IsOnNavMesh => agent != null && agent.enabled && agent.isOnNavMesh; // 현재 몬스터의 NavMesh 진입 상태 반환

        void Awake() // NavMeshAgent를 CharacterController 보조 방식으로 초기화
        {
            agent = GetComponent<NavMeshAgent>(); // 같은 오브젝트의 NavMeshAgent 가져오기
            agent.updatePosition = false; // Transform 이동은 기존 CharacterController가 담당하도록 설정
            agent.updateRotation = false; // 회전은 기존 MonsterAI가 담당하도록 설정
            agent.autoBraking = false; // 통로 이동 중 불필요한 감속 방지
            agent.acceleration = Mathf.Max(1f, acceleration); // Inspector 가속도를 안전한 값으로 적용
            agent.stoppingDistance = Mathf.Max(0f, stoppingDistance); // 정지 거리를 음수가 되지 않도록 적용
        }

        void OnEnable() // 컴포넌트 활성화 시 경로 상태 초기화
        {
            hasDestination = false; // 이전 목표 경로 제거
            nextRepathTime = 0f; // 즉시 첫 경로 검색 허용
            warnedMissingNavMesh = false; // 경고 출력 상태 초기화
        }

        void LateUpdate() // CharacterController 이동 이후 Agent 내부 위치 동기화
        {
            if (!IsOnNavMesh) // Agent가 유효한 NavMesh 위에 있는지 확인
            {
                return; // 위치 동기화 중단
            }

            agent.nextPosition = transform.position; // CharacterController가 이동시킨 실제 위치를 Agent에 전달
        }

        public bool TryGetMoveDirection(Vector3 target, float speed, out Vector3 moveDirection) // 목표까지의 NavMesh 이동 방향 계산
        {
            moveDirection = Vector3.zero; // 경로 대기 또는 실패에 대비해 기본 방향 설정

            if (!EnsureOnNavMesh()) // 몬스터를 NavMesh 위에 배치할 수 있는지 확인
            {
                return false; // NavMesh 사용 실패를 MonsterAI에 전달
            }

            agent.nextPosition = transform.position; // 경로 계산 전에 실제 Transform 위치 동기화
            agent.speed = Mathf.Max(0f, speed); // 현재 MonsterAI 이동속도를 Agent에 적용
            agent.acceleration = Mathf.Max(1f, acceleration); // Inspector 가속도 적용
            agent.stoppingDistance = Mathf.Max(0f, stoppingDistance); // 목표 정지 거리 적용

            float safeRepathInterval = Mathf.Max(0.02f, repathInterval); // 지나치게 짧은 경로 갱신 간격 방지
            float safeMoveThreshold = Mathf.Max(0.01f, targetMoveThreshold); // 목표 이동 거리 기준 보정
            bool targetMoved = (target - lastTarget).sqrMagnitude >= safeMoveThreshold * safeMoveThreshold; // 이전 목표에서 충분히 이동했는지 확인
            bool shouldRepath = !hasDestination || targetMoved || Time.time >= nextRepathTime; // 경로를 다시 계산해야 하는지 확인

            if (shouldRepath) // 새로운 경로 계산 조건 확인
            {
                float safeTargetDistance = Mathf.Max(0.1f, targetSampleDistance); // 목표 NavMesh 검색 거리 보정

                if (NavMesh.SamplePosition(target, out NavMeshHit targetHit, safeTargetDistance, NavMesh.AllAreas)) // 목표 주변의 이동 가능 위치 검색
                {
                    hasDestination = agent.SetDestination(targetHit.position); // 검색된 위치를 Agent 목표로 설정
                    lastTarget = target; // 이번 목표 위치 저장
                    nextRepathTime = Time.time + safeRepathInterval; // 다음 경로 계산 가능 시각 저장
                }
                else // 목표 주변에서 NavMesh를 찾지 못한 경우
                {
                    hasDestination = false; // 유효한 목표 경로가 없음을 저장
                    nextRepathTime = Time.time + safeRepathInterval; // 매 프레임 과도한 검색을 하지 않도록 지연
                    return true; // NavMesh는 사용 가능하므로 직선 이동 대신 현재 위치 대기
                }
            }

            if (agent.pathPending) // Agent가 아직 경로를 계산하고 있는지 확인
            {
                return true; // 경로 계산 완료 전에는 현재 위치 대기
            }

            if (!agent.hasPath || agent.pathStatus == NavMeshPathStatus.PathInvalid) // 이동 가능한 경로가 없는지 확인
            {
                return true; // 벽을 향한 직선 이동을 하지 않고 현재 위치 대기
            }

            Vector3 desiredVelocity = agent.desiredVelocity; // 경로의 다음 모서리를 향한 이동속도 가져오기
            desiredVelocity.y = 0f; // 수직 방향은 CharacterController 중력이 담당하도록 제거

            if (desiredVelocity.sqrMagnitude <= 0.001f) // 유효한 이동 방향이 없는지 확인
            {
                return true; // 경로는 사용 중이지만 이동 없이 대기
            }

            moveDirection = desiredVelocity.normalized; // MonsterAI에서 사용할 수평 단위 방향 반환
            warnedMissingNavMesh = false; // 정상 경로 사용 시 누락 경고 상태 초기화
            return true; // NavMesh 이동 방향 사용 성공 반환
        }

        bool EnsureOnNavMesh() // 현재 몬스터를 가장 가까운 NavMesh 위치에 연결
        {
            if (agent == null || !agent.enabled) // Agent가 없거나 비활성화되었는지 확인
            {
                return false; // NavMesh 사용 불가 반환
            }

            if (agent.isOnNavMesh) // 이미 NavMesh 위에 있는지 확인
            {
                return true; // 추가 위치 보정 없이 사용 가능 반환
            }

            float safeDistance = Mathf.Max(0.1f, selfSampleDistance); // 몬스터 주변 검색 거리 보정

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit selfHit, safeDistance, NavMesh.AllAreas)) // 몬스터 주변에서 가장 가까운 NavMesh 검색
            {
                bool warped = agent.Warp(selfHit.position); // Agent를 검색된 NavMesh 위치로 이동

                if (warped) // Agent 위치 보정 성공 여부 확인
                {
                    agent.nextPosition = transform.position; // 보정된 Transform 위치를 Agent 내부 위치와 동기화
                    warnedMissingNavMesh = false; // 누락 경고 상태 초기화
                    return true; // NavMesh 사용 가능 반환
                }
            }

            if (showDebugWarning && !warnedMissingNavMesh) // 경고 출력이 활성화되어 있고 아직 출력하지 않았는지 확인
            {
                Debug.LogWarning($"[MonsterNavigation] {name} 주변에서 NavMesh를 찾지 못했습니다."); // 현재 몬스터의 NavMesh 위치 누락 경고 출력
                warnedMissingNavMesh = true; // 같은 경고의 매 프레임 반복 방지
            }

            return false; // NavMesh 연결 실패 반환
        }

        public void StopNavigation() // 현재 Agent 경로를 안전하게 초기화
        {
            hasDestination = false; // 목표 경로 보유 상태 초기화

            if (IsOnNavMesh) // Agent가 NavMesh 위에 있는지 확인
            {
                agent.ResetPath(); // 현재 계산된 경로 제거
            }
        }
    }
}