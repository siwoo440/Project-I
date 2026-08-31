using System.IO; // 자동 생성 에셋 폴더와 씬 파일 확인 기능 참조
using System.Linq; // 씬 루트 검색 기능 참조
using ProjectI.Combat; // 공통 체력·경직·진영 기능 참조
using ProjectI.Diagnostics; // F1 Monster AI 페이지 참조
using ProjectI.Monsters; // Day17 몬스터 AI·4종 몬스터·소환 기능 참조
using ProjectI.Player; // 플레이어 이동·소음 기능 참조
using UnityEditor; // 유니티 에디터 메뉴·에셋 생성 기능 참조
using UnityEditor.SceneManagement; // 씬 열기·저장 기능 참조
using UnityEngine; // 유니티 오브젝트·재질·물리 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 자동 구성 도구 네임스페이스
{
    [InitializeOnLoad] // 컴파일 완료 후 Day17 몬스터 AI 자동 구성
    public static class Phase4Day17Setup // 공통 Monster AI와 4종 테스트 몬스터를 자동 생성하는 도구
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string Day17RootName = "===Day17 Monster AI==="; // Day17 몬스터 시험장 루트 이름
        private const string ReadyMarkerName = "===Day17 Monster AI Ready v4==="; // 웃는 석상 불사·화면 관찰 규칙 보정 버전 자동 구성 완료 마커 이름
        private const string LegacyReadyMarkerName = "===Day17 Monster AI Ready v3==="; // 이전 몬스터 체력 절반 보정 버전 완료 마커 이름
        private const string MaterialFolder = "Assets/ProjectI/Art/Generated/Day17"; // Day17 몬스터·소환선 재질 생성 폴더
        private const string MonsterDataFolder = "Assets/ProjectI/Resources/Monsters"; // 런타임 몬스터 데이터 에셋 폴더
        private const string UndeadDataPath = MonsterDataFolder + "/Day17_CorruptedUndead.asset"; // 기본 부패한 망자 데이터 에셋 경로
        private const string ArcherDataPath = MonsterDataFolder + "/Day17_CorruptedUndeadArcher.asset"; // 부패한 망자 궁수 데이터 에셋 경로
        private const string StatueDataPath = MonsterDataFolder + "/Day17_SmilingStatue.asset"; // 웃는 석상 데이터 에셋 경로
        private const string MimicDataPath = MonsterDataFolder + "/Day17_ChestMimic.asset"; // 상자 미믹 데이터 에셋 경로
        private static readonly Vector3 SpawnLineCenter = new Vector3(-27f, 0f, 25.8f); // 첨부 이미지의 SprintLane 북쪽 끝 4종 몬스터 나란히 소환 중심
        private const float SpawnSpacing = 2.15f; // 네 SpawnPoint가 파란 Lane 폭 안에서 겹치지 않는 가로 간격

        static Phase4Day17Setup() // 자동 설정 등록
        {
            EditorApplication.delayCall += TryAutoApply; // 스크립트 컴파일 완료 후 Day17 자동 구성 예약
        }

        [MenuItem("Tools/Project I/Day 17/Apply Monster AI + 4 Monster Types")] // 수동 Day17 4종 몬스터 구성 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 Day17 전체 구성 실행
        {
            ApplyDay17(true, true); // 강제 재구성과 결과 대화상자 활성화
        }

        private static void TryAutoApply() // 컴파일 완료 후 자동 구성 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // Batch 또는 Play 전환 상태 확인
            {
                return; // 자동 구성 제외 상태에서는 중단
            }

            ApplyDay17(false, false); // v4 완료 마커가 없을 때 한 번 자동 적용
        }

        private static void ApplyDay17(bool showDialog, bool force) // Day17 공통 AI·4종 몬스터·소환선 전체 적용
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 탐사 씬 존재 여부 확인
            {
                return; // 씬 누락 시 자동 구성 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 단독 열기
            GameObject existingMarker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 현재 v4 완료 마커 조회

            if (!force && existingMarker != null) // 이미 4종 Day17 구성이 적용됐는지 확인
            {
                return; // 반복 자동 적용 방지
            }

            PlayerMovement playerMovement = Object.FindFirstObjectByType<PlayerMovement>(); // 플레이어 이동·발소리 기준 조회
            PlayerDamageReceiver playerReceiver = Object.FindFirstObjectByType<PlayerDamageReceiver>(); // Enemy → Player Damage Pipeline 대상 조회

            if (playerMovement == null || playerReceiver == null) // 선행 플레이어 전투 기반 존재 여부 확인
            {
                Debug.LogError("[Project I] Day17 구성 전에 PlayerMovement와 PlayerDamageReceiver가 필요합니다."); // 선행 시스템 누락 오류 출력
                return; // Day17 구성 중단
            }

            RemoveExistingRoot(scene, Day17RootName); // 기존 Day17 시험장 루트 제거
            RemoveExistingRoot(scene, ReadyMarkerName); // 현재 v4 완료 마커 제거
            RemoveExistingRoot(scene, LegacyReadyMarkerName); // 이전 v3 체력 보정 완료 마커 제거
            RemoveExistingRoot(scene, "===Day17 Monster AI Ready v2==="); // 이전 4종 몬스터 첫 완료 마커 제거
            RemoveExistingRoot(scene, "===Day17 Monster AI Ready==="); // 초기 궁수 전용 완료 마커도 함께 제거
            EnsureAssetFolder(MaterialFolder); // Day17 재질 폴더 존재 보장
            EnsureAssetFolder(MonsterDataFolder); // MonsterData 에셋 폴더 존재 보장
            MonsterData undeadData = GetOrCreateMeleeData(UndeadDataPath, MonsterArchetype.CorruptedUndead, "부패한 망자", 70f, 2.15f, 3.45f, 22f, 115f, 22f, 1.85f, 0.36f, 1.15f, 22f, 18f, 1.40f, 48f, 0.18f); // 기본 근접 부패한 망자 데이터 생성·갱신
            MonsterData archerData = GetOrCreateArcherData(); // 부패한 망자 궁수 행동 데이터 생성·갱신
            MonsterData statueData = GetOrCreateMeleeData(StatueDataPath, MonsterArchetype.SmilingStatue, "웃는 석상", 0f, 0.1f, 5.70f, 30f, 180f, 0f, 1.55f, 0.18f, 1.25f, 34f, 28f, 2.20f, 95f, 0.72f); // 체력 없는 불사·관찰 기반 웃는 석상 데이터 생성·갱신
            MonsterData mimicData = GetOrCreateMeleeData(MimicDataPath, MonsterArchetype.ChestMimic, "상자 미믹", 80f, 1.8f, 4.0f, 20f, 150f, 20f, 1.70f, 0.28f, 1.05f, 30f, 24f, 2.50f, 65f, 0.45f); // 위장·변신 상자 미믹 데이터 생성·갱신
            Material flesh = GetOrCreateMaterial("Monster_Flesh", new Color(0.18f, 0.20f, 0.13f), 0.05f, 0.18f); // 부패한 피부 어두운 녹갈색 재질 생성
            Material bone = GetOrCreateMaterial("Monster_Bone", new Color(0.52f, 0.48f, 0.35f), 0.02f, 0.20f); // 노출된 뼈·이빨 재질 생성
            Material rust = GetOrCreateMaterial("Monster_Rust", new Color(0.23f, 0.10f, 0.055f), 0.62f, 0.26f); // 녹슨 금속 장비 재질 생성
            Material cloth = GetOrCreateMaterial("Monster_Cloth", new Color(0.12f, 0.075f, 0.055f), 0.0f, 0.18f); // 낡은 천·가죽 재질 생성
            Material bowWood = GetOrCreateMaterial("Monster_BowWood", new Color(0.20f, 0.09f, 0.025f), 0.02f, 0.24f); // 활·상자 목재 재질 생성
            Material stringMaterial = GetOrCreateMaterial("Monster_String", new Color(0.035f, 0.028f, 0.022f), 0.0f, 0.10f); // 활 시위 재질 생성
            Material eyeMaterial = GetOrCreateMaterial("Monster_Eye", new Color(0.60f, 0.035f, 0.02f), 0.05f, 0.38f, new Color(0.55f, 0.02f, 0.0f)); // 붉은 눈 발광 재질 생성
            Material stone = GetOrCreateMaterial("Monster_StatueStone", new Color(0.30f, 0.31f, 0.30f), 0.05f, 0.18f); // 웃는 석상 회색 석재 재질 생성
            Material darkStone = GetOrCreateMaterial("Monster_StatueDark", new Color(0.09f, 0.095f, 0.09f), 0.02f, 0.12f); // 웃는 입·눈 홈용 어두운 석재 재질 생성
            Material tongue = GetOrCreateMaterial("Monster_MimicTongue", new Color(0.34f, 0.055f, 0.07f), 0.0f, 0.24f); // 미믹 혀·내부 살점 재질 생성
            Material spawnMaterial = GetOrCreateMaterial("Monster_Spawn", new Color(0.20f, 0.08f, 0.07f), 0.25f, 0.24f); // 첨부 위치 확인용 소환선 표시 재질 생성
            Material spawnAccent = GetOrCreateMaterial("Monster_SpawnAccent", new Color(0.48f, 0.17f, 0.08f), 0.30f, 0.30f); // SpawnPoint 강조 재질 생성
            GameObject day17Root = new GameObject(Day17RootName); // Day17 시험장 루트 생성
            CreateSpawnLineFloor(day17Root.transform, spawnMaterial, spawnAccent); // SprintLane 북쪽 끝 4종 나란히 소환선 시각 생성
            MonsterArrowProjectile arrowTemplate = CreateMonsterArrowTemplate(day17Root.transform, bowWood, bone, cloth); // 궁수 공용 포물선 화살 템플릿 생성
            GameObject undeadPrototype = CreateCorruptedUndeadMeleePrototype(day17Root.transform, undeadData, flesh, bone, rust, cloth, eyeMaterial); // 기본 근접 부패한 망자 프로토타입 생성
            GameObject archerPrototype = CreateCorruptedUndeadArcherPrototype(day17Root.transform, archerData, arrowTemplate, flesh, bone, rust, cloth, bowWood, stringMaterial, eyeMaterial); // 궁수 프로토타입 생성
            GameObject statuePrototype = CreateSmilingStatuePrototype(day17Root.transform, statueData, stone, darkStone); // 관찰 기반 웃는 석상 프로토타입 생성
            GameObject mimicPrototype = CreateChestMimicPrototype(day17Root.transform, mimicData, bowWood, rust, bone, tongue, eyeMaterial); // 위장·변신 상자 미믹 프로토타입 생성
            CreateSpawnPoints(day17Root.transform, new[] { undeadPrototype, archerPrototype, statuePrototype, mimicPrototype }, spawnMaterial, spawnAccent); // 첨부 이미지 위치에 4종 몬스터를 한 마리씩 나란히 소환하도록 지점 생성
            day17Root.AddComponent<MonsterAIDebugPage>(); // F1 Monster AI 진단 페이지 추가
            PlayerNoiseEmitter playerNoise = playerMovement.GetComponent<PlayerNoiseEmitter>(); // 기존 플레이어 발소리 발생기 조회

            if (playerNoise == null) // Day17 발소리 기능 최초 적용 여부 확인
            {
                playerNoise = playerMovement.gameObject.AddComponent<PlayerNoiseEmitter>(); // 플레이어에 공통 청각용 이동 소음 발생기 추가
            }

            playerNoise.Configure(6f, 14f, 0.72f, 0.42f); // 걷기·달리기 청각 반경과 발생 간격 구성
            EditorUtility.SetDirty(playerNoise); // 플레이어 소음 설정 씬 저장 대상으로 표시
            GameObject marker = new GameObject(ReadyMarkerName); // Day17 v4 웃는 석상 불사 규칙 자동 적용 완료 마커 생성
            EditorUtility.SetDirty(marker); // 완료 마커 씬 저장 대상으로 표시
            EditorSceneManager.MarkSceneDirty(scene); // 변경된 탐사 씬 저장 상태 표시
            EditorSceneManager.SaveScene(scene); // Day17 4종 몬스터 시험장과 플레이어 소음 기능 저장
            AssetDatabase.SaveAssets(); // MonsterData·재질 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 갱신
            bool validationPassed = Phase4Day17Validator.Validate(false); // Day17 정적 구조 검증 실행

            if (showDialog) // 수동 실행 결과 표시 여부 확인
            {
                string message = validationPassed ? "Day17 공통 Monster AI와 부패한 망자·궁수·웃는 석상·상자 미믹 구성이 완료되었습니다." : "Day17 검증 실패 - Console을 확인하세요."; // 구성 결과 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 완료 또는 실패 대화상자 출력
            }
        }

        private static MonsterData GetOrCreateArcherData() // 부패한 망자 궁수 ScriptableObject 생성·갱신
        {
            MonsterData data = AssetDatabase.LoadAssetAtPath<MonsterData>(ArcherDataPath); // 기존 궁수 데이터 에셋 조회

            if (data == null) // 최초 Day17 데이터 생성 여부 확인
            {
                data = ScriptableObject.CreateInstance<MonsterData>(); // 새 MonsterData 인스턴스 생성
                AssetDatabase.CreateAsset(data, ArcherDataPath); // 지정 Resources 경로에 에셋 저장
            }

            data.ConfigureArcher("부패한 망자 궁수", 55f, 2.0f, 3.15f, 3.0f, 24f, 110f, 24f, 8f, 13f, 17f, 0.80f, 2.05f, 24f, 14f, 6f, 0.25f, 42f, 0.25f); // Day17 궁수 체력·감지·거리 유지·활 공격 수치 구성
            EditorUtility.SetDirty(data); // 수정된 MonsterData 저장 대상으로 표시
            return data; // 완성된 궁수 데이터 반환
        }

        private static MonsterData GetOrCreateMeleeData(string path, MonsterArchetype archetype, string displayName, float health, float walk, float chase, float vision, float angle, float hearing, float attackRange, float windup, float cooldown, float damage, float stagger, float knockback, float threshold, float resistance) // 근접형 3종 MonsterData 생성·갱신
        {
            MonsterData data = AssetDatabase.LoadAssetAtPath<MonsterData>(path); // 지정 경로 기존 MonsterData 조회

            if (data == null) // 최초 데이터 에셋 생성 여부 확인
            {
                data = ScriptableObject.CreateInstance<MonsterData>(); // 새 MonsterData 인스턴스 생성
                AssetDatabase.CreateAsset(data, path); // 지정 Resources 경로에 에셋 저장
            }

            data.ConfigureMelee(archetype, displayName, health, walk, chase, vision, angle, hearing, attackRange, windup, cooldown, damage, stagger, knockback, threshold, resistance); // 근접형 행동 수치 구성
            EditorUtility.SetDirty(data); // 수정 데이터 저장 대상으로 표시
            return data; // 완성된 MonsterData 반환
        }

        private static void CreateSpawnLineFloor(Transform parent, Material spawnMaterial, Material accentMaterial) // 첨부 이미지의 SprintLane 북쪽 어두운 바닥에 4종 소환선 표시 생성
        {
            CreateBox(parent, "MonsterSpawnLine_Floor", SpawnLineCenter + new Vector3(0f, 0.025f, 0f), new Vector3(9.2f, 0.05f, 2.2f), spawnMaterial, false); // 네 몬스터가 나란히 설 어두운 붉은 소환선 바닥 생성
            CreateBox(parent, "MonsterSpawnLine_FrontTrim", SpawnLineCenter + new Vector3(0f, 0.055f, -1.02f), new Vector3(9.2f, 0.05f, 0.10f), accentMaterial, false); // 플레이어 쪽에서 위치를 알아보기 쉬운 전면 강조선 생성
        }

        private static void CreateSpawnPoints(Transform parent, GameObject[] prototypes, Material spawnMaterial, Material accentMaterial) // 부패한 망자·궁수·석상·미믹을 같은 Z축에 한 마리씩 나란히 소환하는 지점 생성
        {
            string[] spawnNames = { "CorruptedUndead", "CorruptedUndeadArcher", "SmilingStatue", "ChestMimic" }; // 왼쪽부터 오른쪽까지 4종 런타임 이름 정의

            for (int index = 0; index < prototypes.Length; index++) // 현재 테스트용 4종 SpawnPoint 반복 생성
            {
                float centeredIndex = index - ((prototypes.Length - 1) * 0.5f); // 짝수 개수도 SpawnLineCenter를 기준으로 좌우 대칭 배치할 인덱스 계산
                float x = SpawnLineCenter.x + (centeredIndex * SpawnSpacing); // 중앙 기준 가로 X 위치 계산
                Vector3 position = new Vector3(x, 0f, SpawnLineCenter.z); // 첨부 위치와 일치하는 같은 Z축 소환 위치 생성
                string spawnName = index < spawnNames.Length ? spawnNames[index] : $"Monster_{index + 1:00}"; // 현재 몬스터 종류 이름 조회
                GameObject spawnRoot = new GameObject($"MonsterSpawn_{index + 1:00}_{spawnName}"); // 런타임 소환 기능 루트 생성
                spawnRoot.transform.SetParent(parent); // Day17 시험장 아래 배치
                spawnRoot.transform.position = position; // 나란히 정렬된 월드 위치 적용
                spawnRoot.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // SprintLane 남쪽 플레이어 방향을 바라보도록 회전
                MonsterSpawnPoint spawnPoint = spawnRoot.AddComponent<MonsterSpawnPoint>(); // 공통 런타임 몬스터 소환 기능 추가
                spawnPoint.Configure(prototypes[index], $"Day17_{spawnName}", true, index * 0.25f); // 각 SpawnPoint에 서로 다른 프로토타입을 약간의 시차로 소환하도록 구성
                CreateBox(spawnRoot.transform, "SpawnBase", new Vector3(0f, 0.06f, 0f), new Vector3(1.55f, 0.12f, 1.35f), spawnMaterial, false, true); // 몬스터 발 위치를 막지 않는 시각 전용 소환 패드 생성
                CreateBox(spawnRoot.transform, "SpawnAccent", new Vector3(0f, 0.13f, -0.56f), new Vector3(1.28f, 0.05f, 0.10f), accentMaterial, false, true); // SpawnPoint 전면 강조선 생성
                EditorUtility.SetDirty(spawnPoint); // SpawnPoint 설정 씬 저장 대상으로 표시
            }
        }

        private static MonsterArrowProjectile CreateMonsterArrowTemplate(Transform parent, Material wood, Material bone, Material cloth) // 망자 궁수 공용 비활성 포물선 화살 템플릿 생성
        {
            GameObject root = new GameObject("Day17_UndeadArrowTemplate"); // 적 화살 템플릿 루트 생성
            root.transform.SetParent(parent); // Day17 시험장 아래 저장
            root.transform.position = new Vector3(0f, -100f, 0f); // 편집 화면에서 보이지 않는 위치로 이동
            Rigidbody body = root.AddComponent<Rigidbody>(); // 포물선 비행용 Rigidbody 추가
            body.mass = 0.06f; // 가벼운 목재 화살 질량 설정
            body.useGravity = true; // 런타임 화살 중력 기본 활성화
            body.isKinematic = true; // 템플릿 자체 물리 이동 비활성화
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>(); // 빠른 화살 충돌용 캡슐 Collider 추가
            collider.direction = 2; // 화살 전방 Z축으로 Collider 방향 지정
            collider.radius = 0.035f; // 가는 화살 샤프트 충돌 반경 설정
            collider.height = 0.92f; // 화살 전체 길이에 맞춘 충돌 높이 설정
            root.AddComponent<MonsterArrowProjectile>(); // Enemy Damage Pipeline 화살 기능 추가
            CreatePrimitivePart(root.transform, "Arrow_Shaft", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.02f), new Vector3(0.025f, 0.36f, 0.025f), wood, new Vector3(90f, 0f, 0f)); // 긴 목재 화살대 생성
            CreatePrimitivePart(root.transform, "Arrow_Head", PrimitiveType.Cube, new Vector3(0f, 0f, 0.39f), new Vector3(0.09f, 0.07f, 0.17f), bone, new Vector3(0f, 45f, 0f)); // 거친 뼈·금속 느낌 화살촉 생성
            CreatePrimitivePart(root.transform, "Fletching_L", PrimitiveType.Cube, new Vector3(-0.055f, 0f, -0.34f), new Vector3(0.08f, 0.012f, 0.18f), cloth, new Vector3(0f, 0f, 12f)); // 왼쪽 낡은 깃 생성
            CreatePrimitivePart(root.transform, "Fletching_R", PrimitiveType.Cube, new Vector3(0.055f, 0f, -0.34f), new Vector3(0.08f, 0.012f, 0.18f), cloth, new Vector3(0f, 0f, -12f)); // 오른쪽 낡은 깃 생성
            root.SetActive(false); // 씬 저장용 원본 화살 템플릿 비활성화
            return root.GetComponent<MonsterArrowProjectile>(); // 완성된 적 화살 템플릿 반환
        }

        private static GameObject CreateCorruptedUndeadArcherPrototype(Transform parent, MonsterData data, MonsterArrowProjectile arrowTemplate, Material flesh, Material bone, Material rust, Material cloth, Material bowWood, Material stringMaterial, Material eyeMaterial) // 프리미티브 조합 부패한 망자 궁수 프로토타입 생성
        {
            GameObject root = new GameObject("Day17_CorruptedUndeadArcher_Prototype"); // 비활성 런타임 복제용 몬스터 프로토타입 루트 생성
            root.transform.SetParent(parent); // Day17 시험장 아래 저장
            root.transform.position = new Vector3(0f, -100f, 0f); // 편집 화면에서 프로토타입 숨김 위치 지정
            CharacterController controller = root.AddComponent<CharacterController>(); // 충돌 기반 공통 몬스터 이동 추가
            controller.height = 2.15f; // 망자 궁수 전체 키에 맞는 충돌 높이 설정
            controller.radius = 0.38f; // 몸통 폭에 맞는 충돌 반경 설정
            controller.center = new Vector3(0f, 1.075f, 0f); // 발바닥 기준 충돌 중심 높이 설정
            controller.stepOffset = 0.28f; // 테스트 맵 작은 턱 통과 높이 설정
            CombatHealth health = root.AddComponent<CombatHealth>(); // 기존 Damage Pipeline 공통 체력 추가
            health.Configure(data.DisplayName, CombatFaction.Enemy, data.MaxHealth); // Enemy 진영·체력 구성
            CombatReaction reaction = root.AddComponent<CombatReaction>(); // 기존 경직·넉백 반응 연결
            reaction.Configure(data.StaggerThreshold, 10f, 0.42f, data.KnockbackResistance, 9f, 0.38f); // 부패한 망자 경직·넉백 저항 수치 구성
            MonsterTargetSelector selector = root.AddComponent<MonsterTargetSelector>(); // 대상·마지막 위치 기억 기능 추가
            MonsterMotor motor = root.AddComponent<MonsterMotor>(); // 공통 이동 계층 추가
            MonsterSensor sensor = root.AddComponent<MonsterSensor>(); // 시각·청각 감각 추가
            CorruptedUndeadArcherAttack attack = root.AddComponent<CorruptedUndeadArcherAttack>(); // 활 원거리 공격 기능 추가
            MonsterBrain brain = root.AddComponent<MonsterBrain>(); // 감지·이동·공격 상태 머신 추가
            GameObject visualRoot = new GameObject("UndeadVisual"); // 망자 외형 전체 시각 루트 생성
            visualRoot.transform.SetParent(root.transform, false); // 몬스터 루트 자식 연결
            Transform visual = visualRoot.transform; // 시각 루트 Transform 저장
            Transform pelvis = CreatePrimitivePart(visual, "Pelvis", PrimitiveType.Capsule, new Vector3(0f, 0.87f, 0f), new Vector3(0.42f, 0.34f, 0.30f), flesh, Vector3.zero).transform; // 골반·하복부 생성
            CreatePrimitivePart(visual, "Torso", PrimitiveType.Capsule, new Vector3(0f, 1.38f, 0f), new Vector3(0.52f, 0.48f, 0.34f), flesh, new Vector3(0f, 0f, -5f)); // 비틀린 상체 생성
            CreatePrimitivePart(visual, "ChestArmor", PrimitiveType.Cube, new Vector3(0.02f, 1.47f, 0.23f), new Vector3(0.72f, 0.47f, 0.09f), rust, new Vector3(-4f, 0f, 5f)); // 깨진 녹슨 흉갑 조각 생성
            CreatePrimitivePart(visual, "Rib_01", PrimitiveType.Cube, new Vector3(-0.28f, 1.40f, 0.34f), new Vector3(0.20f, 0.05f, 0.07f), bone, new Vector3(0f, 0f, -10f)); // 왼쪽 노출 갈비뼈 생성
            CreatePrimitivePart(visual, "Rib_02", PrimitiveType.Cube, new Vector3(-0.26f, 1.31f, 0.34f), new Vector3(0.22f, 0.05f, 0.07f), bone, new Vector3(0f, 0f, -6f)); // 왼쪽 하단 갈비뼈 생성
            CreatePrimitivePart(visual, "Rib_03", PrimitiveType.Cube, new Vector3(0.28f, 1.39f, 0.34f), new Vector3(0.20f, 0.05f, 0.07f), bone, new Vector3(0f, 0f, 10f)); // 오른쪽 노출 갈비뼈 생성
            CreatePrimitivePart(visual, "Rib_04", PrimitiveType.Cube, new Vector3(0.26f, 1.30f, 0.34f), new Vector3(0.22f, 0.05f, 0.07f), bone, new Vector3(0f, 0f, 6f)); // 오른쪽 하단 갈비뼈 생성
            CreatePrimitivePart(visual, "Neck", PrimitiveType.Cylinder, new Vector3(0f, 1.78f, 0f), new Vector3(0.14f, 0.18f, 0.14f), flesh, new Vector3(0f, 0f, 4f)); // 가는 부패 목 생성
            CreatePrimitivePart(visual, "Head", PrimitiveType.Sphere, new Vector3(-0.04f, 2.00f, 0.02f), new Vector3(0.48f, 0.58f, 0.44f), flesh, new Vector3(5f, -4f, -6f)); // 비대칭 부패 머리 생성
            CreatePrimitivePart(visual, "SkullPlate", PrimitiveType.Sphere, new Vector3(-0.12f, 2.08f, 0.14f), new Vector3(0.34f, 0.28f, 0.16f), bone, new Vector3(-8f, 6f, 12f)); // 피부가 벗겨진 두개골 조각 생성
            CreatePrimitivePart(visual, "Jaw", PrimitiveType.Cube, new Vector3(0.01f, 1.83f, 0.15f), new Vector3(0.34f, 0.15f, 0.22f), bone, new Vector3(8f, 0f, -4f)); // 벌어진 뼈 턱 생성
            CreatePrimitivePart(visual, "Eye_L", PrimitiveType.Sphere, new Vector3(-0.12f, 2.03f, 0.23f), new Vector3(0.075f, 0.075f, 0.055f), eyeMaterial, Vector3.zero); // 왼쪽 붉은 눈 생성
            CreatePrimitivePart(visual, "Eye_R", PrimitiveType.Sphere, new Vector3(0.10f, 2.03f, 0.23f), new Vector3(0.065f, 0.065f, 0.050f), eyeMaterial, Vector3.zero); // 오른쪽 붉은 눈 생성
            Transform eyePoint = new GameObject("EyePoint").transform; // 시각 Raycast 전용 눈 위치 생성
            eyePoint.SetParent(root.transform, false); // 몬스터 루트 자식 연결
            eyePoint.localPosition = new Vector3(0f, 1.98f, 0.28f); // 실제 눈 앞쪽 위치 지정
            CreatePrimitivePart(visual, "Leg_L", PrimitiveType.Cylinder, new Vector3(-0.20f, 0.46f, 0f), new Vector3(0.14f, 0.42f, 0.14f), flesh, new Vector3(0f, 0f, 3f)); // 왼쪽 부패 다리 생성
            CreatePrimitivePart(visual, "Leg_R", PrimitiveType.Cylinder, new Vector3(0.21f, 0.46f, -0.02f), new Vector3(0.14f, 0.42f, 0.14f), flesh, new Vector3(0f, 0f, -5f)); // 오른쪽 부패 다리 생성
            CreatePrimitivePart(visual, "Boot_L", PrimitiveType.Cube, new Vector3(-0.20f, 0.10f, 0.09f), new Vector3(0.27f, 0.17f, 0.42f), cloth, Vector3.zero); // 왼발 낡은 장화 생성
            CreatePrimitivePart(visual, "Boot_R", PrimitiveType.Cube, new Vector3(0.21f, 0.10f, 0.07f), new Vector3(0.27f, 0.17f, 0.42f), cloth, Vector3.zero); // 오른발 낡은 장화 생성
            Transform leftArm = new GameObject("Arm_L_Root").transform; // 활 지지 팔 애니메이션 루트 생성
            leftArm.SetParent(visual, false); // 시각 루트 자식 연결
            leftArm.localPosition = new Vector3(-0.37f, 1.56f, 0.04f); // 왼쪽 어깨 위치 지정
            leftArm.localRotation = Quaternion.Euler(0f, -8f, 20f); // 활을 든 기본 왼팔 각도 지정
            CreatePrimitivePart(leftArm, "UpperArm_L", PrimitiveType.Cylinder, new Vector3(-0.20f, -0.12f, 0.10f), new Vector3(0.11f, 0.31f, 0.11f), flesh, new Vector3(22f, 0f, 64f)); // 왼쪽 상완 생성
            CreatePrimitivePart(leftArm, "Forearm_L", PrimitiveType.Cylinder, new Vector3(-0.43f, -0.08f, 0.27f), new Vector3(0.10f, 0.31f, 0.10f), bone, new Vector3(58f, 0f, 72f)); // 뼈가 드러난 왼쪽 전완 생성
            Transform rightArm = new GameObject("Arm_R_Root").transform; // 시위 당김 팔 애니메이션 루트 생성
            rightArm.SetParent(visual, false); // 시각 루트 자식 연결
            rightArm.localPosition = new Vector3(0.38f, 1.56f, 0.03f); // 오른쪽 어깨 위치 지정
            rightArm.localRotation = Quaternion.Euler(0f, 5f, -18f); // 활 준비 기본 오른팔 각도 지정
            CreatePrimitivePart(rightArm, "UpperArm_R", PrimitiveType.Cylinder, new Vector3(0.20f, -0.10f, 0.05f), new Vector3(0.11f, 0.31f, 0.11f), flesh, new Vector3(-16f, 0f, -64f)); // 오른쪽 상완 생성
            CreatePrimitivePart(rightArm, "Forearm_R", PrimitiveType.Cylinder, new Vector3(0.39f, -0.05f, 0.20f), new Vector3(0.10f, 0.30f, 0.10f), bone, new Vector3(48f, 0f, -68f)); // 오른쪽 전완 생성
            CreatePrimitivePart(visual, "Rag_SkirtFront", PrimitiveType.Cube, new Vector3(0f, 0.88f, 0.26f), new Vector3(0.66f, 0.48f, 0.05f), cloth, new Vector3(8f, 0f, 0f)); // 허리 아래 찢어진 천 앞자락 생성
            CreatePrimitivePart(visual, "Rag_SkirtLeft", PrimitiveType.Cube, new Vector3(-0.31f, 0.83f, 0.04f), new Vector3(0.18f, 0.55f, 0.05f), cloth, new Vector3(0f, 10f, -6f)); // 왼쪽 찢어진 천 조각 생성
            CreatePrimitivePart(visual, "ShoulderPlate", PrimitiveType.Cube, new Vector3(0.43f, 1.63f, 0.02f), new Vector3(0.36f, 0.12f, 0.34f), rust, new Vector3(0f, 8f, -12f)); // 오른쪽 녹슨 어깨 갑옷 생성
            Transform bowRoot = new GameObject("BowRoot").transform; // 활 조준 애니메이션 루트 생성
            bowRoot.SetParent(visual, false); // 몬스터 시각 루트 자식 연결
            bowRoot.localPosition = new Vector3(-0.64f, 1.47f, 0.39f); // 왼손 앞쪽 활 위치 지정
            bowRoot.localRotation = Quaternion.Euler(0f, 4f, 3f); // 기본 활 정면 자세 지정
            CreatePrimitivePart(bowRoot, "Bow_Grip", PrimitiveType.Cube, Vector3.zero, new Vector3(0.10f, 0.36f, 0.10f), bowWood, Vector3.zero); // 활 중앙 손잡이 생성
            CreatePrimitivePart(bowRoot, "Bow_Limb_Left", PrimitiveType.Cube, new Vector3(-0.38f, 0.22f, 0f), new Vector3(0.72f, 0.07f, 0.08f), bowWood, new Vector3(0f, 0f, 24f)); // 활 왼쪽 상단 활대 생성
            CreatePrimitivePart(bowRoot, "Bow_Limb_Right", PrimitiveType.Cube, new Vector3(0.38f, -0.22f, 0f), new Vector3(0.72f, 0.07f, 0.08f), bowWood, new Vector3(0f, 0f, 24f)); // 활 오른쪽 하단 활대 생성
            CreatePrimitivePart(bowRoot, "Bow_Limb_Upper", PrimitiveType.Cube, new Vector3(0.25f, 0.40f, 0f), new Vector3(0.52f, 0.065f, 0.075f), bowWood, new Vector3(0f, 0f, -26f)); // 활 상단 반대쪽 곡선 활대 생성
            CreatePrimitivePart(bowRoot, "Bow_Limb_Lower", PrimitiveType.Cube, new Vector3(-0.25f, -0.40f, 0f), new Vector3(0.52f, 0.065f, 0.075f), bowWood, new Vector3(0f, 0f, -26f)); // 활 하단 반대쪽 곡선 활대 생성
            Transform stringRoot = new GameObject("Bow_StringRoot").transform; // 시위 당김 위치 전체 루트 생성
            stringRoot.SetParent(bowRoot, false); // 활 루트 자식 연결
            CreatePrimitivePart(stringRoot, "Bow_String_Upper", PrimitiveType.Cylinder, new Vector3(0f, 0.34f, 0.02f), new Vector3(0.015f, 0.34f, 0.015f), stringMaterial, Vector3.zero); // 상단 시위 생성
            CreatePrimitivePart(stringRoot, "Bow_String_Lower", PrimitiveType.Cylinder, new Vector3(0f, -0.34f, 0.02f), new Vector3(0.015f, 0.34f, 0.015f), stringMaterial, Vector3.zero); // 하단 시위 생성
            GameObject nockedArrow = CreatePrimitivePart(bowRoot, "NockedArrow", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.34f), new Vector3(0.025f, 0.32f, 0.025f), bowWood, new Vector3(90f, 0f, 0f)); // 활에 얹힌 장전 화살 시각 생성
            CreatePrimitivePart(nockedArrow.transform, "NockedArrowHead", PrimitiveType.Cube, new Vector3(0f, 0.24f, 0f), new Vector3(0.08f, 0.13f, 0.08f), bone, new Vector3(0f, 45f, 0f)); // 장전 화살촉 생성
            Transform muzzle = new GameObject("ArrowMuzzle").transform; // 실제 적 화살 생성 위치 생성
            muzzle.SetParent(bowRoot, false); // 활 루트 자식 연결
            muzzle.localPosition = new Vector3(0f, 0f, 0.68f); // 활 앞쪽 화살 발사 위치 지정
            GameObject quiver = new GameObject("Quiver"); // 등에 찬 화살통 루트 생성
            quiver.transform.SetParent(visual, false); // 몬스터 시각 루트 자식 연결
            quiver.transform.localPosition = new Vector3(0.36f, 1.27f, -0.25f); // 오른쪽 등 뒤 화살통 위치 지정
            quiver.transform.localRotation = Quaternion.Euler(-12f, 0f, -16f); // 비스듬한 화살통 각도 지정
            CreatePrimitivePart(quiver.transform, "QuiverBody", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.17f, 0.46f, 0.17f), cloth, new Vector3(0f, 0f, 0f)); // 가죽 화살통 본체 생성

            for (int arrowIndex = 0; arrowIndex < 5; arrowIndex++) // 화살통에 보이는 예비 화살 5개 생성
            {
                float xOffset = ((arrowIndex % 3) - 1) * 0.07f; // 화살통 안 좌우 위치 계산
                float zOffset = (arrowIndex / 3) * 0.06f; // 화살통 앞뒤 위치 계산
                CreatePrimitivePart(quiver.transform, $"QuiverArrow_{arrowIndex + 1}", PrimitiveType.Cylinder, new Vector3(xOffset, 0.42f, zOffset), new Vector3(0.018f, 0.34f, 0.018f), bowWood, Vector3.zero); // 예비 화살대 생성
            }

            sensor.Configure(data, eyePoint, root); // 시야·청각 감각에 데이터·눈·자기 루트 연결
            motor.Configure(data, reaction); // 이동 계층에 데이터·경직 반응 연결
            attack.Configure(data, sensor, muzzle, arrowTemplate, bowRoot, stringRoot, leftArm, rightArm, nockedArrow); // 활 공격 기능에 시각·투사체·데이터 연결
            brain.Configure(data, health, reaction, sensor, selector, motor, attack); // 공통 AI Brain 전체 참조 연결
            root.SetActive(false); // 씬에는 프로토타입만 저장하고 Play Mode SpawnPoint가 복제하도록 비활성화
            EditorUtility.SetDirty(health); // 공통 체력 설정 저장 대상으로 표시
            EditorUtility.SetDirty(reaction); // 공통 반응 설정 저장 대상으로 표시
            EditorUtility.SetDirty(sensor); // 감각 설정 저장 대상으로 표시
            EditorUtility.SetDirty(motor); // 이동 설정 저장 대상으로 표시
            EditorUtility.SetDirty(attack); // 활 공격 설정 저장 대상으로 표시
            EditorUtility.SetDirty(brain); // AI Brain 설정 저장 대상으로 표시
            _ = pelvis; // 모델 골반 Transform 생성 완료 명시
            return root; // 완성된 비활성 궁수 프로토타입 반환
        }


        private static GameObject CreateCorruptedUndeadMeleePrototype(Transform parent, MonsterData data, Material flesh, Material bone, Material rust, Material cloth, Material eyeMaterial) // 기본 추적·근접 공격 부패한 망자 프로토타입 생성
        {
            GameObject root = new GameObject("Day17_CorruptedUndead_Prototype"); // 비활성 런타임 복제용 기본 망자 프로토타입 생성
            root.transform.SetParent(parent); // Day17 시험장 아래 저장
            root.transform.position = new Vector3(0f, -100f, 0f); // 편집 화면에서 프로토타입 숨김 위치 지정
            CharacterController controller = root.AddComponent<CharacterController>(); // 충돌 기반 이동용 CharacterController 추가
            controller.height = 2.08f; // 기본 망자 키에 맞는 충돌 높이 설정
            controller.radius = 0.40f; // 몸통 폭에 맞는 충돌 반경 설정
            controller.center = new Vector3(0f, 1.04f, 0f); // 발바닥 기준 충돌 중심 설정
            controller.stepOffset = 0.26f; // 테스트 맵 작은 턱 통과 높이 설정
            CombatHealth health = root.AddComponent<CombatHealth>(); // 기존 Damage Pipeline 공통 체력 추가
            health.Configure(data.DisplayName, CombatFaction.Enemy, data.MaxHealth); // Enemy 진영·최대 체력 구성
            CombatReaction reaction = root.AddComponent<CombatReaction>(); // 기존 경직·넉백 반응 연결
            reaction.Configure(data.StaggerThreshold, 11f, 0.44f, data.KnockbackResistance, 9f, 0.40f); // 근접 망자 경직·넉백 저항 수치 구성
            MonsterTargetSelector selector = root.AddComponent<MonsterTargetSelector>(); // 대상·마지막 확인 위치 기억 기능 추가
            MonsterMotor motor = root.AddComponent<MonsterMotor>(); // 공통 충돌 이동 계층 추가
            MonsterSensor sensor = root.AddComponent<MonsterSensor>(); // 시각·청각 감지 추가
            MonsterMeleeAttack meleeAttack = root.AddComponent<MonsterMeleeAttack>(); // 공통 단발 근접 공격 추가
            MonsterBrain brain = root.AddComponent<MonsterBrain>(); // 감지·추적·근접 공격 상태 머신 추가
            GameObject visualObject = new GameObject("UndeadMeleeVisual"); // 근접 망자 외형 시각 루트 생성
            visualObject.transform.SetParent(root.transform, false); // 몬스터 루트 자식 연결
            Transform visual = visualObject.transform; // 시각 Transform 저장
            CreatePrimitivePart(visual, "Melee_Pelvis", PrimitiveType.Capsule, new Vector3(0f, 0.84f, 0f), new Vector3(0.43f, 0.34f, 0.31f), flesh, new Vector3(0f, 0f, 4f)); // 뒤틀린 골반 생성
            CreatePrimitivePart(visual, "Melee_Torso", PrimitiveType.Capsule, new Vector3(0f, 1.36f, 0.02f), new Vector3(0.54f, 0.49f, 0.35f), flesh, new Vector3(5f, 0f, -7f)); // 부패한 상체 생성
            CreatePrimitivePart(visual, "Melee_ChestArmor", PrimitiveType.Cube, new Vector3(-0.05f, 1.47f, 0.24f), new Vector3(0.66f, 0.42f, 0.09f), rust, new Vector3(-5f, -4f, -9f)); // 깨진 녹슨 흉갑 생성

            for (int ribIndex = 0; ribIndex < 4; ribIndex++) // 노출된 갈비뼈 4개 생성
            {
                float side = ribIndex < 2 ? -1f : 1f; // 좌우 갈비뼈 방향 계산
                float row = ribIndex % 2; // 상하 갈비뼈 행 계산
                CreatePrimitivePart(visual, $"Melee_Rib_{ribIndex + 1:00}", PrimitiveType.Cube, new Vector3(side * 0.27f, 1.39f - (row * 0.10f), 0.34f), new Vector3(0.21f, 0.05f, 0.07f), bone, new Vector3(0f, 0f, side * 9f)); // 좌우 갈비뼈 조각 생성
            }

            CreatePrimitivePart(visual, "Melee_Neck", PrimitiveType.Cylinder, new Vector3(0.03f, 1.76f, 0f), new Vector3(0.14f, 0.18f, 0.14f), flesh, new Vector3(0f, 0f, -5f)); // 가는 부패 목 생성
            CreatePrimitivePart(visual, "Melee_Head", PrimitiveType.Sphere, new Vector3(0.06f, 1.98f, 0.02f), new Vector3(0.48f, 0.56f, 0.43f), flesh, new Vector3(-6f, 7f, 8f)); // 비뚤어진 부패 머리 생성
            CreatePrimitivePart(visual, "Melee_Skull", PrimitiveType.Sphere, new Vector3(0.15f, 2.07f, 0.12f), new Vector3(0.30f, 0.25f, 0.15f), bone, new Vector3(7f, -5f, -8f)); // 드러난 두개골 조각 생성
            CreatePrimitivePart(visual, "Melee_Jaw", PrimitiveType.Cube, new Vector3(0.04f, 1.82f, 0.17f), new Vector3(0.34f, 0.15f, 0.20f), bone, new Vector3(12f, 0f, 4f)); // 벌어진 턱 생성
            CreatePrimitivePart(visual, "Melee_Eye_L", PrimitiveType.Sphere, new Vector3(-0.07f, 2.01f, 0.23f), new Vector3(0.065f, 0.065f, 0.052f), eyeMaterial, Vector3.zero); // 왼쪽 붉은 눈 생성
            CreatePrimitivePart(visual, "Melee_Eye_R", PrimitiveType.Sphere, new Vector3(0.16f, 2.01f, 0.22f), new Vector3(0.07f, 0.07f, 0.054f), eyeMaterial, Vector3.zero); // 오른쪽 붉은 눈 생성
            Transform eyePoint = new GameObject("EyePoint").transform; // 시각 Raycast 기준 눈 위치 생성
            eyePoint.SetParent(root.transform, false); // 몬스터 루트 자식 연결
            eyePoint.localPosition = new Vector3(0f, 1.96f, 0.27f); // 실제 눈 앞쪽 위치 지정
            CreatePrimitivePart(visual, "Melee_Leg_L", PrimitiveType.Cylinder, new Vector3(-0.20f, 0.44f, 0f), new Vector3(0.15f, 0.40f, 0.15f), flesh, new Vector3(0f, 0f, 7f)); // 왼쪽 다리 생성
            CreatePrimitivePart(visual, "Melee_Leg_R", PrimitiveType.Cylinder, new Vector3(0.21f, 0.43f, -0.03f), new Vector3(0.15f, 0.41f, 0.15f), flesh, new Vector3(0f, 0f, -5f)); // 오른쪽 다리 생성
            CreatePrimitivePart(visual, "Melee_Boot_L", PrimitiveType.Cube, new Vector3(-0.20f, 0.09f, 0.10f), new Vector3(0.28f, 0.17f, 0.43f), cloth, Vector3.zero); // 왼쪽 낡은 장화 생성
            CreatePrimitivePart(visual, "Melee_Boot_R", PrimitiveType.Cube, new Vector3(0.21f, 0.09f, 0.08f), new Vector3(0.28f, 0.17f, 0.43f), cloth, Vector3.zero); // 오른쪽 낡은 장화 생성
            Transform leftArm = new GameObject("Melee_Arm_L_Root").transform; // 왼쪽 전방 팔 루트 생성
            leftArm.SetParent(visual, false); // 시각 루트 자식 연결
            leftArm.localPosition = new Vector3(-0.38f, 1.55f, 0.03f); // 왼쪽 어깨 위치 지정
            leftArm.localRotation = Quaternion.Euler(18f, -8f, 26f); // 비틀린 왼팔 기본 각도 지정
            CreatePrimitivePart(leftArm, "Melee_UpperArm_L", PrimitiveType.Cylinder, new Vector3(-0.18f, -0.13f, 0.12f), new Vector3(0.12f, 0.32f, 0.12f), flesh, new Vector3(26f, 0f, 58f)); // 왼쪽 상완 생성
            CreatePrimitivePart(leftArm, "Melee_Claw_L", PrimitiveType.Cube, new Vector3(-0.39f, -0.08f, 0.36f), new Vector3(0.18f, 0.10f, 0.32f), bone, new Vector3(15f, 0f, 10f)); // 왼쪽 뼈 손·발톱 생성
            Transform rightArm = new GameObject("Melee_Arm_R_Root").transform; // 실제 공격 모션 오른팔 루트 생성
            rightArm.SetParent(visual, false); // 시각 루트 자식 연결
            rightArm.localPosition = new Vector3(0.40f, 1.56f, 0.02f); // 오른쪽 어깨 위치 지정
            rightArm.localRotation = Quaternion.Euler(12f, 8f, -22f); // 공격 전 오른팔 기본 각도 지정
            CreatePrimitivePart(rightArm, "Melee_UpperArm_R", PrimitiveType.Cylinder, new Vector3(0.19f, -0.12f, 0.12f), new Vector3(0.12f, 0.33f, 0.12f), flesh, new Vector3(25f, 0f, -58f)); // 오른쪽 상완 생성
            CreatePrimitivePart(rightArm, "Melee_Claw_R", PrimitiveType.Cube, new Vector3(0.40f, -0.07f, 0.37f), new Vector3(0.20f, 0.11f, 0.34f), bone, new Vector3(-12f, 0f, -8f)); // 실제 공격 뼈 손·발톱 생성
            CreatePrimitivePart(visual, "Melee_Rag", PrimitiveType.Cube, new Vector3(-0.30f, 0.92f, 0.24f), new Vector3(0.26f, 0.52f, 0.05f), cloth, new Vector3(6f, -8f, 9f)); // 허리 찢어진 천 조각 생성
            Transform attackOrigin = new GameObject("MeleeAttackOrigin").transform; // 근접 피해 시작 위치 생성
            attackOrigin.SetParent(root.transform, false); // 몬스터 루트 자식 연결
            attackOrigin.localPosition = new Vector3(0f, 1.35f, 0.48f); // 몸 앞쪽 공격 시작점 지정
            sensor.Configure(data, eyePoint, root); // 시야·청각 감각 연결
            motor.Configure(data, reaction); // 이동 데이터·경직 반응 연결
            meleeAttack.Configure(data, sensor, attackOrigin, rightArm, new Vector3(-28f, -8f, 34f), new Vector3(42f, 10f, -48f)); // 오른팔을 뒤로 당겼다가 앞으로 긁는 단발 공격 구성
            brain.Configure(data, health, reaction, sensor, selector, motor, null, meleeAttack); // 공통 Brain에 근접 공격 구조 연결
            root.SetActive(false); // 씬에는 프로토타입만 저장하고 SpawnPoint가 런타임 복제하도록 비활성화
            EditorUtility.SetDirty(health); // 체력 설정 저장 대상으로 표시
            EditorUtility.SetDirty(reaction); // 반응 설정 저장 대상으로 표시
            EditorUtility.SetDirty(sensor); // 감각 설정 저장 대상으로 표시
            EditorUtility.SetDirty(motor); // 이동 설정 저장 대상으로 표시
            EditorUtility.SetDirty(meleeAttack); // 근접 공격 설정 저장 대상으로 표시
            EditorUtility.SetDirty(brain); // 공통 AI 설정 저장 대상으로 표시
            return root; // 완성된 근접 부패한 망자 프로토타입 반환
        }

        private static GameObject CreateSmilingStatuePrototype(Transform parent, MonsterData data, Material stone, Material darkStone) // 관찰되면 멈추는 웃는 석상 프로토타입 생성
        {
            GameObject root = new GameObject("Day17_SmilingStatue_Prototype"); // 비활성 웃는 석상 프로토타입 루트 생성
            root.transform.SetParent(parent); // Day17 시험장 아래 저장
            root.transform.position = new Vector3(0f, -100f, 0f); // 편집 화면 숨김 위치 지정
            CharacterController controller = root.AddComponent<CharacterController>(); // 석상 충돌 이동용 CharacterController 추가
            controller.height = 2.35f; // 석상 전체 키에 맞는 충돌 높이 설정
            controller.radius = 0.43f; // 석상 몸체 폭 충돌 반경 설정
            controller.center = new Vector3(0f, 1.175f, 0f); // 발바닥 기준 충돌 중심 설정
            controller.stepOffset = 0.22f; // 작은 턱 이동 허용 높이 설정
            MonsterMotor motor = root.AddComponent<MonsterMotor>(); // 체력·피격 없이 시야 밖에서만 추적하는 이동 계층 추가
            MonsterMeleeAttack meleeAttack = root.AddComponent<MonsterMeleeAttack>(); // 관찰되지 않을 때 근접 공격 기능 추가
            SmilingStatueBehavior behavior = root.AddComponent<SmilingStatueBehavior>(); // 플레이어 관찰 기반 규칙 행동 추가
            GameObject visualObject = new GameObject("SmilingStatueVisual"); // 석상 시각 루트 생성
            visualObject.transform.SetParent(root.transform, false); // 석상 루트 자식 연결
            Transform visual = visualObject.transform; // 시각 Transform 저장
            CreatePrimitivePart(visual, "Statue_Pedestal", PrimitiveType.Cylinder, new Vector3(0f, 0.10f, 0f), new Vector3(0.60f, 0.10f, 0.60f), stone, Vector3.zero); // 원형 석상 받침대 생성
            CreatePrimitivePart(visual, "Statue_LowerRobe", PrimitiveType.Cylinder, new Vector3(0f, 0.56f, 0f), new Vector3(0.48f, 0.48f, 0.48f), stone, new Vector3(0f, 0f, 0f)); // 긴 석재 하체·로브 생성
            CreatePrimitivePart(visual, "Statue_Torso", PrimitiveType.Capsule, new Vector3(0f, 1.34f, 0f), new Vector3(0.50f, 0.52f, 0.34f), stone, new Vector3(0f, 0f, -2f)); // 상체 석재 몸통 생성
            CreatePrimitivePart(visual, "Statue_Neck", PrimitiveType.Cylinder, new Vector3(0f, 1.78f, 0f), new Vector3(0.16f, 0.16f, 0.16f), stone, Vector3.zero); // 목 생성
            CreatePrimitivePart(visual, "Statue_Head", PrimitiveType.Sphere, new Vector3(0f, 2.04f, 0.01f), new Vector3(0.52f, 0.58f, 0.46f), stone, new Vector3(-2f, 0f, 0f)); // 석상 머리 생성
            CreatePrimitivePart(visual, "Statue_EyeSocket_L", PrimitiveType.Sphere, new Vector3(-0.13f, 2.08f, 0.25f), new Vector3(0.075f, 0.055f, 0.035f), darkStone, Vector3.zero); // 왼쪽 검은 눈 홈 생성
            CreatePrimitivePart(visual, "Statue_EyeSocket_R", PrimitiveType.Sphere, new Vector3(0.13f, 2.08f, 0.25f), new Vector3(0.075f, 0.055f, 0.035f), darkStone, Vector3.zero); // 오른쪽 검은 눈 홈 생성
            Transform smileRoot = new GameObject("Statue_SmileRoot").transform; // 관찰 상태를 보여줄 미소 시각 루트 생성
            smileRoot.SetParent(visual, false); // 시각 루트 자식 연결
            smileRoot.localPosition = new Vector3(0f, 1.91f, 0.25f); // 얼굴 앞쪽 입 위치 지정
            CreatePrimitivePart(smileRoot, "Statue_Smile_Left", PrimitiveType.Cube, new Vector3(-0.10f, 0.01f, 0f), new Vector3(0.18f, 0.035f, 0.035f), darkStone, new Vector3(0f, 0f, -14f)); // 왼쪽 올라간 미소선 생성
            CreatePrimitivePart(smileRoot, "Statue_Smile_Right", PrimitiveType.Cube, new Vector3(0.10f, 0.01f, 0f), new Vector3(0.18f, 0.035f, 0.035f), darkStone, new Vector3(0f, 0f, 14f)); // 오른쪽 올라간 미소선 생성
            Transform leftArm = new GameObject("Statue_Arm_L_Root").transform; // 왼쪽 석상 팔 루트 생성
            leftArm.SetParent(visual, false); // 시각 루트 자식 연결
            leftArm.localPosition = new Vector3(-0.42f, 1.55f, 0.03f); // 왼쪽 어깨 위치 지정
            leftArm.localRotation = Quaternion.Euler(-8f, 0f, 18f); // 앞쪽을 향한 왼팔 자세 지정
            CreatePrimitivePart(leftArm, "Statue_Arm_L", PrimitiveType.Cylinder, new Vector3(-0.20f, -0.12f, 0.13f), new Vector3(0.13f, 0.36f, 0.13f), stone, new Vector3(30f, 0f, 60f)); // 왼쪽 석재 팔 생성
            Transform rightArm = new GameObject("Statue_Arm_R_Root").transform; // 실제 공격 모션 오른팔 루트 생성
            rightArm.SetParent(visual, false); // 시각 루트 자식 연결
            rightArm.localPosition = new Vector3(0.42f, 1.55f, 0.03f); // 오른쪽 어깨 위치 지정
            rightArm.localRotation = Quaternion.Euler(-8f, 0f, -18f); // 앞쪽을 향한 오른팔 자세 지정
            CreatePrimitivePart(rightArm, "Statue_Arm_R", PrimitiveType.Cylinder, new Vector3(0.20f, -0.12f, 0.13f), new Vector3(0.13f, 0.36f, 0.13f), stone, new Vector3(30f, 0f, -60f)); // 오른쪽 석재 팔 생성
            CreatePrimitivePart(visual, "Statue_Hand_L", PrimitiveType.Sphere, new Vector3(-0.54f, 1.24f, 0.30f), new Vector3(0.20f, 0.17f, 0.15f), stone, Vector3.zero); // 왼손 생성
            CreatePrimitivePart(visual, "Statue_Hand_R", PrimitiveType.Sphere, new Vector3(0.54f, 1.24f, 0.30f), new Vector3(0.20f, 0.17f, 0.15f), stone, Vector3.zero); // 오른손 생성
            Transform facePoint = new GameObject("Statue_FacePoint").transform; // 플레이어 카메라 관찰 Raycast 목표점 생성
            facePoint.SetParent(root.transform, false); // 석상 루트 자식 연결
            facePoint.localPosition = new Vector3(0f, 2.05f, 0.28f); // 얼굴 정면 목표 위치 지정
            Transform attackOrigin = new GameObject("Statue_AttackOrigin").transform; // 석상 근접 공격 시작 위치 생성
            attackOrigin.SetParent(root.transform, false); // 석상 루트 자식 연결
            attackOrigin.localPosition = new Vector3(0f, 1.45f, 0.50f); // 몸 앞쪽 공격 시작 위치 지정
            motor.Configure(data, null); // 경직·넉백이 없는 불사 석상 이동 데이터 연결
            meleeAttack.Configure(data, null, attackOrigin, rightArm, new Vector3(-18f, -8f, 24f), new Vector3(38f, 8f, -42f)); // 석상 오른팔 단순 타격 모션 구성
            behavior.Configure(data, motor, meleeAttack, facePoint, smileRoot); // 화면 안에 보이면 이동·공격이 완전히 멈추는 불사 관찰 규칙 구성
            root.SetActive(false); // 런타임 SpawnPoint 복제용 비활성 프로토타입 저장
            EditorUtility.SetDirty(motor); // 이동 설정 저장 대상으로 표시
            EditorUtility.SetDirty(meleeAttack); // 공격 설정 저장 대상으로 표시
            EditorUtility.SetDirty(behavior); // 관찰 규칙 설정 저장 대상으로 표시
            return root; // 완성된 웃는 석상 프로토타입 반환
        }

        private static GameObject CreateChestMimicPrototype(Transform parent, MonsterData data, Material wood, Material rust, Material bone, Material tongue, Material eyeMaterial) // 상자 위장·변신·추적·공격 미믹 프로토타입 생성
        {
            GameObject root = new GameObject("Day17_ChestMimic_Prototype"); // 비활성 상자 미믹 프로토타입 생성
            root.transform.SetParent(parent); // Day17 시험장 아래 저장
            root.transform.position = new Vector3(0f, -100f, 0f); // 편집 화면 숨김 위치 지정
            CharacterController controller = root.AddComponent<CharacterController>(); // 변신 후 이동 충돌용 CharacterController 추가
            controller.height = 1.42f; // 상자와 열린 뚜껑 포함 충돌 높이 설정
            controller.radius = 0.52f; // 상자 폭 기반 충돌 반경 설정
            controller.center = new Vector3(0f, 0.71f, 0f); // 지면 기준 충돌 중심 설정
            controller.stepOffset = 0.20f; // 작은 턱 통과 높이 설정
            CombatHealth health = root.AddComponent<CombatHealth>(); // 공통 Damage Pipeline 체력 추가
            health.Configure(data.DisplayName, CombatFaction.Enemy, data.MaxHealth); // Enemy 진영·미믹 체력 구성
            CombatReaction reaction = root.AddComponent<CombatReaction>(); // 경직·넉백 반응 추가
            reaction.Configure(data.StaggerThreshold, 8f, 0.36f, data.KnockbackResistance, 8f, 0.50f); // 미믹 경직·넉백 저항 구성
            MonsterTargetSelector selector = root.AddComponent<MonsterTargetSelector>(); // 변신 후 대상·기억 기능 추가
            MonsterMotor motor = root.AddComponent<MonsterMotor>(); // 변신 후 추적 이동 계층 추가
            MonsterSensor sensor = root.AddComponent<MonsterSensor>(); // 변신 후 시각·청각 감각 추가
            MonsterMeleeAttack meleeAttack = root.AddComponent<MonsterMeleeAttack>(); // 변신 후 물기 공격 기능 추가
            MonsterBrain brain = root.AddComponent<MonsterBrain>(); // 변신 완료 뒤 활성화할 공통 AI 추가
            ChestMimicBehavior mimic = root.AddComponent<ChestMimicBehavior>(); // 상자 위장·변신 제어 기능 추가
            GameObject visualObject = new GameObject("ChestMimicVisual"); // 미믹 전체 시각 루트 생성
            visualObject.transform.SetParent(root.transform, false); // 몬스터 루트 자식 연결
            Transform visual = visualObject.transform; // 시각 Transform 저장
            CreatePrimitivePart(visual, "Mimic_ChestBody", PrimitiveType.Cube, new Vector3(0f, 0.54f, 0f), new Vector3(1.05f, 0.72f, 0.78f), wood, Vector3.zero); // 기본 나무 상자 몸통 생성
            CreatePrimitivePart(visual, "Mimic_Band_Left", PrimitiveType.Cube, new Vector3(-0.38f, 0.55f, 0.01f), new Vector3(0.10f, 0.76f, 0.82f), rust, Vector3.zero); // 왼쪽 녹슨 금속 띠 생성
            CreatePrimitivePart(visual, "Mimic_Band_Right", PrimitiveType.Cube, new Vector3(0.38f, 0.55f, 0.01f), new Vector3(0.10f, 0.76f, 0.82f), rust, Vector3.zero); // 오른쪽 녹슨 금속 띠 생성
            CreatePrimitivePart(visual, "Mimic_Lock", PrimitiveType.Cube, new Vector3(0f, 0.50f, 0.42f), new Vector3(0.22f, 0.28f, 0.10f), rust, Vector3.zero); // 정면 잠금쇠 생성
            Transform lidRoot = new GameObject("Mimic_LidRoot").transform; // 위로 열리는 상자 뚜껑 변신 루트 생성
            lidRoot.SetParent(visual, false); // 시각 루트 자식 연결
            lidRoot.localPosition = new Vector3(0f, 0.91f, -0.34f); // 상자 뒤쪽 경첩 위치 지정
            CreatePrimitivePart(lidRoot, "Mimic_Lid", PrimitiveType.Cube, new Vector3(0f, 0f, 0.34f), new Vector3(1.08f, 0.22f, 0.80f), wood, Vector3.zero); // 두꺼운 목재 뚜껑 생성
            CreatePrimitivePart(lidRoot, "Mimic_LidBand", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.36f), new Vector3(0.13f, 0.26f, 0.84f), rust, Vector3.zero); // 뚜껑 중앙 금속 띠 생성
            GameObject hiddenParts = new GameObject("Mimic_RevealedParts"); // 변신 후 보이는 몬스터 내부 파트 루트 생성
            hiddenParts.transform.SetParent(visual, false); // 시각 루트 자식 연결
            Transform hidden = hiddenParts.transform; // 숨은 파트 Transform 저장
            CreatePrimitivePart(hidden, "Mimic_Eye_L", PrimitiveType.Sphere, new Vector3(-0.24f, 0.72f, 0.39f), new Vector3(0.13f, 0.13f, 0.08f), eyeMaterial, Vector3.zero); // 왼쪽 붉은 눈 생성
            CreatePrimitivePart(hidden, "Mimic_Eye_R", PrimitiveType.Sphere, new Vector3(0.24f, 0.72f, 0.39f), new Vector3(0.13f, 0.13f, 0.08f), eyeMaterial, Vector3.zero); // 오른쪽 붉은 눈 생성
            Transform jawRoot = new GameObject("Mimic_JawAttackRoot").transform; // 물기 공격 모션 시각 루트 생성
            jawRoot.SetParent(hidden, false); // 숨은 파트 루트 자식 연결
            jawRoot.localPosition = new Vector3(0f, 0.69f, 0.42f); // 상자 입 정면 위치 지정

            for (int toothIndex = 0; toothIndex < 7; toothIndex++) // 상하 이빨을 여러 개 생성
            {
                float x = -0.39f + (toothIndex * 0.13f); // 좌우 이빨 위치 계산
                CreatePrimitivePart(jawRoot, $"Mimic_Tooth_Upper_{toothIndex + 1}", PrimitiveType.Cube, new Vector3(x, 0.22f, 0f), new Vector3(0.075f, 0.18f, 0.10f), bone, new Vector3(18f, 0f, 0f)); // 윗줄 날카로운 이빨 생성
                CreatePrimitivePart(jawRoot, $"Mimic_Tooth_Lower_{toothIndex + 1}", PrimitiveType.Cube, new Vector3(x, -0.20f, 0f), new Vector3(0.075f, 0.18f, 0.10f), bone, new Vector3(-18f, 0f, 0f)); // 아랫줄 날카로운 이빨 생성
            }

            CreatePrimitivePart(jawRoot, "Mimic_Tongue", PrimitiveType.Capsule, new Vector3(0f, -0.08f, 0.22f), new Vector3(0.20f, 0.18f, 0.38f), tongue, new Vector3(70f, 0f, 0f)); // 길게 튀어나온 붉은 혀 생성
            CreatePrimitivePart(hidden, "Mimic_Leg_L", PrimitiveType.Capsule, new Vector3(-0.34f, 0.17f, -0.08f), new Vector3(0.16f, 0.25f, 0.16f), tongue, new Vector3(0f, 0f, 15f)); // 변신 후 왼쪽 살점 다리 생성
            CreatePrimitivePart(hidden, "Mimic_Leg_R", PrimitiveType.Capsule, new Vector3(0.34f, 0.17f, -0.08f), new Vector3(0.16f, 0.25f, 0.16f), tongue, new Vector3(0f, 0f, -15f)); // 변신 후 오른쪽 살점 다리 생성
            Transform eyePoint = new GameObject("EyePoint").transform; // 변신 후 공통 시각 감지 기준점 생성
            eyePoint.SetParent(root.transform, false); // 몬스터 루트 자식 연결
            eyePoint.localPosition = new Vector3(0f, 0.78f, 0.42f); // 상자 눈 높이 정면 위치 지정
            Transform attackOrigin = new GameObject("MimicAttackOrigin").transform; // 물기 공격 시작 위치 생성
            attackOrigin.SetParent(root.transform, false); // 미믹 루트 자식 연결
            attackOrigin.localPosition = new Vector3(0f, 0.68f, 0.52f); // 상자 입 앞쪽 위치 지정
            sensor.Configure(data, eyePoint, root); // 변신 후 시각·청각 감각 연결
            motor.Configure(data, reaction); // 변신 후 이동 데이터 연결
            meleeAttack.Configure(data, sensor, attackOrigin, jawRoot, new Vector3(-18f, 0f, 0f), new Vector3(30f, 0f, 0f)); // 턱을 뒤로 당겼다가 앞으로 물어뜯는 단발 모션 구성
            brain.Configure(data, health, reaction, sensor, selector, motor, null, meleeAttack); // 변신 후 사용할 공통 추적·근접 AI 연결
            mimic.Configure(data, health, brain, motor, meleeAttack, lidRoot, hiddenParts, 3.2f, 0.72f); // 접근·피격 변신 규칙과 뚜껑 모션 구성
            root.SetActive(false); // SpawnPoint 런타임 복제용 프로토타입 비활성화
            EditorUtility.SetDirty(health); // 체력 설정 저장 대상으로 표시
            EditorUtility.SetDirty(reaction); // 반응 설정 저장 대상으로 표시
            EditorUtility.SetDirty(sensor); // 감각 설정 저장 대상으로 표시
            EditorUtility.SetDirty(motor); // 이동 설정 저장 대상으로 표시
            EditorUtility.SetDirty(meleeAttack); // 근접 공격 설정 저장 대상으로 표시
            EditorUtility.SetDirty(brain); // 공통 AI 설정 저장 대상으로 표시
            EditorUtility.SetDirty(mimic); // 위장·변신 규칙 저장 대상으로 표시
            return root; // 완성된 상자 미믹 프로토타입 반환
        }

        private static GameObject CreatePrimitivePart(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material, Vector3 localEuler) // Collider 없는 프리미티브 시각 파트 생성
        {
            GameObject part = GameObject.CreatePrimitive(type); // 지정 프리미티브 생성
            part.name = name; // 시각 파트 이름 지정
            part.transform.SetParent(parent, false); // 요청 부모 아래 로컬 배치
            part.transform.localPosition = localPosition; // 로컬 위치 적용
            part.transform.localEulerAngles = localEuler; // 로컬 회전 적용
            part.transform.localScale = localScale; // 로컬 크기 적용
            Collider collider = part.GetComponent<Collider>(); // 자동 생성 Collider 조회

            if (collider != null) // 시각 파트 Collider 존재 여부 확인
            {
                Object.DestroyImmediate(collider); // 루트 CharacterController·화살 Collider만 사용하도록 시각 Collider 제거
            }

            Renderer renderer = part.GetComponent<Renderer>(); // 프리미티브 Renderer 조회

            if (renderer != null && material != null) // 재질 적용 가능 여부 확인
            {
                renderer.sharedMaterial = material; // 요청 재질 적용
            }

            return part; // 완성된 시각 파트 반환
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 worldPosition, Vector3 worldScale, Material material, bool colliderEnabled, bool localPosition = false) // 소환선·SpawnPad 박스 생성
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube); // 박스 프리미티브 생성
            box.name = name; // 오브젝트 이름 지정
            box.transform.SetParent(parent, false); // 요청 부모 아래 배치

            if (localPosition) // SpawnPoint 자식 로컬 배치 여부 확인
            {
                box.transform.localPosition = worldPosition; // 요청 로컬 위치 적용
            }
            else // Day17 루트 기준 월드 배치 처리
            {
                box.transform.position = worldPosition; // 요청 월드 위치 적용
            }

            box.transform.localScale = worldScale; // 박스 크기 적용
            Renderer renderer = box.GetComponent<Renderer>(); // 박스 Renderer 조회

            if (renderer != null && material != null) // 재질 적용 가능 여부 확인
            {
                renderer.sharedMaterial = material; // 요청 재질 적용
            }

            Collider collider = box.GetComponent<Collider>(); // 박스 Collider 조회

            if (collider != null) // Collider 존재 여부 확인
            {
                collider.enabled = colliderEnabled; // 요청된 충돌 활성 상태 적용
            }

            return box; // 완성된 박스 반환
        }

        private static Material GetOrCreateMaterial(string materialName, Color baseColor, float metallic, float smoothness, Color? emission = null) // Day17 URP 재질 생성·갱신
        {
            string path = $"{MaterialFolder}/{materialName}.mat"; // 재질 에셋 경로 계산
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 재질 조회
            Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // URP Lit 셰이더 조회

            if (shader == null) // URP Lit 셰이더 조회 실패 여부 확인
            {
                shader = Shader.Find("Standard"); // 편집 환경 대체 Standard 셰이더 사용
            }

            if (material == null) // 재질 최초 생성 여부 확인
            {
                material = new Material(shader); // 선택 셰이더 기반 새 재질 생성
                material.name = materialName; // 재질 이름 지정
                AssetDatabase.CreateAsset(material, path); // Day17 재질 폴더에 에셋 저장
            }

            if (material.HasProperty("_BaseColor")) // URP 기본색 속성 존재 여부 확인
            {
                material.SetColor("_BaseColor", baseColor); // URP 기본색 적용
            }

            if (material.HasProperty("_Color")) // Standard 호환 기본색 속성 존재 여부 확인
            {
                material.SetColor("_Color", baseColor); // 호환 기본색 적용
            }

            if (material.HasProperty("_Metallic")) // 금속도 속성 존재 여부 확인
            {
                material.SetFloat("_Metallic", Mathf.Clamp01(metallic)); // 금속도 적용
            }

            if (material.HasProperty("_Smoothness")) // 표면 매끄러움 속성 존재 여부 확인
            {
                material.SetFloat("_Smoothness", Mathf.Clamp01(smoothness)); // 매끄러움 적용
            }

            if (emission.HasValue && material.HasProperty("_EmissionColor")) // 발광색 요청과 속성 존재 여부 확인
            {
                material.EnableKeyword("_EMISSION"); // 발광 키워드 활성화
                material.SetColor("_EmissionColor", emission.Value); // 요청 발광색 적용
            }

            EditorUtility.SetDirty(material); // 변경 재질 저장 대상으로 표시
            return material; // 완성된 재질 반환
        }

        private static void EnsureAssetFolder(string folderPath) // 중첩 에셋 폴더 존재 보장
        {
            string[] parts = folderPath.Split('/'); // 전체 폴더 경로 조각 분리
            string current = parts[0]; // Assets 루트부터 시작

            for (int index = 1; index < parts.Length; index++) // 하위 폴더 경로 순회
            {
                string next = $"{current}/{parts[index]}"; // 다음 누적 폴더 경로 계산

                if (!AssetDatabase.IsValidFolder(next)) // 현재 하위 폴더가 존재하지 않는지 확인
                {
                    AssetDatabase.CreateFolder(current, parts[index]); // 누락 하위 폴더 생성
                }

                current = next; // 다음 단계 부모 경로 갱신
            }
        }

        private static void RemoveExistingRoot(Scene scene, string rootName) // 지정 이름의 기존 자동 생성 씬 루트 제거
        {
            GameObject existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == rootName); // 같은 이름의 씬 루트 검색

            if (existing != null) // 제거 대상 존재 여부 확인
            {
                Object.DestroyImmediate(existing); // 기존 자동 생성 루트 즉시 제거
            }
        }
    }
}
