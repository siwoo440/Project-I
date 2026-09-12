using System.Collections.Generic; // 목록 사용
using ProjectI.Generation; // 배치 규칙 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    [DisallowMultipleComponent] // 한 오브젝트에 하나
    public sealed class DungeonModule : MonoBehaviour // 방·복도 프리팹 하나 (모듈). 출입구끼리 맞춰 서로 붙습니다
    {
        [SerializeField] private string moduleId = string.Empty; // 모듈 이름
        [SerializeField] private ModuleRole role = ModuleRole.Room; // 역할
        [SerializeField] private int weight = 10; // 뽑기 가중치
        [SerializeField] private Vector2Int[] cells = System.Array.Empty<Vector2Int>(); // 차지하는 칸 (모듈 로컬 격자)
        [SerializeField] private DungeonSocket[] sockets = System.Array.Empty<DungeonSocket>(); // 출입구
        [SerializeField] private int floorSpan = 1; // 차지하는 층 수 (세로형 방은 2)
        [SerializeField] private Transform contentRoot; // 회수품·소품을 놓을 기준

        public string ModuleId => string.IsNullOrEmpty(moduleId) ? name : moduleId; // 이름 공개
        public ModuleRole Role => role; // 역할 공개
        public int Weight => weight; // 가중치 공개
        public IReadOnlyList<Vector2Int> Cells => cells; // 칸 공개
        public IReadOnlyList<DungeonSocket> Sockets => sockets; // 출입구 공개
        public Transform ContentRoot => contentRoot != null ? contentRoot : transform; // 소품 기준 공개
        public int FloorSpan => floorSpan < 1 ? 1 : floorSpan; // 층 수 공개

        public void Configure(string id, ModuleRole moduleRole, int pickWeight, Vector2Int[] moduleCells, DungeonSocket[] moduleSockets, Transform content, int moduleFloorSpan = 1) // 구성 (프리팹 제작기에서 호출)
        {
            moduleId = id; // 이름
            role = moduleRole; // 역할
            weight = pickWeight; // 가중치
            cells = moduleCells; // 칸
            sockets = moduleSockets; // 출입구
            contentRoot = content; // 소품 기준
            floorSpan = moduleFloorSpan; // 층 수
        }

        public ModuleDefinition ToDefinition() // 배치 규칙용 정의로 변환
        {
            List<CellPoint> cellPoints = new List<CellPoint>(cells.Length); // 칸

            foreach (Vector2Int cell in cells) // 칸 순회
            {
                cellPoints.Add(new CellPoint(cell.x, cell.y)); // 변환
            }

            List<SocketDefinition> socketDefinitions = new List<SocketDefinition>(sockets.Length); // 출입구

            foreach (DungeonSocket socket in sockets) // 출입구 순회
            {
                if (socket != null) // 확인
                {
                    socketDefinitions.Add(socket.ToDefinition()); // 변환
                }
            }

            return new ModuleDefinition(ModuleId, role, cellPoints, socketDefinitions, weight, FloorSpan); // 정의
        }

        public DungeonSocket SocketAt(int index) // 번호로 출입구 조회
        {
            return index >= 0 && index < sockets.Length ? sockets[index] : null; // 반환
        }

        public Bounds LocalBounds() // 모듈이 차지하는 로컬 부피 (칸 기준)
        {
            if (cells.Length == 0) // 칸 없음
            {
                return new Bounds(Vector3.zero, Vector3.zero); // 빈 범위
            }

            float size = ModuleDoorway.CellSize; // 칸 크기
            Vector3 min = new Vector3(cells[0].x * size, 0f, cells[0].y * size); // 최소
            Vector3 max = min; // 최대

            foreach (Vector2Int cell in cells) // 칸 순회
            {
                Vector3 corner = new Vector3(cell.x * size, 0f, cell.y * size); // 칸 원점
                min = Vector3.Min(min, corner); // 최소 갱신
                max = Vector3.Max(max, corner + new Vector3(size, 0f, size)); // 최대 갱신
            }

            Bounds bounds = new Bounds((min + max) * 0.5f, max - min); // 범위
            bounds.Encapsulate(new Vector3(bounds.center.x, ModuleDoorway.Height + 1.2f, bounds.center.z)); // 높이 포함
            return bounds; // 반환
        }
    }
}
