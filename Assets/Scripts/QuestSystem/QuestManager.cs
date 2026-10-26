using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    private Dictionary<QuestSO, Dictionary<QuestObjective, int>> questProgress = new();
    private List<QuestSO> completedQuests = new();

    private void OnEnable()
    {
        QuestEvents.IsQuestComplete += IsQuestComplete;
    }
    private void OnDisable()
    {
        QuestEvents.IsQuestComplete -= IsQuestComplete;
    }

    #region Quest Accept Logic
    public bool IsQuestAccepted(QuestSO questSO)
    {
        if (questSO == null)
            return false;

        return questProgress.ContainsKey(questSO);
    }

    public List<QuestSO> GetActiveQuests()
    {
        return new List<QuestSO>(questProgress.Keys);
    }

    public void AcceptQuest(QuestSO questSO)
    {
        if (questSO == null || questProgress.ContainsKey(questSO))
            return;

        questProgress[questSO] = new Dictionary<QuestObjective, int>();

        if (questSO.objectives == null)
            return;

        foreach (var objective in questSO.objectives)
        {
            UpdateObjectiveProgress(questSO, objective);
        }
    }
    #endregion

    #region Quest Complete Logic
    public bool IsQuestComplete(QuestSO questSO)
    {
        if (questSO == null || questSO.objectives == null)
            return false;

        if (!questProgress.TryGetValue(questSO, out var progressDict))
        {
            return false;
        }

        foreach (var objective in questSO.objectives)
        {
            UpdateObjectiveProgress(questSO, objective);
        }

        foreach (var objective in questSO.objectives)
        {
            if (objective == null)
                continue;

            if (!progressDict.TryGetValue(objective, out int amount) || amount < objective.requiredAmount)
            {
                return false;
            }
        }

        return true;
    }

    public void CompleteQuest(QuestSO questSO)
    {
        if (questSO == null)
            return;

        questProgress.Remove(questSO);

        if (!completedQuests.Contains(questSO))
            completedQuests.Add(questSO);

        if (InventoryManager.Instance == null)
            return;

        // 扣除任务物品
        if (questSO.objectives != null)
        {
            foreach (var objective in questSO.objectives)
            {
                if (objective != null && objective.targetItem != null && objective.requiredAmount > 0)
                {
                    InventoryManager.Instance.RemoveItem(objective.targetItem, objective.requiredAmount);
                }
            }
        }

        // 发放奖励
        if (questSO.rewards != null)
        {
            foreach (var reward in questSO.rewards)
            {
                if (reward != null && reward.itemSO != null && reward.quantity > 0)
                {
                    InventoryManager.Instance.AddItem(reward.itemSO, reward.quantity);
                }
            }
        }
    }

    public bool GetCompleteQuest(QuestSO questSO)
    {
        if (questSO == null)
            return false;

        return completedQuests.Contains(questSO);
    }
    #endregion

    public void UpdateObjectiveProgress(QuestSO questSO, QuestObjective objective)
    {
        if (questSO == null || objective == null)
            return;

        if (!questProgress.TryGetValue(questSO, out var progressDictionary))
        {
            return;
        }

        int newAmount = 0;

        if (objective.targetItem != null)
        {
            // 物品类目标：进度 = 背包里实际拥有的数量
            if (InventoryManager.Instance != null)
                newAmount = InventoryManager.Instance.GetItemQuantity(objective.targetItem);
        }
        else if (objective.targetLocation != null)
        {
            if (GameManager.Instance != null && GameManager.Instance.LocationHistoryTracker != null &&
                GameManager.Instance.LocationHistoryTracker.HasVisited(objective.targetLocation))
            {
                newAmount = objective.requiredAmount;
            }
        }
        else if (objective.targetNPC != null)
        {
            if (GameManager.Instance != null && GameManager.Instance.DialogueHistoryTracker != null &&
                GameManager.Instance.DialogueHistoryTracker.HasSpokenWith(objective.targetNPC))
            {
                newAmount = objective.requiredAmount;
            }
        }

        progressDictionary[objective] = newAmount;
    }

    public string GetProgressText(QuestSO questSO, QuestObjective objective)
    {
        if (objective == null)
            return string.Empty;

        int currentAmount = GetCurrentAmount(questSO, objective);

        if (currentAmount >= objective.requiredAmount)
        {
            return "已完成";
        }
        else if (objective.targetItem != null)
        {
            return $"{currentAmount}/{objective.requiredAmount}";
        }
        else
        {
            return "进度中";
        }
    }

    public int GetCurrentAmount(QuestSO questSO, QuestObjective objective)
    {
        if (questSO == null || objective == null)
            return 0;

        if (questProgress.TryGetValue(questSO, out var objectiveDictionary))
        {
            if (objectiveDictionary.TryGetValue(objective, out int amount))
            {
                return amount;
            }
        }
        return 0;
    }
}
