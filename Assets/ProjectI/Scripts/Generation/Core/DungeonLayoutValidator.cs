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

            if (count < config.MinRooms || count > config.MaxRooms) // 방 수 범위
            {
                errors.Add($"방 수 {count} (허용 {config.MinRooms}~{config.MaxRooms})"); // 오류
            }

            if (layout.StartRoomId != 0 || count == 0 || !layout.Room(0).Cell.Equals(new GridPoint(0, 0))) // 시작 방 위치
            {
                errors.Add("시작 방이 (0,0)에 없음"); // 오류
                return errors; // 이후 검사 불가
            }

            HashSet<GridPoint> cells = new HashSet<GridPoint>(); // 칸 중복 검사

            foreach (RoomNode room in layout.Rooms) // 방 순회
            {
                if (!cells.Add(room.Cell)) // 중복 칸
                {
                    errors.Add($"방 겹침 {room.Cell}"); // 오류
                }

                if (room.Cell.X < config.GridMinX || room.Cell.X > config.GridMaxX || room.Cell.Y < 0 || room.Cell.Y > config.GridMaxY) // 격자 범위
                {
                    errors.Add($"격자 범위 밖 방 {room.Cell}"); // 오류
                }
            }

            HashSet<long> pairs = new HashSet<long>(); // 문 중복 검사
            int lockedCount = 0; // 잠긴 문 수

            foreach (DoorEdge door in layout.Doors) // 문 순회
            {
                GridPoint a = layout.Room(door.A).Cell; // A 칸
                GridPoint b = layout.Room(door.B).Cell; // B 칸

                if (door.A == door.B || Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) != 1 || GridDirections.Between(a, b) != door.FromA) // 인접·방향 확인
                {
                    errors.Add($"잘못된 문 {door.A}-{door.B}"); // 오류
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

            foreach (RoomNode room in layout.Rooms) // 막다른 방 집계
            {
                deadEnds += room.Id != layout.StartRoomId && layout.DegreeOf(room.Id) == 1 ? 1 : 0; // 집계
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

                foreach (SubDoorPlacement other in layout.SubDoors) // 다른 서브문과 공간 거리
                {
                    if (other != sub && DungeonLayoutGenerator.GridDistance(layout.Room(sub.RoomId).Cell, layout.Room(other.RoomId).Cell) < 2) // 옆 칸 금지
                    {
                        errors.Add($"서브문 {sub.Index + 1}·{other.Index + 1}이 옆 칸 방에 있어 벽을 사이에 두고 겹칠 수 있음"); // 오류
                    }
                }

                if (DungeonLayoutGenerator.GridDistance(layout.Room(sub.RoomId).Cell, layout.Room(layout.StartRoomId).Cell) < 2) // 정문 방과 옆 칸 금지
                {
                    errors.Add($"서브문 {sub.Index + 1}이 정문 방 바로 옆 칸에 있음"); // 오류
                }

                if (sub.RoomId == layout.StartRoomId || sub.RoomId == layout.DeepestRoomId) // 시작 방·최심부 금지
                {
                    errors.Add($"서브문 {sub.Index}이 시작 방 또는 최심부에 있음"); // 오류
                }

                if (unlocked[sub.RoomId] < 0) // 잠긴 문 너머 금지
                {
                    errors.Add($"서브문 {sub.Index}이 잠긴 문 너머에 있음"); // 오류
                }

                if (layout.HasDoorOnWall(sub.RoomId, sub.Wall)) // 방 문과 같은 벽 금지
                {
                    errors.Add($"서브문 {sub.Index}이 방 문과 같은 벽에 있음"); // 오류
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
