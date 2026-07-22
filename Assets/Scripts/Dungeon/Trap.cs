using System.Collections; // 함정 발동 지연과 재사용 대기시간 처리
using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public enum TrapType // 함정 종류 구분
    {
        Spike, // 20 피해를 주는 가시 함정
        Axe, // 60 피해를 주는 도끼 함정
        InstantDeath // 플레이어를 즉사시키는 함정
    }

    [RequireComponent(typeof(BoxCollider))] // 발동 영역으로 사용할 BoxCollider 필수 지정
    [RequireComponent(typeof(Rigidbody))] // Trigger 이벤트를 안정적으로 받기 위한 Rigidbody 필수 지정
    public class Trap : MonoBehaviour // 플레이어 진입을 감지해 피해를 주는 함정
    {
        [Header("함정 설정")] // Inspector 함정 설정 구분
        [Tooltip("현재 프리팹의 함정 종류")] [SerializeField] TrapType trapType = TrapType.Spike; // 현재 프리팹의 함정 종류
        [Tooltip("일반 함정이 플레이어에게 줄 피해")] [SerializeField] float damage = 20f; // 일반 함정이 플레이어에게 줄 피해
        [Tooltip("플레이어 감지 후 실제 발동까지의 지연시간")] [SerializeField] float activationDelay = 0f; // 플레이어 감지 후 실제 발동까지의 지연시간
        [Tooltip("재발동이 가능해질 때까지의 대기시간")] [SerializeField] float cooldown = 1.5f; // 재발동이 가능해질 때까지의 대기시간
        [Tooltip("한 번 발동한 뒤 영구 비활성화할지 결정")] [SerializeField] bool oneShot = false; // 한 번 발동한 뒤 영구 비활성화할지 결정

        [Header("상태 이상")] // Inspector 함정 상태 이상 설정 구분
        [Tooltip("발동 시 출혈 적용 여부")] [SerializeField] bool applyBleeding; // 발동 시 출혈 적용 여부
        [Tooltip("출혈 지속시간")] [SerializeField] float bleedingDuration = 8f; // 출혈 지속시간
        [Tooltip("출혈 1회 피해량")] [SerializeField] float bleedingDamagePerTick = 4f; // 출혈 1회 피해량
        [Tooltip("출혈 피해 간격")] [SerializeField] float bleedingTickInterval = 2f; // 출혈 피해 간격
        [Tooltip("발동 시 둔화 적용 여부")] [SerializeField] bool applySlow; // 발동 시 둔화 적용 여부
        [Tooltip("둔화 지속시간")] [SerializeField] float slowDuration = 5f; // 둔화 지속시간
        [Tooltip("둔화 이동속도 배율")] [SerializeField][Range(0.1f, 1f)] float slowMultiplier = 0.6f; // 둔화 이동속도 배율

        [Header("피드백")] // Inspector 함정 피드백 설정 구분
        [Tooltip("함정 루트 기준 피드백 발생 위치")] [SerializeField] Vector3 feedbackOffset = new Vector3(0f, 0.2f, 0f); // 함정 루트 기준 피드백 발생 위치

        [Header("디버그")] // 디버그 설정 구분
        [Tooltip("함정 발동 로그를 출력할지 결정")] [SerializeField] bool showDebug = true; // 함정 발동 로그를 출력할지 결정

        BoxCollider triggerCollider; // 플레이어를 감지할 Trigger Collider
        Rigidbody trapRigidbody; // Trigger 이벤트를 안정적으로 발생시킬 Rigidbody
        ThreatFeedback threatFeedback; // 함정 경고와 발동 피드백
        bool canActivate = true; // 현재 함정이 발동 가능한 상태인지 저장

        public TrapType Type => trapType; // 외부에서 현재 함정 종류 확인

        void Awake() // 함정의 Collider와 Rigidbody 초기화
        {
            triggerCollider = GetComponent<BoxCollider>(); // 현재 오브젝트의 BoxCollider 가져오기
            trapRigidbody = GetComponent<Rigidbody>(); // 현재 오브젝트의 Rigidbody 가져오기
            threatFeedback = GetComponent<ThreatFeedback>(); // 같은 오브젝트의 공통 피드백 검색
            triggerCollider.isTrigger = true; // Collider를 물리 장애물이 아닌 감지 영역으로 설정
            trapRigidbody.useGravity = false; // 함정에 중력이 적용되지 않도록 설정
            trapRigidbody.isKinematic = true; // 물리 힘에 의해 함정이 움직이지 않도록 설정
        }

        void OnTriggerEnter(Collider other) // 다른 Collider가 함정 영역에 진입했을 때 호출
        {
            if (!canActivate) { return; }// 함정이 재사용 대기 중인지 확인 // 중복 발동을 방지

            PlayerController player = other.GetComponentInParent<PlayerController>(); // 진입한 Collider에서 플레이어 검색

            if (player == null) // 진입한 대상이 플레이어가 아닌지 확인
            {
                return; // 몬스터와 아이템에는 함정을 발동하지 않음
            }

            canActivate = false; // 피해 처리 전에 함정을 재사용 대기 상태로 변경
            if (threatFeedback != null) // 함정 피드백 존재 여부 확인
            {
                Vector3 feedbackPosition = transform.position + feedbackOffset; // 함정 경고 발생 위치 계산
                threatFeedback.PlayWarningAt(feedbackPosition); // 함정 발동 전 경고 피드백 재생
            }
            StartCoroutine(ActivateTrap(player)); // 발동 지연과 피해 처리 시작
        }

        IEnumerator ActivateTrap(PlayerController player) // 설정된 지연 후 플레이어에게 함정 효과 적용
        {
            float safeActivationDelay = Mathf.Max(0f, activationDelay); // 발동 지연시간이 음수가 되지 않도록 제한

            if (safeActivationDelay > 0f) // 실제 발동 지연시간이 있는지 확인
            {
                yield return new WaitForSeconds(safeActivationDelay); // 설정된 시간만큼 발동 대기
            }

            if (player != null && !player.IsDead) // 플레이어가 존재하고 살아 있는지 확인
            {
                if (trapType == TrapType.InstantDeath) // 즉사 함정인지 확인
                {
                    player.TakeFatalDamage(); // 방패를 무시하고 즉사 피해 적용
                }
                else // 가시 또는 도끼 함정 처리
                {
                    float safeDamage = Mathf.Max(0f, damage); // 일반 피해가 음수가 되지 않도록 제한
                    player.TakeDamage(safeDamage); // 기존 플레이어 피해 처리로 일반 피해 적용
                }

                if (trapType != TrapType.InstantDeath && !player.IsDead) // 일반 함정과 생존 상태 확인
                {
                    PlayerStatusEffectSystem statusEffectSystem = player.GetComponent<PlayerStatusEffectSystem>(); // 플레이어 상태 이상 시스템 검색

                    if (statusEffectSystem != null) // 상태 이상 시스템 존재 여부 확인
                    {
                        if (applyBleeding) // 출혈 적용 설정 확인
                        {
                            statusEffectSystem.ApplyBleeding( // 플레이어에게 출혈 적용
                                bleedingDuration, // 출혈 지속시간 전달
                                bleedingDamagePerTick, // 출혈 1회 피해량 전달
                                bleedingTickInterval); // 출혈 피해 간격 전달
                        }

                        if (applySlow) // 둔화 적용 설정 확인
                        {
                            statusEffectSystem.ApplySlow(slowDuration, slowMultiplier); // 플레이어에게 둔화 적용
                        }
                    }
                }

                if (threatFeedback != null) // 함정 피드백 존재 여부 확인
                {
                    Vector3 feedbackPosition = transform.position + feedbackOffset; // 함정 발동 피드백 위치 계산
                    threatFeedback.PlayAttackAt(feedbackPosition); // 함정 발동 소리와 파티클 재생
                }

                if (showDebug) // 디버그 로그 출력 여부 확인
                {
                    Debug.Log($"[Trap] {GetTrapName()} 발동"); // 발동한 함정 이름 출력
                }
            }

            if (oneShot) // 일회용 함정인지 확인
            {
                triggerCollider.enabled = false; // 발동 영역을 비활성화해 재발동 방지
                yield break; // 함정 처리를 종료
            }

            float safeCooldown = Mathf.Max(0f, cooldown); // 재사용 대기시간이 음수가 되지 않도록 제한

            if (safeCooldown > 0f) // 실제 재사용 대기시간이 있는지 확인
            {
                yield return new WaitForSeconds(safeCooldown); // 설정된 재사용 대기시간만큼 대기
            }

            canActivate = true; // 함정을 다시 발동 가능한 상태로 변경
        }

        string GetTrapName() // 함정 종류에 해당하는 한글 이름 반환
        {
            switch (trapType) // 현재 함정 종류 확인
            {
                case TrapType.Spike:    return "가시 함정"; // 가시 함정 처리// 가시 함정 이름 반환
                case TrapType.Axe:      return "도끼 함정"; // 도끼 함정 처리// 도끼 함정 이름 반환
                default:                return "즉사 함정"; // 즉사 함정 처리// 즉사 함정 이름 반환
            }
        }
    }
}