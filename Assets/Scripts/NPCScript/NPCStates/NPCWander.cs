using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC 自由游走行为。注意：这是运行时代码，绝对不能引用 UnityEditor 命名空间，
/// 否则打包（Build）会直接失败。历史版本在文件顶部写了 using UnityEditor;。
/// </summary>
public class NPCWander : MonoBehaviour
{
    [Header("Wander Area")]
    public float wanderWidth = 5;
    public float wanderHeight = 5;
    public Vector2 startingPosition;

    public float pauseDuration = 1;
    public float speed = 2;
    public Vector2 target;

    private Rigidbody2D rb;
    private Animator anim;
    private bool isPaused;
    private Coroutine pauseRoutine;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        startingPosition = transform.position;
        target = startingPosition;
        StartPauseRoutine();
    }

    private void OnDisable()
    {
        StopPauseRoutine();

        isPaused = false;
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }
    }

    private void Update()
    {
        if (rb == null)
            return;

        if (isPaused)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        if (Vector2.Distance(transform.position, target) < .1f)
        {
            StartPauseRoutine();
            return;
        }

        Move();
    }

    private void Move()
    {
        Vector2 direction = (target - (Vector2)transform.position).normalized;
        if (direction.x > 0 && transform.localScale.x < 0 ||
            direction.x < 0 && transform.localScale.x > 0)
        {
            transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
        }
        rb.velocity = direction * speed;
    }

    /// <summary>
    /// 统一入口：保证同一时刻只有一个暂停协程在跑，避免碰撞时反复叠加协程。
    /// </summary>
    private void StartPauseRoutine()
    {
        if (!isActiveAndEnabled)
            return;

        if (pauseRoutine != null)
        {
            StopCoroutine(pauseRoutine);
        }

        pauseRoutine = StartCoroutine(PauseAndPickNewDestination());
    }

    private void StopPauseRoutine()
    {
        if (pauseRoutine != null)
        {
            StopCoroutine(pauseRoutine);
            pauseRoutine = null;
        }
    }

    IEnumerator PauseAndPickNewDestination()
    {
        isPaused = true;
        if (anim != null)
        {
            anim.Play("PawnIdle");
        }
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }

        yield return new WaitForSeconds(pauseDuration);

        target = GetRandomTarget();
        isPaused = false;

        if (anim != null)
        {
            anim.Play("PawnWalk");
        }

        pauseRoutine = null;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        StartPauseRoutine();
    }

    private Vector2 GetRandomTarget()
    {
        float halfWidth = wanderWidth / 2;
        float halfHeight = wanderHeight / 2;
        int edge = Random.Range(0, 4);

        return edge switch
        {
            0 => new Vector2(startingPosition.x - halfWidth, Random.Range(startingPosition.y - halfHeight,
            startingPosition.y + halfHeight)),

            1 => new Vector2(startingPosition.x + halfWidth, Random.Range(startingPosition.y - halfHeight,
            startingPosition.y + halfHeight)),

            2 => new Vector2(Random.Range(startingPosition.x - halfWidth, startingPosition.x + halfWidth),
            startingPosition.y - halfHeight),

            _ => new Vector2(Random.Range(startingPosition.x - halfWidth, startingPosition.x + halfWidth),
            startingPosition.y + halfHeight),
        };
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(startingPosition, new Vector3(wanderWidth, wanderHeight, 0));
    }
}
