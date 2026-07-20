using UnityEngine; // Unity 기본 기능 사용
using UnityEngine.InputSystem; // Keyboard.current 입력 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class VillageSaveLedger : MonoBehaviour, IInteractable // 마을 저장 창을 여는 장부
    {
        [Header("저장 연결")] // Inspector 저장 연결 구분
        [SerializeField] CampaignSaveManager saveManager; // 저장과 불러오기를 처리할 관리자

        [Header("화면 설정")] // Inspector 임시 화면 설정 구분
        [SerializeField] string ledgerTitle = "도굴단 원정 장부"; // 저장 창 제목

        bool windowOpen; // 현재 저장 창 표시 여부
        bool deleteConfirmation; // 저장 파일 삭제 재확인 여부
        PlayerController lockedController; // 저장 창을 연 플레이어 이동 컴포넌트
        PlayerInteractor lockedInteractor; // 저장 창을 연 플레이어 상호작용 컴포넌트
        bool controllerWasEnabled; // 창을 열기 전 이동 컴포넌트 활성 상태
        bool interactorWasEnabled; // 창을 열기 전 상호작용 컴포넌트 활성 상태
        CursorLockMode previousCursorLockMode; // 창을 열기 전 마우스 잠금 상태
        bool previousCursorVisible; // 창을 열기 전 마우스 표시 상태

        void Awake() // 저장 관리자 참조 자동 검색
        {
            ResolveSaveManager(); // 저장 관리자 연결 시도
        }

        void Update() // 저장 창 닫기 키 입력 확인
        {
            if (!windowOpen) // 저장 창이 닫혀 있는지 확인
            {
                return; // 키 입력 검사 중단
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) // Esc 키 입력 여부 확인
            {
                CloseWindow(); // 저장 창 닫기
            }
        }

        void OnDestroy() // Scene 종료 시 잠긴 플레이어 상태 복구
        {
            RestorePlayerControl(); // 플레이어 이동과 마우스 상태 복구
        }

        public string GetPrompt() // 플레이어에게 표시할 저장 장부 상호작용 문구
        {
            return "[E] 원정 장부 확인"; // 저장 장부 안내 반환
        }

        public void Interact(PlayerInteractor interactor) // 플레이어가 장부와 상호작용할 때 저장 창 열기
        {
            if (windowOpen) // 저장 창이 이미 열려 있는지 확인
            {
                return; // 중복 창 열기 방지
            }

            ResolveSaveManager(); // 저장 관리자 참조 다시 확인

            if (saveManager == null) // 저장 관리자 검색 결과 확인
            {
                Debug.LogError("[VillageSaveLedger] CampaignSaveManager가 없습니다."); // 관리자 누락 오류 출력
                return; // 저장 창 열기 중단
            }

            windowOpen = true; // 저장 창 표시 활성화
            deleteConfirmation = false; // 이전 삭제 확인 상태 초기화
            LockPlayerControl(interactor); // 저장 창 사용 중 플레이어 조작 잠금
        }

        void ResolveSaveManager() // Scene과 싱글톤에서 저장 관리자 참조 검색
        {
            if (CampaignSaveManager.Instance != null) // 전역 저장 관리자 존재 여부 확인
            {
                saveManager = CampaignSaveManager.Instance; // 전역 저장 관리자 연결
                return; // 추가 검색 중단
            }

            if (saveManager == null) // Inspector 참조가 없는지 확인
            {
                saveManager = FindFirstObjectByType<CampaignSaveManager>(); // 현재 Scene에서 저장 관리자 검색
            }
        }

        void LockPlayerControl(PlayerInteractor interactor) // 저장 창 사용 중 이동과 상호작용 잠금
        {
            lockedInteractor = interactor; // 창을 연 플레이어 상호작용 컴포넌트 저장
            lockedController = interactor != null ? interactor.GetComponent<PlayerController>() : null; // 같은 플레이어의 이동 컴포넌트 검색

            controllerWasEnabled = lockedController != null && lockedController.enabled; // 기존 이동 활성 상태 저장
            interactorWasEnabled = lockedInteractor != null && lockedInteractor.enabled; // 기존 상호작용 활성 상태 저장
            previousCursorLockMode = Cursor.lockState; // 기존 마우스 잠금 상태 저장
            previousCursorVisible = Cursor.visible; // 기존 마우스 표시 상태 저장

            if (lockedController != null) // 이동 컴포넌트 존재 여부 확인
            {
                lockedController.enabled = false; // 저장 창 사용 중 플레이어 이동과 시점 정지
            }

            if (lockedInteractor != null) // 상호작용 컴포넌트 존재 여부 확인
            {
                lockedInteractor.enabled = false; // 저장 창 사용 중 추가 상호작용 차단
            }

            Cursor.lockState = CursorLockMode.None; // 마우스 잠금 해제
            Cursor.visible = true; // 저장 창 조작용 마우스 표시
        }

        void RestorePlayerControl() // 저장 창을 닫을 때 플레이어 조작 상태 복원
        {
            if (lockedController != null) // 저장된 이동 컴포넌트 존재 여부 확인
            {
                lockedController.enabled = controllerWasEnabled; // 창을 열기 전 이동 상태 복원
            }

            if (lockedInteractor != null) // 저장된 상호작용 컴포넌트 존재 여부 확인
            {
                lockedInteractor.enabled = interactorWasEnabled; // 창을 열기 전 상호작용 상태 복원
            }

            Cursor.lockState = previousCursorLockMode; // 창을 열기 전 마우스 잠금 상태 복원
            Cursor.visible = previousCursorVisible; // 창을 열기 전 마우스 표시 상태 복원

            lockedController = null; // 저장된 이동 컴포넌트 참조 제거
            lockedInteractor = null; // 저장된 상호작용 컴포넌트 참조 제거
        }

        void CloseWindow() // 저장 창 닫기와 플레이어 상태 복구
        {
            windowOpen = false; // 저장 창 표시 비활성화
            deleteConfirmation = false; // 삭제 확인 상태 초기화
            RestorePlayerControl(); // 플레이어 이동과 마우스 상태 복구
        }

        void OnGUI() // 저장과 불러오기용 임시 화면 표시
        {
            if (!windowOpen) // 저장 창 표시 여부 확인
            {
                return; // GUI 표시 중단
            }

            float width = 520f; // 저장 창 너비
            float height = 340f; // 저장 창 높이
            float x = (Screen.width - width) * 0.5f; // 화면 가로 중앙 위치 계산
            float y = (Screen.height - height) * 0.5f; // 화면 세로 중앙 위치 계산

            GUI.Box(new Rect(x, y, width, height), ledgerTitle); // 저장 창 배경과 제목 표시
            GUI.Label(new Rect(x + 25f, y + 40f, width - 50f, 50f), saveManager.GetSaveSummary()); // 기존 저장 기록 요약 표시
            GUI.Label(new Rect(x + 25f, y + 100f, width - 50f, 40f), saveManager.LastMessage); // 최근 처리 결과 표시

            if (GUI.Button(new Rect(x + 25f, y + 160f, 145f, 45f), "현재 진행 저장")) // 수동 저장 버튼 입력 확인
            {
                saveManager.SaveGame(); // 현재 캠페인 상태 저장
                deleteConfirmation = false; // 삭제 확인 상태 초기화
            }

            GUI.enabled = saveManager.HasSaveFile; // 저장 파일이 있을 때만 불러오기 버튼 활성화

            if (GUI.Button(new Rect(x + 187f, y + 160f, 145f, 45f), "저장 기록 불러오기")) // 불러오기 버튼 입력 확인
            {
                saveManager.LoadGame(); // 저장된 캠페인 상태 복원
                deleteConfirmation = false; // 삭제 확인 상태 초기화
            }

            GUI.enabled = saveManager.HasSaveFile; // 저장 파일이 있을 때만 삭제 버튼 활성화
            string deleteLabel = deleteConfirmation ? "정말 삭제" : "저장 기록 삭제"; // 삭제 확인 상태에 맞는 버튼 문구 계산

            if (GUI.Button(new Rect(x + 349f, y + 160f, 145f, 45f), deleteLabel)) // 저장 파일 삭제 버튼 입력 확인
            {
                if (deleteConfirmation) // 삭제 재확인이 완료됐는지 확인
                {
                    saveManager.DeleteSave(); // 실제 저장 파일 삭제
                    deleteConfirmation = false; // 삭제 확인 상태 초기화
                }
                else // 첫 번째 삭제 버튼 입력인 경우
                {
                    deleteConfirmation = true; // 두 번째 확인 버튼 표시
                }
            }

            GUI.enabled = true; // 이후 GUI 버튼 활성 상태 복원
            GUI.Label(new Rect(x + 25f, y + 225f, width - 50f, 35f), "삭제는 두 번 눌러야 실행됩니다."); // 저장 삭제 안전 안내 표시
            GUI.Label(new Rect(x + 25f, y + 255f, width - 50f, 35f), "던전에서는 마지막 마을 저장 지점으로 되돌아갑니다."); // 체크포인트 규칙 안내 표시

            if (GUI.Button(new Rect(x + 185f, y + 295f, 150f, 35f), "닫기")) // 저장 창 닫기 버튼 입력 확인
            {
                CloseWindow(); // 저장 창 닫기
            }
        }
    }
}