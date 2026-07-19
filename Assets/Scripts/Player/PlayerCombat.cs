using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectI
{
    /// <summary>
    /// 플레이어 기본 공격. (기획서 PART 4.5) 좌클릭 → 전방을 감지해 몬스터에게 데미지.
    /// 데미지 = max(1, 공격력 × 치명타배수 − 대상 방어력).
    /// ※ 무기별 수치(WeaponData)는 13일차, 방어/막기·사망은 14일차에서 연동. 지금은 기본 공격력.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("기본 공격 (임시 수치)")]
        [SerializeField] float attackDamage = 30f;
        [SerializeField] float attackRange = 2.5f;
        [SerializeField] float attackCooldown = 0.5f;
        [SerializeField] float hitRadius = 0.4f;
        [SerializeField, Range(0f, 1f)] float critChance = 0.2f;
        [SerializeField] float critMultiplier = 1.8f;

        Transform cam;
        float lastAttack = -999f;

        void Awake()
        {
            var c = GetComponentInChildren<Camera>();
            cam = c != null ? c.transform : transform;
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (Cursor.lockState != CursorLockMode.Locked) return; // 커서 잠금(플레이 중)일 때만
            if (mouse.leftButton.wasPressedThisFrame && Time.time - lastAttack >= attackCooldown)
                Attack();
        }

        void Attack()
        {
            lastAttack = Time.time;
            if (cam == null) return;

            // 플레이어 몸을 지나 전방으로 스피어캐스트(자기 자신 충돌 방지)
            Vector3 origin = cam.position + cam.forward * 0.6f;
            float dist = Mathf.Max(0.1f, attackRange - 0.6f);

            if (Physics.SphereCast(origin, hitRadius, cam.forward, out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                var monster = hit.collider.GetComponentInParent<MonsterAI>();
                if (monster != null)
                {
                    bool crit = Random.value < critChance;
                    float raw = attackDamage * (crit ? critMultiplier : 1f);
                    float dmg = Mathf.Max(1f, raw - monster.Defense);
                    monster.TakeDamage(dmg);
                    Debug.Log($"[전투] {dmg:F0} 피해{(crit ? " (치명타!)" : "")}");
                    return;
                }
            }
            Debug.Log("[전투] 빗나감");
        }
    }
}
