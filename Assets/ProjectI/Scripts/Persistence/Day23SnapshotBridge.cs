using System; // 콜백과 문자열 기능 사용
using System.Collections.Generic; // 아이템 집합과 목록 기능 사용
using System.Reflection; // 23일차 private 직렬화 상태 복구 기능 사용
using ProjectI.Economy; // 사무소 경제·보관 시스템 참조
using ProjectI.Items; // 빠른 슬롯과 WorldItem 데이터 참조
using UnityEngine; // 유니티 오브젝트 검색과 Transform 기능 사용

namespace ProjectI.Persistence // 일차 저장·복구 네임스페이스
{
    public static class Day23SnapshotBridge // 23일차 기존 코드를 갈아엎지 않고 저장 상태를 연결하는 호환 계층
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic; // private 직렬화 필드 조회 옵션
        private static readonly FieldInfo DebtPhaseField = typeof(DebtLedger).GetField("currentPhaseIndex", PrivateInstance); // 채무 단계 private 필드
        private static readonly FieldInfo DebtPaidField = typeof(DebtLedger).GetField("paidInCurrentPhase", PrivateInstance); // 현재 단계 납부액 private 필드
        private static readonly FieldInfo InventorySlotsField = typeof(PlayerInventory).GetField("slots", PrivateInstance); // 빠른 슬롯 배열 private 필드
        private static readonly FieldInfo InventorySelectedField = typeof(PlayerInventory).GetField("selectedIndex", PrivateInstance); // 선택 슬롯 private 필드
        private static readonly FieldInfo InventoryStorageRootField = typeof(PlayerInventory).GetField("storageRoot", PrivateInstance); // 인벤토리 보관 루트 private 필드
        private static readonly MethodInfo InventoryRefreshMethod = typeof(PlayerInventory).GetMethod("RefreshSelectedItem", PrivateInstance); // 선택 아이템 화면 갱신 private 메서드
        private static readonly FieldInfo PedestalDisplayPointField = typeof(OfficeStoragePedestal).GetField("displayPoint", PrivateInstance); // 단상 표시 위치 private 필드
        private static readonly FieldInfo PedestalStoredItemField = typeof(OfficeStoragePedestal).GetField("storedItem", PrivateInstance); // 단상 보관 아이템 private 필드

        public static bool CaptureEconomy(EconomySnapshotData target) // 현재 로드된 사무소 경제 상태 캡처
        {
            if (target == null) // 결과 대상 유효성 확인
            {
                return false; // 캡처 불가 반환
            }

            CampaignEconomy economy = FindFirst<CampaignEconomy>(); // 현재 공동 경제 상태 조회
            DebtLedger debt = FindFirst<DebtLedger>(); // 현재 채무 장부 상태 조회

            if (economy == null) // 사무소가 로드되지 않은 상태인지 확인
            {
                return false; // 이전 메모리 경제 상태를 유지하도록 실패 반환
            }

            target.hasData = true; // 정상 경제 데이터 존재 표시
            target.sharedFunds = economy.SharedFunds; // 공동 자금 저장
            target.saleMultiplier = economy.SaleMultiplier; // 판매 배율 저장

            if (debt != null) // 채무 시스템 존재 여부 확인
            {
                target.debtPhaseIndex = debt.IsCompleted ? debt.CurrentPhase : Mathf.Max(0, debt.CurrentPhase - 1); // 0기반 현재 단계 저장
                target.paidInCurrentPhase = debt.PaidInCurrentPhase; // 현재 단계 누적 납부액 저장
            }

            return true; // 경제 상태 캡처 성공
        }

        public static void RestoreEconomy(EconomySnapshotData source) // 저장된 사무소 경제 상태 복구
        {
            if (source == null || !source.hasData) // 실제 저장 경제 데이터 여부 확인
            {
                return; // 복구할 경제 상태 없음
            }

            CampaignEconomy economy = FindFirst<CampaignEconomy>(); // 현재 사무소 공동 경제 조회

            if (economy != null) // 경제 오브젝트가 로드됐는지 확인
            {
                economy.Configure(source.sharedFunds, source.saleMultiplier); // 기존 공개 Configure로 공동 자금과 배율 복구
            }

            DebtLedger debt = FindFirst<DebtLedger>(); // 현재 사무소 채무 장부 조회

            if (debt != null && DebtPhaseField != null && DebtPaidField != null) // 채무 private 상태 접근 가능 여부 확인
            {
                DebtPhaseField.SetValue(debt, Mathf.Max(0, source.debtPhaseIndex)); // 저장된 0기반 채무 단계 복구
                DebtPaidField.SetValue(debt, Mathf.Max(0, source.paidInCurrentPhase)); // 저장된 현재 단계 납부액 복구
                debt.Configure(economy); // 기존 NormalizeState와 경제 참조 재연결
            }
        }

        public static int CaptureInventory(List<ItemInstanceData> destination, HashSet<WorldItem> captured) // 플레이어 빠른 슬롯 전체 캡처
        {
            PlayerInventory inventory = FindFirst<PlayerInventory>(); // Persistent Player 인벤토리 조회

            if (inventory == null || destination == null || captured == null) // 필수 대상 확인
            {
                return 0; // 선택 슬롯 기본값 반환
            }

            for (int slotIndex = 0; slotIndex < inventory.SlotCount; slotIndex++) // 모든 빠른 슬롯 순회
            {
                WorldItem item = inventory.GetItem(slotIndex); // 해당 슬롯 실제 아이템 조회

                if (item == null || captured.Contains(item)) // 빈 슬롯 또는 중복 캡처 확인
                {
                    continue; // 다음 슬롯 검사
                }

                ItemInstanceData data = CreateBaseItemData(item); // 공통 개별 아이템 데이터 생성
                data.location = SnapshotItemLocation.PlayerInventory; // 플레이어 인벤토리 위치 기록
                data.slotIndex = slotIndex; // 정확한 빠른 슬롯 번호 기록
                destination.Add(data); // 스냅샷 목록에 추가
                captured.Add(item); // 다른 위치에서 중복 캡처 방지
            }

            return inventory.SelectedIndex; // 현재 선택 슬롯 반환
        }

        public static void ClearInventoryForRestore() // 기존 빠른 슬롯 참조를 스냅샷 복구 전에 비움
        {
            PlayerInventory inventory = FindFirst<PlayerInventory>(); // 현재 Persistent Player 인벤토리 조회

            if (inventory == null || InventorySlotsField == null) // 필수 반사 필드 확인
            {
                return; // 초기화 중단
            }

            Transform storageRoot = InventoryStorageRootField?.GetValue(inventory) as Transform; // 기존 숨김 보관 루트 조회
            PlayerCarryController carry = inventory.GetComponent<PlayerCarryController>(); // 현재 손에 든 아이템 컨트롤러 조회
            carry?.HolsterHeldItem(storageRoot); // 기존 손 아이템을 먼저 안전하게 숨김 처리
            QuickSlot[] slots = InventorySlotsField.GetValue(inventory) as QuickSlot[]; // 실제 슬롯 배열 조회

            if (slots == null) // 슬롯 배열 유효성 확인
            {
                return; // 추가 초기화 불필요
            }

            foreach (QuickSlot slot in slots) // 전체 슬롯 순회
            {
                slot?.Clear(); // 기존 WorldItem 참조 제거
            }
        }

        public static bool RestoreInventoryItem(WorldItem item, int slotIndex) // 복구 아이템을 저장 당시 정확한 슬롯에 배치
        {
            PlayerInventory inventory = FindFirst<PlayerInventory>(); // 현재 Persistent Player 인벤토리 조회

            if (inventory == null || item == null || InventorySlotsField == null) // 복구 필수 조건 확인
            {
                return false; // 슬롯 복구 실패
            }

            QuickSlot[] slots = InventorySlotsField.GetValue(inventory) as QuickSlot[]; // 내부 빠른 슬롯 배열 조회

            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null) // 정확한 슬롯 유효성 확인
            {
                return false; // 잘못된 슬롯 데이터 거부
            }

            Transform storageRoot = InventoryStorageRootField?.GetValue(inventory) as Transform; // 플레이어 숨김 보관 루트 조회
            item.IgnoreCollisionsWith(inventory.transform); // 복구 아이템과 플레이어 충돌 무시 적용
            item.Store(storageRoot); // 빠른 슬롯 보관 상태로 전환
            slots[slotIndex].SetItem(item); // 저장 당시 슬롯에 직접 등록
            return true; // 복구 성공 반환
        }

        public static void FinalizeInventorySelection(int selectedIndex) // 모든 슬롯 복구 후 선택 슬롯과 손 표시 갱신
        {
            PlayerInventory inventory = FindFirst<PlayerInventory>(); // 현재 PlayerInventory 조회

            if (inventory == null) // 인벤토리 유효성 확인
            {
                return; // 선택 복구 중단
            }

            int clamped = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, inventory.SlotCount - 1)); // 저장된 선택 인덱스 범위 보정
            InventorySelectedField?.SetValue(inventory, clamped); // private 선택 슬롯 값 복구
            InventoryRefreshMethod?.Invoke(inventory, null); // 기존 화면 운반 갱신 로직 실행
        }

        public static void CaptureOfficeStorage(List<ItemInstanceData> destination, HashSet<WorldItem> captured) // 사무소 보관 단상 아이템 캡처
        {
            if (destination == null || captured == null) // 결과 대상 확인
            {
                return; // 캡처 중단
            }

            OfficeStoragePedestal[] pedestals = FindAll<OfficeStoragePedestal>(); // 현재 로드된 모든 사무소 단상 조회

            foreach (OfficeStoragePedestal pedestal in pedestals) // 단상 순회
            {
                WorldItem item = pedestal == null ? null : pedestal.StoredItem; // 현재 단상 보관 아이템 조회

                if (item == null || captured.Contains(item)) // 비어 있거나 이미 캡처된 아이템인지 확인
                {
                    continue; // 다음 단상 검사
                }

                ItemInstanceData data = CreateBaseItemData(item); // 공통 개별 아이템 데이터 생성
                data.location = SnapshotItemLocation.OfficeStorage; // 사무소 단상 위치 기록
                data.storageKey = BuildTransformPath(pedestal.transform); // 같은 단상을 찾을 안정적인 계층 경로 저장
                destination.Add(data); // 스냅샷 목록에 추가
                captured.Add(item); // 중복 캡처 방지
            }
        }

        public static void ClearOfficeStorageForRestore() // 기존 단상 WorldItem 참조 초기화
        {
            if (PedestalStoredItemField == null) // private 필드 접근 가능 여부 확인
            {
                return; // 초기화 불가
            }

            OfficeStoragePedestal[] pedestals = FindAll<OfficeStoragePedestal>(); // 현재 사무소 단상 전체 조회

            foreach (OfficeStoragePedestal pedestal in pedestals) // 단상 순회
            {
                if (pedestal != null) // 유효 단상 확인
                {
                    PedestalStoredItemField.SetValue(pedestal, null); // 기존 보관 아이템 참조 제거
                }
            }
        }

        public static bool RestoreOfficeStorageItem(WorldItem item, string storageKey) // 저장 당시 사무소 단상에 아이템 복구
        {
            if (item == null || PedestalStoredItemField == null) // 복구 대상 유효성 확인
            {
                return false; // 단상 복구 실패
            }

            OfficeStoragePedestal[] pedestals = FindAll<OfficeStoragePedestal>(); // 현재 사무소 단상 전체 조회
            OfficeStoragePedestal target = null; // 복구 대상 단상 초기화

            foreach (OfficeStoragePedestal pedestal in pedestals) // 단상 순회
            {
                if (pedestal != null && string.Equals(BuildTransformPath(pedestal.transform), storageKey, StringComparison.Ordinal)) // 저장 경로와 동일한 단상 확인
                {
                    target = pedestal; // 정확한 단상 선택
                    break; // 검색 종료
                }
            }

            if (target == null) // 원래 단상을 찾지 못했는지 확인
            {
                return false; // 임의 단상 배치를 하지 않고 실패 반환
            }

            Transform displayPoint = PedestalDisplayPointField?.GetValue(target) as Transform; // 단상 실제 표시 위치 조회

            if (displayPoint == null) // 표시 위치 누락 확인
            {
                displayPoint = target.transform.Find("DisplayPoint"); // 기존 이름 규칙으로 재검색
            }

            if (displayPoint == null) // 최종 표시 위치 확인
            {
                return false; // 복구 위치가 없으면 실패
            }

            item.Store(displayPoint); // WorldItem을 보관 상태로 전환
            item.transform.SetParent(displayPoint, false); // 단상 표시 위치 아래로 연결
            item.transform.localPosition = Vector3.zero; // 단상 중심 위치 복원
            item.transform.localRotation = Quaternion.identity; // 단상 기본 회전 복원
            Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true); // Store가 숨긴 Renderer 목록 조회

            foreach (Renderer renderer in renderers) // 모든 외형 순회
            {
                if (renderer != null) // 유효 Renderer 확인
                {
                    renderer.enabled = true; // 단상 위 실제 회수품 다시 표시
                }
            }

            OfficeStoredItemState state = item.GetComponent<OfficeStoredItemState>(); // 사무소 영구 보관 상태 조회

            if (state == null) // 이전 아이템이라 상태 컴포넌트가 없는지 확인
            {
                state = item.gameObject.AddComponent<OfficeStoredItemState>(); // 영구 보관 보호 상태 추가
            }

            state.SetStored(target, true); // 원래 단상의 영구 보관 상태 복구
            PedestalStoredItemField.SetValue(target, item); // 단상 내부 참조 복구
            return true; // 복구 성공 반환
        }

        public static ItemInstanceData CreateBaseItemData(WorldItem item) // 위치별 공통 개별 아이템 필드 캡처
        {
            WorldItemIdentity identity = item == null ? null : item.GetComponent<WorldItemIdentity>(); // 저장 식별자 조회

            if (identity == null && item != null) // 아직 식별자 없는 기존 아이템인지 확인
            {
                identity = item.gameObject.AddComponent<WorldItemIdentity>(); // 런타임 안전용 식별자 추가
            }

            identity?.EnsureInstanceId(); // 개별 GUID 보장
            RecoverableValue recoverable = item == null ? null : item.GetComponent<RecoverableValue>(); // 회수품 가격 상태 조회
            return new ItemInstanceData // 공통 데이터 생성
            {
                instanceId = identity == null ? string.Empty : identity.InstanceId, // 동일 개별 아이템 ID 저장
                itemId = identity == null ? string.Empty : identity.ItemId, // 복구 Definition ID 저장
                displayName = item == null ? string.Empty : item.DisplayName, // 표시 이름 저장
                value = recoverable == null ? 0 : recoverable.Value, // 회수품 가치 저장
                isSold = recoverable != null && recoverable.IsSold, // 판매 완료 상태 저장
                sceneName = item == null ? string.Empty : item.gameObject.scene.name, // 현재 씬 이름 저장
                position = item == null ? Vector3.zero : item.transform.position, // 기본 월드 위치 저장
                rotation = item == null ? Quaternion.identity : item.transform.rotation // 기본 월드 회전 저장
            };
        }

        public static string BuildTransformPath(Transform target) // 씬 내 단상 식별용 전체 계층 경로 생성
        {
            if (target == null) // Transform 유효성 확인
            {
                return string.Empty; // 빈 식별 키 반환
            }

            string path = target.name; // 현재 오브젝트 이름으로 시작
            Transform current = target.parent; // 부모부터 루트 방향 검색 시작

            while (current != null) // 모든 부모 순회
            {
                path = current.name + "/" + path; // 부모 이름을 앞에 추가
                current = current.parent; // 한 단계 위로 이동
            }

            return path; // 완성된 계층 경로 반환
        }

        private static T FindFirst<T>() where T : UnityEngine.Object // 비활성 포함 첫 컴포넌트 조회
        {
            T[] objects = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 로드된 전체 대상 조회
            return objects != null && objects.Length > 0 ? objects[0] : null; // 첫 대상 또는 null 반환
        }

        private static T[] FindAll<T>() where T : UnityEngine.Object // 비활성 포함 전체 컴포넌트 조회
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 로드된 전체 대상 반환
        }
    }
}
