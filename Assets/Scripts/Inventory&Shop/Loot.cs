using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Loot : MonoBehaviour
{
    public ItemSO itemSO;
    public SpriteRenderer sr;
    public Animator anim;

    public bool canBePickedUp = true;
    public int quantity;

    public static event Action<ItemSO, int> OnItemLooted;

    private bool pickedUp;

    private void OnValidate()
    {
        // 编辑期只刷新显示，不再改对象名字（改名会污染场景/预制体）
        if (itemSO == null || sr == null)
            return;

        sr.sprite = itemSO.icon;
    }

    public void Initialize(ItemSO itemSO, int quantity)
    {
        this.itemSO = itemSO;
        this.quantity = quantity;
        canBePickedUp = false;
        pickedUp = false;

        UpdateAppearance();
    }

    public void UpdateAppearance()
    {
        if (itemSO == null)
            return;

        if (sr != null)
            sr.sprite = itemSO.icon;

        gameObject.name = itemSO.itemName;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (pickedUp || !canBePickedUp)
            return;

        if (collision == null || !collision.CompareTag("Player"))
            return;

        pickedUp = true;
        canBePickedUp = false;

        if (anim != null)
            anim.Play("LootPickUp");

        // 没有任何订阅者（例如场景里没有 InventoryManager）时，
        // 历史上会直接抛 NullReferenceException。
        if (OnItemLooted == null)
        {
            Debug.LogWarning($"[Loot] 没有 InventoryManager 在监听掉落事件，{itemSO?.itemName} 将丢失。");
        }

        OnItemLooted?.Invoke(itemSO, quantity);
        Destroy(gameObject, .5f);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision == null || !collision.CompareTag("Player"))
            return;

        if (!pickedUp)
            canBePickedUp = true;
    }
}
