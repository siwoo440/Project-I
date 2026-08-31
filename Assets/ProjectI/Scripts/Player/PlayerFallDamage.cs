using ProjectI.Combat; // 공통 전투 Damage Pipeline 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Player // 플레이어 기능 네임스페이스
{
    [RequireComponent(typeof(PlayerMovement))] // 이동 컴포넌트 필수 지정
    [RequireComponent(typeof(PlayerHealth))] // 체력 컴포넌트 필수 지정
    public sealed class PlayerFallDamage : MonoBehaviour // 플레이어 추락 피해 컴포넌트
    {
        [SerializeField] private float safeFallDistance = 3f; // 피해 없는 최대 추락 거리
        [SerializeField] private float damagePerMeter = 20f; // 안전 거리 초과 미터당 피해
        [SerializeField] private float maximumDamage = 100f; // 한 번의 추락 최대 피해
        private PlayerMovement movement; // 플레이어 이동 참조
        private PlayerHealth health; // 플레이어 체력 참조
        private PlayerDamageReceiver damageReceiver; // 공통 Damage Pipeline용 플레이어 피해 수신기

        public float LastFallDistance { get; private set; } // 마지막 착지 추락 거리 공개
        public float LastAppliedDamage { get; private set; } // 마지막 추락 피해 공개

        private void Awake() // 추락 피해 컴포넌트 초기화
        {
            movement = GetComponent<PlayerMovement>(); // 이동 컴포넌트 참조 획득
            health = GetComponent<PlayerHealth>(); // 체력 컴포넌트 참조 획득
            damageReceiver = GetComponent<PlayerDamageReceiver>(); // 공통 피해 수신기 참조 획득
        }

        private void OnEnable() // 컴포넌트 활성화 처리
        {
            if (movement == null) // 이동 참조 누락 확인
            {
                movement = GetComponent<PlayerMovement>(); // 이동 컴포넌트 참조 재획득
            }

            damageReceiver = GetComponent<PlayerDamageReceiver>(); // Day14 Setup 이후 공통 피해 수신기 재확인
            movement.Landed += HandleLanded; // 착지 이벤트 구독
        }

        private void OnDisable() // 컴포넌트 비활성화 처리
        {
            if (movement != null) // 이동 참조 존재 확인
            {
                movement.Landed -= HandleLanded; // 착지 이벤트 구독 해제
            }
        }

        private void HandleLanded(float fallDistance) // 착지 거리 처리
        {
            LastFallDistance = fallDistance; // 마지막 추락 거리 저장
            float calculatedDamage = FallDamageCalculator.Calculate(fallDistance, safeFallDistance, damagePerMeter, maximumDamage); // 추락 피해 계산

            if (calculatedDamage <= 0f) // 피해 없는 착지 확인
            {
                LastAppliedDamage = 0f; // 마지막 피해량 0으로 기록
                return; // 피해 처리 종료
            }

            if (damageReceiver != null) // 공통 피해 수신기 존재 여부 확인
            {
                DamageInfo damageInfo = new DamageInfo(gameObject, null, CombatFaction.Environment, CombatDamageType.Fall, calculatedDamage, transform.position, Vector3.up, Vector3.zero, 0); // 환경 추락 피해 정보 생성
                DamagePipeline.TryApply(damageInfo, damageReceiver, out CombatHitResult hitResult); // 공통 Damage Pipeline으로 추락 피해 적용
                LastAppliedDamage = hitResult.AppliedDamage; // 실제 적용된 추락 피해 기록
                return; // 공통 피해 처리 완료
            }

            LastAppliedDamage = health.TakeDamage(calculatedDamage); // Day14 구성 누락 시 기존 체력 직접 피해 방식으로 안전 폴백
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            safeFallDistance = Mathf.Max(0f, safeFallDistance); // 안전 추락 거리 음수 방지
            damagePerMeter = Mathf.Max(0f, damagePerMeter); // 미터당 피해 음수 방지
            maximumDamage = Mathf.Max(0f, maximumDamage); // 최대 피해 음수 방지
        }
    }
}
