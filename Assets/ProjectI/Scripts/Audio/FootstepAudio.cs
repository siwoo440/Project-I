using ProjectI.Player; // 이동·웅크리기
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Audio // 소리 네임스페이스
{
    [RequireComponent(typeof(PlayerMovement))] // 이동 필요
    public sealed class FootstepAudio : MonoBehaviour // 이동 거리에 맞춘 발소리와 착지음 (바닥 이름으로 재질 구분)
    {
        [SerializeField] private float walkStride = 1.9f; // 걷기 보폭 (m)
        [SerializeField] private float sprintStride = 2.6f; // 달리기 보폭
        [SerializeField] private float minSpeed = 0.6f; // 발소리 최소 속도
        [SerializeField] private float landAirTime = 0.35f; // 착지음 최소 체공 시간
        private PlayerMovement movement; // 이동
        private PlayerCrouch crouch; // 웅크리기
        private float travelled; // 누적 거리
        private float airTime; // 체공 시간
        private bool wasGrounded = true; // 이전 지상 여부

        public int StepCount { get; private set; } // 발소리 수 (검증용)

        private void Awake() // 참조
        {
            movement = GetComponent<PlayerMovement>(); // 이동
            crouch = GetComponent<PlayerCrouch>(); // 웅크리기
        }

        private void Update() // 발소리
        {
            bool grounded = movement.IsGrounded || movement.IsClimbing; // 지상
            float speed = movement.CurrentPlanarSpeed; // 속도

            if (!grounded) // 공중
            {
                airTime += Time.deltaTime; // 체공
                wasGrounded = false; // 기록
                return; // 종료
            }

            if (!wasGrounded && airTime >= landAirTime) // 착지
            {
                SoundPlayer.PlayAt(SoundId.Land, transform.position, Mathf.Clamp(airTime, 0.4f, 1f)); // 착지음
                travelled = 0f; // 보폭 초기화
            }

            wasGrounded = true; // 기록
            airTime = 0f; // 초기화

            if (speed < minSpeed) // 거의 멈춤
            {
                travelled = Mathf.Min(travelled, walkStride * 0.5f); // 다음 걸음은 반 보폭 뒤
                return; // 종료
            }

            travelled += speed * Time.deltaTime; // 누적
            float stride = movement.IsSprinting ? sprintStride : walkStride; // 보폭

            if (travelled < stride) // 아직
            {
                return; // 종료
            }

            travelled -= stride; // 다음 걸음
            bool crouching = crouch != null && crouch.IsCrouching; // 웅크림
            float volume = crouching ? 0.15f : movement.IsSprinting ? 0.55f : 0.35f; // 세기
            SoundPlayer.PlayAt(SurfaceSound(), transform.position, volume, 0.08f, 18f); // 재생
            StepCount++; // 기록
        }

        private SoundId SurfaceSound() // 발밑 재질
        {
            Vector3 origin = transform.position + (Vector3.up * 0.3f); // 발 위
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.2f, ~0, QueryTriggerInteraction.Ignore)) // 바닥
            {
                return SoundId.FootstepStone; // 기본
            }

            string key = hit.collider.name; // 이름
            Renderer renderer = hit.collider.GetComponent<Renderer>(); // 재질

            if (renderer != null && renderer.sharedMaterial != null) // 재질 이름
            {
                key += " " + renderer.sharedMaterial.name; // 합침
            }

            key = key.ToLowerInvariant(); // 소문자

            if (key.Contains("wood") || key.Contains("floor") || key.Contains("board") || key.Contains("plank") || key.Contains("wagon") || key.Contains("deck")) // 나무
            {
                return SoundId.FootstepWood; // 나무
            }

            if (key.Contains("grass") || key.Contains("ground") || key.Contains("earth") || key.Contains("dirt") || key.Contains("hay") || key.Contains("terrain")) // 흙
            {
                return SoundId.FootstepDirt; // 흙
            }

            return SoundId.FootstepStone; // 돌
        }
    }
}
