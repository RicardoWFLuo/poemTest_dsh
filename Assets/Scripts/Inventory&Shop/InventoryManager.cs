using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    public InventorySlot[] itemSlots;
    public int gold;
    public TMP_Text goldText;
    public UseItem useItem;
    public GameObject lootPrefab;
    public Transform player;

    public static event Action<int> OnExperienceGained;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        UpdateGoldUI();

        if (itemSlots == null)
            return;

        foreach (var slot in itemSlots)
        {
            if (slot != null)
                slot.UpdateUI();
        }
    }

    private void OnEnable()
    {
        Loot.OnItemLooted += AddItem;
    }
    private void OnDisable()
    {
        Loot.OnItemLooted -= AddItem;
    }

    public void UpdateGoldUI()
    {
        if (goldText != null)
            goldText.text = gold.ToString();
    }

    public void AddItem(ItemSO itemSO, int quantity)
    {
        if (itemSO == null || quantity <= 0)
            return;

        if (itemSO.isGold)
        {
            gold += quantity;
            UpdateGoldUI();
            return;
        }

        if (itemSO.isEXP)
        {
            OnExperienceGained?.Invoke(quantity);
            return;
        }

        if (itemSlots == null)
            return;

        int stackSize = Mathf.Max(1, itemSO.stackSize);
        int remaining = quantity;

        // 1) 先叠加到已有的同类物品堆上
        foreach (var slot in itemSlots)
        {
            if (remaining <= 0)
                break;

            if (slot == null || slot.itemSO != itemSO)
                continue;

            int space = stackSize - slot.quantity;
            if (space <= 0)
                continue;

            int amountToAdd = Mathf.Min(space, remaining);
            slot.quantity += amountToAdd;
            remaining -= amountToAdd;
            slot.UpdateUI();
        }

        // 2) 再放进空格子。
        //    历史版本这里是 "slot.quantity = quantity; return;"，
        //    只填一个格子就返回，超过一叠数量的物品会被静默吞掉。
        foreach (var slot in itemSlots)
        {
            if (remaining <= 0)
                break;

            if (slot == null || slot.itemSO != null)
                continue;

            int amountToAdd = Mathf.Min(stackSize, remaining);
            slot.itemSO = itemSO;
            slot.quantity = amountToAdd;
            remaining -= amountToAdd;
            slot.UpdateUI();
        }

        // 3) 背包真的放不下了才掉在地上
        if (remaining > 0)
        {
            DropLoot(itemSO, remaining);
        }
    }

    public void DropItem(InventorySlot slot)
    {
        if (slot == null || slot.itemSO == null || slot.quantity <= 0)
            return;

        DropLoot(slot.itemSO, 1);
        slot.quantity--;

        if (slot.quantity <= 0)
        {
            slot.quantity = 0;
            slot.itemSO = null;
        }

        slot.UpdateUI();
    }

    private void DropLoot(ItemSO itemSO, int quantity)
    {
        if (itemSO == null || quantity <= 0)
            return;

        if (lootPrefab == null || player == null)
        {
            Debug.LogWarning($"[InventoryManager] 背包已满且无法生成掉落物，{itemSO.itemName} x{quantity} 丢失。请检查 lootPrefab / player 引用。");
            return;
        }

        GameObject spawned = Instantiate(lootPrefab, player.position, Quaternion.identity);
        if (spawned.TryGetComponent(out Loot loot))
        {
            loot.Initialize(itemSO, quantity);
        }
    }



    public void UseItem(InventorySlot slot)
    {
        if (slot == null || slot.itemSO == null || slot.quantity <= 0)
            return;

        if (useItem == null)
        {
            Debug.LogWarning("[InventoryManager] 未指定 UseItem 组件，无法使用物品。");
            return;
        }

        useItem.ApplyItemEffects(slot.itemSO);

        slot.quantity--;
        if (slot.quantity <= 0)
        {
            slot.quantity = 0;
            slot.itemSO = null;
        }
        slot.UpdateUI();
    }

    public bool HasItem(ItemSO itemSO)
    {
        if (itemSO == null || itemSlots == null)
            return false;

        foreach (var slot in itemSlots)
        {
            if (slot != null && slot.itemSO == itemSO && slot.quantity > 0)
            {
                return true;
            }
        }
        return false;
    }

    public void RemoveItem(ItemSO itemSO, int quantity)
    {
        if (itemSO == null || quantity <= 0 || itemSlots == null)
            return;

        int remaining = quantity;

        for (int i = 0; i < itemSlots.Length && remaining > 0; i++)
        {
            var slot = itemSlots[i];
            if (slot == null || slot.itemSO != itemSO)
                continue;

            int taken = Mathf.Min(slot.quantity, remaining);
            slot.quantity -= taken;
            remaining -= taken;

            if (slot.quantity <= 0)
            {
                slot.quantity = 0;
                slot.itemSO = null;
            }

            slot.UpdateUI();
        }
    }

    public int GetItemQuantity(ItemSO itemSO)
    {
        if (itemSO == null || itemSlots == null)
            return 0;

        int total = 0;

        foreach (var slot in itemSlots)
        {
            if (slot != null && slot.itemSO == itemSO)
            {
                total += slot.quantity;
            }
        }
        return total;
    }
}
