using System; // 조건 대리자 사용
using System.Collections.Generic; // 목록 사용
using ProjectI.Items; // WorldItem 참조
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Economy // 사무소 경제 기능 네임스페이스
{
    public static class OfficeSurfaceSlots // 판매대·수령대처럼 윗면에 물건을 놓는 곳의 자리 계산 (33일차 공용화)
    {
        public static bool TryGetLocalBox(Transform surface, out Vector3 localMin, out Vector3 localMax) // 윗면 상자 콜라이더의 로컬 범위
        {
            BoxCollider box = surface == null ? null : surface.GetComponent<BoxCollider>(); // 상자 콜라이더

            if (box != null) // 상자 기준
            {
                localMin = box.center - (box.size * 0.5f); // 최소
                localMax = box.center + (box.size * 0.5f); // 최대
                return true; // 성공
            }

            Collider anyCollider = surface == null ? null : surface.GetComponent<Collider>(); // 다른 콜라이더

            if (anyCollider == null) // 없음
            {
                localMin = localMax = Vector3.zero; // 초기화
                return false; // 실패
            }

            Bounds bounds = anyCollider.bounds; // 월드 범위
            localMin = surface.InverseTransformPoint(bounds.min); // 근사 최소
            localMax = surface.InverseTransformPoint(bounds.max); // 근사 최대
            return true; // 성공
        }

        public static float TopWorldY(Transform surface) // 윗면 월드 높이
        {
            return TryGetLocalBox(surface, out _, out Vector3 localMax) ? surface.TransformPoint(new Vector3(0f, localMax.y, 0f)).y : surface.position.y; // 계산
        }

        public static bool IsOnSurface(Transform surface, Vector3 worldPosition, float zoneHeight) // 위치가 윗면 위인지
        {
            if (!TryGetLocalBox(surface, out Vector3 localMin, out Vector3 localMax)) // 범위
            {
                return false; // 판정 불가
            }

            Vector3 local = surface.InverseTransformPoint(worldPosition); // 로컬 좌표
            float topY = surface.TransformPoint(new Vector3(0f, localMax.y, 0f)).y; // 윗면 높이
            return local.x >= localMin.x && local.x <= localMax.x && local.z >= localMin.z && local.z <= localMax.z && worldPosition.y >= topY - 0.05f && worldPosition.y <= topY + zoneHeight; // 윗면 영역 안
        }

        public static List<WorldItem> CollectItems(Transform surface, float zoneHeight, Predicate<WorldItem> filter) // 윗면 위에 놓인 월드 물체
        {
            List<WorldItem> result = new List<WorldItem>(); // 결과

            if (surface == null) // 대상 없음
            {
                return result; // 빈 목록
            }

            foreach (WorldItem item in UnityEngine.Object.FindObjectsByType<WorldItem>(FindObjectsSortMode.None)) // 활성 WorldItem
            {
                if (item != null && item.gameObject.scene == surface.gameObject.scene && !item.IsHeld && !item.IsStored && (filter == null || filter(item)) && IsOnSurface(surface, item.transform.position, zoneHeight)) // 윗면 위
                {
                    result.Add(item); // 등록
                }
            }

            result.Sort((a, b) => surface.InverseTransformPoint(a.transform.position).x.CompareTo(surface.InverseTransformPoint(b.transform.position).x)); // 왼쪽부터
            return result; // 반환
        }

        public static List<Vector3> FindFreeSlots(Transform surface, IList<WorldItem> occupiedBy, int count, float spacing, float margin, float radius) // 비어 있는 자리 여러 개 (윗면 높이)
        {
            List<Vector3> slots = new List<Vector3>(); // 결과

            if (count <= 0 || !TryGetLocalBox(surface, out Vector3 localMin, out Vector3 localMax)) // 요청·범위 확인
            {
                return slots; // 빈 목록
            }

            List<Vector3> taken = new List<Vector3>(); // 사용 중 위치

            if (occupiedBy != null) // 기존 물체
            {
                foreach (WorldItem item in occupiedBy) // 순회
                {
                    if (item != null) // 확인
                    {
                        taken.Add(item.transform.position); // 등록
                    }
                }
            }

            float minX = localMin.x + LocalLength(surface, margin, Vector3.right); // 왼쪽 끝
            float maxX = localMax.x - LocalLength(surface, margin, Vector3.right); // 오른쪽 끝
            float minZ = localMin.z + LocalLength(surface, margin, Vector3.forward); // 앞쪽 끝
            float maxZ = localMax.z - LocalLength(surface, margin, Vector3.forward); // 뒤쪽 끝
            float stepX = LocalLength(surface, spacing, Vector3.right); // 가로 간격
            float stepZ = LocalLength(surface, spacing, Vector3.forward); // 세로 간격

            for (float z = minZ; z <= maxZ + 0.0001f; z += stepZ) // 줄 순회
            {
                for (float x = minX; x <= maxX + 0.0001f; x += stepX) // 칸 순회
                {
                    Vector3 candidate = surface.TransformPoint(new Vector3(x, localMax.y, z)); // 후보
                    bool occupied = false; // 사용 여부

                    foreach (Vector3 position in taken) // 거리 비교
                    {
                        Vector3 offset = position - candidate; // 차이
                        offset.y = 0f; // 수평 거리만

                        if (offset.magnitude < radius) // 가까움
                        {
                            occupied = true; // 사용 중
                            break; // 중단
                        }
                    }

                    if (occupied) // 사용 중
                    {
                        continue; // 다음
                    }

                    slots.Add(candidate); // 등록
                    taken.Add(candidate); // 같은 요청 안에서 중복 방지

                    if (slots.Count >= count) // 충분
                    {
                        return slots; // 반환
                    }
                }
            }

            return slots; // 찾은 만큼 반환
        }

        private static float LocalLength(Transform surface, float worldLength, Vector3 localAxis) // 월드 길이 → 로컬 길이
        {
            float scale = surface.TransformVector(localAxis).magnitude; // 축 배율
            return scale <= 0.0001f ? worldLength : worldLength / scale; // 변환
        }
    }
}
