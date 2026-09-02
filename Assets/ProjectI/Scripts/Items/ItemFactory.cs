using ProjectI.Economy; // RecoverableValue 복구 기능 사용
using UnityEngine; // Prefab 생성 기능 사용

namespace ProjectI.Items // 프로젝트 아이템 데이터 네임스페이스
{
    public static class ItemFactory // 일차 스냅샷 복구 전용 WorldItem 생성기
    {
        public static WorldItem SpawnForRecovery(ItemInstanceData data) // 저장 데이터에서 실제 WorldItem 생성
        {
            if (data == null || string.IsNullOrWhiteSpace(data.itemId)) // 복구 데이터 유효성 확인
            {
                return null; // 종류 ID가 없으면 생성 중단
            }

            ItemDefinition definition = ItemRegistry.Find(data.itemId); // ItemId 기반 정의 조회

            if (definition == null || definition.RecoveryPrefab == null) // 복구 Prefab 존재 여부 확인
            {
                Debug.LogError($"[Project I] 스냅샷 아이템 복구 실패 / ItemId={data.itemId} / RecoveryPrefab 누락"); // 복구 실패 원인 출력
                return null; // 생성 불가 반환
            }

            GameObject instance = Object.Instantiate(definition.RecoveryPrefab); // 저장 전 사용하던 종류의 Prefab 생성
            WorldItem item = instance.GetComponent<WorldItem>(); // 생성된 실제 WorldItem 조회

            if (item == null) // 복구 Prefab 구성 오류 확인
            {
                Object.Destroy(instance); // 잘못 생성된 오브젝트 제거
                Debug.LogError($"[Project I] 스냅샷 아이템 복구 실패 / WorldItem 누락 / ItemId={data.itemId}"); // 오류 로그 출력
                return null; // 생성 실패 반환
            }

            WorldItemIdentity identity = instance.GetComponent<WorldItemIdentity>(); // 생성된 식별자 조회

            if (identity == null) // 이전 Prefab이라 식별자가 없는지 확인
            {
                identity = instance.AddComponent<WorldItemIdentity>(); // 복구 대상에 식별자 추가
            }

            identity.RestoreIdentity(definition, data.instanceId); // 저장된 동일 InstanceId 복원
            RecoverableValue recoverable = instance.GetComponent<RecoverableValue>(); // 회수품 가격 컴포넌트 조회

            if (recoverable != null) // 가격 데이터가 있는 아이템인지 확인
            {
                recoverable.Configure(data.value); // 저장된 확정 가치 복원

                if (data.isSold) // 판매 완료 상태 여부 확인
                {
                    recoverable.MarkSold(); // 판매 완료 플래그 복원
                }
            }

            instance.name = string.IsNullOrWhiteSpace(data.displayName) ? definition.DisplayName : data.displayName; // 복구 오브젝트 이름 정리
            return item; // 생성된 실제 아이템 반환
        }
    }
}
