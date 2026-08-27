using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Player // 플레이어 기능 네임스페이스
{
    [RequireComponent(typeof(PlayerMovement))] // 이동 컴포넌트 필수 지정
    [RequireComponent(typeof(PlayerStamina))] // 스태미나 컴포넌트 필수 지정
    [RequireComponent(typeof(PlayerLook))] // 시점 컴포넌트 필수 지정
    [RequireComponent(typeof(PlayerHealth))] // 체력 컴포넌트 필수 지정
    [RequireComponent(typeof(PlayerCrouch))] // 웅크리기 컴포넌트 필수 지정
    [RequireComponent(typeof(PlayerFallDamage))] // 추락 피해 컴포넌트 필수 지정
    public sealed class PlayerDebugHud : MonoBehaviour // 4일차 플레이 테스트 HUD
    {
        private PlayerMovement movement; // 이동 상태 참조
        private PlayerStamina stamina; // 스태미나 상태 참조
        private PlayerLook playerLook; // 커서 상태 참조
        private PlayerHealth health; // 체력 상태 참조
        private PlayerCrouch crouch; // 웅크리기 상태 참조
        private PlayerFallDamage fallDamage; // 추락 피해 상태 참조

        private void Awake() // HUD 초기화
        {
            movement = GetComponent<PlayerMovement>(); // 이동 컴포넌트 참조 획득
            stamina = GetComponent<PlayerStamina>(); // 스태미나 컴포넌트 참조 획득
            playerLook = GetComponent<PlayerLook>(); // 시점 컴포넌트 참조 획득
            health = GetComponent<PlayerHealth>(); // 체력 컴포넌트 참조 획득
            crouch = GetComponent<PlayerCrouch>(); // 웅크리기 컴포넌트 참조 획득
            fallDamage = GetComponent<PlayerFallDamage>(); // 추락 피해 컴포넌트 참조 획득
        }

        private void OnGUI() // 디버그 HUD 출력
        {
            GUI.Box(new Rect(18f, 18f, 350f, 170f), "Day 4 - Player Movement / Survival Test"); // HUD 배경과 제목 출력
            GUI.Label(new Rect(32f, 48f, 325f, 24f), "WASD 이동 / Shift 달리기 / Space 점프"); // 이동 조작 안내 출력
            GUI.Label(new Rect(32f, 70f, 325f, 24f), "Left Ctrl 웅크리기 / ESC 커서"); // 웅크리기 조작 안내 출력
            GUI.Label(new Rect(32f, 92f, 325f, 24f), $"속도: {movement.CurrentPlanarSpeed:0.0} m/s / 지상: {movement.IsGrounded}"); // 이동 상태 출력
            GUI.Label(new Rect(32f, 114f, 325f, 24f), $"웅크림: {crouch.IsCrouching} / 천장 차단: {crouch.IsStandBlocked}"); // 웅크림 상태 출력
            GUI.Label(new Rect(32f, 136f, 325f, 24f), $"마지막 추락: {fallDamage.LastFallDistance:0.0} m / 피해: {fallDamage.LastAppliedDamage:0}"); // 추락 상태 출력
            GUI.Label(new Rect(32f, 158f, 325f, 24f), playerLook.IsCursorLocked ? "커서: 잠금" : "커서: 해제"); // 커서 상태 출력
            DrawStatusBars(); // 체력과 스태미나 바 출력
            DrawCrosshair(); // 화면 중앙 조준선 출력
        }

        private void DrawStatusBars() // 상태 바 출력
        {
            float barWidth = Mathf.Min(360f, Screen.width - 40f); // 상태 바 너비 계산
            float barX = 20f; // 상태 바 X 위치 지정
            float healthY = Screen.height - 90f; // 체력 바 Y 위치 지정
            float staminaY = Screen.height - 54f; // 스태미나 바 Y 위치 지정
            GUI.Box(new Rect(barX, healthY, barWidth, 28f), string.Empty); // 체력 바 외곽 출력
            GUI.DrawTexture(new Rect(barX + 4f, healthY + 4f, (barWidth - 8f) * health.Normalized, 20f), Texture2D.whiteTexture); // 현재 체력 비율 출력
            GUI.Label(new Rect(barX + 10f, healthY + 4f, barWidth - 20f, 20f), $"HP {health.CurrentHealth:0}/{health.MaxHealth:0}"); // 체력 수치 출력
            GUI.Box(new Rect(barX, staminaY, barWidth, 28f), string.Empty); // 스태미나 바 외곽 출력
            GUI.DrawTexture(new Rect(barX + 4f, staminaY + 4f, (barWidth - 8f) * stamina.Normalized, 20f), Texture2D.whiteTexture); // 현재 스태미나 비율 출력
            GUI.Label(new Rect(barX + 10f, staminaY + 4f, barWidth - 20f, 20f), $"STAMINA {stamina.CurrentStamina:0}/{stamina.MaxStamina:0}"); // 스태미나 수치 출력
        }

        private void DrawCrosshair() // 조준선 출력
        {
            if (!playerLook.IsCursorLocked) // 커서 잠금 해제 상태 확인
            {
                return; // 조준선 출력 중단
            }

            float centerX = Screen.width * 0.5f; // 화면 중앙 X 계산
            float centerY = Screen.height * 0.5f; // 화면 중앙 Y 계산
            GUI.DrawTexture(new Rect(centerX - 1f, centerY - 8f, 2f, 16f), Texture2D.whiteTexture); // 세로 조준선 출력
            GUI.DrawTexture(new Rect(centerX - 8f, centerY - 1f, 16f, 2f), Texture2D.whiteTexture); // 가로 조준선 출력
        }
    }
}
