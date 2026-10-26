using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ShopInfo : MonoBehaviour
{
    public CanvasGroup infoPannel;
    public TMP_Text itemNameText;
    public TMP_Text itemDescriptionText;

    [Header("Stat Fields")]
    public TMP_Text[] statsTexts;

    private RectTransform infoPanelRect;

    private void Awake()
    {
        infoPanelRect = GetComponent<RectTransform>();

        if (infoPannel != null)
            infoPannel.alpha = 0;
    }

    public void ShowItemInfo(ItemSO itemSO)
    {
        if (itemSO == null)
            return;

        if (infoPannel != null)
            infoPannel.alpha = 1;

        if (itemNameText != null)
            itemNameText.text = itemSO.itemName;
        if (itemDescriptionText != null)
            itemDescriptionText.text = itemSO.itemDescription;

        List<string> stats = new List<string>();
        if (itemSO.currentHealth > 0)
            stats.Add("生命值:" + itemSO.currentHealth);
        if (itemSO.maxHealth > 0)
            stats.Add("生命上限:" + itemSO.maxHealth);
        if (itemSO.damage > 0)
            stats.Add("伤害:" + itemSO.damage);
        if (itemSO.speed > 0)
            stats.Add("速度:" + itemSO.speed);
        if (itemSO.duration > 0)
            stats.Add("持续时间:" + itemSO.duration);

        // 关键修复：无论有没有属性，都要先把所有属性行清空，
        // 否则从"有属性"的物品移到"无属性"的物品时，旧属性会残留在面板上。
        if (statsTexts != null)
        {
            for (int i = 0; i < statsTexts.Length; i++)
            {
                if (statsTexts[i] == null)
                    continue;

                bool hasStat = i < stats.Count;
                statsTexts[i].gameObject.SetActive(hasStat);

                if (hasStat)
                    statsTexts[i].text = stats[i];
            }
        }

        FollowMouse();
    }

    public void HideItemInfo()
    {
        if (infoPannel != null)
            infoPannel.alpha = 0;

        if (itemNameText != null)
            itemNameText.text = "";
        if (itemDescriptionText != null)
            itemDescriptionText.text = "";

        if (statsTexts == null)
            return;

        foreach (var statsText in statsTexts)
        {
            if (statsText != null)
                statsText.gameObject.SetActive(false);
        }
    }

    public void FollowMouse()
    {
        if (infoPanelRect == null)
            return;

        // UI 的 RectTransform 位置是世界坐标，鼠标是屏幕坐标，
        // 需要按 Canvas 缩放换算，否则在非 1:1 缩放下提示框会跑偏。
        RectTransform parent = infoPanelRect.parent as RectTransform;
        Canvas canvas = GetComponentInParent<Canvas>();

        Vector2 localPoint;
        if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, Input.mousePosition, canvas != null ? canvas.worldCamera : null, out localPoint))
        {
            infoPanelRect.anchoredPosition = localPoint + new Vector2(10, -10);
        }
    }
}
