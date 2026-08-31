using System.Collections.Generic; // 검증 실패 항목 목록 기능 참조
using System.IO; // 대상 씬 존재 여부 확인 기능 참조
using System.Linq; // SpawnPoint·컴포넌트 검색 기능 참조
using ProjectI.Combat; // 진영·체력·경직 규칙 검증 참조
using ProjectI.Diagnostics; // F1 Monster AI 페이지 검증 참조
using ProjectI.Monsters; // Day17 Monster AI·4종 몬스터·소환 기능 검증 참조
using UnityEditor; // 유니티 에디터 메뉴·에셋 조회 기능 참조
using UnityEditor.SceneManagement; // 씬 열기 기능 참조
using UnityEngine; // 유니티 오브젝트 검색 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 자동 검증 도구 네임스페이스
{
    public static class Phase4Day17Validator // Day17 공통 Monster AI·4종 테스트 몬스터 정적 검증
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string Day17RootName = "===Day17 Monster AI==="; // Day17 시험장 루트 이름
        private const string ReadyMarkerName = "===Day17 Monster AI Ready v4==="; // 웃는 석상 불사·화면 관찰 규칙 보정 버전 완료 마커 이름
        private const string UndeadDataPath = "Assets/ProjectI/Resources/Monsters/Day17_CorruptedUndead.asset"; // 기본 부패한 망자 데이터 경로
        private const string ArcherDataPath = "Assets/ProjectI/Resources/Monsters/Day17_CorruptedUndeadArcher.asset"; // 궁수 데이터 경로
        private const string StatueDataPath = "Assets/ProjectI/Resources/Monsters/Day17_SmilingStatue.asset"; // 웃는 석상 데이터 경로
        private const string MimicDataPath = "Assets/ProjectI/Resources/Monsters/Day17_ChestMimic.asset"; // 상자 미믹 데이터 경로
        private static readonly Vector3 ExpectedSpawnCenter = new Vector3(-27f, 0f, 25.8f); // 첨부 이미지 기준 예상 4종 소환선 중심

        [MenuItem("Tools/Project I/Day 17/Validate")] // 수동 Day17 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 기반 전체 검증 실행
        {
            Validate(true); // 결과 대화상자를 포함한 검증 실행
        }

        public static bool Validate(bool showDialog) // Day17 공통 AI·4종 몬스터 구조 검증 실행
        {
            List<string> failures = new List<string>(); // 검증 실패 목록 생성

            if (!File.Exists(ExplorationOfficeScenePath)) // 탐사 씬 파일 존재 여부 확인
            {
                failures.Add("ExplorationOffice.unity 누락"); // 씬 누락 실패 기록
                return FinishValidation(failures, showDialog); // 즉시 결과 반환
            }

            Scene scene = SceneManager.GetActiveScene(); // 현재 활성 씬 조회

            if (scene.path != ExplorationOfficeScenePath) // 현재 씬이 탐사 사무소인지 확인
            {
                scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 검증 대상 탐사 씬 열기
            }

            GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == Day17RootName); // Day17 시험장 루트 조회
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(item => item.name == ReadyMarkerName); // Day17 v4 완료 마커 조회
            MonsterData undeadData = AssetDatabase.LoadAssetAtPath<MonsterData>(UndeadDataPath); // 기본 부패한 망자 데이터 조회
            MonsterData archerData = AssetDatabase.LoadAssetAtPath<MonsterData>(ArcherDataPath); // 부패한 망자 궁수 데이터 조회
            MonsterData statueData = AssetDatabase.LoadAssetAtPath<MonsterData>(StatueDataPath); // 웃는 석상 데이터 조회
            MonsterData mimicData = AssetDatabase.LoadAssetAtPath<MonsterData>(MimicDataPath); // 상자 미믹 데이터 조회
            MonsterSpawnPoint[] spawnPoints = root == null ? new MonsterSpawnPoint[0] : root.GetComponentsInChildren<MonsterSpawnPoint>(true); // Day17 4종 나란히 소환 지점 전체 조회
            GameObject undeadPrototype = root == null ? null : FindChildRecursive(root.transform, "Day17_CorruptedUndead_Prototype"); // 기본 부패한 망자 프로토타입 조회
            GameObject archerPrototype = root == null ? null : FindChildRecursive(root.transform, "Day17_CorruptedUndeadArcher_Prototype"); // 궁수 프로토타입 조회
            GameObject statuePrototype = root == null ? null : FindChildRecursive(root.transform, "Day17_SmilingStatue_Prototype"); // 웃는 석상 프로토타입 조회
            GameObject mimicPrototype = root == null ? null : FindChildRecursive(root.transform, "Day17_ChestMimic_Prototype"); // 상자 미믹 프로토타입 조회
            GameObject arrowTemplateObject = root == null ? null : FindChildRecursive(root.transform, "Day17_UndeadArrowTemplate"); // 비활성 적 화살 템플릿 조회
            MonsterArrowProjectile arrowTemplate = arrowTemplateObject == null ? null : arrowTemplateObject.GetComponent<MonsterArrowProjectile>(); // 적 포물선 화살 기능 조회
            MonsterAIDebugPage debugPage = root == null ? null : root.GetComponent<MonsterAIDebugPage>(); // F1 Monster AI 페이지 조회
            PlayerNoiseEmitter playerNoise = Object.FindFirstObjectByType<PlayerNoiseEmitter>(); // 플레이어 걷기·달리기 소음 발생기 조회
            Require(root != null, "Day17 Monster AI 루트 누락", failures); // 시험장 루트 존재 검증
            Require(marker != null, "Day17 v4 완료 마커 누락", failures); // 웃는 석상 불사 규칙 버전 완료 마커 존재 검증
            Require(debugPage != null, "F1 MonsterAIDebugPage 누락", failures); // F1 AI 진단 페이지 존재 검증
            Require(playerNoise != null, "PlayerNoiseEmitter 누락", failures); // 이동 청각 테스트 기능 검증
            Require(undeadData != null && undeadData.Archetype == MonsterArchetype.CorruptedUndead, "기본 부패한 망자 MonsterData 누락 또는 유형 오류", failures); // 기본 망자 데이터 검증
            Require(archerData != null && archerData.Archetype == MonsterArchetype.CorruptedUndeadArcher, "부패한 망자 궁수 MonsterData 누락 또는 유형 오류", failures); // 궁수 데이터 검증
            Require(statueData != null && statueData.Archetype == MonsterArchetype.SmilingStatue, "웃는 석상 MonsterData 누락 또는 유형 오류", failures); // 석상 데이터 검증
            Require(mimicData != null && mimicData.Archetype == MonsterArchetype.ChestMimic, "상자 미믹 MonsterData 누락 또는 유형 오류", failures); // 미믹 데이터 검증
            Require(spawnPoints.Length == 4, "첨부 위치의 나란한 Monster SpawnPoint가 정확히 4개가 아님", failures); // 4종 한 마리씩 소환 구조 검증
            ValidateSpawnLine(spawnPoints, failures); // 첨부 이미지 위치·가로 정렬·서로 다른 프로토타입 검증
            ValidateUndeadPrototype(undeadPrototype, failures); // 근접 부패한 망자 구성 검증
            ValidateArcherPrototype(archerPrototype, archerData, arrowTemplateObject, arrowTemplate, root, failures); // 궁수 구성 검증
            ValidateStatuePrototype(statuePrototype, failures); // 웃는 석상 관찰 규칙 구성 검증
            ValidateMimicPrototype(mimicPrototype, failures); // 상자 미믹 위장·변신 구성 검증
            Require(CombatFactionRules.CanDamage(CombatFaction.Enemy, CombatFaction.Player), "Enemy → Player Damage Pipeline 진영 규칙이 허용되지 않음", failures); // 모든 몬스터의 플레이어 피해 규칙 검증
            return FinishValidation(failures, showDialog); // 최종 검증 결과 반환
        }

        private static void ValidateUndeadPrototype(GameObject prototype, List<string> failures) // 기본 부패한 망자 추적·근접 공격 구조 검증
        {
            Require(prototype != null && !prototype.activeSelf, "기본 부패한 망자 비활성 프로토타입 누락 또는 활성 상태", failures); // Spawn용 비활성 원본 검증

            if (prototype == null) // 프로토타입 누락 여부 확인
            {
                return; // 세부 검증 중단
            }

            MonsterBrain brain = prototype.GetComponent<MonsterBrain>(); // 공통 AI Brain 조회
            MonsterMeleeAttack attack = prototype.GetComponent<MonsterMeleeAttack>(); // 공통 근접 공격 조회
            CombatHealth health = prototype.GetComponent<CombatHealth>(); // 절반 체력 적용 여부 확인용 공통 체력 조회
            Require(brain != null && brain.MeleeAttack != null, "기본 부패한 망자 MonsterBrain·MeleeAttack 연결 누락", failures); // 추적·근접 공격 연결 검증
            Require(health != null && Mathf.Abs(health.MaxHealth - 70f) < 0.1f, "기본 부패한 망자 체력이 70으로 적용되지 않음", failures); // 절반 수준 체력 검증
            Require(attack != null && attack.Damage > 0f, "기본 부패한 망자 근접 피해 수치 오류", failures); // 실제 근접 피해 수치 검증
            Require(prototype.GetComponent<MonsterSensor>() != null && prototype.GetComponent<MonsterMotor>() != null, "기본 부패한 망자 시각·청각 또는 이동 계층 누락", failures); // 감지·이동 공통 기능 검증
            Require(FindChildRecursive(prototype.transform, "Melee_Claw_R") != null, "기본 부패한 망자 공격 발톱 모델 누락", failures); // 근접 외형 검증
        }

        private static void ValidateArcherPrototype(GameObject prototype, MonsterData data, GameObject arrowTemplateObject, MonsterArrowProjectile arrowTemplate, GameObject root, List<string> failures) // 부패한 망자 궁수 원거리 공격 구조 검증
        {
            Require(prototype != null && !prototype.activeSelf, "부패한 망자 궁수 비활성 프로토타입 누락 또는 활성 상태", failures); // Spawn용 비활성 원본 검증

            if (prototype != null) // 궁수 프로토타입 존재 여부 확인
            {
                MonsterBrain brain = prototype.GetComponent<MonsterBrain>(); // 공통 AI Brain 조회
                CombatHealth health = prototype.GetComponent<CombatHealth>(); // 공통 체력 조회
                CorruptedUndeadArcherAttack attack = prototype.GetComponent<CorruptedUndeadArcherAttack>(); // 활 공격 기능 조회
                Require(brain != null && brain.RangedAttack != null, "궁수 MonsterBrain·RangedAttack 연결 누락", failures); // 공통 AI와 활 공격 연결 검증
                Require(health != null && health.Faction == CombatFaction.Enemy && Mathf.Abs(health.MaxHealth - 55f) < 0.1f, "궁수 CombatHealth Enemy 진영·55 체력 구성 오류", failures); // 절반으로 낮춘 궁수 체력 검증
                Require(attack != null && attack.ProjectileSpeed >= 20f && attack.Damage > 0f, "궁수 원거리 공격 수치·참조 오류", failures); // 활 공격 기본 수치 검증
            }

            Require(arrowTemplate != null && arrowTemplateObject != null && arrowTemplateObject.GetComponent<Rigidbody>() != null && arrowTemplateObject.GetComponent<Rigidbody>().useGravity, "중력 포물선 적 화살 템플릿 누락", failures); // Rigidbody 중력 화살 검증
            Require(root == null || FindChildRecursive(root.transform, "Bow_Limb_Left") != null, "부패한 망자 활 왼쪽 활대 모델 누락", failures); // 활 상세 모델 검증
            Require(root == null || FindChildRecursive(root.transform, "Bow_StringRoot") != null, "부패한 망자 활 시위 모델 누락", failures); // 시위 당김 구조 검증
            Require(data == null || (data.AimTime >= 0.5f && data.AttackCooldown >= 1f && data.ProjectileSpeed >= 20f), "망자 궁수 조준·쿨타임·화살 속도 수치 오류", failures); // 원거리 전투 템포 검증
        }

        private static void ValidateStatuePrototype(GameObject prototype, List<string> failures) // 웃는 석상 관찰 시 정지 규칙 구조 검증
        {
            Require(prototype != null && !prototype.activeSelf, "웃는 석상 비활성 프로토타입 누락 또는 활성 상태", failures); // Spawn용 비활성 원본 검증

            if (prototype == null) // 프로토타입 누락 여부 확인
            {
                return; // 세부 검증 중단
            }

            SmilingStatueBehavior behavior = prototype.GetComponent<SmilingStatueBehavior>(); // 화면 관찰 기반 특수 행동 조회
            MonsterMeleeAttack attack = prototype.GetComponent<MonsterMeleeAttack>(); // 화면 밖 근접 공격 조회
            CombatHealth health = prototype.GetComponent<CombatHealth>(); // 석상에 체력이 잘못 추가됐는지 확인
            CombatReaction reaction = prototype.GetComponent<CombatReaction>(); // 석상에 피격 반응이 잘못 추가됐는지 확인
            bool hasDamageable = prototype.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component is IDamageable); // 석상 계층에 피해 수신 인터페이스가 존재하는지 확인
            Require(behavior != null && behavior.Data != null && behavior.Data.Archetype == MonsterArchetype.SmilingStatue, "웃는 석상 화면 관찰 규칙 행동 누락", failures); // 화면 진입 정지 규칙 기능 검증
            Require(behavior != null && behavior.IsInvulnerable, "웃는 석상이 불사·비피격 상태가 아님", failures); // 런타임 체력 컴포넌트 제거 상태 검증
            Require(behavior == null || behavior.Data == null || behavior.Data.MaxHealth <= 0.01f, "웃는 석상 MonsterData에 체력이 남아 있음", failures); // 데이터 단계에서도 체력 없음 검증
            Require(health == null && reaction == null && !hasDamageable, "웃는 석상에 CombatHealth·CombatReaction·IDamageable이 존재함", failures); // 모든 플레이어 공격·경직·넉백 차단 검증
            Require(attack != null && attack.Damage >= 30f, "웃는 석상 화면 밖 근접 공격 누락 또는 피해량 오류", failures); // 시야 밖 공격 기능 검증
            Require(FindChildRecursive(prototype.transform, "Statue_SmileRoot") != null && FindChildRecursive(prototype.transform, "Statue_FacePoint") != null, "웃는 석상 미소·관찰 얼굴 포인트 누락", failures); // 관찰 외형·Raycast 기준점 검증
        }

        private static void ValidateMimicPrototype(GameObject prototype, List<string> failures) // 상자 미믹 위장·변신·근접 공격 구조 검증
        {
            Require(prototype != null && !prototype.activeSelf, "상자 미믹 비활성 프로토타입 누락 또는 활성 상태", failures); // Spawn용 비활성 원본 검증

            if (prototype == null) // 프로토타입 누락 여부 확인
            {
                return; // 세부 검증 중단
            }

            ChestMimicBehavior mimic = prototype.GetComponent<ChestMimicBehavior>(); // 위장·변신 행동 조회
            MonsterBrain brain = prototype.GetComponent<MonsterBrain>(); // 변신 뒤 공통 추적 AI 조회
            MonsterMeleeAttack attack = prototype.GetComponent<MonsterMeleeAttack>(); // 물기 공격 기능 조회
            CombatHealth health = prototype.GetComponent<CombatHealth>(); // 절반 체력 적용 여부 확인용 미믹 체력 조회
            Require(mimic != null && mimic.Data != null && mimic.Data.Archetype == MonsterArchetype.ChestMimic, "상자 미믹 위장·변신 행동 누락", failures); // 미믹 특수 행동 검증
            Require(health != null && Mathf.Abs(health.MaxHealth - 80f) < 0.1f, "상자 미믹 체력이 80으로 적용되지 않음", failures); // 절반 수준 체력 검증
            Require(brain != null && attack != null, "상자 미믹 변신 후 추적·근접 공격 기능 누락", failures); // 변신 후 전투 기능 검증
            Require(FindChildRecursive(prototype.transform, "Mimic_LidRoot") != null && FindChildRecursive(prototype.transform, "Mimic_RevealedParts") != null, "상자 미믹 뚜껑·변신 내부 파트 누락", failures); // 변신 시각 구조 검증
            Require(FindChildRecursive(prototype.transform, "Mimic_Tongue") != null && FindChildRecursive(prototype.transform, "Mimic_Tooth_Upper_1") != null, "상자 미믹 혀·이빨 모델 누락", failures); // 미믹 상세 모델 검증
        }

        private static void ValidateSpawnLine(MonsterSpawnPoint[] spawnPoints, List<string> failures) // 첨부 이미지 기준 4종 SpawnPoint 위치·정렬·프로토타입 검증
        {
            if (spawnPoints == null || spawnPoints.Length != 4) // 4개 SpawnPoint 조건 충족 여부 확인
            {
                return; // 개수 실패는 상위 검증에서 기록하고 위치 검증 생략
            }

            MonsterSpawnPoint[] ordered = spawnPoints.OrderBy(point => point.transform.position.x).ToArray(); // 왼쪽부터 오른쪽 순서로 정렬
            float zMin = ordered.Min(point => point.transform.position.z); // 네 지점 최소 Z 위치 계산
            float zMax = ordered.Max(point => point.transform.position.z); // 네 지점 최대 Z 위치 계산
            float centerX = ordered.Average(point => point.transform.position.x); // 네 지점 평균 X 중심 계산
            float centerZ = ordered.Average(point => point.transform.position.z); // 네 지점 평균 Z 중심 계산
            Require(zMax - zMin < 0.05f, "4종 Monster SpawnPoint가 같은 Z축에 나란히 정렬되지 않음", failures); // 가로 일렬 배치 검증
            Require(Mathf.Abs(centerX - ExpectedSpawnCenter.x) < 0.15f && Mathf.Abs(centerZ - ExpectedSpawnCenter.z) < 0.15f, "Monster SpawnLine이 첨부 이미지의 SprintLane 북쪽 위치와 다름", failures); // 요청 위치 중심 검증
            Require(ordered.All(point => point.SpawnOnStart && point.Prototype != null), "하나 이상의 Monster SpawnPoint가 Play 시작 자동 소환 또는 프로토타입 연결 누락", failures); // 런타임 자동 소환 연결 검증
            Require(ordered.Select(point => point.Prototype).Distinct().Count() == 4, "4개 SpawnPoint가 서로 다른 4종 프로토타입을 사용하지 않음", failures); // 각 SpawnPoint 한 종류씩 연결 검증
            Require(ordered.Zip(ordered.Skip(1), (left, right) => Vector3.Distance(left.transform.position, right.transform.position)).All(distance => distance > 1.8f), "4종 SpawnPoint 간격이 너무 좁음", failures); // 초기 몬스터 겹침 방지 검증
        }

        private static GameObject FindChildRecursive(Transform root, string childName) // 지정 루트 아래 이름 기반 자식 검색
        {
            if (root == null) // 검색 루트 누락 확인
            {
                return null; // 대상 없음 반환
            }

            Transform match = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child != null && child.name == childName); // 비활성 프로토타입 포함 전체 자식에서 이름 검색
            return match == null ? null : match.gameObject; // 검색된 GameObject 반환
        }

        private static void Require(bool condition, string failureMessage, List<string> failures) // 단일 검증 조건 처리
        {
            if (!condition) // 검증 조건 실패 여부 확인
            {
                failures.Add(failureMessage); // 실패 목록에 원인 추가
            }
        }

        private static bool FinishValidation(List<string> failures, bool showDialog) // 검증 결과 로그·대화상자 출력
        {
            bool passed = failures.Count == 0; // 전체 검증 통과 여부 계산

            if (passed) // 모든 검증 조건 정상 여부 확인
            {
                Debug.Log("[Project I][Day17] 4종 몬스터와 불사·화면 관찰형 웃는 석상 규칙이 정적으로 정상입니다."); // 성공 로그 출력
            }
            else // 하나 이상의 검증 실패 처리
            {
                Debug.LogError($"[Project I][Day17] 검증 실패\n- {string.Join("\n- ", failures)}"); // 실패 항목 전체 Console 출력
            }

            if (showDialog) // 수동 검증 결과 대화상자 표시 여부 확인
            {
                string message = passed ? "Day17 4종 몬스터 정적 검증을 통과했습니다." : $"Day17 검증 실패\n\n- {string.Join("\n- ", failures)}"; // 대화상자 문구 생성
                EditorUtility.DisplayDialog("Project I", message, "확인"); // 검증 결과 대화상자 출력
            }

            return passed; // 최종 검증 결과 반환
        }
    }
}
