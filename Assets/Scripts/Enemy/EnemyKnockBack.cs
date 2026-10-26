using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人受击硬直。
/// 参数语义：knockBackTime = 被击退滑行的时长；stunTime = 从受击开始到恢复行动的时长。
/// 注意调用方传参顺序必须是 (transform, force, knockBackTime, stunTime)。
/// 历史版本把两个参数写反了，导致敌人被击退后要多僵直 0.2 秒。
/// </summary>
public class EnemyKnockBack : MonoBehaviour
{
    private Rigidbody2D rb;
    private EnemyMovement enemyMovement;
    private Coroutine stunRoutine;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        enemyMovement = GetComponent<EnemyMovement>();
    }

    public void KnockBack(Transform forceTransform, float knockBackForce, float knockBackTime, float stunTime)
    {
        if (rb == null || enemyMovement == null)
            return;

        if (stunRoutine != null)
        {
            StopCoroutine(stunRoutine);
        }

        enemyMovement.ChangeState(EnemyState.KnockBack);

        Vector2 direction = ((Vector2)transform.position - (Vector2)forceTransform.position).normalized;
        if (direction == Vector2.zero)
            direction = Vector2.right;

        rb.velocity = direction * knockBackForce;

        stunRoutine = StartCoroutine(StunTimer(knockBackTime, stunTime));
    }

    IEnumerator StunTimer(float knockBackTime, float stunTime)
    {
        yield return new WaitForSeconds(knockBackTime);

        if (rb != null)
            rb.velocity = Vector2.zero;

        float remaining = stunTime - knockBackTime;
        if (remaining > 0)
            yield return new WaitForSeconds(remaining);

        stunRoutine = null;

        if (enemyMovement != null)
            enemyMovement.ChangeState(EnemyState.Idle);
    }
}
