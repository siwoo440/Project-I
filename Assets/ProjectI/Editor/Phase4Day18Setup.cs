using System.IO; // 씬·에셋 폴더 확인 참조
using System.Linq; // 씬 루트 검색 참조
using ProjectI.Diagnostics; // F1 Trap 페이지 참조
using ProjectI.Traps; // Day18 함정 런타임 기능 참조
using UnityEditor; // Editor 메뉴·재질 생성 기능 참조
using UnityEditor.SceneManagement; // 씬 열기·저장 기능 참조
using UnityEngine; // GameObject·Primitive·Material 기능 참조
using UnityEngine.SceneManagement; // Scene 자료형 참조

namespace ProjectI.EditorTools // 프로젝트 에디터 자동 구성 도구 네임스페이스
{
    [InitializeOnLoad] // 컴파일 완료 후 Day18 함정 시험장 자동 구성
    public static class Phase4Day18Setup // 바닥·천장 가시·도끼·압력판 시험장을 자동 생성하는 도구
    {
        private const string ScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 테스트 대상 탐사 사무소 씬 경로
        private const string RootName = "===Day18 Trap System==="; // Day18 함정 시험장 루트 이름
        private const string ReadyMarkerName = "===Day18 Trap System Ready==="; // Day18 자동 구성 완료 마커 이름
        private const string MaterialFolder = "Assets/ProjectI/Art/Generated/Day18"; // Day18 생성 재질 폴더
        private static readonly Vector3 TestCenter = new Vector3(-27f, 0f, 18.2f); // Day17 몬스터 Spawn 앞 SprintLane 함정 시험장 중심

        static Phase4Day18Setup() // 자동 설정 등록
        {
            EditorApplication.delayCall += TryAutoApply; // 스크립트 컴파일 완료 뒤 한 번 자동 적용 예약
        }

        [MenuItem("Tools/Project I/Day 18/Apply Trap System")] // 수동 Day18 함정 구성 메뉴 등록
        public static void ApplyFromMenu() // 사용자가 강제로 Day18 시험장을 재구성하는 진입점
        {
            ApplyDay18(true, true); // 강제 재구성과 완료 대화상자 활성화
        }

        private static void TryAutoApply() // 컴파일 완료 후 자동 적용 시도
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // Batch 또는 Play 전환 중인지 확인
            {
                return; // 자동 씬 수정 제외 상태에서는 중단
            }

            ApplyDay18(false, false); // 완료 마커가 없을 때만 자동 구성
        }

        private static void ApplyDay18(bool showDialog, bool force) // Day18 시험장 전체 생성·저장
        {
            if (!File.Exists(ScenePath)) // 대상 씬 존재 여부 확인
            {
                return; // 씬 누락 시 구성 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); // 최신 ExplorationOffice 씬 단독 열기
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 완료 마커 검색

            if (!force && marker != null) // 이미 Day18 자동 구성이 완료됐는지 확인
            {
                return; // 반복 생성 방지
            }

            RemoveRoot(scene, RootName); // 이전 Day18 시험장 제거
            RemoveRoot(scene, ReadyMarkerName); // 이전 완료 마커 제거
            EnsureFolder(MaterialFolder); // Day18 재질 폴더 생성
            Material stone = GetOrCreateMaterial("Trap_Stone", new Color(0.22f, 0.22f, 0.21f), 0.05f, 0.25f); // 함정 바닥·프레임 석재 재질 생성
            Material darkMetal = GetOrCreateMaterial("Trap_DarkMetal", new Color(0.10f, 0.09f, 0.08f), 0.72f, 0.24f); // 녹슨 금속 프레임 재질 생성
            Material blade = GetOrCreateMaterial("Trap_Blade", new Color(0.36f, 0.36f, 0.34f), 0.82f, 0.36f); // 날·가시 금속 재질 생성
            Material rust = GetOrCreateMaterial("Trap_Rust", new Color(0.28f, 0.11f, 0.055f), 0.58f, 0.20f); // 체인·브래킷 녹슨 재질 생성
            Material warning = GetOrCreateMaterial("Trap_Warning", new Color(0.38f, 0.08f, 0.045f), 0.15f, 0.28f); // 작동부 강조 재질 생성
            GameObject root = new GameObject(RootName); // Day18 시험장 루트 생성
            CreateTestLane(root.transform, stone, rust); // 기존 SprintLane 위 함정 시험 구획 표시 생성
            FloorSpikeTrap floorSpike = CreateFloorSpike(root.transform, new Vector3(TestCenter.x - 3.3f, 0f, TestCenter.z + 1.6f), stone, darkMetal, blade); // 압력판 연동 바닥 가시 생성
            PressurePlate plate = CreatePressurePlate(root.transform, new Vector3(TestCenter.x - 3.3f, 0f, TestCenter.z - 1.6f), stone, rust, warning, new TrapControllerBase[] { floorSpike }); // 바닥 가시 작동 압력판 생성
            SwingingAxeTrap axe = CreateAxeTrap(root.transform, new Vector3(TestCenter.x, 0f, TestCenter.z + 0.3f), stone, rust, blade); // 중앙 통로 도끼 함정 생성
            CreateHiddenTrigger(root.transform, new Vector3(TestCenter.x, 1.0f, TestCenter.z - 1.8f), new Vector3(2.6f, 2.0f, 1.2f), axe); // 도끼 앞 숨은 자동 Trigger 생성
            CeilingSpikeSlamTrap ceiling = CreateCeilingSpike(root.transform, new Vector3(TestCenter.x + 3.3f, 0f, TestCenter.z + 0.3f), stone, darkMetal, blade, rust); // 우측 자동 주기 천장 가시판 생성
            root.AddComponent<TrapDebugPage>(); // F1 Trap 진단 페이지 자동 등록
            GameObject readyMarker = new GameObject(ReadyMarkerName); // Day18 자동 구성 완료 마커 생성
            EditorUtility.SetDirty(plate); // 압력판 링크 저장 대상으로 표시
            EditorUtility.SetDirty(floorSpike); // 바닥 가시 설정 저장 대상으로 표시
            EditorUtility.SetDirty(axe); // 도끼 설정 저장 대상으로 표시
            EditorUtility.SetDirty(ceiling); // 천장 가시 설정 저장 대상으로 표시
            EditorUtility.SetDirty(readyMarker); // 완료 마커 저장 대상으로 표시
            EditorSceneManager.MarkSceneDirty(scene); // 변경된 씬 저장 상태 표시
            EditorSceneManager.SaveScene(scene); // 함정 시험장 씬 저장
            AssetDatabase.SaveAssets(); // 생성 재질 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 갱신
            bool validationPassed = Phase4Day18Validator.Validate(false); // Day18 구조·피해 규칙 정적 검증 실행

            if (showDialog) // 수동 실행 결과 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", validationPassed ? "Day18 함정 시스템 구성이 완료되었습니다." : "Day18 검증 실패 - Console을 확인하세요.", "확인"); // 구성 결과 대화상자 표시
            }
        }

        private static void CreateTestLane(Transform parent, Material stone, Material accent) // 함정 시험 공간 바닥·구획선 생성
        {
            CreatePart(parent, "Trap_TestFloor", PrimitiveType.Cube, TestCenter + new Vector3(0f, -0.07f, 0f), new Vector3(10.8f, 0.14f, 8.0f), stone, Vector3.zero, true); // 기존 Lane 위 얇은 함정 시험 바닥 생성
            CreatePart(parent, "Trap_LaneDivider_L", PrimitiveType.Cube, TestCenter + new Vector3(-1.65f, 0.02f, 0f), new Vector3(0.06f, 0.04f, 7.6f), accent, Vector3.zero, false); // 좌측 시험 구획선 생성
            CreatePart(parent, "Trap_LaneDivider_R", PrimitiveType.Cube, TestCenter + new Vector3(1.65f, 0.02f, 0f), new Vector3(0.06f, 0.04f, 7.6f), accent, Vector3.zero, false); // 우측 시험 구획선 생성
        }

        private static FloorSpikeTrap CreateFloorSpike(Transform parent, Vector3 position, Material stone, Material metal, Material blade) // 바닥 돌출 가시 함정 모델·기능 생성
        {
            GameObject root = new GameObject("FloorSpikeTrap_01"); // 바닥 가시 루트 생성
            root.transform.SetParent(parent, false); // Day18 루트에 연결
            root.transform.position = position; // 시험장 좌측 위치 설정
            CreatePart(root.transform, "StoneBase", PrimitiveType.Cube, new Vector3(0f, 0.10f, 0f), new Vector3(2.4f, 0.20f, 2.4f), stone, Vector3.zero, true); // 가시 함정 석재 받침 생성
            CreatePart(root.transform, "IronFrame", PrimitiveType.Cube, new Vector3(0f, 0.22f, 0f), new Vector3(2.15f, 0.12f, 2.15f), metal, Vector3.zero, false); // 금속 틈·프레임 생성
            GameObject moving = new GameObject("MovingSpikes"); // 상승·하강 가시 묶음 루트 생성
            moving.transform.SetParent(root.transform, false); // 함정 루트에 연결

            for (int x = -1; x <= 1; x++) // 가로 3열 가시 생성
            {
                for (int z = -1; z <= 1; z++) // 세로 3열 가시 생성
                {
                    CreatePart(moving.transform, $"Spike_{x + 2}_{z + 2}", PrimitiveType.Cylinder, new Vector3(x * 0.60f, 0.78f, z * 0.60f), new Vector3(0.18f, 0.75f, 0.18f), blade, Vector3.zero, false); // 9개 금속 가시 기둥 생성
                    CreatePart(moving.transform, $"SpikeTip_{x + 2}_{z + 2}", PrimitiveType.Sphere, new Vector3(x * 0.60f, 1.50f, z * 0.60f), new Vector3(0.19f, 0.32f, 0.19f), blade, Vector3.zero, false); // 가시 끝 뾰족 실루엣 보강
                }
            }

            GameObject damageObject = new GameObject("DamageVolume"); // 가시 공통 피해 Trigger 생성
            damageObject.transform.SetParent(moving.transform, false); // 가시와 함께 이동하도록 연결
            damageObject.transform.localPosition = new Vector3(0f, 0.9f, 0f); // 가시 중앙 피해 영역 위치 설정
            BoxCollider damageCollider = damageObject.AddComponent<BoxCollider>(); // 가시 피해 Trigger Collider 추가
            damageCollider.isTrigger = true; // 물리 충돌 대신 Damage Pipeline Trigger로 사용
            damageCollider.size = new Vector3(2.0f, 1.75f, 2.0f); // 9개 가시 전체 피해 범위 설정
            TrapDamageSource damageSource = damageObject.AddComponent<TrapDamageSource>(); // 공통 중복 방지 피해 소스 추가
            damageSource.Configure(root.transform); // 자기 함정 루트 피해 제외 구성
            FloorSpikeTrap trap = root.AddComponent<FloorSpikeTrap>(); // 바닥 가시 상태 머신 추가
            trap.Configure("Floor Spike", damageSource, moving.transform, new Vector3(0f, -1.45f, 0f), Vector3.zero, 35f, 25f, 0.3f); // 가시 위치·피해 수치 구성
            return trap; // 생성된 바닥 가시 반환
        }

        private static CeilingSpikeSlamTrap CreateCeilingSpike(Transform parent, Vector3 position, Material stone, Material metal, Material blade, Material rust) // 자동 주기 천장 내려찍기 가시판 생성
        {
            GameObject root = new GameObject("CeilingSpikeSlamTrap_01"); // 천장 가시 루트 생성
            root.transform.SetParent(parent, false); // Day18 루트 연결
            root.transform.position = position; // 시험장 우측 위치 설정
            CreatePart(root.transform, "CeilingFrame", PrimitiveType.Cube, new Vector3(0f, 4.9f, 0f), new Vector3(2.6f, 0.28f, 2.6f), stone, Vector3.zero, true); // 천장 고정 프레임 생성
            CreatePart(root.transform, "GuideRail_L", PrimitiveType.Cube, new Vector3(-1.05f, 2.7f, 0f), new Vector3(0.14f, 4.2f, 0.18f), metal, Vector3.zero, false); // 좌측 가이드 레일 생성
            CreatePart(root.transform, "GuideRail_R", PrimitiveType.Cube, new Vector3(1.05f, 2.7f, 0f), new Vector3(0.14f, 4.2f, 0.18f), metal, Vector3.zero, false); // 우측 가이드 레일 생성
            CreatePart(root.transform, "Chain_L", PrimitiveType.Cylinder, new Vector3(-0.72f, 4.0f, 0f), new Vector3(0.07f, 0.70f, 0.07f), rust, Vector3.zero, false); // 좌측 체인 실루엣 생성
            CreatePart(root.transform, "Chain_R", PrimitiveType.Cylinder, new Vector3(0.72f, 4.0f, 0f), new Vector3(0.07f, 0.70f, 0.07f), rust, Vector3.zero, false); // 우측 체인 실루엣 생성
            GameObject moving = new GameObject("MovingSpikePlate"); // 내려찍는 철판 루트 생성
            moving.transform.SetParent(root.transform, false); // 함정 루트에 연결
            CreatePart(moving.transform, "HeavyPlate", PrimitiveType.Cube, Vector3.zero, new Vector3(2.35f, 0.30f, 2.35f), metal, Vector3.zero, true); // 무거운 이동 철판 생성

            for (int x = -1; x <= 1; x++) // 가로 3열 천장 가시 생성
            {
                for (int z = -1; z <= 1; z++) // 세로 3열 천장 가시 생성
                {
                    CreatePart(moving.transform, $"DownSpike_{x + 2}_{z + 2}", PrimitiveType.Cylinder, new Vector3(x * 0.62f, -0.70f, z * 0.62f), new Vector3(0.20f, 0.72f, 0.20f), blade, Vector3.zero, false); // 아래 방향 가시 몸체 생성
                    CreatePart(moving.transform, $"DownTip_{x + 2}_{z + 2}", PrimitiveType.Sphere, new Vector3(x * 0.62f, -1.38f, z * 0.62f), new Vector3(0.20f, 0.34f, 0.20f), blade, Vector3.zero, false); // 내려찍기 가시 끝 생성
                }
            }

            GameObject damageObject = new GameObject("DamageVolume"); // 이동 철판 피해 Trigger 생성
            damageObject.transform.SetParent(moving.transform, false); // 철판과 함께 이동하도록 연결
            damageObject.transform.localPosition = new Vector3(0f, -0.85f, 0f); // 가시 영역 중앙에 배치
            BoxCollider damageCollider = damageObject.AddComponent<BoxCollider>(); // 천장 가시 피해 Trigger 추가
            damageCollider.isTrigger = true; // 실제 Character 이동 차단은 HeavyPlate Collider가 담당하고 Trigger는 피해만 처리
            damageCollider.size = new Vector3(2.15f, 1.55f, 2.15f); // 가시판 전체 피해 영역 설정
            TrapDamageSource damageSource = damageObject.AddComponent<TrapDamageSource>(); // 중복 방지 Damage Pipeline 소스 추가
            damageSource.Configure(root.transform); // 자기 루트 제외 구성
            CeilingSpikeSlamTrap trap = root.AddComponent<CeilingSpikeSlamTrap>(); // 자동 주기 천장 가시 상태 머신 추가
            trap.Configure("Ceiling Spike Slam", damageSource, moving.transform, new Vector3(0f, 4.15f, 0f), new Vector3(0f, 1.72f, 0f), 70f, 55f, 1.0f, 0.4f); // 천장·바닥 위치와 강한 피해 수치 구성
            return trap; // 생성된 천장 가시 반환
        }

        private static SwingingAxeTrap CreateAxeTrap(Transform parent, Vector3 position, Material stone, Material rust, Material blade) // Swing 도끼 함정 모델·기능 생성
        {
            GameObject root = new GameObject("SwingingAxeTrap_01"); // 도끼 함정 루트 생성
            root.transform.SetParent(parent, false); // Day18 루트에 연결
            root.transform.position = position; // 중앙 통로 위치 설정
            CreatePart(root.transform, "WallPost", PrimitiveType.Cube, new Vector3(-1.35f, 1.8f, 0f), new Vector3(0.35f, 3.6f, 0.55f), stone, Vector3.zero, true); // 도끼 지지 석재 기둥 생성
            CreatePart(root.transform, "PivotBracket", PrimitiveType.Cylinder, new Vector3(-1.05f, 3.15f, 0f), new Vector3(0.28f, 0.25f, 0.28f), rust, new Vector3(90f, 0f, 0f), false); // 금속 회전 브래킷 생성
            GameObject pivot = new GameObject("AxePivot"); // 실제 도끼 회전 루트 생성
            pivot.transform.SetParent(root.transform, false); // 함정 루트에 연결
            pivot.transform.localPosition = new Vector3(-1.05f, 3.15f, 0f); // 브래킷 중심 위치 지정
            CreatePart(pivot.transform, "WoodHandle", PrimitiveType.Cube, new Vector3(0f, -1.35f, 0f), new Vector3(0.22f, 2.7f, 0.22f), rust, Vector3.zero, true); // 긴 도끼 자루 생성
            Transform axeHead = CreatePart(pivot.transform, "AxeHead", PrimitiveType.Cube, new Vector3(0.0f, -2.65f, 0f), new Vector3(1.30f, 0.60f, 0.30f), blade, new Vector3(0f, 0f, -8f), true); // 큰 금속 도끼머리 생성
            CreatePart(pivot.transform, "BladeEdge", PrimitiveType.Cube, new Vector3(0.65f, -2.65f, 0f), new Vector3(0.22f, 0.82f, 0.34f), blade, new Vector3(0f, 0f, -14f), false); // 날 끝 실루엣 보강
            CreatePart(pivot.transform, "CounterWeight", PrimitiveType.Sphere, new Vector3(0f, 0.18f, 0f), new Vector3(0.45f, 0.45f, 0.45f), rust, Vector3.zero, false); // 회전축 반대쪽 무게추 생성
            GameObject damageObject = new GameObject("DamageVolume"); // 도끼날 주변 피해 Trigger 생성
            damageObject.transform.SetParent(axeHead, false); // AxeHead 이동·회전을 따라가도록 연결
            BoxCollider damageCollider = damageObject.AddComponent<BoxCollider>(); // 도끼 피해 Trigger Collider 추가
            damageCollider.isTrigger = true; // 실제 충돌과 별개로 Damage Pipeline 판정만 사용
            damageCollider.size = new Vector3(1.6f, 1.0f, 0.85f); // 도끼날 주변 피해 범위 설정
            TrapDamageSource damageSource = damageObject.AddComponent<TrapDamageSource>(); // 공통 함정 피해 소스 추가
            damageSource.Configure(root.transform); // 자기 함정 피해 제외 구성
            SwingingAxeTrap trap = root.AddComponent<SwingingAxeTrap>(); // 도끼 Swing 상태 머신 추가
            trap.Configure("Swinging Axe", damageSource, pivot.transform, 55f, 40f, 2.0f); // 도끼 피해·경직·넉백 수치 구성
            return trap; // 생성된 도끼 함정 반환
        }

        private static PressurePlate CreatePressurePlate(Transform parent, Vector3 position, Material stone, Material metal, Material warning, TrapControllerBase[] traps) // Player·Monster 공통 압력판 생성
        {
            GameObject root = new GameObject("PressurePlate_01"); // 압력판 루트 생성
            root.transform.SetParent(parent, false); // Day18 루트 연결
            root.transform.position = position; // 바닥 가시 앞쪽 위치 지정
            CreatePart(root.transform, "PlateFrame", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), new Vector3(2.2f, 0.16f, 1.55f), stone, Vector3.zero, true); // 주변 석재 프레임 생성
            Transform plate = CreatePart(root.transform, "MovingPlate", PrimitiveType.Cube, new Vector3(0f, 0.18f, 0f), new Vector3(1.85f, 0.16f, 1.20f), warning, Vector3.zero, false); // 실제 눌리는 금속·석재 상판 생성
            CreatePart(root.transform, "MechanismSlot", PrimitiveType.Cube, new Vector3(0f, -0.02f, 0f), new Vector3(1.1f, 0.08f, 0.65f), metal, Vector3.zero, false); // 상판 아래 작동 틈 표현
            BoxCollider trigger = root.AddComponent<BoxCollider>(); // 압력판 감지 Trigger 추가
            trigger.isTrigger = true; // 플레이어·몬스터 이동을 막지 않는 감지 영역 사용
            trigger.center = new Vector3(0f, 0.48f, 0f); // 발·몬스터 Collider가 안정적으로 들어오는 높이 설정
            trigger.size = new Vector3(1.9f, 0.85f, 1.25f); // 상판 크기에 맞춘 감지 범위 설정
            PressurePlate pressurePlate = root.AddComponent<PressurePlate>(); // 압력판 상태·링크 기능 추가
            pressurePlate.Configure(plate, new Vector3(0f, 0.18f, 0f), new Vector3(0f, 0.10f, 0f), traps); // 눌림 위치와 연결 바닥 가시 구성
            return pressurePlate; // 생성된 압력판 반환
        }

        private static void CreateHiddenTrigger(Transform parent, Vector3 position, Vector3 size, TrapControllerBase targetTrap) // 통로 진입 시 도끼를 작동시키는 숨은 Trigger 생성
        {
            GameObject root = new GameObject("Axe_HiddenTrigger"); // 숨은 Trigger 오브젝트 생성
            root.transform.SetParent(parent, false); // Day18 루트 연결
            root.transform.position = position; // 도끼 앞쪽 통로 위치 지정
            BoxCollider collider = root.AddComponent<BoxCollider>(); // 통로 Trigger Collider 추가
            collider.isTrigger = true; // 이동을 막지 않는 감지 영역 사용
            collider.size = size; // 플레이어·몬스터가 통과할 충분한 범위 설정
            TrapTriggerVolume trigger = root.AddComponent<TrapTriggerVolume>(); // 공통 Player·Monster Trigger 기능 추가
            trigger.Configure(targetTrap); // Swing 도끼 연결
        }

        private static Transform CreatePart(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 scale, Material material, Vector3 euler, bool keepCollider) // 프리미티브 모델 부품 공통 생성
        {
            GameObject part = GameObject.CreatePrimitive(type); // 지정 Primitive 생성
            part.name = name; // Hierarchy에서 알아보기 쉬운 부품 이름 지정
            part.transform.SetParent(parent, false); // 대상 모델 루트에 연결
            part.transform.localPosition = localPosition; // 부품 로컬 위치 적용
            part.transform.localEulerAngles = euler; // 부품 로컬 회전 적용
            part.transform.localScale = scale; // 부품 크기 적용
            Renderer renderer = part.GetComponent<Renderer>(); // 생성된 Primitive Renderer 조회

            if (renderer != null && material != null) // 재질 적용 가능 여부 확인
            {
                renderer.sharedMaterial = material; // 생성 재질 연결
            }

            Collider collider = part.GetComponent<Collider>(); // Primitive 기본 Collider 조회

            if (!keepCollider && collider != null) // 장식용 부품 Collider 제거 필요 여부 확인
            {
                Object.DestroyImmediate(collider); // 불필요한 복합 Collider가 Trigger 판정을 방해하지 않도록 제거
            }

            return part.transform; // 추가 자식 연결용 Transform 반환
        }

        private static Material GetOrCreateMaterial(string name, Color color, float metallic, float smoothness) // Day18 URP 재질 생성·갱신
        {
            string path = $"{MaterialFolder}/{name}.mat"; // 생성 재질 경로 계산
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 재질 조회

            if (material == null) // 최초 생성 여부 확인
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // 현재 URP Lit Shader 검색
                material = new Material(shader); // 새 재질 생성
                material.name = name; // 재질 이름 설정
                AssetDatabase.CreateAsset(material, path); // 지정 경로에 재질 에셋 저장
            }

            material.SetColor("_BaseColor", color); // 기본 색상 적용
            material.SetFloat("_Metallic", Mathf.Clamp01(metallic)); // 금속도 적용
            material.SetFloat("_Smoothness", Mathf.Clamp01(smoothness)); // 표면 매끄러움 적용
            EditorUtility.SetDirty(material); // 수정 재질 저장 대상으로 표시
            return material; // 생성·갱신 재질 반환
        }

        private static void EnsureFolder(string path) // 중첩 Asset 폴더 존재 보장
        {
            string[] parts = path.Split('/'); // 경로를 폴더 단위로 분리
            string current = parts[0]; // Assets 루트부터 시작

            for (int index = 1; index < parts.Length; index++) // 하위 폴더 순서대로 생성
            {
                string next = current + "/" + parts[index]; // 현재 단계 전체 경로 계산

                if (!AssetDatabase.IsValidFolder(next)) // 현재 폴더 존재 여부 확인
                {
                    AssetDatabase.CreateFolder(current, parts[index]); // 누락 폴더 생성
                }

                current = next; // 다음 단계 기준 경로 갱신
            }
        }

        private static void RemoveRoot(Scene scene, string rootName) // 이전 자동 생성 루트 안전 제거
        {
            GameObject existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == rootName); // 지정 이름 루트 조회

            if (existing != null) // 기존 루트 존재 여부 확인
            {
                Object.DestroyImmediate(existing); // 중복 생성 방지를 위해 기존 자동 생성 루트 제거
            }
        }
    }
}
