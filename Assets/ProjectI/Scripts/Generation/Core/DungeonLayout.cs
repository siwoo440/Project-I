using System; // 해시·예외 기능 사용
using System.Collections.Generic; // 방·문 목록 기능 사용

namespace ProjectI.Generation // 절차적 던전 생성 핵심 네임스페이스 (Unity 비의존 순수 C#)
{
    public enum GridDirection // 격자 수평 방향 (층 이동은 세로형 방이 담당)
    {
        North = 0, // +Y (월드 +Z)
        East = 1, // +X
        South = 2, // -Y (월드 -Z)
        West = 3 // -X
    }

    public enum RoomKind // 방 형태
    {
        Room, // 일반 방
        Corridor, // 좁은 연결 복도
        Vertical // 두 층을 잇는 세로형 방 (계단통·사다리 방·수직 통로)
    }

    public enum VerticalKind // 세로형 방 종류
    {
        None, // 세로형 방 아님
        Stairwell, // 계단통 — 걸어서 오르내림
        Ladder, // 사다리 방 — F로 타고 W/S로 오르내림
        Shaft // 좁은 수직 통로 — 사다리와 같은 방식, 복도 크기
    }

    [Flags]
    public enum RoomContent // 방에 표시하는 생성 후보
    {
        None = 0, // 후보 없음
        Loot = 1, // 회수품 생성
        Trap = 2, // 함정 후보 (이후 일차)
        Monster = 4, // 몬스터 후보 (이후 일차)
        Key = 8 // 열쇠 생성
    }

    public readonly struct GridPoint : IEquatable<GridPoint> // 정수 격자 좌표 (층 포함)
    {
        public readonly int X; // 가로 칸
        public readonly int Y; // 세로 칸
        public readonly int Floor; // 층 (0 = 메인 방이 있는 1층, 양수 = 위층, 음수 = 지하층)

        public GridPoint(int x, int y) : this(x, y, 0) // 1층 좌표
        {
        }

        public GridPoint(int x, int y, int floor) // 층 포함 좌표
        {
            X = x; // 가로 저장
            Y = y; // 세로 저장
            Floor = floor; // 층 저장
        }

        public GridPoint Step(GridDirection direction) // 같은 층에서 한 칸 이동한 좌표
        {
            switch (direction) // 방향별 이동
            {
                case GridDirection.North: return new GridPoint(X, Y + 1, Floor); // 북
                case GridDirection.East: return new GridPoint(X + 1, Y, Floor); // 동
                case GridDirection.South: return new GridPoint(X, Y - 1, Floor); // 남
                default: return new GridPoint(X - 1, Y, Floor); // 서
            }
        }

        public GridPoint WithFloor(int floor) => new GridPoint(X, Y, floor); // 같은 칸의 다른 층
        public GridPoint Above => new GridPoint(X, Y, Floor + 1); // 한 층 위
        public GridPoint Below => new GridPoint(X, Y, Floor - 1); // 한 층 아래
        public bool SameColumn(GridPoint other) => X == other.X && Y == other.Y; // 층을 무시한 같은 칸 여부

        public bool Equals(GridPoint other) => X == other.X && Y == other.Y && Floor == other.Floor; // 값 비교
        public override bool Equals(object obj) => obj is GridPoint other && Equals(other); // 값 비교
        public override int GetHashCode() => (X * 73856093) ^ (Y * 19349663) ^ (Floor * 83492791); // 해시
        public override string ToString() => $"({X},{Y} {FloorName(Floor)})"; // 진단 문자열

        public static string FloorName(int floor) // 사람이 읽는 층 이름
        {
            return floor >= 0 ? $"{floor + 1}F" : $"B{-floor}"; // 0 → 1F, 1 → 2F, -1 → B1
        }
    }

    public static class GridDirections // 방향 도구
    {
        public static readonly GridDirection[] All = { GridDirection.North, GridDirection.East, GridDirection.South, GridDirection.West }; // 전체 방향

        public static GridDirection Opposite(GridDirection direction) // 반대 방향
        {
            return (GridDirection)(((int)direction + 2) % 4); // 180도
        }

        public static GridDirection Between(GridPoint from, GridPoint to) // 인접 두 칸 사이 방향 (같은 층)
        {
            if (to.X == from.X + 1) return GridDirection.East; // 동
            if (to.X == from.X - 1) return GridDirection.West; // 서
            return to.Y > from.Y ? GridDirection.North : GridDirection.South; // 북·남
        }
    }

    public sealed class RoomNode // 방 하나
    {
        public int Id; // 방 번호
        public GridPoint Cell; // 격자 좌표 (세로형 방은 아래층 칸)
        public RoomKind Kind; // 방 형태
        public VerticalKind Vertical = VerticalKind.None; // 세로형 방 종류
        public GridDirection LowerWall; // 세로형 방 아래층 출입 벽
        public GridDirection UpperWall; // 세로형 방 위층 출입 벽
        public GridDirection ClimbWall; // 세로형 방 계단·사다리 기준 벽
        public int Depth; // 시작 방으로부터 문 개수 거리
        public bool OnMainPath; // 주 경로 여부
        public int BranchId = -1; // 분기 번호 (주 경로는 -1)
        public bool IsDeadEnd; // 막다른 방 여부
        public bool IsEntrance; // 시작 방·서브문 방 (콘텐츠 제외)
        public bool IsLockedSide; // 잠긴 문 너머 여부
        public RoomContent Content; // 생성 후보

        public bool IsVertical => Kind == RoomKind.Vertical; // 세로형 방 여부
        public int LowerFloor => Cell.Floor; // 아래층
        public int UpperFloor => Kind == RoomKind.Vertical ? Cell.Floor + 1 : Cell.Floor; // 위층 (세로형 방만 다름)

        public bool OccupiesFloor(int floor) // 해당 층을 차지하는지
        {
            return floor == LowerFloor || floor == UpperFloor; // 아래·위층 확인
        }
    }

    public sealed class DoorEdge // 두 방을 잇는 문
    {
        public int A; // 방 A
        public int B; // 방 B
        public GridDirection FromA; // A 기준 문 방향
        public int Floor; // 문이 놓인 층 (세로형 방은 아래층·위층 양쪽에 문을 가짐)
        public bool IsLoop; // 순환로 연결 여부
        public bool IsLocked; // 잠긴 문 여부

        public int Other(int roomId) => roomId == A ? B : A; // 반대편 방
        public GridDirection DirectionFrom(int roomId) => roomId == A ? FromA : GridDirections.Opposite(FromA); // 해당 방 기준 방향
    }

    public sealed class SubDoorPlacement // 실내 서브문 배치
    {
        public int Index; // 외부 서브문과 짝이 되는 번호 (0부터)
        public int RoomId; // 배치 방
        public GridDirection Wall; // 배치 벽
    }

    public sealed class DungeonLayout // 생성 결과
    {
        public int Seed; // 입력 시드
        public int Attempt; // 성공한 시도 번호
        public readonly List<RoomNode> Rooms = new List<RoomNode>(); // 방 목록
        public readonly List<DoorEdge> Doors = new List<DoorEdge>(); // 문 목록
        public readonly List<SubDoorPlacement> SubDoors = new List<SubDoorPlacement>(); // 실내 서브문 목록
        public int StartRoomId; // 시작 방 (메인문)
        public GridDirection MainDoorWall = GridDirection.South; // 시작 방 메인문 벽
        public int DeepestRoomId = -1; // 가장 깊은 방
        public int KeyRoomId = -1; // 열쇠 방 (-1: 잠긴 문 없음)
        public int LockedDoorIndex = -1; // 잠긴 문 번호 (-1: 없음)

        public RoomNode Room(int id) => Rooms[id]; // 방 조회

        public int MinFloor // 가장 낮은 층
        {
            get
            {
                int min = 0; // 결과

                foreach (RoomNode room in Rooms) // 방 순회
                {
                    min = Math.Min(min, room.LowerFloor); // 최저
                }

                return min; // 반환
            }
        }

        public int MaxFloor // 가장 높은 층
        {
            get
            {
                int max = 0; // 결과

                foreach (RoomNode room in Rooms) // 방 순회
                {
                    max = Math.Max(max, room.UpperFloor); // 최고
                }

                return max; // 반환
            }
        }

        public int FloorCount => MaxFloor - MinFloor + 1; // 층 수

        public int MaxDepth // 최대 깊이
        {
            get
            {
                int max = 0; // 결과

                foreach (RoomNode room in Rooms) // 방 순회
                {
                    max = Math.Max(max, room.Depth); // 최대값
                }

                return max; // 반환
            }
        }

        public IEnumerable<RoomNode> RoomsOnFloor(int floor) // 해당 층을 차지하는 방
        {
            foreach (RoomNode room in Rooms) // 방 순회
            {
                if (room.OccupiesFloor(floor)) // 층 확인
                {
                    yield return room; // 반환
                }
            }
        }

        public IEnumerable<RoomNode> VerticalRooms // 세로형 방 목록
        {
            get
            {
                foreach (RoomNode room in Rooms) // 방 순회
                {
                    if (room.IsVertical) // 세로형 확인
                    {
                        yield return room; // 반환
                    }
                }
            }
        }

        public int CountRoomsAnchoredOn(int floor) // 해당 층을 기준 층으로 삼는 방 수 (세로형 방은 아래층에서 1회)
        {
            int count = 0; // 결과

            foreach (RoomNode room in Rooms) // 방 순회
            {
                count += room.Cell.Floor == floor ? 1 : 0; // 집계
            }

            return count; // 반환
        }

        public IEnumerable<DoorEdge> DoorsOf(int roomId) // 방에 연결된 문
        {
            foreach (DoorEdge door in Doors) // 문 순회
            {
                if (door.A == roomId || door.B == roomId) // 연결 확인
                {
                    yield return door; // 반환
                }
            }
        }

        public bool HasDoorOnWall(int roomId, GridDirection wall) // 해당 벽에 방 문이 있는지 (층 구분 없음)
        {
            foreach (DoorEdge door in DoorsOf(roomId)) // 연결 문 순회
            {
                if (door.DirectionFrom(roomId) == wall) // 방향 비교
                {
                    return true; // 있음
                }
            }

            return false; // 없음
        }

        public bool HasDoorOnWall(int roomId, GridDirection wall, int floor) // 해당 층·벽에 방 문이 있는지
        {
            foreach (DoorEdge door in DoorsOf(roomId)) // 연결 문 순회
            {
                if (door.Floor == floor && door.DirectionFrom(roomId) == wall) // 층·방향 비교
                {
                    return true; // 있음
                }
            }

            return false; // 없음
        }

        public int DegreeOf(int roomId) // 연결 문 개수
        {
            int degree = 0; // 개수

            foreach (DoorEdge _ in DoorsOf(roomId)) // 문 순회
            {
                degree++; // 집계
            }

            return degree; // 반환
        }

        public int[] ComputeDistances(int fromRoomId, bool passLockedDoor) // 문 개수 기준 거리 (도달 불가 -1)
        {
            int[] distance = new int[Rooms.Count]; // 거리 배열

            for (int i = 0; i < distance.Length; i++) // 초기화
            {
                distance[i] = -1; // 미도달
            }

            Queue<int> queue = new Queue<int>(); // BFS 큐
            distance[fromRoomId] = 0; // 시작
            queue.Enqueue(fromRoomId); // 시작 등록

            while (queue.Count > 0) // BFS
            {
                int current = queue.Dequeue(); // 현재 방

                foreach (DoorEdge door in DoorsOf(current)) // 이웃 순회
                {
                    if (door.IsLocked && !passLockedDoor) // 잠긴 문 통과 여부
                    {
                        continue; // 건너뜀
                    }

                    int next = door.Other(current); // 이웃 방

                    if (distance[next] >= 0) // 방문 확인
                    {
                        continue; // 건너뜀
                    }

                    distance[next] = distance[current] + 1; // 거리 기록
                    queue.Enqueue(next); // 등록
                }
            }

            return distance; // 결과
        }

        public string Signature() // 구조 비교용 서명 (같은 시드 → 같은 서명)
        {
            unchecked
            {
                int hash = 17; // 초기값

                foreach (RoomNode room in Rooms) // 방
                {
                    hash = hash * 31 + room.Cell.GetHashCode() + (int)room.Kind * 3 + (int)room.Vertical * 11; // 누적
                }

                foreach (DoorEdge door in Doors) // 문
                {
                    hash = hash * 31 + door.A * 7 + door.B * 13 + door.Floor * 23 + (door.IsLocked ? 1 : 0); // 누적
                }

                foreach (SubDoorPlacement sub in SubDoors) // 서브문
                {
                    hash = hash * 31 + sub.RoomId * 5 + (int)sub.Wall; // 누적
                }

                return $"{Rooms.Count}R-{Doors.Count}D-{FloorCount}L-{hash:X8}"; // 서명
            }
        }
    }
}
