using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectI
{
    /// <summary>
    /// 1인칭 플레이어 컨트롤러. (기획서 PART 4)
    /// 이동/마우스룩/달리기/점프/앉기 + 체력·스태미너 + 낙하 피해.
    /// 입력: 새 Input System 저수준 폴링(Keyboard/Mouse.current). ※키 리바인딩은 이후 설정 파트에서.
    /// TODO: 무게 페널티(InventorySystem 연동, 일차 4), 공격/방어(일차 12).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("이동")]
        [SerializeField] float walkSpeed = 4f;
        [SerializeField] float runSpeed = 7f;
        [SerializeField] float crouchSpeed = 2f;
        [SerializeField] float jumpHeight = 1.1f;
        [SerializeField] float gravity = -20f;

        [Header("시점")]
        [SerializeField] float mouseSensitivity = 0.08f;
        [SerializeField] float minPitch = -85f;
        [SerializeField] float maxPitch = 85f;

        [Header("앉기")]
        [SerializeField] float standHeight = 1.8f;
        [SerializeField] float crouchHeight = 1.0f;
        [SerializeField] float standEyeLocalY = 0.7f;
        [SerializeField] float crouchEyeLocalY = 0.1f;

        [Header("스탯 (기획서 PART 4.1)")]
        [SerializeField] float maxHealth = 100f;
        [SerializeField] float maxStamina = 100f;
        [SerializeField] float sprintStaminaPerSec = 15f;
        [SerializeField] float jumpStaminaCost = 10f;
        [SerializeField] float staminaRegenPerSec = 12f;
        [SerializeField] float staminaRegenDelay = 1f;

        [Header("낙하 피해")]
        [SerializeField] float fallDamageThreshold = 10f; // 이 낙하속도(m/s) 초과분에 피해
        [SerializeField] float fallDamagePerSpeed = 4f;

        [Header("디버그")]
        [SerializeField] bool showDebug = true;

        CharacterController controller;
        Transform cam;
        float pitch;
        float verticalVelocity;
        float health;
        float stamina;
        float lastStaminaUseTime;
        bool isCrouching;
        bool wasGrounded;
        float peakFallSpeed;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            var camComp = GetComponentInChildren<Camera>();
            if (camComp != null) cam = camComp.transform;
            controller.height = standHeight;
            controller.center = Vector3.zero;
            health = maxHealth;
            stamina = maxStamina;
            if (cam != null)
                cam.localPosition = new Vector3(cam.localPosition.x, standEyeLocalY, cam.localPosition.z);
        }

        void Start()
        {
            LockCursor(true);
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            // 에디터 테스트 편의: Esc로 커서 해제, 게임뷰 클릭으로 재잠금
            if (kb.escapeKey.wasPressedThisFrame) LockCursor(false);
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                LockCursor(true);

            HandleLook(mouse);
            HandleCrouch(kb);
            HandleMovement(kb);
        }

        void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void HandleLook(Mouse mouse)
        {
            if (mouse == null || cam == null || Cursor.lockState != CursorLockMode.Locked) return;
            Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
            transform.Rotate(Vector3.up, delta.x);              // 좌우(요) = 몸통 회전
            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
            cam.localRotation = Quaternion.Euler(pitch, 0f, 0f); // 상하(피치) = 카메라만
        }

        void HandleCrouch(Keyboard kb)
        {
            bool wantCrouch = kb.leftCtrlKey.isPressed;
            if (wantCrouch == isCrouching) return;
            isCrouching = wantCrouch;

            controller.height = isCrouching ? crouchHeight : standHeight;
            controller.center = isCrouching
                ? new Vector3(0f, -(standHeight - crouchHeight) / 2f, 0f) // 발 위치 유지
                : Vector3.zero;
            if (cam != null)
            {
                float eye = isCrouching ? crouchEyeLocalY : standEyeLocalY;
                cam.localPosition = new Vector3(cam.localPosition.x, eye, cam.localPosition.z);
            }
        }

        void HandleMovement(Keyboard kb)
        {
            bool grounded = controller.isGrounded;

            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            Vector3 input = new Vector3(x, 0f, z);
            if (input.sqrMagnitude > 1f) input.Normalize();

            bool moving = input.sqrMagnitude > 0.01f;
            bool sprinting = kb.leftShiftKey.isPressed && moving && !isCrouching && z > 0f && stamina > 0f;

            float speed = isCrouching ? crouchSpeed : (sprinting ? runSpeed : walkSpeed);
            Vector3 horizontal = transform.TransformDirection(input) * speed;

            // 스태미너: 달리기 소모 / 미사용 시 딜레이 후 회복
            if (sprinting)
            {
                stamina = Mathf.Max(0f, stamina - sprintStaminaPerSec * Time.deltaTime);
                lastStaminaUseTime = Time.time;
            }
            else if (Time.time - lastStaminaUseTime >= staminaRegenDelay)
            {
                stamina = Mathf.Min(maxStamina, stamina + staminaRegenPerSec * Time.deltaTime);
            }

            // 중력 & 점프
            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (grounded && kb.spaceKey.wasPressedThisFrame && !isCrouching && stamina >= jumpStaminaCost)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                stamina -= jumpStaminaCost;
                lastStaminaUseTime = Time.time;
            }
            verticalVelocity += gravity * Time.deltaTime;

            if (!grounded) peakFallSpeed = Mathf.Max(peakFallSpeed, -verticalVelocity);

            controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);

            // 착지 판정 → 낙하 피해
            bool nowGrounded = controller.isGrounded;
            if (nowGrounded && !wasGrounded)
            {
                if (peakFallSpeed > fallDamageThreshold)
                {
                    float dmg = (peakFallSpeed - fallDamageThreshold) * fallDamagePerSpeed;
                    TakeDamage(dmg);
                    Debug.Log($"[Player] 낙하 피해 {dmg:F0} (낙하속도 {peakFallSpeed:F1} m/s)");
                }
                peakFallSpeed = 0f;
            }
            wasGrounded = nowGrounded;
        }

        /// <summary>피해 적용. (물약 회복·전투는 이후 파트에서 연동)</summary>
        public void TakeDamage(float amount)
        {
            health = Mathf.Max(0f, health - amount);
            if (health <= 0f) Debug.Log("[Player] 사망 (체력 0)");
        }

        void OnGUI()
        {
            if (!showDebug) return;
            GUI.Label(new Rect(10, 10, 500, 20), $"체력: {health:F0} / {maxHealth:F0}");
            GUI.Label(new Rect(10, 30, 500, 20), $"스태미너: {stamina:F0} / {maxStamina:F0}");
            GUI.Label(new Rect(10, 50, 500, 20), $"앉기: {(isCrouching ? "O" : "X")}    접지: {(controller.isGrounded ? "O" : "X")}");
            GUI.Label(new Rect(10, 70, 640, 20), "이동 WASD / 달리기 Shift / 점프 Space / 앉기 Ctrl / 커서해제 Esc(재잠금:클릭)");
        }
    }
}
