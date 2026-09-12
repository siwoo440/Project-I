using System; // 난수·수학 기능 사용
using System.Collections.Generic; // 목록·사전 기능 사용

namespace ProjectI.Generation // 절차적 던전 생성 핵심 네임스페이스
{
    public static class DungeonLayoutGenerator // 1층 → 세로형 방으로 위·아래 층 확장 → 순환로 → 잠긴 문 → 서브문 → 콘텐츠 순서의 격자 그래프 생성기
    {
        public static DungeonLayout Generate(DungeonGenerationConfig config, int seed) // 규칙 검증을 통과한 결과만 반환 (실패 시 null)
        {
            if (config == null || config.SubDoorCount < 0 || config.FloorsAbove < 0 || config.FloorsBelow < 0) // 입력 확인
            {
                return null; // 생성 불가
            }

            for (int attempt = 0; attempt < Math.Max(1, config.MaxAttempts); attempt++) // 시도 반복 (같은 시드는 항상 같은 순서)
            {
                Random random = new Random(unchecked(seed * 486187739 + attempt * 7919 + 17)); // 시도별 결정적 난수
                DungeonLayout layout = TryBuild(config, random); // 한 번 생성

                if (layout == null) // 생성 실패 확인
                {
                    continue; // 다음 시도
                }

                layout.Seed = seed; // 시드 기록
                layout.Attempt = attempt; // 시도 번호 기록

                if (DungeonLayoutValidator.Validate(layout, config).Count == 0) // 전체 규칙 검증
                {
                    return layout; // 성공 반환
                }
            }

            return null; // 모든 시도 실패
        }

        private static DungeonLayout TryBuild(DungeonGenerationConfig config, Random random) // 한 번의 생성 시도
        {
            DungeonLayout layout = new DungeonLayout(); // 결과
            Dictionary<GridPoint, int> occupied = new Dictionary<GridPoint, int>(); // 칸 → 방 번호 (세로형 방은 두 칸 점유)
            int target = Math.Min(random.Next(config.MinRooms, config.MaxRooms + 1), config.GridCellCount); // 1층 목표 방 수
            layout.StartRoomId = AddRoom(layout, occupied, new GridPoint(0, 0, 0), true, -1); // 시작 방 (메인문 방)

            List<int> mainPath = new List<int> { layout.StartRoomId }; // 주 경로
            int mainLength = Math.Max(4, (int)Math.Round(target * config.MainPathRatio)); // 주 경로 길이
            int current = layout.StartRoomId; // 현재 끝 방

            for (int step = 1; step < mainLength; step++) // 주 경로 성장
            {
                GridDirection? direction = PickDirection(config, random, occupied, layout.Room(current).Cell, true); // 안쪽(북쪽) 우선 방향

                if (direction == null) // 막힘 확인
                {
                    return null; // 시도 실패
                }

                int next = AddRoom(layout, occupied, layout.Room(current).Cell.Step(direction.Value), true, -1); // 주 경로 방 추가
                Connect(layout, current, next, false, 0); // 문 연결
                mainPath.Add(next); // 주 경로 기록
                current = next; // 끝 갱신
            }

            if (layout.DegreeOf(layout.StartRoomId) < 2) // 시작 방 출구 2개 이상 보장
            {
                GridDirection? side = PickDirection(config, random, occupied, layout.Room(layout.StartRoomId).Cell, false); // 옆 방향

                if (side != null) // 빈 칸 확인
                {
                    int branch = AddRoom(layout, occupied, layout.Room(layout.StartRoomId).Cell.Step(side.Value), false, 0); // 시작 방 분기
                    Connect(layout, layout.StartRoomId, branch, false, 0); // 연결
                }
            }

            if (!GrowFloor(layout, occupied, random, config, 0, target, mainPath)) // 1층 분기 성장
            {
                return null; // 시도 실패
            }

            int[] order = config.FloorOrder(); // 생성 순서 (1층 → 위 → 아래)

            for (int index = 1; index < order.Length; index++) // 추가 층
            {
                int targetFloor = order[index]; // 새 층
                int sourceFloor = targetFloor > 0 ? targetFloor - 1 : targetFloor + 1; // 이어지는 기존 층

                if (!BuildLinkedFloor(layout, occupied, random, config, sourceFloor, targetFloor)) // 세로형 방 + 새 층
                {
                    return null; // 시도 실패
                }
            }

            RefreshDerived(layout); // 깊이·막다른 방 계산
            AddLoops(layout, occupied, random, config); // 순환로 추가
            RefreshDerived(layout); // 재계산

            if (!ReduceDeadEnds(layout, occupied, random, config)) // 막다른 방 비율 보정
            {
                return null; // 시도 실패
            }

            AssignCorridors(layout, random, config); // 직선 통과 방을 복도로 지정

            if (config.EnableLockedDoor) // 잠긴 문 사용 여부
            {
                PlaceLockedDoorAndKey(layout, random); // 잠긴 문·열쇠 배치
            }

            if (!PlaceSubDoors(layout, random, config)) // 서브문 배치 (외부 서브문 수와 동일, 1층만)
            {
                return null; // 시도 실패
            }

            AssignContent(layout, random, config); // 회수품·함정·몬스터 후보
            return layout; // 결과
        }

        private static bool GrowFloor(DungeonLayout layout, Dictionary<GridPoint, int> occupied, Random random, DungeonGenerationConfig config, int floor, int target, List<int> preferredAnchors) // 한 층의 방을 목표 수까지 분기 성장
        {
            List<int> floorRooms = new List<int>(); // 이 층의 일반 방

            foreach (RoomNode room in layout.Rooms) // 현재 방 순회
            {
                if (!room.IsVertical && room.Cell.Floor == floor) // 같은 층 일반 방
                {
                    floorRooms.Add(room.Id); // 등록
                }
            }

            int branchId = layout.Rooms.Count + 1; // 분기 번호 (층마다 겹치지 않게)
            int guard = 0; // 무한 반복 방지

            while (floorRooms.Count < target && guard++ < 400) // 목표 방 수까지 분기 성장
            {
                bool usePreferred = preferredAnchors != null && preferredAnchors.Count > 0 && random.NextDouble() < 0.7; // 주 경로 우선 여부
                int anchor = usePreferred ? preferredAnchors[random.Next(preferredAnchors.Count)] : floorRooms[random.Next(floorRooms.Count)]; // 분기 시작 방
                int length = random.Next(1, 4); // 분기 길이 1~3
                int tip = anchor; // 분기 끝

                for (int step = 0; step < length && floorRooms.Count < target; step++) // 분기 성장
                {
                    GridDirection? direction = PickDirection(config, random, occupied, layout.Room(tip).Cell, false); // 방향 선택

                    if (direction == null) // 막힘 확인
                    {
                        break; // 분기 종료
                    }

                    int next = AddRoom(layout, occupied, layout.Room(tip).Cell.Step(direction.Value), false, branchId); // 분기 방 추가
                    Connect(layout, tip, next, false, floor); // 연결
                    floorRooms.Add(next); // 등록
                    tip = next; // 끝 갱신
                }

                branchId++; // 다음 분기
            }

            return floorRooms.Count >= target; // 목표 달성 여부
        }

        private static bool BuildLinkedFloor(DungeonLayout layout, Dictionary<GridPoint, int> occupied, Random random, DungeonGenerationConfig config, int sourceFloor, int targetFloor) // 세로형 방 하나로 기존 층과 새 층을 연결하고 새 층을 성장
        {
            int lowerFloor = Math.Min(sourceFloor, targetFloor); // 세로형 방 기준 층 (아래쪽)
            List<int> anchors = new List<int>(); // 세로형 방을 붙일 수 있는 기존 층 방

            foreach (RoomNode room in layout.Rooms) // 방 순회
            {
                if (!room.IsVertical && room.Cell.Floor == sourceFloor) // 기존 층 일반 방
                {
                    anchors.Add(room.Id); // 후보 등록
                }
            }

            Shuffle(anchors, random); // 무작위 순서

            foreach (int anchorId in anchors) // 후보 순회
            {
                List<GridDirection> placements = new List<GridDirection>(); // 세로형 방을 놓을 방향

                foreach (GridDirection direction in GridDirections.All) // 4방향
                {
                    GridPoint cell = layout.Room(anchorId).Cell.Step(direction); // 세로형 방 칸 (기존 층 기준)

                    if (!InBounds(config, cell) || occupied.ContainsKey(cell)) // 범위·점유 확인
                    {
                        continue; // 제외
                    }

                    if (occupied.ContainsKey(cell.WithFloor(targetFloor))) // 새 층 같은 칸 점유 확인
                    {
                        continue; // 제외
                    }

                    placements.Add(direction); // 후보 등록
                }

                Shuffle(placements, random); // 무작위 순서

                foreach (GridDirection direction in placements) // 배치 시도
                {
                    GridPoint column = layout.Room(anchorId).Cell.Step(direction); // 세로형 방 칸 (층 무시)
                    GridDirection sourceWall = GridDirections.Opposite(direction); // 세로형 방에서 기존 층 문이 있는 벽
                    List<GridDirection> exits = new List<GridDirection>(); // 새 층 출구 방향

                    foreach (GridDirection exit in GridDirections.All) // 4방향
                    {
                        if (exit == sourceWall) // 아래·위층 문이 같은 벽이면 계단·사다리 구멍과 겹칠 수 있어 금지
                        {
                            continue; // 제외
                        }

                        GridPoint cell = column.WithFloor(targetFloor).Step(exit); // 새 층 첫 방 칸

                        if (InBounds(config, cell) && !occupied.ContainsKey(cell)) // 범위·점유 확인
                        {
                            exits.Add(exit); // 후보 등록
                        }
                    }

                    if (exits.Count == 0) // 새 층으로 나갈 수 없음
                    {
                        continue; // 다음 배치
                    }

                    GridDirection targetWall = exits[random.Next(exits.Count)]; // 새 층 출구 벽
                    int verticalId = AddVerticalRoom(layout, occupied, column.WithFloor(lowerFloor), random, config, sourceFloor, sourceWall, targetWall); // 세로형 방 생성
                    Connect(layout, anchorId, verticalId, false, sourceFloor); // 기존 층 문
                    int firstId = AddRoom(layout, occupied, column.WithFloor(targetFloor).Step(targetWall), false, -1); // 새 층 첫 방
                    Connect(layout, verticalId, firstId, false, targetFloor); // 새 층 문
                    int floorTarget = Math.Min(random.Next(config.MinOtherFloorRooms, config.MaxOtherFloorRooms + 1), config.GridCellCount - 1); // 새 층 목표 방 수

                    if (GrowFloor(layout, occupied, random, config, targetFloor, floorTarget, null)) // 새 층 성장
                    {
                        return true; // 성공
                    }

                    return false; // 새 층을 채우지 못함 → 시도 전체 재생성
                }
            }

            return false; // 세로형 방을 놓을 자리가 없음
        }

        private static int AddRoom(DungeonLayout layout, Dictionary<GridPoint, int> occupied, GridPoint cell, bool onMainPath, int branchId) // 일반 방 추가
        {
            RoomNode room = new RoomNode { Id = layout.Rooms.Count, Cell = cell, OnMainPath = onMainPath, BranchId = branchId, Kind = RoomKind.Room }; // 새 방
            layout.Rooms.Add(room); // 목록 등록
            occupied[cell] = room.Id; // 칸 점유
            return room.Id; // 번호 반환
        }

        private static int AddVerticalRoom(DungeonLayout layout, Dictionary<GridPoint, int> occupied, GridPoint lowerCell, Random random, DungeonGenerationConfig config, int sourceFloor, GridDirection sourceWall, GridDirection targetWall) // 두 층을 차지하는 세로형 방 추가
        {
            bool sourceIsLower = sourceFloor == lowerCell.Floor; // 기존 층이 아래층인지
            RoomNode room = new RoomNode // 세로형 방
            {
                Id = layout.Rooms.Count, // 번호
                Cell = lowerCell, // 아래층 칸
                Kind = RoomKind.Vertical, // 세로형
                BranchId = -1, // 분기 없음
                LowerWall = sourceIsLower ? sourceWall : targetWall, // 아래층 문 벽
                UpperWall = sourceIsLower ? targetWall : sourceWall // 위층 문 벽
            };

            double roll = random.NextDouble(); // 종류 추첨
            room.Vertical = roll < config.StairwellRatio ? VerticalKind.Stairwell : random.NextDouble() < config.ShaftRatio ? VerticalKind.Shaft : VerticalKind.Ladder; // 계단통 · 수직 통로 · 사다리 방

            if (config.ForcedVerticalKind != VerticalKind.None) // 테스트용 종류 고정
            {
                room.Vertical = config.ForcedVerticalKind; // 고정 종류 적용
            }

            room.ClimbWall = room.Vertical == VerticalKind.Stairwell ? room.LowerWall : PickClimbWall(room, random); // 계단은 아래층 문 벽에서 시작, 사다리는 남은 벽
            layout.Rooms.Add(room); // 목록 등록
            occupied[lowerCell] = room.Id; // 아래층 칸 점유
            occupied[lowerCell.Above] = room.Id; // 위층 칸 점유
            return room.Id; // 번호 반환
        }

        private static GridDirection PickClimbWall(RoomNode room, Random random) // 사다리를 붙일 벽 (아래·위층 문이 없는 벽)
        {
            List<GridDirection> walls = new List<GridDirection>(); // 후보

            foreach (GridDirection direction in GridDirections.All) // 4방향
            {
                if (direction != room.LowerWall && direction != room.UpperWall) // 문이 없는 벽
                {
                    walls.Add(direction); // 등록
                }
            }

            return walls.Count == 0 ? GridDirections.Opposite(room.LowerWall) : walls[random.Next(walls.Count)]; // 선택
        }

        private static void Connect(DungeonLayout layout, int a, int b, bool isLoop, int floor) // 두 인접 방 사이 문 추가
        {
            layout.Doors.Add(new DoorEdge { A = a, B = b, FromA = GridDirections.Between(layout.Room(a).Cell, layout.Room(b).Cell), Floor = floor, IsLoop = isLoop }); // 문 등록
        }

        private static bool AreConnected(DungeonLayout layout, int a, int b) // 두 방 사이 문 존재 여부
        {
            foreach (DoorEdge door in layout.DoorsOf(a)) // 문 순회
            {
                if (door.Other(a) == b) // 연결 확인
                {
                    return true; // 있음
                }
            }

            return false; // 없음
        }

        private static bool InBounds(DungeonGenerationConfig config, GridPoint cell) // 격자·층 범위 확인
        {
            if (cell.Floor > config.FloorsAbove || cell.Floor < -config.FloorsBelow) // 층 범위
            {
                return false; // 범위 밖
            }

            return cell.X >= config.GridMinX && cell.X <= config.GridMaxX && cell.Y >= 0 && cell.Y <= config.GridMaxY; // 범위 안 여부
        }

        private static GridDirection? PickDirection(DungeonGenerationConfig config, Random random, Dictionary<GridPoint, int> occupied, GridPoint from, bool forwardBias) // 같은 층 빈 인접 칸 방향 가중 선택
        {
            List<GridDirection> options = new List<GridDirection>(); // 후보 방향
            List<int> weights = new List<int>(); // 가중치
            int total = 0; // 가중치 합

            foreach (GridDirection direction in GridDirections.All) // 4방향
            {
                GridPoint cell = from.Step(direction); // 후보 칸

                if (!InBounds(config, cell) || occupied.ContainsKey(cell)) // 범위·점유 확인
                {
                    continue; // 제외
                }

                int freeAround = 0; // 후보 칸 주변 빈 칸 수

                foreach (GridDirection around in GridDirections.All) // 주변 검사
                {
                    GridPoint next = cell.Step(around); // 주변 칸

                    if (!next.Equals(from) && InBounds(config, next) && !occupied.ContainsKey(next)) // 빈 칸 확인
                    {
                        freeAround++; // 집계
                    }
                }

                int weight = forwardBias ? (direction == GridDirection.North ? 5 : direction == GridDirection.South ? 1 : 2) : 2; // 방향 가중
                weight = freeAround == 0 ? 1 : weight + freeAround; // 막힌 칸은 낮은 가중
                options.Add(direction); // 후보 등록
                weights.Add(weight); // 가중 등록
                total += weight; // 합계
            }

            if (options.Count == 0) // 후보 없음
            {
                return null; // 막힘
            }

            int roll = random.Next(total); // 가중 추첨

            for (int i = 0; i < options.Count; i++) // 누적 비교
            {
                roll -= weights[i]; // 차감

                if (roll < 0) // 선택
                {
                    return options[i]; // 방향 반환
                }
            }

            return options[options.Count - 1]; // 안전 반환
        }

        private static void RefreshDerived(DungeonLayout layout) // 깊이·막다른 방·최심부 갱신
        {
            int[] distance = layout.ComputeDistances(layout.StartRoomId, true); // 시작 방 기준 거리
            int deepest = layout.StartRoomId; // 최심부

            foreach (RoomNode room in layout.Rooms) // 방 순회
            {
                room.Depth = Math.Max(0, distance[room.Id]); // 깊이
                room.IsDeadEnd = room.Id != layout.StartRoomId && !room.IsVertical && layout.DegreeOf(room.Id) == 1; // 막다른 방 (세로형 방 제외)

                if (room.Depth > layout.Room(deepest).Depth && !room.IsVertical) // 더 깊은 일반 방
                {
                    deepest = room.Id; // 갱신
                }
            }

            layout.DeepestRoomId = deepest; // 기록
        }

        private static void AddLoops(DungeonLayout layout, Dictionary<GridPoint, int> occupied, Random random, DungeonGenerationConfig config) // 깊이 차이가 큰 같은 층 인접 방을 순환로로 연결
        {
            int count = layout.Rooms.Count; // 방 수 (루프 중 변하지 않음)

            for (int id = 0; id < count; id++) // 방 순회
            {
                if (layout.Room(id).IsVertical) // 세로형 방은 문 2개 고정
                {
                    continue; // 제외
                }

                foreach (GridDirection direction in GridDirections.All) // 4방향
                {
                    if (!occupied.TryGetValue(layout.Room(id).Cell.Step(direction), out int other) || other <= id) // 같은 층 인접 방·중복 확인
                    {
                        continue; // 제외
                    }

                    if (layout.Room(other).IsVertical) // 세로형 방 제외
                    {
                        continue; // 제외
                    }

                    if (AreConnected(layout, id, other) || Math.Abs(layout.Room(id).Depth - layout.Room(other).Depth) < 2) // 이미 연결 또는 깊이 차 부족
                    {
                        continue; // 제외
                    }

                    if (random.NextDouble() < config.LoopChance) // 확률 판정
                    {
                        Connect(layout, id, other, true, layout.Room(id).Cell.Floor); // 순환로 연결
                    }
                }
            }
        }

        private static bool ReduceDeadEnds(DungeonLayout layout, Dictionary<GridPoint, int> occupied, Random random, DungeonGenerationConfig config) // 막다른 방 비율을 규칙 이하로 보정
        {
            for (int pass = 0; pass < layout.Rooms.Count; pass++) // 반복 보정
            {
                List<int> deadEnds = new List<int>(); // 막다른 방 목록

                foreach (RoomNode room in layout.Rooms) // 방 순회
                {
                    if (room.IsDeadEnd) // 막다른 방
                    {
                        deadEnds.Add(room.Id); // 등록
                    }
                }

                int allowed = (int)Math.Floor((layout.Rooms.Count - 1) * config.MaxDeadEndRatio); // 허용 개수

                if (deadEnds.Count <= allowed) // 규칙 충족
                {
                    return true; // 성공
                }

                bool connected = false; // 이번 반복 연결 여부
                int start = random.Next(deadEnds.Count); // 무작위 시작

                for (int offset = 0; offset < deadEnds.Count && !connected; offset++) // 막다른 방 순회
                {
                    int id = deadEnds[(start + offset) % deadEnds.Count]; // 대상 방

                    foreach (GridDirection direction in GridDirections.All) // 인접 방 탐색
                    {
                        if (!occupied.TryGetValue(layout.Room(id).Cell.Step(direction), out int other)) // 같은 층 인접 방
                        {
                            continue; // 없음
                        }

                        if (other == layout.StartRoomId || layout.Room(other).IsVertical || AreConnected(layout, id, other)) // 제외 조건
                        {
                            continue; // 제외
                        }

                        Connect(layout, id, other, true, layout.Room(id).Cell.Floor); // 순환로 연결
                        connected = true; // 연결 완료
                        break; // 다음 반복
                    }
                }

                if (!connected) // 더 연결할 수 없음
                {
                    return false; // 시도 실패
                }

                RefreshDerived(layout); // 재계산
            }

            return false; // 보정 실패
        }

        private static void AssignCorridors(DungeonLayout layout, Random random, DungeonGenerationConfig config) // 직선 통과 방을 복도로 지정 (연속 수 제한 유지)
        {
            List<int> candidates = new List<int>(); // 후보

            foreach (RoomNode room in layout.Rooms) // 방 순회
            {
                if (room.Id == layout.StartRoomId || room.IsVertical || room.Id == layout.DeepestRoomId) // 제외 조건
                {
                    continue; // 제외
                }

                if (layout.DegreeOf(room.Id) != 2) // 문 2개만
                {
                    continue; // 제외
                }

                List<GridDirection> walls = new List<GridDirection>(); // 문 방향

                foreach (DoorEdge door in layout.DoorsOf(room.Id)) // 문 순회
                {
                    walls.Add(door.DirectionFrom(room.Id)); // 방향 기록
                }

                if (walls[0] != GridDirections.Opposite(walls[1])) // 마주보는 방향만
                {
                    continue; // 제외
                }

                candidates.Add(room.Id); // 후보 등록
            }

            Shuffle(candidates, random); // 무작위 순서
            int maxCorridors = Math.Max(1, layout.Rooms.Count / 4); // 전체 복도 상한
            int assigned = 0; // 지정 수

            foreach (int id in candidates) // 후보 순회
            {
                if (assigned >= maxCorridors || random.NextDouble() >= config.CorridorChance) // 상한·확률
                {
                    continue; // 제외
                }

                layout.Room(id).Kind = RoomKind.Corridor; // 임시 지정

                if (CorridorChainLength(layout, id) > config.MaxStraightCorridors) // 연속 수 확인
                {
                    layout.Room(id).Kind = RoomKind.Room; // 되돌림
                    continue; // 제외
                }

                assigned++; // 집계
            }
        }

        private static int CorridorChainLength(DungeonLayout layout, int fromRoomId) // 연결된 복도 묶음 크기
        {
            HashSet<int> visited = new HashSet<int>(); // 방문
            Stack<int> stack = new Stack<int>(); // 탐색
            stack.Push(fromRoomId); // 시작

            while (stack.Count > 0) // DFS
            {
                int current = stack.Pop(); // 현재

                if (!visited.Add(current)) // 방문 확인
                {
                    continue; // 건너뜀
                }

                foreach (DoorEdge door in layout.DoorsOf(current)) // 이웃
                {
                    int next = door.Other(current); // 이웃 방

                    if (layout.Room(next).Kind == RoomKind.Corridor) // 복도 연속
                    {
                        stack.Push(next); // 탐색
                    }
                }
            }

            return visited.Count; // 묶음 크기
        }

        private static void PlaceLockedDoorAndKey(DungeonLayout layout, Random random) // 다리(Bridge) 문 하나를 잠그고 열쇠를 도달 가능한 방에 배치
        {
            List<int> candidates = new List<int>(); // 잠글 수 있는 문
            int maxLockedRooms = Math.Max(1, layout.Rooms.Count / 3); // 잠긴 구역 최대 크기

            for (int index = 0; index < layout.Doors.Count; index++) // 문 순회
            {
                DoorEdge door = layout.Doors[index]; // 대상 문

                if (door.A == layout.StartRoomId || door.B == layout.StartRoomId) // 시작 방 문 제외
                {
                    continue; // 제외
                }

                if (layout.Room(door.A).IsVertical || layout.Room(door.B).IsVertical) // 세로형 방 문 제외 (층 전체가 잠기는 것을 막음)
                {
                    continue; // 제외
                }

                door.IsLocked = true; // 임시 잠금
                int[] distance = layout.ComputeDistances(layout.StartRoomId, false); // 잠금 상태 도달 거리
                door.IsLocked = false; // 원복
                int unreachable = 0; // 잠금으로 막히는 방 수

                foreach (int value in distance) // 거리 순회
                {
                    unreachable += value < 0 ? 1 : 0; // 집계
                }

                if (unreachable >= 1 && unreachable <= maxLockedRooms) // 다리 문이며 구역 크기 적정
                {
                    candidates.Add(index); // 후보 등록
                }
            }

            if (candidates.Count == 0) // 후보 없음
            {
                return; // 잠긴 문 없이 진행
            }

            int lockedIndex = candidates[random.Next(candidates.Count)]; // 잠글 문 선택
            layout.Doors[lockedIndex].IsLocked = true; // 잠금
            layout.LockedDoorIndex = lockedIndex; // 기록
            int[] reachable = layout.ComputeDistances(layout.StartRoomId, false); // 잠긴 상태 도달 거리
            List<int> keyRooms = new List<int>(); // 열쇠 후보
            List<int> keyWeights = new List<int>(); // 가중치
            int total = 0; // 가중 합

            foreach (RoomNode room in layout.Rooms) // 방 순회
            {
                room.IsLockedSide = reachable[room.Id] < 0; // 잠긴 구역 표시

                if (room.IsLockedSide || room.Id == layout.StartRoomId || room.Kind != RoomKind.Room) // 열쇠 제외 조건
                {
                    continue; // 제외
                }

                int weight = 1 + room.Depth + (room.IsDeadEnd ? 3 : 0); // 깊고 막다른 방 선호
                keyRooms.Add(room.Id); // 후보 등록
                keyWeights.Add(weight); // 가중 등록
                total += weight; // 합계
            }

            if (keyRooms.Count == 0) // 열쇠 둘 곳 없음
            {
                layout.Doors[lockedIndex].IsLocked = false; // 잠금 해제
                layout.LockedDoorIndex = -1; // 기록 해제

                foreach (RoomNode room in layout.Rooms) // 방 순회
                {
                    room.IsLockedSide = false; // 표시 해제
                }

                return; // 잠긴 문 없이 진행
            }

            int roll = random.Next(total); // 가중 추첨

            for (int i = 0; i < keyRooms.Count; i++) // 누적 비교
            {
                roll -= keyWeights[i]; // 차감

                if (roll < 0) // 선택
                {
                    layout.KeyRoomId = keyRooms[i]; // 열쇠 방
                    break; // 종료
                }
            }

            layout.Room(layout.KeyRoomId).Content |= RoomContent.Key; // 열쇠 표시
        }

        private static bool PlaceSubDoors(DungeonLayout layout, Random random, DungeonGenerationConfig config) // 외부 서브문 수와 같은 개수의 실내 서브문을 1층의 서로 떨어진 방에 배치
        {
            int needed = config.SubDoorCount; // 필요 개수
            layout.Room(layout.StartRoomId).IsEntrance = true; // 시작 방은 입구 방

            if (needed == 0) // 서브문 없는 외부 씬
            {
                return true; // 완료
            }

            int maxDepth = layout.MaxDepth; // 최대 깊이
            int[][] distances = new int[layout.Rooms.Count][]; // 방 사이 거리

            for (int id = 0; id < layout.Rooms.Count; id++) // 전체 쌍 거리
            {
                distances[id] = layout.ComputeDistances(id, true); // BFS
            }

            int[] depthLimits = { Math.Max(config.MinSubDoorSpacing, (int)Math.Ceiling(maxDepth * config.SubDoorMaxDepthRatio)), maxDepth }; // 앞·중간 구간 우선, 부족하면 전체
            int[] spacings = { config.MinSubDoorSpacing, 1 }; // 간격 우선, 부족하면 완화

            foreach (int spacing in spacings) // 간격 단계
            {
                foreach (int depthLimit in depthLimits) // 깊이 단계
                {
                    List<int> candidates = new List<int>(); // 후보 방

                    foreach (RoomNode room in layout.Rooms) // 방 순회
                    {
                        if (room.Cell.Floor != 0 || room.IsVertical) // 서브문은 외부와 이어지는 1층에만 (세로형 방 제외)
                        {
                            continue; // 제외
                        }

                        if (room.Id == layout.StartRoomId || room.Id == layout.DeepestRoomId || room.Id == layout.KeyRoomId || room.IsLockedSide || room.Kind != RoomKind.Room) // 제외 조건
                        {
                            continue; // 제외
                        }

                        if (room.Depth < spacing || room.Depth > depthLimit || FreeWalls(layout, room.Id).Count == 0 || GridDistance(room.Cell, layout.Room(layout.StartRoomId).Cell) < 2) // 깊이·빈 벽·정문 방 옆 칸 조건
                        {
                            continue; // 제외
                        }

                        candidates.Add(room.Id); // 후보 등록
                    }

                    Shuffle(candidates, random); // 무작위 순서
                    List<int> chosen = new List<int>(); // 선택 방

                    foreach (int candidate in candidates) // 욕심 선택
                    {
                        bool farEnough = true; // 간격 충족 여부

                        foreach (int other in chosen) // 기존 선택과 비교
                        {
                            farEnough &= distances[candidate][other] >= spacing; // 문 경로 간격 확인
                            farEnough &= GridDistance(layout.Room(candidate).Cell, layout.Room(other).Cell) >= 2; // 공간상 옆 칸 금지 (벽을 사이에 둔 겹침 방지)
                        }

                        if (farEnough) // 조건 충족
                        {
                            chosen.Add(candidate); // 선택
                        }

                        if (chosen.Count == needed) // 필요 개수 충족
                        {
                            break; // 종료
                        }
                    }

                    if (chosen.Count < needed) // 부족
                    {
                        continue; // 다음 완화 단계
                    }

                    for (int index = 0; index < needed; index++) // 서브문 기록
                    {
                        List<GridDirection> walls = FreeWalls(layout, chosen[index]); // 빈 벽
                        layout.SubDoors.Add(new SubDoorPlacement { Index = index, RoomId = chosen[index], Wall = walls[random.Next(walls.Count)] }); // 배치 등록
                        layout.Room(chosen[index]).IsEntrance = true; // 입구 방 표시
                    }

                    return true; // 성공
                }
            }

            return false; // 외부 서브문 수를 맞출 수 없음 → 재생성
        }

        public static int GridDistance(GridPoint a, GridPoint b) // 격자 거리 (층이 다르면 멀다고 계산)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Floor - b.Floor) * 2; // 거리
        }

        private static List<GridDirection> FreeWalls(DungeonLayout layout, int roomId) // 방 문·메인문이 없는 벽
        {
            List<GridDirection> walls = new List<GridDirection>(); // 결과

            foreach (GridDirection direction in GridDirections.All) // 4방향
            {
                if (layout.HasDoorOnWall(roomId, direction)) // 방 문 확인
                {
                    continue; // 제외
                }

                if (roomId == layout.StartRoomId && direction == layout.MainDoorWall) // 메인문 벽 확인
                {
                    continue; // 제외
                }

                walls.Add(direction); // 등록
            }

            return walls; // 반환
        }

        private static void AssignContent(DungeonLayout layout, Random random, DungeonGenerationConfig config) // 회수품·함정·몬스터 후보 표시 (입구 방·세로형 방 제외)
        {
            List<int> candidates = new List<int>(); // 회수품 후보
            List<int> weights = new List<int>(); // 가중치

            foreach (RoomNode room in layout.Rooms) // 방 순회
            {
                if (room.IsEntrance || room.IsVertical) // 입구 방(설계 문서 8.12)·세로형 방 제외
                {
                    continue; // 제외
                }

                if (random.NextDouble() < (room.Kind == RoomKind.Corridor ? 0.4 : 0.3)) // 함정 후보
                {
                    room.Content |= RoomContent.Trap; // 표시
                }

                if (room.Depth >= 2 && random.NextDouble() < 0.35) // 몬스터 후보
                {
                    room.Content |= RoomContent.Monster; // 표시
                }

                if (room.Id == layout.KeyRoomId) // 열쇠 방은 회수품 제외
                {
                    continue; // 제외
                }

                candidates.Add(room.Id); // 회수품 후보
                weights.Add((room.Depth + 1) * (room.IsDeadEnd ? 2 : 1) * (room.IsLockedSide ? 2 : 1)); // 깊이·막다른 방·잠긴 구역 선호
            }

            int lootRooms = Math.Min(candidates.Count, Math.Max(config.MinLootRooms, Math.Min(config.MaxLootRooms, (int)Math.Round(layout.Rooms.Count * 0.45)))); // 회수품 방 수

            for (int pick = 0; pick < lootRooms; pick++) // 가중 비복원 추첨
            {
                int total = 0; // 가중 합

                foreach (int weight in weights) // 합계
                {
                    total += weight; // 누적
                }

                int roll = random.Next(Math.Max(1, total)); // 추첨

                for (int i = 0; i < candidates.Count; i++) // 누적 비교
                {
                    roll -= weights[i]; // 차감

                    if (roll < 0) // 선택
                    {
                        layout.Room(candidates[i]).Content |= RoomContent.Loot; // 회수품 표시
                        candidates.RemoveAt(i); // 비복원
                        weights.RemoveAt(i); // 비복원
                        break; // 다음 추첨
                    }
                }
            }
        }

        private static void Shuffle<T>(List<T> list, Random random) // Fisher-Yates 섞기
        {
            for (int i = list.Count - 1; i > 0; i--) // 뒤에서부터
            {
                int j = random.Next(i + 1); // 교환 대상
                T temp = list[i]; // 임시
                list[i] = list[j]; // 교환
                list[j] = temp; // 교환
            }
        }
    }
}
