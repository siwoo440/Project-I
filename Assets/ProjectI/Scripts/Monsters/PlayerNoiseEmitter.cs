using ProjectI.Player; // 플레이어 이동 상태 참조
using UnityEngine; // MonoBehaviour와 시간 기능 참조

namespace ProjectI.Monsters // 몬스터 공통 AI 네임스페이스
{
    [RequireComponent(typeof(PlayerMovement))] // 플레이어 이동 상태 필수 지정
    public sealed class PlayerNoiseEmitter : MonoBehaviour // 걷기·달리기 상태를 청각 이벤트로 변환하는 플레이어 소음 발생기
    {
        [SerializeField] private float walkRadius = 6f; // 일반 이동 발소리 전달 거리
        [SerializeField] private float sprintRadius = 14f; // 달리기 발소리 전달 거리
        [SerializeField] private float walkInterval = 0.72f; // 일반 이동 발소리 발생 간격
        [SerializeField] private float sprintInterval = 0.42f; // 달리기 발소리 발생 간격
        private PlayerMovement movement; // 기존 플레이어 이동 참조
        private float nextNoiseTime; // 다음 발소리 발생 가능 시각

        private void Awake() // 플레이어 소음 기능 초기화
        {
            movement = GetComponent<PlayerMovement>(); // 같은 플레이어 이동 컴포넌트 조회
        }

        private void Update() // 프레임별 이동 소음 발생 조건 확인
        {
            if (movement == null || !movement.IsGrounded || movement.CurrentPlanarSpeed < 0.35f) // 지상 실제 이동 상태 여부 확인
            {
                return; // 정지·공중 상태에서는 발소리 발생 생략
            }

            if (Time.time < nextNoiseTime) // 발소리 간격 대기 여부 확인
            {
                return; // 다음 발소리 시점까지 대기
            }

            bool sprinting = movement.IsSprinting; // 현재 달리기 여부 조회
            float radius = sprinting ? sprintRadius : walkRadius; // 이동 상태별 청각 반경 선택
            float loudness = sprinting ? 0.85f : 0.38f; // 달리기를 더 큰 소음으로 설정
            string label = sprinting ? "Sprint Footstep" : "Footstep"; // F1 진단용 소음 이름 선택
            MonsterNoiseSystem.Emit(gameObject, transform.position, radius, loudness, MonsterNoiseKind.Footstep, label); // 공통 청각 시스템에 발소리 전달
            nextNoiseTime = Time.time + (sprinting ? sprintInterval : walkInterval); // 다음 발소리 발생 시각 계산
        }

        public void Configure(float walk, float sprint, float walkStep, float sprintStep) // Day17 자동 Setup용 발소리 수치 구성
        {
            walkRadius = Mathf.Max(0f, walk); // 걷기 소음 거리 음수 방지
            sprintRadius = Mathf.Max(walkRadius, sprint); // 달리기 소음을 걷기 이상으로 보정
            walkInterval = Mathf.Max(0.1f, walkStep); // 걷기 발소리 간격 최소값 보정
            sprintInterval = Mathf.Max(0.1f, sprintStep); // 달리기 발소리 간격 최소값 보정
        }
    }
}
