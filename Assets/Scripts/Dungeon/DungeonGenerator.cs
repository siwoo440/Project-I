using System.Collections.Generic;
using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 절차적 던전 생성 (그리드 랜덤 워크). (기획서 PART 6.1)
    /// 방 프리팹을 격자에 이어붙이고, 이동하는 쪽 벽을 열어 통로를 만든다. 매 플레이마다 다른 배치.
    /// </summary>
    public class DungeonGenerator : MonoBehaviour
    {
        [Header("생성 설정")]
        [SerializeField] Room[] roomPrefabs;   // 방 프리팹(들)
        [SerializeField] int roomCount = 8;
        [SerializeField] float cellSize = 12f; // 방 한 칸 크기(프리팹 크기와 일치)
        [SerializeField] Transform player;     // 시작 방으로 이동시킬 플레이어

        [Header("시드")]
        [SerializeField] bool randomSeed = true;
        [SerializeField] int seed = 0;

        readonly Dictionary<Vector2Int, Room> placed = new Dictionary<Vector2Int, Room>();

        // 인덱스 = Room.Dir (0=N +z, 1=E +x, 2=S -z, 3=W -x)
        static readonly Vector2Int[] DirVec =
        {
            new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0)
        };

        void Start() => Generate();

        public void Generate()
        {
            if (roomPrefabs == null || roomPrefabs.Length == 0)
            {
                Debug.LogError("[Dungeon] roomPrefabs가 비어 있습니다.");
                return;
            }

            foreach (var r in placed.Values) if (r) Destroy(r.gameObject);
            placed.Clear();

            if (randomSeed) seed = Random.Range(int.MinValue, int.MaxValue);
            Random.InitState(seed);

            Vector2Int cur = Vector2Int.zero;
            PlaceRoom(cur);

            int guard = 0;
            while (placed.Count < roomCount && guard++ < roomCount * 20)
            {
                int di = Random.Range(0, 4);
                Vector2Int next = cur + DirVec[di];
                if (!placed.ContainsKey(next)) PlaceRoom(next);
                Connect(cur, (Room.Dir)di, next);  // 이동 경로의 두 방 사이 벽 열기
                cur = next;
            }

            // 플레이어를 시작 방(원점)으로
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.position = new Vector3(0f, 1.1f, 0f);
                if (cc != null) cc.enabled = true;
            }

            Debug.Log($"[Dungeon] 생성 완료 — 방 {placed.Count}개 (seed {seed})");
        }

        void PlaceRoom(Vector2Int cell)
        {
            if (placed.ContainsKey(cell)) return;
            var prefab = roomPrefabs[Random.Range(0, roomPrefabs.Length)];
            var pos = new Vector3(cell.x * cellSize, 0f, cell.y * cellSize);
            placed[cell] = Instantiate(prefab, pos, Quaternion.identity, transform);
        }

        void Connect(Vector2Int a, Room.Dir d, Vector2Int b)
        {
            if (placed.TryGetValue(a, out var ra)) ra.OpenSide(d);
            if (placed.TryGetValue(b, out var rb)) rb.OpenSide(Room.Opposite(d));
        }
    }
}
