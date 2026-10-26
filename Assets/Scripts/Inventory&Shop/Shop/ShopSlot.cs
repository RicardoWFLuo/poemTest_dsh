using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    public ItemSO itemSO;
    public TMP_Text itemNameText;
    public TMP_Text priceText;
    public Image itemImage;

    [SerializeField] private ShopManager shopManager;
    [SerializeField] private ShopInfo shopInfo;

    public int price;

    public void Initialize(ItemSO newItemSO, int price)
    {
        if (newItemSO == null)
            return;

        itemSO = newItemSO;

        if (itemImage != null)
            itemImage.sprite = newItemSO.icon;
        if (itemNameText != null)
            itemNameText.text = newItemSO.itemName;

        this.price = price;

        if (priceText != null)
            priceText.text = price.ToString();
    }

    public void OnBuyButtonClicked()
    {
        if (shopManager == null)
        {
            Debug.LogWarning("[ShopSlot] 未指定 ShopManager，无法购买。");
            return;
        }

        shopManager.TryBuyItem(itemSO, price);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (itemSO != null && shopInfo != null)
        {
            shopInfo.ShowItemInfo(itemSO);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (shopInfo != null)
            shopInfo.HideItemInfo();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (itemSO != null && shopInfo != null)
        {
            shopInfo.FollowMouse();
        }
    }
}
