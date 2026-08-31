using System.IO; // 자동 생성 에셋 폴더와 씬 파일 확인 기능 참조
using System.Linq; // 씬 루트와 기존 오브젝트 검색 기능 참조
using ProjectI.Combat; // Day15 근접 전투 기능 참조
using ProjectI.Diagnostics; // F1 Combat 진단 페이지 참조
using ProjectI.Items; // 기존 WorldItem·CarryType 참조
using ProjectI.Player; // 기존 플레이어 전투 기반 참조
using UnityEditor; // 유니티 에디터 메뉴와 에셋 생성 기능 참조
using UnityEditor.SceneManagement; // 씬 열기와 저장 기능 참조
using UnityEngine; // 유니티 오브젝트·재질·물리 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 자동 구성 도구 네임스페이스
{
    [InitializeOnLoad] // 컴파일 완료 후 Day15 근접 전투 자동 구성
    public static class Phase4Day15Setup // 검·도끼 단발 공격과 경직·넉백 시험장 자동 구성
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string Day14RootName = "===Day14 Combat Foundation==="; // 선행 Day14 전투 시험장 루트 이름
        private const string Day15RootName = "===Day15 Single Melee Combat==="; // Day15 근접 전투 시험장 루트 이름
        private const string ReadyMarkerName = "===Day15 Single Melee Combat Ready v5==="; // 검·도끼 베기 방향 반전 보정 버전 자동 구성 완료 마커 이름
        private const string LegacyReadyMarkerName = "===Day15 Single Melee Combat Ready v4==="; // 이전 단순 휘두르기 버전 완료 마커 이름
        private const string MaterialFolder = "Assets/ProjectI/Art/Generated/Day15"; // Day15 무기·더미 재질 생성 폴더
        private const string AttackAssetFolder = "Assets/ProjectI/Resources/Combat"; // 공통 공격 데이터 생성 폴더
        private const string SwordAttackPath = AttackAssetFolder + "/Day15_SwordSlash.asset"; // Day15 검 단발 공격 데이터 경로
        private const string AxeAttackPath = AttackAssetFolder + "/Day15_AxeSwing.asset"; // Day15 도끼 단발 공격 데이터 경로
        private static readonly Vector3 CombatRangeCenter = new Vector3(-27f, 0f, 2f); // Day3 01_SprintLane 파란 테스트 구역 중심 위치

        static Phase4Day15Setup() // 자동 설정 등록
        {
            EditorApplication.delayCall += TryAutoApply; // 스크립트 컴파일 완료 후 Day15 자동 구성 예약
        }

        [MenuItem("Tools/Project I/Day 15/Apply Single Melee Combat")] // 수동 Day15 근접 전투 구성 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 Day15 전체 구성 실행
        {
            ApplyDay15(true, true); // 강제 재구성과 결과 대화상자 활성화
        }

        private static void TryAutoApply() // 컴파일 완료 후 자동 구성 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // Batch 또는 Play 전환 상태 확인
            {
                return; // 자동 구성 제외 상태에서는 중단
            }

            ApplyDay15(false, false); // 완료 마커가 없을 때 한 번 자동 적용
        }

        private static void ApplyDay15(bool showDialog, bool force) // Day15 단발 근접 전투 전체 적용
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 탐사 씬 존재 여부 확인
            {
                return; // 대상 씬 누락 시 자동 구성 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 단독 열기
            GameObject day14Root = scene.GetRootGameObjects().FirstOrDefault(root => root.name == Day14RootName); // 선행 Day14 전투 시험장 루트 조회
            GameObject existingMarker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 Day15 완료 마커 조회

            if (!force && existingMarker != null) // 이미 Day15 자동 구성이 적용됐는지 확인
            {
                return; // 반복 자동 적용 방지
            }

            if (day14Root == null) // 선행 공통 전투 기반 존재 여부 확인
            {
                Debug.LogError("[Project I] Day15 구성 전에 Day14 Combat Foundation이 필요합니다."); // 선행 전투 기반 누락 오류 출력
                return; // Day15 구성 중단
            }

            PlayerInputReader inputReader = Object.FindFirstObjectByType<PlayerInputReader>(); // 기존 싱글 플레이어 입력 래퍼 조회
            GameObject player = inputReader == null ? null : inputReader.gameObject; // 기존 플레이어 루트 조회
            CombatController combatController = player == null ? null : player.GetComponent<CombatController>(); // Day14 공통 전투 제어기 조회

            if (player == null || combatController == null) // 플레이어와 공통 전투 기반 존재 여부 확인
            {
                Debug.LogError("[Project I] Day15 구성 전에 Player와 CombatController가 필요합니다."); // 선행 플레이어 전투 기반 누락 오류 출력
                return; // Day15 구성 중단
            }

            RemoveExistingRoot(scene, Day15RootName); // 기존 Day15 시험장 루트 제거
            RemoveExistingRoot(scene, ReadyMarkerName); // 현재 Day15 완료 마커 제거
            RemoveExistingRoot(scene, LegacyReadyMarkerName); // 이전 단순 휘두르기 버전 완료 마커 제거
            RemoveExistingRoot(scene, "===Day15 Single Melee Combat Ready v3==="); // 이전 들기 각도 보정 버전 완료 마커 제거
            RemoveExistingRoot(scene, "===Day15 Single Melee Combat Ready v2==="); // 파란 구역 첫 이동 버전 완료 마커 제거
            RemoveExistingRoot(scene, "===Day15 Single Melee Combat Ready==="); // 초기 Day15 완료 마커도 함께 제거
            RemoveLegacyDay14TestSwords(); // Day14 임시 테스트 검을 Day15 정식 검·도끼로 교체
            EnsureAssetFolder(MaterialFolder); // Day15 재질 폴더 존재 보장
            EnsureAssetFolder(AttackAssetFolder); // 공통 공격 데이터 폴더 존재 보장
            AttackDefinition swordAttack = GetOrCreateSwordAttack(); // 검 단발 공격 데이터 생성 또는 갱신
            AttackDefinition axeAttack = GetOrCreateAxeAttack(); // 도끼 단발 공격 데이터 생성 또는 갱신
            Material swordSteel = GetOrCreateMaterial("Melee_SwordSteel", new Color(0.52f, 0.55f, 0.58f), 0.88f, 0.78f); // 검날 밝은 강철 재질 생성
            Material darkSteel = GetOrCreateMaterial("Melee_DarkSteel", new Color(0.11f, 0.12f, 0.13f), 0.82f, 0.55f); // 무기 어두운 철제 부품 재질 생성
            Material leather = GetOrCreateMaterial("Melee_Leather", new Color(0.12f, 0.065f, 0.035f), 0.05f, 0.28f); // 검·도끼 가죽 손잡이 재질 생성
            Material wood = GetOrCreateMaterial("Melee_Wood", new Color(0.24f, 0.12f, 0.045f), 0.02f, 0.23f); // 도끼 목재 자루 재질 생성
            Material brass = GetOrCreateMaterial("Melee_Brass", new Color(0.42f, 0.27f, 0.07f), 0.72f, 0.52f); // 검 장식 황동 재질 생성
            Material axeSteel = GetOrCreateMaterial("Melee_AxeSteel", new Color(0.30f, 0.32f, 0.34f), 0.84f, 0.58f); // 도끼날 중량감 강철 재질 생성
            Material standMaterial = GetOrCreateMaterial("Melee_Stand", new Color(0.10f, 0.105f, 0.11f), 0.60f, 0.32f); // 무기 전시대 철제 재질 생성
            Material accentMaterial = GetOrCreateMaterial("Melee_Accent", new Color(0.63f, 0.37f, 0.045f), 0.45f, 0.40f); // 무기 구역 황동색 강조 재질 생성
            Material heavyBodyMaterial = GetOrCreateMaterial("Melee_HeavyBody", new Color(0.25f, 0.06f, 0.045f), 0.12f, 0.28f); // 중장 더미 붉은 몸체 재질 생성
            Material heavyArmorMaterial = GetOrCreateMaterial("Melee_HeavyArmor", new Color(0.18f, 0.19f, 0.20f), 0.72f, 0.36f); // 중장 더미 갑옷 재질 생성
            GameObject day15Root = new GameObject(Day15RootName); // Day15 근접 전투 시험장 루트 생성
            CreateWeaponDisplayArea(day15Root.transform, swordAttack, axeAttack, swordSteel, darkSteel, leather, wood, brass, axeSteel, standMaterial, accentMaterial); // 검·도끼 모델과 월드 아이템 전시 공간 생성
            ConfigureExistingCombatReactions(); // Day14 기존 더미에 경직·넉백 반응 기능 연결
            CreateHeavyReactionDummy(day15Root.transform, heavyBodyMaterial, heavyArmorMaterial, standMaterial); // 높은 경직·넉백 저항 시험용 중장 더미 생성
            CombatDebugPage debugPage = Object.FindFirstObjectByType<CombatDebugPage>(); // 기존 F1 Combat 진단 페이지 조회

            if (debugPage != null) // 기존 진단 페이지 존재 여부 확인
            {
                debugPage.Configure(combatController); // 변경된 단발 공격과 반응 시스템 기준으로 진단 대상 재연결
                EditorUtility.SetDirty(debugPage); // 진단 페이지 씬 저장 대상으로 표시
            }

            GameObject marker = new GameObject(ReadyMarkerName); // Day15 자동 적용 완료 마커 생성
            EditorUtility.SetDirty(combatController); // 쿨타임 확장 전투 제어기 씬 저장 대상으로 표시
            EditorUtility.SetDirty(marker); // 완료 마커 씬 저장 대상으로 표시
            EditorSceneManager.MarkSceneDirty(scene); // 변경된 탐사 씬 저장 상태 표시
            EditorSceneManager.SaveScene(scene); // Day15 검·도끼·반응 시험장 저장
            AssetDatabase.SaveAssets(); // 공격 데이터와 재질 에셋 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 상태 갱신
            bool validationPassed = Phase4Day15Validator.Validate(false); // Day15 정적 구조와 단발 공격 규칙 검증 실행

            if (showDialog) // 수동 실행 결과 표시 여부 확인
            {
                string message = validationPassed ? "Day15 검·도끼 단발 공격과 경직·넉백 구성이 완료되었습니다." : "Day15 검증 실패 - Console을 확인하세요."; // 구성 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 완료 또는 실패 결과 대화상자 출력
            }
        }

        private static void CreateWeaponDisplayArea(Transform parent, AttackDefinition swordAttack, AttackDefinition axeAttack, Material swordSteel, Material darkSteel, Material leather, Material wood, Material brass, Material axeSteel, Material standMaterial, Material accentMaterial) // 검·도끼 월드 배치와 상세 모델 생성
        {
            Vector3 swordStand = CombatRangeCenter + new Vector3(-2.2f, 0f, -3.35f); // 검 전시대 기준 위치 계산
            Vector3 axeStand = CombatRangeCenter + new Vector3(2.2f, 0f, -3.35f); // 도끼 전시대 기준 위치 계산
            CreateWeaponStand(parent, "Sword_DisplayStand", swordStand, standMaterial, accentMaterial); // 검용 철제 전시대 생성
            CreateWeaponStand(parent, "Axe_DisplayStand", axeStand, standMaterial, accentMaterial); // 도끼용 철제 전시대 생성
            CreateDetailedSword(parent, swordStand + new Vector3(0f, 0.76f, 0f), swordAttack, swordSteel, darkSteel, leather, brass); // 전시대 위 상세 검 월드 아이템 생성
            CreateDetailedAxe(parent, axeStand + new Vector3(0f, 0.80f, 0f), axeAttack, axeSteel, darkSteel, wood, leather); // 전시대 위 상세 도끼 월드 아이템 생성
            CreateBox(parent, "Weapon_DisplayBackRail", CombatRangeCenter + new Vector3(0f, 0.95f, -4.15f), new Vector3(8.8f, 0.10f, 0.10f), standMaterial, false); // 검·도끼 전시 공간 뒤쪽 레일 장식 생성
            CreateBox(parent, "Weapon_DisplayAccent", CombatRangeCenter + new Vector3(0f, 0.08f, -3.35f), new Vector3(8.6f, 0.04f, 2.0f), accentMaterial, false); // 무기 전시 구역 바닥 강조 표식 생성
        }

        private static void CreateWeaponStand(Transform parent, string objectName, Vector3 position, Material standMaterial, Material accentMaterial) // 무기 하나를 올려둘 낮은 철제 받침 생성
        {
            CreateBox(parent, objectName + "_Base", position + new Vector3(0f, 0.16f, 0f), new Vector3(3.0f, 0.28f, 1.55f), standMaterial, true); // 무기 전시대 본체 생성
            CreateBox(parent, objectName + "_Top", position + new Vector3(0f, 0.34f, 0f), new Vector3(2.70f, 0.08f, 1.30f), accentMaterial, true); // 무기 전시대 상판 생성
            CreateBox(parent, objectName + "_FrontTrim", position + new Vector3(0f, 0.29f, -0.73f), new Vector3(2.65f, 0.18f, 0.08f), accentMaterial, false); // 전시대 전면 강조 장식 생성
        }

        private static void CreateDetailedSword(Transform parent, Vector3 position, AttackDefinition attackDefinition, Material bladeMaterial, Material darkSteelMaterial, Material leatherMaterial, Material brassMaterial) // Day15 상세 검 월드 아이템 생성
        {
            GameObject sword = new GameObject("Day15_IronSword"); // 검 기능 루트 생성
            sword.transform.SetParent(parent); // Day15 시험장 아래 배치
            sword.transform.position = position; // 검 전시대 월드 위치 지정
            sword.transform.rotation = Quaternion.Euler(0f, 0f, 90f); // 전시대 위 수평 배치 자세 지정
            Rigidbody body = sword.AddComponent<Rigidbody>(); // 기존 WorldItem 필수 월드 물리 Rigidbody 추가
            body.mass = 2.2f; // 철제 검 질량 설정
            BoxCollider pickupCollider = sword.AddComponent<BoxCollider>(); // 월드 획득과 물리 충돌용 대표 Collider 추가
            pickupCollider.center = new Vector3(0f, 0.84f, 0f); // 검 전체 형태 기준 Collider 중심 지정
            pickupCollider.size = new Vector3(0.32f, 1.95f, 0.28f); // 검 전체 형태 기준 Collider 크기 지정
            WorldItem worldItem = sword.AddComponent<WorldItem>(); // 기존 F 획득·빠른 슬롯 기능 추가
            worldItem.Configure("Iron Sword", 0.22f, CarryType.OneHand); // 한손 검 아이템 설정
            worldItem.ConfigureCarryPose(CarryType.OneHand, new Vector3(0.24f, -0.24f, 0.19f), new Vector3(14f, 72f, 2f)); // 1인칭 화면 오른쪽에서 검 날 면이 앞을 보도록 세운 포즈 설정
            MeleeWeaponTrace trace = sword.AddComponent<MeleeWeaponTrace>(); // 검날 근접 궤적 검사 기능 추가
            MeleeWeaponItem weaponItem = sword.AddComponent<MeleeWeaponItem>(); // 좌클릭 단발 공격 기능 추가
            GameObject visualPivotObject = new GameObject("VisualPivot"); // 공격 중 검 전체를 회전할 시각 피벗 생성
            visualPivotObject.transform.SetParent(sword.transform, false); // 검 기능 루트 아래 시각 피벗 배치
            Transform visualPivot = visualPivotObject.transform; // 검 시각 피벗 Transform 저장
            CreatePrimitivePart(visualPivot, "Pommel_Core", PrimitiveType.Sphere, new Vector3(0f, -0.09f, 0f), new Vector3(0.22f, 0.22f, 0.22f), darkSteelMaterial, Vector3.zero); // 검 손잡이 끝 철제 폼멜 생성
            CreatePrimitivePart(visualPivot, "Pommel_BrassRing", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.16f, 0.035f, 0.16f), brassMaterial, Vector3.zero); // 폼멜 황동 링 장식 생성
            CreatePrimitivePart(visualPivot, "Grip_Core", PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f), new Vector3(0.125f, 0.25f, 0.125f), leatherMaterial, Vector3.zero); // 가죽 검 손잡이 본체 생성

            for (int index = 0; index < 6; index++) // 검 손잡이 가죽 랩 장식 반복
            {
                float y = 0.07f + (index * 0.075f); // 각 가죽 랩 높이 계산
                CreatePrimitivePart(visualPivot, $"Grip_Wrap_{index + 1:00}", PrimitiveType.Cylinder, new Vector3(0f, y, 0f), new Vector3(0.142f, 0.014f, 0.142f), darkSteelMaterial, Vector3.zero); // 얇은 손잡이 감기 장식 생성
            }

            CreatePrimitivePart(visualPivot, "Guard_Center", PrimitiveType.Cube, new Vector3(0f, 0.53f, 0f), new Vector3(0.42f, 0.11f, 0.18f), darkSteelMaterial, Vector3.zero); // 검 가드 중앙 블록 생성
            CreatePrimitivePart(visualPivot, "Guard_Left", PrimitiveType.Cube, new Vector3(-0.37f, 0.55f, 0f), new Vector3(0.42f, 0.085f, 0.14f), darkSteelMaterial, new Vector3(0f, 0f, -11f)); // 왼쪽 퀼론 가드 생성
            CreatePrimitivePart(visualPivot, "Guard_Right", PrimitiveType.Cube, new Vector3(0.37f, 0.55f, 0f), new Vector3(0.42f, 0.085f, 0.14f), darkSteelMaterial, new Vector3(0f, 0f, 11f)); // 오른쪽 퀼론 가드 생성
            CreatePrimitivePart(visualPivot, "Blade_Collar", PrimitiveType.Cube, new Vector3(0f, 0.64f, 0f), new Vector3(0.22f, 0.16f, 0.15f), brassMaterial, Vector3.zero); // 가드 위 블레이드 칼라 생성
            CreatePrimitivePart(visualPivot, "Blade_Core", PrimitiveType.Cube, new Vector3(0f, 1.22f, 0f), new Vector3(0.17f, 1.12f, 0.075f), bladeMaterial, Vector3.zero); // 검날 중앙 몸체 생성
            CreatePrimitivePart(visualPivot, "Blade_Edge_L", PrimitiveType.Cube, new Vector3(-0.095f, 1.22f, 0f), new Vector3(0.030f, 1.14f, 0.085f), bladeMaterial, Vector3.zero); // 왼쪽 날 엣지 생성
            CreatePrimitivePart(visualPivot, "Blade_Edge_R", PrimitiveType.Cube, new Vector3(0.095f, 1.22f, 0f), new Vector3(0.030f, 1.14f, 0.085f), bladeMaterial, Vector3.zero); // 오른쪽 날 엣지 생성
            CreatePrimitivePart(visualPivot, "Blade_Fuller", PrimitiveType.Cube, new Vector3(0f, 1.19f, -0.045f), new Vector3(0.045f, 0.92f, 0.012f), darkSteelMaterial, Vector3.zero); // 검날 중앙 홈을 표현하는 어두운 풀러 생성
            CreatePrimitivePart(visualPivot, "Blade_Tip", PrimitiveType.Cube, new Vector3(0f, 1.82f, 0f), new Vector3(0.14f, 0.22f, 0.065f), bladeMaterial, new Vector3(0f, 0f, 45f)); // 마름모 형태 검 끝 모델 생성
            GameObject startPoint = new GameObject("TraceStart"); // 검날 하단 궤적 기준점 생성
            startPoint.transform.SetParent(visualPivot, false); // 검 시각 피벗 아래 시작점 배치
            startPoint.transform.localPosition = new Vector3(0f, 0.67f, 0f); // 블레이드 칼라 위 시작점 지정
            GameObject endPoint = new GameObject("TraceEnd"); // 검날 끝 궤적 기준점 생성
            endPoint.transform.SetParent(visualPivot, false); // 검 시각 피벗 아래 끝점 배치
            endPoint.transform.localPosition = new Vector3(0f, 1.96f, 0f); // 검 끝보다 약간 바깥쪽 끝점 지정
            trace.Configure(startPoint.transform, endPoint.transform, ~0); // 전체 물리 Layer를 대상으로 검 궤적 구성
            weaponItem.Configure(attackDefinition, trace, visualPivot); // 검 단발 공격 데이터와 시각 피벗 연결
            EditorUtility.SetDirty(worldItem); // 검 월드 아이템 설정 씬 저장 대상으로 표시
            EditorUtility.SetDirty(trace); // 검 궤적 설정 씬 저장 대상으로 표시
            EditorUtility.SetDirty(weaponItem); // 검 공격 설정 씬 저장 대상으로 표시
        }

        private static void CreateDetailedAxe(Transform parent, Vector3 position, AttackDefinition attackDefinition, Material axeSteelMaterial, Material darkSteelMaterial, Material woodMaterial, Material leatherMaterial) // Day15 상세 도끼 월드 아이템 생성
        {
            GameObject axe = new GameObject("Day15_IronAxe"); // 도끼 기능 루트 생성
            axe.transform.SetParent(parent); // Day15 시험장 아래 배치
            axe.transform.position = position; // 도끼 전시대 월드 위치 지정
            axe.transform.rotation = Quaternion.Euler(0f, 0f, 90f); // 전시대 위 수평 배치 자세 지정
            Rigidbody body = axe.AddComponent<Rigidbody>(); // 기존 WorldItem 필수 월드 물리 Rigidbody 추가
            body.mass = 3.8f; // 중량 도끼 질량 설정
            BoxCollider pickupCollider = axe.AddComponent<BoxCollider>(); // 월드 획득과 물리 충돌용 대표 Collider 추가
            pickupCollider.center = new Vector3(-0.12f, 0.78f, 0f); // 도끼 전체 형태 기준 Collider 중심 지정
            pickupCollider.size = new Vector3(0.82f, 1.75f, 0.34f); // 도끼 자루와 날을 포함한 Collider 크기 지정
            WorldItem worldItem = axe.AddComponent<WorldItem>(); // 기존 F 획득·빠른 슬롯 기능 추가
            worldItem.Configure("Iron Axe", 0.26f, CarryType.OneHand); // 한손 도끼 아이템 설정
            worldItem.ConfigureCarryPose(CarryType.OneHand, new Vector3(0.27f, -0.25f, 0.18f), new Vector3(12f, 74f, -2f)); // 1인칭 화면 오른쪽에서 도끼 날이 앞을 보도록 세운 포즈 설정
            MeleeWeaponTrace trace = axe.AddComponent<MeleeWeaponTrace>(); // 도끼날 근접 궤적 검사 기능 추가
            MeleeWeaponItem weaponItem = axe.AddComponent<MeleeWeaponItem>(); // 좌클릭 단발 공격 기능 추가
            GameObject visualPivotObject = new GameObject("VisualPivot"); // 공격 중 도끼 전체를 회전할 시각 피벗 생성
            visualPivotObject.transform.SetParent(axe.transform, false); // 도끼 기능 루트 아래 시각 피벗 배치
            Transform visualPivot = visualPivotObject.transform; // 도끼 시각 피벗 Transform 저장
            CreatePrimitivePart(visualPivot, "Handle_Core", PrimitiveType.Cylinder, new Vector3(0f, 0.70f, 0f), new Vector3(0.115f, 0.72f, 0.115f), woodMaterial, Vector3.zero); // 긴 목재 도끼 자루 본체 생성
            CreatePrimitivePart(visualPivot, "Handle_Butt", PrimitiveType.Cylinder, new Vector3(0f, -0.04f, 0f), new Vector3(0.145f, 0.08f, 0.145f), darkSteelMaterial, Vector3.zero); // 도끼 자루 끝 철제 캡 생성

            for (int index = 0; index < 5; index++) // 도끼 하단 가죽 손잡이 랩 반복
            {
                float y = 0.08f + (index * 0.085f); // 각 가죽 랩 높이 계산
                CreatePrimitivePart(visualPivot, $"Handle_Wrap_{index + 1:00}", PrimitiveType.Cylinder, new Vector3(0f, y, 0f), new Vector3(0.135f, 0.018f, 0.135f), leatherMaterial, Vector3.zero); // 도끼 손잡이 가죽 감기 장식 생성
            }

            CreatePrimitivePart(visualPivot, "Head_Socket", PrimitiveType.Cylinder, new Vector3(0f, 1.43f, 0f), new Vector3(0.17f, 0.17f, 0.17f), darkSteelMaterial, new Vector3(0f, 0f, 90f)); // 도끼머리와 자루를 고정하는 철제 소켓 생성
            CreatePrimitivePart(visualPivot, "Head_Core", PrimitiveType.Cube, new Vector3(-0.02f, 1.46f, 0f), new Vector3(0.44f, 0.30f, 0.25f), axeSteelMaterial, Vector3.zero); // 도끼머리 중앙 중량 블록 생성
            CreatePrimitivePart(visualPivot, "Axe_Cheek", PrimitiveType.Cube, new Vector3(-0.38f, 1.47f, 0f), new Vector3(0.48f, 0.44f, 0.16f), axeSteelMaterial, new Vector3(0f, 0f, -8f)); // 도끼날 넓은 볼 부분 생성
            CreatePrimitivePart(visualPivot, "Axe_Edge", PrimitiveType.Cube, new Vector3(-0.66f, 1.48f, 0f), new Vector3(0.10f, 0.52f, 0.18f), axeSteelMaterial, new Vector3(0f, 0f, -8f)); // 넓은 절삭날 엣지 생성
            CreatePrimitivePart(visualPivot, "Head_Poll", PrimitiveType.Cube, new Vector3(0.35f, 1.46f, 0f), new Vector3(0.30f, 0.22f, 0.22f), darkSteelMaterial, Vector3.zero); // 도끼머리 뒤쪽 폴 생성
            CreatePrimitivePart(visualPivot, "Head_TopBand", PrimitiveType.Cube, new Vector3(-0.03f, 1.64f, 0f), new Vector3(0.46f, 0.07f, 0.27f), darkSteelMaterial, Vector3.zero); // 도끼머리 상단 보강 밴드 생성
            GameObject startPoint = new GameObject("TraceStart"); // 도끼날 안쪽 궤적 기준점 생성
            startPoint.transform.SetParent(visualPivot, false); // 도끼 시각 피벗 아래 시작점 배치
            startPoint.transform.localPosition = new Vector3(-0.24f, 1.28f, 0f); // 도끼날 안쪽 아래 기준 위치 지정
            GameObject endPoint = new GameObject("TraceEnd"); // 도끼 절삭날 끝 궤적 기준점 생성
            endPoint.transform.SetParent(visualPivot, false); // 도끼 시각 피벗 아래 끝점 배치
            endPoint.transform.localPosition = new Vector3(-0.74f, 1.71f, 0f); // 도끼날 바깥 위쪽 끝 위치 지정
            trace.Configure(startPoint.transform, endPoint.transform, ~0); // 전체 물리 Layer를 대상으로 도끼 궤적 구성
            weaponItem.Configure(attackDefinition, trace, visualPivot); // 도끼 단발 공격 데이터와 시각 피벗 연결
            EditorUtility.SetDirty(worldItem); // 도끼 월드 아이템 설정 씬 저장 대상으로 표시
            EditorUtility.SetDirty(trace); // 도끼 궤적 설정 씬 저장 대상으로 표시
            EditorUtility.SetDirty(weaponItem); // 도끼 공격 설정 씬 저장 대상으로 표시
        }

        private static void ConfigureExistingCombatReactions() // Day14 더미에 경직·넉백 반응 기능 연결
        {
            CombatHealth[] targets = Object.FindObjectsByType<CombatHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 씬 공통 전투 대상 전체 조회

            foreach (CombatHealth target in targets) // 기존 전투 대상 순회
            {
                if (target == null) // 유효 대상 여부 확인
                {
                    continue; // 누락 대상 건너뜀
                }

                CombatReaction reaction = GetOrAddComponent<CombatReaction>(target.gameObject); // 기존 대상에 공통 경직·넉백 반응 기능 추가

                if (target.gameObject.name == "CombatDummy_EnemyBehindWall") // 벽 뒤 중량 더미 여부 확인
                {
                    reaction.Configure(80f, 10f, 0.30f, 0.65f, 9f, 0.46f); // 높은 경직 한계와 넉백 저항 적용
                }
                else if (target.Faction == CombatFaction.Ally) // 아군 Friendly Fire 시험 대상 여부 확인
                {
                    reaction.Configure(45f, 12f, 0.32f, 0.25f, 10f, 0.44f); // 중간 수준 반응 수치 적용
                }
                else // 일반 적 전투 시험 대상 처리
                {
                    reaction.Configure(30f, 12f, 0.35f, 0.10f, 12f, 0.44f); // 검 누적과 도끼 즉시 경직 비교용 기본 수치 적용
                }

                EditorUtility.SetDirty(reaction); // 기존 더미 반응 설정 씬 저장 대상으로 표시
            }
        }

        private static void CreateHeavyReactionDummy(Transform parent, Material bodyMaterial, Material armorMaterial, Material trimMaterial) // 높은 경직·넉백 저항 시험용 중장 더미 생성
        {
            GameObject root = new GameObject("CombatDummy_Heavy"); // 중장 더미 기능 루트 생성
            root.transform.SetParent(parent); // Day15 시험장 아래 배치
            root.transform.position = CombatRangeCenter + new Vector3(0f, 0f, 3.65f); // 기존 더미 뒤쪽 중앙에 배치
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>(); // 공통 근접 피격용 캡슐 Collider 추가
            collider.center = new Vector3(0f, 1.35f, 0f); // 중장 더미 몸통 중앙 높이 지정
            collider.height = 2.6f; // 중장 더미 전체 피격 높이 지정
            collider.radius = 0.58f; // 중장 더미 넓은 피격 반경 지정
            CombatHealth health = root.AddComponent<CombatHealth>(); // 공통 Damage Pipeline 체력 추가
            health.Configure("Heavy Combat Dummy", CombatFaction.Enemy, 180f); // 높은 체력의 적 진영 더미 구성
            CombatReaction reaction = root.AddComponent<CombatReaction>(); // 경직·넉백 반응 기능 추가
            reaction.Configure(80f, 9f, 0.28f, 0.65f, 8f, 0.55f); // 도끼와 검의 경직 차이를 확인할 높은 저항 설정
            CreatePrimitivePart(root.transform, "Pedestal", PrimitiveType.Cylinder, new Vector3(0f, 0.16f, 0f), new Vector3(1.45f, 0.16f, 1.45f), trimMaterial, Vector3.zero); // 중장 더미 하단 받침 생성
            CreatePrimitivePart(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 1.30f, 0f), new Vector3(0.82f, 1.00f, 0.82f), bodyMaterial, Vector3.zero); // 두꺼운 중장 더미 몸체 생성
            CreatePrimitivePart(root.transform, "ChestArmor", PrimitiveType.Cube, new Vector3(0f, 1.45f, -0.44f), new Vector3(1.02f, 0.78f, 0.16f), armorMaterial, Vector3.zero); // 전면 중장 갑옷판 생성
            CreatePrimitivePart(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 2.48f, 0f), new Vector3(0.68f, 0.68f, 0.68f), bodyMaterial, Vector3.zero); // 중장 더미 머리 생성
            CreatePrimitivePart(root.transform, "Helmet", PrimitiveType.Cube, new Vector3(0f, 2.56f, -0.05f), new Vector3(0.76f, 0.30f, 0.72f), armorMaterial, Vector3.zero); // 각진 철제 투구 생성
            CreatePrimitivePart(root.transform, "Shoulder_L", PrimitiveType.Sphere, new Vector3(-0.58f, 1.83f, 0f), new Vector3(0.38f, 0.30f, 0.42f), armorMaterial, Vector3.zero); // 왼쪽 중장 어깨판 생성
            CreatePrimitivePart(root.transform, "Shoulder_R", PrimitiveType.Sphere, new Vector3(0.58f, 1.83f, 0f), new Vector3(0.38f, 0.30f, 0.42f), armorMaterial, Vector3.zero); // 오른쪽 중장 어깨판 생성
            CreatePrimitivePart(root.transform, "Belt", PrimitiveType.Cube, new Vector3(0f, 0.88f, -0.39f), new Vector3(0.90f, 0.18f, 0.12f), armorMaterial, Vector3.zero); // 중장 더미 허리 철제 밴드 생성
            EditorUtility.SetDirty(health); // 중장 더미 체력 씬 저장 대상으로 표시
            EditorUtility.SetDirty(reaction); // 중장 더미 반응 설정 씬 저장 대상으로 표시
        }

        private static AttackDefinition GetOrCreateSwordAttack() // Day15 검 단발 공격 데이터 생성 또는 갱신
        {
            AttackDefinition definition = AssetDatabase.LoadAssetAtPath<AttackDefinition>(SwordAttackPath); // 기존 검 공격 에셋 조회

            if (definition == null) // 검 공격 에셋 누락 여부 확인
            {
                definition = ScriptableObject.CreateInstance<AttackDefinition>(); // 새 검 공격 ScriptableObject 생성
                AssetDatabase.CreateAsset(definition, SwordAttackPath); // 공통 Combat Resources에 검 공격 에셋 저장
            }

            definition.ConfigureDetailed("Iron Sword Slash", 25f, CombatDamageType.Physical, 10f, 0.12f, 0.16f, 0.20f, 0.65f, 0.65f, 0.11f, 10f, 1.4f, new Vector3(-14f, 6f, -26f), new Vector3(10f, -10f, 44f)); // 검의 준비·타격 Z 회전 부호를 뒤집어 실제 베기 방향 반전
            EditorUtility.SetDirty(definition); // 검 공격 데이터 에셋 저장 대상으로 표시
            return definition; // 구성된 검 공격 데이터 반환
        }

        private static AttackDefinition GetOrCreateAxeAttack() // Day15 도끼 단발 공격 데이터 생성 또는 갱신
        {
            AttackDefinition definition = AssetDatabase.LoadAssetAtPath<AttackDefinition>(AxeAttackPath); // 기존 도끼 공격 에셋 조회

            if (definition == null) // 도끼 공격 에셋 누락 여부 확인
            {
                definition = ScriptableObject.CreateInstance<AttackDefinition>(); // 새 도끼 공격 ScriptableObject 생성
                AssetDatabase.CreateAsset(definition, AxeAttackPath); // 공통 Combat Resources에 도끼 공격 에셋 저장
            }

            definition.ConfigureDetailed("Iron Axe Heavy Swing", 45f, CombatDamageType.Physical, 20f, 0.28f, 0.20f, 0.42f, 1.15f, 0.40f, 0.16f, 35f, 3.5f, new Vector3(-20f, 8f, -34f), new Vector3(18f, -12f, 56f)); // 도끼의 준비·타격 Z 회전 부호를 뒤집어 실제 베기 방향 반전
            EditorUtility.SetDirty(definition); // 도끼 공격 데이터 에셋 저장 대상으로 표시
            return definition; // 구성된 도끼 공격 데이터 반환
        }

        private static void RemoveLegacyDay14TestSwords() // Day14 임시 테스트 검 오브젝트 정리
        {
            MeleeWeaponItem[] weapons = Object.FindObjectsByType<MeleeWeaponItem>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 씬 근접 무기 전체 조회

            foreach (MeleeWeaponItem weapon in weapons) // 기존 근접 무기 순회
            {
                if (weapon != null && weapon.gameObject.name == "Day14_CombatTestSword") // Day14 임시 테스트 검 이름 확인
                {
                    Object.DestroyImmediate(weapon.gameObject); // Day15 정식 검·도끼와 겹치지 않도록 임시 검 삭제
                }
            }
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component // 기존 컴포넌트 재사용 또는 신규 추가
        {
            T component = target.GetComponent<T>(); // 대상 오브젝트의 기존 컴포넌트 조회

            if (component == null) // 대상 컴포넌트 누락 여부 확인
            {
                component = target.AddComponent<T>(); // 누락 컴포넌트 신규 추가
            }

            return component; // 기존 또는 신규 컴포넌트 반환
        }

        private static GameObject CreateBox(Transform parent, string objectName, Vector3 position, Vector3 scale, Material material, bool keepCollider) // 단순 박스 시험 구조 생성
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube); // Unity Cube Primitive 생성
            box.name = objectName; // 시험 구조 이름 지정
            box.transform.SetParent(parent); // 지정 부모 아래 배치
            box.transform.position = position; // 월드 위치 지정
            box.transform.localScale = scale; // 박스 크기 지정
            ApplyMaterial(box, material); // 지정 재질 적용

            if (!keepCollider) // 실제 물리 Collider 유지 여부 확인
            {
                Collider collider = box.GetComponent<Collider>(); // Primitive 기본 Collider 조회

                if (collider != null) // 기본 Collider 존재 여부 확인
                {
                    Object.DestroyImmediate(collider); // 장식용 박스 Collider 제거
                }
            }

            return box; // 생성된 박스 반환
        }

        private static GameObject CreatePrimitivePart(Transform parent, string objectName, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Material material, Vector3 localEuler) // 무기·더미 상세 모델 파트 생성
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType); // 지정 Unity Primitive 생성
            part.name = objectName; // 모델 파트 이름 지정
            part.transform.SetParent(parent, false); // 부모 기준 로컬 배치 설정
            part.transform.localPosition = localPosition; // 모델 파트 로컬 위치 지정
            part.transform.localRotation = Quaternion.Euler(localEuler); // 모델 파트 로컬 회전 지정
            part.transform.localScale = localScale; // 모델 파트 로컬 크기 지정
            ApplyMaterial(part, material); // 모델 파트 재질 적용
            Collider collider = part.GetComponent<Collider>(); // Primitive 자동 Collider 조회

            if (collider != null) // 장식 파트 Collider 존재 여부 확인
            {
                Object.DestroyImmediate(collider); // 대표 루트 Collider만 사용하도록 장식 Collider 제거
            }

            return part; // 생성된 모델 파트 반환
        }

        private static void ApplyMaterial(GameObject target, Material material) // Renderer에 생성 재질 적용
        {
            Renderer renderer = target.GetComponent<Renderer>(); // 대상 Renderer 조회

            if (renderer != null && material != null) // Renderer와 재질 유효성 확인
            {
                renderer.sharedMaterial = material; // 에디터 공유 재질 연결
            }
        }

        private static Material GetOrCreateMaterial(string materialName, Color color, float metallic, float smoothness) // Day15 URP 호환 재질 생성 또는 갱신
        {
            string path = $"{MaterialFolder}/{materialName}.mat"; // 생성 재질 에셋 경로 계산
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 동일 이름 재질 조회

            if (material == null) // 기존 재질 누락 여부 확인
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // URP Lit 셰이더 조회
                shader = shader == null ? Shader.Find("Standard") : shader; // URP 셰이더 누락 시 Standard 폴백
                material = new Material(shader); // 지정 셰이더 기반 새 재질 생성
                AssetDatabase.CreateAsset(material, path); // Day15 생성 재질 에셋 저장
            }

            material.color = color; // 기본 색상 설정

            if (material.HasProperty("_Metallic")) // Metallic 속성 지원 여부 확인
            {
                material.SetFloat("_Metallic", metallic); // 금속성 수치 적용
            }

            if (material.HasProperty("_Smoothness")) // Smoothness 속성 지원 여부 확인
            {
                material.SetFloat("_Smoothness", smoothness); // 표면 매끄러움 수치 적용
            }

            EditorUtility.SetDirty(material); // 변경 재질 저장 대상으로 표시
            return material; // 생성 또는 갱신 재질 반환
        }

        private static void RemoveExistingRoot(Scene scene, string rootName) // 지정 이름의 기존 씬 루트 제거
        {
            GameObject existingRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == rootName); // 지정 이름 기존 루트 조회

            if (existingRoot != null) // 기존 루트 존재 여부 확인
            {
                Object.DestroyImmediate(existingRoot); // 기존 자동 생성 루트 제거
            }
        }

        private static void EnsureAssetFolder(string folderPath) // 중첩 에셋 폴더 존재 보장
        {
            string[] segments = folderPath.Split('/'); // 에셋 경로를 폴더 단위로 분리
            string currentPath = segments[0]; // Assets 루트부터 경로 구성 시작

            for (int index = 1; index < segments.Length; index++) // 하위 폴더 전체 순회
            {
                string nextPath = currentPath + "/" + segments[index]; // 다음 하위 폴더 전체 경로 계산

                if (!AssetDatabase.IsValidFolder(nextPath)) // 하위 폴더 존재 여부 확인
                {
                    AssetDatabase.CreateFolder(currentPath, segments[index]); // 누락 하위 폴더 생성
                }

                currentPath = nextPath; // 다음 반복용 현재 경로 갱신
            }
        }
    }
}
