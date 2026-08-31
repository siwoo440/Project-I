using System.IO; // 자동 생성 에셋 폴더와 씬 파일 확인 기능 참조
using System.Linq; // 씬 루트 이름 검색 기능 참조
using ProjectI.Combat; // Day14 공통 전투 기능 참조
using ProjectI.Diagnostics; // F1 Combat 진단 페이지 참조
using ProjectI.Items; // 기존 WorldItem·CarryType 참조
using ProjectI.Player; // 기존 플레이어 체력·스태미나·이동 참조
using UnityEditor; // 유니티 에디터 메뉴와 에셋 생성 기능 참조
using UnityEditor.SceneManagement; // 씬 열기와 저장 기능 참조
using UnityEngine; // 유니티 오브젝트·재질·물리 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 자동 구성 도구 네임스페이스
{
    [InitializeOnLoad] // 컴파일 완료 후 Day14 전투 기반 자동 구성
    public static class Phase4Day14Setup // 공통 Damage Pipeline과 전투 시험장 자동 구성
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string CombatRootName = "===Day14 Combat Foundation==="; // Day14 전투 시험장 루트 이름
        private const string ReadyMarkerName = "===Day14 Combat Foundation Ready v2==="; // Day14 시험장 이동 보정 버전 자동 구성 완료 마커 이름
        private const string LegacyReadyMarkerName = "===Day14 Combat Foundation Ready==="; // 기존 Day14 완료 마커 이름
        private const string MaterialFolder = "Assets/ProjectI/Art/Generated/Day14"; // Day14 테스트 재질 생성 폴더
        private const string AttackAssetFolder = "Assets/ProjectI/Resources/Combat"; // Day14 공격 데이터 생성 폴더
        private const string AttackAssetPath = AttackAssetFolder + "/Day14_TestSword.asset"; // Day14 테스트 검 공격 데이터 경로
        private static readonly Vector3 CombatRangeCenter = new Vector3(-27f, 0f, 12f); // 01_SprintLane 북쪽 개방 구역으로 옮긴 Day14 전투 시험장 중심

        static Phase4Day14Setup() // 자동 설정 등록
        {
            EditorApplication.delayCall += TryAutoApply; // 스크립트 컴파일 완료 후 Day14 자동 구성 예약
        }

        [MenuItem("Tools/Project I/Day 14/Apply Combat Foundation")] // 수동 Day14 전투 기반 구성 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 Day14 전체 구성 실행
        {
            ApplyDay14(true, true); // 강제 재구성과 결과 대화상자 활성화
        }

        private static void TryAutoApply() // 컴파일 완료 후 자동 구성 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // Batch 또는 Play 전환 상태 확인
            {
                return; // 자동 구성 제외 상태에서는 중단
            }

            ApplyDay14(false, false); // 완료 마커가 없을 때 한 번 자동 적용
        }

        private static void ApplyDay14(bool showDialog, bool force) // Day14 공통 전투 기반 전체 적용
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 탐사 씬 존재 여부 확인
            {
                return; // 대상 씬 누락 시 자동 구성 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 단독 열기
            GameObject existingMarker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 Day14 완료 마커 조회

            if (!force && existingMarker != null) // 이미 Day14 자동 구성이 적용됐는지 확인
            {
                return; // 반복 자동 적용 방지
            }

            PlayerInputReader inputReader = Object.FindFirstObjectByType<PlayerInputReader>(); // 기존 싱글 플레이어 입력 래퍼 조회
            GameObject player = inputReader == null ? null : inputReader.gameObject; // 기존 플레이어 루트 조회

            if (player == null) // 기존 플레이어 존재 여부 확인
            {
                Debug.LogError("[Project I] Day14 구성 전에 기존 Player가 필요합니다."); // 선행 Player 누락 오류 출력
                return; // Day14 구성 중단
            }

            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>(); // 기존 플레이어 체력 조회
            PlayerStamina playerStamina = player.GetComponent<PlayerStamina>(); // 기존 플레이어 스태미나 조회
            PlayerMovement playerMovement = player.GetComponent<PlayerMovement>(); // 기존 플레이어 이동 조회

            if (playerHealth == null || playerStamina == null || playerMovement == null) // 기존 핵심 플레이어 시스템 존재 여부 확인
            {
                Debug.LogError("[Project I] Day14 구성 전에 PlayerHealth·PlayerStamina·PlayerMovement가 필요합니다."); // 선행 시스템 누락 오류 출력
                return; // Day14 구성 중단
            }

            RemoveExistingRoot(scene, CombatRootName); // 기존 Day14 시험장 루트 제거
            RemoveExistingRoot(scene, ReadyMarkerName); // 현재 Day14 완료 마커 제거
            RemoveExistingRoot(scene, LegacyReadyMarkerName); // 기존 Day14 완료 마커 제거
            EnsureAssetFolder(MaterialFolder); // Day14 재질 폴더 존재 보장
            EnsureAssetFolder(AttackAssetFolder); // 공격 데이터 폴더 존재 보장
            Material floorMaterial = GetOrCreateMaterial("Combat_Floor", new Color(0.08f, 0.085f, 0.09f), 0.15f, 0.45f); // 전투 시험장 바닥 재질 생성
            Material enemyMaterial = GetOrCreateMaterial("Combat_Enemy", new Color(0.46f, 0.055f, 0.04f), 0.1f, 0.35f); // 적 더미 붉은 재질 생성
            Material allyMaterial = GetOrCreateMaterial("Combat_Ally", new Color(0.035f, 0.20f, 0.48f), 0.1f, 0.35f); // 아군 더미 푸른 재질 생성
            Material neutralMaterial = GetOrCreateMaterial("Combat_Trim", new Color(0.22f, 0.24f, 0.25f), 0.55f, 0.45f); // 시험장 금속 장식 재질 생성
            Material wallMaterial = GetOrCreateMaterial("Combat_Blocker", new Color(0.15f, 0.16f, 0.17f), 0.15f, 0.30f); // 벽 충돌 시험 재질 생성
            Material swordMaterial = GetOrCreateMaterial("Combat_Sword", new Color(0.48f, 0.50f, 0.52f), 0.8f, 0.72f); // 테스트 검 강철 재질 생성
            Material handleMaterial = GetOrCreateMaterial("Combat_Handle", new Color(0.08f, 0.055f, 0.035f), 0.15f, 0.35f); // 테스트 검 손잡이 재질 생성
            Material markerMaterial = GetOrCreateMaterial("Combat_Marker", new Color(0.72f, 0.48f, 0.04f), 0.15f, 0.4f); // 시험 구역 표시 재질 생성
            AttackDefinition testAttack = GetOrCreateTestAttackDefinition(); // Day14 테스트 검 공격 데이터 생성
            PlayerDamageReceiver damageReceiver = GetOrAddComponent<PlayerDamageReceiver>(player); // 기존 PlayerHealth Damage Pipeline 어댑터 추가
            CombatController combatController = GetOrAddComponent<CombatController>(player); // 플레이어 공통 전투 상태 제어기 추가
            Camera playerCamera = player.GetComponentInChildren<Camera>(true); // 기존 1인칭 플레이어 카메라 조회
            combatController.Configure(playerHealth, playerStamina, playerMovement, playerCamera == null ? player.transform : playerCamera.transform); // 기존 플레이어 시스템을 공통 전투 제어기에 연결
            GameObject combatRoot = new GameObject(CombatRootName); // Day14 전투 시험장 루트 생성
            CreateCombatRange(combatRoot.transform, floorMaterial, enemyMaterial, allyMaterial, neutralMaterial, wallMaterial, swordMaterial, handleMaterial, markerMaterial, testAttack); // 전투 시험장과 테스트 대상 생성
            CombatDebugPage debugPage = combatRoot.AddComponent<CombatDebugPage>(); // F1 Combat 진단 페이지 추가
            debugPage.Configure(combatController); // 진단 페이지에 플레이어 전투 제어기 연결
            GameObject marker = new GameObject(ReadyMarkerName); // Day14 자동 적용 완료 마커 생성
            EditorUtility.SetDirty(damageReceiver); // 플레이어 피해 수신기 씬 저장 대상으로 표시
            EditorUtility.SetDirty(combatController); // 플레이어 전투 제어기 씬 저장 대상으로 표시
            EditorUtility.SetDirty(debugPage); // F1 Combat 페이지 씬 저장 대상으로 표시
            EditorUtility.SetDirty(marker); // 완료 마커 씬 저장 대상으로 표시
            EditorSceneManager.MarkSceneDirty(scene); // 변경된 탐사 씬 저장 상태 표시
            EditorSceneManager.SaveScene(scene); // Day14 전투 시험장과 플레이어 구성 저장
            AssetDatabase.SaveAssets(); // 공격 데이터와 재질 에셋 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 상태 갱신
            bool validationPassed = Phase4Day14Validator.Validate(false); // Day14 정적 구조와 공통 규칙 검증 실행

            if (showDialog) // 수동 실행 결과 표시 여부 확인
            {
                string message = validationPassed ? "Day14 공통 Damage Pipeline과 전투 시험장 구성이 완료되었습니다." : "Day14 검증 실패 - Console을 확인하세요."; // 구성 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 완료 또는 실패 결과 대화상자 출력
            }
        }

        private static void CreateCombatRange(Transform parent, Material floorMaterial, Material enemyMaterial, Material allyMaterial, Material neutralMaterial, Material wallMaterial, Material swordMaterial, Material handleMaterial, Material markerMaterial, AttackDefinition testAttack) // Day14 전투 시험장 모델과 기능 생성
        {
            CreateBox(parent, "Combat_TestFloor", CombatRangeCenter + new Vector3(0f, 0.035f, 0f), new Vector3(13f, 0.07f, 10f), floorMaterial, false); // 기존 바닥 위 전투 구역 표식 생성
            CreateBox(parent, "Combat_SouthMarker", CombatRangeCenter + new Vector3(0f, 0.055f, -4.4f), new Vector3(12f, 0.04f, 0.18f), markerMaterial, false); // 시험장 남쪽 경계 표시 생성
            CreateBox(parent, "Combat_NorthMarker", CombatRangeCenter + new Vector3(0f, 0.055f, 4.4f), new Vector3(12f, 0.04f, 0.18f), markerMaterial, false); // 시험장 북쪽 경계 표시 생성
            CreateBox(parent, "Combat_WestMarker", CombatRangeCenter + new Vector3(-5.6f, 0.055f, 0f), new Vector3(0.18f, 0.04f, 8.6f), markerMaterial, false); // 시험장 서쪽 경계 표시 생성
            CreateBox(parent, "Combat_EastMarker", CombatRangeCenter + new Vector3(5.6f, 0.055f, 0f), new Vector3(0.18f, 0.04f, 8.6f), markerMaterial, false); // 시험장 동쪽 경계 표시 생성
            CreateCombatDummy(parent, "CombatDummy_Enemy", CombatRangeCenter + new Vector3(-3.2f, 0f, 1.5f), CombatFaction.Enemy, enemyMaterial, neutralMaterial); // 일반 적 진영 피해 시험 더미 생성
            CreateCombatDummy(parent, "CombatDummy_Ally", CombatRangeCenter + new Vector3(0f, 0f, 1.5f), CombatFaction.Ally, allyMaterial, neutralMaterial); // Friendly Fire 차단 시험 아군 더미 생성
            CreateBox(parent, "Combat_BlockerWall", CombatRangeCenter + new Vector3(3.2f, 1.3f, 1.6f), new Vector3(3.0f, 2.6f, 0.35f), wallMaterial, true); // 검 공격 벽 차단 시험 벽 생성
            CreateCombatDummy(parent, "CombatDummy_EnemyBehindWall", CombatRangeCenter + new Vector3(3.2f, 0f, 3.45f), CombatFaction.Enemy, enemyMaterial, neutralMaterial); // 벽 뒤 적 피해 차단 시험 더미 생성
            CreateTestSword(parent, CombatRangeCenter + new Vector3(-4.2f, 0.85f, -3.1f), swordMaterial, handleMaterial, testAttack); // F로 줍고 좌클릭 사용하는 테스트 검 생성
        }

        private static void CreateCombatDummy(Transform parent, string objectName, Vector3 position, CombatFaction faction, Material bodyMaterial, Material trimMaterial) // 진영별 공통 피해 시험 더미 생성
        {
            GameObject root = new GameObject(objectName); // 더미 기능 루트 생성
            root.transform.SetParent(parent); // Day14 시험장 아래 배치
            root.transform.position = position; // 더미 기준 월드 위치 지정
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>(); // 공통 근접 피격용 캡슐 Collider 추가
            collider.center = new Vector3(0f, 1.25f, 0f); // 더미 몸통 중앙 높이 지정
            collider.height = 2.4f; // 더미 전체 피격 높이 지정
            collider.radius = 0.48f; // 더미 피격 반경 지정
            CombatHealth health = root.AddComponent<CombatHealth>(); // 공통 Damage Pipeline 피격 체력 추가
            health.Configure(objectName, faction, 100f); // 100 체력과 지정 진영으로 더미 구성
            CreatePrimitivePart(root.transform, "Pedestal", PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0f), new Vector3(1.2f, 0.15f, 1.2f), trimMaterial); // 더미 하단 금속 받침 생성
            CreatePrimitivePart(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 1.25f, 0f), new Vector3(0.72f, 0.92f, 0.72f), bodyMaterial); // 더미 몸통 모델 생성
            CreatePrimitivePart(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 2.35f, 0f), new Vector3(0.62f, 0.62f, 0.62f), bodyMaterial); // 더미 머리 모델 생성
            CreatePrimitivePart(root.transform, "ChestPlate", PrimitiveType.Cube, new Vector3(0f, 1.45f, -0.36f), new Vector3(0.72f, 0.58f, 0.08f), trimMaterial); // 더미 전면 장갑판 생성
            CreatePrimitivePart(root.transform, "Shoulder_L", PrimitiveType.Sphere, new Vector3(-0.46f, 1.75f, 0f), new Vector3(0.28f, 0.28f, 0.28f), trimMaterial); // 왼쪽 어깨 관절 장식 생성
            CreatePrimitivePart(root.transform, "Shoulder_R", PrimitiveType.Sphere, new Vector3(0.46f, 1.75f, 0f), new Vector3(0.28f, 0.28f, 0.28f), trimMaterial); // 오른쪽 어깨 관절 장식 생성
            EditorUtility.SetDirty(health); // 더미 체력 설정 씬 저장 대상으로 표시
        }

        private static void CreateTestSword(Transform parent, Vector3 position, Material bladeMaterial, Material handleMaterial, AttackDefinition attackDefinition) // 기존 아이템 체계와 연결된 Day14 테스트 검 생성
        {
            GameObject sword = new GameObject("Day14_CombatTestSword"); // 테스트 검 기능 루트 생성
            sword.transform.SetParent(parent); // Day14 시험장 아래 배치
            sword.transform.position = position; // 월드 획득 위치 지정
            sword.transform.rotation = Quaternion.Euler(0f, 0f, 90f); // 바닥 위 옆으로 놓인 시작 자세 지정
            Rigidbody body = sword.AddComponent<Rigidbody>(); // 기존 WorldItem 필수 월드 물리 Rigidbody 추가
            body.mass = 2f; // 테스트 검 질량 설정
            BoxCollider pickupCollider = sword.AddComponent<BoxCollider>(); // 월드 획득과 물리 충돌용 대표 Collider 추가
            pickupCollider.center = new Vector3(0f, 0.78f, 0f); // 검 전체 형태 기준 Collider 중심 지정
            pickupCollider.size = new Vector3(0.22f, 1.75f, 0.22f); // 검 전체 형태 기준 Collider 크기 지정
            WorldItem worldItem = sword.AddComponent<WorldItem>(); // 기존 F 획득·빠른 슬롯 기능 추가
            worldItem.Configure("Combat Test Sword", 0.20f, CarryType.OneHand); // 한손 테스트 검 아이템 설정
            worldItem.ConfigureCarryPose(CarryType.OneHand, new Vector3(0.18f, -0.12f, 0.22f), new Vector3(78f, 0f, -15f)); // 1인칭 손 위치용 테스트 검 포즈 설정
            MeleeWeaponTrace trace = sword.AddComponent<MeleeWeaponTrace>(); // 근접 무기 궤적 검사 기능 추가
            MeleeWeaponItem weaponItem = sword.AddComponent<MeleeWeaponItem>(); // 기존 좌클릭 Use 체계와 전투 공격 연결
            GameObject visualPivotObject = new GameObject("VisualPivot"); // 공격 중 무기 휘두르기용 시각 피벗 생성
            visualPivotObject.transform.SetParent(sword.transform, false); // 검 기능 루트 아래 시각 피벗 배치
            Transform visualPivot = visualPivotObject.transform; // 시각 피벗 Transform 참조 저장
            CreatePrimitivePart(visualPivot, "Grip", PrimitiveType.Cylinder, new Vector3(0f, 0.20f, 0f), new Vector3(0.13f, 0.22f, 0.13f), handleMaterial); // 검 손잡이 모델 생성
            CreatePrimitivePart(visualPivot, "Pommel", PrimitiveType.Sphere, new Vector3(0f, -0.05f, 0f), new Vector3(0.20f, 0.20f, 0.20f), bladeMaterial); // 검 손잡이 끝 금속 장식 생성
            CreatePrimitivePart(visualPivot, "Guard", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(0.72f, 0.09f, 0.16f), bladeMaterial); // 검 가드 모델 생성
            CreatePrimitivePart(visualPivot, "Blade", PrimitiveType.Cube, new Vector3(0f, 1.02f, 0f), new Vector3(0.16f, 1.10f, 0.075f), bladeMaterial); // 검날 본체 모델 생성
            CreatePrimitivePart(visualPivot, "BladeTip", PrimitiveType.Cube, new Vector3(0f, 1.58f, 0f), new Vector3(0.11f, 0.18f, 0.055f), bladeMaterial); // 검날 끝부분 모델 생성
            GameObject startPoint = new GameObject("TraceStart"); // 검날 하단 궤적 기준점 생성
            startPoint.transform.SetParent(visualPivot, false); // 시각 피벗 아래 기준점 배치
            startPoint.transform.localPosition = new Vector3(0f, 0.48f, 0f); // 가드 바로 위 시작점 위치 지정
            GameObject endPoint = new GameObject("TraceEnd"); // 검날 끝 궤적 기준점 생성
            endPoint.transform.SetParent(visualPivot, false); // 시각 피벗 아래 끝 기준점 배치
            endPoint.transform.localPosition = new Vector3(0f, 1.70f, 0f); // 검날 끝보다 약간 앞선 기준점 위치 지정
            trace.Configure(startPoint.transform, endPoint.transform, ~0); // 전체 물리 Layer를 대상으로 근접 궤적 구성
            weaponItem.Configure(attackDefinition, trace, visualPivot); // 테스트 검 공격 데이터와 시각 피벗 연결
            EditorUtility.SetDirty(worldItem); // 기존 월드 아이템 설정 씬 저장 대상으로 표시
            EditorUtility.SetDirty(trace); // 근접 궤적 설정 씬 저장 대상으로 표시
            EditorUtility.SetDirty(weaponItem); // 근접 무기 설정 씬 저장 대상으로 표시
        }

        private static AttackDefinition GetOrCreateTestAttackDefinition() // Day14 테스트 검 공격 데이터 에셋 생성 또는 갱신
        {
            AttackDefinition definition = AssetDatabase.LoadAssetAtPath<AttackDefinition>(AttackAssetPath); // 기존 Day14 테스트 공격 에셋 조회

            if (definition == null) // 공격 데이터 에셋 누락 여부 확인
            {
                definition = ScriptableObject.CreateInstance<AttackDefinition>(); // 새 공격 데이터 ScriptableObject 생성
                AssetDatabase.CreateAsset(definition, AttackAssetPath); // Day14 Resources/Combat 폴더에 공격 에셋 저장
            }

            definition.Configure("Combat Test Sword Slash", 25f, CombatDamageType.Physical, 12f, 0.15f, 0.18f, 0.30f, 0.65f, 0.12f, 10f, 1.5f); // Day14 공통 전투 시험 수치 구성
            EditorUtility.SetDirty(definition); // 공격 데이터 에셋 저장 대상으로 표시
            return definition; // 구성된 테스트 공격 데이터 반환
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

        private static GameObject CreatePrimitivePart(Transform parent, string objectName, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Material material) // 더미와 무기 세부 모델 파트 생성
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType); // 지정 Unity Primitive 생성
            part.name = objectName; // 모델 파트 이름 지정
            part.transform.SetParent(parent, false); // 부모 기준 로컬 배치 설정
            part.transform.localPosition = localPosition; // 모델 파트 로컬 위치 지정
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

        private static Material GetOrCreateMaterial(string materialName, Color color, float metallic, float smoothness) // Day14 URP 호환 재질 생성 또는 갱신
        {
            string path = $"{MaterialFolder}/{materialName}.mat"; // 생성 재질 에셋 경로 계산
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 동일 이름 재질 조회
            Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // 프로젝트 URP Lit Shader 조회

            if (shader == null) // URP Lit Shader 누락 여부 확인
            {
                shader = Shader.Find("Standard"); // 안전 폴백 Standard Shader 조회
            }

            if (material == null) // 기존 재질 에셋 누락 여부 확인
            {
                material = new Material(shader); // 지정 Shader 기반 새 Material 생성
                AssetDatabase.CreateAsset(material, path); // Day14 생성 재질 에셋 저장
            }
            else if (shader != null && material.shader != shader) // 기존 재질 Shader가 현재 기준과 다른지 확인
            {
                material.shader = shader; // 현재 프로젝트 Shader 기준으로 교체
            }

            material.color = color; // 공통 Material 기본 색상 적용

            if (material.HasProperty("_BaseColor")) // URP BaseColor 프로퍼티 존재 여부 확인
            {
                material.SetColor("_BaseColor", color); // URP 기본 색상 적용
            }

            if (material.HasProperty("_Metallic")) // Metallic 프로퍼티 존재 여부 확인
            {
                material.SetFloat("_Metallic", Mathf.Clamp01(metallic)); // 금속성 값 적용
            }

            if (material.HasProperty("_Smoothness")) // Smoothness 프로퍼티 존재 여부 확인
            {
                material.SetFloat("_Smoothness", Mathf.Clamp01(smoothness)); // 표면 매끄러움 값 적용
            }

            EditorUtility.SetDirty(material); // 생성 또는 갱신 재질 저장 대상으로 표시
            return material; // 구성 완료 재질 반환
        }

        private static void EnsureAssetFolder(string folderPath) // 중첩 Asset 폴더 존재 보장
        {
            if (AssetDatabase.IsValidFolder(folderPath)) // 전체 대상 폴더 존재 여부 확인
            {
                return; // 이미 존재하면 추가 작업 생략
            }

            string[] segments = folderPath.Split('/'); // Assets 기준 폴더 경로 조각 분리
            string currentPath = segments[0]; // 첫 Assets 루트 경로 초기화

            for (int index = 1; index < segments.Length; index++) // 하위 폴더 경로 순서대로 생성
            {
                string nextPath = currentPath + "/" + segments[index]; // 현재 생성 대상 전체 경로 계산

                if (!AssetDatabase.IsValidFolder(nextPath)) // 현재 하위 폴더 누락 여부 확인
                {
                    AssetDatabase.CreateFolder(currentPath, segments[index]); // 누락 하위 폴더 생성
                }

                currentPath = nextPath; // 다음 깊이 생성을 위한 현재 경로 갱신
            }
        }

        private static void RemoveExistingRoot(Scene scene, string rootName) // 지정 이름의 기존 씬 루트 제거
        {
            GameObject existingRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == rootName); // 대상 이름의 기존 루트 검색

            if (existingRoot != null) // 기존 루트 존재 여부 확인
            {
                Object.DestroyImmediate(existingRoot); // 강제 재적용을 위한 기존 Day14 루트 제거
            }
        }
    }
}
