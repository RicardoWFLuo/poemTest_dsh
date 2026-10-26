using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{

    [SerializeField] private ShopSlot[] shopSlots;
    [SerializeField] private InventoryManager inventoryManager;


    private void Start()
    {
        //Debug
        if (inventoryManager == null)
            inventoryManager = FindObjectOfType<InventoryManager>();

        if (shopSlots == null || shopSlots.Length == 0)
            shopSlots = GetComponentsInChildren<ShopSlot>(true);
    }

    public void PopulateShopItems(List<ShopItems> shopItems)
    {
        if (inventoryManager == null)
            inventoryManager = InventoryManager.Instance;

        // 历史版本无条件覆盖 Inspector 里配置的数组；
        // 如果 ShopSlot 不在 ShopManager 的子节点下就会全部丢失。
        if (shopSlots == null || shopSlots.Length == 0)
            shopSlots = GetComponentsInChildren<ShopSlot>(true);

        if (shopSlots == null || shopItems == null)
            return;

        int i = 0;
        for (; i < shopItems.Count && i < shopSlots.Length; i++)
        {
            ShopItems shopItem = shopItems[i];
            if (shopItem == null || shopItem.itemSO == null)
            {
                shopSlots[i].gameObject.SetActive(false);
                continue;
            }

            shopSlots[i].Initialize(shopItem.itemSO, shopItem.price);
            shopSlots[i].gameObject.SetActive(true);
        }

        for (; i < shopSlots.Length; i++)
        {
            shopSlots[i].gameObject.SetActive(false);
        }
    }

    public void TryBuyItem(ItemSO itemSO, int price)
    {
        if (itemSO == null || inventoryManager == null)
            return;

        if (inventoryManager.gold < price)
        {
            Debug.Log($"[Shop] 金币不足，需要 {price}，当前 {inventoryManager.gold}。");
            return;
        }

        if (!HasSpaceForItem(itemSO))
        {
            Debug.Log("[Shop] 背包已满，无法购买。");
            return;
        }

        inventoryManager.gold -= price;
        inventoryManager.UpdateGoldUI();
        inventoryManager.AddItem(itemSO, 1);
    }

    private bool HasSpaceForItem(ItemSO itemSO)
    {
        if (inventoryManager.itemSlots == null)
            return false;

        foreach (var slot in inventoryManager.itemSlots)
        {
            if (slot == null)
                continue;

            if (slot.itemSO == itemSO && slot.quantity < Mathf.Max(1, itemSO.stackSize))
            {
                return true;
            }
            else if (slot.itemSO == null)
            {
                return true;
            }
        }
        return false;
    }

    public void SellItem(ItemSO itemSO)
    {
        if (itemSO == null || inventoryManager == null)
            return;

        if (shopSlots == null)
            return;

        foreach (var slot in shopSlots)
        {
            if (slot == null || slot.itemSO != itemSO)
                continue;

            // 回收价 = 售价 - 1（最低 0），避免出现负收益
            int sellPrice = Mathf.Max(0, slot.price - 1);
            inventoryManager.gold += sellPrice;
            inventoryManager.UpdateGoldUI();
            return;
        }

        Debug.LogWarning($"[Shop] 商店里没有 {itemSO.itemName} 的价格记录，交易已取消。");
    }
}

[System.Serializable]
public class ShopItems
{
    public ItemSO itemSO;
    public int price;
}
