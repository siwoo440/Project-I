using System.Collections.Generic; // 사망 시 비활성화할 기존 기능 목록 구성 참조
using System.IO; // 대상 씬과 프리팹 파일 존재 여부 확인 참조
using System.Linq; // 씬 루트와 플레이어 검색 기능 참조
using ProjectI.Expedition; // Day22 원정 결과와 테스트 상호작용 기능 참조
using ProjectI.Player; // 기존 PlayerHealth와 새 사망 컨트롤러 기능 참조
using ProjectI.Wagon; // Day21 마차 공통 CargoArea 확장 기능 참조
using UnityEditor; // 에디터 메뉴·프리팹·재질 생성 기능 참조
using UnityEditor.SceneManagement; // ExplorationOffice 씬 열기·저장 기능 참조
using UnityEngine; // Primitive 래그돌·물리·Transform 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 자동 구성 도구 네임스페이스
{
    [InitializeOnLoad] // 스크립트 컴파일 완료 후 Day22 자동 패치 등록
    public static class Phase5Day22Setup // 기존 Player를 래그돌 시체로 전환하고 마차 회수·원정 손실 구조를 구성
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // Day22 테스트 대상 씬 경로
        private const string WagonPrefabPath = "Assets/ProjectI/Prefabs/Wagon/Wagon.prefab"; // Day21 공통 마차 프리팹 경로
        private const string LegacyCorpseAreaScriptPath = "Assets/ProjectI/Scripts/Wagon/WagonCorpseArea.cs"; // 이전 분리 영역 코드 자동 삭제 경로
        private const string MaterialFolder = "Assets/ProjectI/Art/Generated/Day22"; // Day22 테스트·래그돌 생성 재질 폴더
        private const string PlayerRootName = "Player"; // 기존 플레이어 루트 이름
        private const string RagdollRootName = "DeathRagdoll"; // Player 내부 사망 전용 래그돌 루트 이름
        private const string SystemRootName = "===Day22 Death Expedition System==="; // Day22 테스트 시스템 루트 이름
        private const string ReadyMarkerName = "===Day22 Death Expedition Ready v1==="; // Day22 적용 완료 마커 이름

        static Phase5Day22Setup() // 자동 적용 예약
        {
            EditorApplication.delayCall += TryAutoApply; // 컴파일 완료 다음 에디터 틱에 적용
        }

        [MenuItem("Tools/Project I/Day 22/Apply Ragdoll Death + Expedition Loss")] // Day22 전체 수동 재적용 메뉴
        public static void ApplyFromMenu() // 수동 전체 적용 실행
        {
            ApplyDay22(true, true); // 래그돌·마차·테스트 구역을 강제 재구성
        }

        [MenuItem("Tools/Project I/Day 22/Rebuild Player Death Ragdoll")] // Player 래그돌만 다시 생성하는 수동 메뉴
        public static void RebuildRagdollFromMenu() // 현재 ExplorationOffice Player 래그돌 재생성
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 씬 존재 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", "ExplorationOffice 씬을 찾을 수 없습니다.", "확인"); // 씬 누락 안내
                return; // 수동 재생성 중단
            }

            EnsureFolder(MaterialFolder); // 래그돌 재질 폴더 확보
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 대상 씬 단독 열기
            GameObject player = FindRoot(scene, PlayerRootName); // Player 루트 조회

            if (player == null) // Player 존재 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", "Player 루트를 찾을 수 없습니다.", "확인"); // Player 누락 안내
                return; // 재생성 중단
            }

            EnsurePlayerDeathRig(player, true); // 기존 래그돌을 제거하고 새 구조로 재생성
            EditorSceneManager.MarkSceneDirty(scene); // Player 구성 변경 상태 표시
            EditorSceneManager.SaveScene(scene); // 래그돌 구성 씬 저장
            AssetDatabase.SaveAssets(); // 생성 재질 저장
            AssetDatabase.Refresh(); // 에셋 데이터 갱신
            EditorUtility.DisplayDialog("Project I", "Player DeathRagdoll 재생성이 완료되었습니다.", "확인"); // 수동 재생성 완료 안내
        }

        private static void TryAutoApply() // Unity 로드 후 자동 Day22 적용 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // 자동 적용 제외 상태 확인
            {
                return; // Batch 또는 Play 전환 중에는 중단
            }

            ApplyDay22(false, false); // 기존 사용자 배치를 보존하는 보정형 자동 적용
        }

        private static void ApplyDay22(bool showDialog, bool force) // Day22 Player·Wagon·원정 결과 전체 구성
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 씬 파일 존재 여부 확인
            {
                Debug.LogError("[Project I] Day22 대상 ExplorationOffice 씬을 찾을 수 없습니다."); // 씬 누락 오류 출력
                return; // 전체 구성 중단
            }

            if (!File.Exists(WagonPrefabPath)) // Day21 공통 Wagon.prefab 존재 여부 확인
            {
                Debug.LogError("[Project I] Day22는 Day21 Wagon.prefab이 필요합니다."); // 선행 마차 프리팹 누락 오류 출력
                return; // 마차 회수 영역 구성 중단
            }

            EnsureFolder(MaterialFolder); // Day22 생성 재질 폴더 확보
            PatchSharedWagonPrefab(); // 모든 맵이 공유하는 Wagon.prefab의 창고 전체를 하나의 CargoArea로 통합
            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 테스트 씬 단독 열기
            GameObject player = FindRoot(scene, PlayerRootName); // 기존 Player 루트 검색

            if (player == null) // 기존 플레이어 존재 여부 확인
            {
                Debug.LogError("[Project I] Day22 적용 중 Player 루트를 찾을 수 없습니다."); // Player 누락 오류 출력
                return; // Player 사망 시스템 구성 중단
            }

            EnsurePlayerDeathRig(player, force); // 기존 Player 내부에 사망 래그돌과 전환 컨트롤러 구성
            GameObject systemRoot = FindRoot(scene, SystemRootName); // 기존 Day22 테스트 루트 조회

            if (systemRoot == null) // Day22 테스트 루트 미생성 확인
            {
                systemRoot = new GameObject(SystemRootName); // 원정 결과와 테스트 오브젝트용 루트 생성
            }

            ExpeditionOutcomeController outcomeController = systemRoot.GetComponent<ExpeditionOutcomeController>(); // 기존 원정 결과 컨트롤러 조회

            if (outcomeController == null) // 결과 컨트롤러 누락 확인
            {
                outcomeController = systemRoot.AddComponent<ExpeditionOutcomeController>(); // Day22 귀환·손실 판정 컨트롤러 추가
            }

            EnsureTestInteractables(systemRoot.transform, outcomeController, force); // 사망·귀환 검증용 F 상호작용 오브젝트 구성
            GameObject readyMarker = FindRoot(scene, ReadyMarkerName); // 기존 완료 마커 조회

            if (readyMarker == null) // 완료 마커 미생성 확인
            {
                readyMarker = new GameObject(ReadyMarkerName); // Day22 적용 완료 마커 생성
            }

            EditorUtility.SetDirty(player); // Player 사망 구성 저장 대상으로 표시
            EditorUtility.SetDirty(systemRoot); // Day22 테스트 루트 저장 대상으로 표시
            EditorUtility.SetDirty(readyMarker); // 완료 마커 저장 대상으로 표시
            EditorSceneManager.MarkSceneDirty(scene); // 씬 전체 변경 상태 표시
            EditorSceneManager.SaveScene(scene); // Player·테스트 시스템 변경 저장
            AssetDatabase.SaveAssets(); // 프리팹·재질 변경 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 갱신
            bool success = Phase5Day22Validator.Validate(false); // Day22 전체 구조 검증 실행
            DeleteLegacyCorpseAreaScript(); // 이전 ZIP의 분리 영역 호환 스크립트를 구성 완료 후 자동 삭제

            if (showDialog) // 수동 실행 결과 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", success ? "Day22 래그돌 사망·마차 공통 회수·원정 손실 구성이 완료되었습니다." : "Day22 검증 실패 - Console을 확인하세요.", "확인"); // 최종 결과 안내
            }
        }

        private static void EnsurePlayerDeathRig(GameObject player, bool force) // 기존 Player 자체가 시체가 되는 래그돌 구조 구성
        {
            Transform existingRoot = player.transform.Find(RagdollRootName); // 기존 사망 래그돌 루트 조회

            if (existingRoot != null && force) // 강제 재생성 상태에서 기존 래그돌 존재 여부 확인
            {
                Object.DestroyImmediate(existingRoot.gameObject); // 이전 자동 생성 래그돌 제거
                existingRoot = null; // 새 구조 생성 준비
            }

            Material bodyMaterial = GetOrCreateMaterial("DeathRagdoll_Body", new Color(0.28f, 0.26f, 0.24f), 0.02f, 0.25f); // 사망 시 보이는 임시 인체 재질 확보
            GameObject ragdollRoot = existingRoot == null ? BuildRagdoll(player.transform, bodyMaterial) : existingRoot.gameObject; // 기존 구조 재사용 또는 새 Primitive 래그돌 생성
            Transform pelvis = ragdollRoot.transform.Find("Pelvis"); // 시체 중심·회수 기준 골반 조회
            PlayerDeathController deathController = player.GetComponent<PlayerDeathController>(); // 기존 Day22 사망 컨트롤러 조회

            if (deathController == null) // 사망 컨트롤러 미부착 확인
            {
                deathController = player.AddComponent<PlayerDeathController>(); // 기존 Player에 Alive→Dead 전환 기능 추가
            }

            PlayerHealth health = player.GetComponent<PlayerHealth>(); // 기존 PlayerHealth 조회
            CharacterController characterController = player.GetComponent<CharacterController>(); // 기존 Player CharacterController 조회
            Camera viewCamera = player.GetComponentInChildren<Camera>(true); // 기존 1인칭 View 카메라 조회
            ProjectI.Items.PlayerInventory inventory = player.GetComponent<ProjectI.Items.PlayerInventory>(); // 기존 빠른 슬롯 인벤토리 조회
            MonoBehaviour[] liveBehaviours = CollectLiveBehaviours(player, deathController); // 사망 순간 정지할 기존 이동·전투·상호작용 기능 수집
            deathController.Configure(health, inventory, characterController, viewCamera, ragdollRoot, pelvis, liveBehaviours); // 기존 Player 구성과 래그돌 참조 연결
            ragdollRoot.SetActive(false); // 생존 상태에서는 사망용 인체 모델을 화면에서 숨김
        }

        private static GameObject BuildRagdoll(Transform playerRoot, Material bodyMaterial) // Primitive와 CharacterJoint로 간단한 인간형 래그돌 생성
        {
            GameObject root = new GameObject(RagdollRootName); // Player 내부 사망 물리 루트 생성
            root.transform.SetParent(playerRoot, false); // 기존 Player를 시체 소유 루트로 유지
            root.transform.localPosition = Vector3.zero; // Player 원점 기준 배치
            root.transform.localRotation = Quaternion.identity; // Player 사망 방향 상속

            Rigidbody pelvis = CreateRagdollPart(root.transform, "Pelvis", PrimitiveType.Capsule, new Vector3(0f, 0.88f, 0f), new Vector3(0.42f, 0.30f, 0.34f), Quaternion.identity, 8f, bodyMaterial); // 골반 중심 물리 몸체 생성
            Rigidbody chest = CreateRagdollPart(root.transform, "Chest", PrimitiveType.Capsule, new Vector3(0f, 1.25f, 0f), new Vector3(0.48f, 0.40f, 0.36f), Quaternion.identity, 10f, bodyMaterial); // 상체 물리 몸체 생성
            Rigidbody head = CreateRagdollPart(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.70f, 0.02f), new Vector3(0.38f, 0.42f, 0.38f), Quaternion.identity, 4f, bodyMaterial); // 머리 물리 몸체 생성
            Rigidbody upperArmLeft = CreateRagdollPart(root.transform, "UpperArm_L", PrimitiveType.Capsule, new Vector3(-0.48f, 1.33f, 0f), new Vector3(0.18f, 0.34f, 0.18f), Quaternion.Euler(0f, 0f, 72f), 2.2f, bodyMaterial); // 왼쪽 상완 생성
            Rigidbody lowerArmLeft = CreateRagdollPart(root.transform, "LowerArm_L", PrimitiveType.Capsule, new Vector3(-0.77f, 1.15f, 0f), new Vector3(0.16f, 0.32f, 0.16f), Quaternion.Euler(0f, 0f, 48f), 1.7f, bodyMaterial); // 왼쪽 하완 생성
            Rigidbody upperArmRight = CreateRagdollPart(root.transform, "UpperArm_R", PrimitiveType.Capsule, new Vector3(0.48f, 1.33f, 0f), new Vector3(0.18f, 0.34f, 0.18f), Quaternion.Euler(0f, 0f, -72f), 2.2f, bodyMaterial); // 오른쪽 상완 생성
            Rigidbody lowerArmRight = CreateRagdollPart(root.transform, "LowerArm_R", PrimitiveType.Capsule, new Vector3(0.77f, 1.15f, 0f), new Vector3(0.16f, 0.32f, 0.16f), Quaternion.Euler(0f, 0f, -48f), 1.7f, bodyMaterial); // 오른쪽 하완 생성
            Rigidbody upperLegLeft = CreateRagdollPart(root.transform, "UpperLeg_L", PrimitiveType.Capsule, new Vector3(-0.20f, 0.58f, 0f), new Vector3(0.22f, 0.38f, 0.22f), Quaternion.identity, 4.5f, bodyMaterial); // 왼쪽 허벅지 생성
            Rigidbody lowerLegLeft = CreateRagdollPart(root.transform, "LowerLeg_L", PrimitiveType.Capsule, new Vector3(-0.20f, 0.23f, 0.02f), new Vector3(0.19f, 0.34f, 0.19f), Quaternion.identity, 3.5f, bodyMaterial); // 왼쪽 종아리 생성
            Rigidbody upperLegRight = CreateRagdollPart(root.transform, "UpperLeg_R", PrimitiveType.Capsule, new Vector3(0.20f, 0.58f, 0f), new Vector3(0.22f, 0.38f, 0.22f), Quaternion.identity, 4.5f, bodyMaterial); // 오른쪽 허벅지 생성
            Rigidbody lowerLegRight = CreateRagdollPart(root.transform, "LowerLeg_R", PrimitiveType.Capsule, new Vector3(0.20f, 0.23f, 0.02f), new Vector3(0.19f, 0.34f, 0.19f), Quaternion.identity, 3.5f, bodyMaterial); // 오른쪽 종아리 생성

            CreateJoint(chest, pelvis, -25f, 25f, 35f); // 허리 관절 연결
            CreateJoint(head, chest, -25f, 25f, 35f); // 목 관절 연결
            CreateJoint(upperArmLeft, chest, -55f, 55f, 70f); // 왼쪽 어깨 관절 연결
            CreateJoint(lowerArmLeft, upperArmLeft, -10f, 80f, 18f); // 왼쪽 팔꿈치 관절 연결
            CreateJoint(upperArmRight, chest, -55f, 55f, 70f); // 오른쪽 어깨 관절 연결
            CreateJoint(lowerArmRight, upperArmRight, -80f, 10f, 18f); // 오른쪽 팔꿈치 관절 연결
            CreateJoint(upperLegLeft, pelvis, -35f, 35f, 42f); // 왼쪽 고관절 연결
            CreateJoint(lowerLegLeft, upperLegLeft, -8f, 70f, 12f); // 왼쪽 무릎 관절 연결
            CreateJoint(upperLegRight, pelvis, -35f, 35f, 42f); // 오른쪽 고관절 연결
            CreateJoint(lowerLegRight, upperLegRight, -8f, 70f, 12f); // 오른쪽 무릎 관절 연결

            root.SetActive(false); // 생존 중 렌더링·물리 계산 비활성화
            return root; // Player 내부 래그돌 루트 반환
        }

        private static Rigidbody CreateRagdollPart(Transform parent, string name, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, float mass, Material material) // 래그돌 한 신체 부위 생성
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType); // Capsule 또는 Sphere 기본 메시·Collider 생성
            part.name = name; // Validator와 회수 기준에 사용할 신체 이름 지정
            part.transform.SetParent(parent, false); // DeathRagdoll 루트 아래 배치
            part.transform.localPosition = localPosition; // 서 있는 기본 인체 포즈 위치 적용
            part.transform.localRotation = localRotation; // 팔 등 신체 방향 적용
            part.transform.localScale = localScale; // 신체 부위 크기 적용
            Renderer renderer = part.GetComponent<Renderer>(); // Primitive Renderer 조회

            if (renderer != null && material != null) // 재질 적용 가능 여부 확인
            {
                renderer.sharedMaterial = material; // 사망 시 보이는 공통 인체 재질 연결
            }

            Rigidbody body = part.AddComponent<Rigidbody>(); // 신체 부위 물리 Rigidbody 추가
            body.mass = Mathf.Max(0.5f, mass); // 신체별 질량 적용
            body.useGravity = true; // 사망 활성화 시 중력 반응 사용
            body.isKinematic = true; // 생존·초기 상태에서는 물리 계산 비활성화
            body.detectCollisions = true; // 활성화 후 바닥 충돌을 사용할 수 있도록 유지
            body.interpolation = RigidbodyInterpolation.Interpolate; // 털썩 쓰러질 때 시각적 보간 적용
            body.linearDamping = 0.12f; // 과도한 미끄러짐 완화
            body.angularDamping = 0.35f; // 관절 떨림과 회전 감쇠
            return body; // 관절 연결에 사용할 Rigidbody 반환
        }

        private static void CreateJoint(Rigidbody childBody, Rigidbody connectedBody, float lowTwist, float highTwist, float swingLimit) // 두 신체 부위를 제한된 CharacterJoint로 연결
        {
            CharacterJoint joint = childBody.gameObject.AddComponent<CharacterJoint>(); // 자식 신체에 관절 추가
            joint.connectedBody = connectedBody; // 부모 역할 Rigidbody 연결
            joint.autoConfigureConnectedAnchor = true; // 현재 서 있는 포즈에서 연결 앵커 자동 계산
            joint.enableCollision = false; // 연결된 자기 신체끼리 불필요한 충돌 방지
            joint.enablePreprocessing = false; // 큰 충격에서 관절 안정성을 높이기 위해 전처리 비활성화
            SoftJointLimit lowLimit = joint.lowTwistLimit; // 낮은 비틀림 제한 구조 복사
            lowLimit.limit = lowTwist; // 낮은 비틀림 각도 설정
            joint.lowTwistLimit = lowLimit; // 낮은 제한 적용
            SoftJointLimit highLimit = joint.highTwistLimit; // 높은 비틀림 제한 구조 복사
            highLimit.limit = highTwist; // 높은 비틀림 각도 설정
            joint.highTwistLimit = highLimit; // 높은 제한 적용
            SoftJointLimit swingOne = joint.swing1Limit; // 첫 번째 스윙 제한 구조 복사
            swingOne.limit = swingLimit; // 첫 번째 스윙 각도 설정
            joint.swing1Limit = swingOne; // 첫 번째 스윙 제한 적용
            SoftJointLimit swingTwo = joint.swing2Limit; // 두 번째 스윙 제한 구조 복사
            swingTwo.limit = swingLimit; // 두 번째 스윙 각도 설정
            joint.swing2Limit = swingTwo; // 두 번째 스윙 제한 적용
        }

        private static MonoBehaviour[] CollectLiveBehaviours(GameObject player, PlayerDeathController deathController) // 사망 순간 중단할 기존 Player 기능 목록 수집
        {
            HashSet<string> disabledTypeNames = new HashSet<string> // PlayerInputReader·진단·PlayerHealth는 남기고 실제 조작 기능만 지정
            {
                "PlayerMovement", // 생존 이동 처리
                "PlayerLook", // 1인칭 시점 조작
                "PlayerCrouch", // 웅크리기 상태 변경
                "PlayerInteractor", // F 상호작용
                "PlayerInventory", // 슬롯 전환·아이템 사용
                "PlayerCarryController", // 손 아이템 화면 동기화
                "CombatController", // 플레이어 근접 전투
                "PlayerNoiseEmitter", // 생존 플레이어 이동 소음
                "PlayerFootstepAudio" // 생존 플레이어 발소리
            }; // 사망 시 비활성화 대상 이름 집합 완료
            List<MonoBehaviour> behaviours = new List<MonoBehaviour>(); // 직렬화할 실제 컴포넌트 목록 생성

            foreach (MonoBehaviour behaviour in player.GetComponents<MonoBehaviour>()) // Player 루트의 기존 기능 전체 순회
            {
                if (behaviour == null || behaviour == deathController) // 유효하지 않거나 사망 컨트롤러 자기 자신인지 확인
                {
                    continue; // 다음 기능 검사
                }

                if (disabledTypeNames.Contains(behaviour.GetType().Name)) // 사망 시 정지 대상 타입인지 확인
                {
                    behaviours.Add(behaviour); // 비활성화 목록에 실제 컴포넌트 추가
                }
            }

            return behaviours.ToArray(); // PlayerDeathController Configure용 배열 반환
        }

        private static void PatchSharedWagonPrefab() // Day21 공통 Wagon.prefab의 창고 전체를 회수품·죽은 플레이어 공통 영역으로 통합
        {
            GameObject wagonRoot = PrefabUtility.LoadPrefabContents(WagonPrefabPath); // 공통 Wagon.prefab 편집용 임시 루트 로드

            if (wagonRoot == null) // 프리팹 로드 성공 여부 확인
            {
                Debug.LogError("[Project I] Day22 Wagon.prefab을 편집할 수 없습니다."); // 프리팹 로드 실패 오류 출력
                return; // 패치 중단
            }

            try // 프리팹 임시 콘텐츠 안전 저장·해제 범위 시작
            {
                Transform legacyCorpseTransform = wagonRoot.transform.Find("CorpseArea"); // 이전 ZIP의 분리된 시체 영역 검색

                if (legacyCorpseTransform != null) // 이전 분리 영역 존재 여부 확인
                {
                    Object.DestroyImmediate(legacyCorpseTransform.gameObject); // 별도 시체 영역을 프리팹에서 완전히 제거
                }

                Transform cargoTransform = wagonRoot.transform.Find("CargoArea"); // Day21 기존 창고 공통 Trigger 조회

                if (cargoTransform == null) // 기존 CargoArea 누락 여부 확인
                {
                    Debug.LogError("[Project I] Wagon.prefab에서 CargoArea를 찾을 수 없습니다."); // 선행 Day21 구조 누락 오류 출력
                    return; // 공통 영역 보정 중단
                }

                cargoTransform.localPosition = new Vector3(0f, 2.30f, -1.70f); // Day21 대형 후방 창고 전체 중심 위치 복구
                cargoTransform.localRotation = Quaternion.identity; // 마차 로컬 축과 동일한 회전 유지
                cargoTransform.localScale = Vector3.one; // Collider 크기 계산용 기본 스케일 유지
                BoxCollider cargoCollider = cargoTransform.GetComponent<BoxCollider>(); // 공통 창고 BoxCollider 조회

                if (cargoCollider == null) // 기존 Trigger Collider 누락 확인
                {
                    cargoCollider = cargoTransform.gameObject.AddComponent<BoxCollider>(); // 공통 창고 Trigger Collider 복구
                }

                cargoCollider.center = Vector3.zero; // 하나의 CargoArea 오브젝트 중심을 공통 판정 중심으로 사용
                cargoCollider.size = new Vector3(3.30f, 2.05f, 7.80f); // 후방 창고 전체를 회수품·죽은 플레이어 동일 범위로 복구
                cargoCollider.isTrigger = true; // 물리 벽이 아닌 공통 회수 상태 판정 영역으로 설정
                WagonCargoArea cargoArea = cargoTransform.GetComponent<WagonCargoArea>(); // 기존 Day21 공통 적재 기능 조회

                if (cargoArea == null) // 공통 적재 기능 누락 여부 확인
                {
                    cargoArea = cargoTransform.gameObject.AddComponent<WagonCargoArea>(); // 동일 Trigger에 회수품·죽은 플레이어 통합 기능 추가
                }

                cargoArea.Configure(cargoCollider); // 하나의 BoxCollider를 모든 회수 판정에 사용하도록 연결
                PrefabUtility.SaveAsPrefabAsset(wagonRoot, WagonPrefabPath); // 수정된 공통 Wagon.prefab 저장
            }
            finally // 저장 성공 여부와 관계없이 임시 프리팹 콘텐츠 해제
            {
                PrefabUtility.UnloadPrefabContents(wagonRoot); // 편집용 임시 프리팹 루트 메모리 해제
            }
        }

        private static void DeleteLegacyCorpseAreaScript() // 이전 분리 CorpseArea 호환 소스와 meta를 자동 정리
        {
            MonoScript legacyScript = AssetDatabase.LoadAssetAtPath<MonoScript>(LegacyCorpseAreaScriptPath); // 이전 WagonCorpseArea 소스 에셋 조회

            if (legacyScript == null) // 이미 삭제됐거나 이전 ZIP을 적용하지 않은 상태인지 확인
            {
                return; // 추가 삭제 불필요
            }

            AssetDatabase.DeleteAsset(LegacyCorpseAreaScriptPath); // 호환용 소스와 연결된 .meta를 함께 프로젝트에서 제거
        }

        private static void EnsureTestInteractables(Transform parent, ExpeditionOutcomeController outcomeController, bool force) // Day22 빠른 수동 검증 오브젝트 구성
        {
            Transform lethalExisting = parent.Find("Day22_Lethal_Ragdoll_Test"); // 기존 치명 피해 테스트 상자 조회

            if (lethalExisting != null && force) // 강제 재생성 상태 확인
            {
                Object.DestroyImmediate(lethalExisting.gameObject); // 이전 테스트 상자 제거
                lethalExisting = null; // 새 생성 준비
            }

            Transform returnExisting = parent.Find("Day22_Return_Result_Terminal"); // 기존 귀환 결과 테스트 단말 조회

            if (returnExisting != null && force) // 강제 재생성 상태 확인
            {
                Object.DestroyImmediate(returnExisting.gameObject); // 이전 결과 단말 제거
                returnExisting = null; // 새 생성 준비
            }

            Material lethalMaterial = GetOrCreateMaterial("DeathTest_Red", new Color(0.55f, 0.08f, 0.06f), 0.04f, 0.28f); // 사망 테스트 식별용 붉은 재질 확보
            Material returnMaterial = GetOrCreateMaterial("ReturnTest_Green", new Color(0.08f, 0.42f, 0.16f), 0.04f, 0.28f); // 귀환 판정 식별용 녹색 재질 확보

            if (lethalExisting == null) // 사망 테스트 오브젝트 미생성 확인
            {
                GameObject lethalObject = GameObject.CreatePrimitive(PrimitiveType.Cube); // F 상호작용 가능한 테스트 큐브 생성
                lethalObject.name = "Day22_Lethal_Ragdoll_Test"; // 계층 식별 이름 지정
                lethalObject.transform.SetParent(parent, false); // Day22 시스템 루트 아래 배치
                lethalObject.transform.position = new Vector3(-5.8f, 0.65f, 14.0f); // Day21 마차 옆 접근 가능한 위치 배치
                lethalObject.transform.localScale = new Vector3(1.3f, 1.3f, 1.3f); // 시선으로 찾기 쉬운 크기 적용
                Renderer lethalRenderer = lethalObject.GetComponent<Renderer>(); // 테스트 큐브 Renderer 조회

                if (lethalRenderer != null) // Renderer 존재 확인
                {
                    lethalRenderer.sharedMaterial = lethalMaterial; // 붉은 식별 재질 적용
                }

                lethalObject.AddComponent<Day22LethalTester>(); // 기존 PlayerHealth.Died 경로를 사용하는 치명 피해 상호작용 추가
            }

            if (returnExisting == null) // 귀환 결과 단말 미생성 확인
            {
                GameObject returnObject = GameObject.CreatePrimitive(PrimitiveType.Cube); // F 상호작용 가능한 귀환 판정 큐브 생성
                returnObject.name = "Day22_Return_Result_Terminal"; // 계층 식별 이름 지정
                returnObject.transform.SetParent(parent, false); // Day22 시스템 루트 아래 배치
                returnObject.transform.position = new Vector3(5.8f, 0.65f, 14.0f); // 마차 반대편 접근 가능한 위치 배치
                returnObject.transform.localScale = new Vector3(1.3f, 1.3f, 1.3f); // 테스트 식별 크기 적용
                Renderer returnRenderer = returnObject.GetComponent<Renderer>(); // 결과 큐브 Renderer 조회

                if (returnRenderer != null) // Renderer 존재 확인
                {
                    returnRenderer.sharedMaterial = returnMaterial; // 녹색 식별 재질 적용
                }

                ExpeditionReturnTerminal terminal = returnObject.AddComponent<ExpeditionReturnTerminal>(); // 현재 생존 상태로 원정 결과를 확정하는 상호작용 추가
                terminal.Configure(outcomeController); // 씬 원정 결과 컨트롤러 참조 연결
            }
        }

        private static Material GetOrCreateMaterial(string materialName, Color baseColor, float metallic, float smoothness) // URP Day22 생성 재질 생성 또는 재사용
        {
            EnsureFolder(MaterialFolder); // 재질 저장 폴더 보장
            string path = $"{MaterialFolder}/{materialName}.mat"; // 생성 재질 에셋 경로 계산
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path); // 기존 동일 이름 재질 조회

            if (material == null) // 최초 생성 여부 확인
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit"); // 프로젝트 URP 기본 Lit Shader 조회

                if (shader == null) // URP Shader 미발견 확인
                {
                    shader = Shader.Find("Standard"); // 에디터 안전용 기본 Shader 대체
                }

                material = new Material(shader); // 새 생성 재질 인스턴스 생성
                AssetDatabase.CreateAsset(material, path); // 프로젝트 에셋으로 저장
            }

            if (material.HasProperty("_BaseColor")) // URP 기본색 속성 존재 확인
            {
                material.SetColor("_BaseColor", baseColor); // 지정 기본색 적용
            }

            if (material.HasProperty("_Color")) // Standard 호환 색상 속성 존재 확인
            {
                material.SetColor("_Color", baseColor); // 대체 기본색 적용
            }

            if (material.HasProperty("_Metallic")) // 금속도 속성 존재 확인
            {
                material.SetFloat("_Metallic", metallic); // 지정 금속도 적용
            }

            if (material.HasProperty("_Smoothness")) // 매끄러움 속성 존재 확인
            {
                material.SetFloat("_Smoothness", smoothness); // 지정 매끄러움 적용
            }

            EditorUtility.SetDirty(material); // 재질 값 저장 대상으로 표시
            return material; // 생성 또는 갱신된 재질 반환
        }

        private static void EnsureFolder(string folderPath) // AssetDatabase 기반 중첩 폴더 안전 생성
        {
            string[] parts = folderPath.Split('/'); // 전체 경로를 폴더 단계별로 분리
            string current = parts[0]; // Assets 루트 기준 시작

            for (int index = 1; index < parts.Length; index++) // 하위 폴더 단계 순회
            {
                string next = $"{current}/{parts[index]}"; // 다음 전체 경로 계산

                if (!AssetDatabase.IsValidFolder(next)) // 대상 폴더 미존재 확인
                {
                    AssetDatabase.CreateFolder(current, parts[index]); // 상위 경로 아래 새 폴더 생성
                }

                current = next; // 다음 단계 기준 경로 갱신
            }
        }

        private static GameObject FindRoot(Scene scene, string rootName) // 지정 이름의 씬 루트 검색
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == rootName); // 첫 일치 루트 반환
        }
    }
}
