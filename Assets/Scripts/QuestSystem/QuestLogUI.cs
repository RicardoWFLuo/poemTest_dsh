using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class QuestLogUI : MonoBehaviour
{
    [SerializeField] private QuestManager questManager;

    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private TMP_Text questDescriptionText;
    [SerializeField] private QuestObjectiveSlot[] objectiveSlots;
    [SerializeField] private QuestRewardSlot[] rewardSlots;

    private QuestSO questSO;

    [SerializeField] private QuestSO noAvailableQuestSO;
    [SerializeField] private QuestLogSlot[] questSlots;

    [SerializeField] private CanvasGroup questCanvas;

    [SerializeField] private CanvasGroup acceptCanvasGroup;
    [SerializeField] private CanvasGroup declineCanvasGroup;
    [SerializeField] private CanvasGroup completeCanvasGroup;

    private void OnEnable()
    {
        QuestEvents.OnQuestOfferRequested += ShowQuestOffer;
        QuestEvents.OnQuestTurnInRequested += ShowQuestTurnIn;
    }

    private void OnDisable()
    {
        QuestEvents.OnQuestOfferRequested -= ShowQuestOffer;
        QuestEvents.OnQuestTurnInRequested -= ShowQuestTurnIn;
    }

    #region show Quest Methods
    public void ShowQuestOffer(QuestSO incomingQuestSO)
    {
        if (incomingQuestSO == null || questManager == null)
            return;

        if (questManager.IsQuestAccepted(incomingQuestSO) ||
            questManager.GetCompleteQuest(incomingQuestSO))
        {
            questSO = noAvailableQuestSO;
            SetCanvasState(acceptCanvasGroup, false);
            SetCanvasState(declineCanvasGroup, true);
            SetCanvasState(completeCanvasGroup, false);
        }
        else
        {
            questSO = incomingQuestSO;
            SetCanvasState(acceptCanvasGroup, true);
            SetCanvasState(declineCanvasGroup, true);
            SetCanvasState(completeCanvasGroup, false);
        }

        HandleQuestClicked(questSO);
        SetCanvasState(questCanvas, true);
    }

    public void ShowQuestTurnIn(QuestSO incomingQuestSO)
    {
        if (incomingQuestSO == null)
            return;

        questSO = incomingQuestSO;
        HandleQuestClicked(questSO);
        SetCanvasState(completeCanvasGroup, true);
        SetCanvasState(acceptCanvasGroup, false);
        SetCanvasState(declineCanvasGroup, false);
        SetCanvasState(questCanvas, true);
    }

    #endregion

    #region On Button Clicked Methods
    public void OnAcceptQuestClick()
    {
        if (questSO == null || questManager == null)
            return;

        questManager.AcceptQuest(questSO);
        QuestEvents.OnQuestAccepted?.Invoke(questSO);

        SetCanvasState(completeCanvasGroup, false);
        SetCanvasState(acceptCanvasGroup, false);
        SetCanvasState(declineCanvasGroup, false);
        RefreshQuestList();
        HandleQuestClicked(noAvailableQuestSO);
    }

    public void OnDeclineQuestClicked()
    {
        SetCanvasState(questCanvas, false);
    }

    public void OnCompleteQuestClicked()
    {
        if (questSO == null || questManager == null)
            return;

        questManager.CompleteQuest(questSO);

        RefreshQuestList();
        HandleQuestClicked(noAvailableQuestSO);
        SetCanvasState(completeCanvasGroup, false);
    }
    #endregion



    private void SetCanvasState(CanvasGroup group, bool activate)
    {
        if (group == null)
            return;

        group.alpha = activate ? 1 : 0;
        group.blocksRaycasts = activate;
        group.interactable = activate;
    }

    public void RefreshQuestList()
    {
        if (questManager == null || questSlots == null)
            return;

        List<QuestSO> activeQuests = questManager.GetActiveQuests();

        for (int i = 0; i < questSlots.Length; i++)
        {
            if (questSlots[i] == null)
                continue;

            if (i < activeQuests.Count)
            {
                questSlots[i].SetQuest(activeQuests[i]);
            }
            else
            {
                questSlots[i].ClearSlot();
            }
        }
    }

    public void HandleQuestClicked(QuestSO questSO)
    {
        this.questSO = questSO;

        // 历史版本直接写 questSO.questName，
        // 当没有可接任务（noAvailableQuestSO 未配置）时会空引用崩溃。
        if (questSO == null)
        {
            if (questNameText != null) questNameText.text = "";
            if (questDescriptionText != null) questDescriptionText.text = "";
            ClearObjectives();
            ClearRewards();
            return;
        }

        if (questNameText != null)
            questNameText.text = questSO.questName;
        if (questDescriptionText != null)
            questDescriptionText.text = questSO.questDescription;

        DisplayObjective();
        DisplayRewards();
    }

    private void DisplayObjective()
    {
        if (objectiveSlots == null)
            return;

        int objectiveCount = questSO != null && questSO.objectives != null ? questSO.objectives.Count : 0;

        for (int i = 0; i < objectiveSlots.Length; i++)
        {
            if (objectiveSlots[i] == null)
                continue;

            if (questSO != null && questManager != null && i < objectiveCount)
            {
                var objective = questSO.objectives[i];
                questManager.UpdateObjectiveProgress(questSO, objective);

                int currentAmount = questManager.GetCurrentAmount(questSO, objective);
                string progress = questManager.GetProgressText(questSO, objective);
                bool isComplete = objective != null && currentAmount >= objective.requiredAmount;

                objectiveSlots[i].gameObject.SetActive(true);
                objectiveSlots[i].RefreshObjectives(objective != null ? objective.description : "", progress, isComplete);
            }
            else
            {
                objectiveSlots[i].gameObject.SetActive(false);
            }
        }
    }

    private void ClearObjectives()
    {
        if (objectiveSlots == null)
            return;

        foreach (var slot in objectiveSlots)
        {
            if (slot != null)
                slot.gameObject.SetActive(false);
        }
    }

    private void ClearRewards()
    {
        if (rewardSlots == null)
            return;

        foreach (var slot in rewardSlots)
        {
            if (slot != null)
                slot.gameObject.SetActive(false);
        }
    }

    private void DisplayRewards()
    {
        if (rewardSlots == null)
            return;

        int rewardCount = questSO != null && questSO.rewards != null ? questSO.rewards.Count : 0;

        for (int i = 0; i < rewardSlots.Length; i++)
        {
            if (rewardSlots[i] == null)
                continue;

            if (questSO != null && i < rewardCount)
            {
                var reward = questSO.rewards[i];

                if (reward == null || reward.itemSO == null)
                {
                    rewardSlots[i].gameObject.SetActive(false);
                    continue;
                }

                rewardSlots[i].DisplayReward(reward.itemSO.icon, reward.quantity);
                rewardSlots[i].gameObject.SetActive(true);
            }
            else
            {
                rewardSlots[i].gameObject.SetActive(false);
            }
        }
    }
}
