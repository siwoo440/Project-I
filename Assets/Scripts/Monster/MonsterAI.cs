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
        MonsterNavigation navigation; // NavMesh 경로 이동 방향을 제공하는 보조 컴포넌트
        PlayerController player;
        CharacterController playerCC;
        LightSystem playerLight;
        ThreatFeedback threatFeedback; // 공격 피드백 재생용 공통 컴포넌트

        float health, vy, lastAttackTime, patrolTimer;
        public event System.Action Damaged; // 외부 기믹에 몬스터 피격 사실 전달
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
            navigation = GetComponent<MonsterNavigation>(); // 같은 오브젝트의 선택적 NavMesh 이동 컴포넌트 검색
            threatFeedback = GetComponent<ThreatFeedback>(); // 같은 오브젝트의 공통 피드백 검색
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
        void MoveToward(Vector3 target, float speed) // NavMesh 또는 기존 직선 방향으로 목표에 이동
        {
            Vector3 direction = Vector3.zero; // 이번 프레임에 사용할 수평 이동 방향 초기화
            bool navigationHandled = false; // NavMesh가 이동 요청을 처리했는지 저장

            if (navigation != null) // NavMesh 이동 컴포넌트가 연결되어 있는지 확인
            {
                navigationHandled = navigation.TryGetMoveDirection( // 목표까지의 NavMesh 이동 방향 요청
                    target, // 이동할 목표 위치 전달
                    speed, // 현재 FSM에서 사용할 이동속도 전달
                    out direction); // 계산된 이동 방향 저장
            }

            if (!navigationHandled) // NavMesh가 없거나 현재 몬스터를 NavMesh에 배치할 수 없는지 확인
            {
                direction = target - transform.position; // 기존 방식으로 목표까지의 직선 방향 계산
                direction.y = 0f; // 수직 방향 제외

                if (direction.sqrMagnitude > 0.01f) // 유효한 직선 방향인지 확인
                {
                    direction.Normalize(); // 일정한 이동속도를 위해 단위 방향으로 변환
                }
            }

            direction.y = 0f; // NavMesh 방향에서도 수직 이동 제거

            if (direction.sqrMagnitude <= 0.01f) // 이동 가능한 방향이 없는지 확인
            {
                return; // 현재 프레임 수평 이동 중단
            }

            direction.Normalize(); // 경로 방향을 안전한 단위 벡터로 변환
            FaceDir(direction); // 이동할 통로 방향으로 몬스터 회전
            horiz = direction * speed; // 기존 CharacterController가 사용할 수평속도 저장
        }

        void FaceToward(Vector3 target)
        {
            Vector3 d = target - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) FaceDir(d.normalized);
        }
        void FaceDir(Vector3 dir) => transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
        float HorizDist(Vector3 p) { Vector3 d = p - transform.position; d.y = 0f; return d.magnitude; }

        // ---- 전투 ----
        public void Alert() // 외부 기믹에서 몬스터를 즉시 추격 상태로 변경
        {
            state = State.Chase; // 현재 FSM 상태를 플레이어 추격으로 변경
        }

        void TryAttack()
        {
            if (Time.time - lastAttackTime < AttackCooldown) return;
            lastAttackTime = Time.time;
            player.TakeDamage(AttackDamage);
            if (threatFeedback != null) // 공통 피드백 존재 여부 확인
            {
                threatFeedback.PlayAttack(); // 몬스터 공격 소리와 파티클 재생
            }

            Debug.Log($"[{Name}] 공격! (-{AttackDamage:F0})");
        }

        /// <summary>몬스터 피격(플레이어 공격은 12일차에서 연동).</summary>
        public void TakeDamage(float amount) // 플레이어 공격으로 몬스터 체력 감소
        {
            float safeDamage = Mathf.Max(0f, amount); // 전달된 피해가 음수가 되지 않도록 제한

            Damaged?.Invoke(); // 체력을 줄이기 전에 외부 기믹에 피격 사실 전달
            health -= safeDamage; // 몬스터 현재 체력 감소

            if (health <= 0f) // 몬스터 체력이 모두 소진되었는지 확인
            {
                Debug.Log($"[{Name}] 처치됨"); // 몬스터 처치 결과 출력
                Destroy(gameObject); // 몬스터 게임 오브젝트 제거
            }
        }
    }
}
