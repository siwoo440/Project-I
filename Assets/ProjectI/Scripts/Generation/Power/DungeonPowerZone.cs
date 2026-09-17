using System; // 이벤트 사용
using System.Collections.Generic; // 목록 사용
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    public sealed class DungeonPowerZone : MonoBehaviour // 배전 구역 하나 (차단기 하나가 담당하는 모듈 묶음)
    {
        [SerializeField] private int zoneId; // 구역 번호
        [SerializeField] private bool breakerOn = true; // 차단기 상태
        private DungeonPowerGrid grid; // 소속 전력망
        private readonly List<DungeonPowerConsumer> consumers = new List<DungeonPowerConsumer>(); // 이 구역에서 전기를 쓰는 것

        public int ZoneId => zoneId; // 번호 공개
        public bool BreakerOn => breakerOn; // 차단기 상태 공개
        public bool HasPower => breakerOn && grid != null && grid.PlantRunning; // 실제로 전기가 들어오는지 (발전기 + 차단기 둘 다)
        public int ConsumerCount => consumers.Count; // 소비처 수 공개
        public event Action<DungeonPowerZone> PowerChanged; // 전력 상태 변화

        public void Configure(DungeonPowerGrid owner, int id, bool startOn) // 구성 (생성기에서 호출)
        {
            grid = owner; // 전력망
            zoneId = id; // 번호
            breakerOn = startOn; // 시작 상태
        }

        public void Register(DungeonPowerConsumer consumer) // 소비처 등록
        {
            if (consumer != null && !consumers.Contains(consumer)) // 중복 확인
            {
                consumers.Add(consumer); // 등록
                consumer.ApplyPower(HasPower); // 현재 상태 반영
            }
        }

        public void SetBreaker(bool on) // 차단기 올리기·내리기
        {
            if (breakerOn == on) // 변화 없음
            {
                return; // 종료
            }

            breakerOn = on; // 상태
            Refresh(); // 반영
        }

        public void Refresh() // 현재 전력 상태를 소비처에 반영
        {
            bool powered = HasPower; // 상태

            foreach (DungeonPowerConsumer consumer in consumers) // 소비처 순회
            {
                if (consumer != null) // 확인
                {
                    consumer.ApplyPower(powered); // 반영
                }
            }

            PowerChanged?.Invoke(this); // 알림
        }
    }
}
