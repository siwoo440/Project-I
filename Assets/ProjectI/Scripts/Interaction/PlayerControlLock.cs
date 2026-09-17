using ProjectI.Items; // 인벤토리 입력 참조
using ProjectI.Player; // 이동 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Interaction // 상호작용 기능 네임스페이스
{
    public static class PlayerControlLock // 창이 열린 동안 플레이어 조작을 멈추는 공용 잠금 (한 번에 창 하나)
    {
        private static Object owner; // 잠근 창
        private static PlayerInteractor lockedInteractor; // 잠긴 상호작용
        private static PlayerInventory lockedInventory; // 잠긴 인벤토리 입력
        private static PlayerMovement lockedMovement; // 잠긴 이동
        private static float savedSpeed = 1f; // 원래 이동 배율
        private static bool savedSprint = true; // 원래 달리기 허용

        public static bool IsLocked => owner != null; // 잠김 여부 (창이 파괴되면 자동 해제로 간주)

        public static bool Acquire(Object requester, PlayerInteractor interactor) // 잠그기 (다른 창이 열려 있으면 실패)
        {
            ClearIfOwnerDestroyed(); // 파괴된 창이 남긴 잠금 정리

            if (requester == null || (owner != null && owner != requester)) // 다른 창
            {
                return false; // 실패
            }

            owner = requester; // 소유
            lockedInteractor = interactor; // 상호작용
            lockedInventory = interactor == null ? null : interactor.GetComponent<PlayerInventory>(); // 인벤토리
            lockedMovement = interactor == null ? null : interactor.GetComponent<PlayerMovement>(); // 이동

            if (lockedMovement != null) // 이동 정지 (중력은 유지)
            {
                savedSpeed = lockedMovement.ExternalSpeedMultiplier; // 원래 배율
                savedSprint = lockedMovement.ExternalSprintAllowed; // 원래 달리기
                lockedMovement.SetExternalMovementModifier(0f, false); // 정지
            }

            SetEnabled(false); // 입력 끔
            Cursor.lockState = CursorLockMode.None; // 커서 표시 (시점 회전도 멈춤)
            Cursor.visible = true; // 표시
            return true; // 성공
        }

        public static void Release(Object requester) // 풀기
        {
            ClearIfOwnerDestroyed(); // 파괴된 창이 남긴 잠금 정리

            if (owner == null || owner != requester) // 소유자만
            {
                return; // 종료
            }

            Clear(); // 정리
        }

        private static void ClearIfOwnerDestroyed() // 창이 닫히지 않고 파괴된 경우 (씬 이동 등)
        {
            if (!ReferenceEquals(owner, null) && owner == null) // 유니티 기준 파괴됨
            {
                Clear(); // 조작 복구
            }
        }

        private static void Clear() // 조작 복구
        {
            if (lockedMovement != null) // 이동 복구
            {
                lockedMovement.SetExternalMovementModifier(savedSpeed, savedSprint); // 원래 값
            }

            SetEnabled(true); // 입력 켬
            Cursor.lockState = CursorLockMode.Locked; // 커서 잠금
            Cursor.visible = false; // 숨김
            owner = null; // 해제
            lockedInteractor = null; // 정리
            lockedInventory = null; // 정리
            lockedMovement = null; // 정리
        }

        private static void SetEnabled(bool enabled) // 입력 컴포넌트 켜고 끔
        {
            if (lockedInventory != null) // 슬롯 전환·버리기
            {
                lockedInventory.enabled = enabled; // 적용
            }

            if (lockedInteractor != null) // F 입력
            {
                lockedInteractor.enabled = enabled; // 적용
            }
        }
    }
}
