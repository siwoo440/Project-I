using System; // 난수·수학 기능 사용
using System.Collections.Generic; // 목록·사전 기능 사용

namespace ProjectI.Generation // 절차적 던전 생성 핵심 네임스페이스
{
    public static class DungeonLayoutGenerator // 1층 → 세로형 방으로 층 확장 → 순환로 → 보스방 → 비밀방 → 잠긴 문 → 서브문 → 콘텐츠 순서의 격자 그래프 생성기
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
            Dictionary<GridPoint, int> occupied = new Dictionary<GridPoint, int>(); // 칸 → 방 번호 (다칸 방은 여러 칸, 세로형 방은 두 층)
            int target = Math.Min(random.Next(config.MinRooms, config.MaxRooms + 1), config.GridCellCount); // 1층 목표 방 수
            layout.StartRoomId = AddRoom(layout, occupied, SingleCell(new GridPoint(0, 0, 0)), true, -1, RoomShape.Single, RoomRole.Normal); // 시작 방 (메인문 방, 항상 1칸)

            List<int> mainPath = new List<int> { layout.StartRoomId }; // 주 경로
            int mainLength = Math.Max(4, (int)Math.Round(target * config.MainPathRatio)); // 주 경로 길이
            int current = layout.StartRoomId; // 현재 끝 방

            for (int step = 1; step < mainLength; step++) // 주 경로 성장
            {
                int next = TryGrowRoom(layout, occupied, random, config, current, 0, PickShape(random, config), true); // 안쪽(북쪽) 우선 성장

                if (next < 0) // 막힘 확인
                {
                    return null; // 시도 실패
                }

                mainPath.Add(next); // 주 경로 기록
                layout.Room(next).OnMainPath = true; // 주 경로 표시
                current = next; // 끝 갱신
            }

            if (layout.DegreeOf(layout.StartRoomId) < 2) // 시작 방 출구 2개 이상 보장
            {
                TryGrowRoom(layout, occupied, random, config, layout.StartRoomId, 0, RoomShape.Single, false); // 시작 방 분기
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

            if (config.EnableBossRoom && !PlaceBossRoom(layout, occupied, random, config)) // 최심층 보스방 배치
            {
                return null; // 시도 실패
            }

            if (config.EnableSecretRoom) // 비밀방 사용 여부
            {
                PlaceSecretRooms(layout, occupied, random, config); // 부술 수 있는 벽 너머 비밀방
            }

            RefreshDerived(layout); // 보스·비밀방 반영
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

        private static List<GridPoint> SingleCell(GridPoint cell) // 한 칸짜리 칸 목록
        {
            return new List<GridPoint> { cell }; // 목록
        }

        private static RoomShape PickShape(Random random, DungeonGenerationConfig config) // 일반 방 모양 추첨
        {
            double roll = random.NextDouble(); // 추첨

            if (roll < config.SingleRoomRatio) // 1칸 방
            {
                return RoomShape.Single; // 기본
            }

            double rest = (roll - config.SingleRoomRatio) / Math.Max(0.0001, 1.0 - config.SingleRoomRatio); // 나머지 구간 정규화

            if (rest < 0.5) // 절반은 2x1
            {
                return RoomShape.Wide; // 긴 방
            }

            return rest < 0.8 ? RoomShape.Ell : RoomShape.Hall; // L자 · 2x2 큰 방
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
                    int next = TryGrowRoom(layout, occupied, random, config, tip, floor, PickShape(random, config), false); // 분기 방 추가

                    if (next < 0) // 막힘 확인
                    {
                        break; // 분기 종료
                    }

                    layout.Room(next).BranchId = branchId; // 분기 번호
                    floorRooms.Add(next); // 등록
                    tip = next; // 끝 갱신
                }

                branchId++; // 다음 분기
            }

            return floorRooms.Count >= target; // 목표 달성 여부
        }

        private static int TryGrowRoom(DungeonLayout layout, Dictionary<GridPoint, int> occupied, Random random, DungeonGenerationConfig config, int fromRoomId, int floor, RoomShape preferredShape, bool forwardBias) // 기존 방 옆에 새 방을 배치하고 문으로 연결 (실패 시 -1)
        {
            RoomNode from = layout.Room(fromRoomId); // 기준 방
            List<GrowthSeed> seeds = new List<GrowthSeed>(); // (기준 칸, 방향, 목표 칸) 후보

            foreach (GridPoint cell in from.Cells) // 기준 방의 모든 칸
            {
                if (cell.Floor != floor) // 다른 층 칸 제외 (세로형 방)
                {
                    continue; // 다음
                }

                foreach (GridDirection direction in GridDirections.All) // 4방향
                {
                    GridPoint targetCell = cell.Step(direction); // 목표 칸

                    if (!InBounds(config, targetCell) || occupied.ContainsKey(targetCell)) // 범위·점유 확인
                    {
                        continue; // 제외
                    }

                    int weight = forwardBias ? (direction == GridDirection.North ? 5 : direction == GridDirection.South ? 1 : 2) : 2; // 방향 가중
                    seeds.Add(new GrowthSeed { Cell = cell, Direction = direction, TargetCell = targetCell, Weight = weight }); // 후보 등록
                }
            }

            if (seeds.Count == 0) // 후보 없음
            {
                return -1; // 막힘
            }

            ShuffleWeighted(seeds, random); // 가중 무작위 순서

            foreach (GrowthSeed seed in seeds) // 후보 순회
            {
                List<RoomShape> shapes = new List<RoomShape> { preferredShape }; // 선호 모양 우선

                if (preferredShape != RoomShape.Single) // 실패 대비 폴백
                {
                    shapes.Add(RoomShape.Single); // 1칸 방으로 대체
                }

                foreach (RoomShape shape in shapes) // 모양 순회
                {
                    List<GridPoint> placed = TryPlaceShape(occupied, config, random, shape, seed.TargetCell); // 칸 배치 시도

                    if (placed == null) // 실패 확인
                    {
                        continue; // 다음 모양
                    }

                    int newRoom = AddRoom(layout, occupied, placed, false, -1, shape, RoomRole.Normal); // 방 추가
                    Connect(layout, fromRoomId, newRoom, false, floor, seed.Cell, seed.TargetCell); // 문 연결
                    return newRoom; // 성공
                }
            }

            return -1; // 배치 실패
        }

        private static List<GridPoint> TryPlaceShape(Dictionary<GridPoint, int> occupied, DungeonGenerationConfig config, Random random, RoomShape shape, GridPoint mustInclude) // 지정 칸을 포함하도록 모양을 놓을 수 있는지 확인
        {
            int rotations = RoomShapes.RotationCount(shape); // 회전 수
            List<int> rotationOrder = new List<int>(); // 회전 순서

            for (int rotation = 0; rotation < rotations; rotation++) // 회전 목록
            {
                rotationOrder.Add(rotation); // 등록
            }

            Shuffle(rotationOrder, random); // 무작위 순서

            foreach (int rotation in rotationOrder) // 회전 순회
            {
                List<GridPoint> offsets = RoomShapes.Offsets(shape, rotation); // 상대 칸
                List<int> anchorOrder = new List<int>(); // 기준 칸 순서

                for (int index = 0; index < offsets.Count; index++) // 기준 후보
                {
                    anchorOrder.Add(index); // 등록
                }

                Shuffle(anchorOrder, random); // 무작위 순서

                foreach (int anchorIndex in anchorOrder) // 어느 칸을 목표 칸에 맞출지
                {
                    GridPoint anchor = offsets[anchorIndex]; // 기준 상대 칸
                    List<GridPoint> cells = new List<GridPoint>(); // 실제 칸 목록
                    bool ok = true; // 배치 가능 여부

                    foreach (GridPoint offset in offsets) // 상대 칸 순회
                    {
                        GridPoint cell = new GridPoint(mustInclude.X + offset.X - anchor.X, mustInclude.Y + offset.Y - anchor.Y, mustInclude.Floor); // 실제 칸
                        ok &= InBounds(config, cell) && !occupied.ContainsKey(cell); // 범위·점유 확인
                        cells.Add(cell); // 등록
                    }

                    if (ok) // 배치 가능
                    {
                        return cells; // 결과
                    }
                }
            }

            return null; // 배치 불가
        }

        private static bool BuildLinkedFloor(DungeonLayout layout, Dictionary<GridPoint, int> occupied, Random random, DungeonGenerationConfig config, int sourceFloor, int targetFloor) // 세로형 방 하나로 기존 층과 새 층을 연결하고 새 층을 성장
        {
            int lowerFloor = Math.Min(sourceFloor, targetFloor); // 세로형 방 기준 층 (아래쪽)
            List<int> anchors = new List<int>(); // 세로형 방을 붙일 수 있는 기존 층 방

            foreach (RoomNode room in layout.Rooms) // 방 순회
            {
                if (!room.IsVertical && room.Cell.Floor == sourceFloor && room.Role == RoomRole.Normal) // 기존 층 일반 방
                {
                    anchors.Add(room.Id); // 후보 등록
                }
            }

            Shuffle(anchors, random); // 무작위 순서

            foreach (int anchorId in anchors) // 후보 순회
            {
                List<GrowthSeed> seeds = new List<GrowthSeed>(); // 세로형 방을 놓을 자리

                foreach (GridPoint cell in layout.Room(anchorId).Cells) // 기준 방 칸
                {
                    foreach (GridDirection direction in GridDirections.All) // 4방향
                    {
                        GridPoint column = cell.Step(direction); // 세로형 방 칸

                        if (!InBounds(config, column) || occupied.ContainsKey(column) || occupied.ContainsKey(column.WithFloor(targetFloor))) // 범위·양쪽 층 점유 확인
                        {
                            continue; // 제외
                        }

                        seeds.Add(new GrowthSeed { Cell = cell, Direction = direction, TargetCell = column, Weight = 1 }); // 후보 등록
                    }
                }

                Shuffle(seeds, random); // 무작위 순서

                foreach (GrowthSeed seed in seeds) // 배치 시도
                {
                    GridPoint column = seed.TargetCell; // 세로형 방 칸
                    GridDirection sourceWall = GridDirections.Opposite(seed.Direction); // 세로형 방에서 기존 층 문이 있는 벽
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
                    Connect(layout, anchorId, verticalId, false, sourceFloor, seed.Cell, column.WithFloor(sourceFloor)); // 기존 층 문
                    GridPoint firstCell = column.WithFloor(targetFloor).Step(targetWall); // 새 층 첫 방 칸
                    int firstId = AddRoom(layout, occupied, SingleCell(firstCell), false, -1, RoomShape.Single, RoomRole.Normal); // 새 층 첫 방
                    Connect(layout, verticalId, firstId, false, targetFloor, column.WithFloor(targetFloor), firstCell); // 새 층 문
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

        private static bool PlaceBossRoom(DungeonLayout layout, Dictionary<GridPoint, int> occupied, Random random, DungeonGenerationConfig config) // 최심층에 3x3 보스방을 입구 1개로 배치
        {
            int bossFloor = -config.FloorsBelow; // 가장 아래층
            List<int> candidates = new List<int>(); // 보스방을 붙일 후보 방

            foreach (RoomNode room in layout.Rooms) // 방 순회
            {
                if (!room.IsVertical && room.Role == RoomRole.Normal && room.Cell.Floor == bossFloor) // 최심층 일반 방
                {
                    candidates.Add(room.Id); // 후보 등록
                }
            }

            candidates.Sort((a, b) => layout.Room(b).Depth.CompareTo(layout.Room(a).Depth)); // 깊은 방부터

            foreach (int anchorId in candidates) // 후보 순회
            {
                int bossId = TryGrowRoom(layout, occupied, random, config, anchorId, bossFloor, RoomShape.Boss, false); // 3x3 배치 시도

                if (bossId < 0) // 실패 확인
                {
                    continue; // 다음 후보
                }

                RoomNode boss = layout.Room(bossId); // 보스방

                if (boss.Shape != RoomShape.Boss) // 폴백으로 1칸이 된 경우
                {
                    continue; // 다음 후보 (되돌리지 않고 일반 방으로 둠)
                }

                boss.Role = RoomRole.Boss; // 보스방 표시
                layout.BossRoomId = bossId; // 기록
                return true; // 성공
            }

            return false; // 배치 실패
        }

        private static void PlaceSecretRooms(DungeonLayout layout, Dictionary<GridPoint, int> occupied, Random random, DungeonGenerationConfig config) // 부술 수 있는 벽 너머 비밀방 배치
        {
            int wanted = config.SecretRoomCount; // 목표 개수
            List<int> candidates = new List<int>(); // 붙일 수 있는 방

            foreach (RoomNode room in layout.Rooms) // 방 순회
            {
                if (!room.IsVertical && room.Role == RoomRole.Normal && room.Id != layout.StartRoomId) // 일반 방만
                {
                    candidates.Add(room.Id); // 후보 등록
                }
            }

            Shuffle(candidates, random); // 무작위 순서

            foreach (int anchorId in candidates) // 후보 순회
            {
                if (layout.SecretRoomIds.Count >= wanted) // 목표 충족
                {
                    return; // 종료
                }

                int secretId = TryGrowRoom(layout, occupied, random, config, anchorId, layout.Room(anchorId).Cell.Floor, RoomShape.Single, false); // 1칸 방 배치

                if (secretId < 0) // 실패 확인
                {
                    continue; // 다음 후보
                }

                layout.Room(secretId).Role = RoomRole.Secret; // 비밀방 표시
                layout.SecretRoomIds.Add(secretId); // 기록

                foreach (DoorEdge door in layout.DoorsOf(secretId)) // 연결 문
                {
                    door.IsBreakable = true; // 부술 수 있는 벽으로 전환
                }
            }
        }

        private static int AddRoom(DungeonLayout layout, Dictionary<GridPoint, int> occupied, List<GridPoint> cells, bool onMainPath, int branchId, RoomShape shape, RoomRole role) // 방 추가 (여러 칸 지원)
        {
            RoomNode room = new RoomNode { Id = layout.Rooms.Count, Cell = cells[0], OnMainPath = onMainPath, BranchId = branchId, Kind = RoomKind.Room, Shape = shape, Role = role }; // 새 방
            room.Cells.AddRange(cells); // 칸 목록
            layout.Rooms.Add(room); // 목록 등록

            foreach (GridPoint cell in cells) // 칸 점유
            {
                occupied[cell] = room.Id; // 등록
            }

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
                Shape = RoomShape.Single, // 항상 1칸
                Role = RoomRole.Normal, // 일반 역할
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
            room.Cells.Add(lowerCell); // 아래층 칸
            room.Cells.Add(lowerCell.Above); // 위층 칸
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

        private static void Connect(DungeonLayout layout, int a, int b, bool isLoop, int floor, GridPoint cellA, GridPoint cellB) // 두 인접 칸 사이 문 추가
        {
            layout.Doors.Add(new DoorEdge // 문 등록
            {
                A = a, // 방 A
                B = b, // 방 B
                CellA = cellA, // A 쪽 칸
                CellB = cellB, // B 쪽 칸
                FromA = GridDirections.Between(cellA, cellB), // A 기준 방향
                Floor = floor, // 층
                IsLoop = isLoop // 순환로 여부
            });
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

        private static void RefreshDerived(DungeonLayout layout) // 깊이·막다른 방·최심부 갱신
        {
            int[] distance = layout.ComputeDistances(layout.StartRoomId, true); // 시작 방 기준 거리
            int deepest = layout.StartRoomId; // 최심부

            foreach (RoomNode room in layout.Rooms) // 방 순회
            {
                room.Depth = Math.Max(0, distance[room.Id]); // 깊이
                room.IsDeadEnd = room.Id != layout.StartRoomId && !room.IsVertical && room.Role == RoomRole.Normal && layout.DegreeOf(room.Id) == 1; // 막다른 방 (세로형·보스·비밀방 제외)

                if (room.Depth > layout.Room(deepest).Depth && !room.IsVertical && room.Role == RoomRole.Normal) // 더 깊은 일반 방
                {
                    deepest = room.Id; // 갱신
                }
            }

            layout.DeepestRoomId = deepest; // 기록
        }

        private static bool CanLink(RoomNode room) // 순환로·보정 연결을 붙일 수 있는 방인지
        {
            return !room.IsVertical && room.Role == RoomRole.Normal; // 세로형·보스·비밀방은 문 수 고정
        }

        private static void AddLoops(DungeonLayout layout, Dictionary<GridPoint, int> occupied, Random random, DungeonGenerationConfig config) // 깊이 차이가 큰 같은 층 인접 방을 순환로로 연결
        {
            int count = layout.Rooms.Count; // 방 수 (루프 중 변하지 않음)

            for (int id = 0; id < count; id++) // 방 순회
            {
                if (!CanLink(layout.Room(id))) // 연결 가능 방 확인
                {
                    continue; // 제외
                }

                foreach (GridPoint cell in layout.Room(id).Cells) // 방의 모든 칸
                {
                    foreach (GridDirection direction in GridDirections.All) // 4방향
                    {
                        GridPoint neighbourCell = cell.Step(direction); // 이웃 칸

                        if (!occupied.TryGetValue(neighbourCell, out int other) || other <= id) // 인접 방·중복 확인
                        {
                            continue; // 제외
                        }

                        if (!CanLink(layout.Room(other))) // 연결 가능 방 확인
                        {
                            continue; // 제외
                        }

                        if (AreConnected(layout, id, other) || Math.Abs(layout.Room(id).Depth - layout.Room(other).Depth) < 2) // 이미 연결 또는 깊이 차 부족
                        {
                            continue; // 제외
                        }

                        if (random.NextDouble() < config.LoopChance) // 확률 판정
                        {
                            Connect(layout, id, other, true, cell.Floor, cell, neighbourCell); // 순환로 연결
                        }
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

                    foreach (GridPoint cell in layout.Room(id).Cells) // 방의 모든 칸
                    {
                        foreach (GridDirection direction in GridDirections.All) // 인접 방 탐색
                        {
                            GridPoint neighbourCell = cell.Step(direction); // 이웃 칸

                            if (!occupied.TryGetValue(neighbourCell, out int other)) // 같은 층 인접 방
                            {
                                continue; // 없음
                            }

                            if (other == id || other == layout.StartRoomId || !CanLink(layout.Room(other)) || AreConnected(layout, id, other)) // 제외 조건
                            {
                                continue; // 제외
                            }

                            Connect(layout, id, other, true, cell.Floor, cell, neighbourCell); // 순환로 연결
                            connected = true; // 연결 완료
                            break; // 다음 반복
                        }

                        if (connected) // 연결 완료
                        {
                            break; // 종료
                        }
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

        private static void AssignCorridors(DungeonLayout layout, Random random, DungeonGenerationConfig config) // 직선 통과 1칸 방을 복도로 지정 (연속 수 제한 유지)
        {
            List<int> candidates = new List<int>(); // 후보

            foreach (RoomNode room in layout.Rooms) // 방 순회
            {
                if (room.Id == layout.StartRoomId || room.IsVertical || room.Id == layout.DeepestRoomId || room.Role != RoomRole.Normal || room.IsMultiCell) // 제외 조건
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

                if (door.A == layout.StartRoomId || door.B == layout.StartRoomId || door.IsBreakable) // 시작 방 문·부술 수 있는 벽 제외
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

                if (room.IsLockedSide || room.Id == layout.StartRoomId || room.Kind != RoomKind.Room || room.Role != RoomRole.Normal) // 열쇠 제외 조건
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
                        if (room.Cell.Floor != 0 || room.IsVertical || room.Role != RoomRole.Normal) // 서브문은 외부와 이어지는 1층 일반 방에만
                        {
                            continue; // 제외
                        }

                        if (room.Id == layout.StartRoomId || room.Id == layout.DeepestRoomId || room.Id == layout.KeyRoomId || room.IsLockedSide || room.Kind != RoomKind.Room) // 제외 조건
                        {
                            continue; // 제외
                        }

                        if (room.Depth < spacing || room.Depth > depthLimit || FreeCellWalls(layout, room.Id).Count == 0 || GridDistance(room, layout.Room(layout.StartRoomId)) < 2) // 깊이·빈 벽·정문 방 옆 칸 조건
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
                            farEnough &= GridDistance(layout.Room(candidate), layout.Room(other)) >= 2; // 공간상 옆 칸 금지 (벽을 사이에 둔 겹침 방지)
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
                        List<CellWall> walls = FreeCellWalls(layout, chosen[index]); // 빈 칸·벽
                        CellWall pick = walls[random.Next(walls.Count)]; // 선택
                        layout.SubDoors.Add(new SubDoorPlacement { Index = index, RoomId = chosen[index], Cell = pick.Cell, Wall = pick.Wall }); // 배치 등록
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

        public static int GridDistance(RoomNode a, RoomNode b) // 두 방에서 가장 가까운 칸 사이 거리
        {
            int best = int.MaxValue; // 최소 거리

            foreach (GridPoint cellA in a.Cells) // A 칸
            {
                foreach (GridPoint cellB in b.Cells) // B 칸
                {
                    best = Math.Min(best, GridDistance(cellA, cellB)); // 최소 갱신
                }
            }

            return best; // 결과
        }

        public static List<CellWall> FreeCellWalls(DungeonLayout layout, int roomId) // 방 문·메인문이 없고 같은 방 칸과 맞닿지 않은 벽
        {
            List<CellWall> walls = new List<CellWall>(); // 결과
            RoomNode room = layout.Room(roomId); // 방

            foreach (GridPoint cell in room.Cells) // 칸 순회
            {
                foreach (GridDirection direction in GridDirections.All) // 4방향
                {
                    if (room.OccupiesCell(cell.Step(direction))) // 같은 방 내부 경계
                    {
                        continue; // 제외
                    }

                    if (layout.HasDoorOnCellWall(roomId, cell, direction)) // 방 문 확인
                    {
                        continue; // 제외
                    }

                    if (roomId == layout.StartRoomId && direction == layout.MainDoorWall) // 메인문 벽 확인
                    {
                        continue; // 제외
                    }

                    walls.Add(new CellWall { Cell = cell, Wall = direction }); // 등록
                }
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

                if (room.IsSecret || room.IsBoss) // 비밀방·보스방은 반드시 회수품
                {
                    room.Content |= RoomContent.Loot; // 표시
                    continue; // 추첨 제외
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

        private static void ShuffleWeighted(List<GrowthSeed> seeds, Random random) // 가중치를 반영한 무작위 정렬
        {
            foreach (GrowthSeed seed in seeds) // 후보 순회
            {
                seed.Order = random.NextDouble() / Math.Max(0.001, seed.Weight); // 가중치가 클수록 앞쪽
            }

            seeds.Sort((a, b) => a.Order.CompareTo(b.Order)); // 정렬
        }

        public struct CellWall // 방의 특정 칸·벽
        {
            public GridPoint Cell; // 칸
            public GridDirection Wall; // 벽 방향
        }

        private sealed class GrowthSeed // 성장 후보 (기준 칸 → 목표 칸)
        {
            public GridPoint Cell; // 기준 방 칸
            public GridDirection Direction; // 성장 방향
            public GridPoint TargetCell; // 새 방이 포함해야 하는 칸
            public int Weight; // 가중치
            public double Order; // 정렬 값
        }
    }
}
