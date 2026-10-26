using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillTreeManager : MonoBehaviour
{
    public SkillSlot[] skillSlots;
    public TMP_Text pointsText;
    public int availablePoints;

    [Header("调试")]
    [Tooltip("在 Console 打印每次点击、解锁级联与整棵树的初始状态。排查技能树问题时打开。")]
    public bool logSkillTree = false;

    private readonly HashSet<SkillSlot> boundSlots = new HashSet<SkillSlot>();
    private readonly HashSet<Button> boundButtons = new HashSet<Button>();
    private int lastClickFrame = -1;

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
        boundSlots.Clear();
        boundButtons.Clear();

        for (int i = 0; i < skillSlots.Length; i++)
        {
            SkillSlot slot = skillSlots[i];

            if (slot == null || slot.skillButton == null)
                continue;

            // 同一个槽位、或同一个按钮，只允许绑定一次。
            // 否则一个按钮被多个槽位共用时，点一次会升级好几个节点 ——
            // 表现就是「点一个、一排都亮」。
            if (!boundSlots.Add(slot) || !boundButtons.Add(slot.skillButton))
            {
                if (logSkillTree)
                    Debug.LogWarning($"[技能树] 槽位 #{i}（{slot.name}）的按钮与其它槽位重复，已跳过绑定。");
                continue;
            }

            SkillSlot captured = slot;
            int capturedIndex = i;
            slot.skillButton.onClick.AddListener(() => HandleSlotClicked(captured, capturedIndex));
        }

        // 关键修复：没有任何前置技能的基础节点必须在开局就解锁。
        // 历史版本只在"某个技能升满"时才会调用 UnLock()，
        // 于是 prerequisites 为空、isUnclocked 又为 false 的根节点永远点不动。
        UnlockEligibleSlots();

        UpdateAbilityPoints(0);

        if (logSkillTree)
            LogTreeSnapshot();
    }

    private void HandleSlotClicked(SkillSlot slot, int index)
    {
        if (slot == null)
            return;

        if (logSkillTree)
        {
            string skill = slot.skillSO != null ? slot.skillSO.skillName : "（无技能）";
            int max = slot.skillSO != null ? slot.skillSO.maxLevel : 0;
            Debug.Log($"[技能树] 点击 #{index} {slot.name} | 技能={skill} | 等级={slot.currentLevel}/{max} | " +
                      $"已解锁={slot.isUnclocked} | 剩余点={availablePoints}");
        }

        CheckAvailablePoints(slot);
    }

    /// <summary>把整棵树的初始状态打到 Console，用来确认前置条件到底有没有生效。</summary>
    private void LogTreeSnapshot()
    {
        if (skillSlots == null)
            return;

        int unlocked = 0;
        int withPrereq = 0;
        var sb = new System.Text.StringBuilder("[技能树] 初始状态：");

        for (int i = 0; i < skillSlots.Length; i++)
        {
            SkillSlot slot = skillSlots[i];

            if (slot == null)
            {
                sb.Append($"\n  #{i} <空引用>");
                continue;
            }

            if (slot.isUnclocked)
                unlocked++;

            int prereqCount = slot.prerequisiteSkillSlots != null ? slot.prerequisiteSkillSlots.Count : 0;

            if (prereqCount > 0)
                withPrereq++;

            string skill = slot.skillSO != null ? slot.skillSO.skillName : "（无技能）";
            sb.Append($"\n  #{i} {skill} 前置={prereqCount} 解锁={slot.isUnclocked} 等级={slot.currentLevel}");
        }

        sb.Append($"\n  合计 {skillSlots.Length} 个节点，开局已解锁 {unlocked} 个，带前置的 {withPrereq} 个");

        Debug.Log(sb.ToString());
    }

    private void CheckAvailablePoints(SkillSlot slot)
    {
        if (slot == null)
            return;

        // 同一帧只处理一次点击。万一存在重复绑定，也不会出现「点一下加好几级」。
        if (Time.frameCount == lastClickFrame)
            return;

        if (availablePoints <= 0)
            return;

        lastClickFrame = Time.frameCount;
        slot.TryUpgradeSkill();
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

        int unlockedTotal = 0;
        bool unlockedAny;

        do
        {
            unlockedAny = false;

            for (int i = 0; i < skillSlots.Length; i++)
            {
                SkillSlot slot = skillSlots[i];

                if (slot == null || slot.isUnclocked)
                    continue;

                if (!slot.CanUnclockedSkill())
                    continue;

                slot.UnLock();
                unlockedAny = true;
                unlockedTotal++;

                if (logSkillTree)
                {
                    string skill = slot.skillSO != null ? slot.skillSO.skillName : "（无技能）";
                    Debug.Log($"[技能树] 解锁 #{i} {slot.name}（{skill}）");
                }
            }
        }
        while (unlockedAny);

        if (logSkillTree && unlockedTotal > 0)
            Debug.Log($"[技能树] 本轮共解锁 {unlockedTotal} 个节点。");
    }

    public void UpdateAbilityPoints(int amount)
    {
        availablePoints = Mathf.Max(0, availablePoints + amount);

        if (pointsText != null)
            pointsText.text = "技能点:" + availablePoints;
    }
}
