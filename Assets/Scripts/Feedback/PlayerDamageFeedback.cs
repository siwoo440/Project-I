using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    [RequireComponent(typeof(PlayerController))] // 피해 이벤트를 제공하는 PlayerController 지정
    public class PlayerDamageFeedback : MonoBehaviour // 플레이어 피격 화면과 카메라 피드백 처리
    {
        [Header("필수 참조")] // Inspector 필수 참조 설정 구분
        [SerializeField] Transform cameraShakePivot; // 카메라 흔들림 전용 부모 Transform

        [Header("화면 점멸")] // Inspector 화면 점멸 설정 구분
        [SerializeField] Color flashColor = new Color(0.65f, 0f, 0f, 1f); // 피격 화면 색상
        [SerializeField][Range(0f, 1f)] float maximumFlashAlpha = 0.35f; // 화면 점멸 최대 투명도
        [SerializeField] float flashDuration = 0.35f; // 화면 점멸 지속시간
        [SerializeField] float damageForMaximumIntensity = 60f; // 최대 강도에 도달할 피해량

        [Header("카메라 흔들림")] // Inspector 카메라 흔들림 설정 구분
        [SerializeField] float shakeDuration = 0.22f; // 일반 피격 흔들림 지속시간
        [SerializeField] float shakeStrength = 0.08f; // 일반 피격 흔들림 세기
        [SerializeField] float fatalShakeMultiplier = 1.8f; // 즉사 피해 흔들림 배율

        [Header("피격 효과음")] // Inspector 피격 효과음 설정 구분
        [SerializeField] AudioClip damageClip; // 일반 피해 효과음
        [SerializeField] AudioClip fatalDamageClip; // 즉사 피해 효과음
        [SerializeField][Range(0f, 1f)] float audioVolume = 0.7f; // 피격 효과음 음량
        [SerializeField] bool useFallbackTone = true; // 효과음 누락 시 임시 전자음 사용 여부

        PlayerController playerController; // 피해 이벤트를 제공하는 플레이어
        Vector3 pivotBaseLocalPosition; // 카메라 흔들림 Pivot 기본 위치

        float flashTimer; // 남은 화면 점멸 시간
        float flashIntensity; // 현재 화면 점멸 강도
        float shakeTimer; // 남은 카메라 흔들림 시간
        float currentShakeDuration; // 현재 흔들림 전체 지속시간
        float currentShakeStrength; // 현재 흔들림 세기

        void Awake() // 플레이어와 카메라 흔들림 참조 초기화
        {
            playerController = GetComponent<PlayerController>(); // 같은 오브젝트의 PlayerController 가져오기

            if (cameraShakePivot == null) // 카메라 흔들림 Pivot 연결 여부 확인
            {
                Camera playerCamera = GetComponentInChildren<Camera>(); // 플레이어 자식 카메라 검색

                if (playerCamera != null && playerCamera.transform.parent != transform) // 카메라 전용 부모 존재 여부 확인
                {
                    cameraShakePivot = playerCamera.transform.parent; // 카메라 부모를 흔들림 Pivot으로 저장
                }
            }

            if (cameraShakePivot != null) // 흔들림 Pivot 존재 여부 확인
            {
                pivotBaseLocalPosition = cameraShakePivot.localPosition; // Pivot 기본 로컬 위치 저장
            }
        }

        void OnEnable() // 플레이어 피해 이벤트 구독
        {
            if (playerController != null) // PlayerController 존재 여부 확인
            {
                playerController.Damaged += HandleDamaged; // 플레이어 피해 이벤트 연결
            }
        }

        void OnDisable() // 플레이어 피해 이벤트 구독 해제와 카메라 복구
        {
            if (playerController != null) // PlayerController 존재 여부 존재 확인
            {
                playerController.Damaged -= HandleDamaged; // 플레이어 피해 이벤트 연결 해제
            }

            if (cameraShakePivot != null) // 흔들림 Pivot 존재 여부 확인
            {
                cameraShakePivot.localPosition = pivotBaseLocalPosition; // Pivot 위치를 기본값으로 복구
            }
        }

        void Update() // 화면 점멸 시간 갱신
        {
            if (flashTimer > 0f) // 화면 점멸 진행 여부 확인
            {
                flashTimer = Mathf.Max(0f, flashTimer - Time.deltaTime); // 남은 화면 점멸 시간 감소
            }
        }

        void LateUpdate() // 플레이어 이동 처리 후 카메라 흔들림 적용
        {
            if (cameraShakePivot == null) // 카메라 흔들림 Pivot 존재 여부 확인
            {
                return; // 흔들림 처리 중단
            }

            if (shakeTimer <= 0f) // 카메라 흔들림 종료 여부 확인
            {
                cameraShakePivot.localPosition = pivotBaseLocalPosition; // Pivot 위치를 기본값으로 복구
                return; // 흔들림 처리 종료
            }

            shakeTimer = Mathf.Max(0f, shakeTimer - Time.deltaTime); // 남은 흔들림 시간 감소
            float safeDuration = Mathf.Max(0.01f, currentShakeDuration); // 전체 흔들림 지속시간 안전값 계산
            float remainingRatio = shakeTimer / safeDuration; // 흔들림 잔여 비율 계산
            Vector2 randomOffset = Random.insideUnitCircle * currentShakeStrength * remainingRatio; // 무작위 화면 흔들림 위치 계산
            cameraShakePivot.localPosition = pivotBaseLocalPosition + new Vector3(randomOffset.x, randomOffset.y, 0f); // Pivot에 흔들림 위치 적용
        }

        void HandleDamaged(float damage, bool fatal) // 플레이어 피해 이벤트에 화면과 카메라 피드백 적용
        {
            float safeMaximumDamage = Mathf.Max(1f, damageForMaximumIntensity); // 최대 강도 기준 피해량 안전값 계산
            float damageRatio = Mathf.Clamp01(damage / safeMaximumDamage); // 실제 피해량 기반 강도 계산
            float intensity = fatal ? 1f : Mathf.Max(0.25f, damageRatio); // 즉사 여부를 반영한 최종 강도 계산

            flashIntensity = Mathf.Max(flashIntensity, intensity); // 연속 피해에서 더 강한 점멸 강도 유지
            flashTimer = Mathf.Max(0.05f, flashDuration); // 화면 점멸 시간 시작

            float fatalMultiplier = fatal ? Mathf.Max(1f, fatalShakeMultiplier) : 1f; // 즉사 피해 흔들림 배율 결정
            currentShakeDuration = Mathf.Max(0.05f, shakeDuration * fatalMultiplier); // 현재 흔들림 지속시간 계산
            currentShakeStrength = Mathf.Max(0f, shakeStrength * intensity * fatalMultiplier); // 현재 흔들림 세기 계산
            shakeTimer = currentShakeDuration; // 카메라 흔들림 시작

            PlayDamageSound(fatal); // 피해 종류에 맞는 효과음 재생
        }

        void PlayDamageSound(bool fatal) // 일반 또는 즉사 피해 효과음 재생
        {
            AudioManager audioManager = AudioManager.Instance; // 전역 AudioManager 가져오기

            if (audioManager == null) // AudioManager 존재 여부 확인
            {
                audioManager = FindFirstObjectByType<AudioManager>(); // Scene에서 AudioManager 다시 검색
            }

            if (audioManager == null) // AudioManager 검색 결과 확인
            {
                return; // 효과음 처리 중단
            }

            AudioClip selectedClip = fatal ? fatalDamageClip : damageClip; // 피해 종류에 맞는 효과음 선택

            if (selectedClip != null) // 실제 효과음 연결 여부 확인
            {
                audioManager.Play3D(selectedClip, transform.position, audioVolume, 0.1f, 5f); // 플레이어 위치에서 피격음 재생
            }
            else if (useFallbackTone) // 임시 전자음 사용 여부 확인
            {
                float frequency = fatal ? 55f : 85f; // 피해 종류에 맞는 임시 주파수 결정
                float duration = fatal ? 0.35f : 0.18f; // 피해 종류에 맞는 재생시간 결정
                audioManager.PlayTone3D(frequency, duration, transform.position, audioVolume * 0.5f, 5f); // 임시 피격 전자음 재생
            }
        }

        void OnGUI() // 붉은 화면 점멸 표시
        {
            if (flashTimer <= 0f) // 화면 점멸 진행 여부 확인
            {
                flashIntensity = 0f; // 점멸 종료 후 강도 초기화
                return; // 화면 표시 중단
            }

            float safeDuration = Mathf.Max(0.01f, flashDuration); // 화면 점멸 지속시간 안전값 계산
            float remainingRatio = flashTimer / safeDuration; // 점멸 잔여 비율 계산
            float alpha = maximumFlashAlpha * flashIntensity * remainingRatio; // 현재 화면 투명도 계산
            Color previousColor = GUI.color; // 기존 GUI 색상 저장
            GUI.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha); // 붉은 반투명 색상 적용
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture); // 화면 전체에 피격 색상 표시
            GUI.color = previousColor; // 기존 GUI 색상 복구
        }
    }
}