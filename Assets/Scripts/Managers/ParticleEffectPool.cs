using System.Collections.Generic; // 파티클 대기열과 활성 목록 사용
using UnityEngine; // Unity 기본 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public class ParticleEffectPool : MonoBehaviour // 파티클 생성과 재사용을 관리
    {
        class ActiveEffect // 현재 재생 중인 파티클 정보
        {
            public ParticleSystem Effect; // 현재 재생 중인 파티클
            public int PrefabId; // 원본 프리팹 식별 번호
            public float ReturnTime; // 풀로 반환할 실시간 시각
        }

        public static ParticleEffectPool Instance { get; private set; } // 현재 활성 파티클 풀 접근점

        [Header("풀 설정")] // Inspector 풀 설정 구분
        [SerializeField][Min(1)] int maxInstancesPerPrefab = 12; // 프리팹 종류별 최대 생성 개수

        readonly Dictionary<int, Queue<ParticleSystem>> availableEffects = new Dictionary<int, Queue<ParticleSystem>>(); // 프리팹별 대기 파티클 목록
        readonly Dictionary<int, int> createdCounts = new Dictionary<int, int>(); // 프리팹별 생성된 전체 개수
        readonly List<ActiveEffect> activeEffects = new List<ActiveEffect>(); // 현재 재생 중인 파티클 목록

        public int ActiveEffectCount => activeEffects.Count; // 현재 재생 중인 파티클 개수

        public int AvailableEffectCount // 현재 대기 중인 파티클 개수 반환
        {
            get
            {
                int count = 0; // 전체 대기 개수 초기화

                foreach (Queue<ParticleSystem> queue in availableEffects.Values) // 프리팹별 대기열 순회
                {
                    count += queue.Count; // 대기 중인 파티클 개수 누적
                }

                return count; // 전체 대기 파티클 개수 반환
            }
        }

        void Awake() // 파티클 풀 싱글톤 초기화
        {
            if (Instance != null && Instance != this) // 기존 파티클 풀 존재 여부 확인
            {
                Destroy(gameObject); // 중복 파티클 풀 제거
                return; // 중복 초기화 중단
            }

            Instance = this; // 현재 오브젝트를 전역 인스턴스로 저장
            transform.SetParent(null); // DontDestroyOnLoad 적용을 위해 루트 오브젝트로 분리
            DontDestroyOnLoad(gameObject); // Scene 전환 후에도 풀 유지
        }

        void Update() // 종료된 파티클을 풀로 반환
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--) // 활성 목록을 역순으로 순회
            {
                ActiveEffect activeEffect = activeEffects[i]; // 현재 활성 파티클 정보 가져오기

                if (activeEffect.Effect == null) // 파티클이 외부에서 제거되었는지 확인
                {
                    activeEffects.RemoveAt(i); // 유효하지 않은 활성 정보 제거
                    continue; // 다음 파티클 검사
                }

                if (Time.unscaledTime >= activeEffect.ReturnTime) // 파티클 반환 시각 도달 여부 확인
                {
                    ReleaseEffect(activeEffect); // 파티클을 대기열로 반환
                    activeEffects.RemoveAt(i); // 활성 목록에서 제거
                }
            }
        }

        void OnDestroy() // 파티클 풀 싱글톤 참조 정리
        {
            if (Instance == this) // 현재 오브젝트가 등록된 인스턴스인지 확인
            {
                Instance = null; // 전역 인스턴스 참조 초기화
            }
        }

        public void Play(ParticleSystem prefab, Vector3 position, Quaternion rotation) // 지정한 파티클을 풀에서 가져와 재생
        {
            if (prefab == null) // 파티클 프리팹 연결 여부 확인
            {
                return; // 파티클 재생 중단
            }

            int prefabId = prefab.GetInstanceID(); // 원본 프리팹 식별 번호 계산
            ParticleSystem effect = GetEffect(prefab, prefabId); // 사용할 파티클 가져오기

            if (effect == null) // 풀 최대 개수 도달 여부 확인
            {
                return; // 추가 파티클 재생 중단
            }

            effect.transform.SetPositionAndRotation(position, rotation); // 파티클 위치와 회전 적용
            effect.gameObject.SetActive(true); // 재사용할 파티클 활성화
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // 이전 파티클 흔적 제거
            effect.Play(true); // 자식 파티클을 포함해 재생

            float returnDelay = CalculateDuration(effect); // 파티클 전체 재생시간 계산

            ActiveEffect activeEffect = new ActiveEffect(); // 활성 파티클 정보 생성
            activeEffect.Effect = effect; // 재생 중인 파티클 저장
            activeEffect.PrefabId = prefabId; // 원본 프리팹 식별 번호 저장
            activeEffect.ReturnTime = Time.unscaledTime + returnDelay; // 풀 반환 시각 저장
            activeEffects.Add(activeEffect); // 활성 파티클 목록에 추가
        }

        ParticleSystem GetEffect(ParticleSystem prefab, int prefabId) // 대기 파티클을 가져오거나 새로 생성
        {
            if (!availableEffects.TryGetValue(prefabId, out Queue<ParticleSystem> queue)) // 프리팹 대기열 존재 여부 확인
            {
                queue = new Queue<ParticleSystem>(); // 새로운 프리팹 대기열 생성
                availableEffects.Add(prefabId, queue); // 프리팹 식별 번호로 대기열 등록
            }

            while (queue.Count > 0) // 대기 중인 파티클이 있는 동안 반복
            {
                ParticleSystem availableEffect = queue.Dequeue(); // 가장 먼저 대기한 파티클 가져오기

                if (availableEffect != null) // 파티클이 외부에서 제거되지 않았는지 확인
                {
                    return availableEffect; // 재사용할 파티클 반환
                }
            }

            createdCounts.TryGetValue(prefabId, out int createdCount); // 현재 프리팹 생성 개수 가져오기

            if (createdCount >= maxInstancesPerPrefab) // 프리팹별 최대 개수 도달 여부 확인
            {
                return null; // 추가 생성 없이 null 반환
            }

            ParticleSystem createdEffect = Instantiate(prefab, transform); // 파티클 풀 자식으로 새 인스턴스 생성
            createdEffect.name = $"{prefab.name}_Pooled_{createdCount + 1}"; // Hierarchy 확인용 이름 설정
            createdEffect.gameObject.SetActive(false); // 첫 사용 전 파티클 비활성화
            createdCounts[prefabId] = createdCount + 1; // 프리팹 생성 개수 갱신

            return createdEffect; // 새로 생성한 파티클 반환
        }

        void ReleaseEffect(ActiveEffect activeEffect) // 재생이 끝난 파티클을 대기열로 반환
        {
            ParticleSystem effect = activeEffect.Effect; // 반환할 파티클 가져오기
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // 남은 파티클 제거
            effect.gameObject.SetActive(false); // 대기 상태로 비활성화

            if (!availableEffects.TryGetValue(activeEffect.PrefabId, out Queue<ParticleSystem> queue)) // 대기열 존재 여부 확인
            {
                queue = new Queue<ParticleSystem>(); // 누락된 대기열 생성
                availableEffects.Add(activeEffect.PrefabId, queue); // 새로운 대기열 등록
            }

            queue.Enqueue(effect); // 파티클을 재사용 대기열에 추가
        }

        float CalculateDuration(ParticleSystem effect) // 파티클 시작 지연과 수명을 포함한 반환시간 계산
        {
            ParticleSystem.MainModule mainModule = effect.main; // 파티클 기본 설정 가져오기
            float startDelay = mainModule.startDelay.constantMax; // 최대 시작 지연시간 가져오기
            float lifetime = mainModule.startLifetime.constantMax; // 최대 파티클 수명 가져오기
            float duration = mainModule.duration; // 파티클 방출 지속시간 가져오기

            return Mathf.Max(0.2f, startDelay + duration + lifetime + 0.2f); // 안전 여유시간을 포함해 반환
        }
    }
}