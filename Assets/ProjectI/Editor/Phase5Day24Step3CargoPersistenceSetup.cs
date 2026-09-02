using System.IO; // 생성 씬 파일 존재 여부 확인
using ProjectI.Loop; // PersistentMapLoader와 Cargo 보존 기능 참조
using ProjectI.Wagon; // WagonCargoArea 기능 참조
using UnityEditor; // Unity Editor 자동 적용 기능 참조
using UnityEditor.SceneManagement; // Editor Scene 열기와 저장 기능 참조
using UnityEngine; // GameObject와 Debug 기능 참조
using UnityEngine.SceneManagement; // Scene 구조 기능 참조

namespace ProjectI.EditorTools // 프로젝트 Editor 도구 네임스페이스
{
    [InitializeOnLoad] // 스크립트 컴파일 후 자동 적용 등록
    public static class Phase5Day24Step3CargoPersistenceSetup // 24일차 3단계 실제 Cargo 보존 자동 구성
    {
        private const string PersistentScenePath = "Assets/ProjectI/Scenes/00_WagonPersistent.unity"; // Step2 Persistent 씬 경로

        static Phase5Day24Step3CargoPersistenceSetup() // Editor 로드 시 자동 실행 예약
        {
            EditorApplication.delayCall += ApplyStep3Automatically; // 컴파일 완료 뒤 한 번 자동 적용
        }

        [MenuItem("Tools/Project I/Day 24/Apply Step 3 - Physical Cargo Persistence")] // 수동 재적용 메뉴 등록
        public static void ApplyStep3() // 00_WagonPersistent에 Cargo 보존 컴포넌트 연결
        {
            if (!File.Exists(PersistentScenePath)) // Step2 Persistent 씬 존재 여부 확인
            {
                Debug.LogError("[Project I] 24일차 3단계 적용 실패 / 먼저 2단계 00_WagonPersistent 씬이 필요합니다."); // 선행 단계 누락 로그 출력
                return; // 적용 중단
            }

            Scene persistentScene = EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single); // Persistent 씬 단독 열기
            PersistentMapLoader loader = FindComponentInScene<PersistentMapLoader>(persistentScene); // 맵 로더 조회
            WagonCargoArea cargoArea = FindComponentInScene<WagonCargoArea>(persistentScene); // Persistent Wagon CargoArea 조회

            if (loader == null || cargoArea == null) // 필수 구성 요소 확인
            {
                Debug.LogError("[Project I] 24일차 3단계 적용 실패 / PersistentMapLoader 또는 WagonCargoArea 누락"); // 누락 상태 로그 출력
                return; // 적용 중단
            }

            GameObject wagonRoot = cargoArea.transform.root.gameObject; // CargoArea 기준 Persistent Wagon 루트 확보
            WagonCargoPersistence persistence = wagonRoot.GetComponent<WagonCargoPersistence>(); // 기존 Cargo 보존 컴포넌트 조회

            if (persistence == null) // 아직 3단계 컴포넌트가 없는지 확인
            {
                persistence = wagonRoot.AddComponent<WagonCargoPersistence>(); // Wagon 루트에 실제 Cargo 보존 관리자 추가
            }

            persistence.Configure(wagonRoot.transform, cargoArea); // Wagon과 CargoArea 참조 연결
            loader.ConfigureCargoPersistence(persistence); // 맵 로더와 Cargo 보존 관리자 연결
            EditorUtility.SetDirty(wagonRoot); // Wagon 변경 상태 기록
            EditorUtility.SetDirty(loader); // Loader 직렬화 변경 상태 기록
            EditorSceneManager.MarkSceneDirty(persistentScene); // Persistent 씬 변경 표시
            EditorSceneManager.SaveScene(persistentScene, PersistentScenePath); // 변경된 Persistent 씬 저장
            AssetDatabase.SaveAssets(); // 직렬화된 에셋 변경 저장
            Debug.Log("[Project I] 24일차 3단계 적용 / 실제 Cargo GameObject Persistent 보존 연결 완료"); // 적용 결과 로그 출력
        }

        private static void ApplyStep3Automatically() // 컴파일 직후 안전한 자동 적용 처리
        {
            EditorApplication.delayCall -= ApplyStep3Automatically; // 중복 자동 호출 제거

            if (EditorApplication.isPlayingOrWillChangePlaymode) // Play Mode 진입 여부 확인
            {
                return; // 실행 중 씬 수정 방지
            }

            if (!File.Exists(PersistentScenePath)) // Step2 씬 생성 여부 확인
            {
                return; // 2단계 이전 프로젝트에서는 자동 적용하지 않음
            }

            Scene activeScene = SceneManager.GetActiveScene(); // 현재 Editor 씬 조회

            if (activeScene.IsValid() && activeScene.isDirty) // 저장되지 않은 사용자 씬 편집 여부 확인
            {
                Debug.LogWarning("[Project I] 저장되지 않은 씬 변경이 있어 24일차 3단계 자동 적용을 건너뜁니다. Tools > Project I > Day 24 메뉴에서 수동 적용하세요."); // 안전 안내 로그 출력
                return; // 사용자 미저장 변경 보호
            }

            ApplyStep3(); // 안전한 상태에서 Cargo 보존 구성 자동 적용
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component // 특정 씬 내부 컴포넌트 검색
        {
            if (!scene.IsValid() || !scene.isLoaded) // Scene 유효성 확인
            {
                return null; // 검색 실패 반환
            }

            foreach (GameObject root in scene.GetRootGameObjects()) // Scene 루트 객체 순회
            {
                T component = root.GetComponentInChildren<T>(true); // 루트 하위에서 대상 컴포넌트 조회

                if (component != null) // 대상 발견 여부 확인
                {
                    return component; // 첫 유효 대상 반환
                }
            }

            return null; // 대상이 없으면 null 반환
        }
    }
}
