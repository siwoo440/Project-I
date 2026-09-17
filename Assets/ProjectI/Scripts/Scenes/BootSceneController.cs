using System.Collections; // 코루틴
using ProjectI.Core; // 프로젝트 핵심 기능 참조
using ProjectI.Diagnostics; // 프로젝트 로그 참조
using ProjectI.UI; // 로고 UI
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.UI; // uGUI

namespace ProjectI.Scenes // 씬 기능 네임스페이스
{
    public sealed class BootSceneController : MonoBehaviour // 부트 씬 제어기 (로고 → 메인 메뉴)
    {
        [SerializeField] private float logoDuration = 1.6f; // 로고 표시 시간

        private IEnumerator Start() // 씬 시작 시점
        {
            ProjectLog.Log("Boot 씬 시작"); // 부트 시작 로그 출력
            Cursor.lockState = CursorLockMode.None; // 커서 풀기
            Cursor.visible = false; // 로고 동안 숨김
            Canvas canvas = RetroUi.CreateCanvas("BootCanvas", 0, transform); // 캔버스
            RetroUi.Solid(canvas.transform, "Black", Color.black); // 검은 바탕
            Text logo = RetroUi.Label(canvas.transform, "Logo", "PROJECT  I", 96, RetroUi.Orange, TextAnchor.MiddleCenter); // 로고
            Text loading = RetroUi.Label(canvas.transform, "Loading", "불러오는 중...", 24, RetroUi.OrangeDim, TextAnchor.LowerCenter); // 안내
            RetroUi.Stretch(loading.rectTransform, 0f, 0f, 0f, 80f); // 아래
            CanvasGroup group = canvas.gameObject.AddComponent<CanvasGroup>(); // 투명도

            for (float time = 0f; time < logoDuration; time += Time.unscaledDeltaTime) // 나타났다 사라짐
            {
                float t = time / logoDuration; // 진행
                group.alpha = Mathf.Clamp01(Mathf.Min(t * 4f, (1f - t) * 4f)); // 페이드
                logo.color = Color.Lerp(RetroUi.OrangeDim, RetroUi.Orange, t); // 밝아짐
                yield return null; // 다음 프레임
            }

            group.alpha = 0f; // 끝

            if (ProjectServices.TryGet(out SceneFlowManager flow)) // 씬 관리자
            {
                flow.LoadMainMenu(); // 메인 메뉴 자동 이동
            }
            else
            {
                ProjectLog.Error("SceneFlowManager가 없어 메인 메뉴로 이동하지 못했습니다."); // 오류
            }
        }
    }
}
