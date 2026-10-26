using UnityEngine;

/// <summary>
/// 敌人的近战攻击组件。
/// 注意：类名必须与文件名 EnemyCombat.cs 完全一致，否则 Unity 无法把脚本绑定到
/// 场景中的组件（会显示为 Missing Script）。历史版本中该类名为 EnemyCombine，
/// 导致 RPG1 场景里 11 个敌人的攻击组件全部失效。
/// </summary>
public class EnemyCombat : MonoBehaviour
{
    public int damage = 1;
    public Transform attackPoint;
    public float weaponRange;
    public float knockBackForce;
    public float stunTime;
    public LayerMask playerLayer;

    public void Attack()
    {
        if (attackPoint == null)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, weaponRange, playerLayer);
        if (hits.Length == 0)
            return;

        // 只伤害第一个命中的玩家碰撞体，并做空引用保护
        if (hits[0].TryGetComponent(out PlayerHealth playerHealth))
        {
            playerHealth.ChangeHealth(-damage);
        }

        if (hits[0].TryGetComponent(out PlayerMovement playerMovement))
        {
            playerMovement.Knockback(transform, knockBackForce, stunTime);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, weaponRange);
    }
}
