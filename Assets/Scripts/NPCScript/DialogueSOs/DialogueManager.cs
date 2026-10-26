using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    [Header("UI References")]
    public CanvasGroup canvasGroup;
    public Image portrait;
    public TMP_Text actorName;
    public TMP_Text dialogueText;
    public Button[] choiceButtons;

    public bool isDialogueActive;

    private DialogueSO currentDialogue;
    private int dialogueIndex;

    private float lastDialogueEndTime;
    private float dialogueCoolDown = .1f;

    private NPCTalk currentNPC;

    // 标记是否已经回答了问题
    private bool hasAnswered = false;

    private void Awake()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        ClearChoices();
    }

    public bool CanStartDialogue()
    {
        return (Time.unscaledTime - lastDialogueEndTime >= dialogueCoolDown);
    }

    public void StartDialogue(DialogueSO dialogueSO, NPCTalk npc = null)
    {
        // 历史版本没有判空：NPCTalk 在没有可用对话时会传入 null，
        // 随后 ShowDialogue 立刻抛 NullReferenceException。
        if (dialogueSO == null)
        {
            EndDialogue();
            return;
        }

        currentDialogue = dialogueSO;
        currentNPC = npc;
        dialogueIndex = 0;
        isDialogueActive = true;
        hasAnswered = false;
        ShowDialogue();
    }

    public void AdvanceDialogue()
    {
        if (!isDialogueActive || currentDialogue == null)
        {
            EndDialogue();
            return;
        }

        // 如果已经回答过问题，直接结束
        if (hasAnswered)
        {
            EndDialogue();
            return;
        }

        // 如果是问题对话，只播放到问题之前
        if (currentDialogue.isQuestion && dialogueIndex >= GetPreQuestionLineCount())
        {
            ShowChoices();
            return;
        }

        if (currentDialogue.lines != null && dialogueIndex < currentDialogue.lines.Length)
        {
            ShowDialogue();
        }
        else
        {
            ShowChoices();
        }
    }

    // 获取问题之前的对话行数
    private int GetPreQuestionLineCount()
    {
        // 取正确和错误语句中较小的索引作为问题前对话数量
        int minResultIndex = Mathf.Min(
            currentDialogue.correctResultLineIndex >= 0 ? currentDialogue.correctResultLineIndex : int.MaxValue,
            currentDialogue.wrongResultLineIndex >= 0 ? currentDialogue.wrongResultLineIndex : int.MaxValue
        );
        int lineCount = currentDialogue.lines != null ? currentDialogue.lines.Length : 0;
        return minResultIndex != int.MaxValue ? minResultIndex : lineCount;
    }

    private void ShowDialogue()
    {
        if (currentDialogue == null || currentDialogue.lines == null ||
            dialogueIndex >= currentDialogue.lines.Length)
        {
            ShowChoices();
            return;
        }

        DialogueLine line = currentDialogue.lines[dialogueIndex];
        dialogueIndex++;

        ApplyLine(line);
    }

    /// <summary>把一行对话显示到 UI 上（说话人可能为空，需要分别判空）。</summary>
    private void ApplyLine(DialogueLine line)
    {
        if (line == null)
            return;

        if (line.speaker != null)
        {
            // 只有记录到有效的说话人才写进对话历史，否则 HasSpokenWith(null) 永远为假
            if (GameManager.Instance != null && GameManager.Instance.DialogueHistoryTracker != null)
            {
                GameManager.Instance.DialogueHistoryTracker.RecordNPC(line.speaker);
            }

            if (portrait != null)
                portrait.sprite = line.speaker.portrait;
            if (actorName != null)
                actorName.text = line.speaker.actorName;
        }

        if (dialogueText != null)
            dialogueText.text = line.text;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    private void ShowChoices()
    {
        if (currentDialogue == null)
        {
            EndDialogue();
            return;
        }

        ClearChoices();

        if (currentDialogue.isQuestion && !hasAnswered)
        {
            ShowQuestionChoices();
            return;
        }

        if (currentDialogue.options != null && currentDialogue.options.Length > 0)
        {
            int shown = 0;
            for (int i = 0; i < currentDialogue.options.Length && i < choiceButtons.Length; i++)
            {
                var option = currentDialogue.options[i];
                var nextDialogue = option.nextDialogue;

                if (SetChoiceButton(i, option.optionText, () => ChooseOption(nextDialogue)))
                    shown++;
            }

            if (shown > 0)
                SelectFirstChoice();
            else
                ShowEndButton();
        }
        else
        {
            if (currentDialogue.turnInQuestOnEnd != null &&
                GameManager.Instance != null && GameManager.Instance.QuestManager != null &&
                GameManager.Instance.QuestManager.IsQuestComplete(currentDialogue.turnInQuestOnEnd))
            {
                QuestEvents.OnQuestTurnInRequested?.Invoke(currentDialogue.turnInQuestOnEnd);
                EndDialogue();
            }
            else if (currentDialogue.offerQuestOnEnd != null)
            {
                EndDialogue();
                QuestEvents.OnQuestOfferRequested?.Invoke(currentDialogue.offerQuestOnEnd);
            }
            else
            {
                ShowEndButton();
            }
        }
    }

    private void ShowQuestionChoices()
    {
        if (dialogueText != null)
            dialogueText.text = currentDialogue.questionText;

        int shown = QuestionChoiceCount();
        for (int i = 0; i < shown; i++)
        {
            int index = i;
            SetChoiceButton(i, currentDialogue.questionChoices[i], () => OnQuestionChoiceSelected(index));
        }

        if (shown > 0)
            SelectFirstChoice();
        else
            ShowEndButton();
    }

    /// <summary>选项数量同时受 questionChoices 与 choiceButtons 长度限制，避免越界。</summary>
    private int QuestionChoiceCount()
    {
        if (currentDialogue.questionChoices == null)
            return 0;

        return Mathf.Min(currentDialogue.questionChoices.Length, choiceButtons.Length);
    }

    /// <summary>
    /// 统一的按钮设置入口：越界或按钮为空时返回 false，绝不抛异常。
    /// 历史版本直接写 choiceButtons[i]，选项比按钮多时会 IndexOutOfRangeException。
    /// </summary>
    private bool SetChoiceButton(int index, string text, UnityAction onClick)
    {
        if (choiceButtons == null || index < 0 || index >= choiceButtons.Length)
            return false;

        Button button = choiceButtons[index];
        if (button == null)
            return false;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = text;

        button.onClick.RemoveAllListeners();
        if (onClick != null)
            button.onClick.AddListener(onClick);
        button.gameObject.SetActive(true);
        return true;
    }

    private void SelectFirstChoice()
    {
        if (EventSystem.current == null || choiceButtons == null || choiceButtons.Length == 0)
            return;

        if (choiceButtons[0] != null)
            EventSystem.current.SetSelectedGameObject(choiceButtons[0].gameObject);
    }

    private void OnQuestionChoiceSelected(int index)
    {
        if (currentDialogue == null)
        {
            EndDialogue();
            return;
        }

        ClearChoices();
        hasAnswered = true;

        if (index == currentDialogue.correctChoiceIndex)
        {
            if (currentDialogue.rewardItem != null && currentDialogue.rewardQuantity > 0 &&
                InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddItem(currentDialogue.rewardItem, currentDialogue.rewardQuantity);
                Debug.Log($"答对了！获得奖励: {currentDialogue.rewardItem.itemName} x{currentDialogue.rewardQuantity}");
            }

            if (currentNPC != null)
            {
                currentNPC.MarkCurrentConversationForRemoval();
            }

            PlayResultLine(currentDialogue.correctResultLineIndex);
        }
        else
        {
            // 答错时清掉已答标记，让玩家可以再次挑战这道题
            hasAnswered = false;
            PlayResultLine(currentDialogue.wrongResultLineIndex);
        }
    }

    private void PlayResultLine(int lineIndex)
    {
        if (currentDialogue != null && currentDialogue.lines != null &&
            lineIndex >= 0 && lineIndex < currentDialogue.lines.Length)
        {
            ApplyLine(currentDialogue.lines[lineIndex]);
            ShowEndButton();
        }
        else
        {
            EndDialogue();
        }
    }

    private void ShowEndButton()
    {
        ClearChoices();

        if (SetChoiceButton(0, "结束", EndDialogue))
            SelectFirstChoice();
    }

    private void ChooseOption(DialogueSO dialogueSO)
    {
        if (dialogueSO == null)
        {
            EndDialogue();
        }
        else
        {
            ClearChoices();
            StartDialogue(dialogueSO, currentNPC);
        }
    }

    private void EndDialogue()
    {
        dialogueIndex = 0;
        isDialogueActive = false;
        hasAnswered = false;
        currentDialogue = null;

        if (currentNPC != null)
        {
            currentNPC.RemoveMarkedConversation();
        }

        currentNPC = null;
        ClearChoices();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        lastDialogueEndTime = Time.unscaledTime;
    }

    private void ClearChoices()
    {
        if (choiceButtons == null)
            return;

        foreach (var button in choiceButtons)
        {
            if (button == null)
                continue;

            button.gameObject.SetActive(false);
            button.onClick.RemoveAllListeners();
        }
    }
}
