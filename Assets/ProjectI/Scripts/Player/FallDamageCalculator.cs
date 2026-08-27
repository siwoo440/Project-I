namespace ProjectI.Player // 플레이어 기능 네임스페이스
{
    public static class FallDamageCalculator // 추락 피해 계산기
    {
        public static float Calculate(float fallDistance, float safeDistance, float damagePerMeter, float maximumDamage) // 추락 거리 기반 피해 계산
        {
            float safeValue = safeDistance > 0f ? safeDistance : 0f; // 안전 거리 음수 방지
            float damageRate = damagePerMeter > 0f ? damagePerMeter : 0f; // 미터당 피해 음수 방지
            float damageLimit = maximumDamage > 0f ? maximumDamage : 0f; // 최대 피해 음수 방지

            if (fallDistance <= safeValue) // 안전 추락 거리 확인
            {
                return 0f; // 안전 거리 이내 피해 없음
            }

            float rawDamage = (fallDistance - safeValue) * damageRate; // 초과 추락 거리 피해 계산
            return rawDamage > damageLimit ? damageLimit : rawDamage; // 최대 피해 이하로 반환
        }
    }
}
