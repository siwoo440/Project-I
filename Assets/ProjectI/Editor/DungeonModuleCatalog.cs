using System.Collections.Generic; // 목록 사용
using ProjectI.Generation; // 역할·출입구 종류 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class DungeonModuleCatalog // 모듈 세트 — 방과 복도의 형태를 칸 단위로 정의합니다 (1칸 = 1m, 31일차에 1/3 축소)
    {
        public const string RoomsFolder = "Rooms"; // 방 저장 폴더
        public const string CorridorsFolder = "Corridors"; // 복도 저장 폴더
        public const string VerticalFolder = "Vertical"; // 세로형 방 저장 폴더
        public const string PowerFolder = "Power"; // 발전실·배전반 저장 폴더

        public static List<DungeonModuleShape> All() // 전체 모듈 형태
        {
            List<DungeonModuleShape> shapes = new List<DungeonModuleShape>(); // 결과
            shapes.AddRange(Rooms()); // 방
            shapes.AddRange(Corridors()); // 복도
            return shapes; // 반환
        }

        private static IEnumerable<DungeonModuleShape> Rooms() // 방 모듈
        {
            // 시작 방 — 외부 정문 전용 출입구 4개(하나만 사용)와 일반 출입구 4개
            yield return new DungeonModuleShape("Room_Entrance_9x9", RoomsFolder, ModuleRole.Entrance, 1,
                new[] { new ShapeRect(0, 0, 9, 9) },
                new[]
                {
                    new ShapeDoor(4, 8, GridDirection.North, SocketKind.Exterior),
                    new ShapeDoor(4, 0, GridDirection.South, SocketKind.Exterior),
                    new ShapeDoor(8, 4, GridDirection.East, SocketKind.Exterior),
                    new ShapeDoor(0, 4, GridDirection.West, SocketKind.Exterior),
                    new ShapeDoor(1, 8, GridDirection.North),
                    new ShapeDoor(7, 8, GridDirection.North),
                    new ShapeDoor(1, 0, GridDirection.South),
                    new ShapeDoor(7, 0, GridDirection.South),
                });

            // 작은 정사각 방
            yield return new DungeonModuleShape("Room_Square_5x5", RoomsFolder, ModuleRole.Room, 14,
                new[] { new ShapeRect(0, 0, 5, 5) },
                new[]
                {
                    new ShapeDoor(2, 4, GridDirection.North),
                    new ShapeDoor(2, 0, GridDirection.South),
                    new ShapeDoor(4, 2, GridDirection.East),
                    new ShapeDoor(0, 2, GridDirection.West),
                });

            // 큰 홀
            yield return new DungeonModuleShape("Room_Hall_7x7", RoomsFolder, ModuleRole.Room, 8,
                new[] { new ShapeRect(0, 0, 7, 7) },
                new[]
                {
                    new ShapeDoor(3, 6, GridDirection.North),
                    new ShapeDoor(3, 0, GridDirection.South),
                    new ShapeDoor(6, 3, GridDirection.East),
                    new ShapeDoor(0, 3, GridDirection.West),
                });

            // 가로로 긴 방 — 긴 면에 출입구 2개씩
            yield return new DungeonModuleShape("Room_Wide_9x5", RoomsFolder, ModuleRole.Room, 9,
                new[] { new ShapeRect(0, 0, 9, 5) },
                new[]
                {
                    new ShapeDoor(2, 4, GridDirection.North),
                    new ShapeDoor(6, 4, GridDirection.North),
                    new ShapeDoor(2, 0, GridDirection.South),
                    new ShapeDoor(6, 0, GridDirection.South),
                    new ShapeDoor(8, 2, GridDirection.East),
                    new ShapeDoor(0, 2, GridDirection.West),
                });

            // ㄱ자 방
            yield return new DungeonModuleShape("Room_L_7x7", RoomsFolder, ModuleRole.Room, 10,
                new[] { new ShapeRect(0, 0, 7, 3), new ShapeRect(0, 3, 3, 4) },
                new[]
                {
                    new ShapeDoor(3, 0, GridDirection.South),
                    new ShapeDoor(6, 1, GridDirection.East),
                    new ShapeDoor(1, 6, GridDirection.North),
                    new ShapeDoor(0, 5, GridDirection.West),
                });

            // T자 방
            yield return new DungeonModuleShape("Room_T_9x7", RoomsFolder, ModuleRole.Room, 10,
                new[] { new ShapeRect(0, 0, 9, 3), new ShapeRect(3, 3, 3, 4) },
                new[]
                {
                    new ShapeDoor(4, 0, GridDirection.South),
                    new ShapeDoor(0, 1, GridDirection.West),
                    new ShapeDoor(8, 1, GridDirection.East),
                    new ShapeDoor(4, 6, GridDirection.North),
                });

            // 십자 방
            yield return new DungeonModuleShape("Room_Cross_9x9", RoomsFolder, ModuleRole.Room, 7,
                new[] { new ShapeRect(3, 0, 3, 9), new ShapeRect(0, 3, 9, 3) },
                new[]
                {
                    new ShapeDoor(4, 0, GridDirection.South),
                    new ShapeDoor(4, 8, GridDirection.North),
                    new ShapeDoor(0, 4, GridDirection.West),
                    new ShapeDoor(8, 4, GridDirection.East),
                });

            // ㄷ자 방 (가운데가 뚫린 형태)
            yield return new DungeonModuleShape("Room_U_9x7", RoomsFolder, ModuleRole.Room, 6,
                new[] { new ShapeRect(0, 0, 9, 3), new ShapeRect(0, 3, 3, 4), new ShapeRect(6, 3, 3, 4) },
                new[]
                {
                    new ShapeDoor(4, 0, GridDirection.South),
                    new ShapeDoor(0, 1, GridDirection.West),
                    new ShapeDoor(8, 1, GridDirection.East),
                    new ShapeDoor(1, 6, GridDirection.North),
                    new ShapeDoor(7, 6, GridDirection.North),
                });

            // 지하묘지: 납골벽 회랑 (좁고 긴 방)
            yield return new DungeonModuleShape("Room_Ossuary_11x3", RoomsFolder, ModuleRole.Room, 9,
                new[] { new ShapeRect(0, 0, 11, 3) },
                new[]
                {
                    new ShapeDoor(0, 1, GridDirection.West),
                    new ShapeDoor(10, 1, GridDirection.East),
                    new ShapeDoor(3, 0, GridDirection.South),
                    new ShapeDoor(7, 2, GridDirection.North),
                });

            // 지하묘지: 석관실
            yield return new DungeonModuleShape("Room_Crypt_7x7", RoomsFolder, ModuleRole.Room, 8,
                new[] { new ShapeRect(0, 0, 7, 3), new ShapeRect(2, 3, 3, 4) },
                new[]
                {
                    new ShapeDoor(3, 0, GridDirection.South),
                    new ShapeDoor(0, 1, GridDirection.West),
                    new ShapeDoor(6, 1, GridDirection.East),
                    new ShapeDoor(3, 6, GridDirection.North),
                });

            // 지하묘지: 봉안당 (꺾인 방)
            yield return new DungeonModuleShape("Room_Columbarium_9x9", RoomsFolder, ModuleRole.Room, 6,
                new[] { new ShapeRect(0, 0, 9, 3), new ShapeRect(0, 3, 3, 3), new ShapeRect(0, 6, 9, 3) },
                new[]
                {
                    new ShapeDoor(4, 0, GridDirection.South),
                    new ShapeDoor(8, 1, GridDirection.East),
                    new ShapeDoor(4, 8, GridDirection.North),
                    new ShapeDoor(8, 7, GridDirection.East),
                });

            // 보스방 — 출입구 1개
            yield return new DungeonModuleShape("Room_Boss_13x13", RoomsFolder, ModuleRole.Boss, 1,
                new[] { new ShapeRect(0, 0, 13, 13) },
                new[] { new ShapeDoor(6, 0, GridDirection.South) });

            // 비밀방 — 부술 수 있는 벽 1개로만 들어감
            yield return new DungeonModuleShape("Room_Secret_7x7", RoomsFolder, ModuleRole.Secret, 1,
                new[] { new ShapeRect(0, 0, 7, 7) },
                new[] { new ShapeDoor(3, 0, GridDirection.South, SocketKind.Breakable) });
        }

        private static IEnumerable<DungeonModuleShape> Corridors() // 복도 모듈
        {
            // 짧은 이음 복도
            yield return new DungeonModuleShape("Corridor_Short_3x3", CorridorsFolder, ModuleRole.Corridor, 14,
                new[] { new ShapeRect(0, 0, 3, 3) },
                new[]
                {
                    new ShapeDoor(1, 0, GridDirection.South),
                    new ShapeDoor(1, 2, GridDirection.North),
                });

            // 직선 복도
            yield return new DungeonModuleShape("Corridor_Straight_3x7", CorridorsFolder, ModuleRole.Corridor, 16,
                new[] { new ShapeRect(0, 0, 3, 7) },
                new[]
                {
                    new ShapeDoor(1, 0, GridDirection.South),
                    new ShapeDoor(1, 6, GridDirection.North),
                });

            // 긴 직선 복도
            yield return new DungeonModuleShape("Corridor_Long_3x11", CorridorsFolder, ModuleRole.Corridor, 9,
                new[] { new ShapeRect(0, 0, 3, 11) },
                new[]
                {
                    new ShapeDoor(1, 0, GridDirection.South),
                    new ShapeDoor(1, 10, GridDirection.North),
                });

            // ㄱ자 복도
            yield return new DungeonModuleShape("Corridor_Corner_7x7", CorridorsFolder, ModuleRole.Corridor, 13,
                new[] { new ShapeRect(0, 0, 3, 7), new ShapeRect(3, 4, 4, 3) },
                new[]
                {
                    new ShapeDoor(1, 0, GridDirection.South),
                    new ShapeDoor(6, 5, GridDirection.East),
                });

            // T자 복도
            yield return new DungeonModuleShape("Corridor_T_7x5", CorridorsFolder, ModuleRole.Corridor, 11,
                new[] { new ShapeRect(0, 0, 7, 3), new ShapeRect(2, 3, 3, 2) },
                new[]
                {
                    new ShapeDoor(0, 1, GridDirection.West),
                    new ShapeDoor(6, 1, GridDirection.East),
                    new ShapeDoor(3, 4, GridDirection.North),
                });

            // 십자 복도
            yield return new DungeonModuleShape("Corridor_Cross_7x7", CorridorsFolder, ModuleRole.Corridor, 8,
                new[] { new ShapeRect(2, 0, 3, 7), new ShapeRect(0, 2, 7, 3) },
                new[]
                {
                    new ShapeDoor(3, 0, GridDirection.South),
                    new ShapeDoor(3, 6, GridDirection.North),
                    new ShapeDoor(0, 3, GridDirection.West),
                    new ShapeDoor(6, 3, GridDirection.East),
                });
        }
    }
}
