using System.Collections.Generic; // 목록 사용
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    public sealed class DungeonPowerConsumer : MonoBehaviour // 전기가 끊기면 꺼지는 것 (조명·승강기 등)
    {
        [SerializeField] private Light[] poweredLights = System.Array.Empty<Light>(); // 전기로 켜지는 조명
        [SerializeField] private Light emergencyLight; // 정전 시에만 켜지는 비상등
        [SerializeField] private bool hasPower = true; // 현재 전력 상태

        public bool HasPower => hasPower; // 상태 공개
        public int PoweredLightCount => poweredLights.Length; // 조명 수 공개
        public Light EmergencyLight => emergencyLight; // 비상등 공개

        public void Configure(IReadOnlyList<Light> lights, Light emergency) // 구성 (생성기에서 호출)
        {
            poweredLights = new Light[lights == null ? 0 : lights.Count]; // 배열

            for (int index = 0; index < poweredLights.Length; index++) // 복사
            {
                poweredLights[index] = lights[index]; // 등록
            }

            emergencyLight = emergency; // 비상등
        }

        public void ApplyPower(bool powered) // 전력 상태 반영
        {
            hasPower = powered; // 상태

            foreach (Light light in poweredLights) // 조명 순회
            {
                if (light != null) // 확인
                {
                    light.enabled = powered; // 켜기·끄기
                }
            }

            if (emergencyLight != null) // 비상등은 반대로
            {
                emergencyLight.enabled = !powered; // 정전일 때만 켜짐
            }
        }
    }
}
