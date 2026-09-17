using ProjectI.Interaction; // 상호작용 규약 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    public sealed class DungeonElevatorButton : MonoBehaviour, IInteractable // 승강기 안 버튼 — 누르면 그 층으로 간다
    {
        [SerializeField] private DungeonElevator elevator; // 승강기
        [SerializeField] private int stopIndex; // 목표 층

        public int StopIndex => stopIndex; // 층 공개
        public DungeonElevator Elevator => elevator; // 승강기 공개

        public string Prompt // 안내 문구
        {
            get
            {
                if (elevator == null)
                {
                    return "승강기 버튼 — 연결되지 않음"; // 구성 누락
                }

                if (!elevator.HasPower)
                {
                    return "승강기 버튼 — 전력 없음"; // 정전
                }

                return elevator.CurrentStop == stopIndex ? $"{elevator.FloorLabel(stopIndex)}층 (현재 위치)" : $"{elevator.FloorLabel(stopIndex)}층으로 이동"; // 문구
            }
        }

        public InteractionType InteractionType => InteractionType.Press; // 누르기
        public float HoldDuration => 0f; // 길게 누르기 없음

        public void Configure(DungeonElevator target, int index) // 구성 (프리팹 제작기에서 호출)
        {
            elevator = target; // 승강기
            stopIndex = index; // 층
        }

        public bool CanInteract(PlayerInteractor interactor) // 조작 가능 여부 (전력이 없어도 안내는 보이게)
        {
            return elevator != null; // 승강기가 연결되어 있으면 조사 가능
        }

        public void Interact(PlayerInteractor interactor) // 이동 요청
        {
            if (elevator == null) // 연결 없음
            {
                return; // 종료
            }

            if (!elevator.HasPower) // 정전
            {
                Debug.Log("[Project I] 승강기 — 전력이 들어오지 않습니다.", this); // 안내
                return; // 종료
            }

            elevator.GoToStop(stopIndex); // 이동
        }
    }
}
