using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class StatsUI : MonoBehaviour
{
    public GameObject[] statsSlots;

    public CanvasGroup statsCanvas;
    private bool statsOpen = false;

    private void Start()
    {
        UpdateAllStats();
    }

    private void OnDisable()
    {
        // 面板被外部强制隐藏时，确保不会把暂停状态一起带走
        if (statsOpen)
        {
            statsOpen = false;
            PauseController.ReleasePause();
        }
    }

    private void Update()
    {
        if (MobileInputManager.Instance == null || statsCanvas == null)
            return;

        if (!MobileInputManager.Instance.GetStatsDown())
            return;

        statsOpen = !statsOpen;

        if (statsOpen)
        {
            PauseController.RequestPause();
            statsCanvas.alpha = 1;
            statsCanvas.blocksRaycasts = true;
            statsCanvas.interactable = true;
        }
        else
        {
            PauseController.ReleasePause();
            statsCanvas.alpha = 0;
            statsCanvas.blocksRaycasts = false;
            statsCanvas.interactable = false;
        }

        UpdateAllStats();
    }

    /// <summary>写入第 index 个属性槽，越界时安全跳过。</summary>
    private void SetStatText(int index, string text)
    {
        if (statsSlots == null || index < 0 || index >= statsSlots.Length || statsSlots[index] == null)
            return;

        TMP_Text label = statsSlots[index].GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = text;
    }

    public void UpdateDamage()
    {
        SetStatText(0, "力量:" + StatsManager.Instance.damage);
    }

    public void UpdateSpeed()
    {
        SetStatText(1, "速度:" + StatsManager.Instance.speed);
    }
    public void UpdateMaxHealth()
    {
        SetStatText(2, "生命值:" + StatsManager.Instance.maxHealth);
    }
    public void UpdateAttackSpeed()
    {
        SetStatText(3, "攻速:" + 1);
    }
    public void UpdateBJ()
    {
        SetStatText(4, "暴击率:" + 1);
    }
    public void UpdateBS()
    {
        SetStatText(5, "暴击伤害:" + 1);
    }
    public void UpdateFY()
    {
        SetStatText(6, "防御:" + 1);
    }
    public void UpdateSBL()
    {
        SetStatText(7, "闪避率:" + 1);
    }
    public void UpdateNLZ()
    {
        SetStatText(8, "法力值:" + 1);
    }
    public void UpdateGDL()
    {
        SetStatText(9, "格挡率:" + 1);
    }




    public void UpdateAllStats()
    {
        if (StatsManager.Instance == null)
            return;

        UpdateDamage();
        UpdateSpeed();
        UpdateMaxHealth();
        UpdateAttackSpeed();
        UpdateBS();
        UpdateFY();
        UpdateSBL();
        UpdateNLZ();
        UpdateGDL();
        UpdateBJ();
    }
}
