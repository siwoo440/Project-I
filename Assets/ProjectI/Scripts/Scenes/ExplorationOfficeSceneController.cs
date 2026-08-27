using ProjectI.Core; // 프로젝트 핵심 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Scenes // 씬 기능 네임스페이스
{
    public sealed class ExplorationOfficeSceneController : MonoBehaviour // 탐사 사무소 씬 제어기
    {
        private void OnGUI() // 테스트 씬 보조 UI 출력
        {
            if (Cursor.lockState != CursorLockMode.None) // 커서 잠금 상태 확인
            {
                return; // 플레이 중 메뉴 버튼 숨김
            }

            float buttonX = Mathf.Max(24f, Screen.width - 244f); // 우측 메뉴 버튼 X 위치 계산

            if (GUI.Button(new Rect(buttonX, 24f, 220f, 42f), "Back to Main Menu")) // 메인 메뉴 이동 버튼 출력
            {
                ProjectServices.Get<SceneFlowManager>().LoadMainMenu(); // 메인 메뉴 이동
            }
        }
    }
}
