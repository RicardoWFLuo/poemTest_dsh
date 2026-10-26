using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class SkillSlot : MonoBehaviour
{
    public List<SkillSlot> prerequisiteSkillSlots;
    public SkillSO skillSO;
    public Image skillIcon;

    public int currentLevel;
    public bool isUnclocked;
    public Button skillButton;
    public TMP_Text skillLevelText;

    public static event Action<SkillSlot> OnAbilityPointSpent;
    public static event Action<SkillSlot> OnSkillMaxed;

    private void OnValidate()
    {
        // 历史版本写成 skillLevelText.text != null，既没判空组件本身，
        // 又对 TMP_Text.text 做了永远为真的判断。
        if (skillSO != null && skillIcon != null && skillLevelText != null)
        {
            UpdateUI();
        }
    }

    public void TryUpgradeSkill()
    {
        if (skillSO == null || !isUnclocked || currentLevel >= skillSO.maxLevel)
            return;

        currentLevel++;
        OnAbilityPointSpent?.Invoke(this);

        if (currentLevel >= skillSO.maxLevel)
        {
            OnSkillMaxed?.Invoke(this);
        }

        UpdateUI();
    }

    public bool CanUnclockedSkill()
    {
        if (prerequisiteSkillSlots == null)
            return true;

        foreach (SkillSlot slot in prerequisiteSkillSlots)
        {
            if (slot == null || slot.skillSO == null)
                continue;

            if (!slot.isUnclocked || slot.currentLevel < slot.skillSO.maxLevel)
            {
                return false;
            }
        }

        return true;
    }

    public void UnLock()
    {
        isUnclocked = true;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (skillSO == null)
            return;

        if (skillIcon != null)
            skillIcon.sprite = skillSO.skillIcon;

        if (skillButton != null)
            skillButton.interactable = isUnclocked;

        if (skillLevelText != null)
            skillLevelText.text = isUnclocked ? currentLevel + "/" + skillSO.maxLevel : "锁定";

        if (skillIcon != null)
            skillIcon.color = isUnclocked ? Color.white : Color.gray;
    }
}
