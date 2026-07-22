using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    [RequireComponent(typeof(MonsterAI))] // 체력과 방어력 및 기존 피격 처리를 위한 MonsterAI 필수 지정
    [RequireComponent(typeof(CharacterController))] // 스토커 이동과 충돌을 위한 CharacterController 필수 지정
    public class Stalker : MonoBehaviour // 목표 플레이어를 지속적으로 추적해 즉사시키는 특수 몬스터
    {
        [Header("추적 설정")] // Inspector 추적 설정 구분
        [Tooltip("플레이어를 추적하는 이동속도")] [SerializeField] float moveSpeed = 5.2f; // 플레이어를 추적하는 이동속도
        [Tooltip("플레이어 방향으로 회전하는 속도")] [SerializeField] float rotationSpeed = 10f; // 플레이어 방향으로 회전하는 속도
        [Tooltip("생성 후 추적을 시작하기 전 유예시간")] [SerializeField] float activationDelay = 3f; // 생성 후 추적을 시작하기 전 유예시간

        [Header("즉사 공격")] // Inspector 즉사 공격 설정 구분
        [Tooltip("플레이어에게 즉사 공격을 적용할 거리")] [SerializeField] float killRange = 1.3f; // 플레이어에게 즉사 공격을 적용할 거리
        [Tooltip("즉사 공격 후 스토커가 제거되기까지의 시간")] [SerializeField] float disappearDelay = 0.5f; // 즉사 공격 후 스토커가 제거되기까지의 시간

        [Header("이동 물리")] // Inspector 이동 물리 설정 구분
        [Tooltip("스토커에 적용할 중력 가속도")] [SerializeField] float gravity = -20f; // 스토커에 적용할 중력 가속도

        [Header("디버그")] // Inspector 디버그 설정 구분
        [Tooltip("스토커 상태 로그를 출력할지 결정")] [SerializeField] bool showDebug = true; // 스토커 상태 로그를 출력할지 결정

        CharacterController controller; // 스토커 이동을 처리할 CharacterController
        MonsterAI monsterAI; // 체력과 방어력 및 피격 처리를 담당할 기존 MonsterAI
        PlayerController targetPlayer; // 스토커가 추적할 목표 플레이어

        float verticalVelocity; // 현재 스토커의 수직 이동속도
        float trackingStartTime; // 실제 추적을 시작할 게임 시각
        bool isTracking; // 현재 목표 추적이 활성화되었는지 저장
        bool hasAttacked; // 즉사 공격을 이미 실행했는지 저장

        public bool IsTracking => isTracking; // 외부에서 현재 추적 상태 확인
        public PlayerController TargetPlayer => targetPlayer; // 외부에서 현재 목표 플레이어 확인

        void Awake() // 스토커 이동과 피격 컴포넌트 초기화
        {
            controller = GetComponent<CharacterController>(); // 같은 오브젝트의 CharacterController 가져오기
            monsterAI = GetComponent<MonsterAI>(); // 같은 오브젝트의 MonsterAI 가져오기
            monsterAI.enabled = false; // 일반 몬스터 FSM을 끄고 스토커 전용 추적만 사용
        }

        void Start() // 수동 배치된 스토커의 플레이어 목표 자동 검색
        {
            if (targetPlayer == null) // 스폰 매니저가 목표를 지정하지 않았는지 확인
            {
                PlayerController foundPlayer = FindFirstObjectByType<PlayerController>(); // 현재 Scene에서 플레이어 검색

                if (foundPlayer != null) // 플레이어를 찾았는지 확인
                {
                    Activate(foundPlayer); // 찾은 플레이어를 스토커 목표로 지정
                }
            }
        }

        void Update() // 매 프레임 플레이어 추적과 중력 이동 처리
        {
            Vector3 horizontalVelocity = Vector3.zero; // 이번 프레임의 수평 이동속도 초기화

            if (CanTrackTarget()) // 목표 플레이어를 추적할 수 있는 상태인지 확인
            {
                Vector3 difference = targetPlayer.transform.position - transform.position; // 스토커에서 플레이어까지의 방향 계산
                difference.y = 0f; // 높이 차이를 수평 추적에서 제외
                float distance = difference.magnitude; // 플레이어까지의 수평거리 계산

                if (difference.sqrMagnitude > 0.01f) // 유효한 방향이 존재하는지 확인
                {
                    Vector3 direction = difference.normalized; // 플레이어 방향을 단위 벡터로 변환
                    Quaternion targetRotation = Quaternion.LookRotation(direction); // 플레이어를 바라보는 목표 회전 계산
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime); // 부드럽게 플레이어 방향으로 회전

                    if (distance > killRange) // 플레이어가 즉사 공격 범위 밖에 있는지 확인
                    {
                        horizontalVelocity = direction * moveSpeed; // 플레이어 방향으로 추적 이동속도 계산
                    }
                    else { KillTarget(); } // 플레이어가 즉사 공격 범위 안에 들어온 경우// 플레이어에게 즉사 공격 적용
                   
                }
            }

            if (controller.isGrounded && verticalVelocity < 0f) // 스토커가 바닥에 있고 하강 중인지 확인
            {
                verticalVelocity = -2f; // 바닥 접촉을 유지할 작은 하강속도 설정
            }

            verticalVelocity += gravity * Time.deltaTime; // 현재 수직속도에 중력 적용
            Vector3 finalVelocity = horizontalVelocity + Vector3.up * verticalVelocity; // 수평 추적과 수직 중력을 하나의 이동속도로 결합
            controller.Move(finalVelocity * Time.deltaTime); // CharacterController를 이용해 최종 이동 적용
        }

        public void Activate(PlayerController target) // 지정된 플레이어를 목표로 스토커 추적 활성화
        {
            if (target == null) { return; } // 전달된 목표 플레이어가 없는지 확인 // 추적 활성화 중단

            targetPlayer = target; // 전달된 플레이어를 현재 추적 목표로 저장
            trackingStartTime = Time.time + Mathf.Max(0f, activationDelay); // 유예시간을 적용한 실제 추적 시작 시각 계산
            isTracking = true; // 스토커 추적 상태 활성화
            hasAttacked = false; // 즉사 공격 상태 초기화

            if (showDebug) // 디버그 로그 출력 여부 확인
            {
                Debug.Log($"[Stalker] 목표 지정 — {activationDelay:F1}초 후 추적 시작"); // 목표 지정과 유예시간 출력
            }
        }

        bool CanTrackTarget() // 현재 플레이어를 추적할 수 있는 상태인지 확인
        {
            if (!isTracking || hasAttacked) { return false; } // 추적이 비활성화되었거나 이미 공격했는지 확인 ->추적 불가능 반환
            if (targetPlayer == null || targetPlayer.IsDead) { return false; } // 목표가 없거나 이미 사망했는지 확인 ->추적 불가능 반환
            return Time.time >= trackingStartTime; // 유예시간이 끝났는지 반환
        }

        void KillTarget() // 공격 범위에 들어온 목표 플레이어 즉사 처리
        {
            if (hasAttacked || targetPlayer == null || targetPlayer.IsDead) // 중복 공격 또는 유효하지 않은 목표인지 확인
            {
                return; // 즉사 공격 중단
            }

            hasAttacked = true; // 즉사 공격 완료 상태 저장
            isTracking = false; // 추가 추적 중단
            targetPlayer.TakeFatalDamage(); // 방패를 무시하고 부활석 확인을 포함한 즉사 피해 적용

            if (showDebug) // 디버그 로그 출력 여부 확인
            {
                Debug.Log("[Stalker] 목표 플레이어에게 즉사 공격을 적용했습니다."); // 즉사 공격 결과 출력
            }

            Destroy(gameObject, Mathf.Max(0f, disappearDelay)); // 설정된 시간 후 스토커 제거
        }
    }
}