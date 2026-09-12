using System.Collections.Generic; // 목록 사용
using ProjectI.Generation; // 출입구 규격·역할 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public readonly struct ShapeRect // 모듈 형태를 이루는 사각 구역 (칸 단위)
    {
        public readonly int X; // 시작 가로
        public readonly int Y; // 시작 세로
        public readonly int Width; // 가로 칸 수
        public readonly int Height; // 세로 칸 수

        public ShapeRect(int x, int y, int width, int height) // 생성
        {
            X = x; // 시작 가로
            Y = y; // 시작 세로
            Width = width; // 가로
            Height = height; // 세로
        }

        public Vector2 CenterCell => new Vector2(X + (Width * 0.5f), Y + (Height * 0.5f)); // 중심 (칸 좌표)
        public float ShortSide => Mathf.Min(Width, Height); // 짧은 변
    }

    public readonly struct ShapeDoor // 모듈 형태의 출입구 (칸 + 바깥 방향)
    {
        public readonly int X; // 안쪽 칸 가로
        public readonly int Y; // 안쪽 칸 세로
        public readonly GridDirection Facing; // 바깥 방향
        public readonly SocketKind Kind; // 종류
        public readonly int FloorOffset; // 모듈 바닥층에서 몇 층 위에 있는 출입구인지

        public ShapeDoor(int x, int y, GridDirection facing, SocketKind kind = SocketKind.Door, int floorOffset = 0) // 생성
        {
            X = x; // 가로
            Y = y; // 세로
            Facing = facing; // 방향
            Kind = kind; // 종류
            FloorOffset = floorOffset; // 층 차이
        }

        public float BaseY => FloorOffset * (DungeonModuleBaker.RoomHeight + (DungeonModuleBaker.WallThickness * 2f)); // 출입구 바닥 높이

        public Vector2Int Cell => new Vector2Int(X, Y); // 칸

        public Vector3 CenterLocal // 출입구 한가운데 바닥 (모듈 로컬)
        {
            get
            {
                float size = ModuleDoorway.CellSize; // 칸 크기
                switch (Facing) // 방향별
                {
                    case GridDirection.North: return new Vector3((X + 0.5f) * size, 0f, (Y + 1f) * size); // 위쪽 면
                    case GridDirection.South: return new Vector3((X + 0.5f) * size, 0f, Y * size); // 아래쪽 면
                    case GridDirection.East: return new Vector3((X + 1f) * size, 0f, (Y + 0.5f) * size); // 오른쪽 면
                    default: return new Vector3(X * size, 0f, (Y + 0.5f) * size); // 왼쪽 면
                }
            }
        }
    }

    public sealed class DungeonModuleShape // 모듈 하나의 형태 정의 (칸 사각형 + 출입구)
    {
        public string Id { get; } // 모듈 이름
        public ModuleRole Role { get; } // 역할
        public int Weight { get; } // 뽑기 가중치
        public string Folder { get; } // 저장 폴더 이름
        public IReadOnlyList<ShapeRect> Rects { get; } // 형태
        public IReadOnlyList<ShapeDoor> Doors { get; } // 출입구

        public DungeonModuleShape(string id, string folder, ModuleRole role, int weight, ShapeRect[] rects, ShapeDoor[] doors) // 생성
        {
            Id = id; // 이름
            Folder = folder; // 폴더
            Role = role; // 역할
            Weight = weight; // 가중치
            Rects = rects; // 형태
            Doors = doors; // 출입구
        }

        public HashSet<Vector2Int> BuildCells() // 형태가 차지하는 칸 모음
        {
            HashSet<Vector2Int> cells = new HashSet<Vector2Int>(); // 결과

            foreach (ShapeRect rect in Rects) // 사각 구역 순회
            {
                for (int x = 0; x < rect.Width; x++) // 가로
                {
                    for (int y = 0; y < rect.Height; y++) // 세로
                    {
                        cells.Add(new Vector2Int(rect.X + x, rect.Y + y)); // 등록
                    }
                }
            }

            return cells; // 반환
        }

        public string Validate() // 형태·출입구가 규격에 맞는지 확인 (문제 없으면 빈 문자열)
        {
            HashSet<Vector2Int> cells = BuildCells(); // 칸

            if (cells.Count == 0) // 빈 형태
            {
                return $"{Id}: 칸이 없습니다"; // 오류
            }

            foreach (ShapeDoor door in Doors) // 출입구 순회
            {
                if (!cells.Contains(door.Cell)) // 안쪽 칸이 형태 밖
                {
                    return $"{Id}: 출입구 {door.Cell} 가 모듈 안에 없습니다"; // 오류
                }

                Vector2Int outside = door.Cell + Step(door.Facing); // 바깥 칸

                if (cells.Contains(outside)) // 바깥이 막혀 있음
                {
                    return $"{Id}: 출입구 {door.Cell} {door.Facing} 바깥이 모듈 내부입니다"; // 오류
                }

                int runLength = MeasureRun(cells, door); // 출입구가 놓인 벽 길이

                if (runLength * ModuleDoorway.CellSize < ModuleDoorway.Width + 0.4f) // 규격 폭이 들어가지 않음
                {
                    return $"{Id}: 출입구 {door.Cell} {door.Facing} 벽 길이 {runLength}m 가 규격 폭 {ModuleDoorway.Width}m 에 모자랍니다"; // 오류
                }
            }

            for (int index = 0; index < Doors.Count; index++) // 출입구 간격 확인
            {
                for (int other = index + 1; other < Doors.Count; other++) // 짝 순회
                {
                    if (Doors[index].Facing != Doors[other].Facing || Doors[index].FloorOffset != Doors[other].FloorOffset) // 다른 면·다른 층
                    {
                        continue; // 다음
                    }

                    float distance = Vector3.Distance(Doors[index].CenterLocal, Doors[other].CenterLocal); // 거리

                    if (distance < ModuleDoorway.Width + 0.6f) // 너무 가까움
                    {
                        return $"{Id}: 출입구 {Doors[index].Cell} 와 {Doors[other].Cell} 가 {distance:F1}m 로 너무 가깝습니다"; // 오류
                    }
                }
            }

            return string.Empty; // 문제 없음
        }

        private static int MeasureRun(HashSet<Vector2Int> cells, ShapeDoor door) // 출입구가 놓인 벽면이 이어지는 칸 수
        {
            Vector2Int outward = Step(door.Facing); // 바깥 방향
            Vector2Int along = new Vector2Int(-outward.y, outward.x); // 벽 길이 방향
            int length = 1; // 자기 칸

            for (int sign = -1; sign <= 1; sign += 2) // 양쪽
            {
                Vector2Int cursor = door.Cell + (along * sign); // 탐색 시작

                while (cells.Contains(cursor) && !cells.Contains(cursor + outward)) // 같은 벽면이 이어지는 동안
                {
                    length++; // 집계
                    cursor += along * sign; // 다음 칸
                }
            }

            return length; // 반환
        }

        public static Vector2Int Step(GridDirection direction) // 방향 → 칸 이동
        {
            switch (direction) // 방향별
            {
                case GridDirection.North: return new Vector2Int(0, 1); // 위
                case GridDirection.East: return new Vector2Int(1, 0); // 오른쪽
                case GridDirection.South: return new Vector2Int(0, -1); // 아래
                default: return new Vector2Int(-1, 0); // 왼쪽
            }
        }

        public static Vector3 Outward(GridDirection direction) // 방향 → 월드 방향
        {
            switch (direction) // 방향별
            {
                case GridDirection.North: return Vector3.forward; // +Z
                case GridDirection.East: return Vector3.right; // +X
                case GridDirection.South: return Vector3.back; // -Z
                default: return Vector3.left; // -X
            }
        }
    }
}
