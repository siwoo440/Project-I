using System.Collections;
using ProjectI.Items;
using ProjectI.Persistence;
using ProjectI.Wagon;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectI.Loop
{
    [DisallowMultipleComponent]
    public sealed class PersistentMapLoader : MonoBehaviour
    {
        private const string OfficeSceneName = "01_Office";
        private const string TestDungeonSceneName = "02_TestDungeon";
        private static PersistentMapLoader instance;

        [SerializeField] private Transform wagonRoot;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField] private WagonCargoPersistence cargoPersistence;
        [SerializeField] private float fadeDuration = 0.75f;
        [SerializeField] private float arrivalDuration = 2.25f;
        [SerializeField] private TravelDestination initialDestination = TravelDestination.Office;
        private WagonTravelBellInteractable travelBell;
        private TravelDestination currentDestination;
        private bool isTransitioning;

        public static PersistentMapLoader Instance => instance;
        public TravelDestination CurrentDestination => currentDestination;
        public bool IsTransitioning => isTransitioning;
        public WagonCargoPersistence CargoPersistence => cargoPersistence;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            currentDestination = initialDestination;
            BindPersistentReferences();
            SetFadeImmediate(1f);
        }

        private IEnumerator Start()
        {
            yield return BootstrapInitialMap();
        }

        private void OnDestroy()
        {
            UnbindBell();

            if (instance == this)
            {
                instance = null;
            }
        }

        public void Configure(Transform targetWagonRoot, Transform targetPlayerRoot, CanvasGroup targetFadeGroup)
        {
            wagonRoot = targetWagonRoot;
            playerRoot = targetPlayerRoot;
            fadeGroup = targetFadeGroup;
            BindPersistentReferences();
        }

        public void ConfigureCargoPersistence(WagonCargoPersistence targetCargoPersistence)
        {
            cargoPersistence = targetCargoPersistence;
            BindPersistentReferences();
        }


        public void CaptureRuntimeOfficeState()
        {
            DailySnapshotService.Instance?.CaptureRuntimeOfficeState();
        }

        public void RestoreRuntimeOfficeState()
        {
            DailySnapshotService.Instance?.RestoreRuntimeOfficeState();
        }

        public IEnumerator LoadDestinationForRecovery(TravelDestination targetDestination)
        {
            if (isTransitioning)
            {
                yield break;
            }

            isTransitioning = true;
            SetFadeImmediate(1f);
            CaptureRuntimeOfficeState();
            string targetSceneName = GetSceneName(targetDestination);
            Scene targetScene = SceneManager.GetSceneByName(targetSceneName);

            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);

                if (loadOperation == null)
                {
                    Debug.LogError($"[Project I] Snapshot 복구용 맵 로드 실패 / Scene={targetSceneName}", this);
                    isTransitioning = false;
                    SetFadeImmediate(0f);
                    yield break;
                }

                yield return loadOperation;
                targetScene = SceneManager.GetSceneByName(targetSceneName);
            }

            MapTravelAnchor anchor = FindAnchor(targetScene, targetDestination);

            if (anchor == null || !anchor.IsConfigured || anchor.StopPoint == null)
            {
                Debug.LogError($"[Project I] Snapshot 복구용 WagonStopPoint 누락 / Scene={targetSceneName}", this);
                isTransitioning = false;
                SetFadeImmediate(0f);
                yield break;
            }

            if (targetScene.IsValid() && targetScene.isLoaded)
            {
                SceneManager.SetActiveScene(targetScene);
            }

            string otherSceneName = targetDestination == TravelDestination.Office ? TestDungeonSceneName : OfficeSceneName;
            Scene otherScene = SceneManager.GetSceneByName(otherSceneName);

            if (otherScene.IsValid() && otherScene.isLoaded)
            {
                AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(otherScene);

                if (unloadOperation != null)
                {
                    yield return unloadOperation;
                }
            }

            TeleportPersistentGroup(anchor.StopPoint.position, anchor.StopPoint.rotation);
            currentDestination = targetDestination;
            RestoreRuntimeOfficeState();
            BindBell();
            isTransitioning = false;
            SetFadeImmediate(0f);
            Debug.Log($"[Project I] Snapshot 복구용 환경 준비 완료 / Map={targetSceneName}", this);
        }

        private IEnumerator BootstrapInitialMap()
        {
            BindPersistentReferences();
            string initialSceneName = GetSceneName(initialDestination);
            Scene initialScene = SceneManager.GetSceneByName(initialSceneName);

            if (!initialScene.isLoaded)
            {
                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(initialSceneName, LoadSceneMode.Additive);

                if (loadOperation == null)
                {
                    Debug.LogError($"[Project I] 초기 맵 로드 실패 / Scene={initialSceneName}", this);
                    yield break;
                }

                yield return loadOperation;
                initialScene = SceneManager.GetSceneByName(initialSceneName);
            }

            if (!initialScene.IsValid() || !initialScene.isLoaded)
            {
                Debug.LogError($"[Project I] 초기 맵을 찾지 못했습니다 / Scene={initialSceneName}", this);
                yield break;
            }

            SceneManager.SetActiveScene(initialScene);
            MapTravelAnchor anchor = FindAnchor(initialScene, initialDestination);

            if (anchor != null && anchor.StopPoint != null)
            {
                TeleportPersistentGroup(anchor.StopPoint.position, anchor.StopPoint.rotation);
            }

            currentDestination = initialDestination;
            RestoreRuntimeOfficeState();
            BindBell();
            yield return FadeTo(0f, fadeDuration);
            Debug.Log($"[Project I] 24일차 2단계 초기 맵 준비 / Persistent + {initialSceneName}", this);
        }

        private void HandleTravelRequested()
        {
            if (isTransitioning)
            {
                return;
            }

            TravelDestination targetDestination = currentDestination == TravelDestination.Office
                ? TravelDestination.TestDungeon
                : TravelDestination.Office;
            StartCoroutine(TravelRoutine(targetDestination));
        }

        private IEnumerator TravelRoutine(TravelDestination targetDestination)
        {
            isTransitioning = true;
            yield return FadeTo(1f, fadeDuration);
            BindPersistentReferences();
            CaptureRuntimeOfficeState();
            cargoPersistence?.CaptureCargoForTravel();

            string previousSceneName = GetSceneName(currentDestination);
            string targetSceneName = GetSceneName(targetDestination);
            Scene targetScene = SceneManager.GetSceneByName(targetSceneName);

            if (!targetScene.isLoaded)
            {
                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);

                if (loadOperation == null)
                {
                    Debug.LogError($"[Project I] 목적지 맵 로드 실패 / Scene={targetSceneName}", this);
                    cargoPersistence?.ReleaseCargoAfterTravel();
                    yield return FadeTo(0f, fadeDuration);
                    isTransitioning = false;
                    yield break;
                }

                yield return loadOperation;
                targetScene = SceneManager.GetSceneByName(targetSceneName);
            }

            MapTravelAnchor targetAnchor = FindAnchor(targetScene, targetDestination);

            if (targetAnchor == null || !targetAnchor.IsConfigured)
            {
                Debug.LogError($"[Project I] 목적지 Wagon Entry/Stop 지점 누락 / Scene={targetSceneName}", this);
                cargoPersistence?.ReleaseCargoAfterTravel();
                yield return FadeTo(0f, fadeDuration);
                isTransitioning = false;
                yield break;
            }

            TeleportPersistentGroup(targetAnchor.EntryPoint.position, targetAnchor.EntryPoint.rotation);

            if (targetScene.IsValid() && targetScene.isLoaded)
            {
                SceneManager.SetActiveScene(targetScene);
            }

            Scene previousScene = SceneManager.GetSceneByName(previousSceneName);

            if (previousScene.IsValid() && previousScene.isLoaded && previousScene.name != targetSceneName)
            {
                AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(previousScene);

                if (unloadOperation != null)
                {
                    yield return unloadOperation;
                }
            }

            currentDestination = targetDestination;
            RestoreRuntimeOfficeState();
            yield return MovePersistentGroup(targetAnchor);
            BindBell();
            isTransitioning = false;
            Debug.Log($"[Project I] 맵 교체 완료 / Persistent 유지 / Map={targetSceneName}", this);
        }

        private IEnumerator MovePersistentGroup(MapTravelAnchor anchor)
        {
            if (wagonRoot == null || anchor == null || anchor.EntryPoint == null || anchor.StopPoint == null)
            {
                cargoPersistence?.ReleaseCargoAfterTravel();
                yield return FadeTo(0f, fadeDuration);
                yield break;
            }

            Vector3 startPosition = anchor.EntryPoint.position;
            Quaternion startRotation = anchor.EntryPoint.rotation;
            Vector3 endPosition = anchor.StopPoint.position;
            Quaternion endRotation = anchor.StopPoint.rotation;
            bool playerIsWagonChild = playerRoot != null && playerRoot.IsChildOf(wagonRoot);
            Vector3 playerLocalPosition = Vector3.zero;
            Quaternion playerLocalRotation = Quaternion.identity;

            if (playerRoot != null && !playerIsWagonChild)
            {
                playerLocalPosition = wagonRoot.InverseTransformPoint(playerRoot.position);
                playerLocalRotation = Quaternion.Inverse(wagonRoot.rotation) * playerRoot.rotation;
            }

            float duration = Mathf.Max(0.1f, arrivalDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, normalized);
                wagonRoot.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, endPosition, eased),
                    Quaternion.Slerp(startRotation, endRotation, eased));

                if (playerRoot != null && !playerIsWagonChild)
                {
                    playerRoot.SetPositionAndRotation(
                        wagonRoot.TransformPoint(playerLocalPosition),
                        wagonRoot.rotation * playerLocalRotation);
                }

                cargoPersistence?.SyncCapturedCargoToWagon();

                if (fadeGroup != null)
                {
                    fadeGroup.alpha = 1f - normalized;
                }

                yield return null;
            }

            wagonRoot.SetPositionAndRotation(endPosition, endRotation);

            if (playerRoot != null && !playerIsWagonChild)
            {
                playerRoot.SetPositionAndRotation(
                    wagonRoot.TransformPoint(playerLocalPosition),
                    wagonRoot.rotation * playerLocalRotation);
            }

            cargoPersistence?.SyncCapturedCargoToWagon();
            cargoPersistence?.ReleaseCargoAfterTravel();
            SetFadeImmediate(0f);
        }

        private void TeleportPersistentGroup(Vector3 targetPosition, Quaternion targetRotation)
        {
            BindPersistentReferences();

            if (wagonRoot == null)
            {
                Debug.LogError("[Project I] Persistent Wagon을 찾지 못했습니다.", this);
                return;
            }

            bool playerIsWagonChild = playerRoot != null && playerRoot.IsChildOf(wagonRoot);
            Vector3 playerLocalPosition = Vector3.zero;
            Quaternion playerLocalRotation = Quaternion.identity;

            if (playerRoot != null && !playerIsWagonChild)
            {
                playerLocalPosition = wagonRoot.InverseTransformPoint(playerRoot.position);
                playerLocalRotation = Quaternion.Inverse(wagonRoot.rotation) * playerRoot.rotation;
            }

            wagonRoot.SetPositionAndRotation(targetPosition, targetRotation);

            if (playerRoot != null && !playerIsWagonChild)
            {
                playerRoot.SetPositionAndRotation(
                    wagonRoot.TransformPoint(playerLocalPosition),
                    wagonRoot.rotation * playerLocalRotation);
            }

            cargoPersistence?.SyncCapturedCargoToWagon();
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            if (fadeGroup == null)
            {
                yield break;
            }

            float startAlpha = fadeGroup.alpha;
            float safeDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            fadeGroup.blocksRaycasts = true;

            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / safeDuration);
                fadeGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, normalized);
                yield return null;
            }

            fadeGroup.alpha = targetAlpha;
            fadeGroup.blocksRaycasts = targetAlpha > 0.01f;
        }

        private void SetFadeImmediate(float alpha)
        {
            if (fadeGroup == null)
            {
                return;
            }

            fadeGroup.alpha = Mathf.Clamp01(alpha);
            fadeGroup.blocksRaycasts = fadeGroup.alpha > 0.01f;
        }

        private void BindPersistentReferences()
        {
            if (wagonRoot == null)
            {
                WagonCargoArea cargoArea = Object.FindFirstObjectByType<WagonCargoArea>();

                if (cargoArea != null)
                {
                    wagonRoot = cargoArea.transform.root;
                }
            }

            if (cargoPersistence == null && wagonRoot != null)
            {
                cargoPersistence = wagonRoot.GetComponentInChildren<WagonCargoPersistence>(true);
            }

            if (playerRoot == null)
            {
                PlayerCarryController carryController = Object.FindFirstObjectByType<PlayerCarryController>();

                if (carryController != null)
                {
                    playerRoot = carryController.transform.root;
                }
            }
        }

        private void BindBell()
        {
            BindPersistentReferences();
            WagonTravelBellInteractable nextBell = wagonRoot != null
                ? wagonRoot.GetComponentInChildren<WagonTravelBellInteractable>(true)
                : Object.FindFirstObjectByType<WagonTravelBellInteractable>();

            if (travelBell == nextBell)
            {
                return;
            }

            UnbindBell();
            travelBell = nextBell;

            if (travelBell != null)
            {
                travelBell.TravelRequested += HandleTravelRequested;
            }
            else
            {
                Debug.LogWarning("[Project I] Persistent Wagon의 이동 종을 찾지 못했습니다.", this);
            }
        }

        private void UnbindBell()
        {
            if (travelBell != null)
            {
                travelBell.TravelRequested -= HandleTravelRequested;
                travelBell = null;
            }
        }

        private static string GetSceneName(TravelDestination destination)
        {
            return destination == TravelDestination.TestDungeon
                ? TestDungeonSceneName
                : OfficeSceneName;
        }

        private static MapTravelAnchor FindAnchor(Scene scene, TravelDestination destination)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();

            foreach (GameObject root in roots)
            {
                MapTravelAnchor[] anchors = root.GetComponentsInChildren<MapTravelAnchor>(true);

                foreach (MapTravelAnchor anchor in anchors)
                {
                    if (anchor != null && anchor.Destination == destination)
                    {
                        return anchor;
                    }
                }
            }

            return null;
        }

        private void OnValidate()
        {
            fadeDuration = Mathf.Max(0.1f, fadeDuration);
            arrivalDuration = Mathf.Max(0.1f, arrivalDuration);
        }
    }
}
