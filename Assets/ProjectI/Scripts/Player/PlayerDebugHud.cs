using ProjectI.Diagnostics; // 공통 F1 디버그 페이지 기능 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Player // 플레이어 기능 네임스페이스
{
    [RequireComponent(typeof(PlayerMovement))] // 이동 컴포넌트 필수 지정
    [RequireComponent(typeof(PlayerStamina))] // 스태미나 컴포넌트 필수 지정
    [RequireComponent(typeof(PlayerLook))] // 시점 컴포넌트 필수 지정
    [RequireComponent(typeof(PlayerHealth))] // 체력 컴포넌트 필수 지정
    [RequireComponent(typeof(PlayerCrouch))] // 웅크리기 컴포넌트 필수 지정
    [RequireComponent(typeof(PlayerFallDamage))] // 추락 피해 컴포넌트 필수 지정
    public sealed class PlayerDebugHud : DebugPageProvider // 플레이어 상태를 F1 디버그 페이지로 제공하고 실제 HUD 요소는 계속 표시
    {
        private PlayerMovement movement; // 이동 상태 참조
        private PlayerStamina stamina; // 스태미나 상태 참조
        private PlayerLook playerLook; // 커서 상태 참조
        private PlayerHealth health; // 체력 상태 참조
        private PlayerCrouch crouch; // 웅크리기 상태 참조
        private PlayerFallDamage fallDamage; // 추락 피해 상태 참조

        public override string PageName => "Player Debug"; // 공통 디버그 창에 표시할 페이지 이름
        public override int SortOrder => 10; // 플레이어 페이지를 첫 번째 그룹에 배치

        private void Awake() // HUD 초기화
        {
            movement = GetComponent<PlayerMovement>(); // 이동 컴포넌트 참조 획득
            stamina = GetComponent<PlayerStamina>(); // 스태미나 컴포넌트 참조 획득
            playerLook = GetComponent<PlayerLook>(); // 시점 컴포넌트 참조 획득
            health = GetComponent<PlayerHealth>(); // 체력 컴포넌트 참조 획득
            crouch = GetComponent<PlayerCrouch>(); // 웅크리기 컴포넌트 참조 획득
            fallDamage = GetComponent<PlayerFallDamage>(); // 추락 피해 컴포넌트 참조 획득
        }

        private void OnGUI() // 실제 플레이 HUD에서 계속 표시해야 하는 요소 출력
        {
            DrawStatusBars(); // HP와 스태미나 바는 F1 창과 무관하게 항상 출력
            DrawCrosshair(); // 중앙 조준선은 F1 창과 무관하게 항상 출력
        }

        public override string BuildDebugText() // 공통 F1 디버그 창의 플레이어 페이지 내용 생성
        {
            string cursorState = playerLook != null && playerLook.IsCursorLocked ? "잠금" : "해제"; // 현재 커서 상태 문자열 생성
            float speed = movement == null ? 0f : movement.CurrentPlanarSpeed; // 현재 평면 이동 속도 조회
            bool grounded = movement != null && movement.IsGrounded; // 현재 지상 여부 조회
            bool crouching = crouch != null && crouch.IsCrouching; // 현재 웅크림 여부 조회
            bool standBlocked = crouch != null && crouch.IsStandBlocked; // 현재 천장으로 일어서기 차단 여부 조회
            float fallDistance = fallDamage == null ? 0f : fallDamage.LastFallDistance; // 마지막 추락 거리 조회
            float fallDamageValue = fallDamage == null ? 0f : fallDamage.LastAppliedDamage; // 마지막 추락 피해 조회
            float currentHealth = health == null ? 0f : health.CurrentHealth; // 현재 체력 조회
            float maxHealth = health == null ? 0f : health.MaxHealth; // 최대 체력 조회
            float currentStamina = stamina == null ? 0f : stamina.CurrentStamina; // 현재 스태미나 조회
            float maxStamina = stamina == null ? 0f : stamina.MaxStamina; // 최대 스태미나 조회
            return $"Movement / Survival\n\n속도 : {speed:0.0} m/s\n지상 : {grounded}\n웅크림 : {crouching}\n천장 차단 : {standBlocked}\n마지막 추락 : {fallDistance:0.0} m\n추락 피해 : {fallDamageValue:0}\nHP : {currentHealth:0}/{maxHealth:0}\nStamina : {currentStamina:0}/{maxStamina:0}\nCursor : {cursorState}"; // 플레이어 디버그 정보를 하나의 페이지 문자열로 반환
        }

        private void DrawStatusBars() // 상태 바 출력
        {
            if (health == null || stamina == null) // HP 또는 스태미나 참조 누락 확인
            {
                return; // 상태 바 출력 중단
            }

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
            if (playerLook == null || !playerLook.IsCursorLocked) // 커서 잠금 해제 또는 시점 참조 누락 상태 확인
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
