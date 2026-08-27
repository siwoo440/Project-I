using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.World // 월드 기능 네임스페이스
{
    [RequireComponent(typeof(Rigidbody))] // 리지드바디 필수 지정
    public sealed class MovingPlatform : MonoBehaviour // 왕복 이동 플랫폼
    {
        [SerializeField] private Vector3 travelOffset = new Vector3(10f, 0f, 0f); // 시작점 기준 이동 거리
        [SerializeField] private float moveSpeed = 2f; // 플랫폼 이동 속도
        [SerializeField] private float waitDuration = 0.5f; // 양 끝 대기 시간
        private Rigidbody body; // 플랫폼 리지드바디
        private Vector3 startPosition; // 시작 월드 위치
        private bool movingToEnd = true; // 현재 끝점 방향 이동 여부
        private float waitRemaining; // 남은 대기 시간

        private void Awake() // 플랫폼 초기화
        {
            body = GetComponent<Rigidbody>(); // 리지드바디 참조 획득
            body.isKinematic = true; // 외부 힘의 영향을 받지 않도록 설정
            body.interpolation = RigidbodyInterpolation.Interpolate; // 프레임 사이 이동 보간 활성화
            startPosition = body.position; // 시작 위치 저장
        }

        private void FixedUpdate() // 물리 프레임별 플랫폼 이동
        {
            if (waitRemaining > 0f) // 끝점 대기 상태 확인
            {
                waitRemaining = Mathf.Max(0f, waitRemaining - Time.fixedDeltaTime); // 남은 대기 시간 감소
                return; // 대기 중 이동 처리 종료
            }

            Vector3 targetPosition = movingToEnd ? startPosition + travelOffset : startPosition; // 현재 목표 위치 계산
            Vector3 nextPosition = Vector3.MoveTowards(body.position, targetPosition, moveSpeed * Time.fixedDeltaTime); // 다음 이동 위치 계산
            body.MovePosition(nextPosition); // 키네마틱 리지드바디 이동 적용

            if ((nextPosition - targetPosition).sqrMagnitude <= 0.0001f) // 목표 위치 도착 여부 확인
            {
                movingToEnd = !movingToEnd; // 다음 이동 방향 반전
                waitRemaining = waitDuration; // 끝점 대기 시간 설정
            }
        }

        public void Configure(Vector3 offset, float speed, float waitTime) // 에디터 자동 설정용 이동 값 지정
        {
            travelOffset = offset; // 이동 거리 저장
            moveSpeed = Mathf.Max(0.1f, speed); // 이동 속도 저장
            waitDuration = Mathf.Max(0f, waitTime); // 대기 시간 저장
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            moveSpeed = Mathf.Max(0.1f, moveSpeed); // 이동 속도 최소값 보정
            waitDuration = Mathf.Max(0f, waitDuration); // 대기 시간 음수 방지
        }
    }
}
