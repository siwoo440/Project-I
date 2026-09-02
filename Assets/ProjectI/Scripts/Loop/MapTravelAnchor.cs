using UnityEngine;

namespace ProjectI.Loop
{
    [DisallowMultipleComponent]
    public sealed class MapTravelAnchor : MonoBehaviour
    {
        [SerializeField] private TravelDestination destination;
        [SerializeField] private Transform entryPoint;
        [SerializeField] private Transform stopPoint;

        public TravelDestination Destination => destination;
        public Transform EntryPoint => entryPoint;
        public Transform StopPoint => stopPoint;
        public bool IsConfigured => entryPoint != null && stopPoint != null;

        public void Configure(TravelDestination targetDestination, Transform targetEntryPoint, Transform targetStopPoint)
        {
            destination = targetDestination;
            entryPoint = targetEntryPoint;
            stopPoint = targetStopPoint;
        }
    }
}
