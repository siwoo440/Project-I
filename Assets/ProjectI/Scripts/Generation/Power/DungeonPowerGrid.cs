using System; // 이벤트 사용
using System.Collections.Generic; // 목록 사용
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    public sealed class DungeonPowerGrid : MonoBehaviour // 던전 전체 전력망 (발전기 1대 + 배전 구역 여러 개)
    {
        [SerializeField] private bool plantRunning = true; // 발전기 가동 여부
        private readonly List<DungeonPowerZone> zones = new List<DungeonPowerZone>(); // 배전 구역

        public bool PlantRunning => plantRunning; // 발전기 상태 공개
        public IReadOnlyList<DungeonPowerZone> Zones => zones; // 구역 공개
        public event Action<DungeonPowerGrid> PlantChanged; // 발전기 상태 변화

        public int PoweredZoneCount // 전기가 들어오는 구역 수
        {
            get
            {
                int count = 0; // 집계

                foreach (DungeonPowerZone zone in zones) // 구역 순회
                {
                    count += zone != null && zone.HasPower ? 1 : 0; // 집계
                }

                return count; // 반환
            }
        }

        public DungeonPowerZone CreateZone(int id, bool breakerOn) // 구역 생성
        {
            GameObject zoneObject = new GameObject($"PowerZone_{id:00}"); // 오브젝트
            zoneObject.transform.SetParent(transform, false); // 전력망 아래
            DungeonPowerZone zone = zoneObject.AddComponent<DungeonPowerZone>(); // 구역
            zone.Configure(this, id, breakerOn); // 구성
            zones.Add(zone); // 등록
            return zone; // 반환
        }

        public DungeonPowerZone Zone(int id) // 번호로 구역 조회
        {
            foreach (DungeonPowerZone zone in zones) // 구역 순회
            {
                if (zone != null && zone.ZoneId == id) // 일치
                {
                    return zone; // 반환
                }
            }

            return null; // 없음
        }

        public void SetPlant(bool running) // 발전기 기동·정지
        {
            if (plantRunning == running) // 변화 없음
            {
                return; // 종료
            }

            plantRunning = running; // 상태
            RefreshAll(); // 전 구역 반영
            PlantChanged?.Invoke(this); // 알림
        }

        public void RefreshAll() // 모든 구역 반영
        {
            foreach (DungeonPowerZone zone in zones) // 구역 순회
            {
                if (zone != null) // 확인
                {
                    zone.Refresh(); // 반영
                }
            }
        }
    }
}
