using System;
using System.Collections;
using ProjectI.Audio;
using ProjectI.Interaction;
using ProjectI.Net;
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
        public string Prompt => BuildPrompt();
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

            if (NetworkSession.IsGuest)
            {
                NetWorldState.RequestTravel(); // 37일차: 참가자는 방장에게 출발 요청 (방장이 확인 후 모두에게 종소리)
                return;
            }

            string blockReason = PersistentMapLoader.Instance == null ? null : PersistentMapLoader.Instance.GetTravelBlockReason();

            if (blockReason != null)
            {
                Debug.Log($"[Project I] 마차 종 / {blockReason}", this);
                SoundPlayer.PlayAt(SoundId.UiDenied, transform.position, 0.5f);
                return;
            }

            TravelRequested?.Invoke();

            if (NetworkSession.IsHost)
            {
                NetWorldState.BroadcastBell(); // 37일차: 모두에게 종소리·연출
                return;
            }

            PlayRemoteRing();
        }

        public void PlayRemoteRing()
        {
            if (isRinging || !isActiveAndEnabled)
            {
                return;
            }

            SoundPlayer.PlayAt(SoundId.WagonBell, transform.position, 1f, 0.02f, 45f);
            StartCoroutine(PlayRingAnimation());
        }

        private string BuildPrompt()
        {
            if (isRinging)
            {
                return "종이 울리는 중";
            }

            PersistentMapLoader loader = PersistentMapLoader.Instance;

            if (loader == null)
            {
                return "마차 종 울리기";
            }

            string blockReason = loader.GetTravelBlockReason();

            if (blockReason != null)
            {
                return blockReason;
            }

            return loader.CurrentDestination == TravelDestination.Office ? "마차 종 울리기 — 원정 출발" : "마차 종 울리기 — 사무소로 귀환";
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
