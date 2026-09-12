using ProjectI.Interaction; // F 상호작용 인터페이스 참조
using ProjectI.Player; // 사다리 이동 처리 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    [RequireComponent(typeof(Collider))] // 시선 상호작용용 Collider
    public sealed class DungeonLadder : MonoBehaviour, IInteractable // 세로형 방의 사다리 (F로 타고 W/S로 오르내림)
    {
        [SerializeField] private Transform bottomPoint; // 사다리 아래 끝 (발 높이)
        [SerializeField] private Transform topPoint; // 사다리 위 끝 (발 높이)
        [SerializeField] private Transform exitTopPoint; // 위에서 내려서는 위치
        [SerializeField] private Transform exitBottomPoint; // 아래에서 내려서는 위치

        public Vector3 BottomPosition => bottomPoint != null ? bottomPoint.position : transform.position; // 아래 끝 공개
        public Vector3 TopPosition => topPoint != null ? topPoint.position : transform.position + Vector3.up * 3f; // 위 끝 공개
        public Vector3 ExitTopPosition => exitTopPoint != null ? exitTopPoint.position : TopPosition; // 위 내림 위치 공개
        public Vector3 ExitBottomPosition => exitBottomPoint != null ? exitBottomPoint.position : BottomPosition; // 아래 내림 위치 공개
        public string Prompt => "사다리 타기 — F / 오르내리기 W·S"; // 안내 문구
        public InteractionType InteractionType => InteractionType.Press; // F 한 번
        public float HoldDuration => 0f; // 길게 누르기 없음

        public void Configure(Transform bottom, Transform top, Transform exitTop, Transform exitBottom) // 생성기 구성
        {
            bottomPoint = bottom; // 아래 끝
            topPoint = top; // 위 끝
            exitTopPoint = exitTop; // 위 내림 위치
            exitBottomPoint = exitBottom; // 아래 내림 위치
        }

        public bool CanInteract(PlayerInteractor interactor) // 사다리를 타지 않은 플레이어만
        {
            if (interactor == null) // 플레이어 확인
            {
                return false; // 불가
            }

            PlayerMovement movement = interactor.GetComponent<PlayerMovement>(); // 이동 컴포넌트
            return movement != null && !movement.IsClimbing; // 이미 타고 있으면 제외
        }

        public void Interact(PlayerInteractor interactor) // 사다리 타기 시작
        {
            PlayerMovement movement = interactor == null ? null : interactor.GetComponent<PlayerMovement>(); // 이동 컴포넌트

            if (movement == null) // 확인
            {
                return; // 종료
            }

            Vector3 bottom = BottomPosition; // 아래 끝
            Vector3 top = TopPosition; // 위 끝
            Vector3 anchor = new Vector3(bottom.x, 0f, bottom.z); // 중심선 (XZ)
            Vector3 face = transform.forward; // 사다리가 바라보는 방향 = 방 안쪽

            if (!movement.BeginClimb(anchor, bottom.y, top.y, -face, ExitTopPosition, ExitBottomPosition)) // 사다리 이동 시작 (플레이어는 사다리를 바라봄)
            {
                Debug.Log("[Project I] 사다리를 탈 수 없습니다.", this); // 안내
            }
        }
    }
}
