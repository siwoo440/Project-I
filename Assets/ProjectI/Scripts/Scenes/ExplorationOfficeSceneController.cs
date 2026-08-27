using ProjectI.Core; // 프로젝트 핵심 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Scenes // 씬 기능 네임스페이스
{
    public sealed class ExplorationOfficeSceneController : MonoBehaviour // 탐사 사무소 씬 제어기
    {
        private void OnGUI() // 임시 개발 UI 출력
        {
            GUI.Label(new Rect(24f, 24f, 420f, 30f), "Project I - Exploration Office"); // 사무소 제목 표시
            GUI.Label(new Rect(24f, 54f, 420f, 30f), "Phase 1 baseline scene is active."); // 기준선 상태 표시

            if (GUI.Button(new Rect(24f, 94f, 220f, 42f), "Back to Main Menu")) // 메인 메뉴 이동 버튼
            {
                ProjectServices.Get<SceneFlowManager>().LoadMainMenu(); // 메인 메뉴 이동
            }
        }
    }
}
