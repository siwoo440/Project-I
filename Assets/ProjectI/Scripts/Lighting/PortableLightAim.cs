using ProjectI.Items; // WorldItem 운반 상태 참조
using UnityEngine; // 카메라와 Transform 기능 참조

namespace ProjectI.Lighting // 휴대 조명 기능 네임스페이스
{
    [RequireComponent(typeof(WorldItem))] // 현재 조명이 손에 들려 있는지 판정할 WorldItem 필수 지정
    public sealed class PortableLightAim : MonoBehaviour // 랜턴 빔을 플레이어 화면 정중앙 방향으로 보정
    {
        [SerializeField] private Transform beamOrigin; // 실제 Spot Light가 붙어 있는 빔 시작 Transform
        [SerializeField] private float aimDistance = 24f; // 카메라 정중앙에서 조준할 전방 거리
        private WorldItem worldItem; // 현재 손 운반 상태 확인용 WorldItem
        private Camera targetCamera; // 플레이어 1인칭 카메라 참조
        private bool wasHeld; // 직전 프레임 손 운반 여부

        private void Awake() // 중앙 조준 기능 초기화
        {
            ResolveReferences(); // WorldItem과 플레이어 카메라 참조 확보
        }

        private void LateUpdate() // 카메라 회전이 끝난 뒤 랜턴 빔 방향 보정
        {
            ResolveReferences(); // 런타임 참조 손실에 대비해 필요한 참조 확보

            if (beamOrigin == null || worldItem == null) // 빔 시작점 또는 월드 아이템 참조 누락 확인
            {
                return; // 방향 보정 중단
            }

            if (!worldItem.IsHeld) // 현재 플레이어 손에 들고 있지 않은지 확인
            {
                if (wasHeld) // 바로 전 프레임까지 손에 들고 있었는지 확인
                {
                    beamOrigin.localRotation = Quaternion.identity; // 바닥에 내려놓으면 아이템 자체 정면을 비추도록 로컬 회전 복원
                }

                wasHeld = false; // 현재 손 운반 상태 저장
                return; // 월드에 놓인 조명은 플레이어 카메라를 추적하지 않음
            }

            if (targetCamera == null) // 플레이어 카메라를 아직 찾지 못했는지 확인
            {
                return; // 카메라가 없으면 현재 아이템 정면 방향 유지
            }

            Vector3 aimPoint = targetCamera.transform.position + (targetCamera.transform.forward * aimDistance); // 플레이어 화면 정중앙의 먼 전방 조준점 계산
            Vector3 beamDirection = aimPoint - beamOrigin.position; // 실제 랜턴 위치에서 화면 정중앙으로 향하는 방향 계산

            if (beamDirection.sqrMagnitude > 0.0001f) // 유효한 조준 방향인지 확인
            {
                beamOrigin.rotation = Quaternion.LookRotation(beamDirection.normalized, targetCamera.transform.up); // 랜턴이 화면 아래·오른쪽에 있어도 빔 중심을 카메라 정중앙으로 맞춤
            }

            wasHeld = true; // 현재 손 운반 상태 저장
        }

        public void Configure(Transform targetBeamOrigin, float targetAimDistance) // 에디터 자동 설정용 빔 시작점과 조준 거리 지정
        {
            beamOrigin = targetBeamOrigin; // Spot Light 시작 Transform 저장
            aimDistance = Mathf.Max(1f, targetAimDistance); // 전방 조준 거리 최소값 보정
            ResolveReferences(); // 구성 직후 필수 참조 확보
        }

        private void ResolveReferences() // WorldItem과 카메라 참조 확보
        {
            if (worldItem == null) // WorldItem 참조 누락 확인
            {
                worldItem = GetComponent<WorldItem>(); // 같은 휴대 조명 루트의 WorldItem 조회
            }

            if (targetCamera == null) // 플레이어 카메라 참조 누락 확인
            {
                targetCamera = Camera.main; // MainCamera 태그가 지정된 1인칭 카메라 우선 조회
            }

            if (targetCamera == null) // MainCamera 태그로 찾지 못했는지 확인
            {
                targetCamera = Object.FindFirstObjectByType<Camera>(); // 현재 씬의 첫 활성 카메라를 안전 대체로 조회
            }
        }

        private void OnValidate() // 인스펙터 값 검증
        {
            aimDistance = Mathf.Max(1f, aimDistance); // 조준 거리 최소값 보정
        }
    }
}
