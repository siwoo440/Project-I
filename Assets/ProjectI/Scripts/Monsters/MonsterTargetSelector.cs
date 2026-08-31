using UnityEngine; // Transform·Vector3·시간 기능 참조

namespace ProjectI.Monsters // 몬스터 공통 AI 네임스페이스
{
    public sealed class MonsterTargetSelector : MonoBehaviour // 현재 대상과 마지막 확인 위치 기억을 관리하는 공통 대상 선택기
    {
        private Transform currentTarget; // 현재 공격·추적 대상
        private Vector3 lastKnownPosition; // 마지막으로 확인하거나 들은 월드 위치
        private float memoryUntil; // 마지막 위치 기억 만료 시각
        private bool hasLastKnownPosition; // 저장된 마지막 위치 존재 여부

        public Transform CurrentTarget => currentTarget; // 현재 대상 공개
        public Vector3 LastKnownPosition => lastKnownPosition; // 마지막 확인 위치 공개
        public bool HasMemory => hasLastKnownPosition && Time.time <= memoryUntil; // 현재 유효한 위치 기억 여부 공개
        public float MemoryRemaining => HasMemory ? Mathf.Max(0f, memoryUntil - Time.time) : 0f; // 남은 기억 시간 공개

        public void SetTarget(Transform target) // 직접 감지하거나 공격받은 대상을 현재 대상으로 지정
        {
            currentTarget = target; // 현재 대상 저장
        }

        public void Remember(Vector3 position, float duration) // 마지막 확인·소리 위치 기억 갱신
        {
            lastKnownPosition = position; // 최신 월드 위치 저장
            hasLastKnownPosition = true; // 위치 기억 존재 상태 활성화
            memoryUntil = Time.time + Mathf.Max(0.1f, duration); // 기억 만료 시각 계산
        }

        public void ClearTarget() // 현재 직접 대상 참조 해제
        {
            currentTarget = null; // 현재 대상 제거
        }

        public void ClearMemory() // 마지막 위치 기억 완전 초기화
        {
            hasLastKnownPosition = false; // 위치 기억 없음 상태 저장
            memoryUntil = 0f; // 기억 만료 시각 초기화
        }
    }
}
