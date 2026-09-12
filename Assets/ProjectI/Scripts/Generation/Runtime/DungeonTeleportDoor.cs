using ProjectI.Interaction; // F 상호작용 인터페이스 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    public enum DungeonDoorSide // 문 위치
    {
        Exterior, // 지상 외부 씬에 고정 배치된 문
        Interior // 지하 실내에 생성된 문
    }

    public enum DungeonDoorKind // 문 종류
    {
        Main, // 정문 (외부 1개 ↔ 시작 방)
        Sub // 서브문 (외부 N개 ↔ 실내 N개, 번호로 1:1 연결)
    }

    [RequireComponent(typeof(Collider))] // 시선 상호작용용 Collider
    public sealed class DungeonTeleportDoor : MonoBehaviour, IInteractable // 외부 ↔ 지하 실내를 순간이동으로 잇는 출입문
    {
        [SerializeField] private DungeonDoorSide side = DungeonDoorSide.Exterior; // 외부·실내 구분
        [SerializeField] private DungeonDoorKind kind = DungeonDoorKind.Main; // 정문·서브문 구분
        [SerializeField] private int subIndex; // 서브문 번호 (0부터, 외부·실내 같은 번호끼리 연결)
        [SerializeField] private Transform arrivalPoint; // 이 문으로 나올 때 서는 위치

        public DungeonDoorSide Side => side; // 위치 공개
        public DungeonDoorKind Kind => kind; // 종류 공개
        public int SubIndex => kind == DungeonDoorKind.Sub ? subIndex : -1; // 서브문 번호 공개
        public Vector3 ArrivalPosition => arrivalPoint != null ? arrivalPoint.position : transform.position - transform.forward * 1.5f; // 도착 위치
        public Quaternion ArrivalRotation => arrivalPoint != null ? arrivalPoint.rotation : Quaternion.LookRotation(-transform.forward); // 도착 방향
        public string Prompt => BuildPrompt(); // 안내 문구
        public InteractionType InteractionType => InteractionType.Press; // F 한 번
        public float HoldDuration => 0f; // 길게 누르기 없음

        public void Configure(DungeonDoorSide targetSide, DungeonDoorKind targetKind, int targetSubIndex, Transform targetArrivalPoint) // 생성·에디터 구성
        {
            side = targetSide; // 위치
            kind = targetKind; // 종류
            subIndex = targetSubIndex; // 번호
            arrivalPoint = targetArrivalPoint; // 도착 지점
        }

        public bool CanInteract(PlayerInteractor interactor) // 안내 문구를 항상 보여주기 위해 플레이어면 허용
        {
            return interactor != null; // 플레이어 확인
        }

        public void Interact(PlayerInteractor interactor) // 짝이 되는 문으로 순간이동
        {
            ModuleDungeonGenerator moduleGenerator = ModuleDungeonGenerator.FindInScene(gameObject.scene); // 모듈 생성기 (30일차 소켓 방식)

            if (moduleGenerator != null) // 모듈 방식 우선
            {
                if (!moduleGenerator.UseDoor(this)) // 이동 실패 확인
                {
                    Debug.Log($"[Project I] 출입문 사용 불가 / {BuildPrompt()}", this); // 안내
                }

                return; // 종료
            }

            ProceduralInteriorGenerator generator = ProceduralInteriorGenerator.FindInScene(gameObject.scene); // 같은 씬 생성기 (이전 격자 방식)

            if (generator == null || !generator.UseDoor(this)) // 이동 실패 확인
            {
                Debug.Log($"[Project I] 출입문 사용 불가 / {BuildPrompt()}", this); // 안내
            }
        }

        private string BuildPrompt() // 문 종류별 안내
        {
            ModuleDungeonGenerator moduleGenerator = ModuleDungeonGenerator.FindInScene(gameObject.scene); // 모듈 생성기

            if (moduleGenerator != null) // 모듈 방식 우선
            {
                if (!moduleGenerator.IsGenerated) // 실내 생성 여부
                {
                    return "입구가 무너져 막혀 있다"; // 생성 실패 문구
                }

                string moduleName = kind == DungeonDoorKind.Main ? "정문" : $"보조 출입구 {subIndex + 1}"; // 문 이름
                return side == DungeonDoorSide.Exterior ? $"{moduleName} — 지하로 들어가기" : $"{moduleName} — 밖으로 나가기"; // 방향별 문구
            }

            ProceduralInteriorGenerator generator = ProceduralInteriorGenerator.FindInScene(gameObject.scene); // 같은 씬 생성기

            if (generator == null || !generator.IsGenerated) // 실내 생성 여부
            {
                return "입구가 무너져 막혀 있다"; // 생성 실패 문구
            }

            string name = kind == DungeonDoorKind.Main ? "정문" : $"보조 출입구 {subIndex + 1}"; // 문 이름
            return side == DungeonDoorSide.Exterior ? $"{name} — 지하로 들어가기" : $"{name} — 밖으로 나가기"; // 방향별 문구
        }
    }
}
