using System.Collections.Generic; // 목록 사용
using ProjectI.Generation; // 배치 결과 참조
using Unity.AI.Navigation; // NavMeshSurface 참조
using UnityEngine; // 유니티 기본 기능 참조
using UnityEngine.AI; // NavMesh 조회 참조

namespace ProjectI.Dungeon // 절차적 던전 런타임 네임스페이스
{
    public sealed class DungeonNavMeshBuilder : MonoBehaviour // 생성된 던전 위에 NavMesh 를 굽고 도달 가능성을 확인합니다
    {
        [SerializeField] private float voxelSize = 0.13f; // 굽기 정밀도 (작을수록 정밀·느림)
        [SerializeField] private float sampleDistance = 2.5f; // 지점을 NavMesh 위로 붙일 때 허용 거리
        private NavMeshSurface surface; // 굽기 대상

        public NavMeshSurface Surface => surface; // 표면 공개
        public bool IsBuilt => surface != null && surface.navMeshData != null; // 굽기 완료 여부
        public readonly Dictionary<int, string> LastReasons = new Dictionary<int, string>(); // 마지막 검사에서 모듈별 도달 불가 사유

        public static void ExcludeFromBuild(GameObject target) // 이 오브젝트와 자식을 NavMesh 굽기에서 제외 (열 수 있는 문은 길을 막지 않음)
        {
            if (target == null) // 확인
            {
                return; // 종료
            }

            NavMeshModifier modifier = target.GetComponent<NavMeshModifier>(); // 기존

            if (modifier == null) // 없으면 추가
            {
                modifier = target.AddComponent<NavMeshModifier>(); // 추가
            }

            modifier.ignoreFromBuild = true; // 굽기에서 제외
            modifier.applyToChildren = true; // 자식까지 함께 제외
        }

        public NavMeshLink AddLink(Transform parent, string name, Vector3 worldStart, Vector3 worldEnd, float width) // 걸어서 갈 수 없는 이동 수단(사다리·승강기)을 잇는 연결
        {
            GameObject linkObject = new GameObject(name); // 오브젝트
            linkObject.transform.SetParent(parent, false); // 부모
            linkObject.transform.position = worldStart; // 시작 지점
            linkObject.transform.rotation = Quaternion.identity; // 회전 없음
            NavMeshLink link = linkObject.AddComponent<NavMeshLink>(); // 연결
            link.startPoint = Vector3.zero; // 로컬 시작
            link.endPoint = linkObject.transform.InverseTransformPoint(worldEnd); // 로컬 끝
            link.width = width; // 폭
            link.bidirectional = true; // 양방향
            link.UpdateLink(); // 반영
            return link; // 반환
        }

        public void Build(Transform generatedRoot) // 생성물 아래 형상으로 NavMesh 굽기
        {
            if (generatedRoot == null) // 대상 없음
            {
                return; // 종료
            }

            surface = surface != null ? surface : generatedRoot.gameObject.AddComponent<NavMeshSurface>(); // 표면
            surface.collectObjects = CollectObjects.Children; // 생성물만
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; // 충돌체 기준 (보이지 않는 계단 경사면 포함)
            surface.agentTypeID = 0; // 기본 이동체 (반지름·높이·경사는 프로젝트 Navigation 설정을 따름)
            surface.overrideVoxelSize = true; // 정밀도 지정
            surface.voxelSize = voxelSize; // 복셀 크기
            surface.BuildNavMesh(); // 굽기
        }

        public void Clear() // NavMesh 제거
        {
            if (surface != null) // 확인
            {
                surface.RemoveData(); // 데이터 제거
            }
        }

        public bool TrySample(Vector3 worldPosition, out Vector3 onMesh) // 지점을 NavMesh 위로 붙이기
        {
            if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas)) // 조회
            {
                onMesh = hit.position; // 결과
                return true; // 성공
            }

            onMesh = worldPosition; // 원래 위치
            return false; // 실패
        }

        public bool IsReachable(Vector3 from, Vector3 to) // 두 지점이 NavMesh 로 이어져 있는지
        {
            if (!TrySample(from, out Vector3 start) || !TrySample(to, out Vector3 end)) // NavMesh 위로 붙이기
            {
                return false; // 실패
            }

            NavMeshPath path = new NavMeshPath(); // 경로
            return NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete; // 완전한 경로인지
        }

        public List<int> FindUnreachableModules(ModuleDungeonGenerator generator) // 시작 방에서 걸어갈 수 없는 모듈 목록
        {
            List<int> unreachable = new List<int>(); // 결과
            LastReasons.Clear(); // 사유 초기화

            if (generator == null || generator.Plan == null) // 생성 전
            {
                return unreachable; // 빈 목록
            }

            Vector3 start = generator.ModuleCenterWorld(generator.Plan.Module(generator.Plan.EntranceIndex)) + (Vector3.up * 0.3f); // 시작 방 기준점

            foreach (PlacedModule module in generator.Plan.Modules) // 모듈 순회
            {
                if (module.Index == generator.Plan.EntranceIndex) // 시작 방
                {
                    continue; // 다음
                }

                Vector3 target = generator.ModuleCenterWorld(module) + (Vector3.up * 0.3f); // 목표

                if (!TrySample(target, out Vector3 _)) // 이 모듈에 NavMesh 자체가 없음
                {
                    unreachable.Add(module.Index); // 등록
                    LastReasons[module.Index] = "NavMesh 없음"; // 사유
                    continue; // 다음
                }

                if (!IsReachable(start, target)) // 경로 없음
                {
                    unreachable.Add(module.Index); // 등록
                    LastReasons[module.Index] = "경로 없음"; // 사유
                }
            }

            return unreachable; // 반환
        }
    }
}
