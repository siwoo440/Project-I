using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    [RequireComponent(typeof(PickupItem))] // 인벤토리 저장용 PickupItem 지정
    public class RecoveryItem : MonoBehaviour // 체력과 상태 이상을 회복하는 소모품
    {
        [Header("회복 설정")] // Inspector 회복 설정 구분
        [Tooltip("사용할 때 회복할 체력")] [SerializeField] float healAmount = 15f; // 사용할 때 회복할 체력
        [Tooltip("사용할 때 출혈 제거 여부")] [SerializeField] bool removeBleeding = true; // 사용할 때 출혈 제거 여부
        [Tooltip("사용할 때 둔화 제거 여부")] [SerializeField] bool removeSlow; // 사용할 때 둔화 제거 여부

        public bool TryUse(PlayerController player) // 플레이어에게 회복 효과 적용 시도
        {
            if (player == null || player.IsDead) // 플레이어 유효성과 사망 상태 확인
            {
                return false; // 사용 실패 반환
            }

            bool changed = false; // 실제 효과 적용 여부 초기화

            if (healAmount > 0f) // 체력 회복량 존재 여부 확인
            {
                changed |= player.Heal(healAmount); // 체력 회복 결과 반영
            }

            PlayerStatusEffectSystem statusEffectSystem = player.GetComponent<PlayerStatusEffectSystem>(); // 플레이어 상태 이상 시스템 검색

            if (statusEffectSystem != null) // 상태 이상 시스템 존재 여부 확인
            {
                if (removeBleeding) // 출혈 제거 설정 확인
                {
                    changed |= statusEffectSystem.ClearBleeding(); // 출혈 제거 결과 반영
                }

                if (removeSlow) // 둔화 제거 설정 확인
                {
                    changed |= statusEffectSystem.ClearSlow(); // 둔화 제거 결과 반영
                }
            }

            if (changed) // 하나 이상의 회복 효과 적용 여부 확인
            {
                PickupItem pickupItem = GetComponent<PickupItem>(); // 같은 오브젝트의 PickupItem 가져오기
                string itemName = pickupItem != null ? pickupItem.DisplayName : gameObject.name; // 사용한 아이템 이름 결정

                Debug.Log($"[RecoveryItem] {itemName} 사용"); // 회복 아이템 사용 결과 출력
                return true; // 사용 성공 반환
            }

            Debug.Log("[RecoveryItem] 회복할 체력이나 상태 이상이 없습니다."); // 사용 불가 이유 출력
            return false; // 사용 실패 반환
        }
    }
}