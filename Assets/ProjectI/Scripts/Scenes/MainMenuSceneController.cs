using ProjectI.Core; // 프로젝트 핵심 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Scenes // 씬 기능 네임스페이스
{
    public sealed class MainMenuSceneController : MonoBehaviour // 메인 메뉴 씬 제어기
    {
        private void OnGUI() // 임시 개발 UI 출력
        {
            GUI.Label(new Rect(24f, 24f, 360f, 30f), "Project I - Main Menu"); // 메인 메뉴 제목 표시
            GUI.Label(new Rect(24f, 54f, 360f, 30f), "Phase 1 Scene Flow Validation"); // 검증 안내 표시

            if (GUI.Button(new Rect(24f, 94f, 220f, 42f), "Exploration Office")) // 사무소 이동 버튼
            {
                ProjectServices.Get<SceneFlowManager>().LoadExplorationOffice(); // 탐사 사무소 이동
            }
        }
    }
}
