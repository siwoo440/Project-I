using ProjectI.Player; // 기존 플레이어 체력 시스템 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    [RequireComponent(typeof(PlayerHealth))] // 기존 플레이어 체력 컴포넌트 필수 지정
    public sealed class PlayerDamageReceiver : MonoBehaviour, IDamageable // 기존 PlayerHealth를 Damage Pipeline에 연결하는 어댑터
    {
        private PlayerHealth health; // 기존 플레이어 체력 참조

        public CombatFaction Faction => CombatFaction.Player; // 플레이어 진영 반환
        public bool IsAlive => health != null && !health.IsDead; // 플레이어 생존 상태 반환
        public Transform DamageTransform => transform; // 플레이어 대표 피격 Transform 반환
        public PlayerHealth Health // Validator와 진단용 체력 참조 공개
        {
            get
            {
                ResolveHealth(); // Edit Mode Validator에서도 기존 PlayerHealth 참조 확보
                return health; // 확보된 기존 플레이어 체력 참조 반환
            }
        }

        private void Awake() // 피해 수신기 초기화
        {
            ResolveHealth(); // 기존 PlayerHealth 참조 확보
        }

        private void OnEnable() // 피해 수신기 활성화 처리
        {
            ResolveHealth(); // 활성화 직후 PlayerHealth 참조 재확인
        }

        public float ApplyDamage(DamageInfo damageInfo) // Damage Pipeline 승인 피해를 기존 체력에 적용
        {
            ResolveHealth(); // 피해 적용 시점 체력 참조 확보
            return health == null ? 0f : health.TakeDamage(damageInfo.BaseDamage); // 기존 PlayerHealth를 통해 실제 피해량 반환
        }

        private void ResolveHealth() // 기존 플레이어 체력 참조 확보
        {
            if (health == null) // 체력 참조 누락 여부 확인
            {
                health = GetComponent<PlayerHealth>(); // 같은 오브젝트의 PlayerHealth 조회
            }
        }
    }
}
