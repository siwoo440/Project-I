using System.Collections.Generic; // 목록 사용
using ProjectI.Dungeon; // 모듈 부품 참조
using ProjectI.Generation; // 출입구 규격 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public sealed class ModuleMaterials // 모듈 제작에 쓰는 재질 모음
    {
        public Material Floor; // 바닥
        public Material Wall; // 벽·천장
        public Material Trim; // 문틀
        public Material Accent; // 강조 (보스·비밀방)
    }

    public static class DungeonModuleBaker // 칸 형태 정의로부터 방 모듈 오브젝트를 만듭니다 (벽·바닥·천장·출입구 자동 생성)
    {
        public const float WallThickness = 0.25f; // 벽·바닥·천장 두께
        public const float RoomHeight = 4f; // 방 높이

        private readonly struct WallRun // 이어지는 바깥 벽 한 줄
        {
            public readonly GridDirection Facing; // 바깥 방향
            public readonly int Fixed; // 고정 좌표 (북·남은 세로 칸, 동·서는 가로 칸)
            public readonly int From; // 시작 칸
            public readonly int To; // 끝 칸 (포함)

            public WallRun(GridDirection facing, int fixedCoord, int from, int to) // 생성
            {
                Facing = facing; // 방향
                Fixed = fixedCoord; // 고정 좌표
                From = from; // 시작
                To = to; // 끝
            }
        }

        public static GameObject Build(DungeonModuleShape shape, ModuleMaterials materials) // 모듈 오브젝트 생성
        {
            HashSet<Vector2Int> cells = shape.BuildCells(); // 칸
            GameObject root = new GameObject(shape.Id); // 모듈 루트
            Transform structure = CreateChild(root.transform, "Structure"); // 구조물
            Transform socketRoot = CreateChild(root.transform, "Sockets"); // 출입구
            Transform contentRoot = CreateChild(root.transform, "Content"); // 소품·회수품 기준
            BuildFloorsAndCeilings(structure, cells, materials, RoomHeight); // 바닥·천장
            BuildWalls(structure, cells, shape, materials, RoomHeight); // 바깥 벽
            DungeonSocket[] sockets = BuildSockets(socketRoot, shape, materials); // 출입구
            BuildLights(root.transform, shape); // 조명
            Vector2Int[] cellArray = new Vector2Int[cells.Count]; // 칸 배열
            cells.CopyTo(cellArray); // 복사
            DungeonModule module = root.AddComponent<DungeonModule>(); // 모듈 부품
            module.Configure(shape.Id, shape.Role, shape.Weight, cellArray, sockets, contentRoot); // 구성
            return root; // 반환
        }

        public static void BuildFloorsAndCeilings(Transform parent, HashSet<Vector2Int> cells, ModuleMaterials materials, float ceilingHeight) // 줄 단위로 합친 바닥·천장
        {
            float size = ModuleDoorway.CellSize; // 칸 크기
            float t = WallThickness; // 두께
            List<int> rows = new List<int>(); // 줄 목록

            foreach (Vector2Int cell in cells) // 칸 순회
            {
                if (!rows.Contains(cell.y)) // 새 줄
                {
                    rows.Add(cell.y); // 등록
                }
            }

            rows.Sort(); // 정렬

            foreach (int row in rows) // 줄 순회
            {
                foreach (Vector2Int span in MergeRow(cells, row)) // 이어진 구간
                {
                    float length = (span.y - span.x + 1) * size; // 길이
                    float centerX = ((span.x + span.y + 1) * 0.5f) * size; // 중심
                    float centerZ = (row + 0.5f) * size; // 중심
                    CreateBox(parent, $"Floor_{row}_{span.x}", new Vector3(centerX, -t * 0.5f, centerZ), new Vector3(length, t, size), materials.Floor); // 바닥
                    CreateBox(parent, $"Ceiling_{row}_{span.x}", new Vector3(centerX, ceilingHeight + (t * 0.5f), centerZ), new Vector3(length, t, size), materials.Wall); // 천장
                }
            }
        }

        private static IEnumerable<Vector2Int> MergeRow(HashSet<Vector2Int> cells, int row) // 한 줄에서 이어진 가로 구간 (x시작, x끝)
        {
            List<int> columns = new List<int>(); // 가로 목록

            foreach (Vector2Int cell in cells) // 칸 순회
            {
                if (cell.y == row) // 같은 줄
                {
                    columns.Add(cell.x); // 등록
                }
            }

            columns.Sort(); // 정렬
            int start = 0; // 구간 시작

            for (int index = 0; index < columns.Count; index++) // 순회
            {
                bool last = index == columns.Count - 1; // 마지막

                if (last || columns[index + 1] != columns[index] + 1) // 구간 끝
                {
                    yield return new Vector2Int(columns[start], columns[index]); // 반환
                    start = index + 1; // 다음 구간
                }
            }
        }

        public static void BuildWalls(Transform parent, HashSet<Vector2Int> cells, DungeonModuleShape shape, ModuleMaterials materials, float wallHeight) // 바깥 경계 벽 (출입구 구멍 포함)
        {
            foreach (WallRun run in CollectRuns(cells)) // 벽 줄 순회
            {
                BuildWallRun(parent, run, shape, materials, wallHeight); // 한 줄 생성
            }
        }

        private static List<WallRun> CollectRuns(HashSet<Vector2Int> cells) // 바깥 면을 이어진 줄로 합침
        {
            List<WallRun> runs = new List<WallRun>(); // 결과

            foreach (GridDirection facing in GridDirections.All) // 네 방향
            {
                Vector2Int step = DungeonModuleShape.Step(facing); // 바깥 방향
                bool horizontal = facing == GridDirection.North || facing == GridDirection.South; // 가로 줄 여부
                Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>(); // 고정 좌표 → 변하는 좌표 목록

                foreach (Vector2Int cell in cells) // 칸 순회
                {
                    if (cells.Contains(cell + step)) // 바깥이 내부면 벽 없음
                    {
                        continue; // 다음
                    }

                    int fixedCoord = horizontal ? cell.y : cell.x; // 고정 좌표
                    int varying = horizontal ? cell.x : cell.y; // 변하는 좌표

                    if (!groups.TryGetValue(fixedCoord, out List<int> list)) // 새 그룹
                    {
                        list = new List<int>(); // 생성
                        groups[fixedCoord] = list; // 등록
                    }

                    list.Add(varying); // 등록
                }

                foreach (KeyValuePair<int, List<int>> group in groups) // 그룹 순회
                {
                    group.Value.Sort(); // 정렬
                    int start = 0; // 구간 시작

                    for (int index = 0; index < group.Value.Count; index++) // 순회
                    {
                        bool last = index == group.Value.Count - 1; // 마지막

                        if (last || group.Value[index + 1] != group.Value[index] + 1) // 구간 끝
                        {
                            runs.Add(new WallRun(facing, group.Key, group.Value[start], group.Value[index])); // 등록
                            start = index + 1; // 다음 구간
                        }
                    }
                }
            }

            return runs; // 반환
        }

        private static void BuildWallRun(Transform parent, WallRun run, DungeonModuleShape shape, ModuleMaterials materials, float wallHeight) // 벽 한 줄 (면 자체 좌표계에서만 계산, 출입구는 제 층 높이에 뚫림)
        {
            float size = ModuleDoorway.CellSize; // 칸 크기
            float t = WallThickness; // 두께
            float length = (run.To - run.From + 1) * size; // 줄 길이
            Vector3 outward = DungeonModuleShape.Outward(run.Facing); // 바깥 방향
            Vector3 center = RunCenter(run, size); // 줄 중심 (경계면 위, 바닥 높이)
            Transform face = CreateChild(parent, $"Wall_{run.Facing}_{run.Fixed}_{run.From}"); // 면 루트
            face.localPosition = center; // 위치
            face.localRotation = Quaternion.LookRotation(outward, Vector3.up); // 로컬 +X = 면 길이, +Y = 위, +Z = 바깥
            List<Vector2> openings = CollectOpenings(run, shape, face); // 이 줄에 뚫리는 출입구 (x = 면 로컬 위치, y = 바닥 높이)
            openings.Sort((left, right) => left.x.CompareTo(right.x)); // 왼쪽부터
            float halfLength = length * 0.5f; // 반길이
            float cursor = -halfLength; // 채우기 시작
            int index = 0; // 번호

            foreach (Vector2 opening in openings) // 출입구 순회
            {
                float min = opening.x - (ModuleDoorway.Width * 0.5f); // 구멍 시작
                float max = opening.x + (ModuleDoorway.Width * 0.5f); // 구멍 끝

                if (min - cursor > 0.01f) // 구멍 앞 벽
                {
                    CreateBox(face, $"Panel_{index}", new Vector3((cursor + min) * 0.5f, wallHeight * 0.5f, -t * 0.5f), new Vector3(min - cursor, wallHeight, t), materials.Wall); // 생성
                }

                if (opening.y > 0.01f) // 출입구 아래 벽 (위층 출입구일 때)
                {
                    CreateBox(face, $"Sill_{index}", new Vector3(opening.x, opening.y * 0.5f, -t * 0.5f), new Vector3(ModuleDoorway.Width, opening.y, t), materials.Wall); // 생성
                }

                float headBase = opening.y + ModuleDoorway.Height; // 문 위 벽 시작
                float headHeight = wallHeight - headBase; // 문 위 벽 높이

                if (headHeight > 0.01f) // 문 위 벽
                {
                    CreateBox(face, $"Header_{index}", new Vector3(opening.x, headBase + (headHeight * 0.5f), -t * 0.5f), new Vector3(ModuleDoorway.Width, headHeight, t), materials.Wall); // 생성
                }

                cursor = max; // 다음 시작
                index++; // 번호
            }

            if (halfLength - cursor > 0.01f) // 마지막 벽
            {
                CreateBox(face, $"Panel_{index}", new Vector3((cursor + halfLength) * 0.5f, wallHeight * 0.5f, -t * 0.5f), new Vector3(halfLength - cursor, wallHeight, t), materials.Wall); // 생성
            }
        }

        private static Vector3 RunCenter(WallRun run, float size) // 벽 줄의 중심 (경계면 위)
        {
            float mid = ((run.From + run.To + 1) * 0.5f) * size; // 길이 방향 중심

            switch (run.Facing) // 방향별
            {
                case GridDirection.North: return new Vector3(mid, 0f, (run.Fixed + 1) * size); // 위쪽 면
                case GridDirection.South: return new Vector3(mid, 0f, run.Fixed * size); // 아래쪽 면
                case GridDirection.East: return new Vector3((run.Fixed + 1) * size, 0f, mid); // 오른쪽 면
                default: return new Vector3(run.Fixed * size, 0f, mid); // 왼쪽 면
            }
        }

        private static List<Vector2> CollectOpenings(WallRun run, DungeonModuleShape shape, Transform face) // 이 벽 줄에 뚫리는 출입구 (x = 면 로컬 위치, y = 바닥 높이)
        {
            List<Vector2> openings = new List<Vector2>(); // 결과
            bool horizontal = run.Facing == GridDirection.North || run.Facing == GridDirection.South; // 가로 줄 여부

            foreach (ShapeDoor door in shape.Doors) // 출입구 순회
            {
                if (door.Facing != run.Facing) // 다른 면
                {
                    continue; // 다음
                }

                int fixedCoord = horizontal ? door.Y : door.X; // 고정 좌표
                int varying = horizontal ? door.X : door.Y; // 변하는 좌표

                if (fixedCoord != run.Fixed || varying < run.From || varying > run.To) // 이 줄이 아님
                {
                    continue; // 다음
                }

                float localX = face.InverseTransformPoint(face.parent.TransformPoint(door.CenterLocal)).x; // 면 로컬 X
                openings.Add(new Vector2(localX, door.BaseY)); // 등록
            }

            return openings; // 반환
        }

        public static DungeonSocket[] BuildSockets(Transform parent, DungeonModuleShape shape, ModuleMaterials materials) // 출입구 표시와 문틀 생성
        {
            DungeonSocket[] sockets = new DungeonSocket[shape.Doors.Count]; // 결과
            float t = WallThickness; // 두께

            for (int index = 0; index < shape.Doors.Count; index++) // 출입구 순회
            {
                ShapeDoor door = shape.Doors[index]; // 출입구
                Transform socketRoot = CreateChild(parent, $"Socket_{index:00}_{door.Facing}"); // 출입구 루트
                socketRoot.localPosition = door.CenterLocal + (Vector3.up * door.BaseY); // 경계면 위
                socketRoot.localRotation = Quaternion.LookRotation(DungeonModuleShape.Outward(door.Facing), Vector3.up); // 바깥을 바라봄
                float halfWidth = (ModuleDoorway.Width + ModuleDoorway.FrameDepth) * 0.5f; // 문틀 바깥 반폭
                Material trim = door.Kind == SocketKind.Exterior ? materials.Accent : materials.Trim; // 문틀 재질
                Transform frameRoot = CreateChild(socketRoot, "Frame"); // 문틀 루트 (벽으로 막을 때 통째로 숨김)
                CreateBox(frameRoot, "Frame_L", new Vector3(-halfWidth + (ModuleDoorway.FrameDepth * 0.5f), ModuleDoorway.Height * 0.5f, -t * 0.5f), new Vector3(ModuleDoorway.FrameDepth, ModuleDoorway.Height, t + 0.02f), trim); // 왼쪽 문틀
                CreateBox(frameRoot, "Frame_R", new Vector3(halfWidth - (ModuleDoorway.FrameDepth * 0.5f), ModuleDoorway.Height * 0.5f, -t * 0.5f), new Vector3(ModuleDoorway.FrameDepth, ModuleDoorway.Height, t + 0.02f), trim); // 오른쪽 문틀
                CreateBox(frameRoot, "Frame_Top", new Vector3(0f, ModuleDoorway.Height - (ModuleDoorway.FrameDepth * 0.5f), -t * 0.5f), new Vector3(ModuleDoorway.Width + ModuleDoorway.FrameDepth, ModuleDoorway.FrameDepth, t + 0.02f), trim); // 위 문틀
                Transform anchor = CreateChild(socketRoot, "DoorAnchor"); // 문짝 기준점
                anchor.localPosition = Vector3.zero; // 경계면 한가운데 바닥
                DungeonSocket socket = socketRoot.gameObject.AddComponent<DungeonSocket>(); // 출입구 부품
                socket.Configure(door.Kind, door.Cell, door.Facing, anchor, frameRoot, door.FloorOffset); // 구성
                sockets[index] = socket; // 등록
            }

            return sockets; // 반환
        }

        private static void BuildLights(Transform parent, DungeonModuleShape shape) // 사각 구역마다 점광원
        {
            Transform lightRoot = CreateChild(parent, "Lights"); // 조명 루트
            Color color = shape.Role == ModuleRole.Boss ? new Color(1f, 0.45f, 0.32f) : shape.Role == ModuleRole.Secret ? new Color(0.72f, 0.55f, 1f) : new Color(1f, 0.72f, 0.45f); // 역할별 색
            int index = 0; // 번호

            foreach (ShapeRect rect in shape.Rects) // 사각 구역 순회
            {
                Vector2 center = rect.CenterCell * ModuleDoorway.CellSize; // 중심
                GameObject lightObject = new GameObject($"RoomLight_{index++:00}"); // 조명
                lightObject.transform.SetParent(lightRoot, false); // 조명 루트 아래
                lightObject.transform.localPosition = new Vector3(center.x, RoomHeight - 0.6f, center.y); // 천장 아래
                Light light = lightObject.AddComponent<Light>(); // 점광원
                light.type = LightType.Point; // 점광원
                light.range = Mathf.Max(6f, rect.ShortSide * 1.8f); // 범위
                light.intensity = shape.Role == ModuleRole.Corridor ? 2.4f : 3.2f; // 밝기
                light.color = color; // 색
                light.shadows = LightShadows.None; // 성능용 그림자 없음
            }
        }

        public static Transform CreateChild(Transform parent, string name) // 빈 자식 생성
        {
            Transform child = new GameObject(name).transform; // 오브젝트
            child.SetParent(parent, false); // 부모
            child.localPosition = Vector3.zero; // 위치
            child.localRotation = Quaternion.identity; // 회전
            return child; // 반환
        }

        public static GameObject CreateBox(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material) // 상자 생성 (충돌체 포함)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube); // 상자
            box.name = name; // 이름
            box.transform.SetParent(parent, false); // 부모
            box.transform.localPosition = localPosition; // 위치
            box.transform.localRotation = Quaternion.identity; // 회전
            box.transform.localScale = size; // 크기

            if (material != null) // 재질 확인
            {
                box.GetComponent<Renderer>().sharedMaterial = material; // 재질
            }

            return box; // 반환
        }
    }
}
