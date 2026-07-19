using UnityEngine; // Unity의 물리 검사, 위치, 수학 기능 사용

namespace ProjectI // 프로젝트 공통 네임스페이스
{
    public static class DungeonSpawnUtility // 던전 자동 스폰이 공통으로 사용하는 기능 모음
    {
        public static float GetBrightnessMultiplier(Room room) // 방의 기본 밝기를 스폰 및 가치 배율로 변환
        {
            if (room == null) // 전달된 방이 없는지 확인
            {
                return 1f; // 방이 없으면 기본 배율 반환
            }

            LightRoom lightRoom = room.GetComponent<LightRoom>(); // 방 루트에서 LightRoom 검색

            if (lightRoom == null) // 방 루트에서 찾지 못했는지 확인
            {
                lightRoom = room.GetComponentInChildren<LightRoom>(); // 방 자식 오브젝트에서 LightRoom 검색
            }

            float brightness = lightRoom != null ? lightRoom.FixedBrightness : 0f; // LightRoom의 기본 밝기를 가져오거나 칠흑으로 처리

            if (brightness >= 81f) // 매우 밝은 방인지 확인
            {
                return 0.5f; // 생성량과 가치에 50% 배율 적용
            }

            if (brightness >= 61f) // 밝은 방인지 확인
            {
                return 0.8f; // 생성량과 가치에 80% 배율 적용
            }

            if (brightness >= 41f) // 보통 밝기의 방인지 확인
            {
                return 1f; // 생성량과 가치에 기본 배율 적용
            }

            if (brightness >= 21f) // 어두운 방인지 확인
            {
                return 1.5f; // 생성량과 가치에 150% 배율 적용
            }

            return 2f; // 칠흑 방에 200% 배율 적용
        }

        public static bool TryFindSpawnPosition( // 방 안에서 안전한 생성 위치 검색
            Room room, // 생성할 방
            Transform player, // 안전거리를 확인할 플레이어 Transform
            float wallMargin, // 벽으로부터 떨어질 거리
            float objectHeight, // 바닥에서 오브젝트를 띄울 높이
            float collisionRadius, // 다른 오브젝트와의 충돌 검사 반경
            int searchAttempts, // 위치 검색 최대 횟수
            float playerSafeDistance, // 플레이어 주변 생성 금지 거리
            out Vector3 spawnPosition) // 검색에 성공한 생성 위치
        {
            spawnPosition = Vector3.zero; // 검색 실패에 대비해 기본 위치 설정

            if (room == null) // 전달된 방이 없는지 확인
            {
                return false; // 위치 검색 실패 반환
            }

            LightRoom lightRoom = room.GetComponent<LightRoom>(); // 방 루트에서 LightRoom 검색

            if (lightRoom == null) // 방 루트에서 찾지 못했는지 확인
            {
                lightRoom = room.GetComponentInChildren<LightRoom>(); // 방 자식 오브젝트에서 LightRoom 검색
            }

            if (lightRoom == null) // 방 전체에서 LightRoom을 찾지 못했는지 확인
            {
                Debug.LogWarning($"[DungeonSpawnUtility] {room.name}에 LightRoom이 없습니다."); // 설정 누락 경고 출력
                return false; // 위치 검색 실패 반환
            }

            Collider roomCollider = lightRoom.GetComponent<Collider>(); // LightRoom 영역의 Collider 검색

            if (roomCollider == null) // LightRoom 오브젝트에 Collider가 없는지 확인
            {
                Debug.LogWarning($"[DungeonSpawnUtility] {room.name}의 LightRoom에 Collider가 없습니다."); // 설정 누락 경고 출력
                return false; // 위치 검색 실패 반환
            }

            Bounds bounds = roomCollider.bounds; // LightRoom Collider의 월드 영역 가져오기
            float safeWallMargin = Mathf.Max(0f, wallMargin); // 벽 여백이 음수가 되지 않도록 제한
            float minX = bounds.min.x + safeWallMargin; // 사용할 수 있는 최소 X 좌표 계산
            float maxX = bounds.max.x - safeWallMargin; // 사용할 수 있는 최대 X 좌표 계산
            float minZ = bounds.min.z + safeWallMargin; // 사용할 수 있는 최소 Z 좌표 계산
            float maxZ = bounds.max.z - safeWallMargin; // 사용할 수 있는 최대 Z 좌표 계산

            if (minX >= maxX || minZ >= maxZ) // 벽 여백을 제외한 공간이 남아 있는지 확인
            {
                Debug.LogWarning($"[DungeonSpawnUtility] {room.name}의 스폰 가능 영역이 너무 작습니다."); // 방 크기 문제 경고 출력
                return false; // 위치 검색 실패 반환
            }

            int safeSearchAttempts = Mathf.Max(1, searchAttempts); // 위치 검색을 최소 한 번은 실행하도록 제한

            for (int attempt = 0; attempt < safeSearchAttempts; attempt++) // 설정된 횟수만큼 위치 검색 반복
            {
                float randomX = Random.Range(minX, maxX); // 방 내부의 무작위 X 좌표 선택
                float randomZ = Random.Range(minZ, maxZ); // 방 내부의 무작위 Z 좌표 선택
                Vector3 rayOrigin = new Vector3(randomX, bounds.max.y + 2f, randomZ); // 방 위쪽에 바닥 검색용 Ray 시작점 생성
                float rayDistance = bounds.size.y + 5f; // 방 전체를 통과할 수 있는 Ray 거리 계산

                if (!Physics.Raycast( // 아래 방향으로 실제 바닥 검색
                    rayOrigin, // Ray 시작 위치 전달
                    Vector3.down, // 아래 방향으로 Ray 발사
                    out RaycastHit floorHit, // 감지된 바닥 충돌 정보 저장
                    rayDistance, // Ray가 이동할 최대 거리 전달
                    Physics.AllLayers, // 모든 물리 레이어를 검사
                    QueryTriggerInteraction.Ignore)) // LightRoom 같은 Trigger Collider는 검사에서 제외
                {
                    continue; // 바닥을 찾지 못하면 다른 위치 검색
                }

                Vector3 candidate = floorHit.point + Vector3.up * objectHeight; // 바닥에서 지정된 높이만큼 올린 후보 위치 계산

                if (!lightRoom.Contains(candidate)) // 후보 위치가 실제 LightRoom 영역 안인지 확인
                {
                    continue; // 방 영역 밖이면 다른 위치 검색
                }

                if (player != null && Vector3.Distance(candidate, player.position) < playerSafeDistance) // 플레이어와 후보 위치의 거리를 확인
                {
                    continue; // 플레이어와 너무 가까우면 다른 위치 검색
                }

                if (Physics.CheckSphere( // 후보 위치 주변에 장애물이 있는지 검사
                    candidate, // 충돌 검사 중심 위치 전달
                    collisionRadius, // 충돌 검사 반경 전달
                    Physics.AllLayers, // 모든 물리 레이어를 검사
                    QueryTriggerInteraction.Ignore)) // LightRoom 같은 Trigger Collider는 검사에서 제외
                {
                    continue; // 실제 장애물과 겹치면 다른 위치 검색
                }

                spawnPosition = candidate; // 검증을 통과한 위치 저장
                return true; // 위치 검색 성공 반환
            }

            return false; // 모든 검색에서 위치를 찾지 못했음을 반환
        }
    }
}
