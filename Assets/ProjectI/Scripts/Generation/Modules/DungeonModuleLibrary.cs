using System.Collections.Generic; // 목록 사용
using ProjectI.Generation; // 배치 규칙 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    [CreateAssetMenu(fileName = "DungeonModuleLibrary", menuName = "Project I/던전 모듈 목록")] // 에셋 생성 메뉴
    public sealed class DungeonModuleLibrary : ScriptableObject // 던전에 쓸 모듈 프리팹 모음
    {
        [SerializeField] private DungeonModule[] modules = System.Array.Empty<DungeonModule>(); // 모듈 프리팹
        [SerializeField] private GameObject doorPrefab; // 여닫이문 프리팹
        [SerializeField] private GameObject lockedDoorPrefab; // 잠긴 문 프리팹
        [SerializeField] private GameObject breakableWallPrefab; // 금 간 벽 프리팹
        [SerializeField] private GameObject exteriorDoorPrefab; // 외부 씬 순간이동 문 프리팹
        [SerializeField] private GameObject wallPlugPrefab; // 연결되지 않은 출입구를 막는 벽 프리팹

        public IReadOnlyList<DungeonModule> Modules => modules; // 모듈 공개
        public GameObject DoorPrefab => doorPrefab; // 문 공개
        public GameObject LockedDoorPrefab => lockedDoorPrefab; // 잠긴 문 공개
        public GameObject BreakableWallPrefab => breakableWallPrefab; // 금 간 벽 공개
        public GameObject ExteriorDoorPrefab => exteriorDoorPrefab; // 외부 문 공개
        public GameObject WallPlugPrefab => wallPlugPrefab; // 막음 벽 공개

        public void Configure(DungeonModule[] moduleList, GameObject door, GameObject lockedDoor, GameObject breakableWall, GameObject exteriorDoor, GameObject wallPlug) // 구성 (프리팹 제작기에서 호출)
        {
            modules = moduleList; // 모듈
            doorPrefab = door; // 문
            lockedDoorPrefab = lockedDoor; // 잠긴 문
            breakableWallPrefab = breakableWall; // 금 간 벽
            exteriorDoorPrefab = exteriorDoor; // 외부 문
            wallPlugPrefab = wallPlug; // 막음 벽
        }

        public List<ModuleDefinition> BuildDefinitions() // 배치 규칙용 정의 목록
        {
            List<ModuleDefinition> definitions = new List<ModuleDefinition>(); // 결과

            foreach (DungeonModule module in modules) // 모듈 순회
            {
                if (module != null) // 확인
                {
                    definitions.Add(module.ToDefinition()); // 변환
                }
            }

            return definitions; // 반환
        }

        public DungeonModule Find(string moduleId) // 이름으로 모듈 프리팹 조회
        {
            foreach (DungeonModule module in modules) // 모듈 순회
            {
                if (module != null && module.ModuleId == moduleId) // 일치
                {
                    return module; // 반환
                }
            }

            return null; // 없음
        }

        public GameObject FillPrefab(PassageFill fill) // 채움 종류에 맞는 프리팹
        {
            switch (fill) // 종류별
            {
                case PassageFill.Door: return doorPrefab; // 여닫이문
                case PassageFill.LockedDoor: return lockedDoorPrefab; // 잠긴 문
                case PassageFill.Breakable: return breakableWallPrefab; // 금 간 벽
                case PassageFill.Exterior: return exteriorDoorPrefab; // 외부 문
                default: return null; // 뚫린 통로
            }
        }
    }
}
