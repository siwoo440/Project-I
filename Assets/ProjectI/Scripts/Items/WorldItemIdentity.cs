using System; // Guid 생성 기능 사용
using UnityEngine; // 유니티 컴포넌트 기능 사용

namespace ProjectI.Items // 프로젝트 아이템 데이터 네임스페이스
{
    [DisallowMultipleComponent] // 한 WorldItem에 고유 ID 컴포넌트 중복 방지
    [RequireComponent(typeof(WorldItem))] // 실제 WorldItem과 항상 함께 사용
    public sealed class WorldItemIdentity : MonoBehaviour // 저장·복구 전용 개별 아이템 식별자
    {
        [SerializeField] private ItemDefinition definition; // 아이템 종류 정의 참조
        [SerializeField] private string instanceId; // 실제 개별 아이템의 영구 고유 ID

        public ItemDefinition Definition => definition; // 아이템 정의 공개
        public string InstanceId => instanceId; // 개별 ID 공개
        public string ItemId => definition == null ? string.Empty : definition.ItemId; // 정의 기반 종류 ID 공개

        private void Awake() // 런타임 개별 ID 보장
        {
            EnsureInstanceId(); // 비어 있는 신규 생성 아이템에 GUID 부여
        }

        public void Configure(ItemDefinition targetDefinition, string targetInstanceId) // Editor 또는 복구 Factory용 설정
        {
            definition = targetDefinition; // 종류 정의 연결
            instanceId = targetInstanceId ?? string.Empty; // 지정된 개별 ID 저장
            EnsureInstanceId(); // 빈 ID이면 새 GUID 생성
        }

        public void ConfigureDefinitionTemplate(ItemDefinition targetDefinition) // Prefab 원본용 정의만 지정
        {
            definition = targetDefinition; // 복구 Prefab 종류 정의 연결
            instanceId = string.Empty; // Prefab에는 개별 인스턴스 ID를 저장하지 않음
        }

        public void RestoreIdentity(ItemDefinition targetDefinition, string restoredInstanceId) // 스냅샷 개별 ID 복원
        {
            definition = targetDefinition; // 저장된 종류 정의 연결
            instanceId = restoredInstanceId ?? string.Empty; // 저장된 개별 ID 복원
            EnsureInstanceId(); // 손상된 빈 ID만 안전하게 새 ID로 보정
        }

        public void EnsureInstanceId() // 개별 ID 유효성 보장
        {
            if (!string.IsNullOrWhiteSpace(instanceId)) // 이미 ID가 존재하는지 확인
            {
                return; // 기존 ID 유지
            }

            instanceId = Guid.NewGuid().ToString("N"); // 충돌 가능성이 매우 낮은 GUID 생성
        }
    }
}
