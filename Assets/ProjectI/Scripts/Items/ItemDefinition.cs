using UnityEngine; // 유니티 ScriptableObject와 Prefab 참조 기능 사용

namespace ProjectI.Items // 프로젝트 아이템 데이터 네임스페이스
{
    [CreateAssetMenu(fileName = "ItemDefinition", menuName = "Project I/Items/Item Definition")] // 아이템 정의 에셋 생성 메뉴 등록
    public sealed class ItemDefinition : ScriptableObject // 저장·복구용 아이템 종류 데이터
    {
        [SerializeField] private string itemId; // 저장 파일에서 사용할 안정적인 아이템 종류 ID
        [SerializeField] private string displayName; // 사람이 확인할 아이템 표시 이름
        [SerializeField] private GameObject recoveryPrefab; // 스냅샷 복구 시 생성할 원본 Prefab

        public string ItemId => itemId; // 아이템 종류 ID 공개
        public string DisplayName => displayName; // 표시 이름 공개
        public GameObject RecoveryPrefab => recoveryPrefab; // 복구 Prefab 공개

        public void Configure(string targetItemId, string targetDisplayName, GameObject targetRecoveryPrefab) // Editor 자동 생성용 정의 값 설정
        {
            itemId = targetItemId ?? string.Empty; // null 없는 안정 ID 저장
            displayName = targetDisplayName ?? string.Empty; // null 없는 표시 이름 저장
            recoveryPrefab = targetRecoveryPrefab; // 복구 Prefab 참조 저장
        }
    }
}
