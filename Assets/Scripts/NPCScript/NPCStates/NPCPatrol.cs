using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCPatrol : MonoBehaviour
{
    public Vector2[] patrolPoints;
    public float speed = 2;
    public float pauseDuration = 1.5f;

    private bool isPaused;
    private Vector2 target;
    private int currentPatrolIndex;

    private Rigidbody2D rb;
    private Animator anim;
    private Coroutine patrolRoutine;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Debug.LogWarning($"[NPCPatrol] {name} 没有配置巡逻点，已停止巡逻。");
            enabled = false;
            return;
        }

        // 从第一个巡逻点开始，而不是先停留再走到"第二个"点
        target = patrolPoints[0];
        patrolRoutine = StartCoroutine(SetPatrolPoint());
    }

    void Update()
    {
        if (rb == null || isPaused)
        {
            if (rb != null)
                rb.velocity = Vector2.zero;
            return;
        }

        Vector2 direction = (target - new Vector2(transform.position.x, transform.position.y)).normalized;

        if (direction.x < 0 && transform.localScale.x > 0
            || direction.x > 0 && transform.localScale.x < 0)
        {
            transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
        }

        rb.velocity = direction * speed;

        if (Vector2.Distance(transform.position, target) < .1f)
        {
            if (patrolRoutine == null)
                patrolRoutine = StartCoroutine(SetPatrolPoint());
        }
    }

    IEnumerator SetPatrolPoint()
    {
        isPaused = true;
        if (rb != null)
            rb.velocity = Vector2.zero;
        if (anim != null)
            anim.Play("PawnIdle");

        yield return new WaitForSeconds(pauseDuration);

        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        target = patrolPoints[currentPatrolIndex];
        isPaused = false;

        if (anim != null)
            anim.Play("PawnWalk");

        patrolRoutine = null;
    }
}
