using UnityEngine;

namespace ProjectI
{
    /// <summary>
    /// 몬스터 기본 AI. (기획서 PART 7.3~7.5)
    /// FSM: 배회(Patrol) → 탐지 → 추격(Chase) → 공격(Attack) → (놓치면) 배회 복귀.
    /// 탐지: 시각(시야각/거리/가림) + 청각(플레이어 이동 속도 = 소음). 밝기 연동: 어두우면 감지력↑.
    /// 이동은 단순 스티어링(직선). ※정밀 경로탐색(NavMesh)은 이후 개선.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MonsterAI : MonoBehaviour
    {
        [SerializeField] MonsterData data;

        [Header("Fallback 스탯 (data 없을 때 사용)")]
        [SerializeField] string fbName = "몬스터";
        [SerializeField] float fbMaxHealth = 80f;
        [SerializeField] float fbDefense = 0f;
        [SerializeField] float fbMoveSpeed = 3.5f;
        [SerializeField] float fbAttackDamage = 20f;
        [SerializeField] float fbAttackRange = 1.6f;
        [SerializeField] float fbAttackCooldown = 1.5f;
        [SerializeField] float fbSightRange = 12f;
        [SerializeField] float fbSightAngle = 110f;
        [SerializeField] float fbHearingRange = 12f;
        [SerializeField] bool fbAggressiveInDark = true;

        [Header("기타")]
        [SerializeField] float gravity = -20f;
        [SerializeField] float noiseToRadius = 1.5f; // 플레이어 속도 → 소음 반경

        enum State { Patrol, Chase, Attack }
        State state = State.Patrol;

        CharacterController cc;
        PlayerController player;
        CharacterController playerCC;
        LightSystem playerLight;

        float health, vy, lastAttackTime, patrolTimer;
        Vector3 patrolTarget, horiz;

        string Name => data != null ? data.displayName : fbName;
        float MaxHealth => data != null ? data.maxHealth : fbMaxHealth;
        public float Defense => data != null ? data.defense : fbDefense;
        float MoveSpeed => data != null ? data.moveSpeed : fbMoveSpeed;
        float AttackDamage => data != null ? data.attackDamage : fbAttackDamage;
        float AttackRange => data != null ? data.attackRange : fbAttackRange;
        float AttackCooldown => data != null ? data.attackCooldown : fbAttackCooldown;
        float SightRange => data != null ? data.sightRange : fbSightRange;
        float SightAngle => data != null ? data.sightAngle : fbSightAngle;
        float HearingRange => data != null ? data.hearingRange : fbHearingRange;
        bool AggressiveInDark => data != null ? data.aggressiveInDark : fbAggressiveInDark;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            health = MaxHealth;
        }

        void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                playerCC = player.GetComponent<CharacterController>();
                playerLight = player.GetComponent<LightSystem>();
            }
            PickPatrol();
        }

        void Update()
        {
            horiz = Vector3.zero;

            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);
                bool detected = CanSee(dist) || CanHear(dist);

                switch (state)
                {
                    case State.Patrol:
                        DoPatrol();
                        if (detected) state = State.Chase;
                        break;
                    case State.Chase:
                        MoveToward(player.transform.position, MoveSpeed);
                        if (dist <= AttackRange) state = State.Attack;
                        else if (!detected && dist > SightRange * DarkMul() * 1.3f) state = State.Patrol;
                        break;
                    case State.Attack:
                        FaceToward(player.transform.position);
                        if (dist > AttackRange * 1.25f) state = State.Chase;
                        else TryAttack();
                        break;
                }
            }

            // 중력 + 수평 이동을 한 번에 적용
            if (cc.isGrounded && vy < 0f) vy = -2f;
            vy += gravity * Time.deltaTime;
            cc.Move((horiz + Vector3.up * vy) * Time.deltaTime);
        }

        // ---- 탐지 ----
        bool CanSee(float dist)
        {
            float range = SightRange * DarkMul();
            if (dist > range) return false;
            Vector3 to = player.transform.position - transform.position; to.y = 0f;
            if (Vector3.Angle(transform.forward, to) > SightAngle * 0.5f) return false;

            Vector3 eye = transform.position + Vector3.up * 1f;
            Vector3 tgt = player.transform.position + Vector3.up * 1f;
            if (Physics.Raycast(eye, (tgt - eye).normalized, out var hit, dist + 1f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<PlayerController>() == null) return false; // 벽 등에 가림
            return true;
        }

        bool CanHear(float dist)
        {
            if (dist > HearingRange || playerCC == null) return false;
            Vector3 v = playerCC.velocity; v.y = 0f;
            float noiseRadius = v.magnitude * noiseToRadius; // 가만히=0, 걷기=작게, 달리기=크게
            return dist <= noiseRadius;
        }

        float DarkMul()
        {
            if (!AggressiveInDark || playerLight == null) return 1f;
            return playerLight.CurrentBrightness < 40f ? 1.5f : 1f; // 어두우면 감지 범위↑
        }

        // ---- 이동 ----
        void DoPatrol()
        {
            patrolTimer -= Time.deltaTime;
            MoveToward(patrolTarget, MoveSpeed * 0.5f);
            if (patrolTimer <= 0f || HorizDist(patrolTarget) < 1f) PickPatrol();
        }
        void PickPatrol()
        {
            Vector2 r = Random.insideUnitCircle * 5f;
            patrolTarget = transform.position + new Vector3(r.x, 0f, r.y);
            patrolTimer = Random.Range(2f, 4f);
        }
        void MoveToward(Vector3 target, float speed)
        {
            Vector3 d = target - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) { d.Normalize(); FaceDir(d); horiz = d * speed; }
        }
        void FaceToward(Vector3 target)
        {
            Vector3 d = target - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) FaceDir(d.normalized);
        }
        void FaceDir(Vector3 dir) => transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
        float HorizDist(Vector3 p) { Vector3 d = p - transform.position; d.y = 0f; return d.magnitude; }

        // ---- 전투 ----
        void TryAttack()
        {
            if (Time.time - lastAttackTime < AttackCooldown) return;
            lastAttackTime = Time.time;
            player.TakeDamage(AttackDamage);
            Debug.Log($"[{Name}] 공격! (-{AttackDamage:F0})");
        }

        /// <summary>몬스터 피격(플레이어 공격은 12일차에서 연동).</summary>
        public void TakeDamage(float amount)
        {
            health -= amount;
            if (health <= 0f) { Debug.Log($"[{Name}] 처치됨"); Destroy(gameObject); }
        }
    }
}
