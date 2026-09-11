using System.Collections.Generic; // 보관 아이템 기록 목록 기능 참조
using ProjectI.Economy; // 판매·단상 보관 상태 참조
using ProjectI.Items; // WorldItem 기능 참조
using ProjectI.Persistence; // 단상 경로 계산·단상 복귀 호환 계층 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 소속 이동 기능 참조

namespace ProjectI.Loop // 원정 루프 기능 네임스페이스
{
    [DisallowMultipleComponent] // Persistent 시스템에 중복 부착 방지
    public sealed class OfficeWorldItemKeeper : MonoBehaviour // Office가 언로드된 동안 사무소 실제 WorldItem을 재생성 없이 보관
    {
        private const string StashRootName = "Day25_OfficeItemStash"; // Persistent 씬 보관 루트 이름
        private readonly List<KeptOfficeItem> keptItems = new List<KeptOfficeItem>(); // 현재 보관 중인 사무소 아이템 기록
        private Transform stashRoot; // 비활성 보관 루트
        private bool hasPendingOfficeState; // Office를 떠나며 런타임 상태를 보관했는지 여부

        public bool HasPendingOfficeState => hasPendingOfficeState; // 다음 Office 로드에서 씬 기본 아이템을 교체해야 하는지 공개
        public int KeptCount => keptItems.Count; // 진단용 보관 아이템 수 공개

        public void StashFromScene(Scene officeScene) // Office 언로드 직전 사무소 아이템을 같은 GameObject로 Persistent 씬에 보관
        {
            if (!officeScene.IsValid() || !officeScene.isLoaded) // 보관 대상 Office 씬 유효성 확인
            {
                return; // 보관 중단
            }

            EnsureStashRoot(); // 비활성 보관 루트 확보
            keptItems.Clear(); // 이전 보관 기록 초기화
            WorldItem[] items = Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 로드된 WorldItem 전체 조회

            foreach (WorldItem item in items) // 모든 WorldItem 순회
            {
                if (item == null || item.gameObject.scene != officeScene || !item.gameObject.activeInHierarchy || item.IsHeld) // Office 월드에 실제로 남아 있는 아이템인지 확인
                {
                    continue; // 다른 씬·비활성(판매·손실)·플레이어 소유 아이템 제외
                }

                RecoverableValue recoverable = item.GetComponent<RecoverableValue>(); // 판매 상태 조회

                if (recoverable != null && recoverable.IsSold) // 판매 완료 아이템인지 확인
                {
                    continue; // 판매품은 Office와 함께 정리
                }

                if (item.transform.parent != null && item.transform.parent.GetComponentInParent<WorldItem>() != null) // 다른 WorldItem의 하위 아이템인지 확인
                {
                    continue; // 상위 아이템과 함께 이동
                }

                OfficeStoredItemState storedState = item.GetComponent<OfficeStoredItemState>(); // 단상 보관 상태 조회
                bool onPedestal = storedState != null && storedState.IsOfficeStored && storedState.Pedestal != null; // 단상 보관품 여부 계산
                keptItems.Add(new KeptOfficeItem // 복귀에 필요한 위치 정보 기록
                {
                    Item = item, // 동일 실제 WorldItem 참조
                    StorageKey = onPedestal ? Day23SnapshotBridge.BuildTransformPath(storedState.Pedestal.transform) : string.Empty, // 단상 계층 경로
                    Position = item.transform.position, // Office 월드 위치
                    Rotation = item.transform.rotation // Office 월드 회전
                });

                item.transform.SetParent(null, true); // 씬 이동을 위해 루트로 분리
                SceneManager.MoveGameObjectToScene(item.gameObject, gameObject.scene); // 동일 GameObject를 Persistent 씬으로 이동
                item.transform.SetParent(stashRoot, true); // 비활성 보관 루트 아래로 이동해 물리·상호작용 정지
            }

            hasPendingOfficeState = true; // 다음 Office 로드는 씬 기본 아이템 대신 보관 상태를 사용
            Debug.Log($"[Project I] 사무소 아이템 보관 / Count={keptItems.Count}", this); // 보관 결과 로그
        }

        public void RestoreIntoScene(Scene officeScene) // Office 재로드 직후 씬 기본 아이템을 제거하고 보관 아이템을 복귀
        {
            if (!hasPendingOfficeState || !officeScene.IsValid() || !officeScene.isLoaded) // 보관 상태와 Office 씬 유효성 확인
            {
                return; // 세션 최초 Office 로드는 씬 기본 아이템을 그대로 사용
            }

            int removedTemplates = RemoveSceneTemplateItems(officeScene); // 재로드로 다시 생긴 씬 기본 아이템 제거
            int restoredCount = 0; // 복귀 성공 개수

            foreach (KeptOfficeItem kept in keptItems) // 보관 기록 순회
            {
                if (kept.Item == null) // 보관 중 제거된 아이템 확인
                {
                    continue; // 다음 기록 처리
                }

                GameObject itemObject = kept.Item.gameObject; // 이동 대상 GameObject
                itemObject.transform.SetParent(null, true); // 보관 루트에서 분리
                SceneManager.MoveGameObjectToScene(itemObject, officeScene); // 동일 GameObject를 Office 씬으로 반환

                if (!string.IsNullOrEmpty(kept.StorageKey)) // 단상 보관품인지 확인
                {
                    if (Day23SnapshotBridge.RestoreOfficeStorageItem(kept.Item, kept.StorageKey)) // 같은 경로의 새 단상에 다시 연결
                    {
                        restoredCount++; // 복귀 성공 집계
                        continue; // 다음 기록 처리
                    }

                    Debug.LogWarning($"[Project I] 단상을 찾지 못해 원래 위치에 월드 아이템으로 복귀 / Item={kept.Item.DisplayName} / Key={kept.StorageKey}", kept.Item); // 단상 누락 안내
                    kept.Item.GetComponent<OfficeStoredItemState>()?.SetStored(null, false); // 단상 보호 상태 해제
                    kept.Item.Release(kept.Position, kept.Rotation, Vector3.zero); // 보관 상태에서 월드 물체로 전환
                    restoredCount++; // 복귀 성공 집계
                    continue; // 다음 기록 처리
                }

                itemObject.transform.SetPositionAndRotation(kept.Position, kept.Rotation); // 떠날 때의 Office 위치·회전 복원
                Rigidbody body = kept.Item.Body; // 아이템 Rigidbody 조회

                if (body != null && !body.isKinematic) // Dynamic Rigidbody 여부 확인
                {
                    body.linearVelocity = Vector3.zero; // 복귀 순간 직선 속도 제거
                    body.angularVelocity = Vector3.zero; // 복귀 순간 회전 속도 제거
                }

                restoredCount++; // 복귀 성공 집계
            }

            keptItems.Clear(); // 보관 기록 정리
            hasPendingOfficeState = false; // 보관 상태 적용 완료
            Physics.SyncTransforms(); // 복귀 위치를 물리 엔진에 즉시 반영
            Debug.Log($"[Project I] 사무소 아이템 복귀 / Restored={restoredCount} / SceneTemplateRemoved={removedTemplates}", this); // 복귀 결과 로그
        }

        public void DiscardStash() // Snapshot 복구가 Office 상태를 대체할 때 보관 상태 폐기
        {
            foreach (KeptOfficeItem kept in keptItems) // 보관 기록 순회
            {
                if (kept.Item != null) // 남아 있는 아이템 확인
                {
                    Destroy(kept.Item.gameObject); // Snapshot 기준 재생성과 중복되지 않도록 제거
                }
            }

            keptItems.Clear(); // 보관 기록 정리
            hasPendingOfficeState = false; // 다음 Office 로드는 복구된 상태를 그대로 사용
        }

        private static int RemoveSceneTemplateItems(Scene officeScene) // Office 씬 파일에서 새로 생성된 기본 WorldItem 제거
        {
            int removed = 0; // 제거 개수

            foreach (GameObject root in officeScene.GetRootGameObjects()) // Office 루트 오브젝트 순회
            {
                foreach (WorldItem template in root.GetComponentsInChildren<WorldItem>(true)) // 하위 WorldItem 전체 조회
                {
                    if (template == null) // 이미 제거된 항목 확인
                    {
                        continue; // 다음 항목 처리
                    }

                    template.gameObject.SetActive(false); // 같은 프레임 검색·물리 판정에서 즉시 제외
                    Destroy(template.gameObject); // 런타임 상태와 중복되는 씬 기본 아이템 제거
                    removed++; // 제거 개수 집계
                }
            }

            return removed; // 제거 결과 반환
        }

        private void EnsureStashRoot() // Persistent 씬 비활성 보관 루트 확보
        {
            if (stashRoot != null) // 이미 생성됐는지 확인
            {
                return; // 추가 생성 불필요
            }

            GameObject rootObject = new GameObject(StashRootName); // 보관 루트 생성
            SceneManager.MoveGameObjectToScene(rootObject, gameObject.scene); // Persistent 씬 소속 보장
            rootObject.SetActive(false); // 보관 아이템 물리·상호작용·렌더링 정지
            stashRoot = rootObject.transform; // 참조 저장
        }

        private sealed class KeptOfficeItem // Office 보관 아이템 기록
        {
            public WorldItem Item; // 동일 실제 WorldItem 참조
            public string StorageKey; // 단상 계층 경로 (단상 보관품만 사용)
            public Vector3 Position; // Office 월드 위치
            public Quaternion Rotation; // Office 월드 회전
        }
    }
}
