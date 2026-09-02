using System.Collections.Generic; // Cargo 기록 목록 기능 참조
using ProjectI.Items; // WorldItem 기능 참조
using ProjectI.Wagon; // WagonCargoArea 기능 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.SceneManagement; // 씬 소속 이동 기능 참조

namespace ProjectI.Loop // 원정 루프 기능 네임스페이스
{
    [DisallowMultipleComponent] // 동일 마차에 중복 Cargo 보존 컴포넌트 방지
    public sealed class WagonCargoPersistence : MonoBehaviour // 마차 내부 실제 WorldItem을 재생성 없이 보존하는 관리자
    {
        [SerializeField] private Transform wagonRoot; // 이동 기준이 되는 Persistent Wagon 루트
        [SerializeField] private WagonCargoArea cargoArea; // 실제 마차 짐칸 판정 영역
        private readonly List<CapturedCargoRecord> capturedCargo = new List<CapturedCargoRecord>(); // 이동 중 고정할 실제 아이템 기록
        private bool cargoLockedForTravel; // 현재 마차 이동 중 Cargo 물리 잠금 여부

        public int CapturedCount => capturedCargo.Count; // 현재 이동에 포함된 실제 Cargo 개수 공개
        public bool CargoLockedForTravel => cargoLockedForTravel; // 현재 Cargo 이동 잠금 상태 공개

        private void Awake() // 런타임 초기 참조 연결
        {
            ResolveReferences(); // Wagon과 CargoArea 자동 탐색
        }

        private void LateUpdate() // 이동이 끝난 뒤 Persistent 씬의 느슨한 아이템 소속 정리
        {
            if (cargoLockedForTravel) // Cargo가 마차와 함께 이동 중인지 확인
            {
                return; // 이동 중에는 씬 소속을 변경하지 않음
            }

            RehomeLoosePersistentWorldItems(); // 마차 밖에 내려놓은 아이템을 현재 맵 씬으로 반환
        }

        public void Configure(Transform targetWagonRoot, WagonCargoArea targetCargoArea) // Editor 자동 설정용 참조 지정
        {
            wagonRoot = targetWagonRoot; // Persistent Wagon 루트 저장
            cargoArea = targetCargoArea; // CargoArea 저장
            ResolveReferences(); // 누락 참조 보정
        }

        public void CaptureCargoForTravel() // 현재 짐칸 안의 실제 WorldItem을 이동 대상으로 확보
        {
            ResolveReferences(); // 이동 직전 최신 참조 확보
            ReleaseCargoAfterTravel(); // 이전 이동이 비정상 종료됐다면 물리 상태 먼저 복구
            capturedCargo.Clear(); // 이전 이동 기록 초기화

            if (wagonRoot == null || cargoArea == null) // 필수 마차 참조 확인
            {
                Debug.LogError("[Project I] Cargo 보존 준비 실패 / Wagon 또는 CargoArea 누락", this); // 누락 상태 로그 출력
                return; // Cargo 캡처 중단
            }

            BoxCollider cargoTrigger = cargoArea.GetComponent<BoxCollider>(); // 실제 Cargo Trigger 조회

            if (cargoTrigger == null) // Cargo Trigger 존재 여부 확인
            {
                Debug.LogError("[Project I] Cargo 보존 준비 실패 / BoxCollider 누락", this); // 누락 Trigger 로그 출력
                return; // Cargo 캡처 중단
            }

            WorldItem[] worldItems = Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 로드된 모든 WorldItem 조회
            Scene persistentScene = wagonRoot.gameObject.scene; // Wagon이 속한 Persistent 씬 확보

            foreach (WorldItem item in worldItems) // 모든 WorldItem 순회
            {
                if (item == null || !item.gameObject.activeInHierarchy || item.IsHeld || item.IsStored) // 월드 Cargo 대상 여부 확인
                {
                    continue; // 비활성 또는 플레이어 소유 아이템 제외
                }

                if (!ContainsPoint(cargoTrigger, item.transform.position)) // 실제 짐칸 내부 여부 확인
                {
                    continue; // Cargo 밖 아이템 제외
                }

                CaptureItem(item, persistentScene); // 동일 GameObject를 Persistent Cargo로 확보
            }

            cargoLockedForTravel = capturedCargo.Count > 0; // 실제 확보 항목이 있을 때 이동 잠금 활성화
            SyncCapturedCargoToWagon(); // 캡처 직후 Wagon 로컬 위치에 정확히 고정
            Debug.Log($"[Project I] Cargo 물리 보존 캡처 / Count={capturedCargo.Count}", this); // 캡처 결과 로그 출력
        }

        public void SyncCapturedCargoToWagon() // Wagon 이동 프레임마다 실제 Cargo Transform 동기화
        {
            if (wagonRoot == null) // Wagon 기준 누락 확인
            {
                return; // 동기화 중단
            }

            foreach (CapturedCargoRecord record in capturedCargo) // 캡처된 실제 Cargo 순회
            {
                if (record.Item == null) // 이동 중 제거된 아이템 확인
                {
                    continue; // 유효하지 않은 아이템 제외
                }

                Vector3 worldPosition = wagonRoot.TransformPoint(record.LocalPosition); // 저장한 Wagon 로컬 위치를 현재 월드 위치로 변환
                Quaternion worldRotation = wagonRoot.rotation * record.LocalRotation; // 저장한 Wagon 로컬 회전을 현재 월드 회전으로 변환
                record.Item.transform.SetPositionAndRotation(worldPosition, worldRotation); // 동일 실제 GameObject 위치와 회전 갱신
            }
        }

        public void ReleaseCargoAfterTravel() // 도착 후 Cargo의 원래 Rigidbody 상태를 복구
        {
            if (capturedCargo.Count == 0) // 복구할 Cargo 존재 여부 확인
            {
                cargoLockedForTravel = false; // 이동 잠금 상태 해제
                return; // 추가 복구 불필요
            }

            SyncCapturedCargoToWagon(); // 최종 Wagon 위치 기준으로 Cargo Transform 확정

            foreach (CapturedCargoRecord record in capturedCargo) // 캡처된 Cargo 순회
            {
                RestoreBodyState(record); // 각 아이템 Rigidbody 상태 복구
            }

            int releasedCount = capturedCargo.Count; // 로그용 복구 개수 저장
            capturedCargo.Clear(); // 이동 전용 기록 제거
            cargoLockedForTravel = false; // Cargo 이동 잠금 해제
            Debug.Log($"[Project I] Cargo 물리 보존 해제 / Count={releasedCount}", this); // 복구 결과 로그 출력
        }

        private void CaptureItem(WorldItem item, Scene persistentScene) // 실제 WorldItem 하나를 이동 Cargo로 전환
        {
            Rigidbody body = item.Body != null ? item.Body : item.GetComponent<Rigidbody>(); // 아이템 Rigidbody 조회
            CapturedCargoRecord record = new CapturedCargoRecord // 동일 아이템의 Wagon 상대 위치와 물리 상태 기록 생성
            {
                Item = item, // 실제 WorldItem 참조 저장
                Body = body, // 실제 Rigidbody 참조 저장
                LocalPosition = wagonRoot.InverseTransformPoint(item.transform.position), // Wagon 기준 로컬 위치 저장
                LocalRotation = Quaternion.Inverse(wagonRoot.rotation) * item.transform.rotation, // Wagon 기준 로컬 회전 저장
                IsKinematic = body != null && body.isKinematic, // 기존 Kinematic 상태 저장
                UseGravity = body != null && body.useGravity, // 기존 중력 상태 저장
                DetectCollisions = body == null || body.detectCollisions, // 기존 충돌 감지 상태 저장
                Interpolation = body == null ? RigidbodyInterpolation.None : body.interpolation, // 기존 보간 상태 저장
                Constraints = body == null ? RigidbodyConstraints.None : body.constraints // 기존 물리 제한 저장
            };

            if (item.transform.parent != null) // Cargo 월드 아이템에 부모가 남아있는지 확인
            {
                item.transform.SetParent(null, true); // 월드 Transform을 유지한 채 루트 GameObject로 분리
            }

            if (persistentScene.IsValid() && persistentScene.isLoaded && item.gameObject.scene != persistentScene) // 아이템이 환경 맵 씬에 남아있는지 확인
            {
                SceneManager.MoveGameObjectToScene(item.gameObject, persistentScene); // 동일 GameObject를 Persistent 씬 소속으로 이동
            }

            if (body != null) // Rigidbody 존재 여부 확인
            {
                if (!body.isKinematic) // Dynamic Rigidbody인지 확인
                {
                    body.linearVelocity = Vector3.zero; // 이동 직전 직선 속도 제거
                    body.angularVelocity = Vector3.zero; // 이동 직전 회전 속도 제거
                }

                body.detectCollisions = false; // 암전 이동 중 충돌 반응 정지
                body.useGravity = false; // 암전 이동 중 중력 정지
                body.isKinematic = true; // Wagon 이동 동안 물리 힘으로 이탈하지 않도록 고정
                body.interpolation = RigidbodyInterpolation.None; // 직접 Transform 동기화 중 물리 보간 비활성화
            }

            capturedCargo.Add(record); // 실제 Cargo 이동 기록에 추가
        }

        private static void RestoreBodyState(CapturedCargoRecord record) // 캡처 이전 Rigidbody 설정 복구
        {
            if (record.Item == null || record.Body == null) // 복구 대상 유효성 확인
            {
                return; // 유효하지 않으면 복구 중단
            }

            Rigidbody body = record.Body; // 실제 Rigidbody 참조 사용
            body.constraints = record.Constraints; // 기존 물리 제한 복구
            body.interpolation = record.Interpolation; // 기존 보간 상태 복구
            body.useGravity = record.UseGravity; // 기존 중력 상태 복구
            body.detectCollisions = record.DetectCollisions; // 기존 충돌 감지 상태 복구
            body.isKinematic = record.IsKinematic; // 기존 Kinematic 상태 복구

            if (!body.isKinematic) // Dynamic 상태로 돌아온 경우 확인
            {
                body.linearVelocity = Vector3.zero; // 도착 순간 불필요한 관성 제거
                body.angularVelocity = Vector3.zero; // 도착 순간 불필요한 회전 관성 제거
            }
        }

        private void RehomeLoosePersistentWorldItems() // Persistent 씬에서 마차 밖에 놓인 월드 아이템을 현재 맵으로 반환
        {
            ResolveReferences(); // 최신 Wagon/Cargo 참조 확보

            if (wagonRoot == null || cargoArea == null) // 필수 참조 확인
            {
                return; // 씬 소속 정리 중단
            }

            Scene persistentScene = wagonRoot.gameObject.scene; // Persistent 씬 조회
            Scene activeScene = SceneManager.GetActiveScene(); // 현재 Office 또는 Dungeon 씬 조회

            if (!persistentScene.IsValid() || !persistentScene.isLoaded || !activeScene.IsValid() || !activeScene.isLoaded || activeScene == persistentScene) // 유효한 환경 맵 여부 확인
            {
                return; // 맵 전환 중에는 소속 변경 중단
            }

            BoxCollider cargoTrigger = cargoArea.GetComponent<BoxCollider>(); // 현재 Cargo Trigger 조회

            if (cargoTrigger == null) // Cargo Trigger 누락 확인
            {
                return; // 안전하게 정리 중단
            }

            WorldItem[] worldItems = Object.FindObjectsByType<WorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 WorldItem 전체 조회

            foreach (WorldItem item in worldItems) // 모든 아이템 순회
            {
                if (item == null || item.gameObject.scene != persistentScene || item.IsHeld || item.IsStored) // Persistent 루트 월드 아이템 대상 확인
                {
                    continue; // 플레이어 소유 또는 다른 씬 아이템 제외
                }

                if (ContainsPoint(cargoTrigger, item.transform.position)) // 아직 마차 Cargo 내부인지 확인
                {
                    continue; // Cargo 아이템은 Persistent 씬에 계속 유지
                }

                if (item.transform.parent != null) // 루트가 아닌 아이템 확인
                {
                    continue; // 임의 계층 구조는 안전을 위해 변경하지 않음
                }

                SceneManager.MoveGameObjectToScene(item.gameObject, activeScene); // 마차 밖에 내려놓은 실제 아이템을 현재 맵 씬으로 반환
            }
        }

        private void ResolveReferences() // Wagon과 CargoArea 자동 탐색
        {
            if (cargoArea == null) // CargoArea 참조 누락 확인
            {
                cargoArea = GetComponentInChildren<WagonCargoArea>(true); // 현재 Wagon 계층에서 CargoArea 조회
            }

            if (wagonRoot == null && cargoArea != null) // Wagon 루트 참조 누락 확인
            {
                wagonRoot = cargoArea.transform.root; // CargoArea 기준 최상위 Wagon 루트 사용
            }
        }

        private static bool ContainsPoint(BoxCollider trigger, Vector3 worldPoint) // 회전된 Cargo Box 내부 점 판정
        {
            if (trigger == null) // Trigger 유효성 확인
            {
                return false; // 내부 판정 실패
            }

            Vector3 localPoint = trigger.transform.InverseTransformPoint(worldPoint) - trigger.center; // 월드 위치를 Trigger 로컬 좌표로 변환
            Vector3 halfSize = trigger.size * 0.5f; // Trigger 반크기 계산
            return Mathf.Abs(localPoint.x) <= halfSize.x && Mathf.Abs(localPoint.y) <= halfSize.y && Mathf.Abs(localPoint.z) <= halfSize.z; // 세 축 모두 내부면 Cargo로 판정
        }

        private sealed class CapturedCargoRecord // 이동 중 실제 Cargo 상태 기록
        {
            public WorldItem Item; // 동일 실제 WorldItem 참조
            public Rigidbody Body; // 동일 실제 Rigidbody 참조
            public Vector3 LocalPosition; // Wagon 기준 로컬 위치
            public Quaternion LocalRotation; // Wagon 기준 로컬 회전
            public bool IsKinematic; // 기존 Kinematic 상태
            public bool UseGravity; // 기존 중력 사용 상태
            public bool DetectCollisions; // 기존 충돌 감지 상태
            public RigidbodyInterpolation Interpolation; // 기존 Rigidbody 보간 설정
            public RigidbodyConstraints Constraints; // 기존 Rigidbody 제한 설정
        }
    }
}
