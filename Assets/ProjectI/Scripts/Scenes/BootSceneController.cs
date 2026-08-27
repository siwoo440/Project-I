using ProjectI.Core; // 프로젝트 핵심 기능 참조
using ProjectI.Diagnostics; // 프로젝트 로그 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Scenes // 씬 기능 네임스페이스
{
    public sealed class BootSceneController : MonoBehaviour // 부트 씬 제어기
    {
        private void Start() // 씬 시작 시점
        {
            ProjectLog.Log("Boot 씬 시작"); // 부트 시작 로그 출력
            ProjectServices.Get<SceneFlowManager>().LoadMainMenu(); // 메인 메뉴 자동 이동
        }
    }
}
