using UnityEngine;

namespace ProjectI.Loop
{
    [DisallowMultipleComponent]
    public sealed class MapTravelAnchor : MonoBehaviour
    {
        [SerializeField] private TravelDestination destination;
        [SerializeField] private Transform entryPoint;
        [SerializeField] private Transform stopPoint;
        [SerializeField] private Transform playerSpawnPoint; // 34일차: 게임 시작·불러오기 때 플레이어를 세울 위치 (없으면 마차 기준 상대 위치 유지)

        public TravelDestination Destination => destination;
        public Transform EntryPoint => entryPoint;
        public Transform StopPoint => stopPoint;
        public Transform PlayerSpawnPoint => playerSpawnPoint;
        public bool IsConfigured => entryPoint != null && stopPoint != null;

        public void Configure(TravelDestination targetDestination, Transform targetEntryPoint, Transform targetStopPoint)
        {
            destination = targetDestination;
            entryPoint = targetEntryPoint;
            stopPoint = targetStopPoint;
        }

        public void ConfigurePlayerSpawn(Transform targetSpawnPoint)
        {
            playerSpawnPoint = targetSpawnPoint;
        }
    }
}
