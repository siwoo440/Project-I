using System; // GUID와 문자열 처리 기능 사용
using System.Collections.Generic; // Definition 중복 처리 집합 사용
using System.IO; // 생성 씬 존재 여부 확인
using System.Text; // 안정적인 ItemId 문자열 생성 기능 사용
using ProjectI.Items; // ItemDefinition과 WorldItemIdentity 사용
using ProjectI.Loop; // PersistentMapLoader 조회 사용
using ProjectI.Persistence; // DailySnapshotService 사용
using UnityEditor; // Editor 자동 구성 기능 사용
using UnityEditor.SceneManagement; // Scene 편집·저장 기능 사용
using UnityEngine; // GameObject와 Debug 기능 사용
using UnityEngine.SceneManagement; // Scene 구조 기능 사용

namespace ProjectI.EditorTools // 프로젝트 Editor 도구 네임스페이스
{
    [InitializeOnLoad] // 스크립트 컴파일 후 자동 적용 등록
    public static class Phase5Day24Step4DailySnapshotSetup // 24일차 4단계 일차 Snapshot·아이템 복구 데이터 자동 구성
    {
        private const string PersistentScenePath = "Assets/ProjectI/Scenes/00_WagonPersistent.unity"; // Step2 Persistent 씬 경로
        private const string OfficeScenePath = "Assets/ProjectI/Scenes/01_Office.unity"; // 런타임 사무소 환경 씬 경로
        private const string DungeonScenePath = "Assets/ProjectI/Scenes/02_TestDungeon.unity"; // 테스트 던전 환경 씬 경로
        private const string SourceOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 23일차 원본 사무소 씬 경로
        private const string DefinitionFolder = "Assets/ProjectI/Resources/Day24Recovery/Definitions"; // 런타임 ItemRegistry용 Resources 정의 폴더
        private const string RecoveryPrefabFolder = "Assets/ProjectI/Resources/Day24Recovery/GeneratedPrefabs"; // 씬 전용 아이템 복구 Prefab 폴더
        private const string ItemPrefabRoot = "Assets/ProjectI/Prefabs"; // 기존 아이템 Prefab 검색 루트

        static Phase5Day24Step4DailySnapshotSetup() // Editor 로드 시 자동 적용 예약
        {
            EditorApplication.delayCall += ApplyStep4Automatically; // 컴파일 완료 뒤 한 번 자동 실행
        }

        [MenuItem("Tools/Project I/Day 24/Apply Step 4 - Daily Snapshot Recovery")] // 수동 재적용 메뉴 등록
        public static void ApplyStep4() // 복구 Definition 생성과 Persistent 저장 서비스 연결
        {
            EnsurePrerequisiteScenes(); // 1~3단계 생성 씬과 Cargo 구성을 우선 보장

            if (!File.Exists(PersistentScenePath)) // 선행 Persistent 씬 최종 확인
            {
                Debug.LogError("[Project I] 24일차 4단계 적용 실패 / 00_WagonPersistent 씬이 없습니다."); // 선행 단계 실패 안내
                return; // 4단계 중단
            }

            EnsureFolder(DefinitionFolder); // ItemDefinition Resources 폴더 생성
            EnsureFolder(RecoveryPrefabFolder); // 씬 전용 복구 Prefab 폴더 생성
            PatchExistingItemPrefabs(); // 기존 Prefab 기반 아이템을 복구 Definition에 연결
            PatchSceneItems(SourceOfficeScenePath); // 23일차 원본 사무소 아이템 식별자 보정
            PatchSceneItems(OfficeScenePath); // 실제 런타임 Office 아이템 식별자 보정
            PatchSceneItems(DungeonScenePath); // TestDungeon 아이템 식별자 보정
            PatchSceneItems(PersistentScenePath); // Persistent 씬 초기 아이템 식별자 보정
            AddSnapshotServiceToPersistentScene(); // 00_WagonPersistent에 일차 저장 서비스 추가
            AssetDatabase.SaveAssets(); // 생성·수정 에셋 저장
            AssetDatabase.Refresh(); // Resources와 Prefab 변경 재검색
            EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single); // 최종 테스트 시작 씬을 Persistent로 열기
            Debug.Log("[Project I] 24일차 4단계 적용 / 일차 Snapshot + SHA-256 + 이전 일차 복구 데이터 구성 완료"); // 적용 완료 로그 출력
        }

        private static void ApplyStep4Automatically() // 컴파일 뒤 안전한 자동 적용 처리
        {
            EditorApplication.delayCall -= ApplyStep4Automatically; // 중복 예약 제거

            if (EditorApplication.isPlayingOrWillChangePlaymode) // Play Mode 진입 중인지 확인
            {
                return; // Editor 에셋 수정 건너뜀
            }

            Scene activeScene = SceneManager.GetActiveScene(); // 현재 열린 씬 조회

            if (activeScene.IsValid() && activeScene.isDirty) // 저장되지 않은 사용자 씬 편집이 있는지 확인
            {
                Debug.LogWarning("[Project I] 저장되지 않은 씬 변경이 있어 24일차 4단계 자동 적용을 건너뜁니다. 저장 후 Tools > Project I > Day 24 메뉴를 실행하세요."); // 사용자 편집 보호 안내
                return; // 자동 수정 중단
            }

            ApplyStep4(); // 선행 단계 포함 4단계 자동 구성 실행
        }

        private static void EnsurePrerequisiteScenes() // Step2·Step3 선행 구조 존재 보장
        {
            if (!File.Exists(PersistentScenePath) || !File.Exists(OfficeScenePath) || !File.Exists(DungeonScenePath)) // 3개 런타임 씬 누락 여부 확인
            {
                Phase5Day24Step2AdditiveMapSetup.ApplyStep2(); // Persistent + Office + Dungeon 씬 생성
            }

            if (File.Exists(PersistentScenePath)) // Persistent 씬 생성 성공 여부 확인
            {
                Phase5Day24Step3CargoPersistenceSetup.ApplyStep3(); // 동일 WorldItem Cargo 보존 기능 연결
            }
        }

        private static void PatchExistingItemPrefabs() // Assets/ProjectI/Prefabs 아래 WorldItem Prefab 전체 데이터화
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { ItemPrefabRoot }); // 프로젝트 기존 Prefab GUID 전체 검색

            foreach (string guid in prefabGuids) // Prefab 에셋 순회
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid); // GUID를 실제 프로젝트 경로로 변환
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath); // 복구용 원본 Prefab 참조 로드

                if (prefabAsset == null || prefabAsset.GetComponentInChildren<WorldItem>(true) == null) // WorldItem 포함 여부 확인
                {
                    continue; // 일반 환경 Prefab 제외
                }

                GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath); // Prefab 편집용 임시 계층 로드

                try // PrefabContents 반드시 해제하도록 보호
                {
                    WorldItem[] items = contents.GetComponentsInChildren<WorldItem>(true); // Prefab 내부 WorldItem 전체 조회

                    foreach (WorldItem item in items) // Prefab 아이템 순회
                    {
                        ItemDefinition definition = GetOrCreateDefinition(item.DisplayName, prefabAsset); // 기존 Prefab을 RecoveryPrefab으로 사용하는 정의 생성
                        WorldItemIdentity identity = item.GetComponent<WorldItemIdentity>(); // Prefab 식별자 조회

                        if (identity == null) // 이전 Prefab이라 식별자가 없는지 확인
                        {
                            identity = item.gameObject.AddComponent<WorldItemIdentity>(); // 저장 전용 식별자 추가
                        }

                        identity.ConfigureDefinitionTemplate(definition); // Prefab은 종류 Definition만 저장하고 개별 InstanceId는 비움
                        EditorUtility.SetDirty(identity); // Prefab 식별자 변경 기록
                    }

                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath); // 기존 Prefab에 Definition 연결 저장
                }
                finally // 성공·실패 모두 임시 Prefab 해제
                {
                    PrefabUtility.UnloadPrefabContents(contents); // 임시 편집 계층 정리
                }
            }
        }

        private static void PatchSceneItems(string scenePath) // 씬에 배치된 개별 WorldItem에 InstanceId와 Definition 부여
        {
            if (!File.Exists(scenePath)) // 대상 씬 존재 여부 확인
            {
                return; // 없는 선택 씬은 건너뜀
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single); // 대상 씬 단독 열기
            WorldItem[] items = FindComponentsInScene<WorldItem>(scene); // 씬 내 WorldItem 전체 조회

            foreach (WorldItem item in items) // 씬 배치 아이템 순회
            {
                if (item == null) // 파괴된 항목 확인
                {
                    continue; // 다음 아이템 검사
                }

                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item.gameObject); // 기존 Prefab 기반인지 확인
                GameObject recoveryPrefab = string.IsNullOrWhiteSpace(prefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath); // 기존 Prefab을 복구 원본으로 사용
                ItemDefinition definition = GetOrCreateDefinition(item.DisplayName, recoveryPrefab); // 표시 이름 기반 안정 Definition 생성
                WorldItemIdentity identity = item.GetComponent<WorldItemIdentity>(); // 씬 개별 식별자 조회

                if (identity == null) // 기존 씬 아이템인지 확인
                {
                    identity = item.gameObject.AddComponent<WorldItemIdentity>(); // 저장 식별자 추가
                }

                string existingInstanceId = identity.InstanceId; // 기존 적용에서 생성된 InstanceId 보존
                identity.Configure(definition, existingInstanceId); // 씬 개별 아이템에는 실제 InstanceId 부여
                EditorUtility.SetDirty(identity); // 씬 직렬화 변경 기록

                if (recoveryPrefab == null) // Prefab 없이 씬에 직접 만들어진 WorldItem인지 확인
                {
                    recoveryPrefab = CreateOrUpdateGeneratedRecoveryPrefab(item, definition); // 복구 가능한 Resources Prefab 자동 생성
                    definition.Configure(definition.ItemId, definition.DisplayName, recoveryPrefab); // 생성 Prefab을 Definition에 연결
                    EditorUtility.SetDirty(definition); // Definition 변경 기록
                }
            }

            EditorSceneManager.MarkSceneDirty(scene); // 식별자 추가 변경 표시
            EditorSceneManager.SaveScene(scene, scenePath); // 개별 GUID와 Definition 연결 저장
        }

        private static GameObject CreateOrUpdateGeneratedRecoveryPrefab(WorldItem sourceItem, ItemDefinition definition) // 씬 전용 아이템의 복구 Prefab 생성
        {
            string safeName = MakeSafeFileName(definition.ItemId); // 파일 시스템 안전 이름 생성
            string path = $"{RecoveryPrefabFolder}/{safeName}.prefab"; // Resources 복구 Prefab 경로 계산
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(sourceItem.gameObject, path); // 씬 아이템 전체 계층을 별도 Prefab으로 복제 저장

            if (saved == null) // Prefab 저장 실패 확인
            {
                Debug.LogError($"[Project I] RecoveryPrefab 생성 실패 / Item={sourceItem.DisplayName} / Path={path}"); // 복구 데이터 생성 실패 로그
                return null; // Definition에 null 반환
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path); // 생성 Prefab의 개별 InstanceId 제거를 위한 편집 로드

            try // 임시 계층 해제 보장
            {
                WorldItemIdentity[] identities = contents.GetComponentsInChildren<WorldItemIdentity>(true); // 생성 Prefab 식별자 전체 조회

                foreach (WorldItemIdentity identity in identities) // 식별자 순회
                {
                    if (identity != null) // 유효 컴포넌트 확인
                    {
                        identity.ConfigureDefinitionTemplate(definition); // 복구 Prefab에는 종류 정보만 남기고 개별 GUID 제거
                        EditorUtility.SetDirty(identity); // 변경 기록
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(contents, path); // 정리된 복구 Prefab 저장
            }
            finally // 성공·실패 모두 임시 계층 해제
            {
                PrefabUtility.UnloadPrefabContents(contents); // 임시 PrefabContents 정리
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path); // 최종 복구 Prefab 에셋 반환
        }

        private static ItemDefinition GetOrCreateDefinition(string displayName, GameObject recoveryPrefab) // 표시 이름 기준 안정 ItemDefinition 생성·갱신
        {
            string itemId = BuildItemId(displayName); // 저장 파일용 안정 ID 생성
            string assetPath = $"{DefinitionFolder}/{MakeSafeFileName(itemId)}.asset"; // Resources Definition 에셋 경로 계산
            ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath); // 기존 Definition 조회

            if (definition == null) // 최초 아이템 종류인지 확인
            {
                definition = ScriptableObject.CreateInstance<ItemDefinition>(); // 새 Definition 메모리 생성
                AssetDatabase.CreateAsset(definition, assetPath); // Resources 아래 실제 에셋 생성
            }

            GameObject resolvedPrefab = recoveryPrefab != null ? recoveryPrefab : definition.RecoveryPrefab; // 이미 생성된 복구 Prefab은 유지
            definition.Configure(itemId, string.IsNullOrWhiteSpace(displayName) ? itemId : displayName, resolvedPrefab); // Definition 안정 필드 갱신
            EditorUtility.SetDirty(definition); // 에셋 변경 기록
            return definition; // 연결할 Definition 반환
        }

        private static void AddSnapshotServiceToPersistentScene() // Persistent 맵 로더 옆에 일차 Snapshot 서비스 배치
        {
            Scene scene = EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single); // 00_WagonPersistent 단독 열기
            PersistentMapLoader loader = FindComponentInScene<PersistentMapLoader>(scene); // Step2 로더 조회

            if (loader == null) // 필수 PersistentMapLoader 누락 확인
            {
                Debug.LogError("[Project I] 4단계 적용 실패 / PersistentMapLoader를 찾지 못했습니다."); // 구성 오류 안내
                return; // 서비스 추가 중단
            }

            DailySnapshotService service = loader.GetComponent<DailySnapshotService>(); // 기존 Snapshot 서비스 조회

            if (service == null) // 최초 4단계 적용인지 확인
            {
                service = loader.gameObject.AddComponent<DailySnapshotService>(); // 같은 Persistent 시스템 오브젝트에 저장 서비스 추가
            }

            EditorUtility.SetDirty(service); // 컴포넌트 변경 기록
            EditorSceneManager.MarkSceneDirty(scene); // Persistent 씬 변경 표시
            EditorSceneManager.SaveScene(scene, PersistentScenePath); // 저장 서비스 포함 씬 저장
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component // 특정 씬 내부 첫 컴포넌트 조회
        {
            T[] components = FindComponentsInScene<T>(scene); // 전체 컴포넌트 조회
            return components.Length > 0 ? components[0] : null; // 첫 대상 또는 null 반환
        }

        private static T[] FindComponentsInScene<T>(Scene scene) where T : Component // 다른 열린 씬과 섞이지 않는 컴포넌트 검색
        {
            List<T> results = new List<T>(); // 검색 결과 목록 생성

            if (!scene.IsValid() || !scene.isLoaded) // 대상 씬 유효성 확인
            {
                return results.ToArray(); // 빈 결과 반환
            }

            foreach (GameObject root in scene.GetRootGameObjects()) // 대상 씬 루트 순회
            {
                results.AddRange(root.GetComponentsInChildren<T>(true)); // 비활성 포함 하위 컴포넌트 수집
            }

            return results.ToArray(); // 배열 결과 반환
        }

        private static string BuildItemId(string displayName) // 표시 이름에서 안정적인 LEGACY ItemId 생성
        {
            string source = string.IsNullOrWhiteSpace(displayName) ? "UnnamedItem" : displayName.Trim(); // 빈 이름 안전 보정
            StringBuilder builder = new StringBuilder("LEGACY_"); // 이전 Day24 규칙과 호환되는 접두어 사용
            bool previousUnderscore = false; // 중복 구분자 방지 상태

            foreach (char character in source) // 표시 이름 문자 순회
            {
                if (char.IsLetterOrDigit(character)) // 한글·영문·숫자처럼 안정 문자 여부 확인
                {
                    builder.Append(character); // 원래 문자 유지
                    previousUnderscore = false; // 구분자 연속 상태 해제
                }
                else if (!previousUnderscore) // 공백·기호를 하나의 밑줄로 변환할지 확인
                {
                    builder.Append('_'); // 안정 구분자 추가
                    previousUnderscore = true; // 연속 기호 압축 상태 설정
                }
            }

            return builder.ToString().TrimEnd('_'); // 마지막 불필요 구분자 제거 후 반환
        }

        private static string MakeSafeFileName(string value) // Asset 파일명에 사용할 안전 문자열 생성
        {
            char[] invalid = Path.GetInvalidFileNameChars(); // 현재 플랫폼 금지 문자 목록 조회
            StringBuilder builder = new StringBuilder(); // 결과 문자열 버퍼 생성

            foreach (char character in value ?? string.Empty) // 원본 문자열 순회
            {
                builder.Append(Array.IndexOf(invalid, character) >= 0 ? '_' : character); // 금지 문자만 밑줄로 치환
            }

            return string.IsNullOrWhiteSpace(builder.ToString()) ? "UnnamedItem" : builder.ToString(); // 빈 결과 안전 보정
        }

        private static void EnsureFolder(string folderPath) // 중첩 Asset 폴더 생성 보장
        {
            string[] segments = folderPath.Split('/'); // Assets부터 경로 구간 분리
            string current = segments[0]; // 시작 Assets 경로 설정

            for (int index = 1; index < segments.Length; index++) // 하위 폴더 순회
            {
                string next = current + "/" + segments[index]; // 다음 전체 폴더 경로 계산

                if (!AssetDatabase.IsValidFolder(next)) // 아직 폴더가 없는지 확인
                {
                    AssetDatabase.CreateFolder(current, segments[index]); // Unity AssetDatabase를 통해 폴더 생성
                }

                current = next; // 다음 단계 부모 경로 갱신
            }
        }
    }
}
