using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    public float speed;
    public float attackRange = 2;
    public float attackCoolDown = 2;
    public float playerDetectRange = 5;
    public Transform detectionPoint;
    public LayerMask playerLayer;

    private float attackCoolDownTimer;
    private int facingDirection = -1;
    private EnemyState enemyState;

    private Rigidbody2D enemyRB;
    private Transform player;
    private Animator anim;

    void Start()
    {
        enemyRB = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        ChangeState(EnemyState.Idle);
    }

    void Update()
    {
        if (enemyState == EnemyState.KnockBack)
            return;

        CheckForPlayer();

        if (attackCoolDownTimer > 0)
        {
            attackCoolDownTimer -= Time.deltaTime;
        }

        if (enemyState == EnemyState.Chasing)
        {
            Chase();
        }
        else if (enemyState == EnemyState.Attacking)
        {
            enemyRB.velocity = Vector2.zero;
        }
    }

    void Chase()
    {
        if (player == null)
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        if (player.position.x > transform.position.x && facingDirection == -1 ||
            player.position.x < transform.position.x && facingDirection == 1)
        {
            Flip();
        }
        Vector2 direction = (player.position - transform.position).normalized;
        enemyRB.velocity = direction * speed;
    }

    void Flip()
    {
        facingDirection *= -1;
        transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
    }

    private void CheckForPlayer()
    {
        if (detectionPoint == null)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(detectionPoint.position, playerDetectRange, playerLayer);
        if (hits.Length == 0 || hits[0] == null)
        {
            player = null;
            enemyRB.velocity = Vector2.zero;
            ChangeState(EnemyState.Idle);
            return;
        }

        player = hits[0].transform;
        float distance = Vector2.Distance(transform.position, player.position);

        if (distance <= attackRange)
        {
            // 冷却结束后重新进入攻击状态，ChangeState 会重播一次攻击动画
            if (attackCoolDownTimer <= 0)
            {
                attackCoolDownTimer = attackCoolDown;
                ChangeState(EnemyState.Attacking);
            }
        }
        else
        {
            // 关键修复：历史版本这里是 "dist > attackRange && enemyState != Attacking"，
            // 导致敌人在一次攻击后永远停留在 Attacking 状态，玩家跑开也不再追击。
            ChangeState(EnemyState.Chasing);
        }
    }

    public void ChangeState(EnemyState newState)
    {
        // 注意：这里故意不写 "if (enemyState == newState) return;"。
        // 重复调用 ChangeState(Attacking) 需要把攻击动画重播一次（false -> true）。

        //退出当前动画
        if (enemyState == EnemyState.Idle)
        {
            anim.SetBool("isIdle", false);
        }
        else if (enemyState == EnemyState.Chasing)
        {
            anim.SetBool("isChasing", false);
        }
        else if (enemyState == EnemyState.Attacking)
        {
            anim.SetBool("isAttacking", false);
        }

        //更新当前状态
        enemyState = newState;

        //更新新动画
        if (enemyState == EnemyState.Idle)
        {
            anim.SetBool("isIdle", true);
        }
        else if (enemyState == EnemyState.Chasing)
        {
            anim.SetBool("isChasing", true);
        }
        else if (enemyState == EnemyState.Attacking)
        {
            anim.SetBool("isAttacking", true);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (detectionPoint == null)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(detectionPoint.position, playerDetectRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}


public enum EnemyState
{
    Idle,
    Chasing,
    Attacking,
    KnockBack,
}
