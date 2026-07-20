using UnityEngine; // Unity 기본 기능 사용
using UnityEngine.InputSystem; // 새 입력 시스템 사용
using System.Collections; // 조각상 이동속도 버프 코루틴 사용
namespace ProjectI // 프로젝트 공통 네임스페이스
{
    /// <summary>
    /// CharacterController 기반 1인칭 플레이어를 제어.
    /// 이동, 시점, 달리기, 점프, 앉기, 체력, 스태미너, 낙하 피해를 처리.
    /// 인벤토리 무게 페널티, 방패 피해 감소, 사망, 부활의 돌 자동 소생을 연동.
    /// </summary>
    [RequireComponent(typeof(CharacterController))] // CharacterController 필수 지정
    public class PlayerController : MonoBehaviour // 1인칭 플레이어 제어 컴포넌트
    {
        [Header("이동")] // 이동 설정 구분
        [SerializeField] float walkSpeed = 4f; // 기본 걷기 속도
        [SerializeField] float runSpeed = 7f; // 달리기 속도
        [SerializeField] float crouchSpeed = 2f; // 앉은 이동 속도
        [SerializeField] float jumpHeight = 1.1f; // 점프 높이
        [SerializeField] float gravity = -20f; // 중력 가속도

        [Header("시점")] // 시점 설정 구분
        [SerializeField] float mouseSensitivity = 0.08f; // 마우스 감도
        [SerializeField] float minPitch = -85f; // 최소 상하 시야각
        [SerializeField] float maxPitch = 85f; // 최대 상하 시야각

        [Header("앉기")] // 앉기 설정 구분
        [SerializeField] float standHeight = 1.8f; // 서 있을 때 충돌체 높이
        [SerializeField] float crouchHeight = 1f; // 앉았을 때 충돌체 높이
        [SerializeField] float standEyeLocalY = 0.7f; // 서 있을 때 카메라 높이
        [SerializeField] float crouchEyeLocalY = 0.1f; // 앉았을 때 카메라 높이

        [Header("스탯 (기획서 PART 4.1)")] // 플레이어 스탯 설정 구분
        [SerializeField] float maxHealth = 100f; // 최대 체력
        [SerializeField] float maxStamina = 100f; // 최대 스태미너
        [SerializeField] float sprintStaminaPerSec = 15f; // 초당 달리기 스태미너 소모량
        [SerializeField] float jumpStaminaCost = 10f; // 점프 스태미너 소모량
        [SerializeField] float staminaRegenPerSec = 12f; // 초당 스태미너 회복량
        [SerializeField] float staminaRegenDelay = 1f; // 스태미너 회복 대기시간

        [Header("낙하 피해")] // 낙하 피해 설정 구분
        [SerializeField] float fallDamageThreshold = 10f; // 피해 발생 최소 낙하속도
        [SerializeField] float fallDamagePerSpeed = 4f; // 초과 낙하속도당 피해량

        [Header("디버그")] // 디버그 설정 구분
        [SerializeField] bool showDebug = true; // 임시 디버그 UI 표시 여부

        [Header("사망 및 부활")] // 사망 설정 구분
        [SerializeField] string reviveItemName = "부활의 돌"; // 자동 소모 부활 아이템 이름
        [SerializeField][Range(0.05f, 1f)] float reviveHealthRatio = 0.3f; // 부활 후 최대 체력 비율

        CharacterController controller; // 플레이어 이동용 컨트롤러
        InventorySystem inventory; // 무게와 부활석 확인용 인벤토리
        PlayerCombat combat; // 방패와 전투 제어용 컴포넌트
        PlayerInteractor interactor; // 상호작용 제어용 컴포넌트
        PlayerStatusEffectSystem statusEffectSystem; // 출혈과 둔화 상태 관리 컴포넌트
        Transform cam; // 1인칭 시점 카메라 Transform

        float pitch; // 현재 카메라 상하 회전값
        float verticalVelocity; // 현재 수직 이동속도
        float health; // 현재 체력
        float stamina; // 현재 스태미너
        float lastStaminaUseTime; // 마지막 스태미너 사용 시각
        bool isCrouching; // 현재 앉기 상태
        bool wasGrounded; // 이전 프레임 접지 상태
        float peakFallSpeed; // 공중에서 기록한 최대 낙하속도
        bool isDead; // 현재 사망 상태
        float movementSpeedBuffMultiplier = 1f; // 조각상이 적용한 현재 이동속도 배율
        Coroutine movementBuffCoroutine; // 현재 실행 중인 이동속도 버프 코루틴

        public bool IsDead => isDead; // 외부 사망 상태 확인
        public float CurrentHealth => health; // HUD에 현재 체력 전달
        public float MaxHealth => maxHealth; // HUD에 최대 체력 전달
        public float CurrentStamina => stamina; // HUD에 현재 스태미너 전달
        public float MaxStamina => maxStamina; // HUD에 최대 스태미너 전달

        public event System.Action<float, bool> Damaged; // 실제 피해량과 즉사 여부를 외부 피드백에 전달
        public event System.Action Died; // 부활할 수 없는 최종 사망 이벤트
        void Awake() // 플레이어 참조와 초기 수치 설정
        {
            controller = GetComponent<CharacterController>(); // CharacterController 가져오기
            inventory = GetComponent<InventorySystem>(); // 인벤토리 컴포넌트 가져오기
            combat = GetComponent<PlayerCombat>(); // 전투 컴포넌트 가져오기
            interactor = GetComponent<PlayerInteractor>(); // 상호작용 컴포넌트 가져오기
            statusEffectSystem = GetComponent<PlayerStatusEffectSystem>(); // 상태 이상 관리 컴포넌트 가져오기
            Camera cameraComponent = GetComponentInChildren<Camera>(); // 자식 카메라 검색

            if (cameraComponent != null) // 카메라 존재 여부 확인
            {
                cam = cameraComponent.transform; // 카메라 Transform 저장
            }

            controller.height = standHeight; // 초기 충돌체 높이 설정
            controller.center = Vector3.zero; // 초기 충돌체 중심 설정
            health = maxHealth; // 현재 체력을 최대치로 초기화
            stamina = maxStamina; // 현재 스태미너를 최대치로 초기화

            if (cam != null) // 카메라 존재 여부 확인
            {
                cam.localPosition = new Vector3(cam.localPosition.x, standEyeLocalY, cam.localPosition.z); // 초기 카메라 높이 설정
            }
        }

        void Start() // 게임 시작 시 커서 상태 설정
        {
            LockCursor(true); // 마우스 커서 잠금
        }

        void Update() // 매 프레임 플레이어 입력 처리
        {
            if (isDead) // 사망 상태 확인
            {
                return; // 모든 플레이어 입력 중단
            }

            Keyboard keyboard = Keyboard.current; // 현재 키보드 입력 가져오기
            Mouse mouse = Mouse.current; // 현재 마우스 입력 가져오기

            if (keyboard == null) // 키보드 연결 여부 확인
            {
                return; // 입력 처리 중단
            }

            if (keyboard.escapeKey.wasPressedThisFrame && PauseMenuUI.Instance == null) // 일시정지 메뉴가 없는 개발 Scene에서 Escape 입력 확인
            {
                LockCursor(false); // 기존 방식으로 마우스 커서 잠금 해제
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) // 게임 화면 재클릭 확인
            {
                LockCursor(true); // 마우스 커서 다시 잠금
            }

            HandleLook(mouse); // 마우스 시점 처리
            HandleCrouch(keyboard); // 앉기 입력 처리
            HandleMovement(keyboard); // 이동과 점프 처리
        }

        void LockCursor(bool locked) // 마우스 커서 잠금 상태 변경
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; // 커서 잠금 모드 설정
            Cursor.visible = !locked; // 잠금 여부에 따른 커서 표시 설정
        }

        void HandleLook(Mouse mouse) // 마우스 입력으로 플레이어 시점 회전
        {
            if (mouse == null || cam == null || Cursor.lockState != CursorLockMode.Locked) // 시점 회전 가능 상태 확인
            {
                return; // 시점 처리 중단
            }

            float settingsMultiplier = GameSettingsManager.Instance != null ? GameSettingsManager.Instance.MouseSensitivityMultiplier : 1f; // 저장된 마우스 감도 배율 가져오기
            Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity * settingsMultiplier; // 기본 감도와 사용자 설정을 적용한 마우스 이동량 계산

            transform.Rotate(Vector3.up, delta.x); // 플레이어 몸체 좌우 회전
            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch); // 카메라 상하 회전 제한
            cam.localRotation = Quaternion.Euler(pitch, 0f, 0f); // 카메라 상하 회전 적용
        }

        void HandleCrouch(Keyboard keyboard) // 앉기 상태와 충돌체 높이 변경
        {
            bool wantsToCrouch = keyboard.leftCtrlKey.isPressed; // 왼쪽 Ctrl 키 상태 확인

            if (wantsToCrouch == isCrouching) // 기존 앉기 상태와 비교
            {
                return; // 중복 상태 변경 방지
            }

            isCrouching = wantsToCrouch; // 현재 앉기 상태 저장
            controller.height = isCrouching ? crouchHeight : standHeight; // 앉기 상태에 따른 충돌체 높이 설정

            if (isCrouching) // 앉기 상태 확인
            {
                controller.center = new Vector3(0f, -(standHeight - crouchHeight) / 2f, 0f); // 발 위치를 유지하며 충돌체 중심 하강
            }
            else // 서기 상태 처리
            {
                controller.center = Vector3.zero; // 충돌체 중심 원위치
            }

            if (cam != null) // 카메라 존재 여부 확인
            {
                float eyeHeight = isCrouching ? crouchEyeLocalY : standEyeLocalY; // 앉기 상태에 따른 카메라 높이 결정
                cam.localPosition = new Vector3(cam.localPosition.x, eyeHeight, cam.localPosition.z); // 카메라 높이 적용
            }
        }

        void HandleMovement(Keyboard keyboard) // 이동과 스태미너 및 낙하 피해 처리
        {
            bool grounded = controller.isGrounded; // 이동 전 접지 상태 확인
            float horizontalInput = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f); // 좌우 입력값 계산
            float verticalInput = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f); // 전후 입력값 계산
            Vector3 input = new Vector3(horizontalInput, 0f, verticalInput); // 로컬 이동 입력 벡터 생성

            if (input.sqrMagnitude > 1f) // 대각선 입력 크기 확인
            {
                input.Normalize(); // 대각선 이동속도 보정
            }

            bool moving = input.sqrMagnitude > 0.01f; // 실제 이동 입력 여부 확인
            bool sprinting = keyboard.leftShiftKey.isPressed && moving && !isCrouching && verticalInput > 0f && stamina > 0f; // 전진 달리기 가능 여부 확인
            
            float speed = isCrouching ? crouchSpeed : sprinting ? runSpeed : walkSpeed; // 현재 이동속도 결정
            speed *= movementSpeedBuffMultiplier; // 조각상 이동속도 버프 배율 적용
           
            if (statusEffectSystem != null) // 상태 이상 시스템 존재 여부 확인
            {
                speed *= statusEffectSystem.MovementMultiplier; // 현재 둔화 이동속도 배율 적용
            }

            if (inventory != null) // 인벤토리 존재 여부 확인
            {
                speed *= inventory.SpeedMultiplier; // 무게 기반 이동속도 배율 적용
            }

            Vector3 horizontalVelocity = transform.TransformDirection(input) * speed; // 월드 기준 수평 이동속도 계산

            if (sprinting) // 달리기 상태 확인
            {
                stamina = Mathf.Max(0f, stamina - sprintStaminaPerSec * Time.deltaTime); // 달리기 스태미너 감소
                lastStaminaUseTime = Time.time; // 마지막 스태미너 사용 시각 갱신
            }
            else if (Time.time - lastStaminaUseTime >= staminaRegenDelay) // 스태미너 회복 대기시간 확인
            {
                float regenerationMultiplier = inventory != null ? inventory.StaminaRegenMultiplier : 1f; // 무게 기반 회복 배율 결정
                stamina = Mathf.Min(maxStamina, stamina + staminaRegenPerSec * regenerationMultiplier * Time.deltaTime); // 스태미너 회복과 최대치 제한
            }

            if (grounded && verticalVelocity < 0f) // 지면에서 하강 중인지 확인
            {
                verticalVelocity = -2f; // 지면 접촉 유지용 수직속도 설정
            }

            if (grounded && keyboard.spaceKey.wasPressedThisFrame && !isCrouching && stamina >= jumpStaminaCost) // 점프 가능 조건 확인
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity); // 목표 높이에 필요한 점프속도 계산
                stamina -= jumpStaminaCost; // 점프 스태미너 소모
                lastStaminaUseTime = Time.time; // 마지막 스태미너 사용 시각 갱신
            }

            verticalVelocity += gravity * Time.deltaTime; // 수직속도에 중력 적용

            if (!grounded) // 공중 상태 확인
            {
                peakFallSpeed = Mathf.Max(peakFallSpeed, -verticalVelocity); // 최대 낙하속도 기록
            }

            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime); // 수평과 수직 이동 적용

            bool nowGrounded = controller.isGrounded; // 이동 후 접지 상태 확인

            if (nowGrounded && !wasGrounded) // 이번 프레임 착지 여부 확인
            {
                if (peakFallSpeed > fallDamageThreshold) // 낙하 피해 기준 초과 여부 확인
                {
                    float fallDamage = (peakFallSpeed - fallDamageThreshold) * fallDamagePerSpeed; // 초과 낙하속도 기반 피해 계산

                    TakeDamage(fallDamage); // 플레이어에게 낙하 피해 적용
                    Debug.Log($"[Player] 낙하 피해 {fallDamage:F0} (낙하속도 {peakFallSpeed:F1} m/s)"); // 낙하 피해 결과 출력
                }

                peakFallSpeed = 0f; // 착지 후 낙하속도 기록 초기화
            }

            wasGrounded = nowGrounded; // 현재 접지 상태 저장
        }

        public void TakeDamage(float amount) // 플레이어 피해 적용
        {
            if (isDead) // 이미 사망했는지 확인
            {
                return; // 사망 후 추가 피해 무시
            }

            amount = Mathf.Max(0f, amount); // 음수 피해 방지

            if (combat != null && combat.CurrentBlockReduction > 0f) // 방패 방어 상태 확인
            {
                amount *= 1f - combat.CurrentBlockReduction; // 방패 피해 감소 적용
            }

            health = Mathf.Max(0f, health - amount); // 현재 체력 감소
            if (amount > 0f) // 실제 피해 발생 여부 확인
            {
                Damaged?.Invoke(amount, false); // 일반 피해량과 즉사 아님 상태 전달
            }


            Debug.Log($"[Player] 피해 {amount:F0}, 남은 체력 {health:F0}/{maxHealth:F0}"); // 피해 결과 출력

            if (health <= 0f) // 체력 소진 확인
            {
                HandleDeath(); // 사망 처리 실행
            }
        }
        public bool Heal(float amount) // 플레이어 현재 체력 회복
        {
            if (isDead) // 플레이어 최종 사망 상태 확인
            {
                return false; // 회복 실패 반환
            }

            float safeAmount = Mathf.Max(0f, amount); // 회복량 안전값 계산

            if (safeAmount <= 0f || health >= maxHealth) // 회복 가능 여부 확인
            {
                return false; // 회복 실패 반환
            }

            float previousHealth = health; // 회복 전 체력 저장
            health = Mathf.Min(maxHealth, health + safeAmount); // 최대 체력을 넘지 않도록 회복
            float recoveredAmount = health - previousHealth; // 실제 회복된 체력 계산
            Debug.Log($"[Player] 체력 {recoveredAmount:F0} 회복 — {health:F0}/{maxHealth:F0}"); // 회복 결과 출력
            return recoveredAmount > 0f; // 실제 회복 성공 여부 반환
        }
        public void TakeStatusDamage(float amount) // 방패를 무시하는 상태 이상 피해 적용
        {
            if (isDead) // 플레이어 사망 상태 확인
            {
                return; // 상태 이상 피해 처리 중단
            }

            float safeDamage = Mathf.Max(0f, amount); // 상태 이상 피해량 안전값 계산

            if (safeDamage <= 0f) // 실제 피해 존재 여부 확인
            {
                return; // 상태 이상 피해 처리 중단
            }

            health = Mathf.Max(0f, health - safeDamage); // 방패 계산 없이 현재 체력 감소
            Damaged?.Invoke(safeDamage, false); // 피격 화면과 카메라 피드백에 실제 피해 전달
            Debug.Log($"[Player] 상태 이상 피해 {safeDamage:F0}, 남은 체력 {health:F0}/{maxHealth:F0}"); // 피해 결과 출력

            if (health <= 0f) // 체력 소진 여부 확인
            {
                HandleDeath(); // 기존 부활과 사망 처리 실행
            }
        }

        public void TakeFatalDamage() // 방패를 무시하고 플레이어를 즉시 사망 상태로 처리
        {
            if (isDead) // 플레이어가 이미 사망했는지 확인
            {
                return; // 중복 사망 처리를 방지
            }

            health = 0f; // 현재 체력을 즉시 0으로 설정
            Damaged?.Invoke(maxHealth, true); // 최대 피해량과 즉사 상태를 외부 피드백에 전달
            Debug.Log("[Player] 즉사 피해를 받았습니다."); // 즉사 피해 발생 기록
            HandleDeath(); // 부활의 돌 확인을 포함한 기존 사망 처리 실행
        }

        public void ApplyMovementBuff(float multiplier, float duration) // 일정 시간 동안 이동속도 버프 적용
        {
            float safeMultiplier = Mathf.Max(1f, multiplier); // 이동속도 배율이 기본값보다 작지 않도록 제한
            float safeDuration = Mathf.Max(0.1f, duration); // 버프 지속시간이 너무 짧거나 음수가 되지 않도록 제한

            if (movementBuffCoroutine != null) // 기존 이동속도 버프가 실행 중인지 확인
            {
                StopCoroutine(movementBuffCoroutine); // 기존 버프 코루틴 중단
            }

            movementBuffCoroutine = StartCoroutine(MovementBuffRoutine(safeMultiplier, safeDuration)); // 새로운 이동속도 버프 시작
        }

        IEnumerator MovementBuffRoutine(float multiplier, float duration) // 이동속도 배율을 적용하고 지속시간 후 해제
        {
            movementSpeedBuffMultiplier = multiplier; // 현재 이동속도에 버프 배율 적용
            Debug.Log($"[Player] 이동속도 버프 적용 — {multiplier:F2}배, {duration:F0}초"); // 버프 적용 결과 출력

            yield return new WaitForSeconds(duration); // 설정된 버프 지속시간만큼 대기

            movementSpeedBuffMultiplier = 1f; // 이동속도 배율을 기본값으로 복구
            movementBuffCoroutine = null; // 실행 중인 버프 코루틴 참조 초기화

            Debug.Log("[Player] 이동속도 버프가 종료되었습니다."); // 버프 종료 결과 출력
        }

        void HandleDeath() // 사망 또는 부활석 자동 소생 처리
        {
            if (inventory != null && inventory.ConsumeItemByName(reviveItemName)) // 부활석 소지 확인과 소모
            {
                health = Mathf.Max(1f, maxHealth * reviveHealthRatio); // 최대 체력 비율로 부활
                if (statusEffectSystem != null) // 상태 이상 시스템 존재 여부 확인
                {
                    statusEffectSystem.ClearAll(); // 부활 시 모든 상태 이상 제거
                }

                Debug.Log($"[Player] {reviveItemName} 사용, 체력 {health:F0}으로 부활"); // 부활 결과 출력

                return; // 사망 상태 진입 방지
            }

            isDead = true; // 사망 상태 활성화
            if (statusEffectSystem != null) // 상태 이상 시스템 존재 여부 확인
            {
                statusEffectSystem.ClearAll(); // 최종 사망 시 모든 상태 이상 제거
            }

            health = 0f; // 현재 체력 0 고정
            verticalVelocity = 0f; // 수직 이동 정지
           
            if (inventory != null) // 인벤토리 존재 여부 확인
            {
                inventory.DropAll(transform.position); // 사망한 위치에 모든 소지품 드랍
            }

            if (combat != null) // 전투 컴포넌트 존재 여부 확인
            {
                combat.enabled = false; // 공격과 방어 입력 차단
            }

            if (interactor != null) // 상호작용 컴포넌트 존재 여부 확인
            {
                interactor.enabled = false; // 상호작용과 아이템 버리기 차단
            }

            LockCursor(false); // 마우스 커서 잠금 해제
            Died?.Invoke(); // 결과 매니저에 최종 사망 전달
            Debug.Log("[Player] 사망, 부활의 돌 없음"); // 최종 사망 결과 출력
        }

        void OnGUI() // 임시 플레이어 UI 표시
        {
            if (isDead && (RunResultManager.Instance == null || !RunResultManager.Instance.HasResult)) 
                // 중앙 결과 화면이 없을 때만 기존 사망 화면 표시
            {
                DrawDeathScreen(); // 사망 화면 표시
            }

            if (!showDebug || !DebugUIToggleController.PlayerInfoVisible) // Inspector 설정과 F1 표시 상태 확인
            {
                return; // 기존 플레이어 디버그 정보 표시 중단
            }

            GUI.Label(new Rect(10f, 10f, 500f, 20f), 
                $"체력: {health:F0} / {maxHealth:F0}"); // 현재 체력 표시
            
            GUI.Label(new Rect(10f, 30f, 500f, 20f), 
                $"스태미너: {stamina:F0} / {maxStamina:F0}"); // 현재 스태미너 표시
           
            GUI.Label(new Rect(10f, 50f, 500f, 20f), 
                $"앉기: {(isCrouching ? "O" : "X")}    접지: {(controller.isGrounded ? "O" : "X")}"); 
            // 앉기와 접지 상태 표시
            
            GUI.Label(new Rect(10f, 70f, 760f, 20f), 
                "이동 WASD / 달리기 Shift / 점프 Space / 앉기 Ctrl / 아이템 사용 R / 커서해제 Esc"); 
            // 플레이어 조작법 표시
        }

        void DrawDeathScreen() // 임시 사망 화면 표시
        {
            float width = 400f; // 사망 창 너비
            float height = 160f; // 사망 창 높이
            float x = (Screen.width - width) * 0.5f; // 화면 중앙 가로 위치
            float y = (Screen.height - height) * 0.5f; // 화면 중앙 세로 위치

            GUI.Box(new Rect(x, y, width, height), "사망"); // 사망 창 배경 표시
            GUI.Label(new Rect(x + 40f, y + 55f, width - 80f, 30f), "부활의 돌이 없어 부활할 수 없습니다."); // 부활 불가 안내 표시
            GUI.Label(new Rect(x + 40f, y + 90f, width - 80f, 30f), "플레이어 조작이 중단되었습니다."); // 조작 중단 안내 표시
        }
    }
}