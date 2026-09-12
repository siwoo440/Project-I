using System.Collections.Generic; // 목록 사용
using ProjectI.Generation; // 역할·출입구 종류 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class DungeonModuleCatalog // 30일차 모듈 세트 — 방과 복도의 형태를 칸 단위로 정의합니다 (1칸 = 1m)
    {
        public const string RoomsFolder = "Rooms"; // 방 저장 폴더
        public const string CorridorsFolder = "Corridors"; // 복도 저장 폴더
        public const string VerticalFolder = "Vertical"; // 세로형 방 저장 폴더

        public static List<DungeonModuleShape> All() // 전체 모듈 형태
        {
            List<DungeonModuleShape> shapes = new List<DungeonModuleShape>(); // 결과
            shapes.AddRange(Rooms()); // 방
            shapes.AddRange(Corridors()); // 복도
            return shapes; // 반환
        }

        private static IEnumerable<DungeonModuleShape> Rooms() // 방 모듈
        {
            // 시작 방 — 외부 씬 문(정문·서브문) 전용 출입구 4개와 일반 출입구 4개
            yield return new DungeonModuleShape("Room_Entrance_13x13", RoomsFolder, ModuleRole.Entrance, 1,
                new[] { new ShapeRect(0, 0, 13, 13) },
                new[]
                {
                    new ShapeDoor(6, 12, GridDirection.North, SocketKind.Exterior),
                    new ShapeDoor(6, 0, GridDirection.South, SocketKind.Exterior),
                    new ShapeDoor(12, 6, GridDirection.East, SocketKind.Exterior),
                    new ShapeDoor(0, 6, GridDirection.West, SocketKind.Exterior),
                    new ShapeDoor(2, 12, GridDirection.North),
                    new ShapeDoor(10, 12, GridDirection.North),
                    new ShapeDoor(2, 0, GridDirection.South),
                    new ShapeDoor(10, 0, GridDirection.South),
                });

            // 작은 정사각 방
            yield return new DungeonModuleShape("Room_Square_7x7", RoomsFolder, ModuleRole.Room, 14,
                new[] { new ShapeRect(0, 0, 7, 7) },
                new[]
                {
                    new ShapeDoor(3, 6, GridDirection.North),
                    new ShapeDoor(3, 0, GridDirection.South),
                    new ShapeDoor(6, 3, GridDirection.East),
                    new ShapeDoor(0, 3, GridDirection.West),
                });

            // 큰 홀
            yield return new DungeonModuleShape("Room_Hall_11x11", RoomsFolder, ModuleRole.Room, 8,
                new[] { new ShapeRect(0, 0, 11, 11) },
                new[]
                {
                    new ShapeDoor(5, 10, GridDirection.North),
                    new ShapeDoor(5, 0, GridDirection.South),
                    new ShapeDoor(10, 5, GridDirection.East),
                    new ShapeDoor(0, 5, GridDirection.West),
                });

            // 가로로 긴 방 — 긴 면에 출입구 2개씩
            yield return new DungeonModuleShape("Room_Wide_13x7", RoomsFolder, ModuleRole.Room, 9,
                new[] { new ShapeRect(0, 0, 13, 7) },
                new[]
                {
                    new ShapeDoor(3, 6, GridDirection.North),
                    new ShapeDoor(9, 6, GridDirection.North),
                    new ShapeDoor(3, 0, GridDirection.South),
                    new ShapeDoor(9, 0, GridDirection.South),
                    new ShapeDoor(12, 3, GridDirection.East),
                    new ShapeDoor(0, 3, GridDirection.West),
                });

            // ㄱ자 방
            yield return new DungeonModuleShape("Room_L_11x11", RoomsFolder, ModuleRole.Room, 10,
                new[] { new ShapeRect(0, 0, 11, 5), new ShapeRect(0, 5, 5, 6) },
                new[]
                {
                    new ShapeDoor(5, 0, GridDirection.South),
                    new ShapeDoor(10, 2, GridDirection.East),
                    new ShapeDoor(2, 10, GridDirection.North),
                    new ShapeDoor(0, 8, GridDirection.West),
                });

            // T자 방
            yield return new DungeonModuleShape("Room_T_13x11", RoomsFolder, ModuleRole.Room, 10,
                new[] { new ShapeRect(0, 0, 13, 5), new ShapeRect(4, 5, 5, 6) },
                new[]
                {
                    new ShapeDoor(6, 0, GridDirection.South),
                    new ShapeDoor(0, 2, GridDirection.West),
                    new ShapeDoor(12, 2, GridDirection.East),
                    new ShapeDoor(6, 10, GridDirection.North),
                });

            // 십자 방
            yield return new DungeonModuleShape("Room_Cross_13x13", RoomsFolder, ModuleRole.Room, 7,
                new[] { new ShapeRect(5, 0, 3, 13), new ShapeRect(0, 5, 13, 3) },
                new[]
                {
                    new ShapeDoor(6, 0, GridDirection.South),
                    new ShapeDoor(6, 12, GridDirection.North),
                    new ShapeDoor(0, 6, GridDirection.West),
                    new ShapeDoor(12, 6, GridDirection.East),
                });

            // ㄷ자 방 (가운데가 뚫린 형태)
            yield return new DungeonModuleShape("Room_U_13x11", RoomsFolder, ModuleRole.Room, 6,
                new[] { new ShapeRect(0, 0, 13, 4), new ShapeRect(0, 4, 4, 7), new ShapeRect(9, 4, 4, 7) },
                new[]
                {
                    new ShapeDoor(6, 0, GridDirection.South),
                    new ShapeDoor(0, 2, GridDirection.West),
                    new ShapeDoor(12, 2, GridDirection.East),
                    new ShapeDoor(1, 10, GridDirection.North),
                    new ShapeDoor(11, 10, GridDirection.North),
                });

            // 보스방 — 출입구 1개
            yield return new DungeonModuleShape("Room_Boss_19x19", RoomsFolder, ModuleRole.Boss, 1,
                new[] { new ShapeRect(0, 0, 19, 19) },
                new[] { new ShapeDoor(9, 0, GridDirection.South) });

            // 비밀방 — 부술 수 있는 벽 1개로만 들어감
            yield return new DungeonModuleShape("Room_Secret_9x9", RoomsFolder, ModuleRole.Secret, 1,
                new[] { new ShapeRect(0, 0, 9, 9) },
                new[] { new ShapeDoor(4, 0, GridDirection.South, SocketKind.Breakable) });
        }

        private static IEnumerable<DungeonModuleShape> Corridors() // 복도 모듈
        {
            // 직선 복도
            yield return new DungeonModuleShape("Corridor_Straight_3x9", CorridorsFolder, ModuleRole.Corridor, 16,
                new[] { new ShapeRect(0, 0, 3, 9) },
                new[]
                {
                    new ShapeDoor(1, 0, GridDirection.South),
                    new ShapeDoor(1, 8, GridDirection.North),
                });

            // 긴 직선 복도
            yield return new DungeonModuleShape("Corridor_Long_3x15", CorridorsFolder, ModuleRole.Corridor, 9,
                new[] { new ShapeRect(0, 0, 3, 15) },
                new[]
                {
                    new ShapeDoor(1, 0, GridDirection.South),
                    new ShapeDoor(1, 14, GridDirection.North),
                });

            // ㄱ자 복도
            yield return new DungeonModuleShape("Corridor_Corner_9x9", CorridorsFolder, ModuleRole.Corridor, 13,
                new[] { new ShapeRect(0, 0, 3, 9), new ShapeRect(3, 6, 6, 3) },
                new[]
                {
                    new ShapeDoor(1, 0, GridDirection.South),
                    new ShapeDoor(8, 7, GridDirection.East),
                });

            // T자 복도
            yield return new DungeonModuleShape("Corridor_T_11x7", CorridorsFolder, ModuleRole.Corridor, 11,
                new[] { new ShapeRect(0, 0, 11, 3), new ShapeRect(4, 3, 3, 4) },
                new[]
                {
                    new ShapeDoor(0, 1, GridDirection.West),
                    new ShapeDoor(10, 1, GridDirection.East),
                    new ShapeDoor(5, 6, GridDirection.North),
                });

            // 십자 복도
            yield return new DungeonModuleShape("Corridor_Cross_11x11", CorridorsFolder, ModuleRole.Corridor, 8,
                new[] { new ShapeRect(4, 0, 3, 11), new ShapeRect(0, 4, 11, 3) },
                new[]
                {
                    new ShapeDoor(5, 0, GridDirection.South),
                    new ShapeDoor(5, 10, GridDirection.North),
                    new ShapeDoor(0, 5, GridDirection.West),
                    new ShapeDoor(10, 5, GridDirection.East),
                });

            // 넓은 이음 복도 (방과 방을 짧게 잇는 용도)
            yield return new DungeonModuleShape("Corridor_Short_3x5", CorridorsFolder, ModuleRole.Corridor, 14,
                new[] { new ShapeRect(0, 0, 3, 5) },
                new[]
                {
                    new ShapeDoor(1, 0, GridDirection.South),
                    new ShapeDoor(1, 4, GridDirection.North),
                });
        }
    }
}
