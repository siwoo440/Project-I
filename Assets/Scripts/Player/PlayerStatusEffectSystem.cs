using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    [RequireComponent(typeof(PlayerController))] // 상태 피해를 받을 PlayerController 지정
    public class PlayerStatusEffectSystem : MonoBehaviour // 플레이어 출혈과 둔화 상태 관리
    {
        [Header("디버그")] // Inspector 디버그 설정 구분
        [Tooltip("상태 이상 OnGUI 표시 여부")] [SerializeField] bool showDebug = true; // 상태 이상 OnGUI 표시 여부

        PlayerController playerController; // 상태 이상 대상 플레이어

        float bleedingRemainingTime; // 남은 출혈 지속시간
        float bleedingDamagePerTick; // 출혈 1회 피해량
        float bleedingTickInterval; // 출혈 피해 발생 간격
        float nextBleedingTickTime; // 다음 출혈 피해 발생 시각

        float slowRemainingTime; // 남은 둔화 지속시간
        float slowMovementMultiplier = 1f; // 현재 둔화 이동속도 배율

        public bool IsBleeding => bleedingRemainingTime > 0f; // 현재 출혈 상태 확인
        public bool IsSlowed => slowRemainingTime > 0f; // 현재 둔화 상태 확인
        public float MovementMultiplier => IsSlowed ? slowMovementMultiplier : 1f; // 현재 상태 이상 이동속도 배율

        void Awake() // 상태 이상 대상 플레이어 초기화
        {
            playerController = GetComponent<PlayerController>(); // 같은 오브젝트의 PlayerController 가져오기
        }

        void Update() // 출혈과 둔화 지속시간 갱신
        {
            if (playerController == null) // PlayerController 존재 여부 확인
            {
                return; // 상태 이상 처리 중단
            }

            if (playerController.IsDead) // 플레이어 최종 사망 여부 확인
            {
                ClearAll(); // 사망 후 모든 상태 이상 제거
                return; // 상태 이상 처리 중단
            }

            UpdateBleeding(); // 출혈 피해와 지속시간 갱신
            UpdateSlow(); // 둔화 지속시간 갱신
        }

        public void ApplyBleeding( // 플레이어에게 출혈 상태 적용
            float duration, // 출혈 지속시간
            float damagePerTick, // 출혈 1회 피해량
            float tickInterval) // 출혈 피해 간격
        {
            if (playerController == null || playerController.IsDead) // 플레이어 상태 확인
            {
                return; // 출혈 적용 중단
            }

            float safeDuration = Mathf.Max(0.1f, duration); // 출혈 지속시간 안전값 계산
            float safeDamage = Mathf.Max(0f, damagePerTick); // 출혈 피해량 안전값 계산
            float safeInterval = Mathf.Max(0.1f, tickInterval); // 출혈 피해 간격 안전값 계산
            bool alreadyBleeding = IsBleeding; // 기존 출혈 상태 저장

            bleedingRemainingTime = Mathf.Max(bleedingRemainingTime, safeDuration); // 더 긴 출혈 지속시간 적용
            bleedingDamagePerTick = Mathf.Max(bleedingDamagePerTick, safeDamage); // 더 강한 출혈 피해량 적용

            if (!alreadyBleeding) // 새로 출혈이 적용되는지 확인
            {
                bleedingTickInterval = safeInterval; // 출혈 피해 간격 저장
                nextBleedingTickTime = Time.time + safeInterval; // 첫 출혈 피해 시각 계산
            }
            else // 기존 출혈이 갱신되는 경우
            {
                bleedingTickInterval = Mathf.Min(bleedingTickInterval, safeInterval); // 더 짧은 피해 간격 유지
                nextBleedingTickTime = Mathf.Min(nextBleedingTickTime, Time.time + safeInterval); // 기존 피해 시점보다 늦어지지 않도록 제한
            }

            Debug.Log($"[Status] 출혈 적용 — {bleedingRemainingTime:F1}초"); // 출혈 적용 결과 출력
        }

        public void ApplySlow(float duration, float movementMultiplier) // 플레이어에게 둔화 상태 적용
        {
            if (playerController == null || playerController.IsDead) // 플레이어 상태 확인
            {
                return; // 둔화 적용 중단
            }

            float safeDuration = Mathf.Max(0.1f, duration); // 둔화 지속시간 안전값 계산
            float safeMultiplier = Mathf.Clamp(movementMultiplier, 0.1f, 1f); // 둔화 이동속도 배율 제한

            if (!IsSlowed) // 기존 둔화 상태 존재 여부 확인
            {
                slowMovementMultiplier = safeMultiplier; // 새로운 이동속도 배율 적용
            }
            else // 기존 둔화가 있는 경우
            {
                slowMovementMultiplier = Mathf.Min(slowMovementMultiplier, safeMultiplier); // 더 강한 둔화 배율 유지
            }

            slowRemainingTime = Mathf.Max(slowRemainingTime, safeDuration); // 더 긴 둔화 지속시간 적용
            Debug.Log($"[Status] 둔화 적용 — 이동속도 {slowMovementMultiplier * 100f:F0}%, {slowRemainingTime:F1}초"); // 둔화 적용 결과 출력
        }

        public bool ClearBleeding() // 현재 출혈 상태 제거
        {
            if (!IsBleeding) // 출혈 상태 존재 여부 확인
            {
                return false; // 제거할 출혈 없음 반환
            }

            bleedingRemainingTime = 0f; // 출혈 지속시간 초기화
            bleedingDamagePerTick = 0f; // 출혈 피해량 초기화
            bleedingTickInterval = 0f; // 출혈 피해 간격 초기화
            nextBleedingTickTime = 0f; // 다음 출혈 피해 시각 초기화
            Debug.Log("[Status] 출혈 제거"); // 출혈 제거 결과 출력
            return true; // 출혈 제거 성공 반환
        }

        public bool ClearSlow() // 현재 둔화 상태 제거
        {
            if (!IsSlowed) // 둔화 상태 존재 여부 확인
            {
                return false; // 제거할 둔화 없음 반환
            }

            slowRemainingTime = 0f; // 둔화 지속시간 초기화
            slowMovementMultiplier = 1f; // 이동속도 배율 기본값 복구
            Debug.Log("[Status] 둔화 제거"); // 둔화 제거 결과 출력
            return true; // 둔화 제거 성공 반환
        }

        public bool ClearAll() // 플레이어의 모든 상태 이상 제거
        {
            bool clearedBleeding = ClearBleeding(); // 출혈 제거 결과 저장
            bool clearedSlow = ClearSlow(); // 둔화 제거 결과 저장
            return clearedBleeding || clearedSlow; // 하나 이상의 상태 제거 여부 반환
        }

        void UpdateBleeding() // 출혈 피해 발생과 지속시간 감소 처리
        {
            if (!IsBleeding) // 출혈 상태 존재 여부 확인
            {
                return; // 출혈 처리 중단
            }

            if (Time.time >= nextBleedingTickTime) // 다음 출혈 피해 시각 도달 여부 확인
            {
                playerController.TakeStatusDamage(bleedingDamagePerTick); // 방패를 무시하는 출혈 피해 적용
                nextBleedingTickTime += bleedingTickInterval; // 다음 출혈 피해 시각 갱신

                if (showDebug) // 디버그 로그 표시 여부 확인
                {
                    Debug.Log($"[Status] 출혈 피해 — {bleedingDamagePerTick:F0}"); // 출혈 피해 결과 출력
                }
            }

            bleedingRemainingTime = Mathf.Max(0f, bleedingRemainingTime - Time.deltaTime); // 남은 출혈 지속시간 감소

            if (bleedingRemainingTime <= 0f) // 출혈 종료 여부 확인
            {
                ClearBleeding(); // 출혈 상태 제거
            }
        }

        void UpdateSlow() // 둔화 지속시간 감소 처리
        {
            if (!IsSlowed) // 둔화 상태 존재 여부 확인
            {
                return; // 둔화 처리 중단
            }

            slowRemainingTime = Mathf.Max(0f, slowRemainingTime - Time.deltaTime); // 남은 둔화 지속시간 감소

            if (slowRemainingTime <= 0f) // 둔화 종료 여부 확인
            {
                ClearSlow(); // 둔화 상태 제거
            }
        }

        void OnGUI() // 현재 상태 이상과 남은 시간 표시
        {
            if (!showDebug || !DebugUIToggleController.PlayerInfoVisible) // Inspector 설정과 F1 표시 상태 확인
            {
                return; // 상태 이상 디버그 정보 표시 중단
            }

            float y = 145f; // 첫 상태 이상 표시 세로 위치

            if (IsBleeding) // 출혈 상태 확인
            {
                GUI.Label(new Rect(10f, y, 400f, 20f), $"출혈: {bleedingRemainingTime:F1}초"); // 출혈 남은 시간 표시
                y += 20f; // 다음 상태 표시 위치 이동
            }

            if (IsSlowed) // 둔화 상태 확인
            {
                GUI.Label(new Rect(10f, y, 400f, 20f), $"둔화: {slowRemainingTime:F1}초 / 이동속도 {slowMovementMultiplier * 100f:F0}%"); // 둔화 정보 표시
            }
        }
    }
}