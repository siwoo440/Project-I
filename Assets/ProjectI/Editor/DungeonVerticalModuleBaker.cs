using System.Collections.Generic; // 목록 사용
using ProjectI.Dungeon; // 모듈 부품 참조
using ProjectI.Generation; // 출입구 규격 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class DungeonVerticalModuleBaker // 두 층을 잇는 세로형 모듈(계단통·사다리 방)을 만듭니다
    {
        public const float FloorStep = DungeonModuleBaker.RoomHeight + (DungeonModuleBaker.WallThickness * 2f); // 한 층 높이 (4.50m)
        private const float StairWidth = 3f; // 계단 폭
        private const float StairStepHeight = 0.3f; // 계단 한 칸 높이
        private const float RampThickness = 0.4f; // 경사 충돌체 두께
        private const float LadderHoleWidth = 2.2f; // 사다리 구멍 폭
        private const float LadderHoleDepth = 2f; // 사다리 구멍 깊이

        public static GameObject BuildStairwell(ModuleMaterials materials) // 계단통 모듈 (아래층 남쪽 문 → 위층 북쪽 문)
        {
            const int Width = 7; // 가로 칸
            const int Length = 13; // 세로 칸
            const float RampStartZ = 2f; // 계단 시작
            const float RampEndZ = 9f; // 계단 끝 (여기서부터 위층 바닥)
            DungeonModuleShape shape = new DungeonModuleShape("Vertical_Stairwell_7x13", DungeonModuleCatalog.VerticalFolder, ModuleRole.Vertical, 6,
                new[] { new ShapeRect(0, 0, Width, Length) },
                new[]
                {
                    new ShapeDoor(3, 0, GridDirection.South), // 아래층 출입구
                    new ShapeDoor(3, Length - 1, GridDirection.North, SocketKind.VerticalUp, 1), // 위층 출입구
                }); // 형태

            GameObject root = BuildShell(shape, materials, out Transform structure, out DungeonSocket[] sockets, out Transform content); // 껍데기 (바닥·천장·벽·출입구)
            float t = DungeonModuleBaker.WallThickness; // 두께
            float rise = FloorStep; // 올라갈 높이
            float run = RampEndZ - RampStartZ; // 수평 길이
            float sideCenterX = Width * 0.5f; // 계단 중심 (가로 한가운데)
            Transform stairs = DungeonModuleBaker.CreateChild(structure, "Stairs"); // 계단 루트
            int steps = Mathf.Max(2, Mathf.CeilToInt(rise / StairStepHeight)); // 계단 수
            float stepRise = rise / steps; // 한 칸 높이
            float tread = run / steps; // 한 칸 깊이

            for (int index = 0; index < steps; index++) // 디딤판 (보이는 부분, 충돌체는 경사면 하나로 대신함)
            {
                float centerZ = RampStartZ + (tread * (index + 0.5f)); // 디딤판 중심
                float height = stepRise * (index + 1); // 바닥부터 높이
                GameObject step = DungeonModuleBaker.CreateBox(stairs, $"Step_{index:00}", new Vector3(sideCenterX, height * 0.5f, centerZ), new Vector3(StairWidth, height, tread), materials.Floor); // 계단 한 칸
                Object.DestroyImmediate(step.GetComponent<Collider>()); // 충돌체 제거 (걸림 방지)
            }

            Quaternion rampRotation = Quaternion.LookRotation(new Vector3(0f, rise, run).normalized, Vector3.up); // 경사 회전
            GameObject ramp = new GameObject("StairRamp"); // 경사 충돌체 (보이지 않음)
            ramp.transform.SetParent(stairs, false); // 계단 아래
            ramp.transform.localRotation = rampRotation; // 회전
            ramp.transform.localPosition = new Vector3(sideCenterX, rise * 0.5f, (RampStartZ + RampEndZ) * 0.5f) + ((rampRotation * Vector3.down) * (RampThickness * 0.5f)); // 경사면 아래로 두께 절반
            BoxCollider rampCollider = ramp.AddComponent<BoxCollider>(); // 충돌체
            rampCollider.size = new Vector3(StairWidth, RampThickness, Mathf.Sqrt((rise * rise) + (run * run))); // 크기
            float upperY = FloorStep - (t * 0.5f); // 위층 바닥 높이
            float landingLength = Length - RampEndZ; // 위층 착지 구간 길이
            DungeonModuleBaker.CreateBox(structure, "UpperFloor_Landing", new Vector3(Width * 0.5f, upperY, RampEndZ + (landingLength * 0.5f)), new Vector3(Width + (t * 2f), t, landingLength + t), materials.Floor); // 위층 착지 바닥 (계단 끝에서 위층 문까지)
            float sideWidth = (Width - StairWidth) * 0.5f; // 계단 옆 바닥 폭

            if (sideWidth > 0.2f) // 계단 옆에도 위층 바닥
            {
                DungeonModuleBaker.CreateBox(structure, "UpperFloor_SideA", new Vector3(sideWidth * 0.5f, upperY, RampStartZ * 0.5f + (RampEndZ * 0.5f)), new Vector3(sideWidth + t, t, run), materials.Floor); // 한쪽
                DungeonModuleBaker.CreateBox(structure, "UpperFloor_SideB", new Vector3(Width - (sideWidth * 0.5f), upperY, RampStartZ * 0.5f + (RampEndZ * 0.5f)), new Vector3(sideWidth + t, t, run), materials.Floor); // 반대쪽
            }

            DungeonModuleBaker.CreateBox(structure, "UpperFloor_Entry", new Vector3(Width * 0.5f, upperY, RampStartZ * 0.5f), new Vector3(Width + (t * 2f), t, RampStartZ + t), materials.Floor); // 계단 입구 위쪽 바닥
            DungeonModuleBaker.CreateBox(structure, "UpperRail_A", new Vector3(sideWidth - 0.1f, FloorStep + 0.5f, (RampStartZ + RampEndZ) * 0.5f), new Vector3(0.2f, 1f, run), materials.Wall); // 추락 방지 난간
            DungeonModuleBaker.CreateBox(structure, "UpperRail_B", new Vector3(Width - sideWidth + 0.1f, FloorStep + 0.5f, (RampStartZ + RampEndZ) * 0.5f), new Vector3(0.2f, 1f, run), materials.Wall); // 반대쪽 난간
            AddFloorLights(root.transform, Width, Length); // 층별 조명
            DungeonModule module = root.AddComponent<DungeonModule>(); // 모듈 부품
            module.Configure(shape.Id, shape.Role, shape.Weight, ToCellArray(shape), sockets, content, 2); // 구성 (두 층 차지)
            return root; // 반환
        }

        public static GameObject BuildLadderRoom(ModuleMaterials materials) // 사다리 방 모듈 (아래층 남쪽 문 → 위층 북쪽 문)
        {
            const int Width = 5; // 가로 칸
            const int Length = 7; // 세로 칸
            DungeonModuleShape shape = new DungeonModuleShape("Vertical_Ladder_5x7", DungeonModuleCatalog.VerticalFolder, ModuleRole.Vertical, 5,
                new[] { new ShapeRect(0, 0, Width, Length) },
                new[]
                {
                    new ShapeDoor(2, 0, GridDirection.South), // 아래층 출입구
                    new ShapeDoor(2, Length - 1, GridDirection.North, SocketKind.VerticalUp, 1), // 위층 출입구
                }); // 형태

            GameObject root = BuildShell(shape, materials, out Transform structure, out DungeonSocket[] sockets, out Transform content); // 껍데기
            float t = DungeonModuleBaker.WallThickness; // 두께
            float upperY = FloorStep - (t * 0.5f); // 위층 바닥 높이
            float holeMinZ = 1f; // 구멍 시작
            float holeMaxZ = holeMinZ + LadderHoleDepth; // 구멍 끝
            float centerX = Width * 0.5f; // 가로 중심
            float sideWidth = (Width - LadderHoleWidth) * 0.5f; // 구멍 옆 바닥 폭
            DungeonModuleBaker.CreateBox(structure, "UpperFloor_Back", new Vector3(centerX, upperY, holeMinZ * 0.5f), new Vector3(Width + (t * 2f), t, holeMinZ + t), materials.Floor); // 구멍 앞쪽 바닥
            DungeonModuleBaker.CreateBox(structure, "UpperFloor_Front", new Vector3(centerX, upperY, holeMaxZ + ((Length - holeMaxZ) * 0.5f)), new Vector3(Width + (t * 2f), t, (Length - holeMaxZ) + t), materials.Floor); // 구멍 뒤쪽 바닥

            if (sideWidth > 0.2f) // 구멍 옆 바닥
            {
                DungeonModuleBaker.CreateBox(structure, "UpperFloor_SideA", new Vector3(sideWidth * 0.5f, upperY, (holeMinZ + holeMaxZ) * 0.5f), new Vector3(sideWidth + t, t, LadderHoleDepth), materials.Floor); // 한쪽
                DungeonModuleBaker.CreateBox(structure, "UpperFloor_SideB", new Vector3(Width - (sideWidth * 0.5f), upperY, (holeMinZ + holeMaxZ) * 0.5f), new Vector3(sideWidth + t, t, LadderHoleDepth), materials.Floor); // 반대쪽
            }

            Material ladderMaterial = materials.Trim; // 사다리 재질
            Transform ladderRoot = DungeonModuleBaker.CreateChild(structure, "Ladder"); // 사다리 루트
            ladderRoot.localPosition = new Vector3(centerX, 0f, holeMinZ - 0.08f); // 구멍 앞쪽 벽면
            ladderRoot.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up); // 앞(+Z)이 방 안쪽
            float ladderHeight = FloorStep + 1f; // 사다리 길이 (위층 바닥보다 길게)
            DungeonModuleBaker.CreateBox(ladderRoot, "Rail_L", new Vector3(-0.35f, ladderHeight * 0.5f, 0.09f), new Vector3(0.1f, ladderHeight, 0.1f), ladderMaterial); // 왼쪽 기둥
            DungeonModuleBaker.CreateBox(ladderRoot, "Rail_R", new Vector3(0.35f, ladderHeight * 0.5f, 0.09f), new Vector3(0.1f, ladderHeight, 0.1f), ladderMaterial); // 오른쪽 기둥
            int rungs = Mathf.Max(2, Mathf.FloorToInt((ladderHeight - 0.3f) / 0.32f)); // 가로대 수

            for (int index = 0; index < rungs; index++) // 가로대
            {
                DungeonModuleBaker.CreateBox(ladderRoot, $"Rung_{index:00}", new Vector3(0f, 0.25f + (index * 0.32f), 0.09f), new Vector3(0.8f, 0.06f, 0.09f), ladderMaterial); // 한 칸
            }

            Transform bottomPoint = DungeonModuleBaker.CreateChild(ladderRoot, "ClimbBottom"); // 아래 끝
            bottomPoint.localPosition = new Vector3(0f, 0.05f, 0.68f); // 위치
            Transform topPoint = DungeonModuleBaker.CreateChild(ladderRoot, "ClimbTop"); // 위 끝
            topPoint.localPosition = new Vector3(0f, FloorStep + 0.15f, 0.68f); // 위치
            Transform exitTop = DungeonModuleBaker.CreateChild(ladderRoot, "ExitTop"); // 위에서 내려서는 위치
            exitTop.localPosition = new Vector3(0f, FloorStep + 0.1f, LadderHoleDepth + 0.9f); // 구멍 너머 바닥
            Transform exitBottom = DungeonModuleBaker.CreateChild(ladderRoot, "ExitBottom"); // 아래에서 내려서는 위치
            exitBottom.localPosition = new Vector3(0f, 0.1f, 1.1f); // 위치
            GameObject body = new GameObject("LadderBody"); // 상호작용 본체
            body.transform.SetParent(ladderRoot, false); // 사다리 아래
            body.transform.localPosition = new Vector3(0f, ladderHeight * 0.5f, 0.16f); // 사다리 면
            BoxCollider bodyCollider = body.AddComponent<BoxCollider>(); // 시선 상호작용용
            bodyCollider.size = new Vector3(0.9f, ladderHeight, 0.12f); // 크기
            DungeonLadder ladder = body.AddComponent<DungeonLadder>(); // 사다리 기능
            ladder.Configure(bottomPoint, topPoint, exitTop, exitBottom); // 기준점 연결
            AddFloorLights(root.transform, Width, Length); // 층별 조명
            DungeonModule module = root.AddComponent<DungeonModule>(); // 모듈 부품
            module.Configure(shape.Id, shape.Role, shape.Weight, ToCellArray(shape), sockets, content, 2); // 구성 (두 층 차지)
            return root; // 반환
        }

        private static GameObject BuildShell(DungeonModuleShape shape, ModuleMaterials materials, out Transform structure, out DungeonSocket[] sockets, out Transform content) // 두 층 높이의 바닥·천장·벽·출입구
        {
            HashSet<Vector2Int> cells = shape.BuildCells(); // 칸
            GameObject root = new GameObject(shape.Id); // 루트
            structure = DungeonModuleBaker.CreateChild(root.transform, "Structure"); // 구조물
            Transform socketRoot = DungeonModuleBaker.CreateChild(root.transform, "Sockets"); // 출입구
            content = DungeonModuleBaker.CreateChild(root.transform, "Content"); // 소품 기준
            float totalHeight = FloorStep + DungeonModuleBaker.RoomHeight; // 두 층 높이
            DungeonModuleBaker.BuildFloorsAndCeilings(structure, cells, materials, totalHeight); // 아래층 바닥 + 위층 천장
            DungeonModuleBaker.BuildWalls(structure, cells, shape, materials, totalHeight); // 두 층을 덮는 바깥 벽 (출입구는 각자 높이에 뚫림)
            sockets = DungeonModuleBaker.BuildSockets(socketRoot, shape, materials); // 출입구
            return root; // 반환
        }

        private static void AddFloorLights(Transform parent, int width, int length) // 아래층·위층 조명
        {
            Transform lightRoot = DungeonModuleBaker.CreateChild(parent, "Lights"); // 조명 루트

            for (int level = 0; level < 2; level++) // 두 층
            {
                GameObject lightObject = new GameObject($"RoomLight_{level:00}"); // 조명
                lightObject.transform.SetParent(lightRoot, false); // 조명 루트 아래
                lightObject.transform.localPosition = new Vector3(width * 0.5f, (level * FloorStep) + DungeonModuleBaker.RoomHeight - 0.6f, length * 0.5f); // 천장 아래
                Light light = lightObject.AddComponent<Light>(); // 점광원
                light.type = LightType.Point; // 점광원
                light.range = Mathf.Max(8f, length * 0.9f); // 범위
                light.intensity = 2.8f; // 밝기
                light.color = new Color(0.85f, 0.85f, 1f); // 세로형 방은 푸른빛
                light.shadows = LightShadows.None; // 그림자 없음
            }
        }

        private static Vector2Int[] ToCellArray(DungeonModuleShape shape) // 형태의 칸 배열
        {
            HashSet<Vector2Int> cells = shape.BuildCells(); // 칸
            Vector2Int[] array = new Vector2Int[cells.Count]; // 배열
            cells.CopyTo(array); // 복사
            return array; // 반환
        }
    }
}
