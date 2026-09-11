using System.Collections.Generic; // ItemId 사전 기능 사용
using UnityEngine; // Resources 로드 기능 사용

namespace ProjectI.Items // 프로젝트 아이템 데이터 네임스페이스
{
    public static class ItemRegistry // 복구 시 ItemId에서 ItemDefinition을 찾는 정적 레지스트리
    {
        private const string ResourcePath = "Day24Recovery/Definitions"; // Resources 기준 정의 폴더
        private static readonly Dictionary<string, ItemDefinition> Definitions = new Dictionary<string, ItemDefinition>(); // ItemId별 정의 캐시
        private static bool loaded; // Resources 로드 완료 여부

        public static ItemDefinition Find(string itemId) // ItemId에 해당하는 정의 조회
        {
            EnsureLoaded(); // 정의 에셋 캐시 준비

            if (string.IsNullOrWhiteSpace(itemId)) // 유효 ID 여부 확인
            {
                return null; // 빈 ID는 복구 불가
            }

            Definitions.TryGetValue(itemId, out ItemDefinition definition); // ID 기반 정의 검색
            return definition; // 검색 결과 반환
        }

        public static void Reload() // Editor 생성 직후 또는 테스트용 캐시 초기화
        {
            loaded = false; // 재로드 허용
            Definitions.Clear(); // 이전 캐시 제거
            EnsureLoaded(); // 최신 Resources 정의 다시 로드
        }

        private static void EnsureLoaded() // Resources의 ItemDefinition 전체 캐시
        {
            if (loaded) // 이미 로드됐는지 확인
            {
                return; // 중복 Resources 검색 방지
            }

            loaded = true; // 이번 로드를 완료 상태로 표시
            Definitions.Clear(); // 안전하게 사전 초기화
            ItemDefinition[] definitions = Resources.LoadAll<ItemDefinition>(ResourcePath); // 생성된 복구 정의 전체 로드

            foreach (ItemDefinition definition in definitions) // 정의 에셋 순회
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.ItemId)) // 유효 정의 확인
                {
                    continue; // 잘못된 정의 제외
                }

                if (Definitions.TryGetValue(definition.ItemId, out ItemDefinition duplicate) && duplicate != definition) // 고정 ID 중복 확인
                {
                    Debug.LogError($"[Project I] ItemId 중복 / {definition.ItemId} / {duplicate.name} · {definition.name}"); // 중복 정의 경고
                }

                Definitions[definition.ItemId] = definition; // 현재 고정 ID 등록
            }

            foreach (ItemDefinition definition in definitions) // 과거 ID 별칭 등록
            {
                if (definition == null) // 유효 정의 확인
                {
                    continue; // 다음 정의
                }

                foreach (string legacyId in definition.LegacyIds) // 과거 ID 순회
                {
                    if (!string.IsNullOrWhiteSpace(legacyId) && !Definitions.ContainsKey(legacyId)) // 현재 ID와 충돌하지 않는 과거 ID인지 확인
                    {
                        Definitions[legacyId] = definition; // 이전 저장 파일의 ID도 같은 정의로 연결
                    }
                }
            }
        }
    }
}
