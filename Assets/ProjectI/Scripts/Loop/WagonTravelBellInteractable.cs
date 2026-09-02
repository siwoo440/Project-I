using System;
using System.Collections;
using ProjectI.Interaction;
using UnityEngine;

namespace ProjectI.Loop
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class WagonTravelBellInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform bellPivot;
        [SerializeField] private Transform rope;
        [SerializeField] private float ringDuration = 0.65f;
        [SerializeField] private float bellAngle = 18f;
        [SerializeField] private float ropeTravel = 0.12f;
        private bool isRinging;
        private Quaternion bellRestRotation;
        private Vector3 ropeRestPosition;

        public event Action TravelRequested;
        public string Prompt => isRinging ? "종이 울리는 중" : "마차 종 울리기";
        public InteractionType InteractionType => InteractionType.Press;
        public float HoldDuration => 0f;
        public bool IsRinging => isRinging;

        private void Awake()
        {
            CacheRestPose();
        }

        public void Configure(Transform targetBellPivot, Transform targetRope)
        {
            bellPivot = targetBellPivot;
            rope = targetRope;
            CacheRestPose();
        }

        public bool CanInteract(PlayerInteractor interactor)
        {
            return !isRinging;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (isRinging)
            {
                return;
            }

            TravelRequested?.Invoke();
            StartCoroutine(PlayRingAnimation());
        }

        private IEnumerator PlayRingAnimation()
        {
            isRinging = true;
            CacheRestPose();
            float duration = Mathf.Max(0.1f, ringDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float wave = Mathf.Sin(normalized * Mathf.PI * 4f) * (1f - normalized);

                if (bellPivot != null)
                {
                    bellPivot.localRotation = bellRestRotation * Quaternion.Euler(0f, 0f, wave * bellAngle);
                }

                if (rope != null)
                {
                    rope.localPosition = ropeRestPosition + (Vector3.down * Mathf.Abs(wave) * ropeTravel);
                }

                yield return null;
            }

            if (bellPivot != null)
            {
                bellPivot.localRotation = bellRestRotation;
            }

            if (rope != null)
            {
                rope.localPosition = ropeRestPosition;
            }

            isRinging = false;
            Debug.Log("[Project I] 24일차 1단계 / 마차 이동 종 작동 확인", this);
        }

        private void CacheRestPose()
        {
            if (bellPivot != null)
            {
                bellRestRotation = bellPivot.localRotation;
            }

            if (rope != null)
            {
                ropeRestPosition = rope.localPosition;
            }
        }

        private void OnValidate()
        {
            ringDuration = Mathf.Max(0.1f, ringDuration);
            bellAngle = Mathf.Clamp(bellAngle, 1f, 45f);
            ropeTravel = Mathf.Clamp(ropeTravel, 0.01f, 0.35f);
        }
    }
}
