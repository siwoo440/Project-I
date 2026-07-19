using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectI
{
    /// <summary>
    /// 플레이어 전투. (기획서 PART 4.5) 손에 든 무기(WeaponData)에 따라 다르게 동작.
    /// 검/도끼=근접 SphereCast, 활=원거리 Raycast(화살 소모), 방패=우클릭 막기(피해 감소).
    /// 무기가 없으면 맨손 기본 공격. 데미지 = max(1, 공격력×치명타배수 − 방어력).
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("맨손 기본 공격")]
        [SerializeField] float unarmedDamage = 12f;
        [SerializeField] float unarmedCooldown = 0.6f;
        [SerializeField] float unarmedRange = 2f;

        [Header("공통")]
        [SerializeField] float hitRadius = 0.4f;
        [SerializeField] float bowFallbackRange = 20f;

        Transform cam;
        InventorySystem inventory;
        float lastAttack = -999f;

        public bool IsBlocking { get; private set; }
        public float CurrentBlockReduction { get; private set; } // 0~1 (방패 막기 중)

        void Awake()
        {
            var c = GetComponentInChildren<Camera>();
            cam = c != null ? c.transform : transform;
            inventory = GetComponent<InventorySystem>();
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || Cursor.lockState != CursorLockMode.Locked)
            {
                IsBlocking = false; CurrentBlockReduction = 0f; return;
            }

            var weapon = GetHeldWeapon();

            // 우클릭 방어 (방패)
            bool block = weapon != null && weapon.type == WeaponType.Shield && mouse.rightButton.isPressed;
            IsBlocking = block;
            CurrentBlockReduction = block ? weapon.blockReduction : 0f;

            // 좌클릭 공격 (무기별 쿨다운)
            float cd = weapon != null ? weapon.attackCooldown : unarmedCooldown;
            if (mouse.leftButton.wasPressedThisFrame && Time.time - lastAttack >= cd)
                Attack(weapon);
        }

        WeaponData GetHeldWeapon()
        {
            var held = cam != null ? cam.GetComponentInChildren<PickupItem>(false) : null;
            return held != null ? held.Weapon : null;
        }

        void Attack(WeaponData weapon)
        {
            lastAttack = Time.time;

            if (weapon != null && weapon.type == WeaponType.Bow) { FireBow(weapon); return; }
            if (weapon != null && weapon.type == WeaponType.Shield) { Debug.Log("[전투] 방패로는 공격 불가"); return; }

            // 근접 (맨손 / 검 / 도끼)
            float dmg = weapon != null ? weapon.attackDamage : unarmedDamage;
            float range = weapon != null ? weapon.attackRange : unarmedRange;
            float cc = weapon != null ? weapon.critChance : 0.1f;
            float cm = weapon != null ? weapon.critMultiplier : 1.5f;
            string label = weapon != null ? weapon.displayName : "맨손";

            Vector3 origin = cam.position + cam.forward * 0.6f;
            float d = Mathf.Max(0.1f, range - 0.6f);
            if (Physics.SphereCast(origin, hitRadius, cam.forward, out var hit, d, ~0, QueryTriggerInteraction.Ignore))
            {
                var m = hit.collider.GetComponentInParent<MonsterAI>();
                if (m != null) { DealDamage(m, dmg, cc, cm, label); return; }
            }
            Debug.Log($"[전투] {label} 빗나감");
        }

        void FireBow(WeaponData weapon)
        {
            if (inventory == null || !inventory.ConsumeItemByName("화살")) { Debug.Log("[전투] 화살이 없습니다"); return; }
            float range = weapon.attackRange > 5f ? weapon.attackRange : bowFallbackRange;
            Vector3 origin = cam.position + cam.forward * 0.6f;
            if (Physics.Raycast(origin, cam.forward, out var hit, range, ~0, QueryTriggerInteraction.Ignore))
            {
                var m = hit.collider.GetComponentInParent<MonsterAI>();
                if (m != null) { DealDamage(m, weapon.attackDamage, weapon.critChance, weapon.critMultiplier, weapon.displayName); return; }
            }
            Debug.Log($"[전투] {weapon.displayName} 화살 빗나감");
        }

        void DealDamage(MonsterAI m, float dmg, float critChance, float critMul, string label)
        {
            bool crit = Random.value < critChance;
            float raw = dmg * (crit ? critMul : 1f);
            float final = Mathf.Max(1f, raw - m.Defense);
            m.TakeDamage(final);
            Debug.Log($"[전투] {label} {final:F0} 피해{(crit ? " (치명타!)" : "")}");
        }
    }
}
