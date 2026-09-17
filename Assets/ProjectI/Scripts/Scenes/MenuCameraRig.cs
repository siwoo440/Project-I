using System; // 콜백
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Scenes // 씬 기능 네임스페이스
{
    public enum MenuCameraPose // 메인 메뉴 카메라 위치
    {
        Main, // 거리 전경
        Servers, // 전신국 앞 (서버 목록)
    }

    public sealed class MenuCameraRig : MonoBehaviour // 메인 메뉴 카메라 이동 (부드럽게 옮긴 뒤 알림)
    {
        [SerializeField] private Camera targetCamera; // 움직일 카메라
        [SerializeField] private Transform mainPose; // 거리 전경 위치
        [SerializeField] private Transform serverPose; // 서버 목록 위치
        [SerializeField] private float moveDuration = 1.6f; // 이동 시간
        [SerializeField] private float swayAmount = 0.12f; // 제자리 흔들림 (m)
        [SerializeField] private float swaySpeed = 0.25f; // 흔들림 속도
        private Vector3 fromPosition; // 출발 위치
        private Quaternion fromRotation; // 출발 방향
        private Transform target; // 목표 자세
        private float progress = 1f; // 진행도 (1 = 도착)
        private Action arrived; // 도착 콜백

        public MenuCameraPose CurrentPose { get; private set; } = MenuCameraPose.Main; // 현재(또는 향하는) 자세
        public bool IsMoving => progress < 1f; // 이동 중

        public void Configure(Camera cameraToMove, Transform main, Transform servers) // 에디터 배치 도구용
        {
            targetCamera = cameraToMove; // 카메라
            mainPose = main; // 전경
            serverPose = servers; // 서버
        }

        private void Awake() // 준비
        {
            if (targetCamera == null) // 미지정
            {
                targetCamera = GetComponentInChildren<Camera>(); // 자식 카메라
            }
        }

        public void SnapTo(MenuCameraPose pose) // 즉시 이동
        {
            Transform poseTransform = PoseTransform(pose); // 자세

            if (targetCamera == null || poseTransform == null) // 설정 누락
            {
                return; // 생략
            }

            CurrentPose = pose; // 기록
            target = poseTransform; // 목표
            progress = 1f; // 도착
            targetCamera.transform.SetPositionAndRotation(poseTransform.position, poseTransform.rotation); // 적용
        }

        public void MoveTo(MenuCameraPose pose, Action onArrived) // 부드럽게 이동
        {
            Transform poseTransform = PoseTransform(pose); // 자세

            if (targetCamera == null || poseTransform == null) // 설정 누락
            {
                onArrived?.Invoke(); // 바로 알림
                return; // 종료
            }

            CurrentPose = pose; // 기록
            fromPosition = targetCamera.transform.position; // 출발
            fromRotation = targetCamera.transform.rotation; // 출발
            target = poseTransform; // 목표
            progress = 0f; // 시작
            arrived = onArrived; // 콜백
        }

        private void LateUpdate() // 이동·흔들림
        {
            if (targetCamera == null || target == null) // 준비 안 됨
            {
                return; // 생략
            }

            Transform view = targetCamera.transform; // 카메라

            if (progress < 1f) // 이동 중
            {
                progress = Mathf.Min(1f, progress + (Time.unscaledDeltaTime / Mathf.Max(0.05f, moveDuration))); // 진행
                float eased = progress * progress * (3f - (2f * progress)); // 부드럽게 시작·정지
                view.SetPositionAndRotation(Vector3.Lerp(fromPosition, SwayedPosition(), eased), Quaternion.Slerp(fromRotation, target.rotation, eased)); // 적용

                if (progress >= 1f) // 도착
                {
                    Action callback = arrived; // 콜백
                    arrived = null; // 정리
                    callback?.Invoke(); // 알림
                }

                return; // 종료
            }

            view.SetPositionAndRotation(SwayedPosition(), target.rotation); // 제자리 흔들림
        }

        private Vector3 SwayedPosition() // 숨 쉬듯 약간 흔들리는 위치
        {
            float time = Time.unscaledTime * swaySpeed; // 시간
            Vector3 offset = (target.right * Mathf.Sin(time) * swayAmount) + (target.up * Mathf.Sin(time * 1.7f) * swayAmount * 0.4f); // 흔들림
            return target.position + offset; // 위치
        }

        private Transform PoseTransform(MenuCameraPose pose) // 자세 조회
        {
            return pose == MenuCameraPose.Servers ? serverPose : mainPose; // 반환
        }
    }
}
