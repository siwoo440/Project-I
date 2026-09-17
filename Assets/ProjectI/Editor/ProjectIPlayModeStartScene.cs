using UnityEditor; // 에디터 기능 참조
using UnityEditor.SceneManagement; // Play 시작 씬 설정 참조

namespace ProjectI.EditorTools // 프로젝트 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 에디터 로드 시 Play 시작 씬만 지정 (에셋·씬 파일은 수정하지 않음)
    public static class ProjectIPlayModeStartScene // Play 버튼 시작 씬: 기본은 Persistent 마차 씬(→ 마을), 메뉴로 Boot 시작 전환
    {
        private const string PersistentScenePath = "Assets/ProjectI/Scenes/00_WagonPersistent.unity"; // 바로 게임 시작
        private const string BootScenePath = "Assets/ProjectI/Scenes/Boot.unity"; // 실제 실행 흐름 (로고 → 메인 메뉴)
        private const string PlayFromBootKey = "ProjectI.PlayFromBoot"; // 에디터 설정 키
        private const string PlayFromBootMenu = "Project I/Play From Boot (Main Menu)"; // 메뉴

        static ProjectIPlayModeStartScene() // 에디터 로드 시 실행
        {
            EditorApplication.delayCall += Apply; // 설정 반영 (메뉴 체크 표시 포함)
        }

        [MenuItem(PlayFromBootMenu, false, 1)] // 전환 메뉴
        private static void TogglePlayFromBoot() // Boot 시작 켜고 끄기
        {
            EditorPrefs.SetBool(PlayFromBootKey, !EditorPrefs.GetBool(PlayFromBootKey, false)); // 전환
            Apply(); // 반영
        }

        private static void Apply() // 시작 씬 지정
        {
            bool fromBoot = EditorPrefs.GetBool(PlayFromBootKey, false); // Boot 시작 여부
            Menu.SetChecked(PlayFromBootMenu, fromBoot); // 체크 표시
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(fromBoot ? BootScenePath : PersistentScenePath); // 에디터 메모리 설정만 변경
        }
    }
}
