using System; // 콜백 기능 참조
using UnityEngine; // PlayerPrefs와 MonoBehaviour 기능 참조
using UnityEngine.InputSystem; // Input Action 재바인딩 기능 참조

namespace ProjectI.Player // 플레이어 입력 기능 네임스페이스
{
    [RequireComponent(typeof(PlayerInputReader))] // 입력 래퍼 필수 지정
    public sealed class InputRebindService : MonoBehaviour // 향후 설정 화면에서 사용할 키 재바인딩 서비스
    {
        private const string BindingOverridesKey = "ProjectI.Input.BindingOverrides"; // 저장된 키 설정 PlayerPrefs 키
        [SerializeField] private PlayerInputReader inputReader; // 재바인딩할 입력 에셋 소유자
        private InputActionRebindingExtensions.RebindingOperation currentOperation; // 현재 진행 중인 재바인딩 작업

        public bool IsRebinding => currentOperation != null; // 현재 재바인딩 진행 여부 공개

        private void Awake() // 재바인딩 서비스 초기화
        {
            if (inputReader == null) // 입력 래퍼 참조 누락 확인
            {
                inputReader = GetComponent<PlayerInputReader>(); // 같은 플레이어의 입력 래퍼 자동 조회
            }

            LoadBindingOverrides(); // 저장된 사용자 키 설정 적용
        }

        private void OnDisable() // 서비스 비활성화 처리
        {
            CancelCurrentRebind(); // 진행 중인 재바인딩 안전하게 종료
        }

        public void Configure(PlayerInputReader reader) // 에디터 자동 설정용 입력 래퍼 지정
        {
            inputReader = reader; // 입력 래퍼 저장
        }

        public string GetBindingDisplay(string actionName, int bindingIndex) // 설정 UI에 표시할 현재 키 이름 반환
        {
            InputAction action = FindAction(actionName); // 대상 액션 조회

            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) // 액션 또는 바인딩 인덱스 유효성 확인
            {
                return string.Empty; // 표시할 키 없음 반환
            }

            return action.GetBindingDisplayString(bindingIndex); // 현재 기본값 또는 사용자 재바인딩 값 표시
        }

        public bool StartInteractiveRebind(string actionName, int bindingIndex, Action<string> onComplete) // 설정 UI 버튼에서 사용할 대화형 키 변경 시작
        {
            CancelCurrentRebind(); // 이전 재바인딩 작업 정리
            InputAction action = FindAction(actionName); // 대상 Input Action 조회

            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) // 대상 바인딩 유효성 확인
            {
                return false; // 재바인딩 시작 실패 반환
            }

            bool wasEnabled = action.enabled; // 재바인딩 전 액션 활성 상태 저장
            action.Disable(); // 입력 충돌 방지를 위해 대상 액션 일시 비활성화
            currentOperation = action.PerformInteractiveRebinding(bindingIndex); // 지정 바인딩에 대한 대화형 재바인딩 생성
            currentOperation.OnComplete(operation => CompleteRebind(action, wasEnabled, onComplete)); // 새 입력을 받았을 때 완료 처리 연결
            currentOperation.OnCancel(operation => CompleteRebind(action, wasEnabled, onComplete)); // 설정 UI에서 취소했을 때 정리 처리 연결
            currentOperation.Start(); // 실제 재바인딩 입력 대기 시작
            return true; // 재바인딩 시작 성공 반환
        }

        public void CancelCurrentRebind() // 현재 키 변경 작업 취소
        {
            if (currentOperation == null) // 진행 중인 작업 존재 여부 확인
            {
                return; // 취소할 작업 없음
            }

            currentOperation.Cancel(); // 진행 중인 재바인딩 취소
        }

        public void ResetBinding(string actionName, int bindingIndex) // 지정 키 하나를 기본값으로 복구
        {
            InputAction action = FindAction(actionName); // 대상 액션 조회

            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) // 대상 바인딩 유효성 확인
            {
                return; // 복구 중단
            }

            action.RemoveBindingOverride(bindingIndex); // 지정 바인딩의 사용자 설정 제거
            SaveBindingOverrides(); // 변경된 키 설정 저장
        }

        public void ResetAllBindings() // 모든 게임플레이 키를 기본값으로 복구
        {
            InputActionAsset actions = GetInputActions(); // 입력 에셋 조회

            if (actions == null) // 입력 에셋 누락 확인
            {
                return; // 전체 복구 중단
            }

            actions.RemoveAllBindingOverrides(); // 모든 사용자 키 변경 제거
            PlayerPrefs.DeleteKey(BindingOverridesKey); // 저장된 키 설정 삭제
            PlayerPrefs.Save(); // PlayerPrefs 변경 즉시 저장
        }

        public void SaveBindingOverrides() // 현재 사용자 키 설정 저장
        {
            InputActionAsset actions = GetInputActions(); // 입력 에셋 조회

            if (actions == null) // 입력 에셋 누락 확인
            {
                return; // 저장 중단
            }

            string json = actions.SaveBindingOverridesAsJson(); // 현재 재바인딩 정보 JSON 생성
            PlayerPrefs.SetString(BindingOverridesKey, json); // 키 설정 JSON 저장
            PlayerPrefs.Save(); // PlayerPrefs 변경 즉시 저장
        }

        public void LoadBindingOverrides() // 이전 실행에서 저장한 키 설정 불러오기
        {
            InputActionAsset actions = GetInputActions(); // 입력 에셋 조회

            if (actions == null || !PlayerPrefs.HasKey(BindingOverridesKey)) // 입력 에셋 또는 저장 데이터 존재 여부 확인
            {
                return; // 불러오기 중단
            }

            string json = PlayerPrefs.GetString(BindingOverridesKey); // 저장된 키 설정 JSON 조회

            if (string.IsNullOrWhiteSpace(json)) // 저장 문자열 유효성 확인
            {
                return; // 빈 데이터 적용 방지
            }

            actions.LoadBindingOverridesFromJson(json); // 저장된 사용자 키 설정 적용
        }

        private void CompleteRebind(InputAction action, bool wasEnabled, Action<string> onComplete) // 재바인딩 완료·취소 공통 정리
        {
            currentOperation?.Dispose(); // Input System 재바인딩 작업 리소스 해제
            currentOperation = null; // 현재 작업 참조 제거

            if (wasEnabled) // 기존 액션 활성 상태 확인
            {
                action.Enable(); // 재바인딩 전 활성 상태 복구
            }

            SaveBindingOverrides(); // 변경된 키 설정 저장
            onComplete?.Invoke(action.GetBindingDisplayString()); // 설정 UI에 최신 표시 문자열 전달
        }

        private InputAction FindAction(string actionName) // Player Map에서 지정 액션 조회
        {
            InputActionAsset actions = GetInputActions(); // 입력 에셋 조회

            if (actions == null) // 입력 에셋 누락 확인
            {
                return null; // 대상 액션 없음 반환
            }

            InputActionMap map = actions.FindActionMap(GameplayInputActions.Map, false); // Player 액션 맵 조회
            return map == null ? null : map.FindAction(actionName, false); // 지정 액션 반환
        }

        private InputActionAsset GetInputActions() // 현재 플레이어 입력 에셋 조회
        {
            return inputReader == null ? null : inputReader.InputActions; // 입력 래퍼가 보유한 에셋 반환
        }
    }
}
