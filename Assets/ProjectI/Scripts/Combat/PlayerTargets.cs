using System.Collections.Generic; // 대상 목록
using ProjectI.Net; // 다른 원정대원 피격 대상
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Combat // 공통 전투 시스템 네임스페이스
{
    public static class PlayerTargets // 39일차: 몬스터가 찾는 플레이어 대상 (내 플레이어 + 다른 원정대원 몸체)
    {
        private static readonly List<Transform> Buffer = new List<Transform>(); // 재사용 목록
        private static PlayerDamageReceiver localReceiver; // 내 플레이어

        public static IDamageable ReceiverOf(Component target) // 대상 계층의 플레이어 피격 대상 (없으면 null)
        {
            if (target == null) // 없음
            {
                return null; // 대상 아님
            }

            PlayerDamageReceiver local = target.GetComponentInParent<PlayerDamageReceiver>(); // 내 플레이어

            if (local != null) // 있음
            {
                return local; // 반환
            }

            return target.GetComponentInParent<RemotePlayerTarget>(); // 다른 원정대원
        }

        public static Transform RootOf(Component target) // 대상 계층의 플레이어 루트
        {
            IDamageable receiver = ReceiverOf(target); // 대상
            return receiver == null ? null : receiver.DamageTransform; // 루트
        }

        public static bool IsAlivePlayer(Component target) // 살아 있는 플레이어 계층인지
        {
            IDamageable receiver = ReceiverOf(target); // 대상
            return receiver != null && receiver.IsAlive; // 결과
        }

        public static List<Transform> Candidates() // 살아 있는 플레이어 목록 (몬스터 AI 는 방장에서만 사용)
        {
            Buffer.Clear(); // 비우기

            if (localReceiver == null || !localReceiver.isActiveAndEnabled) // 내 플레이어
            {
                localReceiver = Object.FindFirstObjectByType<PlayerDamageReceiver>(); // 검색
            }

            if (localReceiver != null && localReceiver.IsAlive) // 살아 있음
            {
                Buffer.Add(localReceiver.transform); // 추가
            }

            foreach (RemotePlayerTarget remote in RemotePlayerTarget.Active) // 다른 원정대원
            {
                if (remote != null && remote.IsAlive && remote.IsInMyMap) // 살아 있고 같은 맵
                {
                    Buffer.Add(remote.transform); // 추가
                }
            }

            return Buffer; // 반환
        }

        public static Transform Nearest(Vector3 position) // 가장 가까운 살아 있는 플레이어
        {
            Transform best = null; // 결과
            float bestDistance = float.PositiveInfinity; // 거리

            foreach (Transform candidate in Candidates()) // 목록
            {
                float distance = (candidate.position - position).sqrMagnitude; // 거리

                if (distance < bestDistance) // 더 가까움
                {
                    bestDistance = distance; // 기록
                    best = candidate; // 기록
                }
            }

            return best; // 반환
        }

        public static bool IsObservedByRemote(Vector3 point, float halfAngle, Transform targetRoot) // 다른 원정대원 누군가가 그 지점을 보고 있는지 (웃는 석상)
        {
            foreach (RemotePlayerTarget remote in RemotePlayerTarget.Active) // 다른 원정대원
            {
                if (remote != null && remote.IsAlive && remote.IsInMyMap && remote.CanSee(point, halfAngle, targetRoot)) // 보고 있음
                {
                    return true; // 관찰됨
                }
            }

            return false; // 아무도 안 봄
        }
    }
}
