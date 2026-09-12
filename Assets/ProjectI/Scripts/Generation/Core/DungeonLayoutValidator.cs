using System; // 수학 기능 사용
using System.Collections.Generic; // 목록·집합 기능 사용

namespace ProjectI.Generation // 절차적 던전 생성 핵심 네임스페이스
{
    public static class DungeonLayoutValidator // 생성 결과가 모든 규칙을 지키는지 검사 (오류 목록 반환, 비어 있으면 통과)
    {
        public static List<string> Validate(DungeonLayout layout, DungeonGenerationConfig config) // 전체 규칙 검사
        {
            List<string> errors = new List<string>(); // 오류 목록

            if (layout == null || config == null) // 입력 확인
            {
                errors.Add("생성 결과 없음"); // 오류
                return errors; // 종료
            }

            int count = layout.Rooms.Count; // 방 수

            if (layout.StartRoomId != 0 || count == 0 || !layout.Room(0).Cell.Equals(new GridPoint(0, 0, 0))) // 시작 방 위치
            {
                errors.Add("시작 방이 1층 (0,0)에 없음"); // 오류
                return errors; // 이후 검사 불가
            }

            ValidateFloors(layout, config, errors); // 층 구성·방 수
            ValidateCells(layout, config, errors); // 칸 중복·격자 범위
            int[] unlocked = ValidateDoorsAndConnectivity(layout, config, errors); // 문·연결성·잠긴 문
            ValidateVerticalRooms(layout, config, errors); // 세로형 방 규칙
            ValidateSpecialRooms(layout, config, errors); // 보스방·비밀방·부술 수 있는 벽 규칙
            ValidateSubDoors(layout, config, unlocked, errors); // 서브문 규칙
            ValidateCorridors(layout, config, errors); // 복도 규칙

            foreach (RoomNode room in layout.Rooms) // 입구 방 콘텐츠 제외
            {
                if (room.IsEntrance && room.Content != RoomContent.None) // 입구 방 콘텐츠
                {
                    errors.Add($"입구 방 {room.Id}에 콘텐츠 {room.Content}"); // 오류
                }
            }

            return errors; // 결과
        }

        private static void ValidateFloors(DungeonLayout layout, DungeonGenerationConfig config, List<string> errors) // 요청한 층이 모두 생성되고 층마다 방 수가 규칙 안인지
        {
            if (layout.MinFloor != -config.FloorsBelow || layout.MaxFloor != config.FloorsAbove) // 층 범위
            {
                errors.Add($"층 범위 {GridPoint.FloorName(layout.MinFloor)}~{GridPoint.FloorName(layout.MaxFloor)} (요청 {GridPoint.FloorName(-config.FloorsBelow)}~{GridPoint.FloorName(config.FloorsAbove)})"); // 오류
            }

            for (int floor = -config.FloorsBelow; floor <= config.FloorsAbove; floor++) // 층 순회
            {
                int rooms = 0; // 해당 층 일반 방 수 (보스·비밀방 제외)

                foreach (RoomNode room in layout.Rooms) // 방 순회
                {
                    rooms += !room.IsVertical && room.Role == RoomRole.Normal && room.Cell.Floor == floor ? 1 : 0; // 집계
                }

                int min = floor == 0 ? config.MinRooms : config.MinOtherFloorRooms; // 최소
                int max = floor == 0 ? config.MaxRooms : config.MaxOtherFloorRooms; // 최대

                if (rooms < min || rooms > max) // 범위 확인
                {
                    errors.Add($"{GridPoint.FloorName(floor)} 방 수 {rooms} (허용 {min}~{max})"); // 오류
                }
            }
        }

        private static void ValidateCells(DungeonLayout layout, DungeonGenerationConfig config, List<string> errors) // 칸 중복·격자 범위 (세로형 방은 두 층을 차지)
        {
            HashSet<GridPoint> cells = new HashSet<GridPoint>(); // 칸 중복 검사

            foreach (RoomNode room in layout.Rooms) // 방 순회
            {
                if (room.Cells.Count == 0 || !room.Cells[0].Equals(room.Cell)) // 기준 칸 확인
                {
                    errors.Add($"방 {room.Id} 칸 목록 불일치"); // 오류
                    continue; // 이후 검사 불가
                }

                if (!room.IsVertical && room.Cells.Count != RoomShapes.CellCount(room.Shape)) // 모양과 칸 수 일치
                {
                    errors.Add($"방 {room.Id} 모양 {room.Shape}인데 칸 {room.Cells.Count}개"); // 오류
                }

                foreach (GridPoint cell in room.Cells) // 칸 순회
                {
                    if (!cells.Add(cell)) // 칸 중복
                    {
                        errors.Add($"방 겹침 {cell}"); // 오류
                    }

                    if (cell.X < config.GridMinX || cell.X > config.GridMaxX || cell.Y < 0 || cell.Y > config.GridMaxY) // 격자 범위
                    {
                        errors.Add($"격자 범위 밖 방 {cell}"); // 오류
                    }

                    if (cell.Floor < -config.FloorsBelow || cell.Floor > config.FloorsAbove) // 층 범위
                    {
                        errors.Add($"층 범위 밖 방 {cell}"); // 오류
                    }
                }

                if (!room.IsVertical && !IsConnectedShape(room)) // 칸이 서로 붙어 있는지
                {
                    errors.Add($"방 {room.Id} 칸이 끊어져 있음"); // 오류
                }

                if (!room.IsVertical && room.Vertical != VerticalKind.None) // 일반 방 표시 불일치
                {
                    errors.Add($"일반 방 {room.Id}에 세로형 종류 {room.Vertical}"); // 오류
                }

                if (room.Kind == RoomKind.Corridor && room.IsMultiCell) // 복도는 1칸만
                {
                    errors.Add($"복도 {room.Id}가 여러 칸 방"); // 오류
                }
            }
        }

        private static int[] ValidateDoorsAndConnectivity(DungeonLayout layout, DungeonGenerationConfig config, List<string> errors) // 문 인접·층·중복·연결성·막다른 방·잠긴 문
        {
            HashSet<long> pairs = new HashSet<long>(); // 문 중복 검사
            int lockedCount = 0; // 잠긴 문 수
            int count = layout.Rooms.Count; // 방 수

            foreach (DoorEdge door in layout.Doors) // 문 순회
            {
                RoomNode a = layout.Room(door.A); // 방 A
                RoomNode b = layout.Room(door.B); // 방 B

                bool cellsValid = a.OccupiesCell(door.CellA) && b.OccupiesCell(door.CellB); // 문 칸이 각 방에 속하는지
                bool adjacent = Math.Abs(door.CellA.X - door.CellB.X) + Math.Abs(door.CellA.Y - door.CellB.Y) == 1; // 맞닿은 칸인지

                if (door.A == door.B || !cellsValid || !adjacent || GridDirections.Between(door.CellA, door.CellB) != door.FromA) // 인접·방향 확인
                {
                    errors.Add($"잘못된 문 {door.A}-{door.B} ({door.CellA}→{door.CellB})"); // 오류
                }

                if (!a.OccupiesFloor(door.Floor) || !b.OccupiesFloor(door.Floor)) // 두 방이 같은 층에서 만나는지
                {
                    errors.Add($"문 {door.A}-{door.B}이 {GridPoint.FloorName(door.Floor)}에서 이어지지 않음"); // 오류
                }

                if (!a.IsVertical && !b.IsVertical && door.CellA.Floor != door.CellB.Floor) // 층이 다른 일반 방 직결 금지
                {
                    errors.Add($"세로형 방 없이 층이 다른 방 {door.A}-{door.B}이 직접 연결됨"); // 오류
                }

                long key = Math.Min(door.A, door.B) * 10000L + Math.Max(door.A, door.B); // 쌍 키

                if (!pairs.Add(key)) // 중복 문
                {
                    errors.Add($"중복 문 {door.A}-{door.B}"); // 오류
                }

                lockedCount += door.IsLocked ? 1 : 0; // 잠긴 문 집계
            }

            if (layout.HasDoorOnWall(layout.StartRoomId, layout.MainDoorWall)) // 메인문 벽 비어 있는지
            {
                errors.Add("시작 방 메인문 벽에 방 문이 있음"); // 오류
            }

            if (count >= 3 && layout.DegreeOf(layout.StartRoomId) < 2) // 시작 방 출구 수
            {
                errors.Add("시작 방 출구가 2개 미만"); // 오류
            }

            int[] all = layout.ComputeDistances(layout.StartRoomId, true); // 잠긴 문 포함 도달 거리
            int[] unlocked = layout.ComputeDistances(layout.StartRoomId, false); // 잠긴 문 제외 도달 거리

            foreach (RoomNode room in layout.Rooms) // 연결성 확인
            {
                if (all[room.Id] < 0) // 고립 방
                {
                    errors.Add($"시작 방에서 도달할 수 없는 방 {room.Id}{room.Cell}"); // 오류
                }
            }

            int deadEnds = 0; // 막다른 방 수

            foreach (RoomNode room in layout.Rooms) // 막다른 방 집계 (보스·비밀방은 의도된 막다른 방이라 제외)
            {
                deadEnds += room.Id != layout.StartRoomId && room.Role == RoomRole.Normal && layout.DegreeOf(room.Id) == 1 ? 1 : 0; // 집계
            }

            if (count > 1 && deadEnds > Math.Floor((count - 1) * config.MaxDeadEndRatio)) // 비율 확인
            {
                errors.Add($"막다른 방 {deadEnds}개 (허용 {Math.Floor((count - 1) * config.MaxDeadEndRatio)})"); // 오류
            }

            if (layout.LockedDoorIndex >= 0) // 잠긴 문이 있는 경우
            {
                if (lockedCount != 1 || !layout.Doors[layout.LockedDoorIndex].IsLocked) // 잠긴 문 1개
                {
                    errors.Add("잠긴 문 기록 불일치"); // 오류
                }

                if (layout.KeyRoomId < 0 || unlocked[layout.KeyRoomId] < 0) // 열쇠 선취 가능
                {
                    errors.Add("열쇠를 잠긴 문을 지나지 않고 얻을 수 없음"); // 오류
                }

                bool blocksSomething = false; // 잠긴 구역 존재

                foreach (int value in unlocked) // 거리 순회
                {
                    blocksSomething |= value < 0; // 확인
                }

                if (!blocksSomething) // 잠가도 막히는 방이 없음
                {
                    errors.Add("잠긴 문 너머 구역 없음"); // 오류
                }
            }
            else if (lockedCount != 0 || layout.KeyRoomId >= 0) // 잠긴 문이 없는데 표시가 남음
            {
                errors.Add("잠긴 문 없음 상태 불일치"); // 오류
            }

            return unlocked; // 서브문 검사에서 재사용
        }

        private static void ValidateVerticalRooms(DungeonLayout layout, DungeonGenerationConfig config, List<string> errors) // 세로형 방(계단통·사다리 방·수직 통로) 규칙
        {
            HashSet<int> linkedPairs = new HashSet<int>(); // 연결된 층 쌍 (아래층 번호)

            foreach (RoomNode room in layout.VerticalRooms) // 세로형 방 순회
            {
                linkedPairs.Add(room.LowerFloor); // 층 쌍 기록

                if (room.Vertical == VerticalKind.None) // 종류 확인
                {
                    errors.Add($"세로형 방 {room.Id} 종류 없음"); // 오류
                }

                if (config.ForcedVerticalKind != VerticalKind.None && room.Vertical != config.ForcedVerticalKind) // 테스트용 고정 종류 확인
                {
                    errors.Add($"세로형 방 {room.Id} 종류 {room.Vertical} ≠ 고정 {config.ForcedVerticalKind}"); // 오류
                }

                if (layout.DegreeOf(room.Id) != 2) // 문 2개 고정
                {
                    errors.Add($"세로형 방 {room.Id} 문 {layout.DegreeOf(room.Id)}개 (2개 필요)"); // 오류
                }

                if (room.LowerWall == room.UpperWall) // 아래·위층 문이 같은 벽
                {
                    errors.Add($"세로형 방 {room.Id} 아래·위층 문이 같은 벽 {room.LowerWall}"); // 오류
                }

                if (!layout.HasDoorOnWall(room.Id, room.LowerWall, room.LowerFloor)) // 아래층 문
                {
                    errors.Add($"세로형 방 {room.Id} {GridPoint.FloorName(room.LowerFloor)} 문 누락"); // 오류
                }

                if (!layout.HasDoorOnWall(room.Id, room.UpperWall, room.UpperFloor)) // 위층 문
                {
                    errors.Add($"세로형 방 {room.Id} {GridPoint.FloorName(room.UpperFloor)} 문 누락"); // 오류
                }

                bool stair = room.Vertical == VerticalKind.Stairwell; // 계단통 여부

                if (stair && room.ClimbWall != room.LowerWall) // 계단은 아래층 문 벽에서 올라감
                {
                    errors.Add($"계단통 {room.Id} 계단 시작 벽 {room.ClimbWall} ≠ 아래층 문 벽 {room.LowerWall}"); // 오류
                }

                if (!stair && (room.ClimbWall == room.LowerWall || room.ClimbWall == room.UpperWall)) // 사다리는 문이 없는 벽에
                {
                    errors.Add($"사다리 {room.Id}가 문이 있는 벽 {room.ClimbWall}에 있음"); // 오류
                }

                if (room.Content != RoomContent.None || room.IsEntrance) // 세로형 방에는 콘텐츠·출입구 없음
                {
                    errors.Add($"세로형 방 {room.Id}에 콘텐츠·출입구 표시"); // 오류
                }
            }

            for (int lower = -config.FloorsBelow; lower < config.FloorsAbove; lower++) // 인접 층 쌍 순회
            {
                if (!linkedPairs.Contains(lower)) // 연결 누락
                {
                    errors.Add($"{GridPoint.FloorName(lower)} ↔ {GridPoint.FloorName(lower + 1)} 세로형 방 없음"); // 오류
                }
            }
        }

        private static bool IsConnectedShape(RoomNode room) // 방의 칸이 서로 맞닿아 하나로 이어지는지
        {
            if (room.Cells.Count <= 1) // 1칸 확인
            {
                return true; // 통과
            }

            HashSet<GridPoint> remaining = new HashSet<GridPoint>(room.Cells); // 남은 칸
            Stack<GridPoint> stack = new Stack<GridPoint>(); // 탐색
            stack.Push(room.Cells[0]); // 시작
            remaining.Remove(room.Cells[0]); // 방문 처리

            while (stack.Count > 0) // 깊이 탐색
            {
                GridPoint current = stack.Pop(); // 현재 칸

                foreach (GridDirection direction in GridDirections.All) // 4방향
                {
                    GridPoint next = current.Step(direction); // 이웃 칸

                    if (remaining.Remove(next)) // 같은 방 칸인지
                    {
                        stack.Push(next); // 탐색
                    }
                }
            }

            return remaining.Count == 0; // 모두 이어졌는지
        }

        private static void ValidateSpecialRooms(DungeonLayout layout, DungeonGenerationConfig config, List<string> errors) // 보스방·비밀방·부술 수 있는 벽 규칙
        {
            int bossCount = 0; // 보스방 수
            int secretCount = 0; // 비밀방 수

            foreach (RoomNode room in layout.Rooms) // 방 순회
            {
                if (room.IsBoss) // 보스방
                {
                    bossCount++; // 집계

                    if (room.Cells.Count != 9 || room.Shape != RoomShape.Boss) // 3x3 확인
                    {
                        errors.Add($"보스방 {room.Id} 칸 {room.Cells.Count}개 (3x3 = 9칸 필요)"); // 오류
                    }

                    if (layout.DegreeOf(room.Id) != 1) // 입구 1개
                    {
                        errors.Add($"보스방 {room.Id} 입구 {layout.DegreeOf(room.Id)}개 (1개 필요)"); // 오류
                    }

                    if (room.Cell.Floor != -config.FloorsBelow) // 최심층 확인
                    {
                        errors.Add($"보스방 {room.Id}이 {GridPoint.FloorName(room.Cell.Floor)}에 있음 (최심층 필요)"); // 오류
                    }

                    if (room.Id == layout.KeyRoomId || room.Kind == RoomKind.Corridor) // 열쇠·복도 금지
                    {
                        errors.Add($"보스방 {room.Id}에 열쇠 또는 복도 표시"); // 오류
                    }
                }

                if (room.IsSecret) // 비밀방
                {
                    secretCount++; // 집계

                    if (layout.DegreeOf(room.Id) != 1) // 입구 1개
                    {
                        errors.Add($"비밀방 {room.Id} 입구 {layout.DegreeOf(room.Id)}개 (1개 필요)"); // 오류
                    }

                    foreach (DoorEdge door in layout.DoorsOf(room.Id)) // 연결 문
                    {
                        if (!door.IsBreakable) // 부술 수 있는 벽 확인
                        {
                            errors.Add($"비밀방 {room.Id} 입구가 부술 수 있는 벽이 아님"); // 오류
                        }
                    }
                }
            }

            if (config.EnableBossRoom && bossCount != 1) // 보스방 개수
            {
                errors.Add($"보스방 {bossCount}개 (1개 필요)"); // 오류
            }

            if (!config.EnableBossRoom && bossCount != 0) // 비활성인데 생성됨
            {
                errors.Add($"보스방 사용 안 함인데 {bossCount}개 생성"); // 오류
            }

            if (config.EnableSecretRoom && secretCount != config.SecretRoomCount) // 비밀방 개수
            {
                errors.Add($"비밀방 {secretCount}개 (요청 {config.SecretRoomCount}개)"); // 오류
            }

            foreach (DoorEdge door in layout.Doors) // 부술 수 있는 벽은 비밀방 전용
            {
                if (door.IsBreakable && !layout.Room(door.A).IsSecret && !layout.Room(door.B).IsSecret) // 비밀방과 연결되지 않은 파괴 벽
                {
                    errors.Add($"부술 수 있는 벽 {door.A}-{door.B}이 비밀방과 연결되지 않음"); // 오류
                }

                if (door.IsBreakable && door.IsLocked) // 잠긴 문과 중복 금지
                {
                    errors.Add($"부술 수 있는 벽 {door.A}-{door.B}이 잠긴 문으로 지정됨"); // 오류
                }
            }
        }

        private static void ValidateSubDoors(DungeonLayout layout, DungeonGenerationConfig config, int[] unlocked, List<string> errors) // 서브문 수·1:1·겹침·위치 규칙
        {
            if (layout.SubDoors.Count != config.SubDoorCount) // 외부 서브문 수와 동일
            {
                errors.Add($"실내 서브문 {layout.SubDoors.Count}개 ≠ 외부 서브문 {config.SubDoorCount}개"); // 오류
            }

            HashSet<int> indices = new HashSet<int>(); // 번호 중복 검사
            HashSet<int> rooms = new HashSet<int>(); // 방 중복 검사

            foreach (SubDoorPlacement sub in layout.SubDoors) // 서브문 순회
            {
                if (sub.Index < 0 || sub.Index >= config.SubDoorCount || !indices.Add(sub.Index)) // 외부 서브문과 1:1 번호
                {
                    errors.Add($"서브문 번호 {sub.Index} 중복 또는 범위 밖"); // 오류
                }

                if (!rooms.Add(sub.RoomId)) // 같은 방에 서브문 2개 금지
                {
                    errors.Add($"서브문이 같은 방 {sub.RoomId}에 겹침"); // 오류
                }

                if (layout.Room(sub.RoomId).Cell.Floor != 0) // 외부와 이어지는 1층만
                {
                    errors.Add($"서브문 {sub.Index + 1}이 {GridPoint.FloorName(layout.Room(sub.RoomId).Cell.Floor)}에 있음 (1층 필요)"); // 오류
                }

                foreach (SubDoorPlacement other in layout.SubDoors) // 다른 서브문과 공간 거리
                {
                    if (other != sub && DungeonLayoutGenerator.GridDistance(layout.Room(sub.RoomId), layout.Room(other.RoomId)) < 2) // 옆 칸 금지
                    {
                        errors.Add($"서브문 {sub.Index + 1}·{other.Index + 1}이 옆 칸 방에 있어 벽을 사이에 두고 겹칠 수 있음"); // 오류
                    }
                }

                if (DungeonLayoutGenerator.GridDistance(layout.Room(sub.RoomId), layout.Room(layout.StartRoomId)) < 2) // 정문 방과 옆 칸 금지
                {
                    errors.Add($"서브문 {sub.Index + 1}이 정문 방 바로 옆 칸에 있음"); // 오류
                }

                if (sub.RoomId == layout.StartRoomId || sub.RoomId == layout.DeepestRoomId) // 시작 방·최심부 금지
                {
                    errors.Add($"서브문 {sub.Index}이 시작 방 또는 최심부에 있음"); // 오류
                }

                if (layout.Room(sub.RoomId).IsVertical) // 세로형 방 금지
                {
                    errors.Add($"서브문 {sub.Index}이 세로형 방에 있음"); // 오류
                }

                if (unlocked[sub.RoomId] < 0) // 잠긴 문 너머 금지
                {
                    errors.Add($"서브문 {sub.Index}이 잠긴 문 너머에 있음"); // 오류
                }

                if (!layout.Room(sub.RoomId).OccupiesCell(sub.Cell)) // 서브문 칸이 방에 속하는지
                {
                    errors.Add($"서브문 {sub.Index + 1} 칸 {sub.Cell}이 방 {sub.RoomId}에 속하지 않음"); // 오류
                }

                if (layout.HasDoorOnCellWall(sub.RoomId, sub.Cell, sub.Wall)) // 방 문과 같은 칸·벽 금지
                {
                    errors.Add($"서브문 {sub.Index}이 방 문과 같은 벽에 있음"); // 오류
                }

                if (layout.Room(sub.RoomId).Role != RoomRole.Normal) // 보스·비밀방 금지
                {
                    errors.Add($"서브문 {sub.Index + 1}이 {layout.Room(sub.RoomId).Role} 방에 있음"); // 오류
                }

                if (!layout.Room(sub.RoomId).IsEntrance) // 입구 방 표시
                {
                    errors.Add($"서브문 방 {sub.RoomId} 입구 방 표시 누락"); // 오류
                }
            }
        }

        private static void ValidateCorridors(DungeonLayout layout, DungeonGenerationConfig config, List<string> errors) // 복도 형태·연속 수 규칙
        {
            HashSet<int> visited = new HashSet<int>(); // 방문 복도

            foreach (RoomNode room in layout.Rooms) // 방 순회
            {
                if (room.Kind != RoomKind.Corridor || visited.Contains(room.Id)) // 새 복도 묶음 시작만
                {
                    continue; // 제외
                }

                int chain = 0; // 연속 복도 수
                Stack<int> stack = new Stack<int>(); // 탐색
                stack.Push(room.Id); // 시작

                while (stack.Count > 0) // 연결된 복도 묶음 탐색
                {
                    int current = stack.Pop(); // 현재

                    if (!visited.Add(current)) // 방문 확인
                    {
                        continue; // 건너뜀
                    }

                    chain++; // 집계

                    if (layout.DegreeOf(current) != 2) // 복도는 문 2개
                    {
                        errors.Add($"복도 {current}의 문 수 {layout.DegreeOf(current)}"); // 오류
                    }

                    foreach (DoorEdge door in layout.DoorsOf(current)) // 이웃 복도
                    {
                        int next = door.Other(current); // 이웃

                        if (layout.Room(next).Kind == RoomKind.Corridor) // 복도 연속
                        {
                            stack.Push(next); // 탐색
                        }
                    }
                }

                if (chain > config.MaxStraightCorridors) // 연속 수 제한
                {
                    errors.Add($"직선 복도 {chain}개 연속 (허용 {config.MaxStraightCorridors})"); // 오류
                }
            }
        }
    }
}
