using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 诗人 NPC 的对话交互。与 NPCTalk 的区别是：这里的对话文本里可以插入诗词内容，
/// 例如 {PoemTitle}、{Poet}、{PoemContent}、{SelectedLines}。
/// </summary>
public class PoemTalk : MonoBehaviour
{
    private Rigidbody2D rb;
    private Animator anim;
    public Animator interactAnim;

    public List<DialogueSO> conversations;
    public DialogueSO currentConversation;

    [Header("Poem Settings")]
    public PoemSO poemData;
    public ActorSO currentActor;

    private bool playerInRange = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            if (interactAnim != null)
                interactAnim.Play("OpenIcon");
        }
    }

    private void OnDisable()
    {
        playerInRange = false;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (interactAnim != null)
                interactAnim.Play("CloseIcon");
        }
    }

    private void Start()
    {
        QuestEvents.OnQuestAccepted += OnQuestAcceptedRemoveOfferings;
    }

    private void OnDestroy()
    {
        QuestEvents.OnQuestAccepted -= OnQuestAcceptedRemoveOfferings;
    }

    private void Update()
    {
        if (!playerInRange)
            return;

        if (MobileInputManager.Instance == null || GameManager.Instance == null)
            return;

        if (!MobileInputManager.Instance.GetInteractDown())
            return;

        DialogueManager dialogueManager = GameManager.Instance.DialogueManager;
        if (dialogueManager == null)
            return;

        if (dialogueManager.isDialogueActive)
        {
            dialogueManager.AdvanceDialogue();
            return;
        }

        if (!dialogueManager.CanStartDialogue())
            return;

        // 没有任何对话满足条件时不要启动对话，避免把 null 传下去
        if (!CheckForNewConversation())
            return;

        dialogueManager.StartDialogue(currentConversation);
    }

    private string ProcessDialogueText(string originalText, DialogueLine line)
    {
        if (!line.usePoemContent || poemData == null)
            return originalText;

        if (string.IsNullOrEmpty(originalText))
            return originalText;

        string processedText = originalText;

        processedText = processedText.Replace("{PoemTitle}", poemData.poemTitle);
        // 作者优先取诗词数据本身，其次才用 NPC 的 ActorSO 名称
        string poetName = poemData.poet;
        if (string.IsNullOrEmpty(poetName) && currentActor != null)
            poetName = currentActor.actorName;
        processedText = processedText.Replace("{Poet}", poetName);
        processedText = processedText.Replace("{PoemContent}", poemData.poemContent);

        if (!string.IsNullOrEmpty(line.poemLineIndices))
        {
            // 同时兼容 \n 与 \r\n 分行
            string[] lines = poemData.poemContent.Replace("\r\n", "\n").Split('\n');
            string[] indices = line.poemLineIndices.Split(',');

            string selectedLines = "";
            foreach (string indexStr in indices)
            {
                if (int.TryParse(indexStr.Trim(), out int index) && index > 0 && index <= lines.Length)
                {
                    selectedLines += lines[index - 1];
                }
            }

            processedText = processedText.Replace("{SelectedLines}", selectedLines.Trim());
        }

        return processedText;
    }

    /// <summary>
    /// 找出当前满足条件的第一条对话，返回 false 表示无可用对话。
    /// </summary>
    private bool CheckForNewConversation()
    {
        currentConversation = null;

        for (int i = 0; i < conversations.Count; i++)
        {
            var convo = conversations[i];
            if (convo == null || !convo.IsConditionMet())
                continue;

            currentConversation = Instantiate(convo);

            if (poemData != null && currentConversation.lines != null)
            {
                foreach (var line in currentConversation.lines)
                {
                    line.text = ProcessDialogueText(line.text, line);
                }
            }

            if (convo.removeAfterPlay)
            {
                WorldState.MarkDialogueConsumed(convo);
                conversations.RemoveAt(i);
            }

            if (convo.removeTheseOnPlay != null && convo.removeTheseOnPlay.Count > 0)
            {
                foreach (var toRemove in convo.removeTheseOnPlay)
                {
                    WorldState.MarkDialogueConsumed(toRemove);
                    conversations.Remove(toRemove);
                }
            }

            return true;
        }

        return false;
    }

    private void OnQuestAcceptedRemoveOfferings(QuestSO acceptedQuest)
    {
        for (int i = conversations.Count - 1; i >= 0; i--)
        {
            var convo = conversations[i];
            if (convo == null)
                continue;

            if (convo.offerQuestOnEnd == acceptedQuest)
            {
                WorldState.MarkDialogueConsumed(convo);
                conversations.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 把「已被消耗」的对话从列表里剔除（读档与切场景后调用）。
    /// </summary>
    public void RemoveConsumedDialogues()
    {
        if (conversations == null)
            return;

        for (int i = conversations.Count - 1; i >= 0; i--)
        {
            var convo = conversations[i];
            if (convo != null && WorldState.IsDialogueConsumed(convo.Id))
                conversations.RemoveAt(i);
        }
    }
}
