using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Player // 플레이어 기능 네임스페이스
{
    [RequireComponent(typeof(PlayerMovement))] // 이동 컴포넌트 필수 지정
    [RequireComponent(typeof(PlayerStamina))] // 스태미나 컴포넌트 필수 지정
    [RequireComponent(typeof(PlayerLook))] // 시점 컴포넌트 필수 지정
    public sealed class PlayerDebugHud : MonoBehaviour // 3일차 플레이 테스트 HUD
    {
        private PlayerMovement movement; // 이동 상태 참조
        private PlayerStamina stamina; // 스태미나 상태 참조
        private PlayerLook playerLook; // 커서 상태 참조

        private void Awake() // HUD 초기화
        {
            movement = GetComponent<PlayerMovement>(); // 이동 컴포넌트 참조 획득
            stamina = GetComponent<PlayerStamina>(); // 스태미나 컴포넌트 참조 획득
            playerLook = GetComponent<PlayerLook>(); // 시점 컴포넌트 참조 획득
        }

        private void OnGUI() // 디버그 HUD 출력
        {
            GUI.Box(new Rect(18f, 18f, 300f, 112f), "Day 3 - Movement Test"); // HUD 배경과 제목 출력
            GUI.Label(new Rect(32f, 48f, 270f, 24f), "WASD 이동 / Shift 달리기 / ESC 커서"); // 기본 조작 안내 출력
            GUI.Label(new Rect(32f, 72f, 270f, 24f), $"속도: {movement.CurrentPlanarSpeed:0.0} m/s"); // 현재 이동 속도 출력
            GUI.Label(new Rect(32f, 94f, 270f, 24f), playerLook.IsCursorLocked ? "커서: 잠금" : "커서: 해제"); // 커서 상태 출력

            float barWidth = Mathf.Min(360f, Screen.width - 40f); // 스태미나 바 너비 계산
            float barX = 20f; // 스태미나 바 X 위치 지정
            float barY = Screen.height - 54f; // 스태미나 바 Y 위치 지정
            GUI.Box(new Rect(barX, barY, barWidth, 28f), string.Empty); // 스태미나 바 외곽 출력
            GUI.DrawTexture(new Rect(barX + 4f, barY + 4f, (barWidth - 8f) * stamina.Normalized, 20f), Texture2D.whiteTexture); // 현재 스태미나 비율 출력
            GUI.Label(new Rect(barX + 10f, barY + 4f, barWidth - 20f, 20f), $"STAMINA {stamina.CurrentStamina:0}/{stamina.MaxStamina:0}"); // 스태미나 수치 출력

            if (playerLook.IsCursorLocked) // 커서 잠금 상태 확인
            {
                float centerX = Screen.width * 0.5f; // 화면 중앙 X 계산
                float centerY = Screen.height * 0.5f; // 화면 중앙 Y 계산
                GUI.DrawTexture(new Rect(centerX - 1f, centerY - 8f, 2f, 16f), Texture2D.whiteTexture); // 세로 조준선 출력
                GUI.DrawTexture(new Rect(centerX - 8f, centerY - 1f, 16f, 2f), Texture2D.whiteTexture); // 가로 조준선 출력
            }
        }
    }
}
