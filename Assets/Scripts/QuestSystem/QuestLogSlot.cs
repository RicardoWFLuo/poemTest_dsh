using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class QuestLogSlot : MonoBehaviour
{
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private TMP_Text questLevelText;

    public QuestSO currentQuest;

    public QuestLogUI questLogUI;

    public void SetQuest(QuestSO questSO)
    {
        if (questSO == null)
        {
            ClearSlot();
            return;
        }

        currentQuest = questSO;

        if (questNameText != null)
            questNameText.text = questSO.questName;
        if (questLevelText != null)
            questLevelText.text = "等级:" + questSO.questLevel;

        gameObject.SetActive(true);
    }

    public void ClearSlot()
    {
        currentQuest = null;
        gameObject.SetActive(false);
    }

    public void OnSlotClicked()
    {
        if (questLogUI == null || currentQuest == null)
            return;

        questLogUI.HandleQuestClicked(currentQuest);
    }
}
