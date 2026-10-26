using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全项目可被存档引用的资源总表。
///
/// 为什么需要它：运行时想把「存档里的 id 字符串」还原成具体资源，
/// 需要一个可以按类型遍历的入口。Unity 没有内置的反查机制，
/// 所以这里用一个放在 Resources 下的清单资源承载。
///
/// 该文件由菜单 Tools/诗词数据/一键初始化 自动生成与填充，不要手工维护。
/// </summary>
[CreateAssetMenu(fileName = "GameDatabase", menuName = "诗词数据/资源索引 GameDatabase")]
public class GameDatabase : ScriptableObject
{
    public List<PoemSO> poems = new List<PoemSO>();
    public List<ItemSO> items = new List<ItemSO>();
    public List<ActorSO> actors = new List<ActorSO>();
    public List<DialogueSO> dialogues = new List<DialogueSO>();
    public List<QuestSO> quests = new List<QuestSO>();
    public List<LocationSO> locations = new List<LocationSO>();
    public List<SkillSO> skills = new List<SkillSO>();

    /// <summary>把所有清单合并成一个可枚举序列，供索引构建使用。</summary>
    public IEnumerable<ScriptableObject> EnumerateAll()
    {
        foreach (var x in poems) if (x != null) yield return x;
        foreach (var x in items) if (x != null) yield return x;
        foreach (var x in actors) if (x != null) yield return x;
        foreach (var x in dialogues) if (x != null) yield return x;
        foreach (var x in quests) if (x != null) yield return x;
        foreach (var x in locations) if (x != null) yield return x;
        foreach (var x in skills) if (x != null) yield return x;
    }
}
