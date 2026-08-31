using System; // 전역 소음 이벤트 기능 참조
using UnityEngine; // GameObject와 월드 위치 기능 참조

namespace ProjectI.Monsters // 몬스터 공통 AI 네임스페이스
{
    public enum MonsterNoiseKind // 몬스터 청각이 구분하는 소음 종류
    {
        Footstep = 0, // 플레이어 걷기·달리기 소음
        Weapon = 1, // 총·석궁·근접 무기 소음
        Impact = 2, // 환경 충돌 소음
        Special = 3 // 향후 특수 이벤트용 소음
    }

    public readonly struct MonsterNoiseEvent // 한 번 발생한 월드 소음 데이터
    {
        public MonsterNoiseEvent(GameObject source, Vector3 position, float radius, float loudness, MonsterNoiseKind kind, string label) // 소음 데이터 생성
        {
            Source = source; // 소음을 발생시킨 오브젝트 저장
            Position = position; // 소음 월드 위치 저장
            Radius = Mathf.Max(0f, radius); // 소음 전달 반경 음수 방지
            Loudness = Mathf.Clamp01(loudness); // 소음 강도 0~1 범위 저장
            Kind = kind; // 소음 종류 저장
            Label = string.IsNullOrWhiteSpace(label) ? kind.ToString() : label; // 진단용 소음 이름 저장
        }

        public GameObject Source { get; } // 소음 발생 오브젝트 공개
        public Vector3 Position { get; } // 소음 월드 위치 공개
        public float Radius { get; } // 소음 전달 반경 공개
        public float Loudness { get; } // 소음 강도 공개
        public MonsterNoiseKind Kind { get; } // 소음 종류 공개
        public string Label { get; } // 진단용 소음 이름 공개
    }

    public static class MonsterNoiseSystem // 몬스터 청각이 구독하는 공통 소음 이벤트 허브
    {
        public static event Action<MonsterNoiseEvent> NoiseEmitted; // 새로운 소음 발생 이벤트

        public static MonsterNoiseEvent LastNoise { get; private set; } // F1 진단용 마지막 소음 공개
        public static bool HasNoise { get; private set; } // 마지막 소음 존재 여부 공개

        public static void Emit(GameObject source, Vector3 position, float radius, float loudness, MonsterNoiseKind kind, string label) // 월드에 소음 이벤트 발생
        {
            if (radius <= 0f || loudness <= 0f) // 실제 전달할 소음인지 확인
            {
                return; // 무음 이벤트 발생 방지
            }

            MonsterNoiseEvent noise = new MonsterNoiseEvent(source, position, radius, loudness, kind, label); // 공통 소음 데이터 생성
            LastNoise = noise; // 마지막 소음 진단 데이터 저장
            HasNoise = true; // 마지막 소음 존재 상태 활성화
            NoiseEmitted?.Invoke(noise); // 구독 중인 모든 MonsterSensor에 즉시 전달
        }
    }
}
