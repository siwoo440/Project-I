using System; // 직렬화 기능 참조
using UnityEngine; // 유니티 직렬화 기능 참조

namespace ProjectI.Items // 아이템 기능 네임스페이스
{
    [Serializable] // 인스펙터 직렬화 허용
    public sealed class QuickSlot // 빠른 슬롯 한 칸 데이터
    {
        [SerializeField] private WorldItem item; // 현재 슬롯에 저장된 월드 아이템

        public WorldItem Item => item; // 현재 아이템 반환
        public bool IsEmpty => item == null; // 슬롯 비어 있음 여부 반환

        public void SetItem(WorldItem value) // 슬롯에 아이템 저장
        {
            item = value; // 현재 슬롯 아이템 변경
        }

        public WorldItem Clear() // 슬롯 아이템 제거
        {
            WorldItem removedItem = item; // 제거 전 아이템 임시 저장
            item = null; // 슬롯 비우기
            return removedItem; // 제거된 아이템 반환
        }
    }
}
