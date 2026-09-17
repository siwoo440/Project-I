using System.Collections.Generic; // 목록 사용
using ProjectI.Dungeon; // 모듈 부품 참조
using ProjectI.Generation; // 출입구 규격 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class DungeonVerticalModuleBaker // 층을 잇는 세로형 모듈(계단통·사다리 방·승강기)을 종류별 바리에이션으로 만듭니다
    {
        public const float FloorStep = DungeonModuleBaker.RoomHeight + (DungeonModuleBaker.WallThickness * 2f); // 한 층 높이 (4.50m)
        private const float StairStepHeight = 0.3f; // 계단 한 칸 높이
        private const float RampThickness = 0.4f; // 경사 충돌체 두께
        private const float CarClearance = 0.03f; // 승강기 판 아랫면을 층 바닥에서 띄우는 높이 (겹침 방지)
        private const float CarThickness = 0.1f; // 승강기 판 두께 (얇게 해서 올라서는 턱을 낮춤)
        private const float MinRun = 6f; // 계단 경사가 40도를 넘지 않게 하는 최소 수평 길이

        public static IEnumerable<GameObject> BuildAll(ModuleMaterials materials) // 세로형 모듈 전체 바리에이션
        {
            yield return BuildStairwell(materials, "Vertical_Stairwell_5x13", 5, 13, GridDirection.North); // 좁은 계단통 (남→북)
            yield return BuildStairwell(materials, "Vertical_Stairwell_7x13", 7, 13, GridDirection.North); // 넓은 계단통 (남→북)
            yield return BuildStairwell(materials, "Vertical_Stairwell_5x13_E", 5, 13, GridDirection.East); // 위층 문이 동쪽인 계단통
            yield return BuildLadderRoom(materials, "Vertical_Ladder_5x5_W", 5, 5, true, GridDirection.North); // 서쪽 벽 사다리 (남→북)
            yield return BuildLadderRoom(materials, "Vertical_Ladder_5x5_E", 5, 5, false, GridDirection.North); // 동쪽 벽 사다리 (남→북)
            yield return BuildLadderRoom(materials, "Vertical_Ladder_5x7_W", 5, 7, true, GridDirection.East); // 서쪽 벽 사다리 (남→동)
            yield return BuildElevator(materials, "Vertical_Elevator_7x7_3", 7, 3, new[] { GridDirection.South, GridDirection.East, GridDirection.North }); // 세 층 승강기
            yield return BuildElevator(materials, "Vertical_Elevator_7x7_2", 7, 2, new[] { GridDirection.South, GridDirection.North }); // 두 층 승강기
            yield return BuildElevator(materials, "Vertical_Elevator_9x9_3", 9, 3, new[] { GridDirection.South, GridDirection.West, GridDirection.North }); // 넓은 세 층 승강기
        }

        // ───────────────────────── 계단통 ─────────────────────────
        public static GameObject BuildStairwell(ModuleMaterials materials, string id, int width, int length, GridDirection upperWall) // 아래층 남쪽 문 → 계단 → 위층 지정 방향 문
        {
            float rampStartZ = 2f; // 계단 시작
            float rampEndZ = length - 4f; // 계단 끝 (위층 착지 구간을 남김)
            rampEndZ = rampEndZ - rampStartZ < MinRun ? rampStartZ + MinRun : rampEndZ; // 경사가 가팔라지지 않게 보정
            ShapeDoor upperDoor = upperWall == GridDirection.East
                ? new ShapeDoor(width - 1, length - 2, GridDirection.East, SocketKind.VerticalUp, 1) // 동쪽 위층 문
                : new ShapeDoor(width / 2, length - 1, GridDirection.North, SocketKind.VerticalUp, 1); // 북쪽 위층 문
            DungeonModuleShape shape = new DungeonModuleShape(id, DungeonModuleCatalog.VerticalFolder, ModuleRole.Vertical, 6,
                new[] { new ShapeRect(0, 0, width, length) },
                new[] { new ShapeDoor(width / 2, 0, GridDirection.South), upperDoor }); // 형태

            GameObject root = BuildShell(shape, materials, out Transform structure, out DungeonSocket[] sockets, out Transform content); // 껍데기
            float t = DungeonModuleBaker.WallThickness; // 두께
            float rise = FloorStep; // 올라갈 높이
            float run = rampEndZ - rampStartZ; // 수평 길이
            float centerX = width * 0.5f; // 가로 중심
            float stairWidth = width - 1.4f; // 계단 폭 (양옆 여유)
            Transform stairs = DungeonModuleBaker.CreateChild(structure, "Stairs"); // 계단 루트
            int steps = Mathf.Max(2, Mathf.CeilToInt(rise / StairStepHeight)); // 계단 수
            float stepRise = rise / steps; // 한 칸 높이
            float tread = run / steps; // 한 칸 깊이

            for (int index = 0; index < steps; index++) // 디딤판 (보이는 부분, 충돌체는 경사면 하나로 대신함)
            {
                float centerZ = rampStartZ + (tread * (index + 0.5f)); // 디딤판 중심
                float height = stepRise * (index + 1); // 바닥부터 높이
                GameObject step = DungeonModuleBaker.CreateBox(stairs, $"Step_{index:00}", new Vector3(centerX, height * 0.5f, centerZ), new Vector3(stairWidth, height, tread), materials.Floor); // 계단 한 칸
                Object.DestroyImmediate(step.GetComponent<Collider>()); // 충돌체 제거 (걸림 방지)
            }

            Quaternion rampRotation = Quaternion.LookRotation(new Vector3(0f, rise, run).normalized, Vector3.up); // 경사 회전
            GameObject ramp = new GameObject("StairRamp"); // 경사 충돌체 (보이지 않음)
            ramp.transform.SetParent(stairs, false); // 계단 아래
            ramp.transform.localRotation = rampRotation; // 회전
            ramp.transform.localPosition = new Vector3(centerX, rise * 0.5f, (rampStartZ + rampEndZ) * 0.5f) + ((rampRotation * Vector3.down) * (RampThickness * 0.5f)); // 경사면 아래로 두께 절반
            BoxCollider rampCollider = ramp.AddComponent<BoxCollider>(); // 충돌체
            rampCollider.size = new Vector3(stairWidth, RampThickness, Mathf.Sqrt((rise * rise) + (run * run))); // 크기
            float upperY = FloorStep - (t * 0.5f); // 위층 바닥 높이
            float landingLength = length - rampEndZ; // 위층 착지 구간
            DungeonModuleBaker.CreateBox(structure, "UpperFloor_Landing", new Vector3(centerX, upperY, rampEndZ + (landingLength * 0.5f)), new Vector3(width + (t * 2f), t, landingLength + t), materials.Floor); // 착지 바닥
            DungeonModuleBaker.CreateBox(structure, "UpperFloor_Entry", new Vector3(centerX, upperY, rampStartZ * 0.5f), new Vector3(width + (t * 2f), t, rampStartZ + t), materials.Floor); // 계단 입구 위쪽 바닥
            float sideWidth = (width - stairWidth) * 0.5f; // 계단 옆 폭
            DungeonModuleBaker.CreateBox(structure, "UpperRail_A", new Vector3(sideWidth * 0.5f, FloorStep + 0.5f, (rampStartZ + rampEndZ) * 0.5f), new Vector3(0.2f, 1f, run), materials.Wall); // 추락 방지 난간
            DungeonModuleBaker.CreateBox(structure, "UpperRail_B", new Vector3(width - (sideWidth * 0.5f), FloorStep + 0.5f, (rampStartZ + rampEndZ) * 0.5f), new Vector3(0.2f, 1f, run), materials.Wall); // 반대쪽 난간
            AddFloorLights(root.transform, width, length, 2); // 층별 조명
            DungeonModule module = root.AddComponent<DungeonModule>(); // 모듈 부품
            module.Configure(shape.Id, shape.Role, shape.Weight, ToCellArray(shape), sockets, content, 2); // 구성 (두 층 차지)
            return root; // 반환
        }

        // ───────────────────────── 사다리 방 ─────────────────────────
        public static GameObject BuildLadderRoom(ModuleMaterials materials, string id, int width, int length, bool ladderOnWest, GridDirection upperWall) // 사다리는 지정한 벽면을 따라, 문 앞은 비움
        {
            ShapeDoor upperDoor = upperWall == GridDirection.East
                ? new ShapeDoor(width - 1, length / 2, GridDirection.East, SocketKind.VerticalUp, 1) // 동쪽 위층 문
                : new ShapeDoor(width / 2, length - 1, GridDirection.North, SocketKind.VerticalUp, 1); // 북쪽 위층 문
            DungeonModuleShape shape = new DungeonModuleShape(id, DungeonModuleCatalog.VerticalFolder, ModuleRole.Vertical, 5,
                new[] { new ShapeRect(0, 0, width, length) },
                new[] { new ShapeDoor(width / 2, 0, GridDirection.South), upperDoor }); // 형태

            GameObject root = BuildShell(shape, materials, out Transform structure, out DungeonSocket[] sockets, out Transform content); // 껍데기
            float t = DungeonModuleBaker.WallThickness; // 두께
            float upperY = FloorStep - (t * 0.5f); // 위층 바닥 높이
            float holeWidth = 1.8f; // 구멍 폭 (벽면을 따라)
            float holeMinZ = 1.4f; // 구멍 시작 (남쪽 문 앞을 비움)
            float holeMaxZ = Mathf.Min(length - 1.4f, holeMinZ + 1.8f); // 구멍 끝 (반대쪽 문 앞을 비움)
            DungeonModuleBaker.CreateBox(structure, "UpperFloor_South", new Vector3(width * 0.5f, upperY, (holeMinZ - t) * 0.5f), new Vector3(width + (t * 2f), t, holeMinZ + t), materials.Floor); // 남쪽 바닥
            DungeonModuleBaker.CreateBox(structure, "UpperFloor_North", new Vector3(width * 0.5f, upperY, holeMaxZ + ((length - holeMaxZ) * 0.5f)), new Vector3(width + (t * 2f), t, (length - holeMaxZ) + t), materials.Floor); // 북쪽 바닥
            float sideWidth = width - holeWidth; // 구멍 옆 바닥 폭
            float sideCenterX = ladderOnWest ? holeWidth + (sideWidth * 0.5f) : sideWidth * 0.5f; // 구멍 옆 바닥 중심
            DungeonModuleBaker.CreateBox(structure, "UpperFloor_Side", new Vector3(sideCenterX, upperY, (holeMinZ + holeMaxZ) * 0.5f), new Vector3(sideWidth + t, t, holeMaxZ - holeMinZ), materials.Floor); // 구멍 옆 바닥
            Material ladderMaterial = materials.Trim; // 사다리 재질
            Transform ladderRoot = DungeonModuleBaker.CreateChild(structure, "Ladder"); // 사다리 루트
            ladderRoot.localPosition = new Vector3(ladderOnWest ? t + 0.02f : width - t - 0.02f, 0f, (holeMinZ + holeMaxZ) * 0.5f); // 벽의 안쪽 면 (벽은 경계에서 안쪽으로 세워짐)
            ladderRoot.localRotation = Quaternion.LookRotation(ladderOnWest ? Vector3.right : Vector3.left, Vector3.up); // 앞(+Z)이 방 안쪽
            float ladderHeight = FloorStep + 1f; // 사다리 길이
            DungeonModuleBaker.CreateBox(ladderRoot, "Rail_L", new Vector3(-0.32f, ladderHeight * 0.5f, 0.09f), new Vector3(0.1f, ladderHeight, 0.1f), ladderMaterial); // 왼쪽 기둥
            DungeonModuleBaker.CreateBox(ladderRoot, "Rail_R", new Vector3(0.32f, ladderHeight * 0.5f, 0.09f), new Vector3(0.1f, ladderHeight, 0.1f), ladderMaterial); // 오른쪽 기둥
            int rungs = Mathf.Max(2, Mathf.FloorToInt((ladderHeight - 0.3f) / 0.32f)); // 가로대 수

            for (int index = 0; index < rungs; index++) // 가로대
            {
                DungeonModuleBaker.CreateBox(ladderRoot, $"Rung_{index:00}", new Vector3(0f, 0.25f + (index * 0.32f), 0.09f), new Vector3(0.74f, 0.06f, 0.09f), ladderMaterial); // 한 칸
            }

            Transform bottomPoint = DungeonModuleBaker.CreateChild(ladderRoot, "ClimbBottom"); // 아래 끝
            bottomPoint.localPosition = new Vector3(0f, 0.05f, 0.62f); // 위치
            Transform topPoint = DungeonModuleBaker.CreateChild(ladderRoot, "ClimbTop"); // 위 끝
            topPoint.localPosition = new Vector3(0f, FloorStep + 0.15f, 0.62f); // 위치
            Transform exitTop = DungeonModuleBaker.CreateChild(ladderRoot, "ExitTop"); // 위에서 내려서는 위치
            exitTop.localPosition = new Vector3(0f, FloorStep + 0.1f, holeWidth + 0.6f); // 구멍 너머 바닥
            Transform exitBottom = DungeonModuleBaker.CreateChild(ladderRoot, "ExitBottom"); // 아래에서 내려서는 위치
            exitBottom.localPosition = new Vector3(0f, 0.1f, 1.1f); // 위치
            GameObject body = new GameObject("LadderBody"); // 상호작용 본체
            body.transform.SetParent(ladderRoot, false); // 사다리 아래
            body.transform.localPosition = new Vector3(0f, ladderHeight * 0.5f, 0.16f); // 사다리 면
            BoxCollider bodyCollider = body.AddComponent<BoxCollider>(); // 시선 상호작용용
            bodyCollider.size = new Vector3(0.84f, ladderHeight, 0.12f); // 크기
            DungeonLadder ladder = body.AddComponent<DungeonLadder>(); // 사다리 기능
            ladder.Configure(bottomPoint, topPoint, exitTop, exitBottom); // 기준점 연결
            AddFloorLights(root.transform, width, length, 2); // 층별 조명
            DungeonModule module = root.AddComponent<DungeonModule>(); // 모듈 부품
            module.Configure(shape.Id, shape.Role, shape.Weight, ToCellArray(shape), sockets, content, 2); // 구성 (두 층 차지)
            return root; // 반환
        }

        // ───────────────────────── 전력 승강기 ─────────────────────────
        public static GameObject BuildElevator(ModuleMaterials materials, string id, int size, int stops, GridDirection[] doorWalls) // 직육면체 승강로를 여러 층 오가며, 판 안과 각 층 문 옆 버튼으로 호출
        {
            int shaftMin = (size - 3) / 2; // 승강로 시작 칸
            int shaftMax = shaftMin + 3; // 승강로 끝 칸 (미포함)
            List<ShapeDoor> doors = new List<ShapeDoor>(); // 출입구

            for (int index = 0; index < stops; index++) // 층마다 문 하나
            {
                SocketKind kind = index == 0 ? SocketKind.Door : SocketKind.VerticalUp; // 아래층만 일반 출입구
                doors.Add(DoorOnWall(doorWalls[index], size, kind, index)); // 등록
            }

            DungeonModuleShape shape = new DungeonModuleShape(id, DungeonModuleCatalog.VerticalFolder, ModuleRole.Vertical, 5,
                new[] { new ShapeRect(0, 0, size, size) }, doors.ToArray()); // 형태

            GameObject root = BuildShell(shape, materials, out Transform structure, out DungeonSocket[] sockets, out Transform content, (stops - 1) * FloorStep); // 층 높이 껍데기
            float t = DungeonModuleBaker.WallThickness; // 두께
            float shaftCenter = (shaftMin + shaftMax) * 0.5f; // 승강로 중심
            float shaftSize = shaftMax - shaftMin; // 승강로 한 변
            Transform landings = DungeonModuleBaker.CreateChild(structure, "Landings"); // 층별 착지 바닥

            for (int index = 1; index < stops; index++) // 위층마다 승강로만 뚫린 바닥
            {
                float y = (index * FloorStep) - (t * 0.5f); // 층 바닥 높이
                DungeonModuleBaker.CreateBox(landings, $"Landing_{index}_S", new Vector3(size * 0.5f, y, (shaftMin - t) * 0.5f), new Vector3(size + (t * 2f), t, shaftMin + t), materials.Floor); // 남쪽 띠
                DungeonModuleBaker.CreateBox(landings, $"Landing_{index}_N", new Vector3(size * 0.5f, y, shaftMax + ((size - shaftMax) * 0.5f)), new Vector3(size + (t * 2f), t, (size - shaftMax) + t), materials.Floor); // 북쪽 띠
                DungeonModuleBaker.CreateBox(landings, $"Landing_{index}_W", new Vector3((shaftMin - t) * 0.5f, y, shaftCenter), new Vector3(shaftMin + t, t, shaftSize), materials.Floor); // 서쪽 띠
                DungeonModuleBaker.CreateBox(landings, $"Landing_{index}_E", new Vector3(shaftMax + ((size - shaftMax) * 0.5f), y, shaftCenter), new Vector3((size - shaftMax) + t, t, shaftSize), materials.Floor); // 동쪽 띠
            }

            float carSize = shaftSize - 0.3f; // 판 크기 (승강로 벽에 닿지 않게)
            Transform car = DungeonModuleBaker.CreateChild(structure, "ElevatorCar"); // 승강기 판
            car.localPosition = new Vector3(shaftCenter, CarClearance, shaftCenter); // 승강로 중앙, 바닥에서 살짝 띄움
            DungeonModuleBaker.CreateBox(car, "Platform", new Vector3(0f, CarThickness * 0.5f, 0f), new Vector3(carSize, CarThickness, carSize), materials.Trim); // 바닥판 (판 기준점 위로만 두께를 둠 — 층 바닥 속으로 내려가지 않게)
            Transform post = DungeonModuleBaker.CreateChild(car, "ButtonPost"); // 버튼 기둥
            post.localPosition = new Vector3(-carSize * 0.5f + 0.3f, 0f, -carSize * 0.5f + 0.3f); // 판 모서리
            DungeonModuleBaker.CreateBox(post, "Post", new Vector3(0f, 0.7f, 0f), new Vector3(0.2f, 1.4f, 0.2f), materials.Accent); // 기둥
            DungeonElevator elevator = car.gameObject.AddComponent<DungeonElevator>(); // 승강기 기능
            float[] stopHeights = new float[stops]; // 층별 정지 높이

            for (int index = 0; index < stops; index++) // 판 안쪽 층 버튼
            {
                stopHeights[index] = (index * FloorStep) + CarClearance; // 정지 높이
                GameObject button = DungeonModuleBaker.CreateBox(post, $"Button_{index}", new Vector3(0.16f, 1.45f - (index * 0.32f), 0.16f), new Vector3(0.26f, 0.26f, 0.26f), materials.Accent); // 버튼 (누르기 쉽게 크게)
                button.AddComponent<DungeonElevatorButton>().Configure(elevator, index); // 버튼 기능
            }

            elevator.Configure(car, stopHeights, new Vector3(0f, 1.1f, 0f), new Vector3(carSize, 2.4f, carSize)); // 구성
            Transform callRoot = DungeonModuleBaker.CreateChild(structure, "CallButtons"); // 층별 호출 버튼

            for (int index = 0; index < stops; index++) // 각 층 문 옆 호출 버튼 (판이 다른 층에 있어도 부를 수 있게)
            {
                ShapeDoor door = doors[index]; // 그 층 문
                Vector3 inward = -DungeonModuleShape.Outward(door.Facing); // 방 안쪽 방향
                Vector3 along = new Vector3(-inward.z, 0f, inward.x); // 문 옆 방향
                Vector3 spot = door.CenterLocal + (Vector3.up * door.BaseY) + (inward * 0.4f) + (along * ((ModuleDoorway.Width * 0.5f) + 0.4f)); // 문 옆 벽 앞
                Transform callPost = DungeonModuleBaker.CreateChild(callRoot, $"CallPost_{index}"); // 호출대
                callPost.localPosition = spot; // 위치
                DungeonModuleBaker.CreateBox(callPost, "Plate", new Vector3(0f, 1.2f, 0f), new Vector3(0.3f, 0.5f, 0.3f), materials.Wall); // 판
                GameObject callButton = DungeonModuleBaker.CreateBox(callPost, "CallButton", new Vector3(0f, 1.35f, 0f), new Vector3(0.34f, 0.34f, 0.34f), materials.Accent); // 호출 버튼
                callButton.AddComponent<DungeonElevatorButton>().Configure(elevator, index); // 그 층으로 호출
            }

            AddFloorLights(root.transform, size, size, stops); // 층별 조명
            DungeonModule module = root.AddComponent<DungeonModule>(); // 모듈 부품
            module.Configure(shape.Id, shape.Role, shape.Weight, ToCellArray(shape), sockets, content, stops); // 구성 (층 수만큼 차지)
            return root; // 반환
        }

        private static ShapeDoor DoorOnWall(GridDirection wall, int size, SocketKind kind, int floorOffset) // 정사각 모듈의 지정한 벽 한가운데 출입구
        {
            int mid = size / 2; // 가운데 칸

            switch (wall) // 방향별
            {
                case GridDirection.North: return new ShapeDoor(mid, size - 1, GridDirection.North, kind, floorOffset); // 북
                case GridDirection.East: return new ShapeDoor(size - 1, mid, GridDirection.East, kind, floorOffset); // 동
                case GridDirection.West: return new ShapeDoor(0, mid, GridDirection.West, kind, floorOffset); // 서
                default: return new ShapeDoor(mid, 0, GridDirection.South, kind, floorOffset); // 남
            }
        }

        private static GameObject BuildShell(DungeonModuleShape shape, ModuleMaterials materials, out Transform structure, out DungeonSocket[] sockets, out Transform content) // 두 층 높이의 껍데기
        {
            return BuildShell(shape, materials, out structure, out sockets, out content, FloorStep); // 기본은 한 층 위까지
        }

        private static GameObject BuildShell(DungeonModuleShape shape, ModuleMaterials materials, out Transform structure, out DungeonSocket[] sockets, out Transform content, float topFloorY) // 지정한 층 높이까지 덮는 껍데기
        {
            HashSet<Vector2Int> cells = shape.BuildCells(); // 칸
            GameObject root = new GameObject(shape.Id); // 루트
            structure = DungeonModuleBaker.CreateChild(root.transform, "Structure"); // 구조물
            Transform socketRoot = DungeonModuleBaker.CreateChild(root.transform, "Sockets"); // 출입구
            content = DungeonModuleBaker.CreateChild(root.transform, "Content"); // 소품 기준
            float totalHeight = topFloorY + DungeonModuleBaker.RoomHeight; // 전체 높이
            DungeonModuleBaker.BuildFloorsAndCeilings(structure, cells, materials, totalHeight); // 아래층 바닥 + 최상층 천장
            DungeonModuleBaker.BuildWalls(structure, cells, shape, materials, totalHeight); // 층을 덮는 바깥 벽 (출입구는 각자 높이에 뚫림)
            sockets = DungeonModuleBaker.BuildSockets(socketRoot, shape, materials); // 출입구
            return root; // 반환
        }

        private static void AddFloorLights(Transform parent, int width, int length, int levels) // 층마다 조명
        {
            Transform lightRoot = DungeonModuleBaker.CreateChild(parent, "Lights"); // 조명 루트

            for (int level = 0; level < levels; level++) // 층 순회
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
