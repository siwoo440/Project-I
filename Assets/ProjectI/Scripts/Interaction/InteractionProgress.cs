using UnityEngine; // 유니티 수학 기능 참조

namespace ProjectI.Interaction // 상호작용 기능 네임스페이스
{
    public static class InteractionProgress // 길게 누르기 진행도 계산 도우미
    {
        public static float Normalize(float elapsed, float duration) // 경과 시간과 필요 시간으로 0~1 진행도 계산
        {
            if (duration <= 0f) // 유효하지 않은 길게 누르기 시간 확인
            {
                return 1f; // 즉시 완료 상태 반환
            }

            return Mathf.Clamp01(elapsed / duration); // 진행도를 0~1 범위로 반환
        }
    }
}
