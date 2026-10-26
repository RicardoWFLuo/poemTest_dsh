using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 运行时「被改动过的世界状态」。
///
/// 目前只记录一件事：哪些对话已经被消耗掉。
/// 之所以单独抽一个静态类而不塞进存档类：NPC 在对话流程里随时会移除对话，
/// 它需要立刻记下这件事；而存档类只在存/读那一刻才存在。
///
/// 注意：这是内存中的状态，靠 SaveManager 落盘与恢复。
/// </summary>
public static class WorldState
{
    private static readonly HashSet<string> consumedDialogueIds = new HashSet<string>();

    /// <summary>已消耗对话的 ID 集合（只读用途）。</summary>
    public static IEnumerable<string> ConsumedDialogueIds => consumedDialogueIds;

    public static int ConsumedDialogueCount => consumedDialogueIds.Count;

    /// <summary>标记一条对话已被消耗。对话没有 ID 时会静默跳过并告警。</summary>
    public static void MarkDialogueConsumed(DialogueSO dialogue)
    {
        if (dialogue == null)
            return;

        string id = dialogue.Id;

        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning($"[WorldState] 对话资源「{dialogue.name}」没有 ID，无法记录消耗状态。" +
                             "请执行菜单：Tools → 诗词数据 → 填充资源 ID。");
            return;
        }

        consumedDialogueIds.Add(id);
    }

    public static bool IsDialogueConsumed(string id)
    {
        return !string.IsNullOrEmpty(id) && consumedDialogueIds.Contains(id);
    }

    /// <summary>用存档里的数据整体替换当前状态（读档时调用）。</summary>
    public static void SetConsumed(IEnumerable<string> ids)
    {
        consumedDialogueIds.Clear();

        if (ids == null)
            return;

        foreach (string id in ids)
        {
            if (!string.IsNullOrEmpty(id))
                consumedDialogueIds.Add(id);
        }
    }

    /// <summary>清空（新游戏时调用）。</summary>
    public static void Clear()
    {
        consumedDialogueIds.Clear();
    }

    /// <summary>关闭 Unity 的域重载时静态字段不会自动重置，这里在每次进入播放时清一次。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        consumedDialogueIds.Clear();
    }
}
