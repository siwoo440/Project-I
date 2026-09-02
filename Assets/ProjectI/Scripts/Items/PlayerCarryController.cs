using ProjectI.Player;
using UnityEngine;

namespace ProjectI.Items
{
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class PlayerCarryController : MonoBehaviour
    {
        [SerializeField] private Transform viewTransform;
        [SerializeField] private Transform oneHandCarryPoint;
        [SerializeField] private Transform twoHandCarryPoint;
        [SerializeField] private float obstructionPadding = 0.08f;
        [SerializeField] private float dropDistance = 0.60f;
        [SerializeField] private float throwVelocityChange = 7f;
        [SerializeField] private LayerMask obstructionMask = ~0;
        private WorldItem heldItem;

        public bool HasItem => heldItem != null;
        public WorldItem HeldItem => heldItem;

        private void Awake()
        {
            if (viewTransform == null)
            {
                Camera childCamera = GetComponentInChildren<Camera>(true);
                viewTransform = childCamera == null ? null : childCamera.transform;
            }

            ResolveCarryPoints();
        }

        private void LateUpdate()
        {
            if (heldItem == null)
            {
                return;
            }

            ResolveCarryPoints();
            Transform carryPoint = GetCarryPoint(heldItem.CarryType);
            heldItem.SnapToCarryPoint(carryPoint);
        }

        public void Configure(Transform view, Transform oneHandPoint, Transform twoHandPoint, PlayerInputReader reader)
        {
            viewTransform = view;
            oneHandCarryPoint = oneHandPoint;
            twoHandCarryPoint = twoHandPoint;
        }

        public bool TryPickup(WorldItem item)
        {
            PlayerInventory inventory = GetComponent<PlayerInventory>();
            return inventory != null && inventory.TryPickup(item);
        }

        public bool EquipItem(WorldItem item)
        {
            if (item == null || heldItem != null || viewTransform == null)
            {
                return false;
            }

            ResolveCarryPoints();
            Transform carryPoint = GetCarryPoint(item.CarryType);

            if (carryPoint == null)
            {
                return false;
            }

            heldItem = item;
            heldItem.IgnoreCollisionsWith(transform);
            heldItem.BeginCarry(carryPoint);
            return true;
        }

        public void HolsterHeldItem(Transform storageRoot)
        {
            if (heldItem == null || storageRoot == null)
            {
                return;
            }

            WorldItem itemToStore = heldItem;
            heldItem = null;
            itemToStore.Store(storageRoot);
        }

        public WorldItem DropHeldItem()
        {
            if (heldItem == null || viewTransform == null)
            {
                return null;
            }

            Vector3 releasePosition = CalculateReleasePosition(dropDistance);
            WorldItem itemToRelease = heldItem;
            Quaternion releaseRotation = ResolveDropRotation(itemToRelease);
            WorldItemDropProfile dropProfile = GetOrCreateDropProfile(itemToRelease);
            heldItem = null;
            itemToRelease.Release(releasePosition, releaseRotation, Vector3.zero);
            dropProfile?.ApplyDropStability();
            return itemToRelease;
        }

        public WorldItem ThrowHeldItem()
        {
            if (heldItem == null || viewTransform == null)
            {
                return null;
            }

            Vector3 releasePosition = CalculateReleasePosition(Mathf.Max(0.85f, dropDistance));
            Quaternion releaseRotation = heldItem.transform.rotation;
            Vector3 velocityChange = viewTransform.forward * throwVelocityChange;
            WorldItem itemToRelease = heldItem;
            WorldItemDropProfile dropProfile = GetOrCreateDropProfile(itemToRelease);
            heldItem = null;
            dropProfile?.RestoreThrowConstraints();
            itemToRelease.Release(releasePosition, releaseRotation, velocityChange);
            return itemToRelease;
        }

        private static WorldItemDropProfile GetOrCreateDropProfile(WorldItem item)
        {
            if (item == null)
            {
                return null;
            }

            WorldItemDropProfile profile = item.GetComponent<WorldItemDropProfile>();

            if (profile == null)
            {
                profile = item.gameObject.AddComponent<WorldItemDropProfile>();
            }

            return profile;
        }

        private Quaternion ResolveDropRotation(WorldItem item)
        {
            Vector3 flatForward = item == null ? Vector3.zero : Vector3.ProjectOnPlane(item.transform.forward, Vector3.up);

            if (flatForward.sqrMagnitude <= 0.0001f && viewTransform != null)
            {
                flatForward = Vector3.ProjectOnPlane(viewTransform.forward, Vector3.up);
            }

            if (flatForward.sqrMagnitude <= 0.0001f)
            {
                flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }

            if (flatForward.sqrMagnitude <= 0.0001f)
            {
                flatForward = Vector3.forward;
            }

            Quaternion horizontalRotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
            WorldItemDropProfile profile = item == null ? null : item.GetComponent<WorldItemDropProfile>();
            Vector3 dropEulerOffset = profile == null ? Vector3.zero : profile.DropEulerOffset;
            return horizontalRotation * Quaternion.Euler(dropEulerOffset);
        }

        private void ResolveCarryPoints()
        {
            if (viewTransform == null)
            {
                return;
            }

            if (oneHandCarryPoint == null)
            {
                oneHandCarryPoint = viewTransform.Find("OneHandCarryPoint");
            }

            if (twoHandCarryPoint == null)
            {
                twoHandCarryPoint = viewTransform.Find("TwoHandCarryPoint");
            }
        }

        private Transform GetCarryPoint(CarryType carryType)
        {
            return carryType == CarryType.TwoHand ? twoHandCarryPoint : oneHandCarryPoint;
        }

        private Vector3 CalculateReleasePosition(float distance)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(viewTransform.forward, Vector3.up).normalized;

            if (flatForward.sqrMagnitude <= 0.0001f)
            {
                flatForward = transform.forward;
            }

            Vector3 origin = transform.position + (Vector3.up * 0.35f);
            float radius = heldItem == null ? 0.18f : heldItem.CarryRadius;
            float safeDistance = Mathf.Max(0.35f, distance);
            RaycastHit[] hits = Physics.SphereCastAll(origin, radius, flatForward, safeDistance, obstructionMask, QueryTriggerInteraction.Ignore);

            foreach (RaycastHit hit in hits)
            {
                if (ShouldIgnoreHit(hit.collider))
                {
                    continue;
                }

                safeDistance = Mathf.Min(safeDistance, Mathf.Max(0.20f, hit.distance - obstructionPadding));
            }

            return origin + (flatForward * safeDistance);
        }

        private bool ShouldIgnoreHit(Collider hitCollider)
        {
            if (hitCollider == null)
            {
                return true;
            }

            Transform hitTransform = hitCollider.transform;

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                return true;
            }

            if (heldItem != null && (hitTransform == heldItem.transform || hitTransform.IsChildOf(heldItem.transform)))
            {
                return true;
            }

            if (hitCollider.GetComponentInParent<WorldItem>() != null)
            {
                return true;
            }

            return false;
        }

        private void OnValidate()
        {
            obstructionPadding = Mathf.Max(0.01f, obstructionPadding);
            dropDistance = Mathf.Max(0.6f, dropDistance);
            throwVelocityChange = Mathf.Max(0.1f, throwVelocityChange);
        }
    }
}
