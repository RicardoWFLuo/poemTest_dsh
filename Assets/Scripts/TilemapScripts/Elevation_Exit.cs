using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 离开高台时恢复碰撞体与渲染层级。
/// </summary>
public class Elevation_Exit : MonoBehaviour
{
    public Collider2D[] mountainColliders;
    public Collider2D[] boundaryColliders;

    [Tooltip("离开高台后玩家的 SpriteRenderer 排序层级")]
    public int playerSortingOrder = 10;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
            return;

        SetCollidersEnabled(mountainColliders, true);
        SetCollidersEnabled(boundaryColliders, false);

        if (collision.gameObject.TryGetComponent(out SpriteRenderer sr))
            sr.sortingOrder = playerSortingOrder;
    }

    private static void SetCollidersEnabled(Collider2D[] colliders, bool enabled)
    {
        if (colliders == null)
            return;

        foreach (Collider2D collider in colliders)
        {
            if (collider != null)
                collider.enabled = enabled;
        }
    }
}
