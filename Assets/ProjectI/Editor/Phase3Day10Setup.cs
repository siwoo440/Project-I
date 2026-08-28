using System.IO; // 씬 파일 존재 여부 확인 기능 참조
using System.Linq; // 씬 루트와 자식 검색 기능 참조
using ProjectI.Brightness; // 기존 자연광 컨트롤러 참조
using ProjectI.Diagnostics; // F1 시간·자연광 페이지 참조
using ProjectI.TimeOfDay; // 게임 시간 컨트롤러 참조
using UnityEditor; // 에디터 메뉴·저장 기능 참조
using UnityEditor.SceneManagement; // 씬 열기·저장 기능 참조
using UnityEngine; // Light와 GameObject 기능 참조
using UnityEngine.SceneManagement; // 씬 자료형 참조

namespace ProjectI.EditorTools // 에디터 도구 네임스페이스
{
    [InitializeOnLoad] // 컴파일 완료 후 Day 10 시간대·자연광 환경 자동 구성
    public static class Phase3Day10Setup // 24시간 흐름과 태양·달 Directional Light·F1 디버그 구성
    {
        private const string ExplorationOfficeScenePath = "Assets/ProjectI/Scenes/ExplorationOffice.unity"; // 탐사 사무소 씬 경로
        private const string TimeRootName = "===Day10 Time Of Day==="; // 시간 시스템 루트 이름
        private const string SunLightName = "Day10_SunDirectionalLight"; // 태양 Directional Light 오브젝트 이름
        private const string MoonLightName = "Day10_MoonDirectionalLight"; // 달 Directional Light 오브젝트 이름
        private const string ReadyMarkerName = "===Day10 Time Of Day Ready==="; // Day 10 자동 적용 완료 마커 이름

        static Phase3Day10Setup() // 자동 적용 등록
        {
            EditorApplication.delayCall += TryAutoApply; // 스크립트 컴파일 완료 후 Day 10 구성 예약
        }

        [MenuItem("Tools/Project I/Day 10/Apply Time + Natural Light")] // 수동 Day 10 적용 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 Day 10 구성 실행
        {
            ApplyDay10(true, true); // 강제 재구성과 결과 대화상자 활성화
        }

        private static void TryAutoApply() // 컴파일 완료 후 자동 구성 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // 자동 실행 제외 상태 확인
            {
                return; // Batch 또는 Play 전환 중에는 자동 구성 중단
            }

            ApplyDay10(false, false); // 완료 마커가 없을 때 한 번 자동 적용
        }

        private static void ApplyDay10(bool showDialog, bool force) // Day 10 전체 시간대·자연광 구성 적용
        {
            if (!File.Exists(ExplorationOfficeScenePath)) // 대상 탐사 씬 존재 여부 확인
            {
                return; // 씬이 없으면 구성 중단
            }

            Scene scene = EditorSceneManager.OpenScene(ExplorationOfficeScenePath, OpenSceneMode.Single); // 탐사 사무소 씬 열기
            GameObject existingMarker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 Day 10 완료 마커 조회

            if (!force && existingMarker != null) // 이미 Day 10 자동 구성이 적용됐는지 확인
            {
                return; // 반복 자동 적용 방지
            }

            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Player"); // F1 페이지를 연결할 플레이어 루트 조회
            NaturalLightController naturalLight = Object.FindFirstObjectByType<NaturalLightController>(); // Day 7부터 사용 중인 기존 자연광 컨트롤러 조회

            if (player == null || naturalLight == null) // 선행 플레이어 또는 자연광 시스템 존재 여부 확인
            {
                Debug.LogError("[Project I] Day 10 구성 전에 Player와 NaturalLightController가 필요합니다."); // 선행 구조 누락 오류 출력
                return; // Day 10 구성 중단
            }

            GameObject timeRoot = GetOrCreateRoot(scene); // 시간대 시스템 전용 루트 확보
            Light sunLight = GetOrCreateDirectionalLight(timeRoot.transform, SunLightName, true); // 태양 Directional Light 확보
            Light moonLight = GetOrCreateDirectionalLight(timeRoot.transform, MoonLightName, false); // 달 Directional Light 확보
            DisableOtherDirectionalLights(sunLight, moonLight); // 기존 고정 Directional Light 중복 조명을 비활성화
            RenderSettings.sun = sunLight; // Unity Skybox가 사용할 주 태양 Light를 Day 10 태양으로 지정

            GameTimeController controller = timeRoot.GetComponent<GameTimeController>(); // 기존 게임 시간 컨트롤러 조회

            if (controller == null) // 시간 컨트롤러가 아직 없는지 확인
            {
                controller = timeRoot.AddComponent<GameTimeController>(); // 24시간 진행 컨트롤러 추가
            }

            controller.Configure(naturalLight, sunLight, moonLight, 12f, 1f); // 12시 시작·현실 1초당 게임 1분 기본값과 자연광 참조 적용
            NaturalLightDebugPage debugPage = player.GetComponent<NaturalLightDebugPage>(); // 기존 F1 시간·자연광 페이지 조회

            if (debugPage == null) // F1 시간 페이지 미생성 여부 확인
            {
                debugPage = player.AddComponent<NaturalLightDebugPage>(); // Light Calculation 다음 F1 시간 페이지 추가
            }

            debugPage.Configure(controller); // F1 페이지에 현재 게임 시간 컨트롤러 연결
            EditorUtility.SetDirty(controller); // 시간 시스템 변경 저장 대상으로 표시
            EditorUtility.SetDirty(debugPage); // F1 페이지 변경 저장 대상으로 표시
            EnsureMarker(scene); // Day 10 완료 마커 확보
            EditorSceneManager.MarkSceneDirty(scene); // 씬 변경 상태 표시
            EditorSceneManager.SaveScene(scene); // Day 10 구성 씬 저장
            AssetDatabase.SaveAssets(); // 프로젝트 에셋 저장
            AssetDatabase.Refresh(); // 프로젝트 에셋 상태 갱신
            bool validationPassed = Phase3Day10Validator.Validate(false); // Day 10 시간대·자연광 구성 자동 검증

            if (showDialog) // 수동 적용 결과 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", validationPassed ? "Day 10 시간대 및 자연광 구성이 완료되었습니다." : "Day 10 검증 실패 - Console을 확인하세요.", "확인"); // 수동 적용 결과 안내
            }
        }

        private static GameObject GetOrCreateRoot(Scene scene) // 시간대 시스템 루트 확보
        {
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == TimeRootName); // 기존 시간 시스템 루트 검색

            if (root == null) // 아직 시간 시스템 루트가 없는지 확인
            {
                root = new GameObject(TimeRootName); // 새 Day 10 시간대 시스템 루트 생성
            }

            return root; // 확보한 시간대 루트 반환
        }

        private static Light GetOrCreateDirectionalLight(Transform parent, string objectName, bool isSun) // 태양 또는 달 Directional Light 생성·재사용
        {
            Transform existing = parent.Find(objectName); // 시간대 루트의 기존 Light 자식 검색
            GameObject lightObject = existing == null ? new GameObject(objectName) : existing.gameObject; // 기존 오브젝트가 없으면 새 Light 오브젝트 생성
            lightObject.transform.SetParent(parent, false); // 시간대 시스템 루트 아래에 연결
            Light light = lightObject.GetComponent<Light>(); // 기존 Unity Light 컴포넌트 조회

            if (light == null) // Light 컴포넌트가 아직 없는지 확인
            {
                light = lightObject.AddComponent<Light>(); // Directional Light 컴포넌트 추가
            }

            light.type = LightType.Directional; // 태양·달 모두 방향성 환경광으로 설정
            light.shadows = LightShadows.Soft; // 환경 조명에 부드러운 그림자 적용
            light.intensity = 0f; // GameTimeController가 현재 시간에 맞게 실제 강도를 다시 설정
            light.color = isSun ? new Color(1f, 0.92f, 0.78f) : new Color(0.48f, 0.60f, 1f); // 태양은 따뜻한 색, 달은 차가운 색으로 구분
            EditorUtility.SetDirty(light); // Light 설정을 씬 저장 대상으로 표시
            return light; // 생성 또는 재사용한 Directional Light 반환
        }

        private static void DisableOtherDirectionalLights(Light sunLight, Light moonLight) // 시간 시스템 외 기존 Directional Light 중복 비활성화
        {
            Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 현재 씬의 활성·비활성 Light 전체 조회

            foreach (Light light in allLights) // 씬 Light 전체 순회
            {
                if (light == null || light.type != LightType.Directional || light == sunLight || light == moonLight) // 대상이 아니거나 Day 10 태양·달인지 확인
                {
                    continue; // 시간 시스템이 제어할 두 Light는 유지
                }

                light.enabled = false; // 기존 Directional Light를 꺼서 시간대 조명과 중복되는 화면 밝기 제거
                EditorUtility.SetDirty(light); // 비활성 상태를 씬 저장 대상으로 표시
            }
        }

        private static void EnsureMarker(Scene scene) // Day 10 자동 적용 완료 마커 확보
        {
            GameObject marker = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ReadyMarkerName); // 기존 완료 마커 검색

            if (marker == null) // 아직 Day 10 완료 마커가 없는지 확인
            {
                marker = new GameObject(ReadyMarkerName); // 새 완료 마커 생성
                marker.hideFlags = HideFlags.HideInHierarchy; // 일반 Hierarchy에서는 개발용 마커 숨김
            }
        }
    }
}
