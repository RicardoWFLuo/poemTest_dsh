using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Arrow : MonoBehaviour
{
    public Rigidbody2D rb;
    public Vector2 direction = Vector2.right;
    public float lifeSpawn = 2;
    public float speed;

    public LayerMask enenmyLayer;
    public LayerMask obstacleLayer;

    public SpriteRenderer sr;
    public Sprite buriedSprite;

    public int damage;
    public float knockBackForce;
    public float knockBackTime;
    public float stunTime;

    void Start()
    {
        if (rb != null)
        {
            rb.velocity = direction * speed;
        }
        RotateArrow();
        Destroy(gameObject, lifeSpawn);
    }

    private void RotateArrow()
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(new Vector3(0, 0, angle));
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null || collision.gameObject == null || rb == null)
            return;

        // 已经钉在目标上的箭不再结算
        if (rb.isKinematic)
            return;

        int layer = collision.gameObject.layer;

        if ((enenmyLayer.value & (1 << layer)) > 0)
        {
            // 目标身上不一定挂着 EnemyHealth / EnemyKnockBack，必须判空
            if (collision.gameObject.TryGetComponent(out EnemyHealth health))
            {
                health.ChangeHealth(-damage);
            }

            if (collision.gameObject.TryGetComponent(out EnemyKnockBack knockBack))
            {
                knockBack.KnockBack(transform, knockBackForce, knockBackTime, stunTime);
            }

            AttachToTarget(collision.gameObject.transform);
        }
        else if ((obstacleLayer.value & (1 << layer)) > 0)
        {
            AttachToTarget(collision.gameObject.transform);
        }
    }

    private void AttachToTarget(Transform target)
    {
        if (sr != null && buriedSprite != null)
            sr.sprite = buriedSprite;

        rb.velocity = Vector2.zero;
        rb.isKinematic = true;

        if (target != null)
            transform.SetParent(target);
    }
}
