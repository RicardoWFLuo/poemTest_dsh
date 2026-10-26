using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueSO", menuName = "Dialogue/DialogueNode")]
public class DialogueSO : ScriptableObject, IIdentifiedAsset
{
    [Header("存档标识（自动填充，勿手改）")]
    [Tooltip("存档与资源反查用的稳定 ID。由菜单 Tools → 诗词数据 → 填充资源 ID 自动生成；填写后不要再改，否则老存档会失效。")]
    public string id;

    public string Id => id;

    public DialogueLine[] lines;
    public DialogueOption[] options;

    [Header("Quest Offer (Optional)")]
    public QuestSO offerQuestOnEnd;

    [Header("Completed Quest Requirement (Options)")]
    public QuestSO[] requiredCompletedQuests;

    [Header("Quest Turn-In (Optional)")]
    public QuestSO turnInQuestOnEnd;

    [Header("Conditional Requirements (Options)")]
    public ActorSO[] requiredNPCs;
    public LocationSO[] requireLocations;
    public ItemSO[] requireItems;

    [Header("Control Flags")]
    public bool removeAfterPlay;
    public List<DialogueSO> removeTheseOnPlay;

    [Header("Choice Question (Optional)")]
    public bool isQuestion;
    [TextArea(3, 5)] public string questionText;
    public string[] questionChoices = new string[4];
    public int correctChoiceIndex;
    public ItemSO rewardItem;
    public int rewardQuantity = 1;

    [Header("Result Lines")]
    public int correctResultLineIndex = -1;
    public int wrongResultLineIndex = -1;

    public bool IsConditionMet()
    {
        if (GameManager.Instance == null)
            return false;

        if (requiredNPCs != null && requiredNPCs.Length > 0)
        {
            if (GameManager.Instance.DialogueHistoryTracker == null)
                return false;

            foreach (var npc in requiredNPCs)
            {
                if (npc != null && !GameManager.Instance.DialogueHistoryTracker.HasSpokenWith(npc))
                {
                    return false;
                }
            }
        }

        if (requireLocations != null && requireLocations.Length > 0)
        {
            if (GameManager.Instance.LocationHistoryTracker == null)
                return false;

            foreach (var location in requireLocations)
            {
                if (location != null && !GameManager.Instance.LocationHistoryTracker.HasVisited(location))
                {
                    return false;
                }
            }
        }

        if (requireItems != null && requireItems.Length > 0)
        {
            if (InventoryManager.Instance == null)
                return false;

            foreach (var item in requireItems)
            {
                if (item != null && !InventoryManager.Instance.HasItem(item))
                {
                    return false;
                }
            }
        }

        if (requiredCompletedQuests != null && requiredCompletedQuests.Length > 0)
        {
            if (GameManager.Instance.QuestManager == null)
                return false;

            foreach (var quest in requiredCompletedQuests)
            {
                if (quest != null && !GameManager.Instance.QuestManager.IsQuestComplete(quest))
                {
                    return false;
                }
            }
        }

        return true;
    }
}

[System.Serializable]
public class DialogueLine
{
    public ActorSO speaker;

    [TextArea(3,5)]
    public string text;

    public bool usePoemContent;
    public string poemLineIndices;
}

[System.Serializable]
public class DialogueOption
{
    public string optionText;
    public DialogueSO nextDialogue;
    public QuestSO offerQuest;
}
