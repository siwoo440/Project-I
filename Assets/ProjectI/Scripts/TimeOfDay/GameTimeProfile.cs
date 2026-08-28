namespace ProjectI.TimeOfDay // 시간대 시스템 네임스페이스
{
    public static class GameTimeProfile // 시간에 따른 게임용 태양·달 밝기 프로필
    {
        public const float MaximumSunBrightness = 0.65f; // 낮 시간대 태양 게임 밝기 최대값
        public const float MaximumMoonBrightness = 0.08f; // 밤 시간대 달 게임 밝기 최대값

        public static float NormalizeHour(float hour) // 임의의 시간을 0 이상 24 미만으로 정규화
        {
            float normalized = hour % 24f; // 24시간을 기준으로 나머지 계산

            if (normalized < 0f) // 음수 시간이 들어왔는지 확인
            {
                normalized += 24f; // 전날 시간을 현재 하루 범위로 보정
            }

            return normalized; // 0~24 범위 시간 반환
        }

        public static GameTimePhase EvaluatePhase(float hour) // 현재 시간을 새벽·낮·저녁·밤으로 분류
        {
            float value = NormalizeHour(hour); // 입력 시간을 하루 범위로 정규화

            if (value >= 5f && value < 7f) // 05:00~07:00 구간 확인
            {
                return GameTimePhase.Dawn; // 새벽 반환
            }

            if (value >= 7f && value < 18f) // 07:00~18:00 구간 확인
            {
                return GameTimePhase.Day; // 낮 반환
            }

            if (value >= 18f && value < 20f) // 18:00~20:00 구간 확인
            {
                return GameTimePhase.Dusk; // 저녁 반환
            }

            return GameTimePhase.Night; // 나머지 20:00~05:00는 밤 반환
        }

        public static float EvaluateSunBrightness(float hour) // 현재 시간의 태양 게임 밝기 계산
        {
            float value = NormalizeHour(hour); // 입력 시간을 하루 범위로 정규화

            if (value >= 5f && value < 7f) // 일출 초반 새벽 구간 확인
            {
                return Lerp(0f, 0.40f, InverseLerp(5f, 7f, value)); // 05시 0에서 07시 0.40까지 부드럽게 증가
            }

            if (value >= 7f && value < 9f) // 아침 태양 상승 구간 확인
            {
                return Lerp(0.40f, MaximumSunBrightness, InverseLerp(7f, 9f, value)); // 09시까지 최대 태양 밝기로 증가
            }

            if (value >= 9f && value < 17f) // 안정적인 낮 구간 확인
            {
                return MaximumSunBrightness; // 낮 동안 최대 태양 밝기 유지
            }

            if (value >= 17f && value < 20f) // 해질녘 태양 감소 구간 확인
            {
                return Lerp(MaximumSunBrightness, 0f, InverseLerp(17f, 20f, value)); // 17시부터 20시까지 태양 밝기 감소
            }

            return 0f; // 밤에는 태양 밝기 없음
        }

        public static float EvaluateMoonBrightness(float hour) // 현재 시간의 달 게임 밝기 계산
        {
            float value = NormalizeHour(hour); // 입력 시간을 하루 범위로 정규화

            if (value >= 20f || value < 5f) // 완전한 밤 구간 확인
            {
                return MaximumMoonBrightness; // 밤에는 약한 최대 달빛 유지
            }

            if (value >= 5f && value < 7f) // 새벽 달빛 감소 구간 확인
            {
                return Lerp(MaximumMoonBrightness, 0f, InverseLerp(5f, 7f, value)); // 해가 뜨면서 달빛을 부드럽게 제거
            }

            if (value >= 18f && value < 20f) // 저녁 달빛 증가 구간 확인
            {
                return Lerp(0f, MaximumMoonBrightness, InverseLerp(18f, 20f, value)); // 해질녘부터 달빛을 부드럽게 증가
            }

            return 0f; // 낮에는 달빛 없음
        }

        private static float InverseLerp(float minimum, float maximum, float value) // 두 시간 사이의 0~1 진행률 계산
        {
            if (maximum <= minimum) // 잘못된 범위인지 확인
            {
                return 0f; // 안전한 기본 진행률 반환
            }

            return Clamp01((value - minimum) / (maximum - minimum)); // 입력 시간을 0~1 진행률로 변환
        }

        private static float Lerp(float from, float to, float t) // 두 밝기 값을 진행률에 따라 선형 보간
        {
            return from + ((to - from) * Clamp01(t)); // 0~1 보간 결과 반환
        }

        private static float Clamp01(float value) // 부동소수 값을 0~1 범위로 제한
        {
            if (value < 0f) // 0 미만 여부 확인
            {
                return 0f; // 최소값 반환
            }

            if (value > 1f) // 1 초과 여부 확인
            {
                return 1f; // 최대값 반환
            }

            return value; // 이미 유효 범위인 값 반환
        }
    }
}
