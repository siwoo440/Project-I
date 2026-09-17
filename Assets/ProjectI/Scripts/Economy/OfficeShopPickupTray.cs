using System.Collections.Generic; // 목록 사용
using ProjectI.Items; // WorldItem 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 소속 이동

namespace ProjectI.Economy // 사무소 경제 기능 네임스페이스
{
    [RequireComponent(typeof(BoxCollider))] // 윗면 범위
    public sealed class OfficeShopPickupTray : MonoBehaviour // 산 물건이 놓이는 수령대 (33일차)
    {
        private const float SlotSpacing = 0.3f; // 자리 간격
        private const float SlotEdgeMargin = 0.14f; // 가장자리 여백
        private const float OccupiedRadius = 0.16f; // 자리 사용 판정 반경
        private const float ZoneHeight = 1.0f; // 윗면 위 인식 높이
        private const float DropHeight = 0.1f; // 윗면에서 띄우는 높이

        public List<WorldItem> CollectItems() // 수령대 위 물건
        {
            return OfficeSurfaceSlots.CollectItems(transform, ZoneHeight, null); // 공용 계산
        }

        public int FreeSlotCount(int wanted) // 비어 있는 자리 수 (최대 wanted)
        {
            return OfficeSurfaceSlots.FindFreeSlots(transform, CollectItems(), wanted, SlotSpacing, SlotEdgeMargin, OccupiedRadius).Count; // 계산
        }

        public List<WorldItem> Deliver(ItemDefinition definition, int quantity) // 산 물건을 수령대 위에 생성
        {
            List<WorldItem> delivered = new List<WorldItem>(); // 결과

            if (definition == null || definition.RecoveryPrefab == null || quantity <= 0) // 확인
            {
                return delivered; // 빈 목록
            }

            List<Vector3> slots = OfficeSurfaceSlots.FindFreeSlots(transform, CollectItems(), quantity, SlotSpacing, SlotEdgeMargin, OccupiedRadius); // 자리
            Quaternion rotation = Quaternion.Euler(0f, transform.eulerAngles.y + 90f, 0f); // 긴 물건이 수령대 폭 방향으로 눕도록

            foreach (Vector3 slot in slots) // 자리마다 하나
            {
                GameObject instance = Instantiate(definition.RecoveryPrefab, slot + (Vector3.up * DropHeight), rotation); // 생성
                SceneManager.MoveGameObjectToScene(instance, gameObject.scene); // 사무소 씬 소속 (사무소 물건으로 저장·보관)
                instance.name = definition.DisplayName; // 이름
                WorldItemIdentity identity = instance.GetComponent<WorldItemIdentity>(); // 식별자

                if (identity == null) // 이전 프리팹 대비
                {
                    identity = instance.AddComponent<WorldItemIdentity>(); // 추가
                }

                identity.Configure(definition, string.Empty); // 새 개별 ID
                WorldItem item = instance.GetComponent<WorldItem>(); // WorldItem

                if (item != null) // 확인
                {
                    ProjectI.Net.NetItemSync.NotifySpawned(item); // 협동: 모두의 수령대에 생성
                    delivered.Add(item); // 등록
                }
            }

            return delivered; // 반환
        }
    }
}
