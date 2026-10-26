using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour
{
    public ItemSO itemSO;
    public int quantity;
    public Image itemImage;
    public TMP_Text quantityText;

    private InventoryManager inventoryManager;
    private static ShopManager activeShop;

    public int slotIndex;

    private void Start()
    {
        inventoryManager = GetComponentInParent<InventoryManager>();

        // 兜底：slot 与 InventoryManager 不在同一层级时也能拿到实例
        if (inventoryManager == null)
            inventoryManager = InventoryManager.Instance;
    }

    private void OnEnable()
    {
        ShopKeeper.OnShopStateChanged += HandleShopStateChanged;
    }
    private void OnDisable()
    {
        ShopKeeper.OnShopStateChanged -= HandleShopStateChanged;
    }

    private void HandleShopStateChanged(ShopManager shopManager, bool isOpen)
    {
        activeShop = isOpen ? shopManager : null;
    }



    public void Update()
    {
        if (MobileInputManager.Instance == null)
            return;

        if (activeShop != null)
        {
            if (slotIndex == 1 && MobileInputManager.Instance.GetItem1Down()) SellSometing();
            if (slotIndex == 2 && MobileInputManager.Instance.GetItem2Down()) SellSometing();
            if (slotIndex == 3 && MobileInputManager.Instance.GetItem3Down()) SellSometing();
        }
        else
        {
            if (slotIndex == 1 && MobileInputManager.Instance.GetItem1Down()) UseItemByKey();
            if (slotIndex == 2 && MobileInputManager.Instance.GetItem2Down()) UseItemByKey();
            if (slotIndex == 3 && MobileInputManager.Instance.GetItem3Down()) UseItemByKey();

            if (slotIndex == 1 && MobileInputManager.Instance.GetItem4Down()) DropItemByKey();
            if (slotIndex == 2 && MobileInputManager.Instance.GetItem5Down()) DropItemByKey();
            if (slotIndex == 3 && MobileInputManager.Instance.GetItem6Down()) DropItemByKey();
        }
    }

    public void UseItemByKey()
    {
        if (quantity <= 0 || itemSO == null || inventoryManager == null)
            return;

        // 部分物品不消耗（例如任务道具），由 ItemSO.isConsumable 控制
        if (!itemSO.isConsumable)
        {
            Debug.Log($"{itemSO.itemName} 不是消耗品，无法使用。");
            return;
        }

        if (itemSO.currentHealth > 0 && StatsManager.Instance != null &&
            StatsManager.Instance.currentHealth >= StatsManager.Instance.maxHealth)
        {
            Debug.Log("血量已满，使用物品会造成浪费");
            return;
        }

        inventoryManager.UseItem(this);
    }

    public void DropItemByKey()
    {
        if (quantity <= 0 || itemSO == null || inventoryManager == null)
            return;

        inventoryManager.DropItem(this);
    }



    public void SellSometing()
    {
        if (activeShop == null || itemSO == null || quantity <= 0)
            return;

        activeShop.SellItem(itemSO);
        quantity--;

        if (quantity <= 0)
        {
            quantity = 0;
            itemSO = null;
        }

        UpdateUI();
    }

    public void UpdateUI()
    {
        if (quantity <= 0)
        {
            quantity = 0;
            itemSO = null;
        }

        if (itemSO != null)
        {
            if (itemImage != null)
            {
                itemImage.sprite = itemSO.icon;
                itemImage.gameObject.SetActive(true);
            }

            if (quantityText != null)
                quantityText.text = quantity.ToString();
        }
        else
        {
            if (itemImage != null)
                itemImage.gameObject.SetActive(false);

            if (quantityText != null)
                quantityText.text = "";
        }
    }
}
