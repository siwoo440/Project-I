using System.Collections.Generic; // 목록
using System.Linq; // 조회
using System.Text; // 보고
using ProjectI.EditorTools.Town; // 마을 제작 도구
using ProjectI.Scenes; // 메뉴 제어기·카메라
using UnityEditor; // 에디터 기능
using UnityEditor.SceneManagement; // 씬 열기·저장
using UnityEngine; // 유니티 기본 기능
using UnityEngine.EventSystems; // UI 입력
using UnityEngine.InputSystem.UI; // 새 Input System UI 모듈
using UnityEngine.Rendering; // 환경광
using UnityEngine.SceneManagement; // 씬

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    public static class Phase14Day35MainMenuSetup // 메인 메뉴 씬 구성 (밤거리 배경 · 카메라 자세 2개 · 메뉴 제어기) — 35일차
    {
        private const string MainMenuScenePath = "Assets/ProjectI/Scenes/MainMenu.unity"; // 메인 메뉴 씬
        private const string BootScenePath = "Assets/ProjectI/Scenes/Boot.unity"; // 부트 씬
        private const float SidewalkTop = 0.15f; // 보도 높이
        private const float RoadTop = 0.02f; // 도로 높이
        private const float FrontZ = 5f; // 북쪽 건물 앞면 z
        private const float Depth = 10f; // 건물 깊이
        private static readonly Vector3 MainCameraPosition = new Vector3(-15.5f, 1.9f, -4.4f); // 거리 전경 위치
        private static readonly Vector3 MainCameraTarget = new Vector3(0f, 5.2f, 8f); // 거리 전경 시선
        private static readonly Vector3 ServerCameraPosition = new Vector3(0f, 2.1f, -1.6f); // 전신국 앞 위치
        private static readonly Vector3 ServerCameraTarget = new Vector3(0f, 2.9f, FrontZ); // 전신국 진열창 시선

        [MenuItem("Project I/Day 35/Build Main Menu Scene")] // 메뉴
        public static void Setup() // 구성
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) // 저장 안 한 변경 확인
            {
                return; // 취소
            }

            TownKit.ResetCache(); // 재질 캐시
            TownPalette.ResetCache(); // 음영 캐시
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single); // 메인 메뉴 씬

            foreach (GameObject root in scene.GetRootGameObjects()) // 이전 내용 (1일차 임시 메뉴 포함)
            {
                Object.DestroyImmediate(root); // 제거
            }

            Transform street = new GameObject("===Menu Street===").transform; // 배경 거리
            SceneManager.MoveGameObjectToScene(street.gameObject, scene); // 씬 소속
            BuildGround(street); // 지면·도로·보도
            Dictionary<string, TownBuildingResult> buildings = BuildBuildings(street); // 건물
            BuildProps(street); // 거리 소품
            BuildSkyline(street); // 뒤쪽 공장·굴뚝
            MarkStatic(street); // 정적 배칭

            Light moon = BuildLighting(scene); // 달빛·안개
            MenuCameraRig rig = BuildCameraRig(scene); // 카메라
            BuildEventSystem(scene); // UI 입력

            GameObject menu = new GameObject("===Main Menu==="); // 메뉴 제어기
            SceneManager.MoveGameObjectToScene(menu, scene); // 씬 소속
            MainMenuSceneController controller = menu.AddComponent<MainMenuSceneController>(); // 제어기
            controller.Configure(rig); // 카메라 연결

            EnsureBuildOrder(); // Build 씬 순서
            EditorSceneManager.MarkSceneDirty(scene); // 변경 표시
            EditorSceneManager.SaveScene(scene); // 저장
            AssetDatabase.SaveAssets(); // 재질 저장
            Debug.Log(Report(street, buildings, moon, rig)); // 보고
        }

        private static void BuildGround(Transform street) // 지면·돌길 도로·판석 보도
        {
            Transform group = TownKit.Group(street, "Ground", Vector3.zero); // 묶음
            TownKit.Box(group, "Earth", new Vector3(0f, -0.25f, 20f), new Vector3(160f, 0.5f, 120f), TownPalette.Ground); // 흙 지면
            TownKit.Box(group, "Road", new Vector3(0f, RoadTop * 0.5f, 0f), new Vector3(90f, RoadTop, 6f), TownPalette.Setts); // 돌길 (z -3~3)

            for (float x = -44.4f; x < 45f; x += 0.6f) // 돌 줄눈 (도로 가로 방향)
            {
                TownKit.Box(group, "SettSeam", new Vector3(x, RoadTop + 0.002f, 0f), new Vector3(0.035f, 0.004f, 5.8f), TownPalette.RoadSeam, false); // 줄눈
            }

            for (float z = -2.5f; z <= 2.5f; z += 1f) // 돌 줄눈 (도로 길이 방향)
            {
                TownKit.Box(group, "SettSeamLong", new Vector3(0f, RoadTop + 0.002f, z), new Vector3(90f, 0.004f, 0.03f), TownPalette.RoadSeam, false); // 줄눈
            }

            for (int side = -1; side <= 1; side += 2) // 배수로·경계석
            {
                TownKit.Box(group, "Gutter", new Vector3(0f, RoadTop + 0.003f, side * 2.8f), new Vector3(90f, 0.006f, 0.4f), TownPalette.Puddle, false); // 젖은 배수로
                TownKit.Box(group, "Curb", new Vector3(0f, (SidewalkTop + 0.02f) * 0.5f, side * 3.1f), new Vector3(90f, SidewalkTop + 0.02f, 0.22f), TownPalette.DressingSoot); // 경계석
            }

            TownKit.Box(group, "Sidewalk_N", new Vector3(0f, SidewalkTop * 0.5f, 10f), new Vector3(90f, SidewalkTop, 13.8f), TownPalette.Flagstone); // 북쪽 보도 (건물 아래까지)
            TownKit.Box(group, "Sidewalk_S", new Vector3(0f, SidewalkTop * 0.5f, -5.1f), new Vector3(90f, SidewalkTop, 3.8f), TownPalette.Flagstone); // 남쪽 보도

            for (float x = -44.1f; x < 45f; x += 0.9f) // 판석 줄눈
            {
                TownKit.Box(group, "FlagSeam_N", new Vector3(x, SidewalkTop + 0.002f, 4.1f), new Vector3(0.03f, 0.004f, 1.8f), TownPalette.RoadSeam, false); // 북쪽
                TownKit.Box(group, "FlagSeam_S", new Vector3(x, SidewalkTop + 0.002f, -5.1f), new Vector3(0.03f, 0.004f, 3.8f), TownPalette.RoadSeam, false); // 남쪽
            }

            TownProps.Puddle(group, new Vector3(-6.5f, RoadTop + 0.004f, -1.1f), 1.6f); // 웅덩이
            TownProps.Puddle(group, new Vector3(3.2f, RoadTop + 0.004f, 1.4f), 1.1f); // 웅덩이
            TownProps.Puddle(group, new Vector3(-11f, RoadTop + 0.004f, 0.6f), 0.9f); // 웅덩이
        }

        private static Dictionary<string, TownBuildingResult> BuildBuildings(Transform street) // 북쪽 건물 3채 (가운데 전신국 = 서버 화면)
        {
            Transform group = TownKit.Group(street, "Buildings", Vector3.zero); // 묶음
            Dictionary<string, TownBuildingResult> result = new Dictionary<string, TownBuildingResult>(); // 결과
            float z = FrontZ + (Depth * 0.5f); // 건물 중심 z (앞면 = -z 방향)

            TownBuildingStyle shop = new TownBuildingStyle { Brick = TownPalette.VictorianBrick, Dressing = TownPalette.Dressing, Paint = TownPalette.PaintGreen, DoorPaint = TownPalette.PaintGreen, AwningA = TownPalette.AwningGreen, AwningB = TownPalette.AwningCream, Wallpaper = TownPalette.WallpaperGreen, Facade = TownFacade.Shopfront, Storeys = 3, Dormers = 2, LitSeed = 4 }; // 잡화점
            TownBuildingStyle telegraph = new TownBuildingStyle { Brick = TownPalette.StockBrick, Dressing = TownPalette.Dressing, Paint = TownPalette.PaintNavy, DoorPaint = TownPalette.PaintNavy, Wallpaper = TownPalette.Wallpaper, Facade = TownFacade.Shopfront, Storeys = 3, Dormers = 1, RoofPitch = 44f, LitSeed = 9 }; // 전신국 (차양 없음: 진열창이 잘 보이게)
            TownBuildingStyle warehouse = new TownBuildingStyle { Brick = TownPalette.SootBrick, Dressing = TownPalette.DressingSoot, Paint = TownPalette.PaintBlack, DoorPaint = TownPalette.PaintOxblood, Wallpaper = TownPalette.Wallpaper, Facade = TownFacade.Warehouse, Storeys = 3, Dormers = 0, RoofPitch = 34f, LitSeed = 6 }; // 화물 창고

            result["Menu_Shop"] = TownBuilding.Build(group, "Menu_Shop", new Vector3(-13f, SidewalkTop, z), 180f, 12f, Depth, shop, "잡화점"); // 왼쪽
            result["Menu_Telegraph"] = TownBuilding.Build(group, "Menu_Telegraph", new Vector3(0f, SidewalkTop, z), 180f, 12f, Depth, telegraph, "전신국"); // 가운데
            result["Menu_Warehouse"] = TownBuilding.Build(group, "Menu_Warehouse", new Vector3(13f, SidewalkTop, z), 180f, 12f, Depth, warehouse, "화물 운송"); // 오른쪽
            TownBuilding.GhostSign(result["Menu_Shop"], true, "석탄 · 등유 · 성냥", 8.4f, 0.55f); // 옆벽 광고

            TownBuildingResult office = result["Menu_Telegraph"]; // 전신국 실내
            TownProps.Table(office.Interior, new Vector3(0f, 0f, office.InnerBackZ + 1.2f), new Vector3(3.2f, 0.95f, 0.8f), 0f); // 접수대
            TownProps.WallMap(office.Interior, new Vector3(-2.6f, 1.9f, office.InnerBackZ + 0.02f), 0f); // 노선 지도
            TownProps.Cabinet(office.Interior, new Vector3(3.4f, 0f, office.InnerBackZ + 0.35f), 0f); // 서류함
            TownProps.Bookshelf(office.Interior, new Vector3(-3.6f, 0f, office.InnerBackZ + 0.3f), 0f, 1.6f); // 책장
            TownProps.Candle(office.Interior, new Vector3(1.45f, 0.95f, office.InnerBackZ + 1.2f)); // 촛대
            TownKit.Label(office.Interior, "Board", "원정대 연결", new Vector3(0.6f, 2.6f, office.InnerBackZ + 0.03f), 180f, 0.32f, new Color(0.93f, 0.78f, 0.38f)); // 뒷벽 글씨 (밖에서 읽힘)

            for (int index = 0; index < 5; index++) // 전신 단말기 (접수대 위)
            {
                Vector3 position = new Vector3(-1.2f + (index * 0.6f), 1.05f, office.InnerBackZ + 1.2f); // 위치
                TownKit.Box(office.Interior, "Telegraph", position, new Vector3(0.36f, 0.14f, 0.26f), TownPalette.Brass, false); // 단말기
                TownKit.Box(office.Interior, "TelegraphKey", position + new Vector3(0f, 0.09f, 0.06f), new Vector3(0.06f, 0.04f, 0.14f), TownPalette.CastIron, false); // 전건
            }

            return result; // 반환
        }

        private static void BuildProps(Transform street) // 거리 소품
        {
            Transform group = TownKit.Group(street, "Props", Vector3.zero); // 묶음
            float[] northLamps = { -19.5f, -6.5f, 6.5f, 19.5f }; // 북쪽 가로등 x

            foreach (float x in northLamps) // 북쪽 가로등
            {
                TownProps.StreetLamp(group, "Lamp_N", new Vector3(x, SidewalkTop, 3.55f), 0f); // 가로등
            }

            TownProps.StreetLamp(group, "Lamp_S", new Vector3(-3f, SidewalkTop, -4.2f), 0f); // 남쪽 가로등
            TownProps.StreetLamp(group, "Lamp_S", new Vector3(12f, SidewalkTop, -4.2f), 0f); // 남쪽 가로등
            TownProps.PillarBox(group, new Vector3(4.6f, SidewalkTop, 3.7f), 180f); // 우체통 (전신국 옆)
            TownProps.NoticeBoard(group, new Vector3(-4.2f, SidewalkTop, 4.2f), 180f, "원정대 모집"); // 게시판
            TownProps.Bench(group, new Vector3(-11f, SidewalkTop, 3.9f), 180f); // 의자
            TownProps.Barrel(group, new Vector3(9.4f, SidewalkTop, 4.1f)); // 통
            TownProps.Barrel(group, new Vector3(10.2f, SidewalkTop, 4.3f), 0.8f); // 통
            TownProps.CrateStack(group, new Vector3(16.6f, SidewalkTop, 4f), 180f); // 상자
            TownProps.Sack(group, new Vector3(15.2f, SidewalkTop, 3.8f)); // 자루
            TownProps.WaterTrough(group, new Vector3(-17f, SidewalkTop, 3.7f), 0f); // 물통
            TownProps.HitchingPost(group, new Vector3(-15f, SidewalkTop, 3.5f), 0f); // 말 묶는 기둥

            for (float x = -1.6f; x <= 1.6f; x += 1.6f) // 전신국 앞 말뚝
            {
                TownProps.Bollard(group, new Vector3(x, SidewalkTop, 3.4f)); // 말뚝
            }

            TownProps.BrickWallRun(group, "SouthWall", new Vector3(-45f, SidewalkTop, -7f), new Vector3(45f, SidewalkTop, -7f), 1.6f, TownPalette.SootBrick); // 남쪽 담 (카메라 뒤)
            TownProps.IronRailing(group, "Railing_W", new Vector3(-6.95f, SidewalkTop, 4.9f), new Vector3(-6.05f, SidewalkTop, 4.9f), 1.1f); // 건물 사이 난간
            TownProps.IronRailing(group, "Railing_E", new Vector3(6.05f, SidewalkTop, 4.9f), new Vector3(6.95f, SidewalkTop, 4.9f), 1.1f); // 건물 사이 난간
        }

        private static void BuildSkyline(Transform street) // 지붕 너머 공장 풍경
        {
            Transform group = TownKit.Group(street, "Skyline", Vector3.zero); // 묶음
            TownProps.FactoryHall(group, new Vector3(-4f, 0f, 36f), 0f, 34f, 14f, 13f); // 제철소
            TownProps.Smokestack(group, new Vector3(-20f, 0f, 30f), 30f); // 굴뚝
            TownProps.Smokestack(group, new Vector3(9f, 0f, 44f), 36f); // 굴뚝
            TownProps.Smokestack(group, new Vector3(27f, 0f, 28f), 26f); // 굴뚝
            float[] heights = { 16f, 19f, 14f, 21f, 17f, 15f, 20f }; // 뒤 건물 높이

            for (int index = 0; index < heights.Length; index++) // 뒤 연립 건물 윤곽
            {
                float x = -36f + (index * 12f); // 위치
                Material brick = index % 2 == 0 ? TownPalette.SootBrick : TownPalette.EngineeringBrick; // 벽돌
                TownKit.Box(group, "BackRow", new Vector3(x, heights[index] * 0.5f, 22f), new Vector3(11.6f, heights[index], 8f), brick, false); // 몸체
                TownKit.Box(group, "BackRowCornice", new Vector3(x, heights[index] + 0.15f, 17.9f), new Vector3(11.8f, 0.3f, 0.3f), TownPalette.DressingSoot, false); // 처마

                for (int stack = -1; stack <= 1; stack += 2) // 굴뚝
                {
                    TownKit.Box(group, "BackStack", new Vector3(x + (stack * 4.5f), heights[index] + 1f, 22f), new Vector3(0.8f, 2f, 1.4f), TownPalette.SootBrick, false); // 굴뚝
                }

                for (int column = -2; column <= 2; column++) // 불 켜진 창 몇 개
                {
                    if ((index + column) % 3 != 0) // 일부만
                    {
                        continue; // 생략
                    }

                    float y = 6f + ((index + column + 5) % 3 * 3.4f); // 층
                    TownKit.Box(group, "BackWindow", new Vector3(x + (column * 2.2f), y, 17.97f), new Vector3(1f, 1.6f, 0.05f), TownPalette.WindowLit, false); // 창
                }
            }
        }

        private static void MarkStatic(Transform street) // 정적 배칭 (글자 제외)
        {
            foreach (Renderer renderer in street.GetComponentsInChildren<Renderer>(true)) // 렌더러
            {
                if (renderer is MeshRenderer && renderer.GetComponent<TextMesh>() == null) // 글자 제외
                {
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic); // 정적
                }
            }
        }

        private static Light BuildLighting(Scene scene) // 달빛·환경광·안개
        {
            GameObject moonObject = new GameObject("Moonlight"); // 달빛
            SceneManager.MoveGameObjectToScene(moonObject, scene); // 씬 소속
            moonObject.transform.rotation = Quaternion.Euler(38f, 140f, 0f); // 뒤쪽 위에서
            Light moon = moonObject.AddComponent<Light>(); // 조명
            moon.type = LightType.Directional; // 방향광
            moon.color = new Color(0.55f, 0.63f, 0.82f); // 푸른 달빛
            moon.intensity = 0.28f; // 약하게
            moon.shadows = LightShadows.Soft; // 그림자

            SceneManager.SetActiveScene(scene); // 환경 설정 대상
            RenderSettings.skybox = null; // 하늘 없음 (카메라 단색)
            RenderSettings.ambientMode = AmbientMode.Flat; // 단색 환경광
            RenderSettings.ambientLight = new Color(0.09f, 0.09f, 0.12f); // 어두운 밤
            RenderSettings.fog = true; // 안개 (매연)
            RenderSettings.fogMode = FogMode.Exponential; // 지수
            RenderSettings.fogDensity = 0.022f; // 농도
            RenderSettings.fogColor = new Color(0.08f, 0.075f, 0.085f); // 매연 색
            RenderSettings.sun = moon; // 주 광원
            return moon; // 반환
        }

        private static MenuCameraRig BuildCameraRig(Scene scene) // 카메라와 자세 2개
        {
            GameObject rigObject = new GameObject("MenuCameraRig"); // 묶음
            SceneManager.MoveGameObjectToScene(rigObject, scene); // 씬 소속
            MenuCameraRig rig = rigObject.AddComponent<MenuCameraRig>(); // 이동기

            GameObject cameraObject = new GameObject("MenuCamera"); // 카메라
            cameraObject.transform.SetParent(rigObject.transform, false); // 부모
            cameraObject.tag = "MainCamera"; // 주 카메라
            Camera camera = cameraObject.AddComponent<Camera>(); // 카메라
            camera.clearFlags = CameraClearFlags.SolidColor; // 단색 배경
            camera.backgroundColor = new Color(0.08f, 0.075f, 0.085f); // 안개와 같은 색
            camera.fieldOfView = 55f; // 화각
            camera.nearClipPlane = 0.1f; // 가까운 면
            camera.farClipPlane = 250f; // 먼 면
            cameraObject.AddComponent<AudioListener>(); // 소리 (이후 메뉴 음악)

            Transform main = Pose(rigObject.transform, "Pose_Main", MainCameraPosition, MainCameraTarget); // 거리 전경
            Transform servers = Pose(rigObject.transform, "Pose_Servers", ServerCameraPosition, ServerCameraTarget); // 전신국 앞
            cameraObject.transform.SetPositionAndRotation(main.position, main.rotation); // 시작 위치
            rig.Configure(camera, main, servers); // 연결
            return rig; // 반환
        }

        private static Transform Pose(Transform parent, string name, Vector3 position, Vector3 target) // 카메라 자세
        {
            GameObject pose = new GameObject(name); // 자세
            pose.transform.SetParent(parent, false); // 부모
            pose.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up)); // 위치·방향
            return pose.transform; // 반환
        }

        private static void BuildEventSystem(Scene scene) // UI 입력 (새 Input System)
        {
            GameObject system = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); // 생성 (기본 UI 입력은 켜질 때 자동 연결)
            SceneManager.MoveGameObjectToScene(system, scene); // 씬 소속
        }

        private static void EnsureBuildOrder() // Boot → MainMenu → 게임 씬 순서 확인
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList(); // 현재 목록
            string[] head = { BootScenePath, MainMenuScenePath }; // 앞 두 칸
            List<EditorBuildSettingsScene> ordered = new List<EditorBuildSettingsScene>(); // 결과

            foreach (string path in head) // 앞 두 칸
            {
                EditorBuildSettingsScene existing = scenes.FirstOrDefault(item => item.path == path); // 기존
                ordered.Add(existing ?? new EditorBuildSettingsScene(path, true)); // 추가
                ordered[ordered.Count - 1].enabled = true; // 켬
            }

            ordered.AddRange(scenes.Where(item => !head.Contains(item.path))); // 나머지 순서 유지

            if (!ordered.Select(item => item.path).SequenceEqual(scenes.Select(item => item.path)) || scenes.Take(2).Any(item => !item.enabled)) // 바뀜
            {
                EditorBuildSettings.scenes = ordered.ToArray(); // 저장
                Debug.Log("[Project I] Build 씬 순서를 Boot → MainMenu → 게임 씬으로 맞췄습니다."); // 기록
            }
        }

        private static string Report(Transform street, Dictionary<string, TownBuildingResult> buildings, Light moon, MenuCameraRig rig) // 완료 보고
        {
            Physics.SyncTransforms(); // 검사 준비
            StringBuilder builder = new StringBuilder("[Project I] 35일차 메인 메뉴 씬 구성 완료"); // 보고
            builder.AppendLine(); // 줄
            builder.AppendLine($"건물 {buildings.Count}채 · 조명 {street.GetComponentsInChildren<Light>(true).Length + 1}개 · 렌더러 {street.GetComponentsInChildren<Renderer>(true).Length}개"); // 규모
            builder.AppendLine($"카메라 전경 {MainCameraPosition} → 서버 {ServerCameraPosition}"); // 카메라
            AppendLineOfSight(builder, "전경 시선", MainCameraPosition, MainCameraTarget); // 가림 확인
            AppendLineOfSight(builder, "서버 시선", ServerCameraPosition, ServerCameraTarget); // 가림 확인
            builder.Append($"Build 씬: {string.Join(" → ", EditorBuildSettings.scenes.Where(item => item.enabled).Select(item => System.IO.Path.GetFileNameWithoutExtension(item.path)))}"); // 순서
            return builder.ToString(); // 반환
        }

        private static void AppendLineOfSight(StringBuilder builder, string label, Vector3 from, Vector3 to) // 카메라와 목표 사이 막힘 확인
        {
            float stopZ = FrontZ - 0.5f; // 건물 앞면 바로 앞에서 멈춤 (시선 목표가 건물 안일 수 있음)
            float t = Mathf.Abs(to.z - from.z) < 0.01f ? 1f : Mathf.Clamp01((stopZ - from.z) / (to.z - from.z)); // 멈출 지점 비율
            Vector3 direction = (to - from) * t; // 방향
            float distance = direction.magnitude; // 거리

            if (Physics.Raycast(from, direction.normalized, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore)) // 막힘
            {
                builder.AppendLine($"{label}: 막힘 — {hit.collider.name} ({hit.point})"); // 경고성 기록
                Debug.LogWarning($"[Project I] 메뉴 카메라 {label} 가림: {hit.collider.name}"); // 경고
                return; // 종료
            }

            builder.AppendLine($"{label}: 열림"); // 정상
        }
    }
}
