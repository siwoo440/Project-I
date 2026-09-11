using System.Collections.Generic; // 문제 목록 기능 참조
using ProjectI.Diagnostics; // F1 디버그 창 관리자 참조
using ProjectI.Items; // 플레이어 인벤토리·빠른 슬롯 HUD 참조
using ProjectI.Persistence; // 저장 서비스 참조
using ProjectI.Wagon; // 마차 적재칸 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.EventSystems; // UI 입력 처리기 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.Loop // 원정 루프 기능 네임스페이스
{
    public static class EnvironmentSceneGuard // Office·던전 같은 환경 씬에 전역 시스템이 섞여 들어오지 않도록 검사·차단
    {
        public static List<string> Inspect(Scene environmentScene, Scene persistentScene, bool disableViolations) // 환경 씬 규칙 위반 목록 반환 (필요 시 비활성화)
        {
            List<string> violations = new List<string>(); // 위반 목록

            if (!environmentScene.IsValid() || !environmentScene.isLoaded) // 검사 대상 확인
            {
                return violations; // 검사 생략
            }

            bool persistentHasEventSystem = HasComponentInScene<EventSystem>(persistentScene); // Persistent UI 입력 처리기 존재 여부
            bool persistentHasListener = HasComponentInScene<AudioListener>(persistentScene); // Persistent 오디오 리스너 존재 여부
            int anchorCount = 0; // 마차 이동 지점 개수

            foreach (GameObject root in environmentScene.GetRootGameObjects()) // 환경 씬 루트 순회
            {
                if (root.name == QuickSlotHud.CanvasName) // 플레이어 빠른 슬롯 HUD Canvas 확인
                {
                    Report(violations, root, "플레이어 HUD Canvas는 00_WagonPersistent에만 두어야 함", disableViolations); // 위반 처리
                }

                foreach (PlayerInventory player in root.GetComponentsInChildren<PlayerInventory>(true)) // 플레이어 복제본 확인
                {
                    Report(violations, player.gameObject, "플레이어는 00_WagonPersistent에만 두어야 함", disableViolations); // 위반 처리
                }

                foreach (WagonCargoArea cargo in root.GetComponentsInChildren<WagonCargoArea>(true)) // 마차 복제본 확인
                {
                    GameObject wagon = cargo.transform.parent == null ? cargo.gameObject : cargo.transform.parent.gameObject; // 마차 루트 (CargoArea의 부모)
                    Report(violations, wagon, "마차는 00_WagonPersistent에만 두어야 함", disableViolations); // 위반 처리
                }

                foreach (PersistentMapLoader loader in root.GetComponentsInChildren<PersistentMapLoader>(true)) // 맵 로더 복제본 확인
                {
                    Report(violations, loader.gameObject, "맵 로더는 00_WagonPersistent에만 두어야 함", disableViolations); // 위반 처리
                }

                foreach (DailySnapshotService service in root.GetComponentsInChildren<DailySnapshotService>(true)) // 저장 서비스 복제본 확인
                {
                    Report(violations, service.gameObject, "저장 서비스는 00_WagonPersistent에만 두어야 함", disableViolations); // 위반 처리
                }

                foreach (DebugPageManager debug in root.GetComponentsInChildren<DebugPageManager>(true)) // F1 디버그 창 확인
                {
                    Report(violations, debug.gameObject, "F1 디버그 창은 00_WagonPersistent에만 두어야 함", disableViolations); // 위반 처리
                }

                if (persistentHasEventSystem) // Persistent에 EventSystem이 있을 때만 중복 검사
                {
                    foreach (EventSystem eventSystem in root.GetComponentsInChildren<EventSystem>(true)) // 중복 EventSystem 확인
                    {
                        Report(violations, eventSystem.gameObject, "EventSystem 중복 (00_WagonPersistent의 것을 사용)", disableViolations); // 위반 처리
                    }
                }

                if (persistentHasListener) // Persistent에 AudioListener가 있을 때만 중복 검사
                {
                    foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) // 중복 AudioListener 확인
                    {
                        violations.Add($"{PathOf(listener.transform)}: AudioListener 중복 (플레이어 카메라의 것을 사용)"); // 위반 기록

                        if (disableViolations) // 런타임 차단 여부 확인
                        {
                            listener.enabled = false; // 환경 오브젝트는 유지하고 컴포넌트만 비활성화
                        }
                    }
                }

                anchorCount += root.GetComponentsInChildren<MapTravelAnchor>(true).Length; // 이동 지점 집계
            }

            if (anchorCount != 1) // 환경 씬마다 마차 진입·정차 지점은 정확히 1개
            {
                violations.Add($"MapTravelAnchor 개수 {anchorCount}개 (정확히 1개 필요)"); // 위반 기록
            }

            return violations; // 결과 반환
        }

        public static void SanitizeLoadedEnvironment(Scene environmentScene, Scene persistentScene, Object context) // 런타임 환경 씬 로드 직후 검사·차단
        {
            List<string> violations = Inspect(environmentScene, persistentScene, true); // 위반 오브젝트 비활성화

            foreach (string violation in violations) // 위반 목록 순회
            {
                Debug.LogWarning($"[Project I] 환경 씬 규칙 위반 / Scene={environmentScene.name} / {violation}. 전역 시스템은 00_WagonPersistent에만 두세요.", context); // 경고
            }
        }

        private static void Report(List<string> violations, GameObject target, string reason, bool disable) // 위반 기록과 오브젝트 비활성화
        {
            violations.Add($"{PathOf(target.transform)}: {reason}"); // 위반 기록

            if (disable && target.activeSelf) // 런타임 차단 여부 확인
            {
                target.SetActive(false); // 전역 시스템 복제본만 비활성화
            }
        }

        private static bool HasComponentInScene<T>(Scene scene) where T : Component // 지정 씬에 컴포넌트가 있는지 확인
        {
            if (!scene.IsValid() || !scene.isLoaded) // 씬 유효성 확인
            {
                return false; // 없음으로 처리
            }

            foreach (GameObject root in scene.GetRootGameObjects()) // 루트 순회
            {
                if (root.GetComponentInChildren<T>(true) != null) // 컴포넌트 확인
                {
                    return true; // 있음
                }
            }

            return false; // 없음
        }

        private static string PathOf(Transform target) // 진단용 계층 경로
        {
            string path = target.name; // 현재 이름

            while (target.parent != null) // 부모 순회
            {
                target = target.parent; // 한 단계 위
                path = target.name + "/" + path; // 경로 추가
            }

            return path; // 경로 반환
        }
    }
}
