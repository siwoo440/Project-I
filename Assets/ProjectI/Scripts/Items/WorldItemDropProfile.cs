using UnityEngine;

namespace ProjectI.Items
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class WorldItemDropProfile : MonoBehaviour
    {
        [SerializeField] private Vector3 dropEulerOffset = Vector3.zero;
        [SerializeField] private ItemStabilityMode stabilityMode = ItemStabilityMode.Free;
        private Rigidbody body;
        private RigidbodyConstraints originalConstraints;
        private bool capturedOriginalConstraints;

        public Vector3 DropEulerOffset => dropEulerOffset;
        public ItemStabilityMode StabilityMode => stabilityMode;

        private void Awake()
        {
            CaptureOriginalConstraints();
        }

        public void Configure(Vector3 eulerOffset, ItemStabilityMode mode)
        {
            dropEulerOffset = eulerOffset;
            stabilityMode = mode;
        }

        public void ApplyDropStability()
        {
            CaptureOriginalConstraints();

            if (body == null)
            {
                return;
            }

            RigidbodyConstraints positionConstraints = originalConstraints &
                (RigidbodyConstraints.FreezePositionX |
                 RigidbodyConstraints.FreezePositionY |
                 RigidbodyConstraints.FreezePositionZ);

            switch (stabilityMode)
            {
                case ItemStabilityMode.Upright:
                    body.constraints = positionConstraints |
                        RigidbodyConstraints.FreezeRotationX |
                        RigidbodyConstraints.FreezeRotationZ;
                    break;

                case ItemStabilityMode.FixedPose:
                    body.constraints = positionConstraints |
                        RigidbodyConstraints.FreezeRotationX |
                        RigidbodyConstraints.FreezeRotationY |
                        RigidbodyConstraints.FreezeRotationZ;
                    break;

                default:
                    body.constraints = originalConstraints;
                    break;
            }
        }

        public void RestoreThrowConstraints()
        {
            CaptureOriginalConstraints();

            if (body != null)
            {
                body.constraints = originalConstraints;
            }
        }

        private void CaptureOriginalConstraints()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (body == null || capturedOriginalConstraints)
            {
                return;
            }

            originalConstraints = body.constraints;
            capturedOriginalConstraints = true;
        }
    }
}
