using System.Collections.Generic; // 목록 사용

namespace ProjectI.Generation // 배치 규칙 네임스페이스 (유니티 비의존)
{
    public enum ModuleRole // 모듈 역할
    {
        Entrance, // 시작 방 (정문·서브문)
        Room, // 일반 방
        Corridor, // 복도
        Boss, // 보스방
        Secret, // 비밀방
        Vertical, // 층을 잇는 세로형 방
        PowerPlant, // 발전실 (던전 전체 전력)
        Breaker, // 배전반 방 (구역 전력)
    }

    public enum SocketKind // 출입구 종류
    {
        Door, // 일반 연결
        Exterior, // 외부 씬 문 전용 (정문·서브문)
        Breakable, // 부술 수 있는 벽 (비밀방 전용)
        VerticalUp, // 세로형 방의 위층 출입구
    }

    public readonly struct CellPoint // 모듈 로컬 격자 칸 (1칸 = 1m)
    {
        public readonly int X; // 가로
        public readonly int Y; // 세로 (월드 Z)

        public CellPoint(int x, int y) // 생성
        {
            X = x; // 가로
            Y = y; // 세로
        }

        public CellPoint Step(GridDirection direction) // 한 칸 이동
        {
            switch (direction) // 방향별
            {
                case GridDirection.North: return new CellPoint(X, Y + 1); // +Y
                case GridDirection.East: return new CellPoint(X + 1, Y); // +X
                case GridDirection.South: return new CellPoint(X, Y - 1); // -Y
                default: return new CellPoint(X - 1, Y); // -X
            }
        }

        public CellPoint Rotated(int quarterTurns) // 원점 기준 90° 단위 회전 (칸 한가운데를 돌린 뒤 다시 칸 번호로 환산)
        {
            int turns = ((quarterTurns % 4) + 4) % 4; // 0~3 보정
            int x = X; // 가로
            int y = Y; // 세로

            for (int index = 0; index < turns; index++) // 회전 반복
            {
                int nextX = -y - 1; // 칸 (x,y)의 중심 (x+0.5, y+0.5)을 돌리면 칸 번호는 (-y-1, x)
                int nextY = x; // 90° 회전
                x = nextX; // 갱신
                y = nextY; // 갱신
            }

            return new CellPoint(x, y); // 결과
        }

        public override bool Equals(object obj) => obj is CellPoint other && other.X == X && other.Y == Y; // 값 비교
        public override int GetHashCode() => (X * 73856093) ^ (Y * 19349663); // 해시
        public override string ToString() => $"({X},{Y})"; // 표시
    }

    public readonly struct WorldCell // 층까지 포함한 월드 격자 칸
    {
        public readonly CellPoint Cell; // 평면 칸
        public readonly int Floor; // 층 (메인 방 = 0, 위로 +1, 아래로 -1)

        public WorldCell(CellPoint cell, int floor) // 생성
        {
            Cell = cell; // 칸
            Floor = floor; // 층
        }

        public override bool Equals(object obj) => obj is WorldCell other && other.Cell.Equals(Cell) && other.Floor == Floor; // 값 비교
        public override int GetHashCode() => Cell.GetHashCode() ^ (Floor * 83492791); // 해시
        public override string ToString() => $"{Cell}{FloorName(Floor)}"; // 표시

        public static string FloorName(int floor) // 층 이름
        {
            return floor == 0 ? "1F" : floor > 0 ? $"{floor + 1}F" : $"B{-floor}"; // 1F 기준 위아래
        }
    }

    public readonly struct SocketDefinition // 모듈 로컬 출입구
    {
        public readonly CellPoint Cell; // 출입구가 뚫린 안쪽 칸
        public readonly GridDirection Facing; // 모듈 바깥 방향
        public readonly SocketKind Kind; // 종류
        public readonly int FloorOffset; // 모듈 바닥층에서 몇 층 위에 있는 출입구인지 (세로형 방의 위층 출구는 1)

        public SocketDefinition(CellPoint cell, GridDirection facing, SocketKind kind, int floorOffset = 0) // 생성
        {
            Cell = cell; // 칸
            Facing = facing; // 방향
            Kind = kind; // 종류
            FloorOffset = floorOffset; // 층 차이
        }

        public CellPoint OutsideCell => Cell.Step(Facing); // 출입구 바깥쪽 칸 (연결 상대의 안쪽 칸이 됨)

        public SocketDefinition Rotated(int quarterTurns) // 회전 적용
        {
            return new SocketDefinition(Cell.Rotated(quarterTurns), GridDirections.Rotate(Facing, quarterTurns), Kind, FloorOffset); // 결과 (층 차이는 회전과 무관)
        }
    }

    public sealed class ModuleDefinition // 방 모듈 한 종류 (프리팹 1개에 대응)
    {
        public string Id { get; } // 모듈 이름
        public ModuleRole Role { get; } // 역할
        public IReadOnlyList<CellPoint> Cells { get; } // 차지하는 칸
        public IReadOnlyList<SocketDefinition> Sockets { get; } // 출입구
        public int Weight { get; } // 뽑기 가중치
        public int FloorSpan { get; } // 차지하는 층 수 (일반 1, 세로형 방 2)
        public bool IsVertical => Role == ModuleRole.Vertical; // 세로형 여부

        public ModuleDefinition(string id, ModuleRole role, IReadOnlyList<CellPoint> cells, IReadOnlyList<SocketDefinition> sockets, int weight, int floorSpan = 1) // 생성
        {
            Id = id; // 이름
            Role = role; // 역할
            Cells = cells; // 칸
            Sockets = sockets; // 출입구
            Weight = weight < 1 ? 1 : weight; // 가중치
            FloorSpan = floorSpan < 1 ? 1 : floorSpan; // 층 수
        }

        public int DoorCount // 일반 연결에 쓸 수 있는 출입구 수
        {
            get
            {
                int count = 0; // 집계

                foreach (SocketDefinition socket in Sockets) // 출입구 순회
                {
                    if (socket.Kind == SocketKind.Door) // 일반 연결
                    {
                        count++; // 집계
                    }
                }

                return count; // 결과
            }
        }
    }
}
