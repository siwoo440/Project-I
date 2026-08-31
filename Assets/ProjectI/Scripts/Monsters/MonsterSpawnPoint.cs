using System.Collections; // 지연 소환 Coroutine 기능 참조
using UnityEngine; // GameObject 복제와 Transform 기능 참조

namespace ProjectI.Monsters // 몬스터 공통 AI 네임스페이스
{
    public sealed class MonsterSpawnPoint : MonoBehaviour // 테스트맵과 향후 던전에서 몬스터 프로토타입을 생성하는 공통 소환 지점
    {
        [SerializeField] private GameObject prototype; // 런타임 복제할 비활성 몬스터 프로토타입
        [SerializeField] private bool spawnOnStart = true; // Play 시작 시 자동 소환 여부
        [SerializeField] private float spawnDelay; // 나란히 배치된 몬스터 발사·이동 동기화를 줄이는 소환 지연
        [SerializeField] private string spawnedName = "Monster"; // 런타임 생성 몬스터 이름
        private GameObject spawnedMonster; // 현재 이 지점에서 생성한 몬스터 인스턴스

        public GameObject Prototype => prototype; // Validator용 몬스터 프로토타입 공개
        public GameObject SpawnedMonster => spawnedMonster; // 현재 런타임 생성 몬스터 공개
        public bool SpawnOnStart => spawnOnStart; // 자동 소환 여부 공개
        public float SpawnDelay => spawnDelay; // 소환 지연 공개

        private void Start() // Play 시작 시 설정에 따른 몬스터 자동 소환
        {
            if (spawnOnStart) // 자동 소환 활성화 여부 확인
            {
                StartCoroutine(SpawnAfterDelay()); // 설정된 시간 후 몬스터 소환 예약
            }
        }

        public void Configure(GameObject targetPrototype, string targetSpawnedName, bool autoSpawn, float delay) // Day17 자동 Setup용 소환 지점 구성
        {
            prototype = targetPrototype; // 복제할 몬스터 프로토타입 저장
            spawnedName = string.IsNullOrWhiteSpace(targetSpawnedName) ? "Monster" : targetSpawnedName; // 런타임 몬스터 이름 저장
            spawnOnStart = autoSpawn; // 시작 자동 소환 설정 저장
            spawnDelay = Mathf.Max(0f, delay); // 소환 지연 음수 방지
        }

        public GameObject SpawnNow() // 현재 위치에 몬스터 즉시 소환
        {
            if (spawnedMonster != null || prototype == null) // 이미 살아있는 인스턴스 또는 프로토타입 누락 여부 확인
            {
                return spawnedMonster; // 기존 인스턴스 또는 없음 반환
            }

            Transform parent = transform.parent; // Day17 시험장 루트를 런타임 몬스터 부모로 사용
            spawnedMonster = Object.Instantiate(prototype, transform.position, transform.rotation, parent); // 비활성 프로토타입 복제
            spawnedMonster.name = spawnedName; // 런타임 몬스터 이름 지정
            spawnedMonster.SetActive(true); // 복제 인스턴스 AI 활성화
            return spawnedMonster; // 생성된 몬스터 반환
        }

        private IEnumerator SpawnAfterDelay() // 설정된 지연 후 한 번 자동 소환
        {
            if (spawnDelay > 0f) // 실제 지연 시간이 존재하는지 확인
            {
                yield return new WaitForSeconds(spawnDelay); // 순차 소환 느낌을 위한 지정 시간 대기
            }

            SpawnNow(); // 현재 SpawnPoint 위치에 몬스터 생성
        }
    }
}
