using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ExpManager : MonoBehaviour
{
    public int level;
    public int currentExp;
    public int expToLevel = 10;
    public float expGrowthMultiplier = 1.2f; //每级需要上级的1.2倍经验
    public Slider expSlider;
    public TMP_Text currentLevelText;

    public static event Action<int> OnLevelUp;

    private void Start()
    {
        UpdateUI();
    }

    private void OnEnable()
    {
        EnemyHealth.OnMonsterDefeated += GainExperience;
        InventoryManager.OnExperienceGained += GainExperience;
    }
    private void OnDisable()
    {
        EnemyHealth.OnMonsterDefeated -= GainExperience;
        InventoryManager.OnExperienceGained -= GainExperience;
    }

    public void GainExperience(int amount)
    {
        if (amount <= 0)
            return;

        currentExp += amount;

        // 一次获得大量经验（例如打怪掉落的经验道具）时可能连升多级，
        // 历史版本只用 if，一次 Update 最多升一级，多余经验被吞掉。
        if (expToLevel <= 0)
        {
            expToLevel = 1;
        }

        while (currentExp >= expToLevel)
        {
            LevelUp();
        }

        UpdateUI();
    }

    private void LevelUp()
    {
        level++;
        currentExp -= expToLevel;
        expToLevel = Mathf.Max(1, Mathf.RoundToInt(expToLevel * expGrowthMultiplier));
        OnLevelUp?.Invoke(1);
    }

    public void UpdateUI()
    {
        if (expSlider != null)
        {
            expSlider.maxValue = expToLevel;
            expSlider.value = currentExp;
        }

        if (currentLevelText != null)
            currentLevelText.text = "Level:" + level;
    }
}
