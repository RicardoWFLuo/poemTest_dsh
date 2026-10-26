using System;
using System.Collections.Generic;

/// <summary>
/// 存档数据（纯数据，不含任何逻辑）。
///
/// 设计要点：
///  · 所有对 ScriptableObject 的引用都存成「字符串 ID」，不存对象引用 —— 对象引用没法写进 JSON；
///  · 只有 List，没有 Dictionary —— Unity 自带的 JsonUtility 不支持字典序列化；
///  · 带 version 字段 —— 以后加字段时可以按版本做迁移，不会一改就废档。
/// </summary>
[Serializable]
public class SaveData
{
    /// <summary>当前存档格式版本。改了 SaveData 结构就把它 +1，并在 SaveManager 里加迁移。</summary>
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public string savedAtUtc;
    public string sceneName;

    public float playerX;
    public float playerY;
    public float playerZ;

    // ---- 背包与货币 ----
    public int gold;
    public List<ItemEntry> items = new List<ItemEntry>();

    // ---- 属性 ----
    public int currentHealth;
    public int maxHealth;
    public int damage;
    public float speed;

    // ---- 经验 ----
    public int level;
    public int currentExp;
    public int expToLevel;

    // ---- 技能树 ----
    public int skillPoints;
    public List<SkillEntry> skills = new List<SkillEntry>();

    // ---- 任务 ----
    public List<string> acceptedQuestIds = new List<string>();
    public List<string> completedQuestIds = new List<string>();

    // ---- 世界状态 ----
    /// <summary>已经说过话的角色（对应 DialogueHistoryTracker）。</summary>
    public List<string> spokenActorIds = new List<string>();

    /// <summary>已经去过的地方（对应 LocationHistoryTracker）。</summary>
    public List<string> visitedLocationIds = new List<string>();

    /// <summary>已经考过的诗（对应 PoemExamNPC 的通过记录）。</summary>
    public List<string> passedPoemIds = new List<string>();

    /// <summary>
    /// 已经被消耗掉的对话。
    /// 这个字段是防刷奖励的关键：任务发放后对话会从 NPC 的列表里被移除，
    /// 若不记录，读档后对话会全部复活，同一个任务的奖励可以被无限领。
    /// </summary>
    public List<string> consumedDialogueIds = new List<string>();
}

/// <summary>背包中的一个格子。</summary>
[Serializable]
public class ItemEntry
{
    public int slotIndex;
    public string itemId;
    public int quantity;
}

/// <summary>技能树中的一个节点。用数组下标定位（预制体里节点顺序是稳定的）。</summary>
[Serializable]
public class SkillEntry
{
    public int slotIndex;
    public int currentLevel;
    public bool unlocked;
}
