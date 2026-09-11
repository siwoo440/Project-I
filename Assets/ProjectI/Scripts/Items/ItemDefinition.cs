using System; // 빈 배열 기능 사용
using System.Collections.Generic; // 읽기 전용 목록 기능 사용
using UnityEngine; // 유니티 ScriptableObject와 Prefab 참조 기능 사용

namespace ProjectI.Items // 프로젝트 아이템 데이터 네임스페이스
{
    [CreateAssetMenu(fileName = "ItemDefinition", menuName = "Project I/Items/Item Definition")] // 아이템 정의 에셋 생성 메뉴 등록
    public sealed class ItemDefinition : ScriptableObject // 저장·복구용 아이템 종류 데이터
    {
        [SerializeField] private string itemId; // 저장 파일에서 사용할 고정 아이템 종류 ID (예: weapon.iron_sword)
        [SerializeField] private string displayName; // 사람이 확인할 아이템 표시 이름
        [SerializeField] private GameObject recoveryPrefab; // 스냅샷 복구 시 생성할 원본 Prefab
        [SerializeField] private string[] legacyIds = Array.Empty<string>(); // 이전 저장 파일 호환용 과거 ID (예: LEGACY_검)

        public string ItemId => itemId; // 아이템 종류 ID 공개
        public string DisplayName => displayName; // 표시 이름 공개
        public GameObject RecoveryPrefab => recoveryPrefab; // 복구 Prefab 공개
        public IReadOnlyList<string> LegacyIds => legacyIds ?? Array.Empty<string>(); // 과거 ID 목록 공개

        public void Configure(string targetItemId, string targetDisplayName, GameObject targetRecoveryPrefab) // Editor 자동 생성용 정의 값 설정
        {
            itemId = targetItemId ?? string.Empty; // null 없는 안정 ID 저장
            displayName = targetDisplayName ?? string.Empty; // null 없는 표시 이름 저장
            recoveryPrefab = targetRecoveryPrefab; // 복구 Prefab 참조 저장
        }

        public bool Matches(string candidateId) // 현재 ID 또는 과거 ID와 일치하는지 확인
        {
            if (string.IsNullOrWhiteSpace(candidateId)) // 빈 ID 확인
            {
                return false; // 일치하지 않음
            }

            if (string.Equals(itemId, candidateId, StringComparison.Ordinal)) // 현재 고정 ID 비교
            {
                return true; // 일치
            }

            return Array.IndexOf(legacyIds ?? Array.Empty<string>(), candidateId) >= 0; // 과거 ID 비교
        }
    }
}
