using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCTalk : MonoBehaviour
{
    private Rigidbody2D rb;
    private Animator anim;
    public Animator interactAnim;

    public List<DialogueSO> conversations;
    public DialogueSO currentConversation;

    private DialogueSO markedForRemoval;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.isKinematic = true;
        }

        if (anim != null)
        {
            anim.Play("PawnIdle");
        }

        // 进入 Talk 状态时先隐藏交互气泡，玩家靠近后由 NPC/触发器逻辑打开
        if (interactAnim != null)
        {
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

    private void OnDisable()
    {
        if (interactAnim != null)
        {
            interactAnim.Play("CloseIcon");
        }

        if (rb != null)
        {
            rb.isKinematic = false;
        }
    }



    public void MarkCurrentConversationForRemoval()
    {
        markedForRemoval = currentConversation;
    }

    public void RemoveMarkedConversation()
    {
        if (markedForRemoval != null)
        {
            conversations.Remove(markedForRemoval);
            markedForRemoval = null;
            currentConversation = null;
        }
    }

    /// <summary>
    /// 找出当前满足条件的第一条对话。
    /// 返回 false 表示没有可用的对话（此时 currentConversation 一定为 null）。
    /// 历史版本在没有匹配项时会把 currentConversation 保留为上一次的值，
    /// 导致玩家重复触发旧对话，或者把 null 传进 DialogueManager 造成空引用崩溃。
    /// </summary>
    private bool CheckForNewConversation()
    {
        currentConversation = null;

        for (int i = 0; i < conversations.Count; i++)
        {
            var convo = conversations[i];
            if (convo == null || !convo.IsConditionMet())
                continue;

            currentConversation = convo;

            if (convo.removeAfterPlay)
                conversations.RemoveAt(i);

            if (convo.removeTheseOnPlay != null && convo.removeTheseOnPlay.Count > 0)
            {
                foreach (var toRemove in convo.removeTheseOnPlay)
                {
                    conversations.Remove(toRemove);
                }
            }

            return true;
        }

        return false;
    }

    private void Update()
    {
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

        dialogueManager.StartDialogue(currentConversation, this);
    }

    private void OnQuestAcceptedRemoveOfferings(QuestSO acceptedQuest)
    {
        for (int i = conversations.Count - 1; i >= 0; i--)
        {
            var convo = conversations[i];
            if (convo == null)
                continue;

            if (convo.offerQuestOnEnd == acceptedQuest)
                conversations.RemoveAt(i);
        }
    }
}
