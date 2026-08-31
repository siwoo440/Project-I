using System.IO; // 자동 생성 재질 폴더와 씬 파일 확인 기능 참조
using System.Linq; // 씬 루트 검색 기능 참조
using ProjectI.Combat; // 테스트 표적 공통 체력·진영 기능 참조
using ProjectI.Combat.Ranged; // Day16 석궁·리볼버 원거리 전투 기능 참조
using ProjectI.Diagnostics; // F1 Ranged Combat 페이지 참조
using ProjectI.Items; // 기존 WorldItem·CarryType 참조
using ProjectI.Player; // 기존 플레이어 입력 기능 참조
using UnityEditor; // 유니티 에디터 메뉴·재질 생성 기능 참조
using UnityEditor.SceneManagement; // 씬 열기·저장 기능 참조
using UnityEngine; // 유니티 오브젝트·재질·물리 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 자동 구성 도구 네임스페이스
{
    [InitializeOnLoad] // 컴파일 완료 후 Day16 원거리 전투 자동 구성
    public static class Phase4Day16Setup // 석궁·리볼버 모델·사격장·장전 기능 자동 구성
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string Day16RootName = "===Day16 Ranged Combat==="; // Day16 원거리 전투 시험장 루트 이름
        private const string ReadyMarkerName = "===Day16 Ranged Combat Ready v2==="; // 석궁 탄속·리볼버 외형 보정 버전 자동 구성 완료 마커 이름
        private const string LegacyReadyMarkerName = "===Day16 Ranged Combat Ready==="; // 기존 Day16 완료 마커 이름
        private const string MaterialFolder = "Assets/ProjectI/Art/Generated/Day16"; // Day16 무기·사격장 재질 생성 폴더
        private static readonly Vector3 RangeCenter = new Vector3(-27f, 0f, -13.5f); // 01_SprintLane 남쪽 사격 구역 중심

        static Phase4Day16Setup() // 자동 설정 등록
        {
            EditorApplication.delayCall += TryAutoApply; // 스크립트 컴파일 완료 후 Day16 자동 구성 예약
        }

        [MenuItem("Tools/Project I/Day 16/Apply Crossbow + Revolver")] // 수동 Day16 구성 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 Day16 전체 구성 실행
        {
            ApplyDay16(true, true); // 강제 재구성과 결과 대화상자 활성화
        }

        private static void TryAutoApply() // 컴파일 완료 후 자동 구성 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // Batch 또는 Play 전환 상태 확인
            {
                return; // 자동 구성 제외 상태에서는 중단
            }

            ApplyDay16(false, false); // 완료 마커가 없을 때 한 번 자동 적용
        }

        private static void ApplyDay16(bool showDialog, bool force) // Day16 원거리 전투 전체 적용
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 탐사 씬 존재 여부 확인
            {
                return; // 씬 누락 시 자동 구성 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 단독 열기
            GameObject existingMarker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 Day16 완료 마커 조회

            if (!force && existingMarker != null) // 이미 Day16 구성이 적용됐는지 확인
            {
                return; // 반복 자동 적용 방지
            }

            PlayerInputReader inputReader = Object.FindFirstObjectByType<PlayerInputReader>(); // 기존 플레이어 입력 래퍼 조회
            PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>(); // 기존 빠른 슬롯 인벤토리 조회

            if (inputReader == null || inventory == null) // 선행 플레이어·인벤토리 존재 여부 확인
            {
                Debug.LogError("[Project I] Day16 구성 전에 PlayerInputReader와 PlayerInventory가 필요합니다."); // 선행 시스템 누락 오류 출력
                return; // Day16 구성 중단
            }

            RemoveExistingRoot(scene, Day16RootName); // 기존 Day16 시험장 루트 제거
            RemoveExistingRoot(scene, ReadyMarkerName); // 현재 Day16 완료 마커 제거
            RemoveExistingRoot(scene, LegacyReadyMarkerName); // 이전 Day16 완료 마커 제거
            EnsureAssetFolder(MaterialFolder); // Day16 재질 폴더 존재 보장
            Material darkSteel = GetOrCreateMaterial("Ranged_DarkSteel", new Color(0.11f, 0.12f, 0.13f), 0.82f, 0.48f); // 원거리 무기 어두운 철재 재질 생성
            Material brightSteel = GetOrCreateMaterial("Ranged_BrightSteel", new Color(0.42f, 0.45f, 0.48f), 0.90f, 0.72f); // 총열·석궁 브래킷 강철 재질 생성
            Material wood = GetOrCreateMaterial("Ranged_Wood", new Color(0.24f, 0.105f, 0.035f), 0.02f, 0.24f); // 석궁 몸체·리볼버 손잡이 목재 재질 생성
            Material leather = GetOrCreateMaterial("Ranged_Leather", new Color(0.09f, 0.045f, 0.025f), 0.02f, 0.22f); // 그립 가죽 재질 생성
            Material brass = GetOrCreateMaterial("Ranged_Brass", new Color(0.52f, 0.31f, 0.06f), 0.72f, 0.48f); // 리볼버 탄약·장식 황동 재질 생성
            Material stringMaterial = GetOrCreateMaterial("Ranged_String", new Color(0.045f, 0.035f, 0.025f), 0.0f, 0.12f); // 석궁 시위 재질 생성
            Material standMaterial = GetOrCreateMaterial("Ranged_Stand", new Color(0.09f, 0.10f, 0.11f), 0.62f, 0.30f); // 무기 전시대 철재 재질 생성
            Material accentMaterial = GetOrCreateMaterial("Ranged_Accent", new Color(0.06f, 0.30f, 0.60f), 0.25f, 0.35f); // SprintLane 연계 파란 강조 재질 생성
            Material targetMaterial = GetOrCreateMaterial("Ranged_Target", new Color(0.42f, 0.055f, 0.045f), 0.12f, 0.28f); // 사격 표적 붉은 재질 생성
            Material targetTrimMaterial = GetOrCreateMaterial("Ranged_TargetTrim", new Color(0.72f, 0.68f, 0.55f), 0.18f, 0.32f); // 표적 중심 밝은 재질 생성
            GameObject day16Root = new GameObject(Day16RootName); // Day16 원거리 시험장 루트 생성
            CrossbowBoltProjectile boltTemplate = CreateBoltTemplate(day16Root.transform, wood, brightSteel, brass); // 런타임 포물선 발사용 비활성 볼트 템플릿 생성
            CreateWeaponStands(day16Root.transform, standMaterial, accentMaterial); // 석궁·리볼버 전시대 생성
            CreateDetailedCrossbow(day16Root.transform, boltTemplate, darkSteel, brightSteel, wood, leather, stringMaterial, brass); // 상세 석궁 월드 아이템 생성
            CreateDetailedRevolver(day16Root.transform, darkSteel, brightSteel, wood, brass); // 상세 리볼버 6발 월드 아이템 생성
            CreateRangedTargets(day16Root.transform, targetMaterial, targetTrimMaterial, standMaterial); // 6m·10m·13m 원거리 피해 시험 표적 생성
            day16Root.AddComponent<RangedCombatDebugPage>(); // F1 Ranged Combat 진단 페이지 추가
            GameObject marker = new GameObject(ReadyMarkerName); // Day16 자동 적용 완료 마커 생성
            EditorUtility.SetDirty(marker); // 완료 마커 씬 저장 대상으로 표시
            EditorSceneManager.MarkSceneDirty(scene); // 변경된 탐사 씬 저장 상태 표시
            EditorSceneManager.SaveScene(scene); // Day16 원거리 시험장과 무기 저장
            AssetDatabase.SaveAssets(); // 생성 재질 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 갱신
            bool validationPassed = Phase4Day16Validator.Validate(false); // Day16 정적 구조 검증 실행

            if (showDialog) // 수동 실행 결과 표시 여부 확인
            {
                string message = validationPassed ? "Day16 석궁·리볼버 원거리 전투 구성이 완료되었습니다." : "Day16 검증 실패 - Console을 확인하세요."; // 구성 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 완료 또는 실패 대화상자 출력
            }
        }

        private static void CreateWeaponStands(Transform parent, Material standMaterial, Material accentMaterial) // SprintLane 남쪽에 두 원거리 무기 전시대 생성
        {
            Vector3 crossbowStand = RangeCenter + new Vector3(-2.15f, 0f, -3.1f); // 석궁 전시대 월드 기준 위치 계산
            Vector3 revolverStand = RangeCenter + new Vector3(2.15f, 0f, -3.1f); // 리볼버 전시대 월드 기준 위치 계산
            CreateBox(parent, "Crossbow_DisplayBase", crossbowStand + new Vector3(0f, 0.16f, 0f), new Vector3(3.0f, 0.28f, 1.5f), standMaterial, true); // 석궁 전시대 본체 생성
            CreateBox(parent, "Crossbow_DisplayTop", crossbowStand + new Vector3(0f, 0.34f, 0f), new Vector3(2.7f, 0.08f, 1.28f), accentMaterial, true); // 석궁 전시대 파란 상판 생성
            CreateBox(parent, "Revolver_DisplayBase", revolverStand + new Vector3(0f, 0.16f, 0f), new Vector3(3.0f, 0.28f, 1.5f), standMaterial, true); // 리볼버 전시대 본체 생성
            CreateBox(parent, "Revolver_DisplayTop", revolverStand + new Vector3(0f, 0.34f, 0f), new Vector3(2.7f, 0.08f, 1.28f), accentMaterial, true); // 리볼버 전시대 파란 상판 생성
            CreateBox(parent, "Ranged_DisplayLane", RangeCenter + new Vector3(0f, 0.07f, -3.1f), new Vector3(7.8f, 0.04f, 2.0f), accentMaterial, false); // 두 무기 전시 구역 바닥 강조 생성
        }

        private static void CreateDetailedCrossbow(Transform parent, CrossbowBoltProjectile boltTemplate, Material darkSteel, Material brightSteel, Material wood, Material leather, Material stringMaterial, Material brass) // 프리미티브 조합 상세 석궁 월드 아이템 생성
        {
            Vector3 position = RangeCenter + new Vector3(-2.15f, 0.93f, -3.1f); // 석궁 전시대 위 월드 위치 계산
            GameObject root = new GameObject("Day16_Crossbow"); // 석궁 기능 루트 생성
            root.transform.SetParent(parent); // Day16 시험장 아래 배치
            root.transform.position = position; // 전시대 위 석궁 위치 지정
            root.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // 월드 전시용 옆면 각도 지정
            Rigidbody body = root.AddComponent<Rigidbody>(); // 기존 WorldItem용 Rigidbody 추가
            body.mass = 3.4f; // 석궁 중량감 질량 설정
            body.interpolation = RigidbodyInterpolation.Interpolate; // 월드 이동 보간 활성화
            BoxCollider collider = root.AddComponent<BoxCollider>(); // F 획득 Raycast와 월드 충돌용 루트 Collider 추가
            collider.center = new Vector3(0f, 0f, 0.05f); // 석궁 전체 모델 중심에 Collider 배치
            collider.size = new Vector3(1.75f, 0.48f, 1.65f); // 활대와 몸체를 포괄하는 충돌 크기 설정
            WorldItem worldItem = root.AddComponent<WorldItem>(); // 기존 빠른 슬롯 월드 아이템 기능 추가
            worldItem.Configure("석궁", 0.36f, CarryType.OneHand); // 무기 슬롯 전환이 잠기지 않도록 석궁을 일반 무기 운반 규칙으로 설정
            worldItem.ConfigureCarryPose(CarryType.OneHand, new Vector3(-0.20f, -0.14f, 0.24f), new Vector3(2f, 0f, 0f)); // 한손 CarryPoint에서 화면 중앙 아래로 보정한 석궁 기본 자세 설정
            GameObject visualObject = new GameObject("VisualPivot"); // 조준·장전 애니메이션 시각 루트 생성
            visualObject.transform.SetParent(root.transform, false); // 석궁 루트 자식으로 연결
            Transform visual = visualObject.transform; // 시각 루트 Transform 저장
            CreatePrimitivePart(visual, "Stock_Main", PrimitiveType.Cube, new Vector3(0f, -0.02f, -0.20f), new Vector3(0.24f, 0.22f, 1.42f), wood, Vector3.zero); // 긴 목재 개머리·몸체 생성
            CreatePrimitivePart(visual, "Stock_Butt", PrimitiveType.Cube, new Vector3(0f, 0f, -0.82f), new Vector3(0.36f, 0.34f, 0.34f), wood, new Vector3(-8f, 0f, 0f)); // 후방 넓은 개머리 생성
            CreatePrimitivePart(visual, "Rail", PrimitiveType.Cube, new Vector3(0f, 0.14f, 0.18f), new Vector3(0.13f, 0.07f, 1.48f), brightSteel, Vector3.zero); // 볼트가 놓이는 금속 레일 생성
            CreatePrimitivePart(visual, "TriggerHousing", PrimitiveType.Cube, new Vector3(0f, -0.16f, -0.26f), new Vector3(0.30f, 0.24f, 0.30f), darkSteel, Vector3.zero); // 방아쇠 금속 하우징 생성
            CreatePrimitivePart(visual, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.32f, -0.38f), new Vector3(0.22f, 0.42f, 0.20f), leather, new Vector3(-18f, 0f, 0f)); // 아래쪽 가죽 손잡이 생성
            CreatePrimitivePart(visual, "Trigger", PrimitiveType.Cube, new Vector3(0f, -0.23f, -0.12f), new Vector3(0.05f, 0.18f, 0.05f), brass, new Vector3(-18f, 0f, 0f)); // 작은 황동 방아쇠 생성
            CreatePrimitivePart(visual, "BowCenter", PrimitiveType.Cube, new Vector3(0f, 0.10f, 0.55f), new Vector3(0.42f, 0.15f, 0.18f), darkSteel, Vector3.zero); // 활대 중심 금속 브래킷 생성
            CreatePrimitivePart(visual, "Limb_Left", PrimitiveType.Cube, new Vector3(-0.78f, 0.10f, 0.58f), new Vector3(1.18f, 0.09f, 0.13f), wood, new Vector3(0f, -7f, 0f)); // 왼쪽 목재 활대 생성
            CreatePrimitivePart(visual, "Limb_Right", PrimitiveType.Cube, new Vector3(0.78f, 0.10f, 0.58f), new Vector3(1.18f, 0.09f, 0.13f), wood, new Vector3(0f, 7f, 0f)); // 오른쪽 목재 활대 생성
            CreatePrimitivePart(visual, "LimbCap_Left", PrimitiveType.Cube, new Vector3(-1.34f, 0.10f, 0.65f), new Vector3(0.16f, 0.15f, 0.18f), brightSteel, Vector3.zero); // 왼쪽 활대 끝 철제 팁 생성
            CreatePrimitivePart(visual, "LimbCap_Right", PrimitiveType.Cube, new Vector3(1.34f, 0.10f, 0.65f), new Vector3(0.16f, 0.15f, 0.18f), brightSteel, Vector3.zero); // 오른쪽 활대 끝 철제 팁 생성
            Transform stringRoot = new GameObject("StringRoot").transform; // 시위 장전 모션 전용 루트 생성
            stringRoot.SetParent(visual, false); // 시각 루트 아래 연결
            CreateBar(stringRoot, "String_Left", new Vector3(-1.32f, 0.10f, 0.65f), new Vector3(0f, 0.10f, 0.25f), 0.018f, stringMaterial); // 왼쪽 활대 끝에서 중앙까지 시위 생성
            CreateBar(stringRoot, "String_Right", new Vector3(1.32f, 0.10f, 0.65f), new Vector3(0f, 0.10f, 0.25f), 0.018f, stringMaterial); // 오른쪽 활대 끝에서 중앙까지 시위 생성
            CreatePrimitivePart(visual, "Stirrup", PrimitiveType.Cube, new Vector3(0f, -0.02f, 0.92f), new Vector3(0.50f, 0.08f, 0.08f), brightSteel, Vector3.zero); // 앞쪽 발 고정용 금속 발판 생성
            CreatePrimitivePart(visual, "ScopeBody", PrimitiveType.Cylinder, new Vector3(0f, 0.30f, -0.02f), new Vector3(0.11f, 0.34f, 0.11f), darkSteel, new Vector3(90f, 0f, 0f)); // 장거리 확대 조준용 짧은 스코프 몸체 생성
            CreatePrimitivePart(visual, "ScopeFront", PrimitiveType.Cylinder, new Vector3(0f, 0.30f, 0.30f), new Vector3(0.15f, 0.06f, 0.15f), brightSteel, new Vector3(90f, 0f, 0f)); // 스코프 전면 링 생성
            CreatePrimitivePart(visual, "ScopeRear", PrimitiveType.Cylinder, new Vector3(0f, 0.30f, -0.34f), new Vector3(0.14f, 0.06f, 0.14f), brightSteel, new Vector3(90f, 0f, 0f)); // 스코프 후면 링 생성
            GameObject loadedBolt = new GameObject("LoadedBoltVisual"); // 레일 위 장전 볼트 시각 루트 생성
            loadedBolt.transform.SetParent(visual, false); // 석궁 시각 루트 아래 연결
            CreatePrimitivePart(loadedBolt.transform, "Shaft", PrimitiveType.Cylinder, new Vector3(0f, 0.20f, 0.34f), new Vector3(0.025f, 0.55f, 0.025f), wood, new Vector3(90f, 0f, 0f)); // 레일 위 볼트 목재 샤프트 생성
            CreatePrimitivePart(loadedBolt.transform, "Head", PrimitiveType.Cube, new Vector3(0f, 0.20f, 0.90f), new Vector3(0.09f, 0.06f, 0.14f), brightSteel, new Vector3(45f, 0f, 0f)); // 레일 위 볼트 철제 촉 생성
            Transform muzzle = new GameObject("Muzzle").transform; // 볼트 생성 위치 Transform 생성
            muzzle.SetParent(visual, false); // 석궁 시각 루트 아래 연결
            muzzle.localPosition = new Vector3(0f, 0.20f, 0.98f); // 활대 앞 레일 끝에 발사 위치 지정
            CrossbowWeaponItem weapon = root.AddComponent<CrossbowWeaponItem>(); // 석궁 사용·조준·장전 기능 추가
            weapon.ConfigureCommon(visual, new Vector3(0f, 0.11f, 0.10f), Vector3.zero, 40f, 12f, 110f, 0.58f, 0.48f); // 우클릭 중앙 이동·강한 확대 조준 설정
            weapon.ConfigureCrossbow(muzzle, boltTemplate, loadedBolt, stringRoot, 95f, 55f, 28f, 1.0f, 1.45f, 12, true); // 기존 38m/s 대비 2.5배 빠른 95m/s 포물선 속도·피해·장전·예비 볼트 구성
            EditorUtility.SetDirty(worldItem); // 석궁 WorldItem 씬 저장 대상으로 표시
            EditorUtility.SetDirty(weapon); // 석궁 기능 씬 저장 대상으로 표시
        }

        private static void CreateDetailedRevolver(Transform parent, Material darkSteel, Material brightSteel, Material wood, Material brass) // 프리미티브 조합 상세 6발 리볼버 월드 아이템 생성
        {
            Vector3 position = RangeCenter + new Vector3(2.15f, 0.90f, -3.1f); // 리볼버 전시대 위 월드 위치 계산
            GameObject root = new GameObject("Day16_Revolver"); // 리볼버 기능 루트 생성
            root.transform.SetParent(parent); // Day16 시험장 아래 배치
            root.transform.position = position; // 전시대 위 리볼버 위치 지정
            root.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // 월드 전시용 옆면 각도 지정
            Rigidbody body = root.AddComponent<Rigidbody>(); // 기존 WorldItem용 Rigidbody 추가
            body.mass = 1.25f; // 리볼버 질량 설정
            body.interpolation = RigidbodyInterpolation.Interpolate; // 월드 물리 보간 활성화
            BoxCollider collider = root.AddComponent<BoxCollider>(); // F 획득 Raycast·월드 충돌용 Collider 추가
            collider.center = new Vector3(0f, 0f, 0.13f); // 리볼버 전체 중심에 Collider 배치
            collider.size = new Vector3(0.43f, 0.56f, 1.02f); // 크기 축소된 총열·손잡이를 포괄하는 충돌 크기 설정
            WorldItem worldItem = root.AddComponent<WorldItem>(); // 기존 빠른 슬롯 월드 아이템 기능 추가
            worldItem.Configure("6연발 리볼버", 0.22f, CarryType.OneHand); // 리볼버 표시 이름·한손 운반 설정
            worldItem.ConfigureCarryPose(CarryType.OneHand, new Vector3(0.21f, -0.21f, 0.24f), new Vector3(2f, 0f, 0f)); // 화면 오른쪽 아래 기본 리볼버 자세 설정
            GameObject visualObject = new GameObject("VisualPivot"); // 조준·장전 시각 루트 생성
            visualObject.transform.SetParent(root.transform, false); // 리볼버 루트 자식 연결
            Transform visual = visualObject.transform; // 시각 루트 Transform 저장
            visual.localScale = Vector3.one * 0.82f; // 리볼버 전체 시각 크기를 약간 줄여 손과 월드에서 모두 자연스럽게 보정
            CreatePrimitivePart(visual, "Frame", PrimitiveType.Cube, new Vector3(0f, 0.05f, 0.12f), new Vector3(0.34f, 0.28f, 0.58f), darkSteel, Vector3.zero); // 리볼버 중앙 프레임 생성
            CreatePrimitivePart(visual, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.13f, 0.57f), new Vector3(0.11f, 0.46f, 0.11f), brightSteel, new Vector3(90f, 0f, 0f)); // 긴 강철 총열 생성
            CreatePrimitivePart(visual, "UnderLug", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.55f), new Vector3(0.16f, 0.11f, 0.60f), darkSteel, Vector3.zero); // 총열 아래 이젝터 하우징 생성
            CreatePrimitivePart(visual, "EjectorRod", PrimitiveType.Cylinder, new Vector3(0f, -0.04f, 0.58f), new Vector3(0.035f, 0.40f, 0.035f), brightSteel, new Vector3(90f, 0f, 0f)); // 총열 아래 이젝터 로드 생성
            Transform cylinderRoot = new GameObject("CylinderRoot").transform; // 발사·장전 회전용 실린더 루트 생성
            cylinderRoot.SetParent(visual, false); // 시각 루트 아래 연결
            cylinderRoot.localPosition = new Vector3(0f, 0.09f, 0.06f); // 프레임 중앙 실린더 위치 지정
            CreatePrimitivePart(cylinderRoot, "Cylinder", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.29f, 0.15f, 0.29f), brightSteel, new Vector3(90f, 0f, 0f)); // 총열 방향과 일치하는 짧은 드럼형 실린더 본체 생성
            GameObject[] rounds = new GameObject[6]; // 약실 안에 전방을 향해 들어가는 황동 탄약 6발 배열 생성

            for (int index = 0; index < rounds.Length; index++) // 6개 약실 탄약 반복 생성
            {
                float angle = index * 60f * Mathf.Deg2Rad; // 현재 약실 원형 각도 계산
                float x = Mathf.Cos(angle) * 0.10f; // 실린더 정면 X 위치 계산
                float y = Mathf.Sin(angle) * 0.10f; // 실린더 정면 Y 위치 계산
                GameObject round = CreatePrimitivePart(cylinderRoot, $"Round_{index + 1}", PrimitiveType.Cylinder, new Vector3(x, y, -0.015f), new Vector3(0.028f, 0.09f, 0.028f), brass, new Vector3(90f, 0f, 0f)); // 총열 방향으로 정렬된 황동 탄약 한 발 생성
                rounds[index] = round; // 리볼버 기능 연결용 탄약 시각 저장
            }

            CreatePrimitivePart(visual, "GripCore", PrimitiveType.Cube, new Vector3(0f, -0.30f, -0.18f), new Vector3(0.30f, 0.58f, 0.28f), wood, new Vector3(-16f, 0f, 0f)); // 기울어진 목재 손잡이 생성
            CreatePrimitivePart(visual, "GripBackstrap", PrimitiveType.Cube, new Vector3(0f, -0.28f, -0.30f), new Vector3(0.34f, 0.58f, 0.08f), darkSteel, new Vector3(-16f, 0f, 0f)); // 손잡이 뒤 철제 프레임 생성
            CreatePrimitivePart(visual, "Hammer", PrimitiveType.Cube, new Vector3(0f, 0.27f, -0.20f), new Vector3(0.16f, 0.16f, 0.20f), darkSteel, new Vector3(-18f, 0f, 0f)); // 후방 해머 생성
            CreatePrimitivePart(visual, "Trigger", PrimitiveType.Cube, new Vector3(0f, -0.12f, -0.02f), new Vector3(0.05f, 0.18f, 0.05f), brass, new Vector3(-15f, 0f, 0f)); // 황동 방아쇠 생성
            CreatePrimitivePart(visual, "TriggerGuard_Left", PrimitiveType.Cube, new Vector3(-0.13f, -0.13f, 0f), new Vector3(0.04f, 0.28f, 0.30f), darkSteel, Vector3.zero); // 방아쇠울 왼쪽 프레임 생성
            CreatePrimitivePart(visual, "TriggerGuard_Right", PrimitiveType.Cube, new Vector3(0.13f, -0.13f, 0f), new Vector3(0.04f, 0.28f, 0.30f), darkSteel, Vector3.zero); // 방아쇠울 오른쪽 프레임 생성
            CreatePrimitivePart(visual, "FrontSight", PrimitiveType.Cube, new Vector3(0f, 0.28f, 0.91f), new Vector3(0.05f, 0.10f, 0.08f), brightSteel, Vector3.zero); // 총열 끝 전방 조준기 생성
            CreatePrimitivePart(visual, "RearSight", PrimitiveType.Cube, new Vector3(0f, 0.29f, -0.14f), new Vector3(0.16f, 0.07f, 0.08f), brightSteel, Vector3.zero); // 프레임 위 후방 조준기 생성
            Transform muzzle = new GameObject("Muzzle").transform; // 즉시 탄도 총구 Transform 생성
            muzzle.SetParent(visual, false); // 시각 루트 아래 연결
            muzzle.localPosition = new Vector3(0f, 0.13f, 1.03f); // 총열 앞쪽 총구 위치 지정
            GameObject muzzleFlash = CreatePrimitivePart(visual, "MuzzleFlash", PrimitiveType.Sphere, new Vector3(0f, 0.13f, 1.09f), new Vector3(0.18f, 0.18f, 0.25f), brass, Vector3.zero); // 발사 순간 짧게 표시할 화염 대체 구체 생성
            muzzleFlash.SetActive(false); // 시작 시 총구 화염 숨김
            RevolverWeaponItem weapon = root.AddComponent<RevolverWeaponItem>(); // 리볼버 6발·탄퍼짐·재장전 기능 추가
            weapon.ConfigureCommon(visual, new Vector3(-0.20f, 0.10f, 0.06f), Vector3.zero, 55f, 14f, 100f, 0.72f, 0.55f); // 우클릭 중앙 조준과 약한 확대 설정
            weapon.ConfigureRevolver(muzzle, cylinderRoot, rounds, muzzleFlash, 6, 6, 24, 28f, 8f, 0.55f, 75f, 0.16f, 1.65f, 0.85f, 0.18f, 0.75f, 4.5f, 3.2f, 0.48f); // 6발·빠른 연사 탄퍼짐·실린더 장전 설정
            EditorUtility.SetDirty(worldItem); // 리볼버 WorldItem 씬 저장 대상으로 표시
            EditorUtility.SetDirty(weapon); // 리볼버 기능 씬 저장 대상으로 표시
        }

        private static CrossbowBoltProjectile CreateBoltTemplate(Transform parent, Material wood, Material steel, Material brass) // 런타임 복제용 비활성 석궁 볼트 모델 생성
        {
            GameObject root = new GameObject("Day16_CrossbowBoltTemplate"); // 볼트 기능 템플릿 루트 생성
            root.transform.SetParent(parent); // Day16 시험장 아래 숨김 템플릿 배치
            root.transform.localPosition = new Vector3(0f, -50f, 0f); // 실제 시험장 밖 아래쪽에 템플릿 배치
            Rigidbody body = root.AddComponent<Rigidbody>(); // 포물선 비행용 Rigidbody 추가
            body.mass = 0.08f; // 가벼운 볼트 질량 설정
            body.useGravity = true; // 포물선 비행 중 중력 사용 설정
            body.interpolation = RigidbodyInterpolation.Interpolate; // 빠른 비행 보간 활성화
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // 빠른 볼트 충돌 보강
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>(); // 볼트 충돌 Collider 추가
            collider.direction = 2; // 볼트 길이 축을 로컬 Z 방향으로 설정
            collider.radius = 0.035f; // 볼트 얇은 충돌 반경 설정
            collider.height = 0.92f; // 볼트 전체 충돌 길이 설정
            CreatePrimitivePart(root.transform, "Shaft", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.025f, 0.42f, 0.025f), wood, new Vector3(90f, 0f, 0f)); // 볼트 목재 샤프트 생성
            CreatePrimitivePart(root.transform, "Head", PrimitiveType.Cube, new Vector3(0f, 0f, 0.48f), new Vector3(0.09f, 0.07f, 0.16f), steel, new Vector3(45f, 0f, 0f)); // 볼트 철제 촉 생성
            CreatePrimitivePart(root.transform, "Fletching_L", PrimitiveType.Cube, new Vector3(-0.055f, 0f, -0.36f), new Vector3(0.08f, 0.02f, 0.18f), brass, Vector3.zero); // 왼쪽 깃 장식 생성
            CreatePrimitivePart(root.transform, "Fletching_R", PrimitiveType.Cube, new Vector3(0.055f, 0f, -0.36f), new Vector3(0.08f, 0.02f, 0.18f), brass, Vector3.zero); // 오른쪽 깃 장식 생성
            CrossbowBoltProjectile projectile = root.AddComponent<CrossbowBoltProjectile>(); // Damage Pipeline·박힘·F 회수 기능 추가
            root.SetActive(false); // 런타임 복제 전 원본 템플릿 비활성화
            return projectile; // 석궁 연결용 볼트 템플릿 반환
        }

        private static void CreateRangedTargets(Transform parent, Material targetMaterial, Material trimMaterial, Material standMaterial) // SprintLane 남쪽 6m·10m·13m 사격 표적 생성
        {
            CreateTarget(parent, "RangedTarget_06m", RangeCenter + new Vector3(0f, 0f, 3.2f), 120f, targetMaterial, trimMaterial, standMaterial); // 가까운 6m급 피해 시험 표적 생성
            CreateTarget(parent, "RangedTarget_10m", RangeCenter + new Vector3(0f, 0f, 7.1f), 150f, targetMaterial, trimMaterial, standMaterial); // 중거리 10m급 피해 시험 표적 생성
            CreateTarget(parent, "RangedTarget_13m", RangeCenter + new Vector3(0f, 0f, 10.1f), 180f, targetMaterial, trimMaterial, standMaterial); // 장거리 13m급 피해 시험 표적 생성
        }

        private static void CreateTarget(Transform parent, string objectName, Vector3 position, float healthValue, Material bodyMaterial, Material trimMaterial, Material standMaterial) // 공통 Damage Pipeline 원거리 사격 표적 생성
        {
            GameObject root = new GameObject(objectName); // 표적 기능 루트 생성
            root.transform.SetParent(parent); // Day16 시험장 아래 배치
            root.transform.position = position; // SprintLane 지정 거리 위치 적용
            BoxCollider collider = root.AddComponent<BoxCollider>(); // 총알·볼트 피격 Collider 추가
            collider.center = new Vector3(0f, 1.25f, 0f); // 표적 판 중심 높이 지정
            collider.size = new Vector3(1.45f, 2.3f, 0.18f); // 넓은 원거리 표적 충돌 크기 설정
            CombatHealth health = root.AddComponent<CombatHealth>(); // 공통 Damage Pipeline 체력 추가
            health.Configure(objectName, CombatFaction.Enemy, healthValue); // 적 진영과 거리별 높은 체력 설정
            CombatReaction reaction = root.AddComponent<CombatReaction>(); // 석궁·리볼버 경직·넉백 시험 기능 추가
            reaction.Configure(45f, 8f, 0.20f, 0.35f, 6f, 0.55f); // 원거리 표적 기본 경직·넉백 반응 설정
            CreatePrimitivePart(root.transform, "Stand", PrimitiveType.Cube, new Vector3(0f, 0.55f, 0.15f), new Vector3(0.15f, 1.10f, 0.15f), standMaterial, Vector3.zero); // 표적 뒤 지지대 생성
            CreatePrimitivePart(root.transform, "Board", PrimitiveType.Cube, new Vector3(0f, 1.35f, 0f), new Vector3(1.40f, 1.80f, 0.12f), bodyMaterial, Vector3.zero); // 붉은 표적 판 생성
            CreatePrimitivePart(root.transform, "Center", PrimitiveType.Cylinder, new Vector3(0f, 1.40f, -0.08f), new Vector3(0.28f, 0.03f, 0.28f), trimMaterial, new Vector3(90f, 0f, 0f)); // 밝은 원형 중심 표식 생성
            EditorUtility.SetDirty(health); // 표적 체력 씬 저장 대상으로 표시
            EditorUtility.SetDirty(reaction); // 표적 반응 씬 저장 대상으로 표시
        }

        private static GameObject CreateBox(Transform parent, string objectName, Vector3 position, Vector3 scale, Material material, bool keepCollider) // 단순 상자형 월드 요소 생성
        {
            GameObject created = GameObject.CreatePrimitive(PrimitiveType.Cube); // Cube 프리미티브 생성
            created.name = objectName; // 오브젝트 이름 지정
            created.transform.SetParent(parent); // 지정 부모 아래 배치
            created.transform.position = position; // 월드 위치 지정
            created.transform.localScale = scale; // 크기 지정
            ApplyMaterial(created, material); // 지정 재질 적용

            if (!keepCollider) // 장식용 Collider 제거 여부 확인
            {
                Object.DestroyImmediate(created.GetComponent<Collider>()); // 불필요한 Collider 제거
            }

            return created; // 생성 오브젝트 반환
        }

        private static GameObject CreatePrimitivePart(Transform parent, string objectName, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material, Vector3 localEuler) // 무기·표적 세부 프리미티브 파트 생성
        {
            GameObject part = GameObject.CreatePrimitive(type); // 지정 형태 프리미티브 생성
            part.name = objectName; // 세부 부품 이름 지정
            part.transform.SetParent(parent, false); // 모델 시각 루트 아래 로컬 배치
            part.transform.localPosition = localPosition; // 부품 로컬 위치 지정
            part.transform.localScale = localScale; // 부품 로컬 크기 지정
            part.transform.localEulerAngles = localEuler; // 부품 로컬 회전 지정
            ApplyMaterial(part, material); // 부품 재질 적용
            Object.DestroyImmediate(part.GetComponent<Collider>()); // 루트 기능 Collider와 중복되는 장식 Collider 제거
            return part; // 생성 부품 반환
        }

        private static void CreateBar(Transform parent, string objectName, Vector3 start, Vector3 end, float thickness, Material material) // 두 로컬 지점 사이 얇은 막대·시위 생성
        {
            Vector3 center = (start + end) * 0.5f; // 두 지점 중간 위치 계산
            Vector3 direction = end - start; // 막대 방향 벡터 계산
            float length = direction.magnitude; // 두 지점 사이 길이 계산
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // 얇은 Cylinder 막대 생성
            bar.name = objectName; // 막대 이름 지정
            bar.transform.SetParent(parent, false); // 지정 시각 부모 아래 배치
            bar.transform.localPosition = center; // 두 지점 중앙에 위치 지정
            bar.transform.localScale = new Vector3(thickness, length * 0.5f, thickness); // Cylinder Y축 길이를 두 지점 거리와 일치
            bar.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized); // Cylinder Y축을 실제 두 지점 방향으로 회전
            ApplyMaterial(bar, material); // 시위·막대 재질 적용
            Object.DestroyImmediate(bar.GetComponent<Collider>()); // 장식 막대 Collider 제거
        }

        private static Material GetOrCreateMaterial(string materialName, Color color, float metallic, float smoothness) // URP Lit 기반 Day16 재질 생성 또는 갱신
        {
            string path = $"{MaterialFolder}/{materialName}.mat"; // 재질 에셋 경로 계산
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 재질 조회

            if (material == null) // 기존 재질 누락 여부 확인
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // URP Lit 셰이더 조회
                material = new Material(shader); // 새 재질 생성
                material.name = materialName; // 재질 이름 지정
                AssetDatabase.CreateAsset(material, path); // 프로젝트 에셋으로 저장
            }

            material.color = color; // 재질 기본 색상 적용
            material.SetFloat("_Metallic", Mathf.Clamp01(metallic)); // 금속성 값 적용
            material.SetFloat("_Smoothness", Mathf.Clamp01(smoothness)); // 표면 매끄러움 적용
            EditorUtility.SetDirty(material); // 변경 재질 저장 대상으로 표시
            return material; // 구성 재질 반환
        }

        private static void ApplyMaterial(GameObject target, Material material) // Renderer에 공유 재질 적용
        {
            Renderer renderer = target == null ? null : target.GetComponent<Renderer>(); // 대상 Renderer 조회

            if (renderer != null && material != null) // Renderer·재질 유효성 확인
            {
                renderer.sharedMaterial = material; // 생성 모델에 공유 재질 지정
            }
        }

        private static void EnsureAssetFolder(string folderPath) // 중첩 Assets 폴더 존재 보장
        {
            string[] parts = folderPath.Split('/'); // 경로를 폴더 단계로 분리
            string current = parts[0]; // Assets 루트를 현재 경로로 초기화

            for (int index = 1; index < parts.Length; index++) // 하위 폴더 단계 순회
            {
                string next = $"{current}/{parts[index]}"; // 다음 전체 폴더 경로 계산

                if (!AssetDatabase.IsValidFolder(next)) // 해당 하위 폴더 존재 여부 확인
                {
                    AssetDatabase.CreateFolder(current, parts[index]); // 누락 폴더 생성
                }

                current = next; // 다음 반복용 현재 경로 갱신
            }
        }

        private static void RemoveExistingRoot(Scene scene, string rootName) // 이름이 같은 기존 자동 생성 루트 제거
        {
            GameObject existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == rootName); // 대상 루트 이름 조회

            if (existing != null) // 기존 루트 존재 여부 확인
            {
                Object.DestroyImmediate(existing); // 이전 자동 생성 구조 제거
            }
        }
    }
}
