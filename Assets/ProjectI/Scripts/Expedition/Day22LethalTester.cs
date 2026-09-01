using ProjectI.Interaction; // 기존 F 상호작용 공통 인터페이스 참조
using ProjectI.Player; // 기존 PlayerHealth 기능 참조
using UnityEngine; // 유니티 컴포넌트 기능 참조

namespace ProjectI.Expedition // 원정 테스트 기능 네임스페이스
{
    public sealed class Day22LethalTester : MonoBehaviour, IInteractable // Day22 래그돌 사망을 즉시 확인하는 테스트 오브젝트
    {
        public string Prompt => "Day22 사망 래그돌 테스트"; // 화면 상호작용 안내 문구
        public InteractionType InteractionType => InteractionType.Press; // F 한 번 누르기 방식 사용
        public float HoldDuration => 0f; // 길게 누르기 시간 불필요

        public bool CanInteract(PlayerInteractor interactor) // 현재 플레이어가 사망 테스트를 실행할 수 있는지 확인
        {
            PlayerHealth health = interactor == null ? null : interactor.GetComponent<PlayerHealth>(); // 상호작용 플레이어 기존 체력 조회
            return health != null && !health.IsDead; // 살아있는 플레이어에게만 테스트 허용
        }

        public void Interact(PlayerInteractor interactor) // F 입력으로 현재 플레이어를 즉시 사망 상태로 전환
        {
            PlayerHealth health = interactor == null ? null : interactor.GetComponent<PlayerHealth>(); // 기존 PlayerHealth 조회

            if (health == null || health.IsDead) // 체력 누락 또는 이미 사망 상태 확인
            {
                return; // 중복 사망 테스트 중단
            }

            health.TakeDamage(Mathf.Max(10000f, health.MaxHealth + 1f)); // 기존 PlayerHealth.Died 경로를 사용하도록 치명 피해 적용
        }
    }
}
