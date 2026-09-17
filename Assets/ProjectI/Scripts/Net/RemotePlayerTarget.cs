using System.Collections.Generic; // 목록
using ProjectI.Combat; // 피격 대상 규격
using UnityEngine; // 유니티 기본 기능 참조

namespace ProjectI.Net // 협동 네트워크 네임스페이스
{
    [RequireComponent(typeof(NetPlayerAvatar))] // 원정대원 몸체
    public sealed class RemotePlayerTarget : MonoBehaviour, IDamageable // 39일차: 방장 몬스터가 다른 원정대원을 보고·공격할 수 있게 하는 대상 (피해는 그 대원에게 전달)
    {
        public static readonly List<RemotePlayerTarget> Active = new List<RemotePlayerTarget>(); // 다른 원정대원 대상

        private NetPlayerAvatar avatar; // 몸체
        private CapsuleCollider body; // 시야·명중 판정용 몸체 충돌
        private Vector3 lastNoisePosition; // 발소리 기준 위치
        private float noiseDistance; // 발소리까지 누적 거리

        public CombatFaction Faction => CombatFaction.Player; // 플레이어 진영
        public bool IsAlive => avatar != null && avatar.IsSpawned && !avatar.IsDeadRemote; // 살아 있음
        public Transform DamageTransform => transform; // 대표 위치
        public bool IsInMyMap => avatar != null && avatar.IsVisibleHere; // 내 화면과 같은 맵
        public ulong OwnerClientId => avatar == null ? 0 : avatar.OwnerClientId; // 대원 번호

        private void Awake() // 준비
        {
            avatar = GetComponent<NetPlayerAvatar>(); // 몸체
            body = gameObject.AddComponent<CapsuleCollider>(); // 몸체 충돌 (막힘·시야·명중)
            body.center = new Vector3(0f, 0.9f, 0f); // 중심
            body.height = 1.8f; // 높이
            body.radius = 0.32f; // 반지름
            body.enabled = false; // 생성 직후에는 끔 (내 몸체면 계속 끔)
        }

        private void OnEnable() // 등록
        {
            if (!Active.Contains(this)) // 중복
            {
                Active.Add(this); // 추가
            }
        }

        private void OnDisable() // 해제
        {
            Active.Remove(this); // 제거
        }

        private void LateUpdate() // 몸체 충돌 켜기·끄기 · 방장 몬스터가 듣는 발소리
        {
            bool remote = avatar != null && avatar.IsSpawned && !avatar.IsOwner; // 다른 대원 몸체
            bool shouldCollide = remote && IsInMyMap && IsAlive; // 같은 맵에 서 있는 다른 대원만
            body.enabled = shouldCollide; // 적용

            if (!shouldCollide || !NetCombatSync.IsAuthority) // 방장만 소리 발생
            {
                lastNoisePosition = transform.position; // 기준 갱신
                return; // 종료
            }

            Vector3 delta = Vector3.ProjectOnPlane(transform.position - lastNoisePosition, Vector3.up); // 이동 거리
            float speed = delta.magnitude / Mathf.Max(0.0001f, Time.deltaTime); // 속도
            noiseDistance += delta.magnitude; // 누적
            lastNoisePosition = transform.position; // 기준 갱신

            if (noiseDistance < 1.8f) // 한 걸음 전
            {
                return; // 대기
            }

            noiseDistance = 0f; // 초기화
            bool sprinting = speed > 4.2f; // 달리기 추정
            ProjectI.Monsters.MonsterNoiseSystem.Emit(gameObject, transform.position, sprinting ? 12f : 6.5f, sprinting ? 0.7f : 0.35f, ProjectI.Monsters.MonsterNoiseKind.Footstep, sprinting ? "Crew Sprint" : "Crew Walk"); // 발소리
        }

        public float ApplyDamage(DamageInfo damageInfo) // 방장: 몬스터 공격을 그 대원에게 전달 (함정은 각자 화면에서 처리)
        {
            if (!NetCombatSync.IsAuthority || damageInfo.SourceFaction != CombatFaction.Enemy || !IsAlive) // 방장 몬스터 공격만
            {
                return 0f; // 적용 안 함
            }

            NetCombatSync.SendPlayerDamage(OwnerClientId, damageInfo); // 전달
            return damageInfo.BaseDamage; // 적용된 것으로 처리 (체력은 그 대원이 가짐)
        }

        public bool CanSee(Vector3 point, float halfAngle, Transform targetRoot) // 이 대원이 그 지점을 보고 있는지 (좌우 방향 기준, 대상 자신의 충돌은 가림이 아님)
        {
            Vector3 eye = transform.position + (Vector3.up * 1.6f); // 눈
            Vector3 toPoint = point - eye; // 방향

            if (toPoint.sqrMagnitude < 0.01f || Vector3.Angle(Vector3.ProjectOnPlane(transform.forward, Vector3.up), Vector3.ProjectOnPlane(toPoint, Vector3.up)) > halfAngle) // 시야 밖
            {
                return false; // 못 봄
            }

            if (Physics.Raycast(eye, toPoint.normalized, out RaycastHit hit, toPoint.magnitude - 0.2f, ~0, QueryTriggerInteraction.Ignore) && hit.collider != body && (targetRoot == null || !hit.collider.transform.IsChildOf(targetRoot))) // 가림
            {
                return false; // 못 봄
            }

            return true; // 봄
        }
    }
}
