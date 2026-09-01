using UnityEditor; // 마차 프리팹 로드·저장과 메뉴 기능 참조
using UnityEngine; // Transform·Vector3·GameObject 기능 참조

namespace ProjectI.EditorTools // 에디터 자동 보정 도구 네임스페이스
{
    [InitializeOnLoad] // 컴파일 완료 후 마차 말 2마리 구성을 자동 적용
    public static class Phase5Day21HorsePairUpgrade // 기존 한 마리를 80% 크기의 좌우 2마리로 보정
    {
        private const string WagonPrefabPath = "Assets/ProjectI/Prefabs/Wagon/Wagon.prefab"; // 공통 마차 프리팹 경로
        private const float HorseScale = 0.8f; // 기존 말 100% 대비 80% 크기
        private const float HorseSideOffset = 0.95f; // 좌우 말 중심 간격을 위한 X축 오프셋
        private const float HorseForwardOffset = 1.71f; // 80% 축소 후 기존 전방 위치를 보정할 Z축 오프셋
        private const float PositionTolerance = 0.001f; // 프리팹 반복 저장 방지를 위한 위치 비교 허용 오차

        static Phase5Day21HorsePairUpgrade() // 자동 보정 예약
        {
            EditorApplication.delayCall += TryAutoApply; // Unity 컴파일 완료 다음 에디터 틱에 말 2마리 적용
        }

        [MenuItem("Tools/Project I/Day 21/Apply 80% Horse Pair")] // 수동 말 2마리 재적용 메뉴 등록
        public static void ApplyFromMenu() // 메뉴 기반 강제 보정 실행
        {
            bool changed = ApplyHorsePair(); // 공통 Wagon.prefab 말 2마리 구조 적용
            bool valid = ValidateHorsePair(false); // 적용 직후 실제 프리팹 구조 검증
            string result = valid ? (changed ? "말 2마리 80% 구성이 적용되었습니다." : "이미 말 2마리 80% 구성이 적용되어 있습니다.") : "말 2마리 검증에 실패했습니다. Console을 확인하세요."; // 수동 실행 결과 문구 계산
            EditorUtility.DisplayDialog("Project I", result, "확인"); // 수동 실행 결과 표시
        }

        [MenuItem("Tools/Project I/Day 21/Validate 80% Horse Pair")] // 수동 말 2마리 검증 메뉴 등록
        public static void ValidateFromMenu() // 메뉴 기반 프리팹 검증 실행
        {
            bool valid = ValidateHorsePair(true); // 말 2마리 구조와 크기·위치 검증
            Debug.Log(valid ? "[Project I] Wagon 말 2마리 80% 검증 PASS" : "[Project I] Wagon 말 2마리 80% 검증 FAIL"); // Console 최종 결과 출력
        }

        private static void TryAutoApply() // Unity 로드 후 자동 보정 진입
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) // Batch 또는 Play 전환 상태 확인
            {
                return; // 안전하지 않은 상태에서는 자동 프리팹 수정 중단
            }

            ApplyHorsePair(); // 기존 Wagon.prefab에 필요한 경우에만 말 2마리 보정 적용
        }

        private static bool ApplyHorsePair() // 공통 Wagon.prefab 말 구조를 좌우 2마리로 변경
        {
            GameObject wagonAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WagonPrefabPath); // Wagon.prefab 에셋 존재 여부 확인

            if (wagonAsset == null) // 공통 마차 프리팹 누락 여부 확인
            {
                Debug.LogError("[Project I] 말 2마리 보정 대상 Wagon.prefab을 찾을 수 없습니다."); // 프리팹 누락 오류 출력
                return false; // 보정 실패 반환
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(WagonPrefabPath); // 실제 수정 가능한 프리팹 콘텐츠 임시 로드

            try // 프리팹 임시 콘텐츠 안전 정리 범위 시작
            {
                Transform visual = prefabRoot.transform.Find("Visual"); // 기존 마차 시각 계층 조회

                if (visual == null) // Visual 계층 누락 여부 확인
                {
                    Debug.LogError("[Project I] Wagon.prefab에서 Visual 루트를 찾을 수 없습니다."); // 시각 계층 누락 오류 출력
                    return false; // 보정 실패 반환
                }

                Transform leftHorse = visual.Find("Horse"); // 기존 한 마리를 왼쪽 말 원본으로 조회

                if (leftHorse == null || leftHorse.Find("Horse_Body") == null) // 기존 말 루트와 핵심 몸통 존재 여부 확인
                {
                    Debug.LogError("[Project I] Wagon.prefab의 기존 Horse/Horse_Body 구조를 찾을 수 없습니다."); // 기존 말 구조 누락 오류 출력
                    return false; // 보정 실패 반환
                }

                bool changed = false; // 실제 프리팹 변경 발생 여부 초기화
                Vector3 leftPosition = new Vector3(-HorseSideOffset, 0f, HorseForwardOffset); // 왼쪽 말 목표 로컬 위치 계산
                Vector3 rightPosition = new Vector3(HorseSideOffset, 0f, HorseForwardOffset); // 오른쪽 말 목표 로컬 위치 계산
                Vector3 targetScale = Vector3.one * HorseScale; // 두 말 공통 80% 로컬 스케일 계산

                if (Vector3.Distance(leftHorse.localPosition, leftPosition) > PositionTolerance) // 왼쪽 말 위치가 목표와 다른지 확인
                {
                    leftHorse.localPosition = leftPosition; // 왼쪽 말을 마차 중심에서 왼쪽으로 이동
                    changed = true; // 프리팹 변경 상태 기록
                }

                if (Vector3.Distance(leftHorse.localScale, targetScale) > PositionTolerance) // 왼쪽 말 크기가 80%와 다른지 확인
                {
                    leftHorse.localScale = targetScale; // 기존 말을 원본 대비 80% 크기로 축소
                    changed = true; // 프리팹 변경 상태 기록
                }

                leftHorse.localRotation = Quaternion.identity; // 기존 말 루트 회전을 기본값으로 정렬
                Transform rightHorse = visual.Find("Horse_Right"); // 이미 생성된 오른쪽 말 존재 여부 조회

                if (rightHorse == null) // 오른쪽 말이 아직 없는지 확인
                {
                    GameObject rightHorseObject = Object.Instantiate(leftHorse.gameObject, visual); // 왼쪽 말 전체 외형·마구를 복제해 두 번째 말 생성
                    rightHorseObject.name = "Horse_Right"; // 두 번째 말을 오른쪽 말로 명확히 식별
                    rightHorse = rightHorseObject.transform; // 생성된 오른쪽 말 Transform 참조 저장
                    changed = true; // 새 말 생성으로 프리팹 변경 상태 기록
                }

                if (Vector3.Distance(rightHorse.localPosition, rightPosition) > PositionTolerance) // 오른쪽 말 위치가 목표와 다른지 확인
                {
                    rightHorse.localPosition = rightPosition; // 오른쪽 말을 마차 중심에서 오른쪽으로 이동
                    changed = true; // 프리팹 변경 상태 기록
                }

                if (Vector3.Distance(rightHorse.localScale, targetScale) > PositionTolerance) // 오른쪽 말 크기가 80%와 다른지 확인
                {
                    rightHorse.localScale = targetScale; // 오른쪽 말도 원본 대비 80% 크기로 통일
                    changed = true; // 프리팹 변경 상태 기록
                }

                rightHorse.localRotation = Quaternion.identity; // 두 번째 말 루트 회전을 왼쪽 말과 동일하게 정렬

                if (changed) // 실제 구조 또는 Transform 변경 발생 여부 확인
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, WagonPrefabPath); // 수정된 말 2마리 구조를 공통 Wagon.prefab에 저장
                    AssetDatabase.SaveAssets(); // 프리팹 변경 내용을 디스크에 저장
                    AssetDatabase.Refresh(); // 현재 씬의 Wagon.prefab 인스턴스 갱신을 위해 에셋 데이터 새로고침
                    Debug.Log("[Project I] Wagon 말 구성을 80% 크기 좌우 2마리로 변경했습니다."); // 자동 보정 완료 로그 출력
                }

                return changed; // 실제 변경 여부 반환
            }
            finally // 성공·실패와 무관하게 프리팹 임시 콘텐츠 정리
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot); // 임시로 연 Wagon.prefab 콘텐츠 메모리 해제
            }
        }

        public static bool ValidateHorsePair(bool showDialog) // 공통 Wagon.prefab 말 2마리 구조 검증
        {
            GameObject wagonAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WagonPrefabPath); // 저장된 Wagon.prefab 로드

            if (wagonAsset == null) // 프리팹 존재 여부 확인
            {
                return ReportValidation(false, showDialog, "Wagon.prefab을 찾을 수 없습니다."); // 프리팹 누락 실패 반환
            }

            Transform visual = wagonAsset.transform.Find("Visual"); // 저장된 프리팹의 Visual 루트 조회
            Transform leftHorse = visual == null ? null : visual.Find("Horse"); // 기존 이름을 유지한 왼쪽 말 조회
            Transform rightHorse = visual == null ? null : visual.Find("Horse_Right"); // 새 오른쪽 말 조회
            bool hasPair = leftHorse != null && rightHorse != null; // 좌우 두 말 모두 존재 여부 계산
            bool hasBodies = hasPair && leftHorse.Find("Horse_Body") != null && rightHorse.Find("Horse_Body") != null; // 두 말 모두 실제 몸통 구조를 가진 복제본인지 확인
            bool leftScaleValid = leftHorse != null && Vector3.Distance(leftHorse.localScale, Vector3.one * HorseScale) <= PositionTolerance; // 왼쪽 말 80% 크기 검증
            bool rightScaleValid = rightHorse != null && Vector3.Distance(rightHorse.localScale, Vector3.one * HorseScale) <= PositionTolerance; // 오른쪽 말 80% 크기 검증
            bool leftPositionValid = leftHorse != null && Vector3.Distance(leftHorse.localPosition, new Vector3(-HorseSideOffset, 0f, HorseForwardOffset)) <= PositionTolerance; // 왼쪽 말 좌측 배치 검증
            bool rightPositionValid = rightHorse != null && Vector3.Distance(rightHorse.localPosition, new Vector3(HorseSideOffset, 0f, HorseForwardOffset)) <= PositionTolerance; // 오른쪽 말 우측 배치 검증
            bool valid = hasPair && hasBodies && leftScaleValid && rightScaleValid && leftPositionValid && rightPositionValid; // 모든 말 2마리 요구사항 최종 판정
            return ReportValidation(valid, showDialog, valid ? "말 2마리 80% 크기와 좌우 배치가 정상입니다." : "말 2마리 크기 또는 위치가 요구값과 다릅니다."); // 검증 결과 출력
        }

        private static bool ReportValidation(bool valid, bool showDialog, string message) // 공통 검증 결과 표시 처리
        {
            if (showDialog) // 수동 검증 대화상자 표시 여부 확인
            {
                EditorUtility.DisplayDialog("Project I", valid ? "PASS\n" + message : "FAIL\n" + message, "확인"); // 사용자에게 검증 결과 표시
            }

            if (!valid) // 실패 여부 확인
            {
                Debug.LogError("[Project I] " + message); // 실패 원인 Console 출력
            }

            return valid; // 최종 검증 결과 반환
        }
    }
}
