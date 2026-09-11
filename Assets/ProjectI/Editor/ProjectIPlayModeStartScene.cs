using UnityEditor; // 에디터 기능 참조
using UnityEditor.SceneManagement; // Play 시작 씬 설정 참조

namespace ProjectI.EditorTools // 프로젝트 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 에디터 로드 시 Play 시작 씬만 지정 (에셋·씬 파일은 수정하지 않음)
    public static class ProjectIPlayModeStartScene // Play 버튼을 Persistent 마차 씬(→ Office)에서 시작시키는 설정
    {
        private const string PersistentScenePath = "Assets/ProjectI/Scenes/00_WagonPersistent.unity"; // Play 시작 씬 경로

        static ProjectIPlayModeStartScene() // 에디터 로드 시 실행
        {
            if (EditorSceneManager.playModeStartScene != null) // 이미 지정된 시작 씬이 있는지 확인
            {
                return; // 사용자 지정 유지
            }

            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PersistentScenePath); // 에디터 메모리 설정만 변경
        }
    }
}
