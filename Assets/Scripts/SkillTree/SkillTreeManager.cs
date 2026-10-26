using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SkillTreeManager : MonoBehaviour
{
    public SkillSlot[] skillSlots;
    public TMP_Text pointsText;
    public int availablePoints;

    private void OnEnable()
    {
        SkillSlot.OnAbilityPointSpent += HandleAbilityPointsSpent;
        SkillSlot.OnSkillMaxed += HandleSkillMaxed;
        ExpManager.OnLevelUp += UpdateAbilityPoints;
    }

    private void OnDisable()
    {
        SkillSlot.OnAbilityPointSpent -= HandleAbilityPointsSpent;
        SkillSlot.OnSkillMaxed -= HandleSkillMaxed;
        ExpManager.OnLevelUp -= UpdateAbilityPoints;
    }

    private void Start()
    {
        foreach (SkillSlot slot in skillSlots)
        {
            if (slot == null || slot.skillButton == null)
                continue;

            SkillSlot captured = slot;
            slot.skillButton.onClick.AddListener(() => CheckAvailablePoints(captured));
        }

        // 关键修复：没有任何前置技能的基础节点必须在开局就解锁。
        // 历史版本只在"某个技能升满"时才会调用 UnLock()，
        // 于是 prerequisites 为空、isUnclocked 又为 false 的根节点永远点不动。
        UnlockEligibleSlots();

        UpdateAbilityPoints(0);
    }

    private void CheckAvailablePoints(SkillSlot slot)
    {
        if (slot == null)
            return;

        if (availablePoints > 0)
        {
            slot.TryUpgradeSkill();
        }
    }

    private void HandleAbilityPointsSpent(SkillSlot skillSlot)
    {
        UpdateAbilityPoints(-1);
    }

    private void HandleSkillMaxed(SkillSlot skillSlot)
    {
        UnlockEligibleSlots();
    }

    /// <summary>把所有"前置已满足"的锁定节点解锁（可重复调用）。</summary>
    private void UnlockEligibleSlots()
    {
        if (skillSlots == null)
            return;

        bool unlockedAny;
        do
        {
            unlockedAny = false;

            foreach (SkillSlot slot in skillSlots)
            {
                if (slot == null || slot.isUnclocked)
                    continue;

                if (slot.CanUnclockedSkill())
                {
                    slot.UnLock();
                    unlockedAny = true;
                }
            }
        }
        while (unlockedAny);
    }

    public void UpdateAbilityPoints(int amount)
    {
        availablePoints = Mathf.Max(0, availablePoints + amount);

        if (pointsText != null)
            pointsText.text = "技能点:" + availablePoints;
    }
}
