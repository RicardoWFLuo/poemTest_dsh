using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class QuestObjectiveSlot : MonoBehaviour
{
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text trankingText;

    public void RefreshObjectives(string description, string progressText, bool isComplete)
    {
        objectiveText.text = description;
        trankingText.text = progressText;


        Color color= isComplete ? Color.grey : Color.white;
        objectiveText.color = color;
        trankingText.color = color;
    }

}
