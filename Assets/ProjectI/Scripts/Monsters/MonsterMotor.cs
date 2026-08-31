using ProjectI.Combat; // 공통 경직·넉백 상태 참조
using UnityEngine; // CharacterController와 이동·회전 기능 참조

namespace ProjectI.Monsters // 몬스터 공통 AI 네임스페이스
{
    [RequireComponent(typeof(CharacterController))] // 물리 충돌 이동을 위한 CharacterController 필수 지정
    public sealed class MonsterMotor : MonoBehaviour // Brain의 목적지를 실제 충돌 이동으로 변환하는 공통 몬스터 이동 계층
    {
        [SerializeField] private MonsterData data; // 이동 속도·회전 속도 데이터
        [SerializeField] private CombatReaction reaction; // 경직·넉백 중 이동 차단용 공통 반응 참조
        private CharacterController controller; // 실제 충돌 이동 컨트롤러
        private Vector3 destination; // 현재 이동 목적지
        private float requestedSpeed; // 현재 요청 이동 속도
        private bool hasDestination; // 이동 목적지 존재 여부
        private float verticalVelocity; // 공중 상태 중력 속도

        public Vector3 Destination => destination; // 현재 목적지 공개
        public bool HasDestination => hasDestination; // 현재 이동 요청 존재 여부 공개
        public float RemainingDistance => hasDestination ? Vector3.ProjectOnPlane(destination - transform.position, Vector3.up).magnitude : 0f; // 목적지까지 수평 잔여 거리 공개

        private void Awake() // 몬스터 이동 기능 초기화
        {
            controller = GetComponent<CharacterController>(); // 같은 오브젝트 CharacterController 조회

            if (reaction == null) // 경직 반응 참조 누락 확인
            {
                reaction = GetComponent<CombatReaction>(); // 같은 몬스터 공통 반응 자동 조회
            }
        }

        private void Update() // 프레임별 목적지 이동과 중력 처리
        {
            if (controller == null || data == null) // 이동 필수 참조 존재 여부 확인
            {
                return; // 이동 처리 중단
            }

            bool blockedByReaction = reaction != null && (reaction.IsStaggered || reaction.KnockbackDistanceRemaining > 0.01f); // 경직·넉백 중 AI 이동 차단 여부 계산
            Vector3 planarVelocity = Vector3.zero; // 이번 프레임 수평 이동 초기화

            if (!blockedByReaction && hasDestination) // 정상 이동 가능한 목적지 존재 여부 확인
            {
                Vector3 delta = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up); // 목적지까지 수평 방향 계산
                float distance = delta.magnitude; // 목적지까지 수평 거리 계산

                if (distance > 0.18f) // 실제 이동이 필요한 거리인지 확인
                {
                    Vector3 direction = ResolveSteering(delta.normalized); // 간단한 장애물 회피를 포함한 이동 방향 계산
                    planarVelocity = direction * requestedSpeed; // 요청 속도를 반영한 수평 이동 속도 계산
                    FaceDirection(direction); // 이동 방향으로 몸체 부드럽게 회전
                }
                else // 목적지에 거의 도착한 경우 처리
                {
                    hasDestination = false; // 이동 목적지 해제
                }
            }

            if (controller.isGrounded && verticalVelocity < 0f) // 지상에서 하강 속도 상태 확인
            {
                verticalVelocity = -2f; // 지면 밀착용 작은 하강 속도 적용
            }
            else // 공중 상태 처리
            {
                verticalVelocity += Physics.gravity.y * Time.deltaTime; // Unity 중력 가속도 누적
            }

            Vector3 motion = planarVelocity + (Vector3.up * verticalVelocity); // 수평 이동과 중력 이동 결합
            controller.Move(motion * Time.deltaTime); // CharacterController 충돌을 반영한 실제 이동 실행
        }

        public void Configure(MonsterData targetData, CombatReaction targetReaction) // Day17 자동 Setup용 이동 참조 구성
        {
            data = targetData; // 몬스터 이동 데이터 저장
            reaction = targetReaction; // 공통 경직 반응 저장
        }

        public void MoveTo(Vector3 worldDestination, float speed) // 지정 목적지로 이동 요청
        {
            destination = worldDestination; // 새 목적지 저장
            requestedSpeed = Mathf.Max(0f, speed); // 이동 속도 음수 방지
            hasDestination = true; // 이동 요청 활성화
        }

        public void RetreatFrom(Vector3 threatPosition, float speed) // 위협 대상 반대 방향으로 거리 확보 이동 요청
        {
            Vector3 away = Vector3.ProjectOnPlane(transform.position - threatPosition, Vector3.up); // 위협 반대 수평 방향 계산

            if (away.sqrMagnitude < 0.001f) // 대상과 위치가 거의 겹쳤는지 확인
            {
                away = -transform.forward; // 현재 후방 방향을 대체 후퇴 방향으로 사용
            }

            destination = transform.position + (away.normalized * 2.8f); // 북쪽 경계벽에 바로 붙지 않도록 약 2.8m 뒤쪽을 임시 후퇴 목적지로 지정
            requestedSpeed = Mathf.Max(0f, speed); // 후퇴 속도 음수 방지
            hasDestination = true; // 후퇴 이동 요청 활성화
        }

        public void Stop() // 현재 이동 요청 즉시 중지
        {
            hasDestination = false; // 목적지 이동 해제
            requestedSpeed = 0f; // 요청 이동 속도 초기화
        }

        public void FaceTarget(Vector3 worldPosition) // 이동 없이 지정 위치를 바라보도록 회전
        {
            Vector3 direction = Vector3.ProjectOnPlane(worldPosition - transform.position, Vector3.up); // 목표까지 수평 방향 계산
            FaceDirection(direction); // 공통 회전 함수로 목표 방향 회전
        }

        private Vector3 ResolveSteering(Vector3 desiredDirection) // 정면 장애물에서 단순 좌우 우회 방향 선택
        {
            Vector3 origin = transform.position + (Vector3.up * 0.9f); // 몸체 중심 높이 장애물 검사 시작점 계산

            if (!Physics.SphereCast(origin, 0.32f, desiredDirection, out RaycastHit hit, 0.8f, ~0, QueryTriggerInteraction.Ignore) || IsSelfCollider(hit.collider)) // 정면 장애물 존재 여부 확인
            {
                return desiredDirection; // 장애물 없으면 원래 목적지 방향 사용
            }

            Vector3 left = Quaternion.Euler(0f, -48f, 0f) * desiredDirection; // 왼쪽 우회 후보 방향 생성
            Vector3 right = Quaternion.Euler(0f, 48f, 0f) * desiredDirection; // 오른쪽 우회 후보 방향 생성
            bool leftClear = !Physics.SphereCast(origin, 0.30f, left, out _, 0.9f, ~0, QueryTriggerInteraction.Ignore); // 왼쪽 우회 통로 확인
            bool rightClear = !Physics.SphereCast(origin, 0.30f, right, out _, 0.9f, ~0, QueryTriggerInteraction.Ignore); // 오른쪽 우회 통로 확인

            if (leftClear) // 왼쪽 우회 가능 여부 확인
            {
                return left; // 왼쪽 우회 방향 반환
            }

            if (rightClear) // 오른쪽 우회 가능 여부 확인
            {
                return right; // 오른쪽 우회 방향 반환
            }

            return desiredDirection; // 양쪽이 막힌 경우 CharacterController 충돌에 맡기고 원래 방향 유지
        }

        private void FaceDirection(Vector3 direction) // 수평 방향으로 몬스터 몸체 부드럽게 회전
        {
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up); // 수직 성분을 제거한 회전 방향 계산

            if (flat.sqrMagnitude < 0.0001f || data == null) // 유효 회전 방향·데이터 여부 확인
            {
                return; // 회전 처리 생략
            }

            Quaternion targetRotation = Quaternion.LookRotation(flat.normalized, Vector3.up); // 목표 수평 회전 생성
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, data.TurnSpeed * Time.deltaTime); // 설정 속도로 목표 방향 회전
        }

        private bool IsSelfCollider(Collider collider) // 장애물 Collider가 현재 몬스터 자신인지 확인
        {
            return collider != null && (collider.transform == transform || collider.transform.IsChildOf(transform)); // 자기 루트·자식 Collider 여부 반환
        }
    }
}
