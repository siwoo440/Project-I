using System; // 직렬화 기능 사용
using UnityEngine; // Vector3와 Quaternion 직렬화 사용

namespace ProjectI.Items // 프로젝트 아이템 데이터 네임스페이스
{
    public enum SnapshotItemLocation // 스냅샷 복구 시 아이템이 돌아갈 위치 종류
    {
        World = 0, // 현재 환경 맵 바닥에 존재
        WagonCargo = 1, // Persistent 마차 CargoArea에 적재
        PlayerInventory = 2, // 플레이어 빠른 슬롯에 보관
        OfficeStorage = 3 // 사무소 보관 단상에 보관
    }

    [Serializable] // JsonUtility 직렬화 허용
    public sealed class ItemInstanceData // 개별 아이템 하나의 일차 스냅샷 데이터
    {
        public string instanceId; // 개별 아이템 고유 ID
        public string itemId; // ItemDefinition 조회용 종류 ID
        public string displayName; // 로그·복구 확인용 이름
        public int value; // 회수품 가격
        public bool isSold; // 판매 완료 여부
        public SnapshotItemLocation location; // 복구 위치 종류
        public string sceneName; // World 위치일 때 원래 환경 씬 이름
        public int slotIndex = -1; // 빠른 슬롯 위치
        public string storageKey; // 사무소 단상 식별 경로
        public Vector3 position; // World 또는 Wagon 로컬 위치
        public Quaternion rotation = Quaternion.identity; // World 또는 Wagon 로컬 회전
    }
}
