using ProjectI.Generation; // 배치 규칙 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    [DisallowMultipleComponent] // 한 오브젝트에 하나
    public sealed class DungeonSocket : MonoBehaviour // 모듈의 출입구 표시 (이 지점끼리 맞춰 모듈을 붙입니다)
    {
        [SerializeField] private SocketKind kind = SocketKind.Door; // 출입구 종류
        [SerializeField] private Vector2Int cell; // 출입구 안쪽 칸 (모듈 로컬 격자)
        [SerializeField] private GridDirection facing = GridDirection.North; // 모듈 바깥 방향
        [SerializeField] private Transform doorAnchor; // 문짝·금 간 벽을 놓을 기준점 (출입구 한가운데 바닥)
        [SerializeField] private int floorOffset; // 모듈 바닥층에서 몇 층 위인지 (세로형 방의 위층 출구는 1)
        [SerializeField] private Transform frameRoot; // 문틀 (벽으로 막을 때는 숨김)

        public SocketKind Kind => kind; // 종류 공개
        public Vector2Int Cell => cell; // 칸 공개
        public GridDirection Facing => facing; // 방향 공개
        public Transform DoorAnchor => doorAnchor != null ? doorAnchor : transform; // 기준점 공개
        public Transform FrameRoot => frameRoot; // 문틀 공개
        public int FloorOffset => floorOffset; // 층 차이 공개
        public CellPoint CellPoint => new CellPoint(cell.x, cell.y); // 배치 규칙용 좌표

        public void Configure(SocketKind socketKind, Vector2Int localCell, GridDirection outwardFacing, Transform anchor, Transform frame, int socketFloorOffset = 0) // 구성 (프리팹 제작기에서 호출)
        {
            kind = socketKind; // 종류
            cell = localCell; // 칸
            facing = outwardFacing; // 방향
            doorAnchor = anchor; // 기준점
            frameRoot = frame; // 문틀
            floorOffset = socketFloorOffset; // 층 차이
        }

        public void SetFrameVisible(bool visible) // 문틀 보이기·숨기기 (벽으로 막을 때는 숨겨 통짜 벽처럼 보이게 함)
        {
            if (frameRoot != null) // 확인
            {
                frameRoot.gameObject.SetActive(visible); // 적용
            }
        }

        public SocketDefinition ToDefinition() // 배치 규칙용 정의로 변환
        {
            return new SocketDefinition(CellPoint, facing, kind, floorOffset); // 변환
        }

        private void OnDrawGizmos() // 에디터에서 출입구 위치·방향 표시
        {
            Gizmos.color = kind == SocketKind.Exterior ? Color.green : kind == SocketKind.Breakable ? Color.magenta : kind == SocketKind.VerticalUp ? Color.cyan : Color.yellow; // 종류별 색
            Matrix4x4 previous = Gizmos.matrix; // 기존 기준
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one); // 소켓 방향 기준으로 그림 (월드 축으로 그리면 동·서 출입구에서 90도 틀어져 보임)
            Vector3 origin = new Vector3(0f, ModuleDoorway.Height * 0.5f, 0f); // 출입구 중심 (소켓 로컬)
            Gizmos.DrawWireCube(origin, new Vector3(ModuleDoorway.Width, ModuleDoorway.Height, 0.1f)); // 규격 표시
            Gizmos.DrawLine(origin, origin + (Vector3.forward * 1.2f)); // 바깥 방향
            Gizmos.matrix = previous; // 기준 복구
        }
    }
}
