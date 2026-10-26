using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 走到高台边缘时切换碰撞体，让玩家可以走到"山的上层"，并把渲染层级提高。
/// </summary>
public class Elevation_Entry : MonoBehaviour
{
    public Collider2D[] mountainColliders;
    public Collider2D[] boundaryColliders;

    [Tooltip("进入高台后玩家的 SpriteRenderer 排序层级")]
    public int playerSortingOrder = 15;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
            return;

        SetCollidersEnabled(mountainColliders, false);
        SetCollidersEnabled(boundaryColliders, true);

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
