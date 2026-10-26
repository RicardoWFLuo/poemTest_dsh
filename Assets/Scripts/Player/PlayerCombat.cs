using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public Transform attackPoint;
    public StatsUI statsUI;
    public LayerMask enemyLayer;


    public Animator anim;
    public float coolDown = 1.5f;
    private float timer;

    private void Update()
    {
        if (timer > 0)
        {
            timer -= Time.deltaTime;
        }
    }

    public void Attack()
    {
        if (timer > 0 || anim == null)
            return;

        anim.SetBool("isAttacking", true);
        timer = coolDown;
    }

    /// <summary>由攻击动画的事件调用，结算命中。</summary>
    public void DealDamage()
    {
        if (attackPoint == null || StatsManager.Instance == null)
            return;

        Collider2D[] enemies = Physics2D.OverlapCircleAll(
            attackPoint.position, StatsManager.Instance.weaponRange, enemyLayer);

        if (enemies.Length == 0)
            return;

        // OverlapCircleAll 的返回顺序是不确定的，这里显式取最近的一个，
        // 避免出现"明明打在眼前怪身上却伤到远处怪"的情况。
        Collider2D target = null;
        float nearestSqr = float.MaxValue;
        foreach (var candidate in enemies)
        {
            if (candidate == null)
                continue;

            float sqr = ((Vector2)candidate.transform.position - (Vector2)attackPoint.position).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                target = candidate;
            }
        }

        if (target == null)
            return;

        if (target.TryGetComponent(out EnemyHealth health))
        {
            health.ChangeHealth(-StatsManager.Instance.damage);
        }

        if (target.TryGetComponent(out EnemyKnockBack knockBack))
        {
            knockBack.KnockBack(transform,
                StatsManager.Instance.knockBackForce,
                StatsManager.Instance.knockBackTime,
                StatsManager.Instance.stunTime);
        }
    }

    public void FinishAttacking()
    {
        if (anim != null)
            anim.SetBool("isAttacking", false);
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint != null && StatsManager.Instance != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, StatsManager.Instance.weaponRange);
        }
    }
}
