using System.Collections.Generic; // 마차에 확보된 아이템 목록 사용
using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class Wagon : MonoBehaviour // 던전 입구의 보물 보관과 익스트랙션 담당
    {
        readonly List<ICarryable> secured = new List<ICarryable>(); // 마차에 확보된 전체 아이템 목록

        Transform cargoRoot; // 확보한 아이템을 숨겨서 보관할 부모 Transform
        bool left; // 마차 탈출 완료 여부

        public int SecuredCount => secured.Count; // 마차에 확보된 전체 아이템 개수 반환

        public int SecuredTreasureCount // 마차에 확보된 실제 보물 개수 반환
        {
            get
            {
                int count = 0; // 확보 보물 개수 초기화

                foreach (ICarryable carryable in secured) // 마차에 확보된 모든 아이템 순회
                {
                    if (carryable is Treasure) // 현재 아이템이 보물인지 확인
                    {
                        count++; // 실제 보물 개수 증가
                    }
                }

                return count; // 최종 확보 보물 개수 반환
            }
        }

        public int SecuredValue // 마차에 확보된 보물의 총 가치 반환
        {
            get
            {
                int value = 0; // 확보 보물 가치 초기화

                foreach (ICarryable carryable in secured) // 마차에 확보된 모든 아이템 순회
                {
                    if (carryable is Treasure treasure) // 현재 아이템이 실제 보물인지 확인
                    {
                        value += treasure.Value; // 현재 보물 가치 누적
                    }
                }

                return value; // 최종 확보 보물 가치 반환
            }
        }

        public bool HasLeft => left; // 마차 탈출 완료 여부 반환

        public event System.Action Left; // 마차 탈출 완료 이벤트

        void Awake() // 마차 보관 지점과 시간 상태 초기화
        {
            Time.timeScale = 1f; // 이전 던전 종료로 정지된 게임시간 복구

            GameObject cargoObject = new GameObject("Cargo"); // 아이템 보관용 자식 오브젝트 생성
            cargoRoot = cargoObject.transform; // 생성된 오브젝트의 Transform 저장
            cargoRoot.SetParent(transform); // 보관 지점을 마차 자식으로 연결
            cargoRoot.localPosition = new Vector3(0f, 1f, 0f); // 보관 지점의 로컬 위치 설정
            cargoRoot.localRotation = Quaternion.identity; // 보관 지점의 로컬 회전 초기화
            cargoRoot.localScale = Vector3.one; // 보관 지점의 로컬 크기 초기화
        }

        public void Deposit(ICarryable carryable) // 전달된 아이템을 마차에 안전하게 확보
        {
            if (carryable == null) // 전달된 아이템 존재 여부 확인
            {
                return; // 비어 있는 아이템 적재 중단
            }

            carryable.EnterInventory(cargoRoot); // 아이템 모델과 물리를 끄고 마차 보관 지점으로 이동
            secured.Add(carryable); // 확보된 아이템 목록에 추가

            Debug.Log($"[Wagon] 적재: {carryable.DisplayName} (전체 {SecuredCount}개, 보물 {SecuredTreasureCount}개, {SecuredValue}골드)"); // 적재 결과 출력
        }

        public void Leave(InventorySystem playerInventory = null) // 플레이어 소지품을 확보하고 던전 탈출 처리
        {
            if (left) // 이미 마차가 떠났는지 확인
            {
                return; // 중복 탈출 처리 방지
            }

            if (playerInventory != null) // 플레이어 인벤토리 존재 여부 확인
            {
                List<ICarryable> remainingItems = playerInventory.TakeAll(); // 살아서 탈출한 플레이어의 모든 소지품 가져오기

                foreach (ICarryable carryable in remainingItems) // 플레이어가 들고 있던 모든 아이템 순회
                {
                    Deposit(carryable); // 남은 아이템을 마차 확보 목록에 추가
                }
            }

            left = true; // 마차 탈출 완료 상태 저장
            Left?.Invoke(); // 결과 매니저에 마차 탈출 완료 전달

            Debug.Log($"[Wagon] 떠나기 — 전체 아이템 {SecuredCount}개, 보물 {SecuredTreasureCount}개, 총 {SecuredValue}골드"); // 최종 확보 결과 출력

            Time.timeScale = 0f; // 임시 결과 확인을 위해 게임 진행 정지
        }

        void OnGUI() // 마차 적재 상태와 임시 탈출 결과 표시
        {
            if (DebugUIToggleController.InventoryInfoVisible) // F2 마차 적재 디버그 정보 표시 상태 확인
            {
                GUI.Label( // 마차 확보 상태 표시
                    new Rect(10f, 150f, 700f, 20f), // 적재 정보 표시 위치와 크기
                    $"마차 적재: 전체 {SecuredCount}개 | 보물 {SecuredTreasureCount}개 | 총 가치 {SecuredValue}골드"); // 적재 정보 문구
            }

            if (left && (RunResultManager.Instance == null || !RunResultManager.Instance.HasResult)) // 중앙 결과 화면이 없을 때만 기존 성공 화면 표시
            {
                GUIStyle titleStyle = new GUIStyle(GUI.skin.label); // 탈출 성공 제목 스타일 생성
                GUIStyle messageStyle = new GUIStyle(GUI.skin.label); // 탈출 결과 상세 스타일 생성

                titleStyle.fontSize = 24; // 제목 글자 크기 설정
                titleStyle.alignment = TextAnchor.MiddleCenter; // 제목 중앙 정렬
                messageStyle.fontSize = 16; // 상세 글자 크기 설정
                messageStyle.alignment = TextAnchor.MiddleCenter; // 상세 문구 중앙 정렬

                GUI.Label( // 탈출 성공 제목 표시
                    new Rect(0f, Screen.height / 2f - 40f, Screen.width, 40f), // 제목 표시 위치와 크기
                    "던전 종료 — 탈출 성공!", // 탈출 성공 제목
                    titleStyle); // 제목 스타일 적용

                GUI.Label( // 확보 보물 결과 표시
                    new Rect(0f, Screen.height / 2f + 4f, Screen.width, 30f), // 확보 결과 표시 위치와 크기
                    $"확보 보물 {SecuredTreasureCount}개 · 총 {SecuredValue}골드", // 확보 결과 문구
                    messageStyle); // 상세 스타일 적용

                GUI.Label( // 임시 종료 안내 표시
                    new Rect(0f, Screen.height / 2f + 34f, Screen.width, 30f), // 안내 표시 위치와 크기
                    "(플레이 정지 — 마을 정산은 이후 단계)", // 임시 종료 안내 문구
                    messageStyle); // 상세 스타일 적용
            }
        }
    }
}