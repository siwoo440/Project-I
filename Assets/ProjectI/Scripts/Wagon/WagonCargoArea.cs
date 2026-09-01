using System.Collections.Generic; // 확보 아이템·죽은 플레이어 집합과 정리 버퍼 기능 참조
using ProjectI.Items; // 기존 WorldItem 기능 참조
using ProjectI.Player; // 죽은 플레이어 래그돌 회수 상태 기능 참조
using UnityEngine; // 유니티 물리 Trigger와 Transform 기능 참조

namespace ProjectI.Wagon // 마차 시스템 네임스페이스
{
    [RequireComponent(typeof(BoxCollider))] // 공통 적재·회수 판정용 Trigger BoxCollider 필수 지정
    public sealed class WagonCargoArea : MonoBehaviour // 마차 뒤 대형 창고 전체에서 회수품과 죽은 플레이어를 동일 영역으로 판정
    {
        [SerializeField] private BoxCollider cargoTrigger; // 실제 공통 창고 판정 범위
        private readonly HashSet<WorldItem> securedItems = new HashSet<WorldItem>(); // 현재 확보된 월드 아이템 집합
        private readonly List<WorldItem> itemCleanupBuffer = new List<WorldItem>(); // 아이템 확보 해제용 임시 목록
        private readonly HashSet<PlayerDeathController> recoveredPlayers = new HashSet<PlayerDeathController>(); // 현재 같은 창고에 회수된 죽은 플레이어 집합
        private readonly List<PlayerDeathController> playerCleanupBuffer = new List<PlayerDeathController>(); // 죽은 플레이어 회수 해제용 임시 목록

        public int SecuredCount => securedItems.Count; // 현재 확보된 회수품 개수 공개
        public int RecoveredPlayerCount => recoveredPlayers.Count; // 같은 영역에 회수된 죽은 플레이어 개수 공개

        private void Awake() // 공통 창고 영역 초기화
        {
            ResolveTrigger(); // BoxCollider 참조 확보
        }

        private void FixedUpdate() // 회수품과 죽은 플레이어의 현재 창고 포함 상태를 물리 프레임마다 정리
        {
            CleanupItems(); // 기존 회수품 확보 상태 갱신
            CleanupRecoveredPlayers(); // 죽은 플레이어 회수 상태 갱신
            DiscoverRecoveredPlayers(); // Kinematic 래그돌도 같은 CargoArea 안에 있으면 회수 처리
        }

        public void Configure(BoxCollider trigger) // 에디터 프리팹 생성·보정용 공통 Trigger 지정
        {
            cargoTrigger = trigger; // 공통 창고 Trigger 참조 저장

            if (cargoTrigger != null) // 유효 Trigger 확인
            {
                cargoTrigger.isTrigger = true; // 물리 충돌 대신 회수 상태 판정만 사용
            }
        }

        public bool IsSecured(WorldItem item) // 특정 회수품의 현재 확보 여부 반환
        {
            return item != null && securedItems.Contains(item); // 확보 집합 포함 여부 반환
        }

        public bool IsRecovered(PlayerDeathController player) // 특정 죽은 플레이어의 현재 회수 여부 반환
        {
            return player != null && recoveredPlayers.Contains(player); // 같은 공통 영역 회수 집합 포함 여부 반환
        }

        public void Release(WorldItem item) // 특정 회수품의 현재 마차 확보 상태 해제
        {
            if (item == null) // 이미 파괴된 아이템 여부 확인
            {
                securedItems.Remove(item); // null 엔트리 제거 시도
                return; // 추가 상태 처리 중단
            }

            securedItems.Remove(item); // 현재 공통 창고 확보 집합에서 제거
            WagonCargoItemState state = item.GetComponent<WagonCargoItemState>(); // 아이템 확보 상태 컴포넌트 조회

            if (state != null && state.SecuredArea == this) // 현재 마차가 부여한 상태인지 확인
            {
                state.SetSecured(null, false); // 아이템 확보 상태 해제
            }
        }

        public void Release(PlayerDeathController player) // 특정 죽은 플레이어의 현재 마차 회수 상태 해제
        {
            if (player == null) // 이미 파괴된 플레이어 참조 여부 확인
            {
                recoveredPlayers.Remove(player); // null 엔트리 제거 시도
                return; // 추가 상태 처리 중단
            }

            recoveredPlayers.Remove(player); // 현재 공통 창고 회수 집합에서 제거

            if (player.RecoveredArea == this) // 현재 마차가 부여한 회수 상태인지 확인
            {
                player.SetRecovered(null, false); // 죽은 플레이어를 미회수 상태로 전환
            }
        }

        private void OnTriggerEnter(Collider other) // 물체가 공통 창고 Trigger에 들어온 순간 처리
        {
            TrySecureItem(other); // 기존 WorldItem 확보 시도
            TryRecoverPlayer(other); // 같은 Collider에서 죽은 플레이어 회수도 시도
        }

        private void OnTriggerStay(Collider other) // Trigger 내부에서 활성화되거나 내려놓은 물체 상태 보정
        {
            TrySecureItem(other); // G 내려놓기 직후 회수품 확보 재시도
            TryRecoverPlayer(other); // 래그돌이 같은 영역에서 멈춘 경우 회수 재시도
        }

        private void OnTriggerExit(Collider other) // 물체가 공통 창고 밖으로 나간 순간 처리
        {
            WorldItem item = other == null ? null : other.GetComponentInParent<WorldItem>(); // 이탈 Collider에서 기존 WorldItem 조회

            if (item != null) // 유효 월드 아이템 확인
            {
                Release(item); // 창고 밖으로 나가면 회수품 미확보 상태로 복귀
            }

            PlayerDeathController player = other == null ? null : other.GetComponentInParent<PlayerDeathController>(); // 이탈 Collider에서 죽은 플레이어 루트 조회

            if (player != null) // 유효 플레이어 참조 확인
            {
                Release(player); // 같은 창고 밖으로 나가면 죽은 플레이어 미회수 상태로 복귀
            }
        }

        private void OnDisable() // 마차 비활성화 시 모든 확보·회수 상태 정리
        {
            itemCleanupBuffer.Clear(); // 아이템 정리 버퍼 초기화
            itemCleanupBuffer.AddRange(securedItems); // 현재 확보 아이템 전체 복사

            foreach (WorldItem item in itemCleanupBuffer) // 확보 아이템 순회
            {
                Release(item); // 모든 회수품 확보 상태 해제
            }

            playerCleanupBuffer.Clear(); // 플레이어 정리 버퍼 초기화
            playerCleanupBuffer.AddRange(recoveredPlayers); // 현재 회수 플레이어 전체 복사

            foreach (PlayerDeathController player in playerCleanupBuffer) // 회수된 죽은 플레이어 순회
            {
                Release(player); // 모든 죽은 플레이어 회수 상태 해제
            }
        }

        private void CleanupItems() // 현재 확보된 회수품이 같은 창고 조건을 유지하는지 갱신
        {
            itemCleanupBuffer.Clear(); // 이전 정리 후보 초기화

            foreach (WorldItem item in securedItems) // 현재 확보 아이템 순회
            {
                if (item == null || item.IsHeld || item.IsStored || !ContainsPoint(item.transform.position)) // 확보 조건이 깨졌는지 확인
                {
                    itemCleanupBuffer.Add(item); // 확보 해제 대상에 등록
                }
            }

            foreach (WorldItem item in itemCleanupBuffer) // 정리 대상 순회
            {
                Release(item); // 확보 상태 해제
            }
        }

        private void CleanupRecoveredPlayers() // 현재 회수된 죽은 플레이어가 같은 창고 조건을 유지하는지 갱신
        {
            playerCleanupBuffer.Clear(); // 이전 정리 후보 초기화

            foreach (PlayerDeathController player in recoveredPlayers) // 현재 회수된 플레이어 순회
            {
                if (player == null || !player.IsDead || player.RagdollCenter == null || !ContainsPoint(player.RagdollCenter.position)) // 사망·위치 조건이 깨졌는지 확인
                {
                    playerCleanupBuffer.Add(player); // 회수 해제 대상에 등록
                }
            }

            foreach (PlayerDeathController player in playerCleanupBuffer) // 정리 대상 순회
            {
                Release(player); // 죽은 플레이어 회수 상태 해제
            }
        }

        private void DiscoverRecoveredPlayers() // 물리가 정지된 래그돌까지 동일 CargoArea 기준으로 회수 탐색
        {
            PlayerDeathController[] players = Object.FindObjectsByType<PlayerDeathController>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 씬 플레이어 사망 컨트롤러 전체 조회

            foreach (PlayerDeathController player in players) // 모든 플레이어 순회
            {
                if (player == null || !player.IsDead || player.RagdollCenter == null || !ContainsPoint(player.RagdollCenter.position)) // 동일 창고 회수 조건 확인
                {
                    continue; // 생존·창고 밖 플레이어 건너뜀
                }

                RecoverPlayer(player); // 같은 공통 CargoArea에 죽은 플레이어 회수 등록
            }
        }

        private void TrySecureItem(Collider source) // Trigger Collider에서 기존 WorldItem 확보 시도
        {
            if (source == null) // Collider 유효성 확인
            {
                return; // 확보 처리 중단
            }

            WorldItem item = source.GetComponentInParent<WorldItem>(); // Collider 부모 방향에서 기존 WorldItem 조회

            if (item == null || item.IsHeld || item.IsStored || !ContainsPoint(item.transform.position)) // 월드 상태와 실제 창고 내부 여부 확인
            {
                return; // 확보 조건 미충족
            }

            WagonCargoItemState state = item.GetComponent<WagonCargoItemState>(); // 기존 확보 상태 컴포넌트 조회

            if (state == null) // Day21 이전 아이템이라 상태 컴포넌트가 없는지 확인
            {
                state = item.gameObject.AddComponent<WagonCargoItemState>(); // 기존 WorldItem에 최소 상태 컴포넌트 추가
            }

            if (state.SecuredArea != null && state.SecuredArea != this) // 다른 마차에서 확보 중인지 확인
            {
                state.SecuredArea.Release(item); // 이전 마차 확보 상태 먼저 해제
            }

            securedItems.Add(item); // 현재 공통 창고 확보 집합에 등록
            state.SetSecured(this, true); // 회수품을 확보 상태로 표시
        }

        private void TryRecoverPlayer(Collider source) // Trigger Collider에서 죽은 플레이어 회수 시도
        {
            if (source == null) // Collider 유효성 확인
            {
                return; // 회수 처리 중단
            }

            PlayerDeathController player = source.GetComponentInParent<PlayerDeathController>(); // 래그돌 Collider 부모에서 기존 Player 루트 조회

            if (player == null || !player.IsDead || player.RagdollCenter == null || !ContainsPoint(player.RagdollCenter.position)) // 죽은 플레이어와 동일 창고 내부 여부 확인
            {
                return; // 회수 조건 미충족
            }

            RecoverPlayer(player); // 공통 영역 죽은 플레이어 회수 등록
        }

        private void RecoverPlayer(PlayerDeathController player) // 현재 CargoArea에 죽은 플레이어를 회수 상태로 등록
        {
            if (player.RecoveredArea != null && player.RecoveredArea != this) // 다른 마차 공통 영역에 이미 회수됐는지 확인
            {
                player.RecoveredArea.Release(player); // 이전 마차 회수 상태 해제
            }

            recoveredPlayers.Add(player); // 현재 같은 창고 회수 집합에 등록
            player.SetRecovered(this, true); // PlayerDeathController에 현재 마차 회수 상태 기록
        }

        private bool ContainsPoint(Vector3 worldPoint) // 회수품과 죽은 플레이어 모두 사용하는 하나의 BoxCollider 내부 점 판정
        {
            ResolveTrigger(); // 공통 Trigger 참조 확보

            if (cargoTrigger == null) // 필요한 Trigger 누락 확인
            {
                return false; // 내부 판정 실패
            }

            Vector3 localPoint = cargoTrigger.transform.InverseTransformPoint(worldPoint) - cargoTrigger.center; // 월드 중심점을 Trigger 로컬 좌표로 변환
            Vector3 halfSize = cargoTrigger.size * 0.5f; // 로컬 Trigger 반크기 계산
            return Mathf.Abs(localPoint.x) <= halfSize.x && Mathf.Abs(localPoint.y) <= halfSize.y && Mathf.Abs(localPoint.z) <= halfSize.z; // 세 축 모두 범위 안이면 같은 회수 영역으로 판정
        }

        private void ResolveTrigger() // 같은 GameObject의 공통 BoxCollider 자동 연결
        {
            if (cargoTrigger == null) // 인스펙터 참조 누락 확인
            {
                cargoTrigger = GetComponent<BoxCollider>(); // 현재 CargoArea BoxCollider 자동 조회
            }

            if (cargoTrigger != null) // 유효 Collider 확인
            {
                cargoTrigger.isTrigger = true; // 항상 하나의 Trigger 영역으로 유지
            }
        }
    }
}
