using ProjectI.Brightness; // 밝기 광원과 영역 기능 참조
using ProjectI.Lighting; // 고정·휴대 조명 표시 이름 참조
using UnityEngine; // 벡터와 Transform 기능 참조

namespace ProjectI.Diagnostics // 프로젝트 공통 디버그 기능 네임스페이스
{
    public enum LightContributionStatus // 현재 플레이어 위치에서 광원이 제외되거나 기여하는 이유
    {
        Active, // 현재 공간과 거리·방향 조건을 만족해 실제 밝기에 기여
        Disabled, // 광원 자체가 꺼져 있음
        OutOfRange, // 현재 위치가 광원 범위 밖에 있음
        DifferentArea, // 현재 플레이어가 광원과 다른 Outdoor/Indoor 공간에 있음
        OutsideCone // 방향성 광원의 조사 각도 밖에 있음
    }

    public readonly struct LightContributionDebugInfo // 광원 하나의 현재 플레이어 위치 기준 계산 결과
    {
        public LightContributionDebugInfo(BrightnessSource source, string displayName, float contribution, float distance, LightContributionStatus status) // 광원 디버그 결과 생성
        {
            Source = source; // 원본 BrightnessSource 저장
            DisplayName = displayName; // 디버그 표시 이름 저장
            Contribution = contribution; // 현재 위치의 실제 기여 밝기 저장
            Distance = distance; // 광원 시작점과 플레이어 사이 거리 저장
            Status = status; // 현재 기여 또는 제외 이유 저장
        }

        public BrightnessSource Source { get; } // 원본 게임용 광원
        public string DisplayName { get; } // F1 페이지와 월드 라벨용 이름
        public float Contribution { get; } // 현재 플레이어 위치에 실제로 더해지는 밝기
        public float Distance { get; } // 현재 플레이어까지의 거리
        public LightContributionStatus Status { get; } // 현재 계산 상태
    }

    public static class BrightnessDebugUtility // F1 광원 라벨과 계산 탭이 공유하는 진단 계산
    {
        public static LightContributionDebugInfo Evaluate(BrightnessSource source, Vector3 samplePosition, IndoorBrightnessArea targetArea) // 광원 하나가 현재 위치에 주는 실제 기여도와 제외 이유 계산
        {
            if (source == null) // 유효한 광원인지 확인
            {
                return new LightContributionDebugInfo(null, "Missing Source", 0f, 0f, LightContributionStatus.Disabled); // 누락 광원 안전 결과 반환
            }

            string displayName = GetDisplayName(source); // 디버그에서 읽기 쉬운 광원 이름 계산
            Vector3 emissionPosition = GetEmissionPosition(source); // 실제 화면 Light와 동일한 광원 시작점 조회
            float distance = Vector3.Distance(emissionPosition, samplePosition); // 광원과 현재 플레이어 위치 사이 거리 계산

            if (!source.SourceEnabled || !source.isActiveAndEnabled) // 논리 또는 컴포넌트 광원이 꺼졌는지 확인
            {
                return new LightContributionDebugInfo(source, displayName, 0f, distance, LightContributionStatus.Disabled); // 꺼진 광원 결과 반환
            }

            if (source.GetEffectiveArea() != targetArea) // 현재 플레이어와 광원이 같은 Outdoor/Indoor 영역인지 확인
            {
                return new LightContributionDebugInfo(source, displayName, 0f, distance, LightContributionStatus.DifferentArea); // 다른 공간 광원 제외 결과 반환
            }

            if (distance >= source.Range) // 현재 위치가 광원 최대 범위 밖인지 확인
            {
                return new LightContributionDebugInfo(source, displayName, 0f, distance, LightContributionStatus.OutOfRange); // 범위 밖 광원 제외 결과 반환
            }

            if (source.EmissionShape == BrightnessEmissionShape.Cone && IsOutsideCone(source, emissionPosition, samplePosition)) // 방향성 광원의 빔 밖인지 확인
            {
                return new LightContributionDebugInfo(source, displayName, 0f, distance, LightContributionStatus.OutsideCone); // 원뿔 밖 광원 제외 결과 반환
            }

            float contribution = source.GetContribution(samplePosition); // 실제 BrightnessSource와 같은 계산을 사용해 현재 밝기 기여도 조회
            return new LightContributionDebugInfo(source, displayName, contribution, distance, LightContributionStatus.Active); // 정상 기여 광원 결과 반환
        }

        public static Vector3 GetEmissionPosition(BrightnessSource source) // 화면 표시와 게임 계산이 사용하는 실제 광원 시작점 반환
        {
            if (source == null) // 광원 누락 여부 확인
            {
                return Vector3.zero; // 안전한 기본 위치 반환
            }

            Light visualLight = source.VisualLight; // 연결된 실제 Unity Light 조회
            return visualLight == null ? source.transform.position : visualLight.transform.position; // 화면 Light가 있으면 그 위치를 우선 반환
        }

        public static string GetDisplayName(BrightnessSource source) // 광원 하나의 읽기 쉬운 디버그 이름 생성
        {
            if (source == null) // 광원 누락 여부 확인
            {
                return "Missing Source"; // 안전한 누락 이름 반환
            }

            FixedLightController fixedLight = source.GetComponentInParent<FixedLightController>(); // 고정 광원 컨트롤러가 부모에 있는지 확인

            if (fixedLight != null) // 고정 환경 광원인지 확인
            {
                return fixedLight.DisplayName; // 벽 횃불 또는 화로 표시 이름 반환
            }

            PortableLightItem portableLight = source.GetComponentInParent<PortableLightItem>(); // 휴대 조명 컨트롤러가 부모에 있는지 확인

            if (portableLight != null) // 횃불 또는 랜턴인지 확인
            {
                string itemName = portableLight.WorldItem == null ? portableLight.name : portableLight.WorldItem.DisplayName; // 월드 아이템 표시 이름 결정

                if (source.transform == portableLight.transform) // 휴대 조명 루트의 대표 광원인지 확인
                {
                    string suffix = source.EmissionShape == BrightnessEmissionShape.Cone ? " Beam" : string.Empty; // 방향성 주 빔이면 Beam 표기 추가
                    return $"{itemName}{suffix}"; // 대표 휴대 광원 이름 반환
                }

                return $"{itemName} {source.gameObject.name}"; // 랜턴 주변 보조광처럼 자식 광원 이름까지 포함
            }

            return source.gameObject.name; // 일반 BrightnessSource는 GameObject 이름 그대로 반환
        }

        private static bool IsOutsideCone(BrightnessSource source, Vector3 emissionPosition, Vector3 samplePosition) // 방향성 광원의 현재 위치 포함 여부 판정
        {
            Vector3 toSample = samplePosition - emissionPosition; // 광원에서 플레이어로 향하는 방향 계산

            if (toSample.sqrMagnitude <= 0.0001f) // 플레이어가 사실상 광원 중심에 있는지 확인
            {
                return false; // 광원 중심은 원뿔 내부로 처리
            }

            Light visualLight = source.VisualLight; // 실제 Spot Light 참조 조회
            Vector3 forward = visualLight == null ? source.transform.forward : visualLight.transform.forward; // 화면 빔과 동일한 전방 방향 결정
            float angle = Vector3.Angle(forward, toSample.normalized); // 빔 중심축과 플레이어 방향 사이 각도 계산
            return angle >= source.ConeAngle * 0.5f; // 전체 Cone 각도의 절반보다 바깥이면 조사 영역 밖으로 판정
        }
    }
}
