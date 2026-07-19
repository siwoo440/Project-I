using System.Collections; // 정산 창 종료 대기 코루틴 사용
using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class VillagePlayerSpawner : MonoBehaviour // 마을 도착 시 플레이어를 마차 위에 배치
    {
        [Header("플레이어 설정")] // Inspector 플레이어 설정 구분
        [SerializeField] PlayerController playerPrefab; // 마을에 생성할 플레이어 프리팹
        [SerializeField] Transform spawnPoint; // 플레이어가 배치될 마차 위 지점
        [SerializeField] bool reuseScenePlayer = true; // Scene에 있는 기존 플레이어 재사용 여부

        [Header("마을 UI")] // Inspector 마을 UI 설정 구분
        [SerializeField] VillageSettlementUI settlementUI; // 정산 창 상태 확인용 UI

        PlayerController currentPlayer; // 현재 마을에 배치된 플레이어
        PlayerInteractor currentInteractor; // 플레이어 상호작용 컴포넌트
        PlayerCombat currentCombat; // 플레이어 전투 컴포넌트

        bool playerControllerWasEnabled; // 기존 이동 컴포넌트 활성 상태
        bool interactorWasEnabled; // 기존 상호작용 컴포넌트 활성 상태
        bool combatWasEnabled; // 기존 전투 컴포넌트 활성 상태

        public PlayerController CurrentPlayer => currentPlayer; // 현재 마을 플레이어 반환

        void Reset() // 컴포넌트 추가 시 기본 참조 자동 검색
        {
            settlementUI = FindFirstObjectByType<VillageSettlementUI>(); // 현재 Scene의 정산 UI 검색
        }

        void Start() // 마을 Scene 시작 시 플레이어 생성과 배치 실행
        {
            SpawnOrMovePlayer(); // 플레이어를 마차 위 도착 지점에 배치
        }

        public void SpawnOrMovePlayer() // 기존 플레이어를 재사용하거나 새 플레이어를 생성
        {
            Transform targetPoint = spawnPoint != null ? spawnPoint : transform; // 실제 도착 위치 결정

            if (reuseScenePlayer) // 기존 Scene 플레이어 재사용 여부 확인
            {
                currentPlayer = FindFirstObjectByType<PlayerController>(); // 현재 Scene의 플레이어 검색
            }

            if (currentPlayer == null && playerPrefab != null) // 기존 플레이어가 없고 프리팹이 연결됐는지 확인
            {
                currentPlayer = Instantiate( // 새로운 플레이어 생성
                    playerPrefab, // 생성할 플레이어 프리팹 전달
                    targetPoint.position, // 마차 위 도착 위치 전달
                    targetPoint.rotation); // 마차 진행 방향 회전 전달
            }

            if (currentPlayer == null) // 플레이어 검색과 생성 결과 확인
            {
                Debug.LogError("[VillagePlayerSpawner] Player Prefab 또는 기존 Player가 없습니다."); // 플레이어 누락 오류 출력
                return; // 배치 처리 중단
            }

            currentInteractor = currentPlayer.GetComponent<PlayerInteractor>(); // 플레이어 상호작용 컴포넌트 가져오기
            currentCombat = currentPlayer.GetComponent<PlayerCombat>(); // 플레이어 전투 컴포넌트 가져오기
            CharacterController characterController = currentPlayer.GetComponent<CharacterController>(); // 플레이어 충돌 컨트롤러 가져오기

            playerControllerWasEnabled = currentPlayer.enabled; // 기존 이동 활성 상태 저장
            interactorWasEnabled = currentInteractor != null && currentInteractor.enabled; // 기존 상호작용 활성 상태 저장
            combatWasEnabled = currentCombat != null && currentCombat.enabled; // 기존 전투 활성 상태 저장

            currentPlayer.enabled = false; // 정산 중 플레이어 이동과 시점 입력 중단

            if (currentInteractor != null) // 상호작용 컴포넌트 존재 여부 확인
            {
                currentInteractor.enabled = false; // 정산 중 상호작용 입력 중단
            }

            if (currentCombat != null) // 전투 컴포넌트 존재 여부 확인
            {
                currentCombat.enabled = false; // 정산 중 공격 입력 중단
            }

            if (characterController != null) // CharacterController 존재 여부 확인
            {
                characterController.enabled = false; // 위치 변경 중 충돌 계산 일시 중단
            }

            currentPlayer.transform.SetParent(null); // 플레이어를 Scene 루트 오브젝트로 분리
            currentPlayer.transform.SetPositionAndRotation( // 플레이어 위치와 회전 적용
                targetPoint.position, // 마차 Floor 위 위치 적용
                targetPoint.rotation); // 마차 진행 방향 회전 적용

            if (characterController != null) // CharacterController 존재 여부 다시 확인
            {
                characterController.enabled = true; // 위치 변경 후 충돌 계산 복구
            }

            Cursor.lockState = CursorLockMode.None; // 정산 UI 조작을 위해 커서 잠금 해제
            Cursor.visible = true; // 마우스 커서 표시

            StartCoroutine(EnablePlayerAfterSettlementRoutine()); // 정산 창이 닫힐 때까지 입력 활성화 대기

            Debug.Log("[VillagePlayerSpawner] 플레이어를 마을 마차 위에 배치했습니다."); // 배치 완료 출력
        }

        IEnumerator EnablePlayerAfterSettlementRoutine() // 정산 완료 후 플레이어 조작 복구
        {
            yield return null; // 모든 마을 UI의 Start 실행까지 한 프레임 대기

            while (settlementUI != null && settlementUI.IsWindowOpen) // 정산 창이 열려 있는 동안 반복
            {
                Cursor.lockState = CursorLockMode.None; // 정산 중 커서 잠금 해제 유지
                Cursor.visible = true; // 정산 중 커서 표시 유지
                yield return null; // 다음 프레임까지 대기
            }

            if (currentPlayer == null) // 대기 중 플레이어가 제거됐는지 확인
            {
                yield break; // 입력 복구 중단
            }

            currentPlayer.enabled = playerControllerWasEnabled; // 기존 이동 컴포넌트 활성 상태 복구

            if (currentInteractor != null) // 상호작용 컴포넌트 존재 여부 확인
            {
                currentInteractor.enabled = interactorWasEnabled; // 기존 상호작용 활성 상태 복구
            }

            if (currentCombat != null) // 전투 컴포넌트 존재 여부 확인
            {
                currentCombat.enabled = combatWasEnabled; // 기존 전투 활성 상태 복구
            }

            Debug.Log("[VillagePlayerSpawner] 정산 종료 후 플레이어 조작을 활성화했습니다."); // 입력 활성화 출력
        }
    }
}