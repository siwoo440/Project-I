using UnityEngine; // 벡터와 수학 기능 참조

namespace ProjectI.Monsters // 몬스터 공통 AI 네임스페이스
{
    public static class MonsterBallistics // 중력 영향을 받는 화살의 낮은 포물선 초기 속도 계산 도우미
    {
        public static bool TryCalculateLaunchVelocity(Vector3 origin, Vector3 target, float speed, float gravityMagnitude, out Vector3 velocity) // 지정 속도로 목표를 향하는 낮은 포물선 속도 계산
        {
            velocity = Vector3.zero; // 실패 기본값 초기화
            float safeSpeed = Mathf.Max(0.1f, speed); // 발사 속도 최소값 보정
            float gravity = Mathf.Max(0.01f, gravityMagnitude); // 중력 크기 최소값 보정
            Vector3 delta = target - origin; // 발사점에서 목표점까지 전체 차이 계산
            Vector3 horizontal = Vector3.ProjectOnPlane(delta, Vector3.up); // 수평 거리 벡터 계산
            float horizontalDistance = horizontal.magnitude; // 수평 거리 크기 계산

            if (horizontalDistance < 0.01f) // 거의 수직 방향 목표인지 확인
            {
                velocity = delta.normalized * safeSpeed; // 수직에 가까운 경우 직접 속도 사용
                return delta.sqrMagnitude > 0.0001f; // 유효 목표 거리 여부 반환
            }

            float speedSquared = safeSpeed * safeSpeed; // 발사 속도 제곱 계산
            float discriminant = (speedSquared * speedSquared) - (gravity * ((gravity * horizontalDistance * horizontalDistance) + (2f * delta.y * speedSquared))); // 탄도 해 존재 여부 판별식 계산

            if (discriminant < 0f) // 지정 속도로 목표 도달 가능한 탄도 해가 없는지 확인
            {
                Vector3 fallback = (delta.normalized + (Vector3.up * 0.08f)).normalized; // 목표 방향에 작은 상승 보정을 더한 대체 방향 생성
                velocity = fallback * safeSpeed; // 대체 직선 기반 초기 속도 적용
                return false; // 정확한 탄도 해 없음 반환
            }

            float root = Mathf.Sqrt(discriminant); // 판별식 제곱근 계산
            float tangent = (speedSquared - root) / (gravity * horizontalDistance); // 낮은 각도 탄도의 탄젠트 계산
            float cosine = 1f / Mathf.Sqrt(1f + (tangent * tangent)); // 발사각 코사인 계산
            float sine = tangent * cosine; // 발사각 사인 계산
            Vector3 horizontalDirection = horizontal / horizontalDistance; // 수평 발사 방향 정규화
            velocity = (horizontalDirection * (safeSpeed * cosine)) + (Vector3.up * (safeSpeed * sine)); // 수평·수직 성분을 결합한 초기 속도 생성
            return true; // 정확한 낮은 포물선 탄도 계산 성공 반환
        }
    }
}
