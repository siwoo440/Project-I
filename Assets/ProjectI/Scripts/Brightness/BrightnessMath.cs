using UnityEngine; // Mathf 수학 기능 참조

namespace ProjectI.Brightness // 밝기 시스템 네임스페이스
{
    public static class BrightnessMath // 밝기 계산에서 공유하는 순수 수학 규칙
    {
        public static float CalculateDistanceAttenuation(float distance, float range) // 광원 거리 감쇠값 계산
        {
            if (range <= 0f) // 유효하지 않은 광원 범위 확인
            {
                return 0f; // 범위가 없으면 영향 없음 반환
            }

            float normalizedDistance = Mathf.Clamp01(distance / range); // 현재 거리를 0~1 범위로 정규화
            return 1f - normalizedDistance; // 광원 중심 1에서 Range 끝 0까지 선형 감쇠
        }

        public static float CalculateContribution(float brightness, float distance, float range) // 한 광원이 특정 위치에 주는 실제 밝기 계산
        {
            float safeBrightness = Mathf.Clamp01(brightness); // 광원 기본 밝기를 0~1로 제한
            float attenuation = CalculateDistanceAttenuation(distance, range); // 현재 위치 거리 감쇠 계산
            return safeBrightness * attenuation; // 기본 밝기와 거리 감쇠를 곱해 실제 기여도 반환
        }

        public static float Combine(float naturalBrightness, float localBrightness) // 자연광과 지역 광원 합계 계산
        {
            return Mathf.Clamp01(naturalBrightness + localBrightness); // 최종 밝기를 0~1 범위로 제한
        }
    }
}
